namespace Jarvis.Windows.Properties {
    internal sealed class Settings : System.Configuration.ApplicationSettingsBase {
        private static Settings defaultInstance = ((Settings)(Synchronized(new Settings())));
        public static Settings Default => defaultInstance;

        [System.Configuration.UserScopedSetting]
        [System.Configuration.DefaultSettingValue("False")]
        public bool ShellMode {
            get { return ((bool)(this["ShellMode"])); }
            set { this["ShellMode"] = value; }
        }

        [System.Configuration.UserScopedSetting]
        [System.Configuration.DefaultSettingValue("False")]
        public bool StartMinimized {
            get { return ((bool)(this["StartMinimized"])); }
            set { this["StartMinimized"] = value; }
        }

        [System.Configuration.UserScopedSetting]
        [System.Configuration.DefaultSettingValue("False")]
        public bool OpenSettings {
            get { return ((bool)(this["OpenSettings"])); }
            set { this["OpenSettings"] = value; }
        }

        [System.Configuration.UserScopedSetting]
        [System.Configuration.DefaultSettingValue("True")]
        public bool WakeWordEnabled {
            get { return ((bool)(this["WakeWordEnabled"])); }
            set { this["WakeWordEnabled"] = value; }
        }
    }
}
