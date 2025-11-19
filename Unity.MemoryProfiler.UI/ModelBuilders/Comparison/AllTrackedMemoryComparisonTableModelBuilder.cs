using System;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.UI.Models;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Models.Comparison;

namespace Unity.MemoryProfiler.UI.ModelBuilders.Comparison
{
    /// <summary>
    /// AllTrackedMemory对比表模型构建器
    /// 等价于Unity的AllTrackedMemoryComparisonTableModelBuilder
    /// </summary>
    internal static class AllTrackedMemoryComparisonTableModelBuilder
    {
        /// <summary>
        /// 构建对比表模型
        /// </summary>
        /// <param name="snapshotA">A快照</param>
        /// <param name="snapshotB">B快照</param>
        /// <param name="includeUnchanged">是否包含未变化的项</param>
        /// <returns>对比表模型</returns>
        internal static ComparisonTableModel Build(
            CachedSnapshot snapshotA,
            CachedSnapshot snapshotB,
            bool includeUnchanged = false)
        {
            if (snapshotA == null)
                throw new ArgumentNullException(nameof(snapshotA));
            if (snapshotB == null)
                throw new ArgumentNullException(nameof(snapshotB));

            // 步骤1：使用AllTrackedMemoryDataBuilder分别构建A和B的模型
            // 使用默认的BuildArgs（与Unity官方一致）
            var buildArgs = new AllTrackedMemoryBuildArgs(
                pathFilter: null,
                excludeNative: false,
                excludeManaged: false,
                excludeGraphics: false,
                breakdownNativeReserved: false,
                breakdownGraphicsResources: true,
                managedGrouping: ManagedGroupingMode.ByType,
                selectionProcessor: null);

            var builderA = new Unity.MemoryProfiler.UI.Services.AllTrackedMemoryDataBuilder(snapshotA);
            var modelA = builderA.Build(buildArgs);

            var builderB = new Unity.MemoryProfiler.UI.Services.AllTrackedMemoryDataBuilder(snapshotB);
            var modelB = builderB.Build(buildArgs);

            // 步骤2：将AllTrackedMemoryTreeNode转换为ComparableTreeNode
            var comparableTreeA = TreeNodeAdapter.ConvertAllTrackedMemoryNodes(modelA.RootNodes);
            var comparableTreeB = TreeNodeAdapter.ConvertAllTrackedMemoryNodes(modelB.RootNodes);

            // 步骤3：使用TreeComparisonBuilder构建对比树
            var treeComparisonBuilder = new TreeComparisonBuilder();
            var comparisonArgs = new TreeComparisonBuilder.BuildArgs(includeUnchanged);
            var comparisonTree = treeComparisonBuilder.Build(
                comparableTreeA,
                comparableTreeB,
                comparisonArgs,
                out var largestAbsoluteSizeDelta);

            // 步骤4：获取快照的总内存大小
            var totalSnapshotSizeA = snapshotA.MetaData.TargetMemoryStats.HasValue 
                ? snapshotA.MetaData.TargetMemoryStats.Value.TotalVirtualMemory 
                : modelA.TotalSnapshotMemory;
            var totalSnapshotSizeB = snapshotB.MetaData.TargetMemoryStats.HasValue 
                ? snapshotB.MetaData.TargetMemoryStats.Value.TotalVirtualMemory 
                : modelB.TotalSnapshotMemory;

            // 步骤5：创建并返回ComparisonTableModel
            var comparisonModel = new ComparisonTableModel(
                comparisonTree,
                totalSnapshotSizeA,
                totalSnapshotSizeB,
                largestAbsoluteSizeDelta);

            return comparisonModel;
        }
    }
}

