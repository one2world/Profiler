using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Extensions;
using Unity.MemoryProfiler.Editor.UI.Models;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// All Tracked Memory 数据构建器
    /// 严格遵循 Unity 官方逻辑：AllTrackedMemoryModelBuilder
    /// 
    /// 核心流程：
    /// 1. BuildMemoryContext: 收集数据（遍历 EntriesMemoryMap + ProcessedNativeRoots）
    /// 2. BuildTree: 从上下文构建树形结构
    /// 
    /// 参考文档：UNITY_ALL_OF_MEMORY_ANALYSIS.md
    /// </summary>
    internal class AllTrackedMemoryDataBuilder
    {
        private readonly CachedSnapshot _snapshot;
        private int _nextId;

        // 分组名称（与 Unity 官方一致）
        private const string NativeGroupName = "Native";
        private const string NativeObjectsGroupName = "Native Objects";
        private const string NativeSubsystemsGroupName = "Unity Subsystems";
        private const string NativeReservedGroupName = "Reserved";
        
        private const string ManagedGroupName = "Managed";
        private const string ManagedObjectsGroupName = "Managed Objects";
        private const string ManagedVMGroupName = "Virtual Machine";
        private const string ManagedReservedGroupName = "Reserved";
        
        private const string GraphicsGroupName = "Graphics (Estimated)";  // SummaryTextContent.kAllMemoryCategoryGraphics
        private const string UntrackedGroupName = "Untracked";
        private const string UntrackedEstimatedGroupName = "Untracked (Estimated)";  // SummaryTextContent.kAllMemoryCategoryUntrackedEstimated
        private const string ExecutablesGroupName = "Executables & Mapped";

        private const string NoNamePlaceholder = "<No Name>";  // 当对象名称为空时的占位符

        public AllTrackedMemoryDataBuilder(CachedSnapshot snapshot)
        {
            _snapshot = snapshot;
            _nextId = 0;
        }

        public AllTrackedMemoryData Build()
        {
            return Build(AllTrackedMemoryBuildArgs.Default);
        }

        /// <summary>
        /// 构建 AllTrackedMemoryData（带参数）
        /// 参考: Unity AllTrackedMemoryModelBuilder.Build(BuildArgs args)
        /// </summary>
        public AllTrackedMemoryData Build(AllTrackedMemoryBuildArgs args)
        {
            var data = new AllTrackedMemoryData();
            
            // 第一步：构建上下文（收集所有数据）
            var context = BuildMemoryContext();
            
            // 第二步：从上下文构建树形结构
            List<AllTrackedMemoryTreeNode> rootNodes;
            
            if (args.ExcludeAll)
            {
                // Unity Line 61-62: 如果 ExcludeAll，返回空树
                rootNodes = new List<AllTrackedMemoryTreeNode>();
            }
            else
            {
                rootNodes = BuildAllMemoryTree(context);
                
                // 第三步：应用 ItemPath 过滤（如果提供）
                if (args.ItemPathFilter != null && args.ItemPathFilter.Count > 0)
                {
                    rootNodes = BuildItemsAtPathInTreeExclusively(args.ItemPathFilter, rootNodes);
                }
            }
            
            // 第四步：排序和计算百分比
            rootNodes = rootNodes.OrderByDescending(n => n.AllocatedSize).ToList();
            
            ulong totalMemoryInTable = 0;
            ulong totalGraphicsMemory = 0;
            
            foreach (var node in rootNodes)
            {
                totalMemoryInTable += node.AllocatedSize;
                if (node.Name == GraphicsGroupName)
                {
                    totalGraphicsMemory = node.AllocatedSize;
                }
            }
            
            if (totalMemoryInTable > 0)
            {
                CalculatePercentages(rootNodes, totalMemoryInTable);
            }
            
            // 获取快照总内存
            ulong totalSnapshotMemory = context.TotalMemory;
            if (totalSnapshotMemory == 0 && _snapshot.MetaData.TargetMemoryStats.HasValue)
            {
                totalSnapshotMemory = _snapshot.MetaData.TargetMemoryStats.Value.TotalVirtualMemory;
            }
            if (totalSnapshotMemory == 0)
            {
                totalSnapshotMemory = totalMemoryInTable;
            }
            
            data.TotalSnapshotMemory = totalSnapshotMemory;
            data.TotalMemoryInTable = totalMemoryInTable;
            data.TotalGraphicsMemory = totalGraphicsMemory;
            data.RootNodes = rootNodes;
            
            return data;
        }

        #region 第一步：数据收集（BuildMemoryContext）

        /// <summary>
        /// 构建内存上下文
        /// 参考: AllTrackedMemoryModelBuilder.BuildAllMemoryContext
        /// 
        /// 分两个阶段：
        /// 1. 遍历 EntriesMemoryMap（处理 SystemMemoryRegion、ManagedHeapSection 等）
        /// 2. 遍历 ProcessedNativeRoots（处理 NativeObject、NativeRootReference）
        /// </summary>
        private BuildContext BuildMemoryContext()
        {
            var context = new BuildContext();
            
            // ===== 阶段 0：初始化 NativeAllocation Splitting 列表 =====
            // 参考: Unity Line 214-229
            // TODO: 这里应该根据 BuildArgs 决定哪些 NativeRootReference 需要被 split
            // 目前简化实现：不进行 splitting（保留空字典）
            // 完整实现需要根据用户选择的 Area/Object 名称来添加到 NativeRootReference2UnsafeAllocations2SizeMap
            
            // ===== 阶段 1：遍历 EntriesMemoryMap =====
            _snapshot.EntriesMemoryMap.ForEachFlatWithResidentSize((index, address, size, residentSize, source) =>
            {
                context.TotalMemory += size;
                
                var memorySize = new MemorySize(size, residentSize);
                
                switch (source.Id)
                {
                    case CachedSnapshot.SourceIndex.SourceId.NativeObject:
                        // 由 ProcessedNativeRoots 处理
                        break;
                        
                    case CachedSnapshot.SourceIndex.SourceId.NativeAllocation:
                        // 处理潜在的子分配（NativeAllocation splitting）
                        // 参考: Unity Line 245-247
                        ProcessNativeAllocationForPotentialSplitting(index, source, memorySize, context);
                        break;
                        
                    case CachedSnapshot.SourceIndex.SourceId.NativeMemoryRegion:
                        // 处理 Native Reserved（Line 248-249 in Unity）
                        ProcessNativeRegion(source, memorySize, context);
                        break;
                        
                    case CachedSnapshot.SourceIndex.SourceId.ManagedObject:
                        // ⚠️ 关键修正：在EntriesMemoryMap遍历时处理纯Managed Objects
                        // 参考: Unity Line 251-252
                        ProcessPureManagedObject(source, memorySize, context);
                        break;
                        
                    case CachedSnapshot.SourceIndex.SourceId.ManagedHeapSection:
                        // 处理 Managed VM + Reserved
                        ProcessManagedHeapSection(source, memorySize, context);
                        break;
                        
                    case CachedSnapshot.SourceIndex.SourceId.SystemMemoryRegion:
                        // 处理 Executables + Untracked（按名称分组）
                        ProcessSystemMemoryRegion(source, memorySize, context);
                        break;
                }
            });
            
            // ===== 阶段 2：遍历 ProcessedNativeRoots =====
            var processedRoots = _snapshot.ProcessedNativeRoots;
            ulong totalEstimatedGraphicsMemory = 0;
            
            for (long i = 0; i < processedRoots.Count; i++)
            {
                ref readonly var data = ref processedRoots.Data[i];
                var source = data.NativeObjectOrRootIndex;
                var sizes = data.AccumulatedRootSizes;
                
                switch (source.Id)
                {
                    case CachedSnapshot.SourceIndex.SourceId.NativeObject:
                        // ProcessUnityObject 会分别处理 Native、Managed、Graphics
                        ProcessNativeObject(source, sizes, context, ref totalEstimatedGraphicsMemory);
                        break;
                        
                    case CachedSnapshot.SourceIndex.SourceId.NativeRootReference:
                        // ProcessNativeRootReference 只处理 NativeSize（Line 579）
                        ProcessNativeRootReference(source, sizes, context, ref totalEstimatedGraphicsMemory);
                        break;
                }
            }
            
            // ===== 阶段 3：处理 Unknown Native/Graphics allocation root =====
            // 参考: Line 289-315 in Unity
            const bool BreakdownGfxResources = true;  // 默认值
            
            // Handle Unknown Native allocation root (Line 289-300)
            var unknownRootMemory = _snapshot.ProcessedNativeRoots.UnknownRootMemory;
            if (unknownRootMemory.NativeSize.Committed > 0)
            {
                // 注意：这里使用 k_FakeInvalidlyRootedAllocationIndex（SourceId.None, 1）
                var unknownAllocationRootIndex = new CachedSnapshot.SourceIndex(CachedSnapshot.SourceIndex.SourceId.None, 1);
                var unknownSize = new MemorySize(
                    (ulong)unknownRootMemory.NativeSize.Committed,
                    (ulong)unknownRootMemory.NativeSize.Resident);
                context.NativeRootReference2SizeMap.AddOrUpdate(unknownAllocationRootIndex, unknownSize);
            }
            
            // Handle Unknown Graphics allocation root (Line 302-315)
            var unrootedGraphicsResourceIndices = _snapshot.ProcessedNativeRoots.UnrootedGraphicsResourceIndices;
            if (unrootedGraphicsResourceIndices.Count > 0 && unknownRootMemory.GfxSize.Committed > 0)
            {
                var indexOfFirstUnrootedGfxResource = unrootedGraphicsResourceIndices[0];
                var gfxSize = BreakdownGfxResources ? 
                    new MemorySize((ulong)unknownRootMemory.GfxSize.Committed, (ulong)unknownRootMemory.GfxSize.Resident) : 
                    default;
                context.GfxObjectIndex2SizeMap.AddOrUpdate(indexOfFirstUnrootedGfxResource, gfxSize);
                totalEstimatedGraphicsMemory += gfxSize.Committed;
            }
            
            // ===== 阶段 4：处理 NonObjectRootedGraphicsResourceIndices =====
            // 参考: Line 319-341 in Unity
            // 这些是不关联 Native Object 的 Graphics 资源
            var nonObjectRootedGfxResources = _snapshot.ProcessedNativeRoots.NonObjectRootedGraphicsResourceIndices;
            for (int i = 0; i < nonObjectRootedGfxResources.Count; i++)
            {
                var source = nonObjectRootedGfxResources[i];
                
                // 获取 Graphics 大小
                if (source.Id == CachedSnapshot.SourceIndex.SourceId.GfxResource)
                {
                    ulong gfxSizeValue = 0;
                    if (BreakdownGfxResources)
                    {
                        gfxSizeValue = _snapshot.NativeGfxResourceReferences.GfxSize[source.Index];
                        if (gfxSizeValue == 0)
                            continue;  // 跳过大小为 0 的
                    }
                    
                    // 添加到 GfxObjectIndex2SizeMap
                    var gfxSize = new MemorySize(gfxSizeValue, 0);
                    context.GfxObjectIndex2SizeMap.AddOrUpdate(source, gfxSize);
                    totalEstimatedGraphicsMemory += gfxSize.Committed;
                }
            }
            
            // ===== 阶段 5：Graphics估算调整 =====
            // 参考: Unity Line 343-388
            // 处理Graphics内存估算和Untracked调整
            var memoryStats = _snapshot.MetaData.TargetMemoryStats;
            {
                var graphicsUsedMemory = memoryStats?.GraphicsUsedMemory ?? 0;
                // 补偿：如果系统regions的Graphics regions小于平台报告的值
                var untrackedToReassign = new MemorySize();
                
                if (BreakdownGfxResources && (context.UntrackedGraphicsResources < graphicsUsedMemory))
                {
                    untrackedToReassign = new MemorySize(graphicsUsedMemory - context.UntrackedGraphicsResources, 0);
                    context.UntrackedGraphicsResources = graphicsUsedMemory;
                }
                
                // 如果已占用的"untracked" Graphics内存小于估算值，从untracked中重新分配
                // 否则，如果我们有更多regions than estimated，只创建"untracked"条目
                if (totalEstimatedGraphicsMemory > context.UntrackedGraphicsResources)
                {
                    untrackedToReassign += new MemorySize(
                        totalEstimatedGraphicsMemory - context.UntrackedGraphicsResources, 0);
                    context.UntrackedGraphicsResources = 0;
                }
                else
                {
                    context.UntrackedGraphicsResources -= totalEstimatedGraphicsMemory;
                }
                
                // 添加untracked graphics resources到untracked map
                if (context.UntrackedGraphicsResources > 0)
                {
                    context.UntrackedRegionsName2SizeMap.AddOrUpdate("Graphics", 
                        new MemorySize(context.UntrackedGraphicsResources, 0));
                }
                
                // 减少untracked
                if (untrackedToReassign.Committed > 0)
                {
                    ReduceUntrackedByGraphicsResourcesSize(context.UntrackedRegionsName2SizeMap, untrackedToReassign);
                }
            }
            
            //// 调试输出
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] Native Objects: {context.NativeObjectIndex2SizeMap.Count}");
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] Native Root References: {context.NativeRootReference2SizeMap.Count}");
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] ManagedMemoryVM: {context.ManagedMemoryVM:N0} bytes");
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] ManagedMemoryReserved: {context.ManagedMemoryReserved:N0} bytes");
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] ManagedObjects Types: {context.ManagedTypeName2ObjectsTreeMap.Count}");
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] Graphics Objects: {context.GfxObjectIndex2SizeMap.Count}");
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] Total Estimated Graphics Memory: {totalEstimatedGraphicsMemory:N0} bytes");
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] UntrackedGraphicsResources: {context.UntrackedGraphicsResources:N0} bytes");
            
            //// 调试：Unity Subsystems 的详细信息
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] === Unity Subsystems Details ===");
            //ulong totalSubsystemsSize = 0;
            //var sortedSubsystems = context.NativeRootReference2SizeMap.OrderByDescending(kvp => kvp.Value.Committed).ToList();
            //foreach (var kvp in sortedSubsystems)
            //{
            //    var source = kvp.Key;
            //    var size = kvp.Value;
            //    totalSubsystemsSize += size.Committed;
                
            //    string name = "Unknown";
            //    if (source.Id == CachedSnapshot.SourceIndex.SourceId.NativeRootReference)
            //    {
            //        name = _snapshot.NativeRootReferences.AreaName[source.Index];
            //    }
            //    else if (source.Id == CachedSnapshot.SourceIndex.SourceId.None)
            //    {
            //        name = "<Unknown Root>";
            //    }
                
            //    System.Diagnostics.Debug.WriteLine($"  - {name}: {size.Committed:N0} bytes ({size.Committed / 1024.0 / 1024.0:F2} MB)");
            //}
            //System.Diagnostics.Debug.WriteLine($"[BuildMemoryContext] Total Unity Subsystems Size: {totalSubsystemsSize:N0} bytes ({totalSubsystemsSize / 1024.0 / 1024.0:F1} MB)");
            
            return context;
        }

        /// <summary>
        /// 处理纯 Managed Object（不关联 Native Object）
        /// ⚠️ 关键修正：在EntriesMemoryMap遍历时立即构建TreeNode
        /// 参考: AllTrackedMemoryModelBuilder.ProcessPureManagedObject (Line 687-737)
        /// 
        /// Unity 逻辑：
        /// 1. 检查是否关联Native Object，如果关联则跳过（由ProcessUnityObject处理）
        /// 2. 立即构建TreeViewItemData（我们构建AllTrackedMemoryTreeNode）
        /// 3. 添加到context.ManagedTypeName2ObjectsTreeMap
        /// </summary>
        private void ProcessPureManagedObject(CachedSnapshot.SourceIndex source, MemorySize size, BuildContext context)
        {
            // 从CrawledData获取Managed Object
            ref readonly var managedObject = ref _snapshot.CrawledData.ManagedObjects[source.Index];
            
            // 检查是否关联Native Object（Line 696-699）
            var nativeObjectIndex = managedObject.NativeObjectIndex;
            if (nativeObjectIndex >= CachedSnapshot.NativeObjectEntriesCache.FirstValidObjectIndex)
            {
                // 关联Native Object的由ProcessedNativeRoots处理
                return;
            }
            
            // 获取类型索引
            var managedTypeIndex = managedObject.ITypeDescription;
            if (managedTypeIndex < 0)
                return;
            
            // 构建分组键
            var groupSource = new CachedSnapshot.SourceIndex(CachedSnapshot.SourceIndex.SourceId.ManagedType, managedTypeIndex);
            
            // ⚠️ 关键：立即构建TreeNode（Unity在这里构建TreeViewItemData）
            var objectName = GetSafeName(source);  // 使用安全名称获取，避免空名称
            
            var treeNode = new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = objectName,
                AllocatedSize = size.Committed,
                ResidentSize = size.Resident,
                ChildCount = 0,
                Children = null,
                Source = source
            };
            
            // 添加到类型分组（Line 736）
            context.ManagedTypeName2ObjectsTreeMap.GetAndAddToListOrCreateList(groupSource, treeNode);
        }

        /// <summary>
        /// 处理 Managed Heap Section
        /// 参考: AllTrackedMemoryModelBuilder.ProcessManagedHeap (Line 753-765)
        /// 
        /// Unity 逻辑：
        /// - VirtualMachine → ManagedMemoryVM
        /// - GarbageCollector → ManagedMemoryReserved
        /// </summary>
        private void ProcessManagedHeapSection(CachedSnapshot.SourceIndex source, MemorySize size, BuildContext context)
        {
            var sectionType = _snapshot.ManagedHeapSections.SectionType[source.Index];
            
            switch (sectionType)
            {
                case CachedSnapshot.MemorySectionType.VirtualMachine:
                    // Virtual Machine 内存 (Line 756)
                    context.ManagedMemoryVM += size.Committed;
                    break;
                    
                case CachedSnapshot.MemorySectionType.GarbageCollector:
                    // GC Reserved 内存 (Line 759)
                    // 注意：Unity 直接累加 size.Committed，这是 GC Heap 的总大小
                    context.ManagedMemoryReserved += size.Committed;
                    break;
            }
        }

        /// <summary>
        /// 处理 Native Memory Region（Native Reserved）
        /// 参考: AllTrackedMemoryModelBuilder.ProcessNativeRegion (Line 533-552)
        /// </summary>
        private void ProcessNativeRegion(CachedSnapshot.SourceIndex source, MemorySize size, BuildContext context)
        {
            // 简化：不检查 GPU allocator（Switch 平台特定）
            // 直接添加到 NativeRegionIndex2SizeMap
            context.NativeRegionIndex2SizeMap.AddOrUpdate(source, size);
        }

        /// <summary>
        /// 处理 System Memory Region
        /// 参考: AllTrackedMemoryModelBuilder.ProcessSystemRegion (Line 767-817)
        /// 
        /// 关键：按 PointType 分类，使用 Region Name 作为子节点名称
        /// </summary>
        private void ProcessSystemMemoryRegion(CachedSnapshot.SourceIndex source, MemorySize size, BuildContext context)
        {
            var name = GetSafeName(source);
            var pointType = _snapshot.EntriesMemoryMap.GetPointType(source);
            
            switch (pointType)
            {
                case CachedSnapshot.EntriesMemoryMapCache.PointType.Mapped:
                    // Executables & Mapped（DLL 文件）
                    context.ExecutablesName2SizeMap.AddOrUpdate(name, size);
                    break;
                    
                case CachedSnapshot.EntriesMemoryMapCache.PointType.Shared:
                case CachedSnapshot.EntriesMemoryMapCache.PointType.Untracked:
                    // Untracked（Thread Stacks、Shared、System Heaps、Private）
                    context.UntrackedRegionsName2SizeMap.AddOrUpdate(name, size);
                    break;
                    
                case CachedSnapshot.EntriesMemoryMapCache.PointType.Device:
                    // Graphics 资源（Line 795-806）
                    // 如果不详细展开Graphics，加入Untracked
                    // 如果详细展开，累加到UntrackedGraphicsResources（后续会调整）
                    const bool BreakdownGfxResources = true;  // 默认值
                    if (!BreakdownGfxResources)
                    {
                        context.UntrackedRegionsName2SizeMap.AddOrUpdate(name, size);
                    }
                    else
                    {
                        context.UntrackedGraphicsResources += size.Committed;
                    }
                    break;
            }
        }

        /// <summary>
        /// 处理 Native Object
        /// ⚠️ 关键修正：立即构建关联的Managed Object TreeNode
        /// 参考: AllTrackedMemoryModelBuilder.ProcessUnityObject (Line 601-685)
        /// 
        /// 关键逻辑：
        /// 1. Graphics entry → GfxObjectIndex2SizeMap
        /// 2. Native entry → NativeObjectIndex2SizeMap（只用 NativeSize）
        /// 3. Managed entry → 立即构建TreeNode并添加到ManagedTypeName2ObjectsTreeMap
        /// </summary>
        private void ProcessNativeObject(CachedSnapshot.SourceIndex source, NativeRootSize sizes, BuildContext context, ref ulong totalEstimatedGraphicsMemory)
        {
            // Handle Graphics entry (Line 617-624 in Unity)
            const bool BreakdownGfxResources = true;  // 默认值
            if (!BreakdownGfxResources || sizes.GfxSize.Committed > 0)
            {
                var gfxSize = BreakdownGfxResources ? 
                    new MemorySize((ulong)sizes.GfxSize.Committed, (ulong)sizes.GfxSize.Resident) : 
                    default;
                context.GfxObjectIndex2SizeMap.AddOrUpdate(source, gfxSize);
                totalEstimatedGraphicsMemory += gfxSize.Committed;
            }
            
            // Handle Native entry (Line 627)
            var nativeSize = new MemorySize((ulong)sizes.NativeSize.Committed, (ulong)sizes.NativeSize.Resident);
            context.NativeObjectIndex2SizeMap.AddOrUpdate(source, nativeSize);
            
            // Handle Managed entry (Line 629-684 in Unity)
            var managedIndex = _snapshot.NativeObjects.ManagedObjectIndex[source.Index];
            if (sizes.ManagedSize.Committed > 0 && managedIndex >= ManagedData.FirstValidObjectIndex)
            {
                ref readonly var managedObject = ref _snapshot.CrawledData.ManagedObjects[managedIndex];
                var nativeObjectIndex = source.Index;
                if (nativeObjectIndex < CachedSnapshot.NativeObjectEntriesCache.FirstValidObjectIndex)
                    return;
                    
                var managedTypeIndex = managedObject.ITypeDescription;
                if (managedTypeIndex < 0)
                    return;
                
                // 构建分组键
                var groupSource = new CachedSnapshot.SourceIndex(CachedSnapshot.SourceIndex.SourceId.ManagedType, managedTypeIndex);
                
                // ⚠️ 关键：立即构建TreeNode（Unity在这里构建TreeViewItemData）
                var objectName = GetSafeName(source);  // 使用安全名称获取，避免空名称
                var managedSize = new MemorySize((ulong)sizes.ManagedSize.Committed, (ulong)sizes.ManagedSize.Resident);
                
                var treeNode = new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = objectName,
                    AllocatedSize = managedSize.Committed,
                    ResidentSize = managedSize.Resident,
                    ChildCount = 0,
                    Children = null,
                    Source = source  // 使用Native Object的source
                };
                
                // 添加到类型分组（Line 682）
                context.ManagedTypeName2ObjectsTreeMap.GetAndAddToListOrCreateList(groupSource, treeNode);
            }
        }

        /// <summary>
        /// 处理 Native Root Reference
        /// 参考: AllTrackedMemoryModelBuilder.ProcessNativeRootReference (Line 554-599)
        /// 
        /// 关键逻辑：
        /// 1. 检查是否是 VM Root Reference
        /// 2. 只使用 NativeSize 添加到 NativeRootReference2SizeMap（Line 579）
        /// 3. 如果 k_SplitNonObjectRootedGfxResourcesByResource == false，处理 Graphics
        ///    但我们设置为 true（Line 188），所以不在这里处理 Graphics
        /// </summary>
        /// <summary>
        /// 处理 NativeAllocation 以进行潜在的子分配拆分
        /// 参考: Unity Line 420-510 ProcessNativeAllocationForPotentialSplittingNonObjectRootedAllocationsByAllocation
        /// 
        /// 只处理非UnityObject rooted的allocations，其他情况由ProcessedNativeRoots处理
        /// </summary>
        private void ProcessNativeAllocationForPotentialSplitting(
            long memoryMapEntryIndex,
            CachedSnapshot.SourceIndex source,
            MemorySize size,
            BuildContext context)
        {
            var nativeAllocations = _snapshot.NativeAllocations;
            var rootReferenceId = nativeAllocations.RootReferenceId[source.Index];
            
            CachedSnapshot.SourceIndex groupSource = default;
            
            // 确定这个allocation的分组源
            if (rootReferenceId >= 1) // FirstValidRootIndex
            {
                // 检查是否关联到 native object
                if (_snapshot.NativeObjects.RootReferenceIdToIndex.TryGetValue(rootReferenceId, out var objectIndex))
                {
                    // 由 ProcessedNativeRoots 处理
                    return;
                }
                
                // 提取 root reference
                if (_snapshot.NativeRootReferences.IdToIndex.TryGetValue(rootReferenceId, out long groupIndex))
                {
                    groupSource = new CachedSnapshot.SourceIndex(CachedSnapshot.SourceIndex.SourceId.NativeRootReference, groupIndex);
                }
            }
            // else: 无效的 root (groupSource 保持默认值)
            
            // 检查是否需要 split
            if (ShouldGroupBeSplitIntoAllocations(context, ref groupSource, out var splitGroup))
            {
                // 将这个 allocation 添加到 split group
                if (!splitGroup.TryAdd(source, size))
                {
                    // 如果已存在（allocation 被分割成多个部分），则累加
                    splitGroup[source] += size;
                }
                
                // 同时累加到 NativeRootReference2SizeMap（用于计算总和）
                context.NativeRootReference2SizeMap.AddOrUpdate(groupSource, size);
            }
            // else: 由 ProcessedNativeRoots 处理
        }
        
        /// <summary>
        /// 检查一个分组是否应该被拆分为单个 allocations
        /// 参考: Unity Line 512-530
        /// </summary>
        private bool ShouldGroupBeSplitIntoAllocations(
            BuildContext context,
            ref CachedSnapshot.SourceIndex groupSource,
            out Dictionary<CachedSnapshot.SourceIndex, MemorySize> splitGroup)
        {
            splitGroup = null!;
            
            // 检查是否在需要 split 的列表中
            if (groupSource.Valid && 
                context.NativeRootReference2UnsafeAllocations2SizeMap.TryGetValue(groupSource, out var rootReferenceGroup))
            {
                splitGroup = rootReferenceGroup;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 生成 NativeAllocation 的显示名称
        /// 参考: Unity NativeAllocationTools.ProduceNativeAllocationName
        /// </summary>
        private string ProduceNativeAllocationName(CachedSnapshot.SourceIndex source, bool truncateTypeNames = true)
        {
            if (source.Id != CachedSnapshot.SourceIndex.SourceId.NativeAllocation)
                return "Unknown Allocation";
            
            var allocations = _snapshot.NativeAllocations;
            var address = allocations.Address[source.Index];
            var size = allocations.Size[source.Index];
            
            // 格式: "0x{Address:X16} ({Size} bytes)"
            var name = $"0x{address:X16} ({size:N0} bytes)";
            
            return name;
        }
        
        private void ProcessNativeRootReference(CachedSnapshot.SourceIndex source, NativeRootSize sizes, BuildContext context, ref ulong totalEstimatedGraphicsMemory)
        {
            // 检查是否是 VM Root Reference (Line 566-570 in Unity)
            var vmRootIndex = _snapshot.NativeRootReferences.VMRootReferenceIndex;
            if (source.Id == vmRootIndex.Id && source.Index == vmRootIndex.Index)
            {
                // VM Root Reference 应该累加到 ManagedMemoryVM
                context.ManagedMemoryVM += (ulong)sizes.NativeSize.Committed;
                return;
            }
            
            // 添加到 NativeRootReference2SizeMap（Line 579: 只用 NativeSize！）
            var nativeSize = new MemorySize((ulong)sizes.NativeSize.Committed, (ulong)sizes.NativeSize.Resident);
            context.NativeRootReference2SizeMap.AddOrUpdate(source, nativeSize);
            
            // Graphics 处理 (Line 582-598)
            // 注意：k_SplitNonObjectRootedGfxResourcesByResource == true 时不在这里处理
            // Graphics 资源在阶段 4（NonObjectRootedGraphicsResourceIndices）中处理
        }

        #endregion

        #region 第二步：树构建（BuildAllMemoryTree）

        /// <summary>
        /// 从上下文构建树形结构
        /// 参考: AllTrackedMemoryModelBuilder.BuildAllMemoryBreakdown
        /// </summary>
        private List<AllTrackedMemoryTreeNode> BuildAllMemoryTree(BuildContext context)
        {
            var rootNodes = new List<AllTrackedMemoryTreeNode>();
            
            // 1. Native
            var nativeNode = BuildNativeTree(context);
            if (nativeNode != null)
                rootNodes.Add(nativeNode);
            
            // 2. Managed
            var managedNode = BuildManagedTree(context);
            if (managedNode != null)
                rootNodes.Add(managedNode);
            
            // 3. Executables & Mapped
            var executablesNode = BuildExecutablesTree(context);
            if (executablesNode != null)
                rootNodes.Add(executablesNode);
            
            // 4. Untracked
            var untrackedNode = BuildUntrackedTree(context);
            if (untrackedNode != null)
                rootNodes.Add(untrackedNode);
            
            // 5. Graphics
            var graphicsNode = BuildGraphicsTree(context);
            if (graphicsNode != null)
                rootNodes.Add(graphicsNode);
            
            return rootNodes;
        }

        /// <summary>
        /// 构建 Native 树
        /// 参考: AllTrackedMemoryModelBuilder.BuildNativeTree
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildNativeTree(BuildContext context)
        {
            var children = new List<AllTrackedMemoryTreeNode>();
            
            // 1. Native Objects（按 NativeType 分组）
            if (context.NativeObjectIndex2SizeMap.Count > 0)
            {
                var nativeObjectsNode = BuildNativeObjectsTree(context);
                if (nativeObjectsNode != null)
                    children.Add(nativeObjectsNode);
            }
            
            // 2. Unity Subsystems（按 AreaName 分组）
            if (context.NativeRootReference2SizeMap.Count > 0)
            {
                var subsystemsNode = BuildUnitySubsystemsTree(context);
                if (subsystemsNode != null)
                    children.Add(subsystemsNode);
            }
            
            // 3. Reserved（Native Memory Regions）
            if (context.NativeRegionIndex2SizeMap.Count > 0)
            {
                var reservedNode = BuildNativeReservedTree(context);
                if (reservedNode != null)
                    children.Add(reservedNode);
            }
            
            if (children.Count == 0)
                return null;
            
            var totalAllocated = children.Sum(c => (decimal)c.AllocatedSize);
            var totalResident = children.Sum(c => (decimal)c.ResidentSize);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = NativeGroupName,
                Category = CategoryType.Native, // 设置Category，用于详情面板显示描述
                AllocatedSize = (ulong)totalAllocated,
                ResidentSize = (ulong)totalResident,
                ChildCount = children.Count,
                Children = children.OrderByDescending(c => c.AllocatedSize).ToList()
            };
        }

        /// <summary>
        /// 构建 Native Objects 树（按 NativeType 分组）
        /// 参考: AllTrackedMemoryModelBuilder.BuildNativeTree 中的 Native Objects 部分 (Line 861-885)
        /// 
        /// 分组逻辑：
        /// 1. 获取每个 NativeObject 的 NativeType
        /// 2. 按 NativeType 分组
        /// 3. 为每个 NativeType 创建一个节点（显示该类型的总大小和实例数量）
        /// 
        /// ⚠️ 修正：实现完整的分组和展开逻辑（参考Unity GroupItems）
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildNativeObjectsTree(BuildContext context)
        {
            // 第一步：为每个Native Object创建节点，并按NativeType分组
            var typeGroupToObjects = new Dictionary<int, List<AllTrackedMemoryTreeNode>>();
            
            foreach (var kvp in context.NativeObjectIndex2SizeMap)
            {
                var source = kvp.Key;
                var size = kvp.Value;
                var nativeObjectIndex = source.Index;
                var nativeTypeIndex = _snapshot.NativeObjects.NativeTypeArrayIndex[nativeObjectIndex];
                
                // 创建单个对象节点
                var objectName = GetSafeName(source);
                var objectNode = new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = objectName,
                    AllocatedSize = size.Committed,
                    ResidentSize = size.Resident,
                    ChildCount = 0,
                    Children = null,
                    Source = source
                };
                
                // 按NativeType分组
                if (!typeGroupToObjects.TryGetValue(nativeTypeIndex, out var list))
                {
                    list = new List<AllTrackedMemoryTreeNode>();
                    typeGroupToObjects[nativeTypeIndex] = list;
                }
                list.Add(objectNode);
            }
            
            // 第二步：为每个NativeType创建分组节点
            var children = new List<AllTrackedMemoryTreeNode>();
            
            foreach (var kvp in typeGroupToObjects.OrderByDescending(g => g.Value.Sum(n => (decimal)n.AllocatedSize)))
            {
                var nativeTypeIndex = kvp.Key;
                var objectNodes = kvp.Value;
                
                // 排序子节点（按分配大小降序）
                var sortedObjects = objectNodes.OrderByDescending(n => n.AllocatedSize).ToList();
                
                var typeName = _snapshot.NativeTypes.TypeName[nativeTypeIndex];
                var typeCommitted = sortedObjects.Sum(n => (decimal)n.AllocatedSize);
                var typeResident = sortedObjects.Sum(n => (decimal)n.ResidentSize);
                
                children.Add(new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = typeName,
                    AllocatedSize = (ulong)typeCommitted,
                    ResidentSize = (ulong)typeResident,
                    ChildCount = sortedObjects.Count,
                    Children = sortedObjects  // ← 添加子节点！
                });
            }
            
            if (children.Count == 0)
                return null;
            
            var totalAllocated = children.Sum(c => (decimal)c.AllocatedSize);
            var totalResident = children.Sum(c => (decimal)c.ResidentSize);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = NativeObjectsGroupName,
                AllocatedSize = (ulong)totalAllocated,
                ResidentSize = (ulong)totalResident,
                ChildCount = children.Count,
                Children = children
            };
        }

        /// <summary>
        /// 构建 Unity Subsystems 树（按 AreaName 分组）
        /// 参考: AllTrackedMemoryModelBuilder.BuildNativeTree 中的 Unity Subsystems 部分 (Line 887-900)
        /// 
        /// 分组逻辑：
        /// 1. 获取每个 NativeRootReference 的 AreaName
        /// 2. 按 AreaName 分组
        /// 3. 为每个 AreaName 创建一个节点
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildUnitySubsystemsTree(BuildContext context)
        {
            // 第一步：为每个 NativeRootReference 创建节点（可能包含子分配节点）
            // 参考: Unity Line 895 GroupItems 调用
            var rootReferenceNodes = new Dictionary<string, List<AllTrackedMemoryTreeNode>>();  // AreaName -> List of RootReference nodes
            
            foreach (var kvp in context.NativeRootReference2SizeMap)
            {
                var source = kvp.Key;
                var totalSize = kvp.Value;
                
                // 获取 ObjectName 和 AreaName
                string objectName;
                string areaName;
                
                if (source.Id == CachedSnapshot.SourceIndex.SourceId.NativeRootReference)
                {
                    objectName = _snapshot.NativeRootReferences.ObjectName[source.Index];
                    areaName = _snapshot.NativeRootReferences.AreaName[source.Index];
                }
                else
                {
                    objectName = "Unknown";
                    areaName = "Unknown";
                }
                
                // 检查是否有子分配需要展开
                List<AllTrackedMemoryTreeNode>? allocationChildren = null;
                if (context.NativeRootReference2UnsafeAllocations2SizeMap.TryGetValue(source, out var allocationSizes) && allocationSizes.Count > 0)
                {
                    allocationChildren = new List<AllTrackedMemoryTreeNode>(allocationSizes.Count);
                    
                    foreach (var allocation in allocationSizes)
                    {
                        var allocationName = ProduceNativeAllocationName(allocation.Key, truncateTypeNames: true);
                        allocationChildren.Add(new AllTrackedMemoryTreeNode
                        {
                            Id = _nextId++,
                            Name = allocationName,
                            AllocatedSize = allocation.Value.Committed,
                            ResidentSize = allocation.Value.Resident,
                            ChildCount = 0,
                            Children = null
                        });
                    }
                }
                
                // 创建 RootReference 节点
                var rootRefNode = new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = objectName,
                    AllocatedSize = totalSize.Committed,
                    ResidentSize = totalSize.Resident,
                    ChildCount = allocationChildren?.Count ?? 0,
                    Children = allocationChildren
                };
                
                // 按 AreaName 分组
                if (!rootReferenceNodes.TryGetValue(areaName, out var list))
                {
                    list = new List<AllTrackedMemoryTreeNode>();
                    rootReferenceNodes[areaName] = list;
                }
                
                list.Add(rootRefNode);
            }
            
            // 第二步：为每个 AreaName 创建分组节点
            var children = new List<AllTrackedMemoryTreeNode>();
            
            foreach (var kvp in rootReferenceNodes.OrderByDescending(g => g.Value.Sum(n => (decimal)n.AllocatedSize)))
            {
                var areaName = kvp.Key;
                var nodes = kvp.Value;
                
                var areaCommitted = nodes.Sum(n => (decimal)n.AllocatedSize);
                var areaResident = nodes.Sum(n => (decimal)n.ResidentSize);
                
                children.Add(new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = areaName,
                    AllocatedSize = (ulong)areaCommitted,
                    ResidentSize = (ulong)areaResident,
                    ChildCount = nodes.Count,
                    Children = nodes
                });
            }
            
            if (children.Count == 0)
                return null;
            
            var totalAllocated = children.Sum(c => (decimal)c.AllocatedSize);
            var totalResident = children.Sum(c => (decimal)c.ResidentSize);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = NativeSubsystemsGroupName,
                AllocatedSize = (ulong)totalAllocated,
                ResidentSize = (ulong)totalResident,
                ChildCount = children.Count,
                Children = children
            };
        }

        /// <summary>
        /// 构建 Native Reserved 树
        /// 参考: AllTrackedMemoryModelBuilder.BuildNativeTree 中的 Reserved 部分 (Line 902-921)
        /// 
        /// 简化：合并为单个节点（不按 ParentIndex 分组展开）
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildNativeReservedTree(BuildContext context)
        {
            if (context.NativeRegionIndex2SizeMap.Count == 0)
                return null;
            
            var totalCommitted = context.NativeRegionIndex2SizeMap.Values.Sum(i => (decimal)i.Committed);
            var totalResident = context.NativeRegionIndex2SizeMap.Values.Sum(i => (decimal)i.Resident);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = "Reserved",
                AllocatedSize = (ulong)totalCommitted,
                ResidentSize = (ulong)totalResident,
                ChildCount = 0,
                Children = null  // 简化：不展开子节点
            };
        }

        /// <summary>
        /// 构建 Managed 树
        /// 参考: AllTrackedMemoryModelBuilder.BuildManagedTree
        /// 
        /// 顺序（严格遵循 Unity）：
        /// 1. Managed Objects（第一个）
        /// 2. Virtual Machine（第二个）
        /// 3. Reserved（第三个）
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildManagedTree(BuildContext context)
        {
            var children = new List<AllTrackedMemoryTreeNode>();
            
            // 1. Managed Objects（第一个，在最前面！）
            var managedObjectsNode = BuildManagedObjectsTree(context);
            if (managedObjectsNode != null)
                children.Add(managedObjectsNode);
            
            // 2. Virtual Machine（第二个）
            if (context.ManagedMemoryVM > 0)
            {
                children.Add(new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = ManagedVMGroupName,
                    AllocatedSize = context.ManagedMemoryVM,
                    ResidentSize = context.ManagedMemoryVM,
                    ChildCount = 0,
                    Children = null
                });
            }
            
            // 3. Reserved（第三个）
            if (context.ManagedMemoryReserved > 0)
            {
                children.Add(new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = ManagedReservedGroupName,
                    AllocatedSize = context.ManagedMemoryReserved,
                    ResidentSize = context.ManagedMemoryReserved,
                    ChildCount = 0,
                    Children = null
                });
            }
            
            if (children.Count == 0)
                return null;
            
            var totalAllocated = children.Sum(c => (decimal)c.AllocatedSize);
            var totalResident = children.Sum(c => (decimal)c.ResidentSize);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = ManagedGroupName,
                Category = CategoryType.Managed, // 设置Category
                AllocatedSize = (ulong)totalAllocated,
                ResidentSize = (ulong)totalResident,
                ChildCount = children.Count,
                Children = children  // 不排序！保持原始顺序
            };
        }

        /// <summary>
        /// 构建 Managed Objects 树（按 ManagedType 分组，类型可展开到对象）
        /// ⚠️ 关键修正：使用已构建的TreeNode（在收集阶段构建的）
        /// 参考: AllTrackedMemoryModelBuilder.BuildTreeFromGroupByIdMap (Line 1272-1314)
        /// 
        /// Unity的逻辑：
        /// 1. 在收集阶段已经构建好了TreeViewItemData
        /// 2. 这里只是按ManagedType分组并计算总大小
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildManagedObjectsTree(BuildContext context)
        {
            if (context.ManagedTypeName2ObjectsTreeMap.Count == 0)
                return null;
            
            // 为每个 ManagedType 创建节点（使用已构建的对象节点）
            var typeChildren = new List<AllTrackedMemoryTreeNode>();
            
            foreach (var kvp in context.ManagedTypeName2ObjectsTreeMap.OrderByDescending(x => x.Value.Sum(o => (decimal)o.AllocatedSize)))
            {
                var typeSource = kvp.Key;
                var objectNodes = kvp.Value;
                
                // 获取类型名称（typeSource 是 SourceIndex(ManagedType, typeIndex)）
                var typeName = typeSource.GetName(_snapshot);
                
                // 对象节点已经构建好了，只需要排序
                var sortedObjectNodes = objectNodes.OrderByDescending(o => o.AllocatedSize).ToList();
                
                // 计算该类型的总大小
                var typeAllocated = objectNodes.Sum(o => (decimal)o.AllocatedSize);
                var typeResident = objectNodes.Sum(o => (decimal)o.ResidentSize);
                
                // 创建类型节点（可展开）
                typeChildren.Add(new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = typeName,
                    AllocatedSize = (ulong)typeAllocated,
                    ResidentSize = (ulong)typeResident,
                    ChildCount = sortedObjectNodes.Count,
                    Children = sortedObjectNodes  // ← 使用已构建的子节点！
                });
            }
            
            var totalAllocated = typeChildren.Sum(c => (decimal)c.AllocatedSize);
            var totalResident = typeChildren.Sum(c => (decimal)c.ResidentSize);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = ManagedObjectsGroupName,
                AllocatedSize = (ulong)totalAllocated,
                ResidentSize = (ulong)totalResident,
                ChildCount = typeChildren.Count,
                Children = typeChildren
            };
        }

        /// <summary>
        /// 构建 Executables & Mapped 树（按名称分组）
        /// 参考: AllTrackedMemoryModelBuilder.BuildTreeFromGroupByNameMap (Line 1316-1358)
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildExecutablesTree(BuildContext context)
        {
            if (context.ExecutablesName2SizeMap.Count == 0)
                return null;
            
            var children = new List<AllTrackedMemoryTreeNode>();
            
            foreach (var kvp in context.ExecutablesName2SizeMap.OrderByDescending(x => x.Value.Committed))
            {
                children.Add(new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = kvp.Key,
                    AllocatedSize = kvp.Value.Committed,
                    ResidentSize = kvp.Value.Resident,
                    ChildCount = 0,
                    Children = null
                });
            }
            
            var totalAllocated = children.Sum(c => (decimal)c.AllocatedSize);
            var totalResident = children.Sum(c => (decimal)c.ResidentSize);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = ExecutablesGroupName,
                Category = CategoryType.ExecutablesAndMapped, // 设置Category
                AllocatedSize = (ulong)totalAllocated,
                ResidentSize = (ulong)totalResident,
                ChildCount = children.Count,
                Children = children
            };
        }

        /// <summary>
        /// 构建 Untracked 树（按名称分组）
        /// 参考: AllTrackedMemoryModelBuilder.BuildTreeFromGroupByNameMap (Line 1316-1358)
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildUntrackedTree(BuildContext context)
        {
            if (context.UntrackedRegionsName2SizeMap.Count == 0)
                return null;
            
            var children = new List<AllTrackedMemoryTreeNode>();
            
            foreach (var kvp in context.UntrackedRegionsName2SizeMap.OrderByDescending(x => x.Value.Committed))
            {
                children.Add(new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = kvp.Key,
                    AllocatedSize = kvp.Value.Committed,
                    ResidentSize = kvp.Value.Resident,
                    ChildCount = 0,
                    Children = null
                });
            }
            
            var totalAllocated = children.Sum(c => (decimal)c.AllocatedSize);
            var totalResident = children.Sum(c => (decimal)c.ResidentSize);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = UntrackedGroupName,
                Category = CategoryType.UnknownEstimated, // 设置Category
                AllocatedSize = (ulong)totalAllocated,
                ResidentSize = (ulong)totalResident,
                ChildCount = children.Count,
                Children = children,
                Unreliable = true  // Untracked 是估算值
            };
        }

        /// <summary>
        /// 构建 Graphics 树
        /// 参考: AllTrackedMemoryModelBuilder.BuildGraphicsMemoryTree (Line 1007-1092)
        /// 
        /// 分组逻辑（Line 1022-1038）：
        /// 1. NativeObject → 按 NativeType 分组（Texture2D、RenderTexture 等）
        /// 2. NativeRootReference → 保持原样（Subsystem 名称）
        /// 3. GfxResource → 如果有 RootId，按 NativeRootReference（Subsystem）分组
        /// 4. 标记为 unreliable（估算值）
        /// 
        /// ⚠️ 修正：实现完整的分组和展开逻辑（参考Unity GroupItems Line 1087）
        /// </summary>
        private AllTrackedMemoryTreeNode? BuildGraphicsTree(BuildContext context)
        {
            if (context.GfxObjectIndex2SizeMap.Count == 0)
                return null;
            
            // 第一步：为每个Graphics对象创建节点，并按GroupKey分组
            // 参考: Unity Line 1087 GroupItems
            var groupKeyToObjects = new Dictionary<CachedSnapshot.SourceIndex, List<AllTrackedMemoryTreeNode>>();
            
            foreach (var kvp in context.GfxObjectIndex2SizeMap)
            {
                var source = kvp.Key;
                var size = kvp.Value;
                var groupKey = GetGraphicsGroupKey(source);
                
                // 创建单个对象节点
                var objectName = GetSafeName(source);
                var objectNode = new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = objectName,
                    AllocatedSize = size.Committed,
                    ResidentSize = size.Resident,
                    ChildCount = 0,
                    Children = null,
                    Unreliable = true,
                    Source = source
                };
                
                // 按GroupKey分组
                if (!groupKeyToObjects.TryGetValue(groupKey, out var list))
                {
                    list = new List<AllTrackedMemoryTreeNode>();
                    groupKeyToObjects[groupKey] = list;
                }
                list.Add(objectNode);
            }
            
            // 第二步：为每个GroupKey创建分组节点
            var children = new List<AllTrackedMemoryTreeNode>();
            
            foreach (var kvp in groupKeyToObjects.OrderByDescending(g => g.Value.Sum(n => (decimal)n.AllocatedSize)))
            {
                var groupKey = kvp.Key;
                var objectNodes = kvp.Value;
                
                // 排序子节点（按分配大小降序）
                var sortedObjects = objectNodes.OrderByDescending(n => n.AllocatedSize).ToList();
                
                var groupName = GetGraphicsGroupName(groupKey);
                var typeCommitted = sortedObjects.Sum(n => (decimal)n.AllocatedSize);
                var typeResident = sortedObjects.Sum(n => (decimal)n.ResidentSize);
                
                children.Add(new AllTrackedMemoryTreeNode
                {
                    Id = _nextId++,
                    Name = groupName,
                    AllocatedSize = (ulong)typeCommitted,
                    ResidentSize = (ulong)typeResident,
                    ChildCount = sortedObjects.Count,
                    Children = sortedObjects,  // ← 添加子节点！
                    Unreliable = true
                });
            }
            
            if (children.Count == 0)
                return null;
            
            var totalAllocated = children.Sum(c => (decimal)c.AllocatedSize);
            var totalResident = children.Sum(c => (decimal)c.ResidentSize);
            
            return new AllTrackedMemoryTreeNode
            {
                Id = _nextId++,
                Name = GraphicsGroupName,
                Category = CategoryType.Graphics, // 设置Category
                AllocatedSize = (ulong)totalAllocated,
                ResidentSize = (ulong)totalResident,
                ChildCount = children.Count,
                Children = children,
                Unreliable = true  // 标记为估算值（Line 1071）
            };
        }
        
        /// <summary>
        /// 获取 Graphics 对象的分组 Key
        /// 参考: AllTrackedMemoryModelBuilder.ObjectIndex2GroupKey (Line 1022-1038)
        /// </summary>
        private CachedSnapshot.SourceIndex GetGraphicsGroupKey(CachedSnapshot.SourceIndex source)
        {
            switch (source.Id)
            {
                case CachedSnapshot.SourceIndex.SourceId.NativeObject:
                    // 按 NativeType 分组
                    var nativeTypeIndex = _snapshot.NativeObjects.NativeTypeArrayIndex[source.Index];
                    return new CachedSnapshot.SourceIndex(CachedSnapshot.SourceIndex.SourceId.NativeType, nativeTypeIndex);
                
                case CachedSnapshot.SourceIndex.SourceId.NativeRootReference:
                    // 保持原样（Subsystem 名称）
                    return source;
                
                case CachedSnapshot.SourceIndex.SourceId.GfxResource:
                    // 如果有 RootId，按 NativeRootReference（Subsystem）分组
                    var rootId = _snapshot.NativeGfxResourceReferences.RootId[source.Index];
                    if (rootId != CachedSnapshot.NativeRootReferenceEntriesCache.InvalidRootIndex)
                    {
                        var rootIndex = _snapshot.NativeRootReferences.IdToIndex[rootId];
                        return new CachedSnapshot.SourceIndex(CachedSnapshot.SourceIndex.SourceId.NativeRootReference, rootIndex);
                    }
                    return new CachedSnapshot.SourceIndex();  // Invalid
                
                default:
                    return new CachedSnapshot.SourceIndex();  // Invalid
            }
        }
        
        /// <summary>
        /// 获取 Graphics 分组的名称
        /// 参考: Unity Line 1040 GroupKey2Name(SourceIndex x) => x.GetName(snapshot)
        /// </summary>
        private string GetGraphicsGroupName(CachedSnapshot.SourceIndex groupKey)
        {
            // 使用 GetSafeName 方法，避免空名称
            return GetSafeName(groupKey);
        }

        #endregion

        #region ItemPath 过滤

        /// <summary>
        /// 返回树中所有通过提供的 filterPath 的项。每个 filter 应用于树的一层，从根开始。子节点将被移除。
        /// 参考: Unity AllTrackedMemoryModelBuilder.BuildItemsAtPathInTreeExclusively (Line 1397-1444)
        /// </summary>
        /// <param name="filterPath">过滤路径（每个字符串对应一层）</param>
        /// <param name="tree">要过滤的树</param>
        /// <returns>过滤后的节点列表（不包含子节点）</returns>
        private List<AllTrackedMemoryTreeNode> BuildItemsAtPathInTreeExclusively(
            List<string> filterPath,
            List<AllTrackedMemoryTreeNode> tree)
        {
            var itemsAtPath = new List<AllTrackedMemoryTreeNode>();

            var items = tree;
            var filterPathQueue = new Queue<string>(filterPath);
            
            while (filterPathQueue.Count > 0)
            {
                var found = false;
                AllTrackedMemoryTreeNode itemOnPath = null;
                var filter = filterPathQueue.Dequeue();
                
                foreach (var item in items)
                {
                    // Unity 使用 ITextFilter.Passes(item.data.Name)
                    // 我们简化为字符串相等比较
                    if (item.Name == filter)
                    {
                        found = true;
                        itemOnPath = item;

                        // 如果我们在路径末端，继续迭代以收集所有匹配路径的兄弟节点。否则 break 以进入下一层。
                        if (filterPathQueue.Count == 0)
                            itemsAtPath.Add(item);
                        else
                            break;
                    }
                }
                
                // 搜索失败
                if (!found)
                    break;

                // 搜索成功。进入树的下一层。
                items = itemOnPath?.Children ?? new List<AllTrackedMemoryTreeNode>();
            }

            // 重建项（移除子节点）
            var exclusiveItems = new List<AllTrackedMemoryTreeNode>(itemsAtPath.Count);
            foreach (var item in itemsAtPath)
            {
                var itemWithoutChildren = new AllTrackedMemoryTreeNode
                {
                    Id = item.Id,
                    Name = item.Name,
                    Category = item.Category,
                    AllocatedSize = item.AllocatedSize,
                    ResidentSize = item.ResidentSize,
                    Percentage = item.Percentage,
                    ChildCount = 0,  // 移除子节点
                    Children = null,
                    Source = item.Source,
                    Unreliable = item.Unreliable
                };
                exclusiveItems.Add(itemWithoutChildren);
            }

            return exclusiveItems;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 安全获取对象名称，如果为空则返回 "<No Name>"
        /// </summary>
        private string GetSafeName(CachedSnapshot.SourceIndex source)
        {
            var name = source.GetName(_snapshot);
            return string.IsNullOrEmpty(name) ? NoNamePlaceholder : name;
        }

        /// <summary>
        /// 递归计算百分比
        /// </summary>
        private void CalculatePercentages(List<AllTrackedMemoryTreeNode> nodes, ulong totalMemory)
        {
            foreach (var node in nodes)
            {
                node.Percentage = (double)node.AllocatedSize / totalMemory;
                
                if (node.Children != null)
                {
                    CalculatePercentages(node.Children, totalMemory);
                }
            }
        }

        /// <summary>
        /// 从Untracked中减去Graphics资源大小
        /// 参考: Unity的ReduceUntrackedByGraphicsResourcesSize (Line 819-838)
        /// 
        /// Unity逻辑：
        /// 1. 按大小降序排序Untracked条目
        /// 2. 从最大的开始减去Graphics大小
        /// 3. 如果某个条目大于要减去的大小，减少它并返回
        /// 4. 否则，完全移除该条目并继续下一个
        /// </summary>
        private void ReduceUntrackedByGraphicsResourcesSize(
            Dictionary<string, MemorySize> untrackedMap, 
            MemorySize graphicsMemorySize)
        {
            // 按大小降序排序
            var untrackedMem = untrackedMap.ToList();
            untrackedMem.Sort((l, r) => -l.Value.Committed.CompareTo(r.Value.Committed));
            
            for (int i = 0; i < untrackedMem.Count; i++)
            {
                var item = untrackedMem[i];
                if (item.Value.Committed > graphicsMemorySize.Committed)
                {
                    // 减少这个条目的大小
                    untrackedMap[item.Key] = new MemorySize(
                        item.Value.Committed - graphicsMemorySize.Committed, 0);
                    return;
                }
                
                // 完全移除这个条目
                graphicsMemorySize -= item.Value;
                untrackedMap.Remove(item.Key);
            }
        }

        #endregion

        #region 数据结构

        /// <summary>
        /// 构建上下文
        /// 参考: AllTrackedMemoryModelBuilder.BuildContext
        /// 
        /// ⚠️ 关键修正：与Unity官方保持完全一致
        /// - Managed Objects 在收集阶段就构建为 TreeNode（而不是存储原始数据）
        /// - 使用 MemorySize (Committed + Resident) 而不是自定义 MemoryItem
        /// </summary>
        private class BuildContext
        {
            // 总内存
            public ulong TotalMemory { get; set; }
            
            // Native 内存
            public Dictionary<CachedSnapshot.SourceIndex, MemorySize> NativeObjectIndex2SizeMap { get; } = new();
            public Dictionary<CachedSnapshot.SourceIndex, MemorySize> NativeRootReference2SizeMap { get; } = new();
            
            // 子分配：NativeRootReference -> NativeAllocations -> MemorySize
            public Dictionary<CachedSnapshot.SourceIndex, Dictionary<CachedSnapshot.SourceIndex, MemorySize>> NativeRootReference2UnsafeAllocations2SizeMap { get; } = new();
            
            public Dictionary<CachedSnapshot.SourceIndex, MemorySize> NativeRegionIndex2SizeMap { get; } = new();
            
            // Managed 内存
            /// <summary>
            /// ⚠️ 关键修正：存储已构建的TreeNode，而不是原始数据
            /// Unity 在收集阶段就构建 TreeViewItemData，我们也在收集阶段构建 AllTrackedMemoryTreeNode
            /// 
            /// 键：SourceIndex (SourceId.ManagedType, typeIndex)
            /// 值：该类型的所有对象节点列表
            /// </summary>
            public Dictionary<CachedSnapshot.SourceIndex, List<AllTrackedMemoryTreeNode>> ManagedTypeName2ObjectsTreeMap { get; } = new();
            
            public ulong ManagedMemoryVM { get; set; }
            public ulong ManagedMemoryReserved { get; set; }
            
            // Graphics 内存
            public Dictionary<CachedSnapshot.SourceIndex, MemorySize> GfxObjectIndex2SizeMap { get; } = new();
            public ulong UntrackedGraphicsResources { get; set; }
            
            // System Memory Regions（按名称分组）
            public Dictionary<string, MemorySize> ExecutablesName2SizeMap { get; } = new();
            public Dictionary<string, MemorySize> UntrackedRegionsName2SizeMap { get; } = new();
        }

        /// <summary>
        /// 内存大小（与Unity官方的MemorySize对应）
        /// Committed: 分配的内存（虚拟地址空间）
        /// Resident: 实际占用的物理内存
        /// </summary>
        public struct MemorySize
        {
            public ulong Committed { get; set; }
            public ulong Resident { get; set; }

            public MemorySize(ulong committed, ulong resident)
            {
                Committed = committed;
                Resident = resident;
            }

            public static MemorySize operator +(MemorySize a, MemorySize b)
            {
                return new MemorySize(a.Committed + b.Committed, a.Resident + b.Resident);
            }

            public static MemorySize operator -(MemorySize a, MemorySize b)
            {
                return new MemorySize(
                    a.Committed > b.Committed ? a.Committed - b.Committed : 0,
                    a.Resident > b.Resident ? a.Resident - b.Resident : 0);
            }
        }

        #endregion
    }

    /// <summary>
    /// Dictionary 扩展方法
    /// 参考: Unity 的 AddItemSizeToMap
    /// </summary>
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// 添加或更新内存大小到字典
        /// </summary>
        public static void AddOrUpdate<TKey>(
            this Dictionary<TKey, AllTrackedMemoryDataBuilder.MemorySize> dict, 
            TKey key, 
            AllTrackedMemoryDataBuilder.MemorySize itemSize) 
            where TKey : notnull
        {
            if (dict.TryGetValue(key, out var existing))
            {
                dict[key] = existing + itemSize;
            }
            else
            {
                dict[key] = itemSize;
            }
        }

        /// <summary>
        /// 添加或更新内存大小到字典（方便调用）
        /// </summary>
        public static void AddOrUpdate<TKey>(
            this Dictionary<TKey, AllTrackedMemoryDataBuilder.MemorySize> dict, 
            TKey key, 
            ulong committed, 
            ulong resident) 
            where TKey : notnull
        {
            dict.AddOrUpdate(key, new AllTrackedMemoryDataBuilder.MemorySize(committed, resident));
        }
        
        /// <summary>
        /// 获取或创建列表并添加项
        /// 参考: Unity 的 GetAndAddToListOrCreateList
        /// </summary>
        public static void GetAndAddToListOrCreateList<TKey, TValue>(
            this Dictionary<TKey, List<TValue>> dict,
            TKey key,
            TValue item)
            where TKey : notnull
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<TValue>();
                dict[key] = list;
            }
            list.Add(item);
        }
    }
}
