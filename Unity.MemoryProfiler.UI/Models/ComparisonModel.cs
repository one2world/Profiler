using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// Comparison模型 - 用于对比两个快照的内存差异
    /// Unity官方实现参考：ComparisonTableModel.cs
    /// </summary>
    public class ComparisonModel : INotifyPropertyChanged
    {
        public ComparisonModel(
            ObservableCollection<ComparisonTreeNode> rootNodes,
            ulong totalSnapshotSizeA,
            ulong totalSnapshotSizeB,
            long largestAbsoluteSizeDelta)
        {
            RootNodes = rootNodes;
            TotalSnapshotSizeA = totalSnapshotSizeA;
            TotalSnapshotSizeB = totalSnapshotSizeB;
            LargestAbsoluteSizeDelta = largestAbsoluteSizeDelta;

            // 计算总大小
            ulong totalSizeA = 0;
            ulong totalSizeB = 0;
            foreach (var node in rootNodes)
            {
                totalSizeA += node.TotalSizeInA;
                totalSizeB += node.TotalSizeInB;
            }
            TotalSizeA = totalSizeA;
            TotalSizeB = totalSizeB;
        }

        /// <summary>
        /// 根节点列表
        /// </summary>
        public ObservableCollection<ComparisonTreeNode> RootNodes { get; }

        /// <summary>
        /// 模型中A的总大小（字节）
        /// </summary>
        public ulong TotalSizeA { get; }

        /// <summary>
        /// 模型中B的总大小（字节）
        /// </summary>
        public ulong TotalSizeB { get; }

        /// <summary>
        /// A快照的总大小（字节）
        /// </summary>
        public ulong TotalSnapshotSizeA { get; }

        /// <summary>
        /// B快照的总大小（字节）
        /// </summary>
        public ulong TotalSnapshotSizeB { get; }

        /// <summary>
        /// 最大绝对大小差异（字节）
        /// </summary>
        public long LargestAbsoluteSizeDelta { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Comparison树节点 - 表示一个可比较的内存项
    /// Unity官方实现参考：ComparisonTableModel.ComparisonData
    /// </summary>
    public class ComparisonTreeNode : INotifyPropertyChanged
    {
        public ComparisonTreeNode(
            string name,
            ulong totalSizeInA,
            ulong totalSizeInB,
            uint countInA,
            uint countInB)
        {
            Name = name;
            TotalSizeInA = totalSizeInA;
            TotalSizeInB = totalSizeInB;
            CountInA = countInA;
            CountInB = countInB;

            // 计算差异
            SizeDelta = (long)totalSizeInB - (long)totalSizeInA;
            CountDelta = (int)countInB - (int)countInA;
            HasChanged = (totalSizeInA != totalSizeInB) || (countInA != countInB);

            // 格式化字符串
            SizeDeltaFormatted = FormatSizeDelta(SizeDelta);
            TotalSizeInAFormatted = FormatBytes(totalSizeInA);
            TotalSizeInBFormatted = FormatBytes(totalSizeInB);

            // 颜色编码
            DeltaColor = GetDeltaColor(SizeDelta);

            Children = new ObservableCollection<ComparisonTreeNode>();
        }

        /// <summary>
        /// 项名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 大小差异（B - A），单位：字节
        /// </summary>
        public long SizeDelta { get; }

        /// <summary>
        /// A中的总大小（包括子项），单位：字节
        /// </summary>
        public ulong TotalSizeInA { get; }

        /// <summary>
        /// B中的总大小（包括子项），单位：字节
        /// </summary>
        public ulong TotalSizeInB { get; }

        /// <summary>
        /// A中的数量
        /// </summary>
        public uint CountInA { get; }

        /// <summary>
        /// B中的数量
        /// </summary>
        public uint CountInB { get; }

        /// <summary>
        /// 数量差异（B - A）
        /// </summary>
        public int CountDelta { get; }

        /// <summary>
        /// 是否有变化
        /// </summary>
        public bool HasChanged { get; }

        /// <summary>
        /// 子节点
        /// </summary>
        public ObservableCollection<ComparisonTreeNode> Children { get; }

        /// <summary>
        /// 是否有子节点
        /// </summary>
        public bool HasChildren => Children.Count > 0;

        // WPF显示属性

        /// <summary>
        /// 格式化的大小差异（如 "+1.5 MB" 或 "-512 KB"）
        /// </summary>
        public string SizeDeltaFormatted { get; }

        /// <summary>
        /// 格式化的A中大小
        /// </summary>
        public string TotalSizeInAFormatted { get; }

        /// <summary>
        /// 格式化的B中大小
        /// </summary>
        public string TotalSizeInBFormatted { get; }

        /// <summary>
        /// 差异颜色（红色=增加，绿色=减少，灰色=无变化）
        /// </summary>
        public Brush DeltaColor { get; }

        private static string FormatSizeDelta(long sizeDelta)
        {
            if (sizeDelta == 0)
                return "0 B";

            var absSize = (ulong)Math.Abs(sizeDelta);
            var sign = sizeDelta > 0 ? "+" : "-";
            return sign + FormatBytes(absSize);
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes == 0) return "0 B";

            const ulong KB = 1024;
            const ulong MB = KB * 1024;
            const ulong GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";
            return $"{bytes} B";
        }

        private static Brush GetDeltaColor(long sizeDelta)
        {
            if (sizeDelta > 0)
                return new SolidColorBrush(Color.FromRgb(211, 47, 47)); // 红色 #D32F2F (增加)
            if (sizeDelta < 0)
                return new SolidColorBrush(Color.FromRgb(56, 142, 60)); // 绿色 #388E3C (减少)
            return new SolidColorBrush(Color.FromRgb(117, 117, 117)); // 灰色 #757575 (无变化)
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

