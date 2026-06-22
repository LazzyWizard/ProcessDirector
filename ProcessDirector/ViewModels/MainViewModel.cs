using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProcessDirector.AppData;
using ProcessDirector.Service;
using ProcessDirector.Services;

namespace ProcessDirector.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private ProcessService _processService;
        private TemperatureService _temperatureService;
        private LoggingService _loggingService;
        private DispatcherTimer _temperatureTimer;
        private bool _isRefreshing = false;
        private SettingsModel _settings;

        private ObservableCollection<ProcessInfo> _allProcesses;
        public ObservableCollection<ProcessInfo> AllProcesses
        {
            get { return _allProcesses; }
            set
            {
                _allProcesses = value;
                OnPropertyChanged("AllProcesses");
            }
        }

        private ProcessInfo _selectedProcess;
        public ProcessInfo SelectedProcess
        {
            get { return _selectedProcess; }
            set
            {
                if (_selectedProcess != value)
                {
                    _selectedProcess = value;
                    OnPropertyChanged("SelectedProcess");
                    (KillProcessCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<string> _logEntries = new ObservableCollection<string>();
        public ObservableCollection<string> LogEntries
        {
            get { return _logEntries; }
            set
            {
                _logEntries = value;
                OnPropertyChanged("LogEntries");
            }
        }

        private string _currentLogFilePath;
        public string CurrentLogFilePath
        {
            get { return _currentLogFilePath; }
            set
            {
                _currentLogFilePath = value;
                OnPropertyChanged("CurrentLogFilePath");
            }
        }

        private string _logDirectory;
        public string LogDirectory
        {
            get { return _logDirectory; }
            set
            {
                _logDirectory = value;
                OnPropertyChanged("LogDirectory");
            }
        }

        private string _statusMessage = "Готов";
        public string StatusMessage
        {
            get { return _statusMessage; }
            set
            {
                _statusMessage = value;
                OnPropertyChanged("StatusMessage");
            }
        }

        private string _cpuTemperature = "--°C";
        public string CpuTemperature
        {
            get { return _cpuTemperature; }
            set
            {
                _cpuTemperature = value;
                OnPropertyChanged("CpuTemperature");
            }
        }

        private string _gpuTemperature = "--°C";
        public string GpuTemperature
        {
            get { return _gpuTemperature; }
            set
            {
                _gpuTemperature = value;
                OnPropertyChanged("GpuTemperature");
            }
        }

        private string _cpuLoad = "--%";
        public string CpuLoad
        {
            get { return _cpuLoad; }
            set
            {
                _cpuLoad = value;
                OnPropertyChanged("CpuLoad");
            }
        }

        private string _gpuLoad = "--%";
        public string GpuLoad
        {
            get { return _gpuLoad; }
            set
            {
                _gpuLoad = value;
                OnPropertyChanged("GpuLoad");
            }
        }

        private string _ramUsage = "0%";
        public string RamUsage
        {
            get { return _ramUsage; }
            set
            {
                _ramUsage = value;
                OnPropertyChanged("RamUsage");
            }
        }

        private double _cpuLoadValue;
        public double CpuLoadValue
        {
            get { return _cpuLoadValue; }
            set
            {
                _cpuLoadValue = value;
                OnPropertyChanged("CpuLoadValue");
            }
        }

        private double _gpuLoadValue;
        public double GpuLoadValue
        {
            get { return _gpuLoadValue; }
            set
            {
                _gpuLoadValue = value;
                OnPropertyChanged("GpuLoadValue");
            }
        }

        private double _ramUsageValue;
        public double RamUsageValue
        {
            get { return _ramUsageValue; }
            set
            {
                _ramUsageValue = value;
                OnPropertyChanged("RamUsageValue");
            }
        }

        private bool _isLoggingActive = false;
        public bool IsLoggingActive
        {
            get { return _isLoggingActive; }
            set
            {
                _isLoggingActive = value;
                OnPropertyChanged("IsLoggingActive");
            }
        }

        private string _logStatus = "Логирование остановлено";
        public string LogStatus
        {
            get { return _logStatus; }
            set
            {
                _logStatus = value;
                OnPropertyChanged("LogStatus");
            }
        }

        private int _processCount;
        public int ProcessCount
        {
            get { return _processCount; }
            set
            {
                _processCount = value;
                OnPropertyChanged("ProcessCount");
            }
        }

        public ICommand RefreshCommand { get; private set; }
        public ICommand KillProcessCommand { get; private set; }
        public ICommand StartLoggingCommand { get; private set; }
        public ICommand StopLoggingCommand { get; private set; }
        public ICommand OpenLogFolderCommand { get; private set; }
        public ICommand OpenCurrentLogCommand { get; private set; }
        public ICommand OpenSettingsCommand { get; private set; }

        public MainViewModel(SettingsModel settings)
        {
            _settings = settings;
            _processService = new ProcessService();
            _loggingService = new LoggingService(_settings.LogFolderPath);
            _allProcesses = new ObservableCollection<ProcessInfo>();

            RefreshCommand = new RelayCommand(_ => RefreshProcessesAsync());
            KillProcessCommand = new RelayCommand(_ => KillSelectedProcess(), _ => SelectedProcess != null);
            StartLoggingCommand = new RelayCommand(_ => StartLogging());
            StopLoggingCommand = new RelayCommand(_ => StopLogging());
            OpenLogFolderCommand = new RelayCommand(_ => OpenLogFolder());
            OpenCurrentLogCommand = new RelayCommand(_ => OpenCurrentLogFile());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());

            _processService.ProcessesUpdated += OnProcessesUpdated;
            _loggingService.NewLogEntry += OnNewLogEntry;
            _processService.LoadingStatus += OnLoadingStatus;

            InitializeTemperatureMonitoring();
            LogDirectory = _loggingService.GetLogDirectory();
        }

        public void UpdateLogDirectory(string newPath)
        {
            _settings.LogFolderPath = newPath;
            _loggingService.UpdateLogDirectory(newPath);
            LogDirectory = _loggingService.GetLogDirectory();
        }

        public void LogEvent(string eventType, string processName, int pid)
        {
            _loggingService.LogEvent(eventType, processName, pid);
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(_settings);
            if (settingsWindow.ShowDialog() == true)
            {
                _settings = settingsWindow.GetUpdatedSettings();
                SettingsManager.Save(_settings);
                UpdateLogDirectory(_settings.LogFolderPath);
            }
        }

        private void OnLoadingStatus(string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = status;
            });
        }

        private void OnProcessesUpdated(System.Collections.Generic.List<ProcessInfo> processes)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                int? savedId = _selectedProcess?.Id;

                _allProcesses.Clear();
                foreach (var p in processes)
                {
                    _allProcesses.Add(p);
                }

                ProcessCount = _allProcesses.Count;

                if (savedId.HasValue)
                {
                    var restoredProcess = _allProcesses.FirstOrDefault(p => p.Id == savedId.Value);
                    if (restoredProcess != null)
                    {
                        _selectedProcess = restoredProcess;
                        OnPropertyChanged("SelectedProcess");
                    }
                }
            });
        }

        private async void RefreshProcessesAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            StatusMessage = "Обновление...";
            await Task.Run(() => _processService.GetProcesses());
            StatusMessage = "Готов";
            _isRefreshing = false;
        }

        private void KillSelectedProcess()
        {
            if (SelectedProcess == null)
            {
                StatusMessage = "Выберите процесс";
                return;
            }

            string name = SelectedProcess.Name.ToLower();
            if (name.Contains("svchost") || name.Contains("csrss") || name.Contains("services") ||
                name.Contains("lsass") || name.Contains("winlogon") || name.Contains("system"))
            {
                StatusMessage = "Невозможно завершить системный процесс: " + SelectedProcess.Name;
                return;
            }

            try
            {
                if (_processService.KillProcess(SelectedProcess.Id))
                {
                    if (_isLoggingActive) _loggingService.LogEvent("UserKill", SelectedProcess.Name, SelectedProcess.Id);
                    StatusMessage = "Процесс " + SelectedProcess.Name + " завершён";
                    SelectedProcess = null;
                }
                else
                {
                    StatusMessage = "Не удалось завершить " + SelectedProcess.Name;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка: " + ex.Message;
            }
        }

        private void OnNewLogEntry(string log)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _logEntries.Add(log);
                if (_logEntries.Count > 1000)
                {
                    _logEntries.RemoveAt(0);
                }
                CurrentLogFilePath = _loggingService.GetCurrentLogFilePath();
            });
        }

        private void StartLogging()
        {
            if (_loggingService.StartSession())
            {
                _isLoggingActive = true;
                LogStatus = "Логирование активно";
                StatusMessage = "Логирование запущено";
                _logEntries.Clear();
                MessageBox.Show("Логирование запущено.\nЛоги сохраняются в " + _settings.LogFolderPath, "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Не удалось запустить логирование", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopLogging()
        {
            _loggingService.StopSession();
            _isLoggingActive = false;
            LogStatus = "Логирование остановлено";
            StatusMessage = "Логирование остановлено";
        }

        private void OpenLogFolder()
        {
            _loggingService.OpenLogDirectory();
        }

        private void OpenCurrentLogFile()
        {
            if (!string.IsNullOrEmpty(CurrentLogFilePath) && System.IO.File.Exists(CurrentLogFilePath))
            {
                Process.Start(new ProcessStartInfo { FileName = CurrentLogFilePath, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("Файл логов не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializeTemperatureMonitoring()
        {
            try
            {
                _temperatureService = new TemperatureService();
                if (_temperatureService.IsHardwareAvailable)
                {
                    _temperatureTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    _temperatureTimer.Tick += (s, e) => UpdateTemperatureData();
                    _temperatureTimer.Start();
                }
            }
            catch { }
        }

        private void UpdateTemperatureData()
        {
            _temperatureService.Update();
            Application.Current.Dispatcher.Invoke(() =>
            {
                CpuTemperature = _temperatureService.CpuTemperature > 0 ? _temperatureService.CpuTemperature.ToString("F1") + "°C" : "--°C";
                GpuTemperature = _temperatureService.GpuTemperature > 0 ? _temperatureService.GpuTemperature.ToString("F1") + "°C" : "--°C";
                CpuLoad = _temperatureService.CpuLoad > 0 ? _temperatureService.CpuLoad.ToString("F0") + "%" : "--%";
                GpuLoad = _temperatureService.GpuLoad > 0 ? _temperatureService.GpuLoad.ToString("F0") + "%" : "--%";
                RamUsage = _temperatureService.RamUsage.ToString("F0") + "%";
                CpuLoadValue = _temperatureService.CpuLoad;
                GpuLoadValue = _temperatureService.GpuLoad;
                RamUsageValue = _temperatureService.RamUsage;
            });
        }

        public void Dispose()
        {
            if (_isLoggingActive) _loggingService.StopSession();
            _temperatureService?.Dispose();
            _processService?.Dispose();
            _temperatureTimer?.Stop();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}