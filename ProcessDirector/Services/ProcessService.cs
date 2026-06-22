using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using ProcessDirector.AppData;

namespace ProcessDirector.Service
{
    public class ProcessService : IDisposable
    {
        private static readonly Dictionary<string, ImageSource> _iconCacheByPath = new Dictionary<string, ImageSource>();
        private static readonly Dictionary<int, ProcessInfo> _processMap = new Dictionary<int, ProcessInfo>();
        private static List<ProcessInfo> _allProcesses = new List<ProcessInfo>();
        private static readonly object _lockObject = new object();
        private static readonly object _cpuLock = new object();
        private static System.Timers.Timer _updateTimer;
        private static bool _isInitialized = false;
        private static readonly Dictionary<int, float> _cpuCache = new Dictionary<int, float>();
        private static readonly Dictionary<int, DateTime> _cpuTimeCache = new Dictionary<int, DateTime>();
        private static readonly Dictionary<int, TimeSpan> _cpuTotalCache = new Dictionary<int, TimeSpan>();
        private static int _updateCounter = 0;

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public event Action<List<ProcessInfo>> ProcessesUpdated;
        public event Action<string> LoadingStatus;

        public ProcessService()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                StartUpdateTimer();
                Task.Run(() => LoadCurrentProcesses());
            }
        }

        private void StartUpdateTimer()
        {
            if (_updateTimer == null)
            {
                _updateTimer = new System.Timers.Timer(2000);
                _updateTimer.Elapsed += (s, e) => UpdateProcessesDataIncremental();
                _updateTimer.Start();
            }
        }

        private List<Process> GetSafeProcesses()
        {
            var result = new List<Process>();
            try
            {
                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        if (proc.Id > 4 && proc.Id != 0)
                        {
                            result.Add(proc);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }

        private long GetProcessMemory(Process proc)
        {
            try { return proc.WorkingSet64; }
            catch { return 0; }
        }

        private string GetExecutablePath(Process proc)
        {
            try
            {
                if (proc == null) return null;
                if (proc.Id == 0 || proc.Id == 4) return null;
                return proc.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        private float GetProcessCpuUsageFast(Process proc)
        {
            try
            {
                int pid = proc.Id;
                TimeSpan currentTotal = proc.TotalProcessorTime;
                DateTime currentTime = DateTime.Now;

                lock (_cpuLock)
                {
                    if (_cpuTotalCache.ContainsKey(pid) && _cpuTimeCache.ContainsKey(pid))
                    {
                        TimeSpan prevTotal = _cpuTotalCache[pid];
                        DateTime prevTime = _cpuTimeCache[pid];

                        double deltaMs = (currentTime - prevTime).TotalMilliseconds;
                        if (deltaMs > 100)
                        {
                            double deltaCpu = (currentTotal - prevTotal).TotalMilliseconds;
                            float cpuUsage = (float)((deltaCpu / deltaMs) * 100 / Environment.ProcessorCount);
                            cpuUsage = Math.Min(100, Math.Max(0, cpuUsage));

                            _cpuTotalCache[pid] = currentTotal;
                            _cpuTimeCache[pid] = currentTime;
                            _cpuCache[pid] = cpuUsage;
                            return cpuUsage;
                        }
                    }

                    _cpuTotalCache[pid] = currentTotal;
                    _cpuTimeCache[pid] = currentTime;

                    if (_cpuCache.ContainsKey(pid))
                        return _cpuCache[pid];
                }
            }
            catch { }
            return 0;
        }

        private bool HasVisibleWindow(Process proc)
        {
            try
            {
                int pid = proc.Id;
                bool found = false;

                EnumWindows((hWnd, lParam) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint windowPid);
                    if (windowPid == pid && IsWindowVisible(hWnd))
                    {
                        found = true;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

                return found;
            }
            catch
            {
                return false;
            }
        }

        private bool IsRealApplication(Process proc)
        {
            try
            {
                return HasVisibleWindow(proc);
            }
            catch
            {
                return false;
            }
        }

        private bool IsSystemProcess(Process proc, string exePath)
        {
            try
            {
                string name = proc.ProcessName.ToLower();
                string[] systemNames = { "svchost", "csrss", "services", "lsass", "winlogon", "system",
                    "smss", "wininit", "spoolsv", "dwm", "taskhost", "runtimebroker", "ctfmon", "sihost",
                    "explorer", "systemsettings", "searchui", "nvidia", "textinputhost", "applicationframehost" };

                if (systemNames.Contains(name)) return true;

                if (!string.IsNullOrEmpty(exePath) &&
                    (exePath.ToLower().Contains("system32") || exePath.ToLower().Contains("syswow64")))
                    return true;
            }
            catch { }
            return false;
        }

        private ProcessDisplayCategory GetDisplayCategory(Process proc, string exePath)
        {
            if (IsSystemProcess(proc, exePath)) return ProcessDisplayCategory.Windows;
            if (IsRealApplication(proc)) return ProcessDisplayCategory.Apps;
            return ProcessDisplayCategory.Background;
        }

        private ImageSource GetIconForProcess(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
                return null;

            if (_iconCacheByPath.TryGetValue(exePath, out ImageSource cached))
                return cached;

            try
            {
                var icon = IconHelper.GetProcessIcon(exePath);
                if (icon != null)
                {
                    _iconCacheByPath[exePath] = icon;
                    return icon;
                }
            }
            catch { }

            return null;
        }

        private void LoadCurrentProcesses()
        {
            try
            {
                LoadingStatus?.Invoke("Loading processes...");

                var processes = new List<ProcessInfo>();
                var runningProcesses = GetSafeProcesses();
                int total = runningProcesses.Count;
                int current = 0;

                foreach (var proc in runningProcesses)
                {
                    current++;
                    if (current % 30 == 0)
                        LoadingStatus?.Invoke("Loading: " + current + "/" + total);

                    try
                    {
                        string name = proc.ProcessName;
                        if (string.IsNullOrEmpty(name) || name == "Idle") continue;

                        string exePath = GetExecutablePath(proc);
                        var processInfo = CreateProcessInfo(proc, exePath);
                        processes.Add(processInfo);
                        _processMap[proc.Id] = processInfo;
                    }
                    catch { }
                }

                lock (_lockObject) { _allProcesses = processes; }
                ProcessesUpdated?.Invoke(_allProcesses);
                LoadingStatus?.Invoke("Loaded " + processes.Count + " processes");
            }
            catch (Exception ex)
            {
                LoadingStatus?.Invoke("Error: " + ex.Message);
            }
        }

        private ProcessInfo CreateProcessInfo(Process proc, string exePath)
        {
            string name = proc.ProcessName;
            var icon = GetIconForProcess(exePath);

            return new ProcessInfo
            {
                Id = proc.Id,
                Name = name,
                MemoryUsage = GetProcessMemory(proc),
                CpuUsage = 0,
                Icon = icon,
                DisplayCategory = GetDisplayCategory(proc, exePath),
                ProcessBaseName = name,
                ExecutablePath = exePath
            };
        }

        private void UpdateProcessesDataIncremental()
        {
            try
            {
                _updateCounter++;

                var runningProcesses = GetSafeProcesses();
                var newProcessIds = new HashSet<int>();

                bool calculateCpu = _updateCounter % 2 == 0;

                foreach (var proc in runningProcesses)
                {
                    try
                    {
                        string name = proc.ProcessName;
                        if (string.IsNullOrEmpty(name) || name == "Idle") continue;

                        int pid = proc.Id;
                        newProcessIds.Add(pid);

                        float cpu = calculateCpu ? GetProcessCpuUsageFast(proc) : 0;
                        long memory = GetProcessMemory(proc);
                        string exePath = GetExecutablePath(proc);
                        var category = GetDisplayCategory(proc, exePath);

                        if (_processMap.ContainsKey(pid))
                        {
                            var existing = _processMap[pid];
                            if (calculateCpu)
                                existing.CpuUsage = cpu;
                            existing.MemoryUsage = memory;
                            existing.DisplayCategory = category;
                            existing.ExecutablePath = exePath;
                        }
                        else
                        {
                            var newProcess = CreateProcessInfo(proc, exePath);
                            if (calculateCpu)
                                newProcess.CpuUsage = cpu;
                            newProcess.MemoryUsage = memory;
                            _processMap[pid] = newProcess;
                        }
                    }
                    catch { }
                }

                var deadProcessIds = new List<int>();
                foreach (var pid in _processMap.Keys)
                {
                    if (!newProcessIds.Contains(pid))
                        deadProcessIds.Add(pid);
                }

                foreach (var pid in deadProcessIds)
                {
                    _processMap.Remove(pid);
                    lock (_cpuLock)
                    {
                        _cpuCache.Remove(pid);
                        _cpuTotalCache.Remove(pid);
                        _cpuTimeCache.Remove(pid);
                    }
                }

                bool shouldSort = deadProcessIds.Count > 0;
                bool hasNewProcesses = false;

                foreach (var pid in newProcessIds)
                {
                    if (_processMap.ContainsKey(pid) && !_allProcesses.Any(p => p.Id == pid))
                    {
                        hasNewProcesses = true;
                        break;
                    }
                }

                if (shouldSort || hasNewProcesses || _allProcesses.Count == 0)
                {
                    lock (_lockObject)
                    {
                        _allProcesses = _processMap.Values
                            .OrderByDescending(p => p.MemoryUsage)
                            .ToList();
                    }
                }

                ProcessesUpdated?.Invoke(_allProcesses);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Update error: " + ex.Message);
            }
        }

        public List<ProcessInfo> GetProcesses()
        {
            lock (_lockObject) { return _allProcesses.ToList(); }
        }

        public bool KillProcess(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
                proc.WaitForExit(2000);
                return true;
            }
            catch { return false; }
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
        }
    }
}