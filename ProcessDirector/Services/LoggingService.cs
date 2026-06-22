using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Timers;

namespace ProcessDirector.Services
{
    public class LoggingService : IDisposable
    {
        private bool _isActive = false;
        private string _logDirectory;
        private string _currentLogFilePath;
        private StreamWriter _logWriter;
        private ManagementEventWatcher _processStartWatcher;
        private ManagementEventWatcher _processStopWatcher;
        private Dictionary<int, DateTime> _processStartTimes = new Dictionary<int, DateTime>();
        private NetworkMonitor _networkMonitor;
        private Timer _networkTimer;
        private bool _logNetworkConnections = false;

        public event Action<string> NewLogEntry;
        public bool IsActive => _isActive;

        public LoggingService(string logDirectory, bool logNetworkConnections = false)
        {
            _logDirectory = logDirectory;
            if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
            _logNetworkConnections = logNetworkConnections;
            _networkMonitor = new NetworkMonitor();
            _networkMonitor.NewConnectionDetected += OnNewConnectionDetected;
        }

        public void UpdateLogDirectory(string newPath)
        {
            _logDirectory = newPath;
            if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
        }

        public void UpdateNetworkLogging(bool enabled)
        {
            _logNetworkConnections = enabled;
            if (!enabled)
            {
                _networkMonitor.Stop();
                _networkTimer?.Stop();
            }
        }

        private void OnNewConnectionDetected(string log)
        {
            WriteLog(log);
            NewLogEntry?.Invoke(log);
        }

        private void StartNetworkMonitoring()
        {
            if (!_logNetworkConnections) return;
            _networkMonitor.Start();
            if (_networkTimer == null)
            {
                _networkTimer = new Timer(3000);
                _networkTimer.Elapsed += (s, e) =>
                {
                    try { _networkMonitor.Update(); }
                    catch { }
                };
                _networkTimer.Start();
            }
        }

        private void StopNetworkMonitoring()
        {
            _networkMonitor.Stop();
            _networkTimer?.Stop();
        }

        private void SetupWatchers()
        {
            try
            {
                _processStartWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
                _processStartWatcher.EventArrived += OnProcessStart;
                _processStartWatcher.Start();

                _processStopWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
                _processStopWatcher.EventArrived += OnProcessStop;
                _processStopWatcher.Start();
            }
            catch (Exception ex) { Debug.WriteLine("Watchers error: " + ex.Message); }
        }

        private int GetParentProcessId(int pid)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
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

        private (string Name, int Pid) GetParentProcessInfo(int pid)
        {
            try
            {
                int parentPid = GetParentProcessId(pid);
                if (parentPid == 0) return ("System", 0);

                try
                {
                    var parentProc = Process.GetProcessById(parentPid);
                    return (parentProc.ProcessName, parentPid);
                }
                catch { return ("Unknown", parentPid); }
            }
            catch { return ("Unknown", 0); }
        }

        private void OnProcessStart(object sender, EventArrivedEventArgs e)
        {
            if (!_isActive) return;
            try
            {
                string name = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
                int pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
                if (name.ToLower() == "processdirector") return;

                _processStartTimes[pid] = DateTime.Now;

                var parentInfo = GetParentProcessInfo(pid);
                WriteLog($"[{DateTime.Now:HH:mm:ss}] START | {name} (PID: {pid}) [Parent: {parentInfo.Name} (PID: {parentInfo.Pid})]");
            }
            catch { }
        }

        private void OnProcessStop(object sender, EventArrivedEventArgs e)
        {
            if (!_isActive) return;
            try
            {
                string name = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
                int pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);

                if (_processStartTimes.TryGetValue(pid, out DateTime startTime))
                {
                    TimeSpan duration = DateTime.Now - startTime;
                    WriteLog($"[{DateTime.Now:HH:mm:ss}] STOP | {name} (PID: {pid}) [Duration: {duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}]");
                    _processStartTimes.Remove(pid);
                }
                else
                {
                    WriteLog($"[{DateTime.Now:HH:mm:ss}] STOP | {name} (PID: {pid})");
                }
            }
            catch { }
        }

        private void WriteLog(string message)
        {
            try
            {
                _logWriter?.WriteLine(message);
                NewLogEntry?.Invoke(message);
            }
            catch { }
        }

        public bool StartSession()
        {
            try
            {
                _processStartTimes.Clear();
                SetupWatchers();
                string fileName = $"ProcessDirector_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                _currentLogFilePath = Path.Combine(_logDirectory, fileName);
                _logWriter = new StreamWriter(_currentLogFilePath, true) { AutoFlush = true };

                WriteLog("============================================================");
                WriteLog($"SESSION START: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                WriteLog("============================================================");

                _isActive = true;
                StartNetworkMonitoring();
                return true;
            }
            catch (Exception ex) { Debug.WriteLine("Start error: " + ex.Message); return false; }
        }

        public bool StopSession()
        {
            try
            {
                WriteLog("============================================================");
                WriteLog($"SESSION STOP: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                WriteLog("============================================================");
                _logWriter?.Close();
                _isActive = false;
                _processStartTimes.Clear();
                StopNetworkMonitoring();
                return true;
            }
            catch { return false; }
        }

        public bool LogEvent(string eventType, string processName = null, int? pid = null)
        {
            if (!_isActive) return false;
            WriteLog($"[{DateTime.Now:HH:mm:ss}] {eventType} | {processName} (PID: {pid})");
            return true;
        }

        public string GetCurrentLogFilePath() => _currentLogFilePath;
        public string GetLogDirectory() => _logDirectory;

        public bool OpenLogDirectory()
        {
            try { Process.Start("explorer.exe", _logDirectory); return true; }
            catch { return false; }
        }

        public void Dispose()
        {
            _logWriter?.Close();
            _processStartWatcher?.Stop();
            _processStopWatcher?.Stop();
            _networkTimer?.Stop();
            _networkMonitor?.Dispose();
        }
    }
}