using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Cwseo.NINA.LiveFocus.Dockables;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyFocuser;

internal static partial class PreviewDisplayChecks {
    private static void RenderControls(LiveFocusDockableVM vm, Action<bool,string> check) {
        var assembly=typeof(LiveFocusDockableVM).Assembly;
        check(assembly.GetName().Name=="Cwseo.NINA.LiveFocus" && assembly.GetCustomAttribute<GuidAttribute>().Value=="ae6b70d2-e99d-4931-8d7c-3e89b6aa27b4",
            "Standalone plugin has its own assembly and identity");
        check(!assembly.GetTypes().Any(t=>t.Name.Contains("AutofocusRunner") || t.Name.Contains("LensConfig") || t.Name.Contains("SpikeCore")),
            "Autofocus, lens configuration and spike analysis engines are excluded");
        var settings=assembly.GetType("Cwseo.NINA.LiveFocus.Properties.Settings");
        var defaults=Activator.CreateInstance(settings,true);
        check((bool)settings.GetProperty("UseFocusStreaming").GetValue(defaults) && !(bool)settings.GetProperty("EnableFocusDiagnostics").GetValue(defaults),
            "New plugin defaults to streaming with diagnostic recording off");
        XNamespace wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source=XDocument.Load("Dockables/LiveFocusDockableView.xaml");
        var missing=new List<string>();
        foreach(var attribute in source.Descendants().Attributes().Where(a=>a.Value.StartsWith("{Binding ") && !a.Value.Contains("RelativeSource") && !a.Value.Contains("Source=") && !a.Value.Contains("ElementName=") && !a.Parent.Ancestors(wpf+"DataTemplate").Any())) {
            string property=attribute.Value[9..].Split(new[]{',','}'})[0].Trim().Split('.')[0];
            if(typeof(LiveFocusDockableVM).GetProperty(property)==null)missing.Add(property);
        }
        check(missing.Count==0,"Refactored UI has no stale view-model bindings: "+string.Join(", ",missing));
        // Supply only the host's theme data, never a real profile service.
        Application.Current.Resources["ProfileService"]=new {ActiveProfile=new {ColorSchemaSettings=new {ColorSchema=new {
            PrimaryColor=Colors.White,SecondaryColor=Colors.Gray,BackgroundColor=Color.FromRgb(27,29,32),
            BorderColor=Color.FromRgb(64,68,73),ButtonBackgroundColor=Color.FromRgb(50,54,60),
            ButtonBackgroundSelectedColor=Color.FromRgb(71,81,96),ButtonForegroundColor=Colors.White
        }}}};
        foreach(string name in new[]{"Brushes","SVGDictionary","Converters"})
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri($"/NINA.WPF.Base;component/Resources/StaticResources/{name}.xaml",UriKind.Relative)});
        var view=new LiveFocusDockableView {DataContext=vm,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(27,29,32))};
        vm.UpdateDeviceInfo(new CameraInfo {Connected=true,DeviceId="Synthetic.UI",XSize=4096,YSize=3072});
        vm.UpdateDeviceInfo(new FocuserInfo {Connected=true,DeviceId="Synthetic.Motor",Position=10000});
        var star=new Cwseo.NINA.LiveFocus.Models.FocusStarSuggestion {Name="Example star",Magnitude=2,Altitude=70,Azimuth=135};
        vm.FocusTargets.Add(star);vm.SelectedFocusTarget=star;
        typeof(LiveFocusDockableVM).GetProperty(nameof(vm.LiveHfr)).SetValue(vm,2.25);
        typeof(LiveFocusDockableVM).GetProperty(nameof(vm.LiveStarProfile)).SetValue(vm,Enumerable.Range(-32,65).Select(x=>new OxyPlot.DataPoint(x,Math.Exp(-x*x/18.0))).ToArray());
        for(int i=0;i<24;i++)vm.LiveHfrPoints.Add(new OxyPlot.DataPoint(i,2.25+2*Math.Exp(-i/5.0)+.08*Math.Sin(i)));
        view.DataContext=null;view.DataContext=vm;
        foreach(int width in new[]{350,650,950}) {
            view.Measure(new Size(width,700));view.Arrange(new Rect(0,0,width,700));view.UpdateLayout();
            view.Dispatcher.Invoke(()=>{},DispatcherPriority.DataBind);view.UpdateLayout();
            var video=Children(view).OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Start live focus");
            check(video.Visibility==Visibility.Visible && video.ActualWidth==36,"Start video button stays visible while idle at "+width);
            var step=Children(view).OfType<Button>().Single(b=>Equals(b.Content,"In −"));
            check(step.Visibility==Visibility.Visible && step.ActualWidth>35,"Focuser direction button is readable at "+width);
            check(((SolidColorBrush)step.Foreground).Color.A==255,"Focuser button text uses the visible host theme at "+width);
            var exposure=Children(view).OfType<Slider>().Single(s=>s.Maximum==5000);
            check(exposure.ActualWidth>=30 && exposure.SmallChange==50,"Exposure slider fits with 50ms increments at "+width);
            var stretch=Children(view).OfType<Slider>().Single(s=>s.Maximum==2.5);
            check(stretch.ActualWidth>20 && stretch.Value==vm.PreviewStretchStrength,"Image stretch remains accessible at "+width);
            var bitmap=new RenderTargetBitmap(width,700,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
            var pixels=new byte[width*700*4];bitmap.CopyPixels(pixels,width*4,0);
            check(pixels.Where((v,i)=>i%4==3).Count(v=>v>0)>width*100,"Offscreen screenshot actually contains rendered UI at "+width);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create($"bin/live-focus-{width}.png");encoder.Save(file);
            var preview=(FrameworkElement)view.FindName("PreviewSurface");
            var graphs=(FrameworkElement)view.FindName("MeasurementsPanel");
            if(width==350) {
                check(preview.ActualHeight>=180 && graphs.Visibility==Visibility.Collapsed,"Narrow dock prioritizes a usable star image with inline HFR");
                var toggle=(ToggleButton)view.FindName("MeasurementsToggle");toggle.IsChecked=true;view.UpdateLayout();
                check(graphs.Visibility==Visibility.Visible && graphs.TranslatePoint(new Point(),view).Y>=preview.TranslatePoint(new Point(),view).Y+preview.ActualHeight,
                    "Graphs toggle displays measurements below the narrow image");
                toggle.IsChecked=false;
            } else check(graphs.Visibility==Visibility.Visible && graphs.TranslatePoint(new Point(),view).X>preview.TranslatePoint(new Point(),view).X,
                "Wide dock places measurement plots beside the larger preview at "+width);
        }
        var vmType=typeof(LiveFocusDockableVM);
        void State(string field,object value){vmType.GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(vm,value);view.DataContext=null;view.DataContext=vm;view.UpdateLayout();view.Dispatcher.Invoke(()=>{},DispatcherPriority.DataBind);view.UpdateLayout();}
        var button=(Button)view.FindName("LiveVideoButton");
        State("assistRunning",true);
        check(button.Command==vm.StopFocusPreviewCommand && AutomationProperties.GetName(button)=="Stop live focus","The same video button becomes Stop during preview");
        CheckIcon("cancel");
        State("isStoppingFocusPreview",true);
        check(!button.IsEnabled && AutomationProperties.GetName(button)=="Stopping live focus","Stop waiting shows a disabled pending state");
        CheckIcon("hourglass");
        State("isStoppingFocusPreview",false);State("assistRunning",false);
        State("isGoingToFocusTarget",true);
        var go=(Button)view.FindName("GotoButton");
        check(go.Command==vm.CancelGotoCommand && Equals(go.Content,"Cancel") && go.ActualWidth==58,"GOTO and cancel share a stable button without shifting the star picker");
        State("isGoingToFocusTarget",false);State("isSelectingRoi",true);
        var surface=(FrameworkElement)view.FindName("PreviewSurface");
        var workspace=(FrameworkElement)view.FindName("PreviewWorkspace");
        check(((FrameworkElement)view.FindName("MeasurementsPanel")).Visibility==Visibility.Collapsed && surface.ActualWidth>workspace.ActualWidth*.9,
            "ROI editing uses the full workspace width and hides plots");
        var outline=(System.Windows.Shapes.Rectangle)view.FindName("RoiOutline");
        var labels=((Canvas)view.FindName("RoiHandles")).Children.OfType<Border>().Where(b=>b.Child is TextBlock).ToArray();
        check(outline.Visibility==Visibility.Visible && labels.Length==1 && ((TextBlock)labels[0].Child).FontSize==13 && Canvas.GetTop(labels[0])+labels[0].DesiredSize.Height<=Canvas.GetTop(outline),
            "Yellow ROI is visible with fixed-size text outside its top edge");
        var roiShot=new RenderTargetBitmap(950,700,96,96,PixelFormats.Pbgra32);roiShot.Render(view);
        var roiEncoder=new PngBitmapEncoder();roiEncoder.Frames.Add(BitmapFrame.Create(roiShot));
        using(var file=File.Create("bin/live-focus-roi.png"))roiEncoder.Save(file);
        State("isSelectingRoi",false);
        var exposureSlider=(Slider)view.FindName("ExposureSlider");
        vm.PreviewExposureMs=250;view.Dispatcher.Invoke(()=>{},DispatcherPriority.DataBind);
        Slider.IncreaseSmall.Execute(null,exposureSlider);check(vm.PreviewExposureMs==300,"Exposure keyboard step increases by exactly 50 ms");
        Slider.DecreaseSmall.Execute(null,exposureSlider);check(vm.PreviewExposureMs==250,"Exposure keyboard step decreases by exactly 50 ms");
        void CheckIcon(string name) {
            var path=Children(button).OfType<System.Windows.Shapes.Path>().Single();
            var bounds=path.TransformToAncestor(button).TransformBounds(new Rect(path.RenderSize));
            check(bounds.Width>8 && bounds.Height>8 && bounds.Left>=0 && bounds.Top>=0 && bounds.Right<=button.ActualWidth && bounds.Bottom<=button.ActualHeight,
                "The "+name+" icon fits completely inside its button");
        }
    }
}
