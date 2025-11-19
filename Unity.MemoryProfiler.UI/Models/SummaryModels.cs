using System;
using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Summary 页面的完整数据模型
    /// </summary>
    public class SummaryData
    {
        public MemoryUsageOnDevice MemoryUsage { get; set; } = new();
        public AllocatedMemoryDistribution MemoryDistribution { get; set; } = new();
        public ManagedHeapUtilization HeapUtilization { get; set; } = new();
        public TopUnityObjectsCategories TopCategories { get; set; } = new();
        
        /// <summary>
        /// 是否为对比模式
        /// </summary>
        public bool CompareMode { get; set; }
    }

    /// <summary>
    /// 内存使用行（用于 GridControl）
    /// </summary>
    public class MemoryUsageRow
    {
        public string Name { get; set; } = string.Empty;
        public double SizeMB { get; set; }
        public double SizeMB_B { get; set; }
        public double DiffMB => SizeMB_B - SizeMB;
        public string FormattedSize { get; set; } = string.Empty;
        public string FormattedSize_B { get; set; } = string.Empty;
        public string FormattedDiff { get; set; } = string.Empty;
    }

    /// <summary>
    /// 设备内存使用情况
    /// </summary>
    public class MemoryUsageOnDevice
    {
        /// <summary>
        /// 总驻留内存 (MB) - Snapshot A
        /// </summary>
        public double TotalResidentMB { get; set; }

        /// <summary>
        /// 总分配内存 (MB) - Snapshot A
        /// </summary>
        public double TotalAllocatedMB { get; set; }

        /// <summary>
        /// 总驻留内存 (MB) - Snapshot B (对比模式)
        /// </summary>
        public double TotalResidentMB_B { get; set; }

        /// <summary>
        /// 总分配内存 (MB) - Snapshot B (对比模式)
        /// </summary>
        public double TotalAllocatedMB_B { get; set; }

        /// <summary>
        /// 驻留内存占比 (0-1)
        /// </summary>
        public double ResidentPercentage => TotalAllocatedMB > 0 ? TotalResidentMB / TotalAllocatedMB : 0;
        
        /// <summary>
        /// 驻留内存占比 (0-1) - Snapshot B
        /// </summary>
        public double ResidentPercentage_B => TotalAllocatedMB_B > 0 ? TotalResidentMB_B / TotalAllocatedMB_B : 0;

        /// <summary>
        /// 获取用于 GridControl 的行数据（对比模式）
        /// </summary>
        public List<MemoryUsageRow> Rows
        {
            get
            {
                return new List<MemoryUsageRow>
                {
                    new MemoryUsageRow
                    {
                        Name = "Total Resident",
                        SizeMB = TotalResidentMB,
                        SizeMB_B = TotalResidentMB_B,
                        FormattedSize = FormatBytes(TotalResidentMB * 1024 * 1024),
                        FormattedSize_B = FormatBytes(TotalResidentMB_B * 1024 * 1024),
                        FormattedDiff = FormatBytesWithSign((TotalResidentMB_B - TotalResidentMB) * 1024 * 1024)
                    },
                    new MemoryUsageRow
                    {
                        Name = "Total Allocated",
                        SizeMB = TotalAllocatedMB,
                        SizeMB_B = TotalAllocatedMB_B,
                        FormattedSize = FormatBytes(TotalAllocatedMB * 1024 * 1024),
                        FormattedSize_B = FormatBytes(TotalAllocatedMB_B * 1024 * 1024),
                        FormattedDiff = FormatBytesWithSign((TotalAllocatedMB_B - TotalAllocatedMB) * 1024 * 1024)
                    }
                };
            }
        }

        private static string FormatBytes(double bytes)
        {
            double absBytes = Math.Abs(bytes);
            if (absBytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024 * 1024):F2} GB";
            if (absBytes >= 1024 * 1024)
                return $"{bytes / (1024 * 1024):F1} MB";
            if (absBytes >= 1024)
                return $"{bytes / 1024:F1} KB";
            return $"{bytes:F0} B";
        }

        private static string FormatBytesWithSign(double bytes)
        {
            var sign = bytes >= 0 ? "+" : "-";
            var formatted = FormatBytes(Math.Abs(bytes));
            return $"{sign}{formatted}";
        }
    }

    /// <summary>
    /// 已分配内存分布
    /// </summary>
    public class AllocatedMemoryDistribution
    {
        /// <summary>
        /// 总分配内存 (MB)
        /// </summary>
        public double TotalAllocatedMB { get; set; }

        /// <summary>
        /// 各类别内存使用情况
        /// </summary>
        public List<MemoryCategory> Categories { get; set; } = new();
    }

    /// <summary>
    /// 内存类别
    /// 参考: Unity 的 MemorySummaryModel.Row
    /// </summary>
    public class MemoryCategory
    {
        /// <summary>
        /// 类别名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 内存大小 (MB) - Snapshot A
        /// </summary>
        public double SizeMB { get; set; }

        /// <summary>
        /// 内存大小 (MB) - Snapshot B (对比模式)
        /// </summary>
        public double SizeMB_B { get; set; }

        /// <summary>
        /// 显示颜色 (用于图表)
        /// </summary>
        public string Color { get; set; } = "#CCCCCC";

        /// <summary>
        /// 占比 (0-1) - Snapshot A
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// 占比 (0-1) - Snapshot B
        /// </summary>
        public double Percentage_B { get; set; }

        /// <summary>
        /// 类别描述（用于 Detail 面板）
        /// 参考: Unity 的 Row.Description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 文档 URL（用于 Detail 面板）
        /// 参考: Unity 的 Row.DocumentationUrl
        /// </summary>
        public string DocumentationUrl { get; set; } = string.Empty;

        /// <summary>
        /// 差值 (MB) = B - A
        /// </summary>
        public double DiffMB => SizeMB_B - SizeMB;

        /// <summary>
        /// 格式化的大小字符串 - Snapshot A
        /// </summary>
        public string FormattedSize => FormatBytes(SizeMB * 1024 * 1024);

        /// <summary>
        /// 格式化的大小字符串 - Snapshot B
        /// </summary>
        public string FormattedSize_B => FormatBytes(SizeMB_B * 1024 * 1024);

        /// <summary>
        /// 格式化的差值字符串
        /// 参考: Unity 的 MakeSizeCell 逻辑 - 负值显示 "-" 前缀，正值显示 "+" 前缀
        /// </summary>
        public string FormattedDiff
        {
            get
            {
                var diffBytes = DiffMB * 1024 * 1024;
                var sign = diffBytes >= 0 ? "+" : "-";
                var formatted = FormatBytes(Math.Abs(diffBytes), showSign: false);
                return $"{sign}{formatted}";
            }
        }

        private static string FormatBytes(double bytes, bool showSign = false)
        {
            string sign = showSign && bytes > 0 ? "+" : "";
            double absBytes = Math.Abs(bytes);
            
            if (absBytes >= 1024 * 1024 * 1024)
                return $"{sign}{bytes / (1024 * 1024 * 1024):F2} GB";
            if (absBytes >= 1024 * 1024)
                return $"{sign}{bytes / (1024 * 1024):F1} MB";
            if (absBytes >= 1024)
                return $"{sign}{bytes / 1024:F1} KB";
            return $"{sign}{bytes:F0} B";
        }
    }

    /// <summary>
    /// 托管堆利用率行（用于 GridControl）
    /// </summary>
    public class ManagedHeapRow
    {
        public string Color { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double SizeMB { get; set; }
        public double SizeMB_B { get; set; }
        public double DiffMB => SizeMB_B - SizeMB;
        public string FormattedSize { get; set; } = string.Empty;
        public string FormattedSize_B { get; set; } = string.Empty;
        public string FormattedDiff { get; set; } = string.Empty;
    }

    /// <summary>
    /// 托管堆利用率
    /// 参考: Unity 的 ManagedMemorySummaryModelBuilder
    /// </summary>
    public class ManagedHeapUtilization
    {
        /// <summary>
        /// 总托管堆大小 (MB) - Snapshot A
        /// </summary>
        public double TotalMB { get; set; }

        /// <summary>
        /// 总托管堆大小 (MB) - Snapshot B
        /// </summary>
        public double TotalMB_B { get; set; }

        /// <summary>
        /// 虚拟机内存 (MB) - Snapshot A
        /// </summary>
        public double VirtualMachineMB { get; set; }

        /// <summary>
        /// 虚拟机内存 (MB) - Snapshot B
        /// </summary>
        public double VirtualMachineMB_B { get; set; }

        /// <summary>
        /// 空闲堆空间 (MB) - Snapshot A
        /// </summary>
        public double EmptyHeapSpaceMB { get; set; }

        /// <summary>
        /// 空闲堆空间 (MB) - Snapshot B
        /// </summary>
        public double EmptyHeapSpaceMB_B { get; set; }

        /// <summary>
        /// 对象占用 (MB) - Snapshot A
        /// </summary>
        public double ObjectsMB { get; set; }

        /// <summary>
        /// 对象占用 (MB) - Snapshot B
        /// </summary>
        public double ObjectsMB_B { get; set; }

        /// <summary>
        /// 各部分占比 - Snapshot A
        /// </summary>
        public double VirtualMachinePercentage => TotalMB > 0 ? VirtualMachineMB / TotalMB : 0;
        public double EmptyHeapPercentage => TotalMB > 0 ? EmptyHeapSpaceMB / TotalMB : 0;
        public double ObjectsPercentage => TotalMB > 0 ? ObjectsMB / TotalMB : 0;

        /// <summary>
        /// 各部分占比 - Snapshot B
        /// </summary>
        public double VirtualMachinePercentage_B => TotalMB_B > 0 ? VirtualMachineMB_B / TotalMB_B : 0;
        public double EmptyHeapPercentage_B => TotalMB_B > 0 ? EmptyHeapSpaceMB_B / TotalMB_B : 0;
        public double ObjectsPercentage_B => TotalMB_B > 0 ? ObjectsMB_B / TotalMB_B : 0;

        /// <summary>
        /// 差值 (MB)
        /// </summary>
        public double TotalDiffMB => TotalMB_B - TotalMB;
        public double VirtualMachineDiffMB => VirtualMachineMB_B - VirtualMachineMB;
        public double EmptyHeapSpaceDiffMB => EmptyHeapSpaceMB_B - EmptyHeapSpaceMB;
        public double ObjectsDiffMB => ObjectsMB_B - ObjectsMB;

        /// <summary>
        /// 格式化的大小字符串
        /// </summary>
        public string FormattedTotal => FormatBytes(TotalMB * 1024 * 1024);
        public string FormattedTotal_B => FormatBytes(TotalMB_B * 1024 * 1024);
        public string FormattedTotalDiff => FormatBytesWithSign(TotalDiffMB * 1024 * 1024);

        public string FormattedVirtualMachine => FormatBytes(VirtualMachineMB * 1024 * 1024);
        public string FormattedVirtualMachine_B => FormatBytes(VirtualMachineMB_B * 1024 * 1024);
        public string FormattedVirtualMachineDiff => FormatBytesWithSign(VirtualMachineDiffMB * 1024 * 1024);

        public string FormattedEmptyHeapSpace => FormatBytes(EmptyHeapSpaceMB * 1024 * 1024);
        public string FormattedEmptyHeapSpace_B => FormatBytes(EmptyHeapSpaceMB_B * 1024 * 1024);
        public string FormattedEmptyHeapSpaceDiff => FormatBytesWithSign(EmptyHeapSpaceDiffMB * 1024 * 1024);

        public string FormattedObjects => FormatBytes(ObjectsMB * 1024 * 1024);
        public string FormattedObjects_B => FormatBytes(ObjectsMB_B * 1024 * 1024);
        public string FormattedObjectsDiff => FormatBytesWithSign(ObjectsDiffMB * 1024 * 1024);

        private static string FormatBytes(double bytes)
        {
            double absBytes = Math.Abs(bytes);
            if (absBytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024 * 1024):F2} GB";
            if (absBytes >= 1024 * 1024)
                return $"{bytes / (1024 * 1024):F1} MB";
            if (absBytes >= 1024)
                return $"{bytes / 1024:F1} KB";
            return $"{bytes:F0} B";
        }

        private static string FormatBytesWithSign(double bytes)
        {
            var sign = bytes >= 0 ? "+" : "-";
            var formatted = FormatBytes(Math.Abs(bytes));
            return $"{sign}{formatted}";
        }

        /// <summary>
        /// 获取用于 GridControl 的行数据（对比模式）
        /// 注意：必须是属性而不是方法，才能在 XAML 中绑定
        /// </summary>
        public List<ManagedHeapRow> Rows
        {
            get
            {
                return new List<ManagedHeapRow>
                {
                    new ManagedHeapRow
                    {
                        Color = "#00BCD4",
                        Name = "Virtual Machine",
                        SizeMB = VirtualMachineMB,
                        SizeMB_B = VirtualMachineMB_B,
                        FormattedSize = FormattedVirtualMachine,
                        FormattedSize_B = FormattedVirtualMachine_B,
                        FormattedDiff = FormattedVirtualMachineDiff
                    },
                    new ManagedHeapRow
                    {
                        Color = "#FFEB3B",
                        Name = "Empty Heap Space",
                        SizeMB = EmptyHeapSpaceMB,
                        SizeMB_B = EmptyHeapSpaceMB_B,
                        FormattedSize = FormattedEmptyHeapSpace,
                        FormattedSize_B = FormattedEmptyHeapSpace_B,
                        FormattedDiff = FormattedEmptyHeapSpaceDiff
                    },
                    new ManagedHeapRow
                    {
                        Color = "#4CAF50",
                        Name = "Objects",
                        SizeMB = ObjectsMB,
                        SizeMB_B = ObjectsMB_B,
                        FormattedSize = FormattedObjects,
                        FormattedSize_B = FormattedObjects_B,
                        FormattedDiff = FormattedObjectsDiff
                    }
                };
            }
        }
    }

    /// <summary>
    /// Unity 对象类别排行
    /// </summary>
    public class TopUnityObjectsCategories
    {
        /// <summary>
        /// 总大小 (MB)
        /// </summary>
        public double TotalMB { get; set; }

        /// <summary>
        /// 类别列表
        /// </summary>
        public List<UnityObjectCategory> Categories { get; set; } = new();
    }

    /// <summary>
    /// Unity 对象类别
    /// 参考: Unity 的 UnityObjectsMemorySummaryModelBuilder
    /// </summary>
    public class UnityObjectCategory
    {
        /// <summary>
        /// 类别名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 内存大小 (MB) - Snapshot A
        /// </summary>
        public double SizeMB { get; set; }

        /// <summary>
        /// 内存大小 (MB) - Snapshot B
        /// </summary>
        public double SizeMB_B { get; set; }

        /// <summary>
        /// 显示颜色
        /// </summary>
        public string Color { get; set; } = "#9B59B6";

        /// <summary>
        /// 占比 (0-1) - Snapshot A
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// 占比 (0-1) - Snapshot B
        /// </summary>
        public double Percentage_B { get; set; }

        /// <summary>
        /// 差值 (MB)
        /// </summary>
        public double DiffMB => SizeMB_B - SizeMB;

        /// <summary>
        /// 格式化的大小字符串 - Snapshot A
        /// </summary>
        public string FormattedSize => FormatBytes(SizeMB * 1024 * 1024);

        /// <summary>
        /// 格式化的大小字符串 - Snapshot B
        /// </summary>
        public string FormattedSize_B => FormatBytes(SizeMB_B * 1024 * 1024);

        /// <summary>
        /// 格式化的差值字符串
        /// </summary>
        public string FormattedDiff
        {
            get
            {
                var diffBytes = DiffMB * 1024 * 1024;
                var sign = diffBytes >= 0 ? "+" : "-";
                var formatted = FormatBytes(Math.Abs(diffBytes));
                return $"{sign}{formatted}";
            }
        }

        private static string FormatBytes(double bytes)
        {
            double absBytes = Math.Abs(bytes);
            if (absBytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024 * 1024):F2} GB";
            if (absBytes >= 1024 * 1024)
                return $"{bytes / (1024 * 1024):F1} MB";
            if (absBytes >= 1024)
                return $"{bytes / 1024:F1} KB";
            return $"{bytes:F0} B";
        }
    }
}

