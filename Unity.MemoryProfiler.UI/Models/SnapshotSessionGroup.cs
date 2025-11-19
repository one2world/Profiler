using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Session分组模型
    /// 基于Unity官方的Session分组逻辑
    /// </summary>
    public class SnapshotSessionGroup : INotifyPropertyChanged
    {
        public string SessionName { get; set; } = "";
        public uint SessionGUID { get; set; }
        public ObservableCollection<SnapshotFileModel> Snapshots { get; set; } = new();

        /// <summary>
        /// Session中快照的数量
        /// </summary>
        public int Count => Snapshots.Count;

        /// <summary>
        /// 是否展开（UI状态）
        /// </summary>
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

