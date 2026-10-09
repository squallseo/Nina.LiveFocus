// Keep defaults in Settings.Settings and app.config in sync.
using System.Configuration;

namespace Cwseo.NINA.LiveFocus.Properties {
    internal sealed partial class Settings : ApplicationSettingsBase {
        private static readonly Settings instance = (Settings)Synchronized(new Settings());
        public static Settings Default => instance;

        [UserScopedSetting, DefaultSettingValue("True")]
        public bool UpdateSettings {
            get => (bool)this[nameof(UpdateSettings)];
            set => this[nameof(UpdateSettings)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("True")]
        public bool UseFocusStreaming {
            get => (bool)this[nameof(UseFocusStreaming)];
            set => this[nameof(UseFocusStreaming)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("False")]
        public bool EnableFocusDiagnostics {
            get => (bool)this[nameof(EnableFocusDiagnostics)];
            set => this[nameof(EnableFocusDiagnostics)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("10000")]
        public int TargetPosition {
            get => (int)this[nameof(TargetPosition)];
            set => this[nameof(TargetPosition)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("600")]
        public int UserStep {
            get => (int)this[nameof(UserStep)];
            set => this[nameof(UserStep)] = value;
        }
    }
}
