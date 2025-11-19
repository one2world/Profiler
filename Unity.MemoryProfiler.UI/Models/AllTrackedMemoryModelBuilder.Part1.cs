using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// AllTrackedMemoryModel构建器 - Part 1: 核心框架
    /// 对应Unity的AllTrackedMemoryModelBuilder (Line 14-1503)
    /// 负责遍历CachedSnapshot并构建完整的内存分组树
    /// </summary>
    internal partial class AllTrackedMemoryModelBuilder : ModelBuilderBase<AllTrackedMemoryModel, AllTrackedMemoryBuildArgs>
    {
        // 常量定义（对应Unity Line 25-44）
        private const string NativeGroupName = "Native";
        private const string NativeObjectsGroupName = "Native Objects";
        private const string NativeSubsystemsGroupName = "Unity Subsystems";
        private const string NativeReservedGroupName = "Reserved";

        private const string ManagedGroupName = "Managed";
        private const string ManagedObjectsGroupName = "Managed Objects";
        private const string ManagedVMGroupName = "Virtual Machine";
        private const string ManagedReservedGroupName = "Reserved";

        private const string GraphicsGroupName = "Graphics";
        private const string GraphicsResourcesGroupName = "Graphics Resources";
        private const string GraphicsReservedGroupName = "Reserved";

        private const string ExecutablesGroupName = "Executables & Mapped";
        private const string UntrackedGroupName = "Untracked";
        private const string UntrackedEstimatedGroupName = "Untracked (Estimated)";
        
        private const string InvalidItemName = "Unknown";
        private const string ReservedItemName = "Reserved";

        private int _nextItemId;

        public AllTrackedMemoryModelBuilder()
        {
            _nextItemId = 10000; // 起始ID，避免与其他ID冲突
        }

        #region IModelBuilder Implementation

        /// <summary>
        /// 同步构建AllTrackedMemoryModel
        /// 对应Unity Build方法 (Line 52-70)
        /// </summary>
        public override AllTrackedMemoryModel Build(CachedSnapshot snapshot, AllTrackedMemoryBuildArgs args)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            // 如果排除所有，返回空模型
            if (args.ExcludeAll)
            {
                return new AllTrackedMemoryModel(
                    new ObservableCollection<TreeNode<MemoryItemData>>(),
                    0, 0, 0,
                    args.SelectionProcessor);
            }

            // 第一阶段：构建上下文（遍历所有数据，填充分组字典）
            var context = BuildAllMemoryContext(snapshot, args);

            // 第二阶段：生成树结构
            var rootNodes = BuildAllMemoryBreakdown(snapshot, args, context);

            // 计算总大小
            long totalMemorySize = 0;
            long totalGraphicsSize = 0;

            foreach (var node in rootNodes)
            {
                totalMemorySize += node.Data?.Size ?? 0;

                if (string.Equals(node.Data?.Name, GraphicsGroupName, StringComparison.OrdinalIgnoreCase))
                {
                    totalGraphicsSize = node.Data?.Size ?? 0;
                }
            }

            // 获取快照总内存大小
            long totalSnapshotSize = context.Total;
            // Workaround: 如果Graphics导致总大小膨胀，使用较大值 (对应Unity Line 27)
            totalSnapshotSize = Math.Max(totalSnapshotSize, totalMemorySize);

            var model = new AllTrackedMemoryModel(
                rootNodes,
                totalMemorySize,
                totalGraphicsSize,
                totalSnapshotSize,
                args.SelectionProcessor);

            return model;
        }

        /// <summary>
        /// 异步构建（带进度报告）
        /// </summary>
        public override async Task<AllTrackedMemoryModel> BuildAsync(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildArgs args,
            IProgress<BuildProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;

            progress?.Report(new BuildProgress
            {
                Stage = "Initializing",
                Percent = 0,
                Message = "Starting AllTrackedMemory build...",
                ElapsedTime = TimeSpan.Zero
            });

            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (args.ExcludeAll)
            {
                progress?.Report(new BuildProgress
                {
                    Stage = "Completed",
                    Percent = 100,
                    Message = "Empty model (all excluded)",
                    ElapsedTime = DateTime.Now - startTime
                });

                return new AllTrackedMemoryModel(
                    new ObservableCollection<TreeNode<MemoryItemData>>(),
                    0, 0, 0,
                    args.SelectionProcessor);
            }

            AllTrackedMemoryBuildContext context = null;
            ObservableCollection<TreeNode<MemoryItemData>> rootNodes = null;

            // 阶段1：构建上下文
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                progress?.Report(new BuildProgress
                {
                    Stage = "Iterating Memory",
                    Percent = 30,
                    Message = "Iterating memory hierarchy...",
                    ElapsedTime = DateTime.Now - startTime
                });

                context = BuildAllMemoryContext(snapshot, args);
            }, cancellationToken);

            // 阶段2：生成树
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                progress?.Report(new BuildProgress
                {
                    Stage = "Generating Tree",
                    Percent = 70,
                    Message = "Generating tree structure...",
                    ElapsedTime = DateTime.Now - startTime
                });

                rootNodes = BuildAllMemoryBreakdown(snapshot, args, context);
            }, cancellationToken);

            // 计算总大小
            long totalMemorySize = 0;
            long totalGraphicsSize = 0;

            foreach (var node in rootNodes)
            {
                totalMemorySize += node.Data?.Size ?? 0;

                if (string.Equals(node.Data?.Name, GraphicsGroupName, StringComparison.OrdinalIgnoreCase))
                {
                    totalGraphicsSize = node.Data?.Size ?? 0;
                }
            }

            long totalSnapshotSize = context.Total;
            totalSnapshotSize = Math.Max(totalSnapshotSize, totalMemorySize);

            var model = new AllTrackedMemoryModel(
                rootNodes,
                totalMemorySize,
                totalGraphicsSize,
                totalSnapshotSize,
                args.SelectionProcessor);

            progress?.Report(new BuildProgress
            {
                Stage = "Completed",
                Percent = 100,
                Message = $"Build completed: {model.RootNodes.Count} groups",
                ElapsedTime = DateTime.Now - startTime
            });

            return model;
        }

        #endregion

        #region Phase 1: Build Context (Iterate Memory Hierarchy)

        /// <summary>
        /// 第一阶段：构建上下文
        /// 遍历CachedSnapshot的所有数据，填充分组字典
        /// 对应Unity BuildAllMemoryContext方法 (Line 190-344)
        /// </summary>
        private AllTrackedMemoryBuildContext BuildAllMemoryContext(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildArgs args)
        {
            var context = new AllTrackedMemoryBuildContext();

            // 使用EntriesMemoryMap.ForEachFlatWithResidentSize遍历所有内存条目
            // 对应Unity Line 233-266
            snapshot.EntriesMemoryMap.ForEachFlatWithResidentSize((index, address, size, residentSize, source) =>
            {
                // 累计总内存
                context.Total += (long)size;

                // 根据source.Id分派到不同的处理方法
                switch (source.Id)
                {
                    case SourceIndex.SourceId.NativeObject:
                        // Native Objects由ProcessedNativeRoots处理，这里跳过
                        break;

                    case SourceIndex.SourceId.NativeAllocation:
                        // Native Allocations（暂时简化处理）
                        break;

                    case SourceIndex.SourceId.NativeMemoryRegion:
                        ProcessNativeRegion(snapshot, source, (long)size, args, context);
                        break;

                    case SourceIndex.SourceId.ManagedObject:
                        ProcessPureManagedObject(snapshot, source, (long)size, args, context);
                        break;

                    case SourceIndex.SourceId.ManagedHeapSection:
                        ProcessManagedHeap(snapshot, source, (long)size, args, context);
                        break;

                    case SourceIndex.SourceId.SystemMemoryRegion:
                        ProcessSystemRegion(snapshot, source, (long)size, args, context);
                        break;

                    default:
                        // 未知类型，忽略
                        break;
                }
            });

            // 处理ProcessedNativeRoots（Native Objects和Root References）
            // 对应Unity Line 269-288
            ProcessNativeRootsContext(snapshot, args, context);

            return context;
        }

        #endregion

        #region Helper Methods - Name/Search Filtering

        /// <summary>
        /// 名称过滤
        /// 对应Unity NameFilter方法 (Line 359-362)
        /// </summary>
        private bool NameFilter(AllTrackedMemoryBuildArgs args, string name)
        {
            // 简化实现：暂时不支持过滤
            return true;
        }

        /// <summary>
        /// 添加大小到Map
        /// 对应Unity AddItemSizeToMap方法
        /// </summary>
        private void AddItemSizeToMap<TKey>(Dictionary<TKey, long> map, TKey key, long size)
        {
            if (map.ContainsKey(key))
                map[key] += size;
            else
                map[key] = size;
        }

        #endregion
    }
}

