using System.ComponentModel;
using System.Windows.Input;
using ProcessDirector.AppData;

namespace ProcessDirector.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private SettingsModel _settings;

        public SettingsViewModel(SettingsModel settings)
        {
            _settings = settings;
        }

        public string LogFolderPath
        {
            get { return _settings.LogFolderPath; }
            set
            {
                _settings.LogFolderPath = value;
                OnPropertyChanged("LogFolderPath");
            }
        }

        public bool SuperF4Enabled
        {
            get { return _settings.SuperF4Enabled; }
            set
            {
                _settings.SuperF4Enabled = value;
                OnPropertyChanged("SuperF4Enabled");
            }
        }

        public int SuperF4Modifiers
        {
            get { return _settings.SuperF4Modifiers; }
            set
            {
                _settings.SuperF4Modifiers = value;
                OnPropertyChanged("SuperF4Modifiers");
            }
        }

        public string SuperF4Key
        {
            get { return _settings.SuperF4Key.ToString(); }
            set
            {
                if (string.IsNullOrEmpty(value) || value == "None")
                    _settings.SuperF4Key = Key.None;
                else
                    _settings.SuperF4Key = (Key)System.Enum.Parse(typeof(Key), value);
                OnPropertyChanged("SuperF4Key");
            }
        }

        public bool RefreshEnabled
        {
            get { return _settings.RefreshHotkeyEnabled; }
            set
            {
                _settings.RefreshHotkeyEnabled = value;
                OnPropertyChanged("RefreshEnabled");
            }
        }

        public int RefreshModifiers
        {
            get { return _settings.RefreshModifiers; }
            set
            {
                _settings.RefreshModifiers = value;
                OnPropertyChanged("RefreshModifiers");
            }
        }

        public string RefreshKey
        {
            get { return _settings.RefreshKey.ToString(); }
            set
            {
                if (string.IsNullOrEmpty(value) || value == "None")
                    _settings.RefreshKey = Key.None;
                else
                    _settings.RefreshKey = (Key)System.Enum.Parse(typeof(Key), value);
                OnPropertyChanged("RefreshKey");
            }
        }

        public bool StartLoggingEnabled
        {
            get { return _settings.StartLoggingHotkeyEnabled; }
            set
            {
                _settings.StartLoggingHotkeyEnabled = value;
                OnPropertyChanged("StartLoggingEnabled");
            }
        }

        public int StartLoggingModifiers
        {
            get { return _settings.StartLoggingModifiers; }
            set
            {
                _settings.StartLoggingModifiers = value;
                OnPropertyChanged("StartLoggingModifiers");
            }
        }

        public string StartLoggingKey
        {
            get { return _settings.StartLoggingKey.ToString(); }
            set
            {
                if (string.IsNullOrEmpty(value) || value == "None")
                    _settings.StartLoggingKey = Key.None;
                else
                    _settings.StartLoggingKey = (Key)System.Enum.Parse(typeof(Key), value);
                OnPropertyChanged("StartLoggingKey");
            }
        }

        public bool StopLoggingEnabled
        {
            get { return _settings.StopLoggingHotkeyEnabled; }
            set
            {
                _settings.StopLoggingHotkeyEnabled = value;
                OnPropertyChanged("StopLoggingEnabled");
            }
        }

        public int StopLoggingModifiers
        {
            get { return _settings.StopLoggingModifiers; }
            set
            {
                _settings.StopLoggingModifiers = value;
                OnPropertyChanged("StopLoggingModifiers");
            }
        }

        public string StopLoggingKey
        {
            get { return _settings.StopLoggingKey.ToString(); }
            set
            {
                if (string.IsNullOrEmpty(value) || value == "None")
                    _settings.StopLoggingKey = Key.None;
                else
                    _settings.StopLoggingKey = (Key)System.Enum.Parse(typeof(Key), value);
                OnPropertyChanged("StopLoggingKey");
            }
        }

        public bool KillProcessEnabled
        {
            get { return _settings.KillProcessHotkeyEnabled; }
            set
            {
                _settings.KillProcessHotkeyEnabled = value;
                OnPropertyChanged("KillProcessEnabled");
            }
        }

        public int KillProcessModifiers
        {
            get { return _settings.KillProcessModifiers; }
            set
            {
                _settings.KillProcessModifiers = value;
                OnPropertyChanged("KillProcessModifiers");
            }
        }

        public string KillProcessKey
        {
            get { return _settings.KillProcessKey.ToString(); }
            set
            {
                if (string.IsNullOrEmpty(value) || value == "None")
                    _settings.KillProcessKey = Key.None;
                else
                    _settings.KillProcessKey = (Key)System.Enum.Parse(typeof(Key), value);
                OnPropertyChanged("KillProcessKey");
            }
        }

        public bool LogNetworkConnections
        {
            get { return _settings.LogNetworkConnections; }
            set
            {
                _settings.LogNetworkConnections = value;
                OnPropertyChanged("LogNetworkConnections");
            }
        }

        public SettingsModel GetSettings()
        {
            return _settings;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}