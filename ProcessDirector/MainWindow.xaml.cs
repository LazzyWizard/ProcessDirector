using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ProcessDirector.AppData;
using ProcessDirector.Services;
using ProcessDirector.ViewModels;

namespace ProcessDirector
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private SettingsModel _settings;
        private HotkeyManager _hotkeyManager;
        private HwndSource _hwndSource;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_TERMINATE = 0x0001;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        public MainWindow()
        {
            InitializeComponent();

            _settings = SettingsManager.Load();
            _viewModel = new MainViewModel(_settings);
            DataContext = _viewModel;

            this.Closed += MainWindow_Closed;
            this.SourceInitialized += MainWindow_SourceInitialized;
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(HwndHook);
            }

            _hotkeyManager = new HotkeyManager(new WindowInteropHelper(this).Handle);
            RegisterHotkeys();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                _hotkeyManager?.ProcessHotkey(id);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void RegisterHotkeys()
        {
            if (_hotkeyManager == null) return;

            _hotkeyManager.UnregisterAll();

            if (_settings.SuperF4Enabled && _settings.SuperF4Key != Key.None)
            {
                _hotkeyManager.RegisterHotkey(
                    _settings.SuperF4Modifiers,
                    _settings.SuperF4Key,
                    () => SuperF4_KillActiveWindow());
            }

            if (_settings.RefreshHotkeyEnabled && _settings.RefreshKey != Key.None)
            {
                _hotkeyManager.RegisterHotkey(
                    _settings.RefreshModifiers,
                    _settings.RefreshKey,
                    () => _viewModel?.RefreshCommand?.Execute(null));
            }

            if (_settings.StartLoggingHotkeyEnabled && _settings.StartLoggingKey != Key.None)
            {
                _hotkeyManager.RegisterHotkey(
                    _settings.StartLoggingModifiers,
                    _settings.StartLoggingKey,
                    () => _viewModel?.StartLoggingCommand?.Execute(null));
            }

            if (_settings.StopLoggingHotkeyEnabled && _settings.StopLoggingKey != Key.None)
            {
                _hotkeyManager.RegisterHotkey(
                    _settings.StopLoggingModifiers,
                    _settings.StopLoggingKey,
                    () => _viewModel?.StopLoggingCommand?.Execute(null));
            }

            if (_settings.KillProcessHotkeyEnabled && _settings.KillProcessKey != Key.None)
            {
                _hotkeyManager.RegisterHotkey(
                    _settings.KillProcessModifiers,
                    _settings.KillProcessKey,
                    () => _viewModel?.KillProcessCommand?.Execute(null));
            }
        }

        private void SuperF4_KillActiveWindow()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return;

                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    if (proc.ProcessName.ToLower() == "processdirector") return;

                    string name = proc.ProcessName.ToLower();
                    if (name.Contains("svchost") || name.Contains("csrss") ||
                        name.Contains("services") || name.Contains("lsass") ||
                        name.Contains("winlogon") || name.Contains("system") ||
                        name.Contains("taskmgr"))
                    {
                        return;
                    }

                    IntPtr hProcess = OpenProcess(PROCESS_TERMINATE | PROCESS_QUERY_INFORMATION, false, (uint)pid);
                    if (hProcess != IntPtr.Zero)
                    {
                        TerminateProcess(hProcess, 0);
                        CloseHandle(hProcess);
                        proc.WaitForExit(2000);

                        if (_viewModel != null && _viewModel.IsLoggingActive)
                        {
                            _viewModel.LogEvent("SuperF4", proc.ProcessName, (int)pid);
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F5 && !_settings.RefreshHotkeyEnabled)
            {
                _viewModel?.RefreshCommand?.Execute(null);
            }
            base.OnKeyDown(e);
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource = null;
            }
            _hotkeyManager?.Dispose();
            _viewModel?.Dispose();
        }
    }

    public class CategoryNameConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is AppData.ProcessDisplayCategory category)
            {
                if (category == AppData.ProcessDisplayCategory.Apps)
                    return "Приложения";
                if (category == AppData.ProcessDisplayCategory.Background)
                    return "Фоновые";
                if (category == AppData.ProcessDisplayCategory.Windows)
                    return "Системные";
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}