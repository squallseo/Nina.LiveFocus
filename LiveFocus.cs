using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using Cwseo.NINA.LiveFocus.Properties;

namespace Cwseo.NINA.LiveFocus
{
    [Export(typeof(IPluginManifest))]
    public sealed class LiveFocus : PluginBase, INotifyPropertyChanged
    {
        public LiveFocus()
        {
            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                Settings.Default.Save();
            }
        }
        public bool UseFocusStreaming
        {
            get => Settings.Default.UseFocusStreaming;
            set { Settings.Default.UseFocusStreaming = value; Settings.Default.Save(); RaisePropertyChanged(); }
        }
        public bool EnableFocusDiagnostics
        {
            get => Settings.Default.EnableFocusDiagnostics;
            set { Settings.Default.EnableFocusDiagnostics = value; Settings.Default.Save(); RaisePropertyChanged(); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
