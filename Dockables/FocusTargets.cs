using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Core.Model;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Equipment.MyGuider.PHD2;
using Cwseo.NINA.LiveFocus.Models;

namespace Cwseo.NINA.LiveFocus.Dockables
{
    public partial class LiveFocusDockableVM
    {
        private IProfileService focusProfileService;
        private CancellationTokenSource gotoCts;
        private bool isGoingToFocusTarget;
        private bool refreshingFocusTargets;
        private bool initialFocusTargetsRequested;
        public async Task EnsureFocusTargetsLoadedAsync()
        {
            if (initialFocusTargetsRequested || disposed) return;
            initialFocusTargetsRequested = true;
            await RunGuarded("Find focus stars", RefreshFocusTargetsAsync);
        }
        private FocusStarSuggestion selectedFocusTarget;
        private double minimumFocusAltitude = 45;
        private double maximumFocusMagnitude = 4;
        public AsyncObservableCollection<FocusStarSuggestion> FocusTargets { get; } = new();
        public ICommand RefreshFocusTargetsCommand { get; private set; }
        public ICommand GotoFocusTargetCommand { get; private set; }
        public ICommand SlewFocusTargetCommand { get; private set; }
        public ICommand CancelGotoCommand { get; private set; }
        public string FocusTargetStatus { get; private set; } = "Refresh to find bright stars using the NINA profile location.";
        public bool IsGoingToFocusTarget => isGoingToFocusTarget;
        // The same profile setting as Options > Equipment > Telescope > No Sync.
        // Use a positive label here; do not keep an independent overriding value.
        public bool SyncMountOnCentering
        {
            get => !focusProfileService.ActiveProfile.TelescopeSettings.NoSync;
            set
            {
                if (disposed || !CanConfigureLive || SyncMountOnCentering == value) return;
                focusProfileService.ActiveProfile.TelescopeSettings.NoSync = !value;
                RaisePropertyChanged();
            }
        }
        private bool? lastSyncMountOnCentering;
        public double MinimumFocusAltitude
        {
            get => minimumFocusAltitude;
            set { if (!double.IsFinite(value)) return; minimumFocusAltitude = Math.Clamp(value, 15, 85); RaisePropertyChanged(); }
        }
        public double MaximumFocusMagnitude
        {
            get => maximumFocusMagnitude;
            set { if (!double.IsFinite(value)) return; maximumFocusMagnitude = Math.Clamp(value, -2, 20); RaisePropertyChanged(); }
        }
        public FocusStarSuggestion SelectedFocusTarget
        {
            get => selectedFocusTarget;
            set { selectedFocusTarget = value; RaisePropertyChanged(); RefreshGotoAvailability(); }
        }

        private void InitializeFocusTargets(IProfileService service)
        {
            focusProfileService = service;
            RefreshFocusTargetsCommand = new AsyncCommand<int>(() => RunGuarded("Find focus stars", RefreshFocusTargetsAsync), _ => !refreshingFocusTargets && !isGoingToFocusTarget);
            GotoFocusTargetCommand = new AsyncCommand<int>(() => RunGuarded("Goto focus star", GotoFocusTargetAsync), _ => CanGotoFocusTarget());
            SlewFocusTargetCommand = new AsyncCommand<int>(() => RunGuarded("Slew to focus star", SlewFocusTargetAsync), _ => GotoUnavailableReason(false) == null);
            CancelGotoCommand = new RelayCommand(_ => gotoCts?.Cancel(), _ => isGoingToFocusTarget);
        }

        private string GotoUnavailableReason(bool center)
        {
            if (disposed) return "The focus panel is closed.";
            if (isGoingToFocusTarget) return "Target movement is running. Use Cancel to stop.";
            if (SelectedFocusTarget == null) return "Select a focus star. Use Refresh if the list is empty.";
            if (TelescopeInfo?.Connected != true) return "Connect the mount.";
            if (TelescopeInfo.AtPark) return "Unpark the mount in NINA's telescope controls.";
            if (TelescopeInfo.Slewing) return "Wait for the mount slew to finish.";
            if (IsMoving) return "Wait for focus movement to finish.";
            if (IsCapturing) return "Stop live focus or wait for the current capture to finish.";
            if (center && CameraInfo?.Connected != true) return "Connect the camera for plate-solve centering.";
            if (CameraInfo?.Connected == true && !cameraMediator.IsFreeToCapture(this))
                return "The camera is in use by another NINA operation. Stop it or wait for completion.";
            return null;
        }

        public string GotoFocusTargetTooltip => GotoUnavailableReason(true) ??
            "Slew to the selected star, then plate solve and center using NINA's Plate Solving settings. " +
            "A connected guider is stopped before the slew and stays stopped for focusing. Remove the Bahtinov mask for solving.";
        public string SlewFocusTargetTooltip => GotoUnavailableReason(false) ??
            "Slew to the selected coordinates without capturing, plate solving or syncing. A connected guider is stopped first. Centering is not verified.";

        private bool CanGotoFocusTarget() => GotoUnavailableReason(true) == null;

        // Idle/disconnected guiders need no stop request. StopGuiding returning
        // false can also mean a disconnect or an already-idle state, not failure.
        private bool GuiderIsIdle()
        {
            string state = (guiderMediator.GetDevice() as IGuider)?.State;
            return state == PhdAppState.STOPPED || state == PhdAppState.LOOPING || state == PhdAppState.SELECTED;
        }

        private async Task PrepareGuiderForSlewAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (guiderMediator.GetInfo()?.Connected != true || GuiderIsIdle()) return;
            bool stopped;
            try { stopped = await guiderMediator.StopGuiding(token); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                token.ThrowIfCancellationRequested();
                if (guiderMediator.GetInfo()?.Connected != true || GuiderIsIdle()) return;
                throw new InvalidOperationException("Could not stop guiding. Stop guiding in NINA/PHD2 and retry GOTO.", e);
            }
            token.ThrowIfCancellationRequested();
            if (!stopped && guiderMediator.GetInfo()?.Connected == true && !GuiderIsIdle())
                throw new InvalidOperationException("Could not stop guiding. Stop guiding in NINA/PHD2 and retry GOTO.");
        }

        private void RefreshGotoAvailability()
        {
            if (focusProfileService != null && lastSyncMountOnCentering != SyncMountOnCentering)
            {
                lastSyncMountOnCentering = SyncMountOnCentering;
                RaisePropertyChanged(nameof(SyncMountOnCentering));
            }
            string reason = GotoUnavailableReason(true) + "|" + GotoUnavailableReason(false);
            if (reason == lastGotoUnavailableReason) return;
            lastGotoUnavailableReason = reason;
            RaisePropertyChanged(nameof(GotoFocusTargetTooltip));
            RaisePropertyChanged(nameof(SlewFocusTargetTooltip));
            CommandManager.InvalidateRequerySuggested();
        }

        private FocusStarSuggestion CalculateSuggestion(string name, Coordinates coordinates, double magnitude)
        {
            var site = focusProfileService.ActiveProfile.AstrometrySettings;
            return FocusStarPlanner.Calculate(name, coordinates, magnitude, site.Latitude, site.Longitude, site.Elevation, DateTime.UtcNow);
        }

        private bool AboveFocusHorizon(FocusStarSuggestion star)
        {
            var horizon = focusProfileService.ActiveProfile.AstrometrySettings.Horizon;
            return FocusStarPlanner.IsAboveHorizon(star, MinimumFocusAltitude, horizon?.GetAltitude(star.Azimuth) ?? 0);
        }

        private async Task<int> RefreshFocusTargetsAsync()
        {
            if (refreshingFocusTargets || disposed) return 0;
            refreshingFocusTargets = true;
            FocusTargetStatus = "Finding visible focus stars…";
            RaisePropertyChanged(nameof(FocusTargetStatus));
            try
            {
                var stars = await new DatabaseInteraction().GetBrightStars();
                if (disposed) return 0;
                var horizon = focusProfileService.ActiveProfile.AstrometrySettings.Horizon;
                var suggestions = FocusStarPlanner.SelectVisibleStars(
                    stars.Where(s => s.Coordinates != null).Select(s => CalculateSuggestion(s.Name, s.Coordinates, s.Magnitude)),
                    MinimumFocusAltitude, MaximumFocusMagnitude, azimuth => horizon?.GetAltitude(azimuth) ?? 0);
                string selectedName = SelectedFocusTarget?.Name;
                FocusTargets.Clear();
                foreach (var star in suggestions) FocusTargets.Add(star);
                SelectedFocusTarget = suggestions.FirstOrDefault(s => s.Name == selectedName) ?? suggestions.FirstOrDefault();
                var site = focusProfileService.ActiveProfile.AstrometrySettings;
                FocusTargetStatus = $"{suggestions.Count}/{stars.Count} catalogue stars · alt ≥ {MinimumFocusAltitude:F0}° · mag ≤ {MaximumFocusMagnitude:F1} · horizon +5°. " +
                    $"Site {site.Latitude:F4}, {site.Longitude:F4} · {DateTimeOffset.Now:HH:mm:ss zzz}. Check exposure saturation after GOTO.";
                RaisePropertyChanged(nameof(FocusTargetStatus));
                return suggestions.Count;
            }
            catch
            {
                FocusTargetStatus = "Could not load stars. Check the NINA profile location and retry Refresh.";
                RaisePropertyChanged(nameof(FocusTargetStatus));
                throw;
            }
            finally { refreshingFocusTargets = false; CommandManager.InvalidateRequerySuggested(); }
        }

        private Task<int> GotoFocusTargetAsync() => MoveFocusTargetAsync(true);
        private Task<int> SlewFocusTargetAsync() => MoveFocusTargetAsync(false);
        private async Task<int> MoveFocusTargetAsync(bool center)
        {
            if (GotoUnavailableReason(center) != null) return 0;
            var target = CalculateSuggestion(SelectedFocusTarget.Name, SelectedFocusTarget.Coordinates, SelectedFocusTarget.Magnitude);
            if (!AboveFocusHorizon(target)) throw new InvalidOperationException("Star is below the current altitude/horizon limit. Refresh the list.");
            if (!double.IsFinite(target.Magnitude) || target.Magnitude > MaximumFocusMagnitude)
                throw new InvalidOperationException("Star is outside the current magnitude limit. Refresh the list.");
            // Snapshot all capture/solver settings and resolve the configured solver
            // before moving the mount. These are NINA's own Center services/exports.
            FocusTargetCentering centering = null;
            ICenteringSolver solver = null;
            string cameraId = CameraInfo?.DeviceId;
            if (center)
            {
                var profile = focusProfileService.ActiveProfile;
                centering = new FocusTargetCentering(profile, cameraMediator.GetInfo(), target.Coordinates);
                solver = plateSolverFactory.GetCenteringSolver(
                    plateSolverFactory.GetPlateSolver(profile.PlateSolveSettings),
                    plateSolverFactory.GetBlindSolver(profile.PlateSolveSettings),
                    imagingMediator, telescopeMediator, filterWheelMediator, domeMediator, domeFollower);
            }
            bool guidingPrepared = false;
            void CheckDevices()
            {
                var camera = center ? cameraMediator.GetInfo() : null;
                var mount = telescopeMediator.GetInfo();
                if (disposed || (center && (camera?.Connected != true || camera.DeviceId != cameraId)) ||
                    mount?.Connected != true || mount.AtPark)
                    throw new InvalidOperationException("Camera/mount state changed during focus-star centering. GOTO stopped.");
                if (guidingPrepared && guiderMediator.GetInfo()?.Connected == true)
                {
                    string state = (guiderMediator.GetDevice() as IGuider)?.State;
                    if (state == PhdAppState.GUIDING || state == PhdAppState.CALIBRATING || state == PhdAppState.LOSTLOCK || state == PhdAppState.PAUSED)
                        throw new InvalidOperationException("Guiding resumed during focus-star centering. Stop guiding and retry GOTO.");
                }
            }
            if (center) solver.CaptureSolver = new GuardedFocusCaptureSolver(solver.CaptureSolver, CheckDevices);
            gotoCts = new CancellationTokenSource();
            var runCts = gotoCts;
            isGoingToFocusTarget = true;
            RaisePropertyChanged(nameof(IsGoingToFocusTarget));
            RefreshGotoAvailability();
            RaisePropertyChanged(nameof(CanConfigureLive)); RaisePropertyChanged(nameof(CanUseFocusStreaming));
            CommandManager.InvalidateRequerySuggested();
            bool ownsCaptureBlock = false;
            bool slewCompleted = false;
            int guidingRestarted = 0;
            Func<object, EventArgs, Task> onGuidingStarted = (_, _) =>
            {
                Interlocked.Exchange(ref guidingRestarted, 1);
                try { runCts.Cancel(); } catch (ObjectDisposedException) { }
                return Task.CompletedTask;
            };
            bool observingGuiding = false;
            void ReportStatus(string status)
            {
                FocusTargetStatus = status;
                RaisePropertyChanged(nameof(FocusTargetStatus));
                applicationStatusMediator.StatusUpdate(new ApplicationStatus { Source = "Live Focus", Status = status });
            }
            var progress = new Progress<ApplicationStatus>(status =>
            {
                // Progress<T> reports can still be queued after completion/cancel.
                if (!disposed && ReferenceEquals(gotoCts, runCts) && !runCts.IsCancellationRequested && !string.IsNullOrWhiteSpace(status.Status))
                    ReportStatus($"Centering {target.Name} | {status.Status}");
            });
            try
            {
                if (center || CameraInfo?.Connected == true)
                {
                    cameraMediator.RegisterCaptureBlock(this);
                    ownsCaptureBlock = true;
                }
                CheckDevices(); runCts.Token.ThrowIfCancellationRequested();
                guiderMediator.GuidingStarted += onGuidingStarted;
                observingGuiding = true;
                if (guiderMediator.GetInfo()?.Connected == true)
                {
                    ReportStatus("Stopping guiding before focus-star GOTO…");
                    await PrepareGuiderForSlewAsync(runCts.Token);
                }
                guidingPrepared = true;
                runCts.Token.ThrowIfCancellationRequested(); CheckDevices();
                // Discard images from the previous field so ROI selection cannot
                // accidentally reuse a pre-slew star image (also on failed solves).
                lock (preparedOverviewLock) { preparedOverview = null; preparedOverviewCamera = null; }
                overviewPixels = null; overviewImage = null; RoiSelectionPreview = null; FocusPreviewImage = null;
                IsSelectingRoi = false;
                RaisePropertyChanged(nameof(RoiSelectionPreview)); RaisePropertyChanged(nameof(FocusPreviewImage)); RaisePropertyChanged(nameof(LiveDisplayImage));
                ResetSharedFocusMeasurements();
                ReportStatus($"Moving to {target.Name}…");
                bool success = await telescopeMediator.SlewToCoordinatesAsync(target.Coordinates, gotoCts.Token);
                if (!success) throw new InvalidOperationException("NINA reported that the slew did not complete.");
                slewCompleted = true;
                runCts.Token.ThrowIfCancellationRequested(); CheckDevices();
                var dome = domeMediator.GetInfo();
                if (dome?.Connected == true && dome.CanSetAzimuth && !domeFollower.IsFollowing)
                {
                    ReportStatus(center ? "Synchronizing dome before plate solving…" : "Synchronizing dome…");
                    if (!await domeFollower.TriggerTelescopeSync()) throw new InvalidOperationException("Dome synchronization did not complete.");
                }
                runCts.Token.ThrowIfCancellationRequested(); CheckDevices();
                if (!center)
                {
                    ReportStatus($"Slewed to {target.Name}. Centering was not requested or verified.");
                    return 1;
                }
                ReportStatus($"Centering {target.Name} | Full-frame plate solving ({centering.Sequence.ExposureTime:F1} s)…");
                double error = await centering.CenterAsync(solver, NullProgress<PlateSolveProgress>.Instance, progress, runCts.Token);
                CheckDevices();
                PreviewCenterX = 50; PreviewCenterY = 50;
                if (Properties.Settings.Default.EnableFocusDiagnostics) Logger.Info($"[LiveFocus/Center] {target.Name} centered; error {error:F1} arcsec, tolerance {centering.Parameter.Threshold * 60:F1} arcsec.");
                ReportStatus($"{target.Name} centered · error {error:F1}″ (tolerance {centering.Parameter.Threshold * 60:F1}″). Focus ROI moved to image center.");
                return 1;
            }
            catch (OperationCanceledException)
            {
                ReportStatus(Volatile.Read(ref guidingRestarted) != 0 ?
                    "Guiding restarted; GOTO/centering stopped. Star centering was not verified." :
                    "GOTO/centering stopped. Star centering was not verified.");
                return 0;
            }
            catch (Exception e)
            {
                ReportStatus((slewCompleted ? center ? "Slew completed; centering failed. " : "Slew completed; post-slew operation failed. " : "GOTO did not complete. ") + e.Message);
                throw;
            }
            finally
            {
                if (observingGuiding) guiderMediator.GuidingStarted -= onGuidingStarted;
                if (ownsCaptureBlock)
                {
                    try { cameraMediator.ReleaseCaptureBlock(this); } catch (Exception e) { Logger.Error("[LiveFocus] Could not release GOTO camera reservation", e); }
                }
                gotoCts.Dispose(); gotoCts = null;
                isGoingToFocusTarget = false;
                RefreshGotoAvailability();
                applicationStatusMediator.StatusUpdate(new ApplicationStatus { Source = "Live Focus", Status = string.Empty });
                RaisePropertyChanged(nameof(IsGoingToFocusTarget));
                RaisePropertyChanged(nameof(CanConfigureLive)); RaisePropertyChanged(nameof(CanUseFocusStreaming));
                RaisePropertyChanged(nameof(FocusTargetStatus));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
