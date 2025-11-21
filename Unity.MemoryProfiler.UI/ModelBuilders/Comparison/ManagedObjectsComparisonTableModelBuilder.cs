using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Models.Comparison;
using Unity.MemoryProfiler.UI.Services;

namespace Unity.MemoryProfiler.UI.ModelBuilders.Comparison
{
    /// <summary>
    /// Managed Objects 对比表模型构建器
    /// 参考: UnityObjectsComparisonTableModelBuilder 和 AllTrackedMemoryComparisonTableModelBuilder
    /// </summary>
    internal static class ManagedObjectsComparisonTableModelBuilder
    {
        /// <summary>
        /// 构建 Managed Objects 对比模型
        /// </summary>
        /// <param name="snapshotA">快照 A</param>
        /// <param name="snapshotB">快照 B</param>
        /// <param name="isReversedMode">是否为反向模式</param>
        /// <param name="includeUnchanged">是否包含未改变的项</param>
        /// <returns>对比表模型</returns>
        public static ComparisonTableModel Build(
            CachedSnapshot snapshotA,
            CachedSnapshot snapshotB,
            bool isReversedMode,
            bool includeUnchanged = false)
        {
            if (snapshotA == null)
                throw new ArgumentNullException(nameof(snapshotA));
            if (snapshotB == null)
                throw new ArgumentNullException(nameof(snapshotB));

            // 步骤1：使用 ManagedObjectsDataBuilder 分别构建 A 和 B 的模型
            var builderA = new ManagedObjectsDataBuilder(snapshotA);
            var modelA = isReversedMode ? builderA.BuildReversed() : builderA.Build();

            var builderB = new ManagedObjectsDataBuilder(snapshotB);
            var modelB = isReversedMode ? builderB.BuildReversed() : builderB.Build();

            // 步骤2：将 ManagedCallStackNode 转换为 ComparableTreeNode
            var comparableTreeA = TreeNodeAdapter.ConvertManagedObjectsNodes(modelA.RootNodes);
            var comparableTreeB = TreeNodeAdapter.ConvertManagedObjectsNodes(modelB.RootNodes);

            // 步骤3：使用 TreeComparisonBuilder 构建对比树
            var treeComparisonBuilder = new TreeComparisonBuilder();
            var comparisonArgs = new TreeComparisonBuilder.BuildArgs(includeUnchanged);
            var comparisonTree = treeComparisonBuilder.Build(
                comparableTreeA,
                comparableTreeB,
                comparisonArgs,
                out var largestAbsoluteSizeDelta);

            // 步骤4：获取快照的总内存大小
            var totalSnapshotSizeA = snapshotA.MetaData.TargetMemoryStats.HasValue
                ? (ulong)snapshotA.MetaData.TargetMemoryStats.Value.TotalVirtualMemory
                : 0;

            var totalSnapshotSizeB = snapshotB.MetaData.TargetMemoryStats.HasValue
                ? (ulong)snapshotB.MetaData.TargetMemoryStats.Value.TotalVirtualMemory
                : 0;

            // 步骤5：构建 ComparisonTableModel
            return new ComparisonTableModel(
                comparisonTree,
                totalSnapshotSizeA,
                totalSnapshotSizeB,
                largestAbsoluteSizeDelta);
        }
    }
}

