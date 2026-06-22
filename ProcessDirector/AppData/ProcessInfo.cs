using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace ProcessDirector.AppData
{
    public enum ProcessDisplayCategory
    {
        Apps,
        Background,
        Windows
    }

    public class ProcessInfo : INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private float _cpuUsage;
        private long _memoryUsage;
        private ImageSource _icon;
        private ProcessDisplayCategory _displayCategory;
        private string _processBaseName;
        private string _executablePath;
        private List<ProcessInfo> _children;

        public int Id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged("Id"); }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged("Name"); }
        }

        public float CpuUsage
        {
            get { return _cpuUsage; }
            set
            {
                _cpuUsage = value;
                OnPropertyChanged("CpuUsage");
                OnPropertyChanged("CpuFormatted");
            }
        }

        public long MemoryUsage
        {
            get { return _memoryUsage; }
            set
            {
                _memoryUsage = value;
                OnPropertyChanged("MemoryUsage");
                OnPropertyChanged("MemoryFormatted");
            }
        }

        public string MemoryFormatted
        {
            get { return (MemoryUsage / 1024 / 1024) + " MB"; }
        }

        public string CpuFormatted
        {
            get { return CpuUsage.ToString("F1") + "%"; }
        }

        public ImageSource Icon
        {
            get { return _icon; }
            set { _icon = value; OnPropertyChanged("Icon"); }
        }

        public ProcessDisplayCategory DisplayCategory
        {
            get { return _displayCategory; }
            set { _displayCategory = value; OnPropertyChanged("DisplayCategory"); }
        }

        public string ProcessBaseName
        {
            get { return _processBaseName; }
            set { _processBaseName = value; OnPropertyChanged("ProcessBaseName"); }
        }

        public string ExecutablePath
        {
            get { return _executablePath; }
            set { _executablePath = value; OnPropertyChanged("ExecutablePath"); }
        }

        public List<ProcessInfo> Children
        {
            get { return _children; }
            set { _children = value; OnPropertyChanged("Children"); }
        }

        public ProcessInfo()
        {
            Children = new List<ProcessInfo>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}