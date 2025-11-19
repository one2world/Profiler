using System.Collections.ObjectModel;
using Unity.MemoryProfiler.Editor;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// AllTrackedMemory对比Model构建器
    /// Unity官方实现参考：AllTrackedMemoryComparisonTableModelBuilder.cs
    /// 核心逻辑100%等价Unity官方实现
    /// </summary>
    internal static class AllTrackedMemoryComparisonModelBuilder
    {
        /// <summary>
        /// 构建AllTrackedMemory对比Model
        /// </summary>
        /// <param name="snapshotA">快照A</param>
        /// <param name="snapshotB">快照B</param>
        /// <param name="includeUnchanged">是否包含未改变的项</param>
        /// <returns>对比Model</returns>
        internal static ComparisonModel Build(
            CachedSnapshot snapshotA,
            CachedSnapshot snapshotB,
            bool includeUnchanged = false)
        {
            // 步骤1：构建两个AllTrackedMemoryModel（使用默认BuildArgs）
            var buildArgs = new AllTrackedMemoryBuildArgs(
                pathFilter: null,
                excludeNative: false,
                excludeManaged: false,
                excludeGraphics: false,
                breakdownNativeReserved: false,
                breakdownGraphicsResources: true,
                managedGrouping: ManagedGroupingMode.ByType,
                selectionProcessor: null);

            var builderA = new AllTrackedMemoryModelBuilder();
            var modelA = builderA.Build(snapshotA, buildArgs);

            var builderB = new AllTrackedMemoryModelBuilder();
            var modelB = builderB.Build(snapshotB, buildArgs);

            // 步骤2：使用TreeComparisonBuilder比较两棵树
            var treeComparisonBuilder = new TreeComparisonBuilder();
            var comparisonTree = treeComparisonBuilder.Build(
                modelA.RootNodes,
                modelB.RootNodes,
                includeUnchanged,
                out var largestAbsoluteSizeDelta);

            // 步骤3：获取快照总大小
            var totalSnapshotSizeA = snapshotA.MetaData.TargetMemoryStats.HasValue
                ? snapshotA.MetaData.TargetMemoryStats.Value.TotalVirtualMemory
                : 0UL;

            var totalSnapshotSizeB = snapshotB.MetaData.TargetMemoryStats.HasValue
                ? snapshotB.MetaData.TargetMemoryStats.Value.TotalVirtualMemory
                : 0UL;

            // 步骤4：创建ComparisonModel
            var comparisonModel = new ComparisonModel(
                new ObservableCollection<ComparisonTreeNode>(comparisonTree),
                totalSnapshotSizeA,
                totalSnapshotSizeB,
                largestAbsoluteSizeDelta);

            return comparisonModel;
        }
    }
}

