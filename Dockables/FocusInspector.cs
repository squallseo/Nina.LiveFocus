using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cwseo.NINA.LiveFocus.Models;

namespace Cwseo.NINA.LiveFocus.Dockables
{
    public partial class LiveFocusDockableVM
    {
        private bool aberrationInspector;
        public bool IsAberrationInspector => aberrationInspector;
        public bool CanUseBahtinovOverlay => !IsAberrationInspector;
        private bool SetAberrationInspector(bool enabled)
        {
            if (aberrationInspector == enabled) return false;
            aberrationInspector = enabled;
            RaisePropertyChanged(nameof(IsAberrationInspector)); RaisePropertyChanged(nameof(CanUseBahtinovOverlay));
            RaisePropertyChanged(nameof(PreviewRoiPreset)); RaisePropertyChanged(nameof(PreviewRoiRectangle));
            RaisePropertyChanged(nameof(RoiLocationText));
            RaisePropertyChanged(nameof(LiveHfrText));
            return true;
        }

        private ImageSource RenderAberrationPreview(FocusRawFrame source, double? strength = null)
        {
            double level = strength ?? PreviewStretchStrength;
            var layout = FocusAberrationMosaic.Plan(source.Width, source.Height);
            var bytes = FocusAberrationMosaic.Display(source, layout, level);
            var bitmap = BitmapSource.Create(layout.Width, layout.Height, 96, 96, PixelFormats.Gray8, null, bytes, layout.Width);
            bitmap.Freeze();
            var group = new DrawingGroup();
            using (var drawing = group.Open())
            {
                drawing.DrawImage(bitmap, new Rect(0, 0, layout.Width, layout.Height));
                foreach (var tile in layout.Tiles)
                {
                    var label = new FormattedText(tile.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 16, Brushes.White, 1)
                        { MaxTextWidth = System.Math.Max(1, layout.Size - 12), Trimming = TextTrimming.CharacterEllipsis };
                    drawing.DrawText(label, new Point(tile.DisplayX + 6, tile.DisplayY - FocusAberrationMosaic.Header + 2));
                }
            }
            var image = new DrawingImage(group); image.Freeze();
            // Keep the original ushort source for stretch changes, but publish
            // mosaic dimensions so the host never rasterizes a full sensor bitmap.
            previewPixels.Add(image, new PreviewPixels(null, layout.Width, layout.Height, null, level, bitmap, source, Inspector: true));
            return image;
        }
    }
}
