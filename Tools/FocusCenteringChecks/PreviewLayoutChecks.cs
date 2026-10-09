using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Cwseo.NINA.LiveFocus.Dockables;

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
        var view=new LiveFocusDockableView {DataContext=vm,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(27,29,32))};
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
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create($"bin/live-focus-{width}.png");encoder.Save(file);
        }
    }
}
