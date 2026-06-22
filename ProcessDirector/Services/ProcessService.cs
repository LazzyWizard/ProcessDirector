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
        private static Dictionary<int, ImageSource> _iconCache = new Dictionary<int, ImageSource>();
        private static Dictionary<int, ProcessInfo> _processMap = new Dictionary<int, ProcessInfo>();
        private static List<ProcessInfo> _allProcesses = new List<ProcessInfo>();
        private static object _lockObject = new object();
        private static System.Timers.Timer _updateTimer;
        private static bool _isInitialized = false;
        private static Dictionary<int, float> _cpuCache = new Dictionary<int, float>();
        private static Dictionary<int, DateTime> _cpuTimeCache = new Dictionary<int, DateTime>();
        private static Dictionary<int, TimeSpan> _cpuTotalCache = new Dictionary<int, TimeSpan>();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

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

        private float GetProcessCpuUsageFast(Process proc)
        {
            try
            {
                int pid = proc.Id;
                TimeSpan currentTotal = proc.TotalProcessorTime;
                DateTime currentTime = DateTime.Now;

                lock (_cpuTotalCache)
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
                return proc.MainWindowHandle != IntPtr.Zero && IsWindowVisible(proc.MainWindowHandle);
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
                string name = proc.ProcessName.ToLower();
                string[] excluded = { "svchost", "csrss", "services", "lsass", "winlogon", "system",
                    "smss", "wininit", "spoolsv", "dwm", "taskhost", "runtimebroker", "ctfmon", "sihost",
                    "explorer", "systemsettings", "searchui", "nvidia", "textinputhost", "applicationframehost" };

                foreach (var ex in excluded)
                    if (name.Contains(ex)) return false;

                if (HasVisibleWindow(proc))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool IsSystemProcess(Process proc)
        {
            try
            {
                string name = proc.ProcessName.ToLower();
                string[] systemNames = { "svchost", "csrss", "services", "lsass", "winlogon", "system",
                    "smss", "wininit", "spoolsv", "dwm", "taskhost", "runtimebroker", "ctfmon", "sihost" };

                if (systemNames.Contains(name)) return true;

                string exePath = proc.MainModule?.FileName?.ToLower() ?? "";
                if (exePath.Contains("system32") || exePath.Contains("syswow64")) return true;
            }
            catch { }
            return false;
        }

        private ProcessDisplayCategory GetDisplayCategory(Process proc)
        {
            if (IsSystemProcess(proc)) return ProcessDisplayCategory.Windows;
            if (IsRealApplication(proc)) return ProcessDisplayCategory.Apps;
            return ProcessDisplayCategory.Background;
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

                        var processInfo = CreateProcessInfo(proc);
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

        private ProcessInfo CreateProcessInfo(Process proc)
        {
            string name = proc.ProcessName;
            return new ProcessInfo
            {
                Id = proc.Id,
                Name = name,
                MemoryUsage = GetProcessMemory(proc),
                CpuUsage = 0,
                Icon = GetProcessIcon(proc),
                DisplayCategory = GetDisplayCategory(proc),
                ProcessBaseName = name
            };
        }

        private void UpdateProcessesDataIncremental()
        {
            try
            {
                var runningProcesses = GetSafeProcesses();
                var newProcessIds = new HashSet<int>();
                var updatedProcesses = new List<ProcessInfo>();

                foreach (var proc in runningProcesses)
                {
                    try
                    {
                        string name = proc.ProcessName;
                        if (string.IsNullOrEmpty(name) || name == "Idle") continue;

                        int pid = proc.Id;
                        newProcessIds.Add(pid);
                        float cpu = GetProcessCpuUsageFast(proc);
                        long memory = GetProcessMemory(proc);

                        if (_processMap.ContainsKey(pid))
                        {
                            var existing = _processMap[pid];
                            existing.CpuUsage = cpu;
                            existing.MemoryUsage = memory;
                            updatedProcesses.Add(existing);
                        }
                        else
                        {
                            var newProcess = CreateProcessInfo(proc);
                            newProcess.CpuUsage = cpu;
                            newProcess.MemoryUsage = memory;
                            _processMap[pid] = newProcess;
                            updatedProcesses.Add(newProcess);
                        }
                    }
                    catch { }
                }

                var deadProcessIds = _processMap.Keys
                    .Where(pid => !newProcessIds.Contains(pid))
                    .ToList();

                foreach (var pid in deadProcessIds)
                {
                    _processMap.Remove(pid);
                    _cpuCache.Remove(pid);
                    _cpuTotalCache.Remove(pid);
                    _cpuTimeCache.Remove(pid);
                }

                lock (_lockObject)
                {
                    _allProcesses = _processMap.Values
                        .OrderByDescending(p => p.MemoryUsage)
                        .ToList();
                }

                ProcessesUpdated?.Invoke(_allProcesses);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Update error: " + ex.Message);
            }
        }

        private ImageSource GetProcessIcon(Process proc)
        {
            try
            {
                if (_iconCache.ContainsKey(proc.Id)) return _iconCache[proc.Id];
                var icon = IconHelper.GetProcessIcon(proc);
                if (icon != null) _iconCache[proc.Id] = icon;
                return icon;
            }
            catch { return null; }
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