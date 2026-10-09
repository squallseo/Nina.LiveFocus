using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NINA.WPF.Base.View;
using NINA.Equipment.Interfaces.ViewModel;

namespace Cwseo.NINA.LiveFocus.Dockables
{
    // Add one owned bottom row to ImageView's public content grid. The viewer's
    // original rows, children, bindings, zoom/scroll and toolbar remain in place.
    internal sealed class MainImageGraphs : IDisposable
    {
        private readonly LiveFocusDockableVM vm;
        private readonly DispatcherTimer timer;
        private ImageView view;
        private Grid grid;
        private RowDefinition row;
        private LiveFocusGraphsView graphs;
        private bool disposed;
        public MainImageGraphs(LiveFocusDockableVM vm)
        {
            this.vm = vm;
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += OnTick;
        }
        public void Start() { if (disposed) return; OnTick(null, EventArgs.Empty); timer.Start(); }
        private void OnTick(object sender, EventArgs e)
        {
            if (disposed) return;
            if (view != null)
            {
                if (view.IsVisible && ReferenceEquals(view.Content, grid) && grid.Children.Contains(graphs)) return;
                Detach();
            }
            if (Application.Current == null) return;
            foreach (Window window in Application.Current.Windows)
                if (window.IsVisible && TryAttach(window)) return;
        }
        internal bool TryAttach(DependencyObject root)
        {
            if (disposed || !vm.ShowLiveGraphs || !vm.ShowInNinaImage || vm.IsSelectingRoi) return false;
            if (view != null) return true;
            var candidate = MainImageRoiEditor.Descendants(root).OfType<ImageView>()
                .FirstOrDefault(v => v.IsVisible && v.DataContext is IImageControlVM);
            if (candidate?.Content is not Grid content || content.RowDefinitions.Count < 2) return false;
            view = candidate; grid = content;
            row = new RowDefinition { Height = GridLength.Auto };
            graphs = new LiveFocusGraphsView { DataContext = vm, ShowStarProfile = false, Margin = new Thickness(4, 6, 4, 4) };
            Grid.SetRow(graphs, grid.RowDefinitions.Count);
            Grid.SetColumnSpan(graphs, Math.Max(1, grid.ColumnDefinitions.Count));
            grid.RowDefinitions.Add(row); grid.Children.Add(graphs);
            view.SizeChanged += OnSizeChanged;
            UpdateHeight();
            return true;
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateHeight();
        private void UpdateHeight()
        {
            if (graphs != null) graphs.Height = Math.Clamp(view.ActualHeight * .28, 120, 180);
        }
        private void Detach()
        {
            if (view == null) return;
            view.SizeChanged -= OnSizeChanged;
            graphs.DataContext = null;
            grid.Children.Remove(graphs);
            // Another extension may append its own row after ours. Keep that
            // extension's row indices unchanged rather than shifting its layout.
            if (grid.RowDefinitions.LastOrDefault() == row) grid.RowDefinitions.Remove(row);
            else row.Height = new GridLength(0);
            view = null; grid = null; row = null; graphs = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; timer.Stop(); timer.Tick -= OnTick; Detach();
        }
    }
}
