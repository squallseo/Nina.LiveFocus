using System;
using OxyPlot;
using Cwseo.NINA.LiveFocus.Models;

namespace Cwseo.NINA.LiveFocus.Dockables
{
    public partial class LiveFocusDockableVM
    {
        private LiveHfrHistory liveHistory;
        private double lastGraphTime = double.NegativeInfinity;
        private bool showLiveGraphs;
        private MainImageGraphs mainImageGraphs;
        public DataPoint[] LiveGraphPoints { get; private set; } = Array.Empty<DataPoint>();
        public bool ShowLiveGraphs
        {
            get => showLiveGraphs;
            set
            {
                if (showLiveGraphs == value) return;
                showLiveGraphs = value; RaisePropertyChanged(); UpdateMainImageGraphs();
            }
        }
        private void RecordLiveHfr(double seconds, double hfr)
        {
            liveHistory.Record(seconds, hfr);
            if (seconds - lastGraphTime < LiveHfrHistory.SampleSeconds) return;
            lastGraphTime = seconds;
            LiveGraphPoints = liveHistory.DisplayPoints();
            RaisePropertyChanged(nameof(LiveGraphPoints));
        }
        private void ClearLiveHistory()
        {
            liveHistory.Clear(); lastGraphTime = double.NegativeInfinity;
            LiveGraphPoints = Array.Empty<DataPoint>(); RaisePropertyChanged(nameof(LiveGraphPoints));
        }
        private void UpdateMainImageGraphs()
        {
            if (!disposed && ShowLiveGraphs && ShowInNinaImage && !IsSelectingRoi)
            {
                mainImageGraphs ??= new MainImageGraphs(this);
                mainImageGraphs.Start();
            }
            else StopMainImageGraphs();
        }
        private void StopMainImageGraphs()
        {
            mainImageGraphs?.Dispose(); mainImageGraphs = null;
        }
    }
}
