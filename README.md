# Live Focus for N.I.N.A.

<img src="Images/live-focus-featured-v1.png" alt="Live Focus: play triangle, focus ring and star" width="256" />

A NINA plugin for moving the focuser while watching a live star image.

![Live Focus layout](docs/live-focus-ui.png)

Compact controls rendered with synthetic test data. Live frames use NINA's main **Image** panel by default; there is no duplicate image workspace in Live Focus. Exposure, In / Out steps, position, local HFR and stretch stay visible. **Setup** starts folded and contains ROI, target movement, absolute position and Bahtinov overlay controls. Only its contents scroll, leaving live controls accessible on short docks.

**Graphs** starts off. Turn it on to show HFR history and the star profile in a short measurement area. ROI editing uses the main Image panel when it is visible; **Retake / Done** remain in the compact Live Focus controls. A temporary local editor is available when the main viewer is closed or **Use NINA Image** is off. Finishing restores your graph choice. To use a local preview instead of the main Image panel, turn off **Setup → Use NINA Image**.

[Small-screen controls (350 × 180)](docs/live-focus-compact.png)

## Features

- Absolute focuser position, adjustable In / Out steps and cancellation.
- Bright-star selection from NINA's catalogue, with adjustable minimum altitude and maximum magnitude, plus profile horizon clearance. Every matching star is listed; there is no 20-star cap.
- Separate **Slew** (coordinates only) and **Slew + Center** (NINA's configured plate solver) buttons. Active guiding is stopped before either move; disconnected, stopped, looping and selected guiders need no stop request. Guiding is not automatically restarted.
- A **Sync mount** toggle beside the target actions directly controls NINA's existing profile **No Sync** setting, with the value inverted. There is no separate overriding setting.
- Live ROI preview, local HFR history and the star intensity profile. On streaming cameras, preview continues during focuser movement.
- Main **Image** output sends the live ROI, stretch and optional Bahtinov overlay to NINA, capped at 10 updates/second. Normal grayscale output shares the existing frozen bitmap; overlay output is rasterized for the host. There are no extra captures, image-history entries or file saves.
- Mouse ROI selection in the main Image panel: click a star, drag the box to move it, or drag an edge/corner to resize. Yellow grips and cursor changes distinguish the actions. The external size label stays at a fixed screen size through zoom, rotation and flip. [Main Image ROI example](docs/live-focus-main-image-roi.png).
- An explicit **Auto ROI** button; starting live preview preserves the chosen ROI.
- Exposure slider in 50 ms increments, optional Bahtinov mask overlay in Setup and a compact stretch slider beside the live controls. Stretch changes only the display, never raw measurements.
- Optional diagnostic FITS/JSON recording and detailed timing logs. Recording is **off by default**; normal errors and warnings are still logged.

Autofocus, curve fitting, spike autofocus, sequencer instructions and lens configuration are outside this plugin's scope. Use NINA's autofocus or the original Manual Focuser plugin for those features. The Bahtinov overlay is a manual focusing aid.

## Requirements and camera support

- Windows x64, NINA **3.2.0.9001 or newer**, and the .NET 8 Windows desktop runtime used by NINA.
- Connected focuser for motor controls; connected telescope for Slew. Slew + Center additionally requires a connected camera and configured plate solver.
- QHY, ToupTek and ZWO ASI native drivers, plus cameras reporting NINA live-view capability, use the host's streaming interface. ASCOM drivers that lack live view use successive single exposures.
- QHY600M's **3x3 bin mode** falls back to single exposures; other readout modes are allowed. Camera-specific ROI alignment is applied automatically.

Streaming uses NINA's existing camera connection, with no extra SDK handles. Stop waits for the camera reader and any motor movement before releasing the capture reservation. Some native SDK downloads cannot be interrupted immediately. Exact frame rate and available ROI sizes depend on the driver and camera.

## Build and install

```powershell
dotnet build LiveFocus.csproj -c Release
```

Output: `bin\Release\net8.0-windows\Cwseo.NINA.LiveFocus.dll`.

To deploy the build, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\InstallPlugin.ps1
```

The script builds Release and verifies the copied DLL hash. With NINA closed, it copies the DLL and license to:

```text
%LOCALAPPDATA%\NINA\Plugins\3.0.0\Live Focus\
```

If NINA is running, it stages the update under `%LOCALAPPDATA%\NINA\PluginStaging\3.0.0\Live Focus\`; restart NINA to apply it. Staging is also refreshed when deploying with NINA closed so an older pending update cannot overwrite the new build. Use `-SkipBuild` to deploy the existing Release output.

NINA supplies its own libraries; do not copy every dependency from the build directory into the plugin folder. Reopen NINA and add the **Live Focus** dockable panel. Its separate DLL, plugin ID and settings namespace allow coexistence with Manual Focuser.

## Using it

1. Connect the camera and focuser. Open **Setup** to optionally choose a focus star and use **Slew** or **Slew + Center**. **Alt ≥** defaults to 45° and **Mag ≤** to 4.0. Smaller magnitude limits select brighter stars; larger limits include fainter stars. Use the refresh icon after editing either filter. A star must also clear the profile horizon by 5°. Remove a Bahtinov mask for plate solving.
2. Open NINA's **Image** panel. In Setup, choose **Edit ROI**, edit the yellow box directly in Image, then use **Done** in Live Focus. Zoom, scroll, rotation and flip keep the ROI in sensor coordinates. With Image closed, use the temporary local editor. **Auto ROI** is an alternative near the target star. Enable Bahtinov overlay here when using a mask.
3. Fold Setup, open NINA's **Image** panel, set exposure and press the video icon to start. Use In / Out to adjust focus while viewing the star and local HFR. Turn on Graphs when needed. Movement Stop and target-move cancellation remain available with Setup folded.
4. Adjust Stretch if the preview is too bright. Stop preview before changing exposure or ROI.

Main Image output is **on by default** and display-only: adjust stretch and read live HFR in Live Focus; NINA's raw-image statistics, processing and save tools continue to refer to normal captures. Live stream frames are never published while idle, selecting ROI or stopping. Explicit **Edit ROI / Retake** publishes the full overview for mouse editing. The overlay attaches only to the host ImageView displaying that overview and is removed on Done, switching to local preview, disposal or replacement by a normal image. It does not replace host bindings or alter the camera's normal imaging ROI. Turning off **Use NINA Image** or stopping live focus ends updates and leaves the last displayed frame in place; the next normal image replaces it.

Output uses NINA's public [IImagingMediator.SetImage](https://github.com/isbeorn/nina/blob/develop/NINA.Equipment/Interfaces/Mediator/IImagingMediator.cs) display API. The dispatcher keeps at most one pending output callback and uses the latest frame; it does not prepare or record every video frame.

**Slew** never captures, solves or syncs the mount. It can work with the camera disconnected; with a connected camera it reserves capture ownership during movement to prevent overlapping exposures. **Slew + Center** uses NINA's centering tolerance and honors the profile's **No Sync** setting. Find it under **Options → Equipment → Telescope → No Sync**, or use **Setup → Focus star → Sync mount** in Live Focus (on means No Sync is off). This changes the shared NINA profile setting and applies to other NINA centering operations too. A successful sync can improve later slews, but it does not guarantee full-sky pointing accuracy. With No Sync enabled, NINA centers using an offset without updating the mount's pointing model. See [NINA No Sync](https://nighttime-imaging.eu/docs/master/site/tabs/options/equipment/#no-sync).

Guider preparation skips idle/disconnected devices. A failed stop request is accepted only if the guider has since disconnected or is confirmed idle; an active/unknown connected state still blocks movement on failure. User cancellation and guiding restarted during the move remain distinct from an already-stopped guider. This does not infer that every pointing failure is a guiding failure: camera ownership, parked mount and plate-solving errors also have their own checks.

Streaming and diagnostics settings are in NINA's plugin options. Diagnostics, when enabled before an operation, are saved under `%LOCALAPPDATA%\NINA\LiveFocus\FocusDiagnostics`. Turning recording off stops further records; existing records remain available.

The star picker uses NINA's existing bright-star catalogue. Increasing the magnitude limit cannot add stars absent from that catalogue. Its status shows the matching/catalogue counts and applied filters, and Refresh preserves your selected star when it still matches. [Star filter layout](docs/live-focus-star-filters.png).

## Target search proposal

The current release has the bright-star picker only. A future shared target picker should offer **Stars / Deep sky / Solar system** categories, a search box and the same Slew actions inside folded Setup:

- **Stars:** keep altitude, magnitude and horizon filters.
- **Deep sky:** reuse NINA's local Sky Atlas catalogue for names and identifiers such as M, NGC and IC. Use object-specific filters; do not apply the bright-star magnitude limit.
- **Solar system:** list the Moon and planets, calculate topocentric coordinates from the profile location and current time, and recalculate just before slewing. Use Slew by default: stellar plate solving centers a coordinate field, not the visible lunar or planetary disk, and may fail when insufficient background stars are visible.

These categories and search controls are a design proposal, not implemented features.

## Verification

The console harnesses use synthetic frames and fake equipment mediators; they never connect or send commands to physical equipment. Catalogue/centering harnesses require a local NINA installation and its initialized catalogue/native astrometry libraries.

```powershell
dotnet run --project Tools/FocusCenteringChecks -c Release
dotnet run --project Tools/FocusCenteringChecks -c Release -- --live-move
dotnet run --project Tools/FocusCenteringChecks -c Release -- --live-move --asi
dotnet run --project Tools/FocusCenteringChecks -c Release -- --auto-roi
dotnet run --project Tools/FocusCenteringChecks -c Release -- --preview-display
dotnet run --project Tools/FocusCenteringChecks -c Release -- --focuser
dotnet run --project Tools/FocusStreamChecks -c Release
dotnet run --project Tools/RoiInteractionChecks -c Release
dotnet run --project Tools/FocusChecks -c Release
```

The UI harness renders the actual dockable at sizes from 350 × 500 to 950 × 700 without opening NINA, checking folding, resizing and active-operation controls. It also renders NINA's real ImageView on a hidden WPF surface, checking ROI coordinate mapping, movement/resizing, cursors, zoom, rotation, flip and cleanup. These checks verify workflow, cleanup and rendering; physical camera throughput and optical performance still need field testing.

## Origin and license

Derived from [squallseo/Nina.ManualFocuser, spike_analysis](https://github.com/squallseo/Nina.ManualFocuser/tree/spike_analysis) at commit `9e9bbddffd6181389c988c2752a74eeb19e7bd2e`, extracting focuser controls, focus-star GOTO and live preview into an independent plugin. Original copyrights and the MPL-2.0 license are retained. See [LICENSE.txt](LICENSE.txt).

Plugin ID: `ae6b70d2-e99d-4931-8d7c-3e89b6aa27b4`.
