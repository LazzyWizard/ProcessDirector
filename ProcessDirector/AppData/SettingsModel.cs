using System;
using System.Windows.Input;

namespace ProcessDirector.AppData
{
    public class SettingsModel
    {
        public string LogFolderPath { get; set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ProcessDirectorLogs");

        public bool SuperF4Enabled { get; set; } = true;
        public int SuperF4Modifiers { get; set; } = 6;
        public Key SuperF4Key { get; set; } = Key.F4;

        public bool RefreshHotkeyEnabled { get; set; } = false;
        public int RefreshModifiers { get; set; } = 0;
        public Key RefreshKey { get; set; } = Key.None;

        public bool StartLoggingHotkeyEnabled { get; set; } = false;
        public int StartLoggingModifiers { get; set; } = 0;
        public Key StartLoggingKey { get; set; } = Key.None;

        public bool StopLoggingHotkeyEnabled { get; set; } = false;
        public int StopLoggingModifiers { get; set; } = 0;
        public Key StopLoggingKey { get; set; } = Key.None;

        public bool KillProcessHotkeyEnabled { get; set; } = false;
        public int KillProcessModifiers { get; set; } = 0;
        public Key KillProcessKey { get; set; } = Key.None;

        public bool LogNetworkConnections { get; set; } = false;
    }
}