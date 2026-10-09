using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Path=System.Windows.Shapes.Path;

// Render the plugin's real WPF geometry, without starting NINA or any equipment.
internal static class Program {
    [STAThread]
    static void Main() {
        _=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var resources=new ResourceDictionary {Source=new Uri("/Cwseo.NINA.LiveFocus;component/Dockables/LiveFocusDockableTemplates.xaml",UriKind.Relative)};
        var geometry=(GeometryGroup)resources["Cwseo.NINA.LiveFocus_SVG"];
        geometry.Freeze();
        var gold=new SolidColorBrush(Color.FromRgb(255,213,79));gold.Freeze();
        var icon=new Path {Data=geometry,Fill=gold,Stretch=Stretch.Uniform,Margin=new Thickness(16)};
        var frame=new Grid();frame.Children.Add(icon);
        Save(frame,256,256,"Images/live-focus-icon-256.png");
        var sheet=new StackPanel {Background=new SolidColorBrush(Color.FromRgb(27,29,32)),Orientation=Orientation.Horizontal};
        foreach(int size in new[]{16,20,24,32,64}) {
            var panel=new StackPanel {Width=100,Margin=new Thickness(8,16,8,16)};
            panel.Children.Add(new TextBlock {Text=$"{size} px",Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,16)});
            foreach(var fill in new Brush[]{Brushes.White,gold}) {
                panel.Children.Add(new Path {Data=geometry,Fill=fill,Stretch=Stretch.Uniform,Width=size,Height=size,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,16)});
            }
            sheet.Children.Add(panel);
        }
        Save(sheet,580,230,"bin/live-focus-icon-sizes.png");
        Console.WriteLine("Rendered transparent 256px icon and 16/20/24/32/64px preview; no equipment used.");
    }
    static void Save(FrameworkElement view,int width,int height,string destination) {
        view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var pixels=new byte[width*height*4];bitmap.CopyPixels(pixels,width*4,0);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(destination);encoder.Save(file);
    }
}
