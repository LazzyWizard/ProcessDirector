using System;
using System.Diagnostics;
using System.IO;
using System.Management;

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

        public event Action<string> NewLogEntry;
        public bool IsActive => _isActive;

        public LoggingService(string logDirectory)
        {
            _logDirectory = logDirectory;
            if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
        }

        public void UpdateLogDirectory(string newPath)
        {
            _logDirectory = newPath;
            if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
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

        private void OnProcessStart(object sender, EventArrivedEventArgs e)
        {
            if (!_isActive) return;
            try
            {
                string name = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
                int pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
                if (name.ToLower() == "processdirector") return;
                WriteLog($"[{DateTime.Now:HH:mm:ss}] START | {name} (PID: {pid})");
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
                WriteLog($"[{DateTime.Now:HH:mm:ss}] STOP | {name} (PID: {pid})");
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
                SetupWatchers();
                string fileName = $"ProcessDirector_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                _currentLogFilePath = Path.Combine(_logDirectory, fileName);
                _logWriter = new StreamWriter(_currentLogFilePath, true) { AutoFlush = true };

                WriteLog("============================================================");
                WriteLog($"SESSION START: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                WriteLog("============================================================");

                _isActive = true;
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
        }
    }
}