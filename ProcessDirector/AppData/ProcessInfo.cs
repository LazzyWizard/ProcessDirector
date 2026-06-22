using System.Collections.Generic;
using System.Windows.Media;

namespace ProcessDirector.AppData
{
    public enum ProcessDisplayCategory
    {
        Apps,
        Background,
        Windows
    }

    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public float CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public string MemoryFormatted
        {
            get { return (MemoryUsage / 1024 / 1024) + " MB"; }
        }
        public string CpuFormatted
        {
            get { return CpuUsage.ToString("F1") + "%"; }
        }
        public ImageSource Icon { get; set; }
        public ProcessDisplayCategory DisplayCategory { get; set; }
        public string ProcessBaseName { get; set; }
        public List<ProcessInfo> Children { get; set; }

        public ProcessInfo()
        {
            Children = new List<ProcessInfo>();
        }
    }
}