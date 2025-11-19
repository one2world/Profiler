using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// AllTrackedMemoryModel构建器 - Part 3: 树生成方法
    /// 对应Unity的树生成逻辑 (Line 147-183, 849-1233)
    /// </summary>
    internal partial class AllTrackedMemoryModelBuilder
    {
        #region Phase 2: Generate Tree From Context

        /// <summary>
        /// 第二阶段：从上下文生成完整的内存树
        /// 对应Unity BuildAllMemoryBreakdown方法 (Line 147-183)
        /// </summary>
        private ObservableCollection<TreeNode<MemoryItemData>> BuildAllMemoryBreakdown(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            var rootNodes = new ObservableCollection<TreeNode<MemoryItemData>>();

            // 生成Native分组
            if (!args.ExcludeNative)
            {
                var nativeGroup = BuildNativeGroup(snapshot, args, context);
                if (nativeGroup != null)
                    rootNodes.Add(nativeGroup);
            }

            // 生成Managed分组
            if (!args.ExcludeManaged)
            {
                var managedGroup = BuildManagedGroup(snapshot, args, context);
                if (managedGroup != null)
                    rootNodes.Add(managedGroup);
            }

            // 生成Executables分组
            var executablesGroup = BuildExecutablesGroup(snapshot, context);
            if (executablesGroup != null)
                rootNodes.Add(executablesGroup);

            // 生成Untracked分组
            var untrackedName = args.BreakdownGraphicsResources ? UntrackedEstimatedGroupName : UntrackedGroupName;
            var untrackedGroup = BuildUntrackedGroup(snapshot, context, untrackedName);
            if (untrackedGroup != null)
                rootNodes.Add(untrackedGroup);

            // 生成Graphics分组
            if (!args.ExcludeGraphics && args.BreakdownGraphicsResources)
            {
                var graphicsGroup = BuildGraphicsGroup(snapshot, args, context);
                if (graphicsGroup != null)
                    rootNodes.Add(graphicsGroup);
            }

            return rootNodes;
        }

        #endregion

        #region Tree Building - Native Group

        /// <summary>
        /// 构建Native分组
        /// 对应Unity BuildNativeTree方法 (Line 849-947)
        /// </summary>
        private TreeNode<MemoryItemData> BuildNativeGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            var children = new List<TreeNode<MemoryItemData>>();

            // Native Objects子组
            var nativeObjectsNode = BuildNativeObjectsSubGroup(snapshot, context);
            if (nativeObjectsNode != null)
                children.Add(nativeObjectsNode);

            // Unity Subsystems子组（Native Root References）
            var subsystemsNode = BuildNativeSubsystemsSubGroup(snapshot, context);
            if (subsystemsNode != null)
                children.Add(subsystemsNode);

            // Reserved子组
            if (args.BreakdownNativeReserved)
            {
                var reservedNode = BuildNativeReservedSubGroup(snapshot, context);
                if (reservedNode != null)
                    children.Add(reservedNode);
            }

            // 如果没有子节点，返回null
            if (children.Count == 0)
                return null;

            // 计算总大小
            long totalSize = children.Sum(child => child.Data?.Size ?? 0);

            // 创建Native Group节点
            var nativeGroupData = new MemoryItemData(NativeGroupName, totalSize);

            var nativeGroupNode = new TreeNode<MemoryItemData>(nativeGroupData);
            
            foreach (var child in children)
                nativeGroupNode.AddChild(child);

            return nativeGroupNode;
        }

        /// <summary>
        /// 构建Native Objects子组（按NativeType分组）
        /// 对应Unity BuildNativeTree中的Native Objects部分 (Line 861-885)
        /// </summary>
        private TreeNode<MemoryItemData> BuildNativeObjectsSubGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildContext context)
        {
            if (context.NativeObjectIndex2SizeMap.Count == 0)
                return null;

            // 按NativeType分组
            var typeGroups = new Dictionary<int, List<(SourceIndex index, long size)>>();

            foreach (var kvp in context.NativeObjectIndex2SizeMap)
            {
                var objIndex = kvp.Key.Index;
                var size = kvp.Value;
                var nativeTypeIndex = snapshot.NativeObjects.NativeTypeArrayIndex[objIndex];

                if (!typeGroups.ContainsKey(nativeTypeIndex))
                    typeGroups[nativeTypeIndex] = new List<(SourceIndex, long)>();

                typeGroups[nativeTypeIndex].Add((kvp.Key, size));
            }

            // 创建Native Objects Group节点
            var nativeObjectsGroupData = new MemoryItemData(
                NativeObjectsGroupName,
                context.NativeObjectIndex2SizeMap.Values.Sum());

            var nativeObjectsGroupNode = new TreeNode<MemoryItemData>(nativeObjectsGroupData);

            // 为每个Type创建子节点
            foreach (var kvp in typeGroups.OrderByDescending(x => x.Value.Sum(y => y.size)))
            {
                var nativeTypeIndex = kvp.Key;
                var objects = kvp.Value;
                var totalSize = objects.Sum(x => x.size);

                var typeName = snapshot.NativeTypes.TypeName[nativeTypeIndex];
                var typeData = new MemoryItemData(typeName, totalSize);

                var typeNode = new TreeNode<MemoryItemData>(typeData);

                // 添加该类型的所有对象（限制数量避免UI卡顿）
                const int maxObjectsPerType = 100;
                int count = 0;
                foreach (var (index, size) in objects.OrderByDescending(x => x.size))
                {
                    if (count >= maxObjectsPerType)
                    {
                        // 添加"...more"提示
                        var moreCount = objects.Count - maxObjectsPerType;
                        var moreData = new MemoryItemData($"... and {moreCount} more", 0);
                        typeNode.AddChild(new TreeNode<MemoryItemData>(moreData));
                        break;
                    }

                    var objectName = index.GetName(snapshot);
                    var objectData = new MemoryItemData(objectName, size);
                    typeNode.AddChild(new TreeNode<MemoryItemData>(objectData));
                    count++;
                }

                nativeObjectsGroupNode.AddChild(typeNode);
            }

            return nativeObjectsGroupNode;
        }

        /// <summary>
        /// 构建Unity Subsystems子组（Native Root References按AreaName分组）
        /// 对应Unity BuildNativeTree中的Unity Subsystems部分 (Line 887-899)
        /// </summary>
        private TreeNode<MemoryItemData> BuildNativeSubsystemsSubGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildContext context)
        {
            if (context.NativeRootReference2SizeMap.Count == 0)
                return null;

            // 按AreaName分组
            var areaGroups = new Dictionary<string, List<(SourceIndex index, long size)>>();

            foreach (var kvp in context.NativeRootReference2SizeMap)
            {
                var refIndex = kvp.Key.Index;
                var size = kvp.Value;
                var areaName = snapshot.NativeRootReferences.AreaName[refIndex];

                if (!areaGroups.ContainsKey(areaName))
                    areaGroups[areaName] = new List<(SourceIndex, long)>();

                areaGroups[areaName].Add((kvp.Key, size));
            }

            // 创建Unity Subsystems Group节点
            var subsystemsGroupData = new MemoryItemData(
                NativeSubsystemsGroupName,
                context.NativeRootReference2SizeMap.Values.Sum());

            var subsystemsGroupNode = new TreeNode<MemoryItemData>(subsystemsGroupData);

            // 为每个Area创建子节点
            foreach (var kvp in areaGroups.OrderByDescending(x => x.Value.Sum(y => y.size)))
            {
                var areaName = kvp.Key;
                var references = kvp.Value;
                var totalSize = references.Sum(x => x.size);

                var areaData = new MemoryItemData(areaName, totalSize);

                var areaNode = new TreeNode<MemoryItemData>(areaData);

                // 添加该Area的所有Root References
                foreach (var (index, size) in references.OrderByDescending(x => x.size))
                {
                    var objectName = snapshot.NativeRootReferences.ObjectName[index.Index];
                    var refData = new MemoryItemData(objectName, size);
                    areaNode.AddChild(new TreeNode<MemoryItemData>(refData));
                }

                subsystemsGroupNode.AddChild(areaNode);
            }

            return subsystemsGroupNode;
        }

        /// <summary>
        /// 构建Native Reserved子组
        /// 对应Unity BuildNativeTree中的Reserved部分 (Line 901-920)
        /// </summary>
        private TreeNode<MemoryItemData> BuildNativeReservedSubGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildContext context)
        {
            if (context.NativeRegionName2SizeMap.Count == 0)
                return null;

            var totalSize = context.NativeRegionName2SizeMap.Values.Sum();

            var reservedGroupData = new MemoryItemData(NativeReservedGroupName, totalSize);

            var reservedGroupNode = new TreeNode<MemoryItemData>(reservedGroupData);

            // 添加每个Region
            foreach (var kvp in context.NativeRegionName2SizeMap.OrderByDescending(x => x.Value))
            {
                var regionName = kvp.Key.GetName(snapshot);
                var regionData = new MemoryItemData(regionName, kvp.Value);
                reservedGroupNode.AddChild(new TreeNode<MemoryItemData>(regionData));
            }

            return reservedGroupNode;
        }

        #endregion

        #region Tree Building - Managed Group

        /// <summary>
        /// 构建Managed分组
        /// 对应Unity BuildManagedTree方法 (Line 949-1012)
        /// </summary>
        private TreeNode<MemoryItemData> BuildManagedGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            var children = new List<TreeNode<MemoryItemData>>();

            // Managed Objects子组（按Type分组）
            var managedObjectsNode = BuildManagedObjectsSubGroup(snapshot, context);
            if (managedObjectsNode != null)
                children.Add(managedObjectsNode);

            // Virtual Machine子组
            if (context.ManagedMemoryVM > 0)
            {
                var vmData = new MemoryItemData(ManagedVMGroupName, context.ManagedMemoryVM);
                children.Add(new TreeNode<MemoryItemData>(vmData));
            }

            // Reserved子组（GC Heap未使用部分）
            if (context.ManagedMemoryReserved > 0)
            {
                var reservedData = new MemoryItemData(ManagedReservedGroupName, context.ManagedMemoryReserved);
                children.Add(new TreeNode<MemoryItemData>(reservedData));
            }

            // 如果没有子节点，返回null
            if (children.Count == 0)
                return null;

            // 计算总大小
            long totalSize = children.Sum(child => child.Data?.Size ?? 0);

            // 创建Managed Group节点
            var managedGroupData = new MemoryItemData(ManagedGroupName, totalSize);

            var managedGroupNode = new TreeNode<MemoryItemData>(managedGroupData);
            
            foreach (var child in children)
                managedGroupNode.AddChild(child);

            return managedGroupNode;
        }

        /// <summary>
        /// 构建Managed Objects子组（按Type分组）
        /// 对应Unity BuildManagedTree中的Managed Objects部分 (Line 964-993)
        /// </summary>
        private TreeNode<MemoryItemData> BuildManagedObjectsSubGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildContext context)
        {
            if (context.ManagedTypeName2ObjectsTreeMap.Count == 0)
                return null;

            // 计算总大小
            long totalSize = 0;
            foreach (var objectsList in context.ManagedTypeName2ObjectsTreeMap.Values)
            {
                totalSize += objectsList.Sum(obj => obj.Data?.Size ?? 0);
            }

            // 创建Managed Objects Group节点
            var managedObjectsGroupData = new MemoryItemData(ManagedObjectsGroupName, totalSize);

            var managedObjectsGroupNode = new TreeNode<MemoryItemData>(managedObjectsGroupData);

            // 为每个Type创建子节点
            foreach (var kvp in context.ManagedTypeName2ObjectsTreeMap.OrderByDescending(x => x.Value.Sum(y => y.Data?.Size ?? 0)))
            {
                var typeIndex = kvp.Key.Index;
                var objects = kvp.Value;
                var typeSize = objects.Sum(obj => obj.Data?.Size ?? 0);

                var typeName = snapshot.TypeDescriptions.TypeDescriptionName[typeIndex];
                var typeData = new MemoryItemData(typeName, typeSize);

                var typeNode = new TreeNode<MemoryItemData>(typeData);

                // 添加该类型的所有对象（限制数量）
                const int maxObjectsPerType = 100;
                int count = 0;
                foreach (var obj in objects.OrderByDescending(x => x.Data?.Size ?? 0))
                {
                    if (count >= maxObjectsPerType)
                    {
                        var moreCount = objects.Count - maxObjectsPerType;
                        var moreData = new MemoryItemData($"... and {moreCount} more", 0);
                        typeNode.AddChild(new TreeNode<MemoryItemData>(moreData));
                        break;
                    }

                    typeNode.AddChild(obj);
                    count++;
                }

                managedObjectsGroupNode.AddChild(typeNode);
            }

            return managedObjectsGroupNode;
        }

        #endregion

        #region Tree Building - Graphics/Executables/Untracked Groups

        /// <summary>
        /// 构建Graphics分组
        /// 对应Unity BuildGraphicsMemoryTree方法 (Line 1068-1127)
        /// </summary>
        private TreeNode<MemoryItemData> BuildGraphicsGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildArgs args,
            AllTrackedMemoryBuildContext context)
        {
            long totalSize = context.GetGraphicsTotalSize();
            if (totalSize == 0)
                return null;

            var graphicsGroupData = new MemoryItemData(GraphicsGroupName, totalSize);

            var graphicsGroupNode = new TreeNode<MemoryItemData>(graphicsGroupData);

            // Graphics Resources子组
            if (context.GfxObjectIndex2SizeMap.Count > 0)
            {
                var gfxResourcesData = new MemoryItemData(
                    GraphicsResourcesGroupName,
                    context.GfxObjectIndex2SizeMap.Values.Sum());
                
                var gfxResourcesNode = new TreeNode<MemoryItemData>(gfxResourcesData);

                foreach (var kvp in context.GfxObjectIndex2SizeMap.OrderByDescending(x => x.Value))
                {
                    var resourceName = kvp.Key.GetName(snapshot);
                    var resourceData = new MemoryItemData(resourceName, kvp.Value);
                    gfxResourcesNode.AddChild(new TreeNode<MemoryItemData>(resourceData));
                }

                graphicsGroupNode.AddChild(gfxResourcesNode);
            }

            // Untracked Graphics Resources
            if (context.UntrackedGraphicsResources > 0)
            {
                var untrackedGfxData = new MemoryItemData("Untracked Graphics", context.UntrackedGraphicsResources);
                graphicsGroupNode.AddChild(new TreeNode<MemoryItemData>(untrackedGfxData));
            }

            return graphicsGroupNode;
        }

        /// <summary>
        /// 构建Executables分组
        /// 对应Unity BuildTreeFromGroupByNameMap (Executables) (Line 164)
        /// </summary>
        private TreeNode<MemoryItemData> BuildExecutablesGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildContext context)
        {
            if (context.ExecutablesName2SizeMap.Count == 0)
                return null;

            long totalSize = context.ExecutablesName2SizeMap.Values.Sum();

            var executablesGroupData = new MemoryItemData(ExecutablesGroupName, totalSize);

            var executablesGroupNode = new TreeNode<MemoryItemData>(executablesGroupData);

            foreach (var kvp in context.ExecutablesName2SizeMap.OrderByDescending(x => x.Value))
            {
                var executableData = new MemoryItemData(kvp.Key, kvp.Value);
                executablesGroupNode.AddChild(new TreeNode<MemoryItemData>(executableData));
            }

            return executablesGroupNode;
        }

        /// <summary>
        /// 构建Untracked分组
        /// 对应Unity BuildTreeFromGroupByNameMap (Untracked) (Line 168-171)
        /// </summary>
        private TreeNode<MemoryItemData> BuildUntrackedGroup(
            CachedSnapshot snapshot,
            AllTrackedMemoryBuildContext context,
            string groupName)
        {
            if (context.UntrackedRegionsName2SizeMap.Count == 0)
                return null;

            long totalSize = context.UntrackedRegionsName2SizeMap.Values.Sum();

            var untrackedGroupData = new MemoryItemData(groupName, totalSize);

            var untrackedGroupNode = new TreeNode<MemoryItemData>(untrackedGroupData);

            foreach (var kvp in context.UntrackedRegionsName2SizeMap.OrderByDescending(x => x.Value))
            {
                var regionData = new MemoryItemData(kvp.Key, kvp.Value);
                untrackedGroupNode.AddChild(new TreeNode<MemoryItemData>(regionData));
            }

            return untrackedGroupNode;
        }

        #endregion
    }
}

