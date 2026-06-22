using System;
using System.Diagnostics;
using System.Management;
using OpenHardwareMonitor.Hardware;

namespace ProcessDirector.Services
{
    public class TemperatureService : IDisposable
    {
        private Computer _computer;
        private readonly object _lockObject = new object();
        private DateTime _lastUpdate = DateTime.MinValue;
        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1);

        public float CpuTemperature { get; private set; } = 0;
        public float CpuLoad { get; private set; } = 0;
        public float GpuTemperature { get; private set; } = 0;
        public float GpuLoad { get; private set; } = 0;
        public float RamUsage { get; private set; } = 0;
        public float RamUsedGB { get; private set; } = 0;
        public float RamTotalGB { get; private set; } = 0;
        public bool IsHardwareAvailable { get; private set; } = false;

        public TemperatureService()
        {
            try
            {
                _computer = new Computer { CPUEnabled = true, GPUEnabled = true };
                _computer.Open();
                IsHardwareAvailable = true;
            }
            catch (Exception ex) { Debug.WriteLine("OHM error: " + ex.Message); }

            RamTotalGB = GetTotalRamInGB();
            UpdateRamData();
        }

        public void Update()
        {
            UpdateRamData();
            if (!IsHardwareAvailable) return;

            lock (_lockObject)
            {
                if (DateTime.Now - _lastUpdate < _updateInterval) return;
                _lastUpdate = DateTime.Now;

                try
                {
                    _computer.Accept(new UpdateVisitor());
                    foreach (var hardware in _computer.Hardware)
                    {
                        hardware.Update();
                        if (hardware.HardwareType == HardwareType.CPU) UpdateCpuData(hardware);
                        else if (hardware.HardwareType == HardwareType.GpuNvidia ||
                                 hardware.HardwareType == HardwareType.GpuAti)
                            UpdateGpuData(hardware);
                    }
                }
                catch (Exception ex) { Debug.WriteLine("Update error: " + ex.Message); }
            }
        }

        private void UpdateCpuData(IHardware hardware)
        {
            float temp = 0, load = 0;
            int tempCount = 0;

            foreach (var sensor in hardware.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                if (sensor.SensorType == SensorType.Temperature &&
                    (sensor.Name.Contains("CPU") || sensor.Name.Contains("Package")))
                {
                    temp += sensor.Value.Value;
                    tempCount++;
                }
                else if (sensor.SensorType == SensorType.Load &&
                         sensor.Name.Contains("CPU Total"))
                    load = sensor.Value.Value;
            }
            if (tempCount > 0) CpuTemperature = temp / tempCount;
            if (load > 0) CpuLoad = load;
        }

        private void UpdateGpuData(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                if (sensor.SensorType == SensorType.Temperature &&
                    sensor.Name.Contains("GPU"))
                    GpuTemperature = sensor.Value.Value;
                else if (sensor.SensorType == SensorType.Load &&
                         sensor.Name.Contains("GPU Core"))
                    GpuLoad = sensor.Value.Value;
            }
        }

        private void UpdateRamData()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        float totalKB = Convert.ToSingle(obj["TotalVisibleMemorySize"]);
                        float freeKB = Convert.ToSingle(obj["FreePhysicalMemory"]);
                        float usedKB = totalKB - freeKB;
                        RamTotalGB = totalKB / 1024 / 1024;
                        RamUsedGB = usedKB / 1024 / 1024;
                        RamUsage = (usedKB / totalKB) * 100;
                        if (RamUsage > 100) RamUsage = 100;
                        break;
                    }
                }
            }
            catch { }
        }

        private float GetTotalRamInGB()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                        return Convert.ToSingle(obj["TotalVisibleMemorySize"]) / 1024 / 1024;
                }
            }
            catch { }
            return 16f;
        }

        public void Dispose() => _computer?.Close();
    }

    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware) { hardware.Update(); foreach (var sub in hardware.SubHardware) sub.Accept(this); }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}