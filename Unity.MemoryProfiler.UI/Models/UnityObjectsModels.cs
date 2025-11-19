using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Unity Objects 页面的完整数据模型
    /// 参考: UnityObjectsModel
    /// </summary>
    public class UnityObjectsData
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
        /// 快照总内存大小 (MB)
        /// </summary>
        public double TotalSnapshotMemoryMB => TotalSnapshotMemory / (1024.0 * 1024.0);

        /// <summary>
        /// 表格总内存大小 (MB)
        /// </summary>
        public double TotalMemoryInTableMB => TotalMemoryInTable / (1024.0 * 1024.0);

        /// <summary>
        /// 格式化的表格总内存
        /// </summary>
        public string FormattedTotalMemoryInTable => FormatBytes(TotalMemoryInTable);

        /// <summary>
        /// 树形节点列表（根节点）
        /// </summary>
        public List<UnityObjectTreeNode> RootNodes { get; set; } = new();

        /// <summary>
        /// 递归排序树形结构
        /// 参考: Unity TreeModel.Sort (使用栈遍历，避免递归调用)
        /// </summary>
        public void Sort(string sortBy, System.ComponentModel.ListSortDirection direction)
        {
            if (RootNodes == null || RootNodes.Count == 0)
                return;

            // 创建比较函数
            System.Comparison<UnityObjectTreeNode> comparison = sortBy switch
            {
                "Name" => (x, y) => string.Compare(x.Name, y.Name, System.StringComparison.OrdinalIgnoreCase),
                "TotalSize" => (x, y) => x.TotalSize.CompareTo(y.TotalSize),
                "NativeSize" => (x, y) => x.NativeSize.CompareTo(y.NativeSize),
                "ManagedSize" => (x, y) => x.ManagedSize.CompareTo(y.ManagedSize),
                "GpuSize" => (x, y) => x.GpuSize.CompareTo(y.GpuSize),
                "Percentage" => (x, y) => x.Percentage.CompareTo(y.Percentage),
                "ChildCount" => (x, y) => x.ChildCount.CompareTo(y.ChildCount),
                _ => (x, y) => x.TotalSize.CompareTo(y.TotalSize)  // 默认按总大小
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
            var stack = new System.Collections.Generic.Stack<UnityObjectTreeNode>(RootNodes);
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
    /// Unity Objects 树形节点
    /// 参考: TreeViewItemData<UnityObjectsModel.ItemData>
    /// 
    /// 重要：Unity 的实现中，每个节点都可以有子节点，形成多级树形结构：
    /// - 类型节点：包含该类型的所有对象作为子节点
    /// - 对象节点：可以是叶子节点，也可以有子节点（如重复项分组）
    /// - MonoBehaviour/ScriptableObject：父节点下按 Managed 类型分组，每个分组下是对象列表
    /// </summary>
    public class UnityObjectTreeNode : ITreeNode
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
        /// Native 内存大小 (bytes)
        /// </summary>
        public ulong NativeSize { get; set; }

        /// <summary>
        /// Managed 内存大小 (bytes)
        /// </summary>
        public ulong ManagedSize { get; set; }

        /// <summary>
        /// GPU 内存大小 (bytes)
        /// </summary>
        public ulong GpuSize { get; set; }

        /// <summary>
        /// 总内存大小 (bytes)
        /// </summary>
        public ulong TotalSize => NativeSize + ManagedSize + GpuSize;

        /// <summary>
        /// 子节点数量（用于显示）
        /// </summary>
        public int ChildCount { get; set; }

        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<UnityObjectTreeNode>? Children { get; set; }

        /// <summary>
        /// 占总内存的百分比
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// 是否是分组节点（类型分组、重复项分组等）
        /// </summary>
        public bool IsGroupNode => Children != null && Children.Count > 0;

        /// <summary>
        /// Native 类型索引（用于内部引用）
        /// </summary>
        internal int NativeTypeIndex { get; set; } = -1;

        /// <summary>
        /// Managed 类型索引（用于内部引用）
        /// </summary>
        internal int ManagedTypeIndex { get; set; } = -1;

        /// <summary>
        /// 对象索引（如果是对象节点）
        /// </summary>
        internal long ObjectIndex { get; set; } = -1;

        /// <summary>
        /// Instance ID（如果是对象节点）
        /// </summary>
        public int InstanceId { get; set; }

        /// <summary>
        /// Source Index（用于引用查找和详情面板）
        /// 参考: Unity的UnityObjectsModel.ItemData.Source
        /// </summary>
        internal Unity.MemoryProfiler.Editor.CachedSnapshot.SourceIndex Source { get; set; }

        // 实现ITreeNode接口
        public System.Collections.Generic.IEnumerable<object>? GetChildren() => Children;

        // 格式化属性
        public string FormattedTotalSize => FormatBytes(TotalSize);
        public string FormattedNativeSize => FormatBytes(NativeSize);
        public string FormattedManagedSize => FormatBytes(ManagedSize);
        public string FormattedGpuSize => FormatBytes(GpuSize);
        public string FormattedPercentage => $"{Percentage * 100:F1}%";

        public double TotalSizeMB => TotalSize / (1024.0 * 1024.0);
        public double NativeSizeMB => NativeSize / (1024.0 * 1024.0);
        public double ManagedSizeMB => ManagedSize / (1024.0 * 1024.0);
        public double GpuSizeMB => GpuSize / (1024.0 * 1024.0);

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
