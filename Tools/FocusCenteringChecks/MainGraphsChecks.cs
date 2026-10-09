using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cwseo.NINA.LiveFocus.Dockables;
using NINA.WPF.Base.View;
using NINA.Equipment.Interfaces.ViewModel;

internal static partial class PreviewDisplayChecks
{
    private static async Task VerifyMainGraphs(LiveFocusDockableVM vm, Action<bool,string> check)
    {
        var host=Fake.Of<IImageControlVM>((m,a)=>m.Name.StartsWith("add_") || m.Name.StartsWith("remove_")?null:Fake.Unexpected(m));
        var view=new ImageView{DataContext=host};
        var root=new Grid{Background=Brushes.Black};root.Children.Add(view);
        using var surface=new HwndSource(new HwndSourceParameters("Live Focus graph verification"){
            Width=650,Height=500,WindowStyle=unchecked((int)0x80000000)});
        surface.RootVisual=root;
        var content=(Grid)view.Content;
        int rows=content.RowDefinitions.Count;
        var children=content.Children.Cast<UIElement>().ToArray();
        var image=BitmapSource.Create(128,128,96,96,PixelFormats.Gray8,null,
            Enumerable.Range(0,128*128).Select(i=>(byte)(i%128+30)).ToArray(),128);
        image.Freeze();view.Image=image;
        async Task Layout(){root.Measure(new Size(650,500));root.Arrange(new Rect(0,0,650,500));root.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);root.UpdateLayout();}
        await Layout();
        vm.ShowInNinaImage=true;vm.ShowLiveGraphs=true;
        object Manager()=>typeof(LiveFocusDockableVM).GetField("mainImageGraphs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);
        bool Attach()=> (bool)Manager().GetType().GetMethod("TryAttach",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Manager(),new object[]{root});
        view.DataContext=new object();
        check(!Attach(),"Other NINA image surfaces cannot receive the live HFR footer");
        view.DataContext=host;
        check(Attach(),"Enabled Graphs attach to the real NINA ImageView's bottom row");
        await Layout();
        var graph=content.Children.OfType<LiveFocusGraphsView>().Single();
        check(content.RowDefinitions.Count==rows+1 && children.All(c=>content.Children.Contains(c)) && ReferenceEquals(view.Image,image),
            "Graph attachment preserves the original Image toolbar, image, rows and children");
        check(Grid.GetRow(graph)==rows && graph.TranslatePoint(new Point(),view).Y>=
            children.Where(c=>Grid.GetRow(c)==rows-1).Max(c=>c.TranslatePoint(new Point(),view).Y+c.RenderSize.Height)-1,
            "HFR graph is below the image viewport rather than over the image");
        check(((FrameworkElement)graph.FindName("ProfileCard")).Visibility==Visibility.Collapsed && !graph.ShowStarProfile,
            "NINA Image footer shows HFR only, with no star profile");
        var plot=(OxyPlot.Wpf.Plot)graph.FindName("PreviewFocusPlot");
        var x=plot.Axes.Single(a=>a.Position==OxyPlot.Axes.AxisPosition.Bottom);
        check(x.Minimum==-120 && x.Maximum==0 && !x.IsPanEnabled && !x.IsZoomEnabled,
            "Rendered time axis has a fixed 120-second window and cannot retain a stale pan or zoom");
        vm.ClearLiveGraphCommand.Execute(null);
        var record=typeof(LiveFocusDockableVM).GetMethod("RecordLiveHfr",BindingFlags.Instance|BindingFlags.NonPublic);
        for(int i=0;i<=700;i++)record.Invoke(vm,new object[]{i*.25,2+.4*Math.Sin(i*.07)});
        await Layout();
        var series=plot.Series.OfType<OxyPlot.Wpf.LineSeries>().Single();
        check(ReferenceEquals(series.ItemsSource,vm.LiveGraphPoints) && vm.LiveGraphPoints[^1].X==0 && vm.LiveGraphPoints[0].X>=-120,
            "The attached plot follows the shifted recent history after several minutes of frames");
        typeof(LiveFocusDockableVM).GetProperty(nameof(vm.LiveHfr)).SetValue(vm,vm.LiveGraphPoints[^1].Y);
        graph.DataContext=null;graph.DataContext=vm;await Layout();
        var shot=new RenderTargetBitmap(650,500,96,96,PixelFormats.Pbgra32);shot.Render(root);
        shot.CopyPixels(new byte[650*500*4],650*4,0);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(shot));
        using(var file=File.Create("bin/live-focus-image-hfr.png"))png.Save(file);
        // The actual compact slider is connected to the hidden test surface.
        var controls=new LiveFocusDockableView{DataContext=vm};
        root.Children.Add(controls);controls.Width=350;controls.Height=180;await Layout();
        var slider=Visuals(controls).OfType<Slider>().Single(s=>s.Maximum==2.5);slider.ApplyTemplate();
        var background=(Grid)slider.Template.FindName("SliderSurface",slider);
        var track=Visuals(slider).OfType<System.Windows.Controls.Primitives.Track>().Single();
        var decrease=track.DecreaseRepeatButton.Background;var increase=track.IncreaseRepeatButton.Background;
        slider.Focus();System.Windows.Input.Keyboard.Focus(slider);
        Slider.IncreaseSmall.Execute(null,slider);await Layout();
        check(background.Background is SolidColorBrush brush && brush.Color.A==0 &&
            ReferenceEquals(track.DecreaseRepeatButton.Background,decrease) && ReferenceEquals(track.IncreaseRepeatButton.Background,increase),
            "Focused stretch adjustment keeps the scale-bar surface and track colors unchanged");
        vm.ResetPreviewStretchCommand.Execute(null);await Layout();
        check(background.Background is SolidColorBrush reset && reset.Color.A==0,
            "Reset uses the same transparent slider surface as adjustment");
        root.Children.Remove(controls);
        vm.ShowLiveGraphs=false;await Layout();
        check(content.RowDefinitions.Count==rows && content.Children.Cast<UIElement>().SequenceEqual(children),
            "Graphs off removes only the owned row and restores the host's original layout");
        vm.ShowLiveGraphs=true;Attach();await Layout();
        typeof(LiveFocusDockableVM).GetProperty(nameof(vm.IsSelectingRoi)).SetValue(vm,true);
        check(content.RowDefinitions.Count==rows,"ROI editing restores the full image viewport by removing the HFR footer");
        typeof(LiveFocusDockableVM).GetProperty(nameof(vm.IsSelectingRoi)).SetValue(vm,false);
        Attach();vm.ShowInNinaImage=false;
        check(content.RowDefinitions.Count==rows,"Local image output detaches the NINA Image footer");
        vm.ShowInNinaImage=true;Attach();await Layout();
        var manager=Manager();manager.GetType().GetMethod("Dispose").Invoke(manager,null);
        check(content.RowDefinitions.Count==rows && !content.Children.OfType<LiveFocusGraphsView>().Any(),
            "Host graph disposal releases its UI, size subscription and timer");
        vm.ShowLiveGraphs=false;vm.ShowInNinaImage=false;
    }
    private static async Task VerifyGraphVmDisposal(LiveFocusDockableVM vm, Action<bool,string> check)
    {
        var host=Fake.Of<IImageControlVM>((m,a)=>m.Name.StartsWith("add_") || m.Name.StartsWith("remove_")?null:Fake.Unexpected(m));
        var view=new ImageView{DataContext=host};var root=new Grid();root.Children.Add(view);
        using var surface=new HwndSource(new HwndSourceParameters("Live Focus graph disposal verification"){
            Width=650,Height=500,WindowStyle=unchecked((int)0x80000000)});
        surface.RootVisual=root;
        root.Measure(new Size(650,500));root.Arrange(new Rect(0,0,650,500));root.UpdateLayout();
        var grid=(Grid)view.Content;int rows=grid.RowDefinitions.Count;
        vm.ShowInNinaImage=true;vm.ShowLiveGraphs=true;
        var manager=typeof(LiveFocusDockableVM).GetField("mainImageGraphs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);
        check((bool)manager.GetType().GetMethod("TryAttach",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,new object[]{root}),
            "Disposal verification begins with a real attached HFR footer");
        vm.Dispose();await Dispatcher.Yield(DispatcherPriority.Background);
        check(grid.RowDefinitions.Count==rows && !grid.Children.OfType<LiveFocusGraphsView>().Any(),
            "VM disposal restores the host viewer despite its disposed update guard");
    }
}
