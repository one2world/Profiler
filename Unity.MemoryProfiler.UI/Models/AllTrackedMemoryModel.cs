using System;
using System.Collections.ObjectModel;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// AllTrackedMemory数据模型
    /// 对应Unity的AllTrackedMemoryModel
    /// 表示完整的内存分组树：Native/Managed/Graphics/Executables/Untracked
    /// </summary>
    internal class AllTrackedMemoryModel : TreeModel<MemoryItemData>
    {
        private long _totalMemorySize;
        private long _totalGraphicsMemorySize;
        private long _totalSnapshotMemorySize;
        private Action<SourceIndex> _selectionProcessor;

        public AllTrackedMemoryModel()
            : base()
        {
        }

        public AllTrackedMemoryModel(
            ObservableCollection<TreeNode<MemoryItemData>> rootNodes,
            long totalMemorySize,
            long totalGraphicsMemorySize,
            long totalSnapshotMemorySize,
            Action<SourceIndex> selectionProcessor = null)
            : base(rootNodes)
        {
            _totalMemorySize = totalMemorySize;
            _totalGraphicsMemorySize = totalGraphicsMemorySize;
            _totalSnapshotMemorySize = totalSnapshotMemorySize;
            _selectionProcessor = selectionProcessor;
        }

        /// <summary>
        /// 总内存大小（包含Graphics）
        /// </summary>
        public long TotalMemorySize
        {
            get => _totalMemorySize;
            set => SetProperty(ref _totalMemorySize, value);
        }

        /// <summary>
        /// Graphics内存总大小
        /// </summary>
        public long TotalGraphicsMemorySize
        {
            get => _totalGraphicsMemorySize;
            set => SetProperty(ref _totalGraphicsMemorySize, value);
        }

        /// <summary>
        /// 快照报告的总内存大小
        /// </summary>
        public long TotalSnapshotMemorySize
        {
            get => _totalSnapshotMemorySize;
            set => SetProperty(ref _totalSnapshotMemorySize, value);
        }

        /// <summary>
        /// 选择处理器（当用户选择某项时调用）
        /// </summary>
        public Action<SourceIndex> SelectionProcessor
        {
            get => _selectionProcessor;
            set => SetProperty(ref _selectionProcessor, value);
        }

        /// <summary>
        /// 格式化的总内存大小
        /// </summary>
        public string TotalMemorySizeFormatted => MemoryItemData.FormatBytes(TotalMemorySize);

        /// <summary>
        /// 格式化的Graphics内存大小
        /// </summary>
        public string TotalGraphicsMemorySizeFormatted => MemoryItemData.FormatBytes(TotalGraphicsMemorySize);

        /// <summary>
        /// 格式化的快照总内存大小
        /// </summary>
        public string TotalSnapshotMemorySizeFormatted => MemoryItemData.FormatBytes(TotalSnapshotMemorySize);

        /// <summary>
        /// 查找特定分组的根节点
        /// </summary>
        public TreeNode<MemoryItemData> FindRootGroup(string groupName)
        {
            return FindFirst(node => 
                node.Parent == null && 
                string.Equals(node.Data?.Name, groupName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取Native分组
        /// </summary>
        public TreeNode<MemoryItemData> NativeGroup => FindRootGroup("Native");

        /// <summary>
        /// 获取Managed分组
        /// </summary>
        public TreeNode<MemoryItemData> ManagedGroup => FindRootGroup("Managed");

        /// <summary>
        /// 获取Graphics分组
        /// </summary>
        public TreeNode<MemoryItemData> GraphicsGroup => FindRootGroup("Graphics");

        /// <summary>
        /// 获取Executables分组
        /// </summary>
        public TreeNode<MemoryItemData> ExecutablesGroup => FindRootGroup("Executables & Mapped");

        /// <summary>
        /// 获取Untracked分组
        /// </summary>
        public TreeNode<MemoryItemData> UntrackedGroup => FindRootGroup("Untracked");

        public override string ToString()
        {
            return $"AllTrackedMemory: {TotalMemorySizeFormatted} ({RootNodes.Count} groups)";
        }
    }
}

