using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

        [DllImport("ntdll.dll")]
        private static extern int NtSuspendProcess(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern int NtResumeProcess(IntPtr hProcess);

        private const uint PROCESS_TERMINATE = 0x0001;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_SUSPEND_RESUME = 0x0800;

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
            Debug.WriteLine("SourceInitialized called");

            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (_hwndSource != null)
            {
                Debug.WriteLine("HwndSource created");
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
                Debug.WriteLine($"WM_HOTKEY received, id: {id}");
                _hotkeyManager?.ProcessHotkey(id);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void RegisterHotkeys()
        {
            if (_hotkeyManager == null)
            {
                Debug.WriteLine("HotkeyManager is null!");
                return;
            }

            _hotkeyManager.UnregisterAll();

            Debug.WriteLine("=== Registering hotkeys ===");
            Debug.WriteLine($"SuperF4Enabled: {_settings.SuperF4Enabled}, SuperF4Key: {_settings.SuperF4Key}, Modifiers: {_settings.SuperF4Modifiers}");

            if (_settings.SuperF4Enabled && _settings.SuperF4Key != Key.None)
            {
                bool result = _hotkeyManager.RegisterHotkey(
                    _settings.SuperF4Modifiers,
                    _settings.SuperF4Key,
                    () => SuperF4_KillActiveWindow());
                Debug.WriteLine($"SuperF4 registered: {result}");
            }
            else
            {
                Debug.WriteLine("SuperF4 disabled or key not set");
            }

            if (_settings.RefreshHotkeyEnabled && _settings.RefreshKey != Key.None)
            {
                bool result = _hotkeyManager.RegisterHotkey(
                    _settings.RefreshModifiers,
                    _settings.RefreshKey,
                    () => _viewModel?.RefreshCommand?.Execute(null));
                Debug.WriteLine($"Refresh registered: {result}");
            }

            if (_settings.StartLoggingHotkeyEnabled && _settings.StartLoggingKey != Key.None)
            {
                bool result = _hotkeyManager.RegisterHotkey(
                    _settings.StartLoggingModifiers,
                    _settings.StartLoggingKey,
                    () => _viewModel?.StartLoggingCommand?.Execute(null));
                Debug.WriteLine($"StartLogging registered: {result}");
            }

            if (_settings.StopLoggingHotkeyEnabled && _settings.StopLoggingKey != Key.None)
            {
                bool result = _hotkeyManager.RegisterHotkey(
                    _settings.StopLoggingModifiers,
                    _settings.StopLoggingKey,
                    () => _viewModel?.StopLoggingCommand?.Execute(null));
                Debug.WriteLine($"StopLogging registered: {result}");
            }

            if (_settings.KillProcessHotkeyEnabled && _settings.KillProcessKey != Key.None)
            {
                bool result = _hotkeyManager.RegisterHotkey(
                    _settings.KillProcessModifiers,
                    _settings.KillProcessKey,
                    () => _viewModel?.KillProcessCommand?.Execute(null));
                Debug.WriteLine($"KillProcess registered: {result}");
            }

            Debug.WriteLine("=== Registration complete ===");
        }

        private void SuperF4_KillActiveWindow()
        {
            Debug.WriteLine("SuperF4 triggered!");
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"SuperF4 error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SuperF4 general error: {ex.Message}");
            }
        }

        public void KillProcessTree(int pid)
        {
            Task.Run(() =>
            {
                try
                {
                    var mainProc = Process.GetProcessById(pid);
                    var allProcesses = Process.GetProcesses();

                    var childrenMap = new Dictionary<int, List<int>>();
                    foreach (var proc in allProcesses)
                    {
                        try
                        {
                            int ppid = GetParentProcessId(proc.Id);
                            if (!childrenMap.ContainsKey(ppid))
                                childrenMap[ppid] = new List<int>();
                            childrenMap[ppid].Add(proc.Id);
                        }
                        catch { }
                    }

                    var allChildren = new List<int>();
                    var stack = new Stack<int>();
                    stack.Push(pid);

                    while (stack.Count > 0)
                    {
                        int current = stack.Pop();
                        if (childrenMap.TryGetValue(current, out var childList))
                        {
                            foreach (int child in childList)
                            {
                                if (child != current && !allChildren.Contains(child))
                                {
                                    allChildren.Add(child);
                                    stack.Push(child);
                                }
                            }
                        }
                    }

                    try
                    {
                        mainProc.Kill();
                        mainProc.WaitForExit(100);
                    }
                    catch { }

                    if (!mainProc.HasExited)
                    {
                        try
                        {
                            IntPtr hProcess = OpenProcess(PROCESS_TERMINATE, false, (uint)pid);
                            if (hProcess != IntPtr.Zero)
                            {
                                TerminateProcess(hProcess, 0);
                                CloseHandle(hProcess);
                            }
                        }
                        catch { }
                    }

                    if (!mainProc.HasExited)
                    {
                        try { mainProc.Close(); } catch { }
                    }

                    foreach (var childPid in allChildren)
                    {
                        try
                        {
                            var childProc = Process.GetProcessById(childPid);
                            childProc.Kill();
                            if (!childProc.WaitForExit(500))
                            {
                                IntPtr hProcess = OpenProcess(PROCESS_TERMINATE, false, (uint)childPid);
                                if (hProcess != IntPtr.Zero)
                                {
                                    TerminateProcess(hProcess, 0);
                                    CloseHandle(hProcess);
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }

        public void KillProcess(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
                proc.WaitForExit(2000);
            }
            catch { }
        }

        public void SuspendProcess(int pid)
        {
            try
            {
                IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, (uint)pid);
                if (hProcess != IntPtr.Zero)
                {
                    NtSuspendProcess(hProcess);
                    CloseHandle(hProcess);
                }
            }
            catch { }
        }

        public void ResumeProcess(int pid)
        {
            try
            {
                IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, (uint)pid);
                if (hProcess != IntPtr.Zero)
                {
                    NtResumeProcess(hProcess);
                    CloseHandle(hProcess);
                }
            }
            catch { }
        }

        public void OpenFileLocation(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                string fileName = proc.MainModule?.FileName;
                if (!string.IsNullOrEmpty(fileName) && System.IO.File.Exists(fileName))
                {
                    string argument = "/select, \"" + fileName + "\"";
                    Process.Start("explorer.exe", argument);
                }
            }
            catch { }
        }

        private int GetParentProcessId(int pid)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = " + pid))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return Convert.ToInt32(obj["ParentProcessId"]);
                    }
                }
            }
            catch { }
            return 0;
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