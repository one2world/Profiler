using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 快照树节点包装类
    /// 用于DevExpress TreeListControl的分层数据绑定
    /// </summary>
    public class SnapshotTreeNode : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        
        /// <summary>
        /// 节点类型：Session或Snapshot
        /// </summary>
        public SnapshotNodeType NodeType { get; set; }
        
        /// <summary>
        /// Session数据（当NodeType=Session时）
        /// </summary>
        public SnapshotSessionGroup? SessionData { get; set; }
        
        /// <summary>
        /// Snapshot数据（当NodeType=Snapshot时）
        /// </summary>
        public SnapshotFileModel? SnapshotData { get; set; }
        
        // 显示属性（统一接口）
        public string Name => NodeType == SnapshotNodeType.Session 
            ? SessionData?.SessionName ?? "" 
            : SnapshotData?.Name ?? "";
        
        public string? DateFormatted => SnapshotData?.DateFormatted;
        public string? SizeFormatted => SnapshotData?.SizeFormatted;
        public int? Count => SessionData?.Count;
        
        public bool IsBase => SnapshotData?.IsBase ?? false;
        public bool IsCompared => SnapshotData?.IsCompared ?? false;
        
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

    public enum SnapshotNodeType
    {
        Session,
        Snapshot
    }
}

