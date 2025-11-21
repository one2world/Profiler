using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Managed Objects 页面的完整数据模型
    /// 按调用栈聚合显示 Managed 对象的内存分配
    /// </summary>
    public class ManagedObjectsData
    {
        /// <summary>
        /// 总内存大小 (bytes)
        /// </summary>
        public ulong TotalSize { get; set; }

        /// <summary>
        /// 总对象数量
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 树形节点列表（根节点）- 按调用栈聚合
        /// </summary>
        public List<ManagedCallStackNode> RootNodes { get; set; } = new();

        /// <summary>
        /// 格式化的总内存大小
        /// </summary>
        public string FormattedTotalSize => FormatBytes(TotalSize);

        private static string FormatBytes(ulong bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// Managed Objects 调用栈节点
    /// 表示一个调用栈及其聚合的内存信息
    /// </summary>
    public class ManagedCallStackNode : ITreeNode
    {
        /// <summary>
        /// 节点 ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取子节点（实现 ITreeNode 接口）
        /// </summary>
        public IEnumerable<object>? GetChildren() => Children;

        /// <summary>
        /// 描述（函数名）
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 分配大小 (bytes)
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// 占总内存的百分比
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// 对象个数
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// 模块名
        /// </summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>
        /// 堆栈Hash（用于查找完整调用栈）
        /// </summary>
        public uint? StackHash { get; set; }

        /// <summary>
        /// 完整的调用栈帧列表（用于详情显示）
        /// </summary>
        public List<string>? CallStackFrames { get; set; }

        /// <summary>
        /// 该调用栈下的所有对象地址列表（用于详情面板）
        /// </summary>
        public List<ulong> ObjectAddresses { get; set; } = new();

        /// <summary>
        /// 文件路径和行号列表（用于代码跳转）
        /// </summary>
        public List<AllocationSite> AllocationSites { get; set; } = new();

        /// <summary>
        /// 子节点（调用栈可以按层级展开）
        /// </summary>
        public List<ManagedCallStackNode>? Children { get; set; }

        /// <summary>
        /// 是否是分组节点
        /// </summary>
        public bool IsGroupNode => Children != null && Children.Count > 0;

        /// <summary>
        /// 格式化的大小
        /// </summary>
        public string FormattedSize => FormatBytes(Size);

        /// <summary>
        /// 格式化的百分比
        /// </summary>
        public string FormattedPercentage => $"{Percentage:F2}%";

        private static string FormatBytes(ulong bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// 分配站点信息（文件路径和行号）
    /// </summary>
    public class AllocationSite
    {
        /// <summary>
        /// 描述（通常是函数名）
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 行号
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// 分配大小
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// 格式化的文件位置
        /// </summary>
        public string FileLocation => LineNumber > 0 ? $"{FilePath}:{LineNumber}" : FilePath;

        /// <summary>
        /// 格式化的大小
        /// </summary>
        public string FormattedSize => FormatBytes(Size);

        private static string FormatBytes(ulong bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// Managed 对象详情节点（按类型分组）
    /// 用于详情面板显示选中调用栈下的对象列表
    /// </summary>
    public class ManagedObjectDetailNode : ITreeNode
    {
        /// <summary>
        /// 节点 ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取子节点（实现 ITreeNode 接口）
        /// </summary>
        public IEnumerable<object>? GetChildren() => Children;

        /// <summary>
        /// 类型名称或对象名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 大小 (bytes)
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// 对象数量（类型分组节点）
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// 对象地址（单个对象节点）
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// 类型索引
        /// </summary>
        public int TypeIndex { get; set; } = -1;

        /// <summary>
        /// Managed 对象索引
        /// </summary>
        public int ManagedObjectIndex { get; set; } = -1;

        /// <summary>
        /// 是否是分组节点（类型分组）
        /// </summary>
        public bool IsGroup { get; set; }

        /// <summary>
        /// 子节点（类型下的具体对象）
        /// </summary>
        public List<ManagedObjectDetailNode>? Children { get; set; }

        /// <summary>
        /// 格式化的大小
        /// </summary>
        public string FormattedSize => FormatBytes(Size);

        private static string FormatBytes(ulong bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}

