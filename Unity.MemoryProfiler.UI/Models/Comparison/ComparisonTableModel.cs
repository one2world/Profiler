using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models.Comparison
{
    /// <summary>
    /// 对比表模型，包含两个快照的对比结果
    /// 等价于Unity的ComparisonTableModel
    /// </summary>
    public class ComparisonTableModel
    {
        public ComparisonTableModel(
            List<ComparisonTreeNode> rootNodes,
            ulong totalSnapshotSizeA,
            ulong totalSnapshotSizeB,
            long largestAbsoluteSizeDelta)
        {
            RootNodes = rootNodes ?? new List<ComparisonTreeNode>();

            // 计算根节点的总大小
            var totalSizeA = 0UL;
            var totalSizeB = 0UL;
            foreach (var rootNode in RootNodes)
            {
                totalSizeA += rootNode.Data.TotalSizeInA;
                totalSizeB += rootNode.Data.TotalSizeInB;
            }

            TotalSizeA = totalSizeA;
            TotalSizeB = totalSizeB;
            TotalSnapshotSizeA = totalSnapshotSizeA;
            TotalSnapshotSizeB = totalSnapshotSizeB;
            LargestAbsoluteSizeDelta = largestAbsoluteSizeDelta;
        }

        /// <summary>
        /// 根节点列表
        /// </summary>
        public List<ComparisonTreeNode> RootNodes { get; }

        /// <summary>
        /// A快照在表中的总大小（所有根节点的TotalSizeInA之和），单位：字节
        /// </summary>
        public ulong TotalSizeA { get; }

        /// <summary>
        /// B快照在表中的总大小（所有根节点的TotalSizeInB之和），单位：字节
        /// </summary>
        public ulong TotalSizeB { get; }

        /// <summary>
        /// A快照的全部内存大小，单位：字节
        /// </summary>
        public ulong TotalSnapshotSizeA { get; }

        /// <summary>
        /// B快照的全部内存大小，单位：字节
        /// </summary>
        public ulong TotalSnapshotSizeB { get; }

        /// <summary>
        /// 表中任意单个项的最大绝对大小差值，单位：字节
        /// 用于计算DeltaBar的比例
        /// </summary>
        public long LargestAbsoluteSizeDelta { get; }
    }
}

