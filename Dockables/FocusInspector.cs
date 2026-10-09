using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cwseo.NINA.LiveFocus.Models;

namespace Cwseo.NINA.LiveFocus.Dockables
{
    public partial class LiveFocusDockableVM
    {
        private bool aberrationInspector = true;
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
            // Keep the original ushort source for stretch changes, but publish
            // mosaic dimensions so the host never rasterizes a full sensor bitmap.
            previewPixels.Add(bitmap, new PreviewPixels(null, layout.Width, layout.Height, null, level, bitmap, source, Inspector: true));
            return bitmap;
        }
    }
}
