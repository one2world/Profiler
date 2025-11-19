using System.Collections.Generic;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// AllTrackedMemoryModel构建器 - Part 2: Process方法
    /// 对应Unity的各种Process方法 (Line 533-817)
    /// </summary>
    internal partial class AllTrackedMemoryModelBuilder
    {
        #region Process Methods

        /// <summary>
        /// 处理Native内存区域
        /// 对应Unity ProcessNativeRegion方法 (Line 533-552)
        /// </summary>
        private void ProcessNativeRegion(
            CachedSnapshot snapshot,
            SourceIndex source,
            long size,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            var name = source.GetName(snapshot);

            // 应用名称过滤（只有在不合并reserved时才过滤）
            if (args.BreakdownNativeReserved && !NameFilter(args, name))
                return;

            // 添加到NativeRegion map
            AddItemSizeToMap(context.NativeRegionName2SizeMap, source, size);
        }

        /// <summary>
        /// 处理Pure Managed对象（没有关联Native对象的Managed对象）
        /// 对应Unity ProcessPureManagedObject方法 (Line 685-737)
        /// </summary>
        private void ProcessPureManagedObject(
            CachedSnapshot snapshot,
            SourceIndex source,
            long size,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            ref readonly var managedObject = ref snapshot.CrawledData.ManagedObjects[source.Index];
            var nativeObjectIndex = managedObject.NativeObjectIndex;
            
            // 有关联Native对象的Managed对象由ProcessedNativeRoots处理，这里跳过
            if (nativeObjectIndex >= NativeObjectEntriesCache.FirstValidObjectIndex)
                return;

            var name = source.GetName(snapshot);

            // 应用名称过滤
            if (!NameFilter(args, name))
                return;

            // 需要有效的类型才能添加
            var managedTypeIndex = managedObject.ITypeDescription;
            if (managedTypeIndex < 0)
                return;

            var groupSource = new SourceIndex(SourceIndex.SourceId.ManagedType, managedTypeIndex);

            // 创建Managed对象的TreeNode
            var itemData = new MemoryItemData(name, size);
            var treeNode = new TreeNode<MemoryItemData>(itemData);

            // 添加到类型分组map
            if (!context.ManagedTypeName2ObjectsTreeMap.ContainsKey(groupSource))
                context.ManagedTypeName2ObjectsTreeMap[groupSource] = new List<TreeNode<MemoryItemData>>();

            context.ManagedTypeName2ObjectsTreeMap[groupSource].Add(treeNode);
        }

        /// <summary>
        /// 处理Managed Heap Section
        /// 对应Unity ProcessManagedHeap方法 (Line 739-765)
        /// </summary>
        private void ProcessManagedHeap(
            CachedSnapshot snapshot,
            SourceIndex source,
            long size,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            // 应用名称过滤
            var name = source.GetName(snapshot);
            if (!NameFilter(args, name))
                return;

            var managedHeaps = snapshot.ManagedHeapSections;
            var sectionType = managedHeaps.SectionType[source.Index];
            
            switch (sectionType)
            {
                case MemorySectionType.VirtualMachine:
                    context.ManagedMemoryVM += size;
                    break;
                case MemorySectionType.GarbageCollector:
                    context.ManagedMemoryReserved += size;
                    break;
                default:
                    // 未知类型，默认归为Reserved
                    context.ManagedMemoryReserved += size;
                    break;
            }
        }

        /// <summary>
        /// 处理System内存区域
        /// 对应Unity ProcessSystemRegion方法 (Line 767-817)
        /// </summary>
        private void ProcessSystemRegion(
            CachedSnapshot snapshot,
            SourceIndex source,
            long size,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            // 应用名称过滤
            var name = source.GetName(snapshot);
            if (!NameFilter(args, name))
                return;

            var regionType = snapshot.EntriesMemoryMap.GetPointType(source);
            
            switch (regionType)
            {
                case EntriesMemoryMapCache.PointType.Mapped:
                    // 可执行文件和映射内存
                    AddItemSizeToMap(context.ExecutablesName2SizeMap, name, size);
                    break;

                case EntriesMemoryMapCache.PointType.Shared:
                case EntriesMemoryMapCache.PointType.Untracked:
                    // 未追踪内存
                    AddItemSizeToMap(context.UntrackedRegionsName2SizeMap, name, size);
                    break;

                case EntriesMemoryMapCache.PointType.Device:
                    // Device内存（Graphics）
                    if (!args.BreakdownGraphicsResources)
                    {
                        // 不展开Graphics资源时，归入Untracked
                        AddItemSizeToMap(context.UntrackedRegionsName2SizeMap, name, size);
                    }
                    else
                    {
                        // 展开时，累计到UntrackedGraphicsResources
                        context.UntrackedGraphicsResources += size;
                    }
                    break;

                case EntriesMemoryMapCache.PointType.AndroidRuntime:
                    // Android平台特定
                    context.AndroidRuntime += size;
                    break;

                default:
                    // 未知类型，归入Untracked
                    AddItemSizeToMap(context.UntrackedRegionsName2SizeMap, name, size);
                    break;
            }
        }

        /// <summary>
        /// 处理ProcessedNativeRoots上下文
        /// 对应Unity处理ProcessedNativeRoots的逻辑 (Line 269-315)
        /// </summary>
        private void ProcessNativeRootsContext(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            var processedNativeRoots = snapshot.ProcessedNativeRoots;
            if (processedNativeRoots == null)
                return;

            long totalEstimatedGraphicsMemory = 0;

            for (long i = 0; i < processedNativeRoots.Count; i++)
            {
                ref readonly var data = ref processedNativeRoots.Data[i];
                
                switch (data.NativeObjectOrRootIndex.Id)
                {
                    case SourceIndex.SourceId.NativeObject:
                        ProcessUnityObject(snapshot, i, in data, args, context, ref totalEstimatedGraphicsMemory);
                        break;

                    case SourceIndex.SourceId.NativeRootReference:
                        ProcessNativeRootReference(snapshot, i, in data, args, context, ref totalEstimatedGraphicsMemory);
                        break;

                    default:
                        break;
                }
            }

            // 处理未根的Graphics资源
            // 对应Unity Line 302-315
            if (processedNativeRoots.UnrootedGraphicsResourceIndices.Count > 0)
            {
                var indexOfFirstUnrootedGfxResource = processedNativeRoots.UnrootedGraphicsResourceIndices[0];
                
                if (args.BreakdownGraphicsResources && processedNativeRoots.UnknownRootMemory.GfxSize.Committed > 0)
                {
                    var size = (long)processedNativeRoots.UnknownRootMemory.GfxSize.Committed;
                    AddItemSizeToMap(context.GfxObjectIndex2SizeMap, indexOfFirstUnrootedGfxResource, size);
                }
            }

            // 处理未追踪的Graphics资源
            // 对应Unity Line 317-318
            context.UntrackedGraphicsResources += (long)processedNativeRoots.NativeAllocationsThatAreUntrackedGraphicsResources.Committed;
        }

        /// <summary>
        /// 处理Unity对象（Native Object）
        /// 对应Unity ProcessUnityObject方法 (Line 371-465)
        /// </summary>
        private void ProcessUnityObject(
            CachedSnapshot snapshot,
            long mappedProcessedRootIndex,
            in ProcessedNativeRoot data,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context,
            ref long totalEstimatedGraphicsMemory)
        {
            ref readonly var itemIndex = ref data.NativeObjectOrRootIndex;
            var name = itemIndex.GetName(snapshot);

            // 应用名称过滤
            if (!NameFilter(args, name))
                return;

            // 累计Native Object大小
            var nativeSize = (long)data.AccumulatedRootSizes.NativeSize.Committed;
            AddItemSizeToMap(context.NativeObjectIndex2SizeMap, itemIndex, nativeSize);

            // 累计Graphics大小
            if (args.BreakdownGraphicsResources && data.AccumulatedRootSizes.GfxSize.Committed > 0)
            {
                var gfxSize = (long)data.AccumulatedRootSizes.GfxSize.Committed;
                AddItemSizeToMap(context.GfxObjectIndex2SizeMap, itemIndex, gfxSize);
                totalEstimatedGraphicsMemory += gfxSize;
            }
        }

        /// <summary>
        /// 处理Native Root Reference
        /// 对应Unity ProcessNativeRootReference方法 (Line 554-653)
        /// </summary>
        private void ProcessNativeRootReference(
            CachedSnapshot snapshot,
            long mappedProcessedRootIndex,
            in ProcessedNativeRoot data,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context,
            ref long totalEstimatedGraphicsMemory)
        {
            ref readonly var itemIndex = ref data.NativeObjectOrRootIndex;
            var areaName = snapshot.NativeRootReferences.AreaName[itemIndex.Index];
            var objectName = snapshot.NativeRootReferences.ObjectName[itemIndex.Index];
            var name = !string.IsNullOrEmpty(objectName) ? objectName : areaName;

            // 应用名称过滤
            if (!NameFilter(args, name))
                return;

            // 累计Native Root Reference大小
            var nativeSize = (long)data.AccumulatedRootSizes.NativeSize.Committed;
            AddItemSizeToMap(context.NativeRootReference2SizeMap, itemIndex, nativeSize);

            // 累计Graphics大小
            if (args.BreakdownGraphicsResources && data.AccumulatedRootSizes.GfxSize.Committed > 0)
            {
                var gfxSize = (long)data.AccumulatedRootSizes.GfxSize.Committed;
                AddItemSizeToMap(context.GfxObjectIndex2SizeMap, itemIndex, gfxSize);
                totalEstimatedGraphicsMemory += gfxSize;
            }
        }

        #endregion
    }
}

