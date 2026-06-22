using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace ProcessDirector.Services
{
    public class NetworkMonitor : IDisposable
    {
        [DllImport("iphlpapi.dll")]
        private static extern int GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwSize,
            bool bOrder,
            int af,
            int tableClass,
            int reserved
        );

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint dwState;
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwRemoteAddr;
            public uint dwRemotePort;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
            public MIB_TCPROW_OWNER_PID table;
        }

        private const int AF_INET = 2;
        private const int TCP_TABLE_OWNER_PID_ALL = 5;

        private HashSet<string> _knownConnections = new HashSet<string>();
        private object _lockObject = new object();
        private bool _isActive = false;

        public event Action<string> NewConnectionDetected;

        public void Start()
        {
            lock (_lockObject)
            {
                _knownConnections.Clear();
                _isActive = true;
            }
        }

        public void Stop()
        {
            lock (_lockObject)
            {
                _isActive = false;
            }
        }

        public void Update()
        {
            if (!_isActive) return;

            try
            {
                var connections = GetTcpConnections();
                var currentKeys = new HashSet<string>();
                var newKeys = new List<string>();

                foreach (var conn in connections)
                {
                    if (conn.State != 5) continue;

                    string ip = ConvertUintToIp(conn.RemoteIp);
                    int port = (int)conn.RemotePort;
                    string key = $"{conn.Pid}|{ip}:{port}";

                    if (!currentKeys.Contains(key))
                        currentKeys.Add(key);

                    if (!_knownConnections.Contains(key) && !newKeys.Contains(key))
                        newKeys.Add(key);
                }

                foreach (var key in newKeys)
                {
                    _knownConnections.Add(key);

                    int pid = int.Parse(key.Split('|')[0]);
                    string ip = key.Split('|')[1].Split(':')[0];
                    int port = int.Parse(key.Split('|')[1].Split(':')[1]);

                    try
                    {
                        var proc = Process.GetProcessById(pid);
                        NewConnectionDetected?.Invoke(
                            $"[{DateTime.Now:HH:mm:ss}] CONNECT | {proc.ProcessName} (PID: {pid}) -> {ip}:{port} (ESTABLISHED)"
                        );
                    }
                    catch { }
                }

                lock (_lockObject)
                {
                    var toRemove = new List<string>();
                    foreach (var key in _knownConnections)
                    {
                        if (!currentKeys.Contains(key))
                            toRemove.Add(key);
                    }
                    foreach (var key in toRemove)
                    {
                        _knownConnections.Remove(key);
                    }
                }
            }
            catch { }
        }

        private List<TcpConnectionInfo> GetTcpConnections()
        {
            var result = new List<TcpConnectionInfo>();

            try
            {
                int size = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                if (size <= 0) return result;

                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    int hr = GetExtendedTcpTable(buffer, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                    if (hr != 0) return result;

                    MIB_TCPTABLE_OWNER_PID table = Marshal.PtrToStructure<MIB_TCPTABLE_OWNER_PID>(buffer);
                    int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                    IntPtr rowPtr = IntPtr.Add(buffer, 4);

                    for (int i = 0; i < table.dwNumEntries; i++)
                    {
                        var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                        result.Add(new TcpConnectionInfo
                        {
                            State = row.dwState,
                            LocalIp = row.dwLocalAddr,
                            LocalPort = row.dwLocalPort,
                            RemoteIp = row.dwRemoteAddr,
                            RemotePort = row.dwRemotePort,
                            Pid = (int)row.dwOwningPid
                        });
                        rowPtr = IntPtr.Add(rowPtr, rowSize);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch { }

            return result;
        }

        private string ConvertUintToIp(uint ip)
        {
            try
            {
                byte[] bytes = BitConverter.GetBytes(ip);
                return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
            }
            catch { return "0.0.0.0"; }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public class TcpConnectionInfo
    {
        public uint State { get; set; }
        public uint LocalIp { get; set; }
        public uint LocalPort { get; set; }
        public uint RemoteIp { get; set; }
        public uint RemotePort { get; set; }
        public int Pid { get; set; }
    }
}