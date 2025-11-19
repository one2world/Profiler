using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Unity.MemoryProfiler.UI.Models.Comparison
{
    /// <summary>
    /// 对比树节点，支持层级结构
    /// </summary>
    public class ComparisonTreeNode
    {
        public ComparisonTreeNode(ComparisonData data, List<ComparisonTreeNode> children = null, object sourceNodesA = null, object sourceNodesB = null)
        {
            Data = data;
            Children = children ?? new List<ComparisonTreeNode>();
            SourceNodesA = sourceNodesA;
            SourceNodesB = sourceNodesB;
        }

        /// <summary>
        /// 节点数据
        /// </summary>
        public ComparisonData Data { get; }

        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<ComparisonTreeNode> Children { get; }

        /// <summary>
        /// A 快照中的原始节点列表（用于 Unity Objects 子表显示）
        /// </summary>
        public object SourceNodesA { get; }

        /// <summary>
        /// B 快照中的原始节点列表（用于 Unity Objects 子表显示）
        /// </summary>
        public object SourceNodesB { get; }

        /// <summary>
        /// 是否有子节点
        /// </summary>
        public bool HasChildren => Children != null && Children.Count > 0;

        // ===== 便捷属性（用于 XAML 绑定）=====

        /// <summary>
        /// 节点名称
        /// </summary>
        public string Name => Data.Name;

        /// <summary>
        /// A快照中的总大小
        /// </summary>
        public ulong TotalSizeInA => Data.TotalSizeInA;

        /// <summary>
        /// B快照中的总大小
        /// </summary>
        public ulong TotalSizeInB => Data.TotalSizeInB;

        /// <summary>
        /// 大小差值
        /// </summary>
        public long SizeDelta => Data.SizeDelta;

        /// <summary>
        /// A快照中的数量
        /// </summary>
        public uint CountInA => Data.CountInA;

        /// <summary>
        /// B快照中的数量
        /// </summary>
        public uint CountInB => Data.CountInB;

        /// <summary>
        /// 数量差值
        /// </summary>
        public int CountDelta => Data.CountDelta;

        /// <summary>
        /// 是否有变化
        /// </summary>
        public bool HasChanged => Data.HasChanged;

        /// <summary>
        /// 格式化的 Size Delta（带符号和单位）
        /// </summary>
        public string SizeDeltaFormatted
        {
            get
            {
                if (SizeDelta == 0)
                    return "0 B";

                var sign = SizeDelta > 0 ? "+" : "";
                return $"{sign}{FormatBytes((ulong)Math.Abs(SizeDelta))}";
            }
        }

        /// <summary>
        /// 格式化的 Size in A
        /// </summary>
        public string TotalSizeInAFormatted => FormatBytes(TotalSizeInA);

        /// <summary>
        /// 格式化的 Size in B
        /// </summary>
        public string TotalSizeInBFormatted => FormatBytes(TotalSizeInB);

        /// <summary>
        /// Delta 颜色（增长=红色，减少=绿色，不变=灰色）
        /// </summary>
        public Brush DeltaColor
        {
            get
            {
                if (SizeDelta > 0)
                    return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                else if (SizeDelta < 0)
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                else
                    return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Gray
            }
        }

        /// <summary>
        /// 格式化字节数
        /// </summary>
        private static string FormatBytes(ulong bytes)
        {
            if (bytes >= 1024UL * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            else if (bytes >= 1024UL * 1024)
                return $"{bytes / (1024.0 * 1024):F2} MB";
            else if (bytes >= 1024UL)
                return $"{bytes / 1024.0:F2} KB";
            else
                return $"{bytes} B";
        }
    }
}

