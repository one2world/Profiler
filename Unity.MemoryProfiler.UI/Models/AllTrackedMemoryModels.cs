using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// All Tracked Memory 页面的完整数据模型
    /// 参考: AllTrackedMemoryModel
    /// </summary>
    public class AllTrackedMemoryData
    {
        /// <summary>
        /// 快照总内存大小 (bytes)
        /// </summary>
        public ulong TotalSnapshotMemory { get; set; }

        /// <summary>
        /// 表格中显示的总内存大小 (bytes)
        /// Allocated Memory In Table
        /// </summary>
        public ulong TotalMemoryInTable { get; set; }

        /// <summary>
        /// Graphics 内存大小 (bytes)
        /// </summary>
        public ulong TotalGraphicsMemory { get; set; }

        /// <summary>
        /// 快照总内存大小 (MB)
        /// </summary>
        public double TotalSnapshotMemoryMB => TotalSnapshotMemory / (1024.0 * 1024.0);

        /// <summary>
        /// 表格总内存大小 (MB)
        /// </summary>
        public double TotalMemoryInTableMB => TotalMemoryInTable / (1024.0 * 1024.0);

        /// <summary>
        /// Graphics 内存大小 (MB)
        /// </summary>
        public double TotalGraphicsMemoryMB => TotalGraphicsMemory / (1024.0 * 1024.0);

        /// <summary>
        /// 格式化的表格总内存
        /// </summary>
        public string FormattedTotalMemoryInTable => FormatBytes(TotalMemoryInTable);

        /// <summary>
        /// 树形节点列表（根节点）
        /// </summary>
        public List<AllTrackedMemoryTreeNode> RootNodes { get; set; } = new();

        /// <summary>
        /// 递归排序树形结构
        /// 参考: Unity TreeModel.Sort (使用栈遍历，避免递归调用)
        /// </summary>
        public void Sort(string sortBy, System.ComponentModel.ListSortDirection direction)
        {
            if (RootNodes == null || RootNodes.Count == 0)
                return;

            // 创建比较函数
            System.Comparison<AllTrackedMemoryTreeNode> comparison = sortBy switch
            {
                "Name" => (x, y) => string.Compare(x.Name, y.Name, System.StringComparison.OrdinalIgnoreCase),
                "AllocatedSize" => (x, y) => x.AllocatedSize.CompareTo(y.AllocatedSize),
                "ResidentSize" => (x, y) => x.ResidentSize.CompareTo(y.ResidentSize),
                "Percentage" => (x, y) => x.Percentage.CompareTo(y.Percentage),
                _ => (x, y) => x.AllocatedSize.CompareTo(y.AllocatedSize)  // 默认按分配大小
            };

            // 应用排序方向
            if (direction == System.ComponentModel.ListSortDirection.Descending)
            {
                var originalComparison = comparison;
                comparison = (x, y) => originalComparison(y, x);  // 反转
            }

            // 排序根节点
            RootNodes.Sort(comparison);

            // 使用栈递归排序所有子节点（参考Unity实现）
            var stack = new Stack<AllTrackedMemoryTreeNode>(RootNodes);
            while (stack.Count > 0)
            {
                var item = stack.Pop();
                if (item.Children != null && item.Children.Count > 0)
                {
                    foreach (var child in item.Children)
                        stack.Push(child);

                    item.Children.Sort(comparison);
                }
            }
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes >= 1024UL * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            if (bytes >= 1024UL * 1024)
                return $"{bytes / (1024.0 * 1024):F2} MB";
            if (bytes >= 1024UL)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} B";
        }
    }

    /// <summary>
    /// All Tracked Memory 树形节点
    /// 参考: TreeViewItemData&lt;AllTrackedMemoryModel.ItemData&gt;
    /// 
    /// Unity 的 All Of Memory 显示所有跟踪的内存，包括：
    /// - Native (Native Objects, Unity Subsystems, Reserved)
    /// - Managed (Managed Objects, Virtual Machine, Reserved)
    /// - Graphics (Graphics Resources, Reserved)
    /// - Executables & Mapped
    /// - Untracked
    /// </summary>
    public class AllTrackedMemoryTreeNode : ITreeNode
    {
        /// <summary>
        /// 节点 ID（用于 TreeView）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 节点名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 内存类别（用于详情面板显示描述）
        /// 参考: Unity的TreeViewItemData的itemId，来自IAnalysisViewSelectable.Category
        /// </summary>
        public CategoryType Category { get; set; } = CategoryType.None;

        /// <summary>
        /// Allocated Size (Committed memory)
        /// </summary>
        public ulong AllocatedSize { get; set; }

        /// <summary>
        /// Resident Size (Physical memory)
        /// </summary>
        public ulong ResidentSize { get; set; }

        /// <summary>
        /// 子节点数量（用于显示）
        /// </summary>
        public int ChildCount { get; set; }

        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<AllTrackedMemoryTreeNode>? Children { get; set; }

        /// <summary>
        /// 占总内存的百分比（基于 Allocated Size）
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// 是否是分组节点
        /// </summary>
        public bool IsGroupNode => Children != null && Children.Count > 0;

        /// <summary>
        /// 信息不可靠（用于估算值）
        /// </summary>
        public bool Unreliable { get; set; }

        /// <summary>
        /// 内部源索引（用于引用）
        /// </summary>
        internal Unity.MemoryProfiler.Editor.CachedSnapshot.SourceIndex Source { get; set; }

        // 实现ITreeNode接口
        public System.Collections.Generic.IEnumerable<object>? GetChildren() => Children;

        // 格式化属性
        public string FormattedAllocatedSize => FormatBytes(AllocatedSize);
        public string FormattedResidentSize => FormatBytes(ResidentSize);
        public string FormattedPercentage => $"{Percentage * 100:F1}%";

        /// <summary>
        /// 显示名称（包含子项数量）
        /// 参考 Unity: "({childCount:N0} Item{((childCount > 1) ? "s" : string.Empty)})"
        /// 格式：Name (24) 或 Name (1,234)
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (ChildCount > 0)
                    return $"{Name} ({ChildCount:N0})";
                return Name;
            }
        }

        public double AllocatedSizeMB => AllocatedSize / (1024.0 * 1024.0);
        public double ResidentSizeMB => ResidentSize / (1024.0 * 1024.0);

        private static string FormatBytes(ulong bytes)
        {
            if (bytes >= 1024UL * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            if (bytes >= 1024UL * 1024)
                return $"{bytes / (1024.0 * 1024):F1} MB";
            if (bytes >= 1024UL)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}

