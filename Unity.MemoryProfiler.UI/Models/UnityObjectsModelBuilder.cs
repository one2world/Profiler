using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.UI.Models;
using Unity.MemoryProfiler.UI.Utilities;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// UnityObjectsModel的构建器
    /// 等价于Unity官方的UnityObjectsModelBuilder
    /// 参考：Editor/UI/Analysis/Breakdowns/UnityObjects/Data/UnityObjectsModelBuilder.cs
    /// </summary>
    internal class UnityObjectsModelBuilder : ModelBuilderBase<UnityObjectsModel, UnityObjectsBuildArgs>
    {
        private int m_ItemId;

        public UnityObjectsModelBuilder()
        {
            m_ItemId = 0; // WPF doesn't need IAnalysisViewSelectable.Category.FirstDynamicId
        }

        public override UnityObjectsModel Build(CachedSnapshot snapshot, UnityObjectsBuildArgs args)
        {
            // 检查快照是否支持此breakdown
            if (!CanBuildBreakdownForSnapshot(snapshot))
                throw new UnsupportedSnapshotVersionException(snapshot);

            // 构建需要按Managed Type区分的Native Unity Object基类类型列表
            // 这些类型（ScriptableObject, MonoBehaviour等）由用户广泛继承
            var nativeUnityObjectBaseTypesToDisambiguateByManagedType = 
                BuildListOfNativeUnityObjectBaseTypesToDisambiguateByManagedType(snapshot);

            // 构建按Type分组的Unity Objects树
            var rootNodes = BuildUnityObjectsGroupedByType(
                snapshot, 
                nativeUnityObjectBaseTypesToDisambiguateByManagedType,
                args, 
                out var totalMemoryInSnapshot);

            // 如果需要扁平化层次结构
            if (args.FlattenHierarchy)
                rootNodes = TreeModelUtility.RetrieveLeafNodesOfTree(rootNodes);

            // 创建模型
            var model = new UnityObjectsModel(rootNodes, totalMemoryInSnapshot, args.SelectionProcessor);
            return model;
        }

        /// <summary>
        /// 检查快照是否可以构建此breakdown
        /// </summary>
        protected static bool CanBuildBreakdownForSnapshot(CachedSnapshot snapshot)
        {
            return true; // 目前总是返回true
        }

        /// <summary>
        /// 构建需要按Managed Type区分的Native Unity Object基类类型列表
        /// 这些是用户广泛继承的类型（ScriptableObject, MonoBehaviour等）
        /// </summary>
        protected static HashSet<SourceIndex> BuildListOfNativeUnityObjectBaseTypesToDisambiguateByManagedType(
            CachedSnapshot snapshot)
        {
            var listOfNativeTypes = new HashSet<SourceIndex>();

            // ScriptableObject (Editor)
            if (snapshot.NativeTypes.EditorScriptableObjectIdx >= NativeTypeEntriesCache.FirstValidTypeIndex)
                listOfNativeTypes.Add(new SourceIndex(SourceIndex.SourceId.NativeType, 
                    snapshot.NativeTypes.EditorScriptableObjectIdx));

            // ScriptableObject (Runtime)
            if (snapshot.NativeTypes.ScriptableObjectIdx >= NativeTypeEntriesCache.FirstValidTypeIndex)
                listOfNativeTypes.Add(new SourceIndex(SourceIndex.SourceId.NativeType, 
                    snapshot.NativeTypes.ScriptableObjectIdx));

            // MonoBehaviour
            if (snapshot.NativeTypes.MonoBehaviourIdx >= NativeTypeEntriesCache.FirstValidTypeIndex)
                listOfNativeTypes.Add(new SourceIndex(SourceIndex.SourceId.NativeType, 
                    snapshot.NativeTypes.MonoBehaviourIdx));

            return listOfNativeTypes;
        }

        /// <summary>
        /// 构建按Type分组的Unity Objects树
        /// </summary>
        List<TreeNode<UnityObjectsItemData>> BuildUnityObjectsGroupedByType(
            CachedSnapshot snapshot,
            HashSet<SourceIndex> nativeUnityObjectBaseTypesToDisambiguateByManagedType,
            in UnityObjectsBuildArgs args,
            out MemorySize totalMemoryInSnapshot)
        {
            // 构建Type索引到Unity Objects的映射
            BuildUnityObjectTypeIndexToUnityObjectsMapForSnapshot(
                snapshot,
                args,
                nativeUnityObjectBaseTypesToDisambiguateByManagedType,
                out var typeIndexToTypeObjectsMap,
                out var disambiguatedTypeIndexToTypeObjectsMap,
                out var nonDisambiguatedTechnicallyManagedTypeItems,
                out totalMemoryInSnapshot);

            // 按PotentialDuplicates过滤（如果需要）
            if (args.PotentialDuplicatesFilter)
            {
                typeIndexToTypeObjectsMap = FilterTypeIndexToTypeObjectsMapForPotentialDuplicatesAndGroup(
                    typeIndexToTypeObjectsMap);
                    
                foreach (var nativeType in nativeUnityObjectBaseTypesToDisambiguateByManagedType)
                {
                    if (disambiguatedTypeIndexToTypeObjectsMap.ContainsKey(nativeType))
                    {
                        disambiguatedTypeIndexToTypeObjectsMap[nativeType] =
                            FilterTypeIndexToTypeObjectsMapForPotentialDuplicatesAndGroup(
                                disambiguatedTypeIndexToTypeObjectsMap[nativeType]);
                    }
                }
                
                nonDisambiguatedTechnicallyManagedTypeItems = FilterTypeIndexToTypeObjectsMapForPotentialDuplicatesAndGroup(
                    nonDisambiguatedTechnicallyManagedTypeItems);
            }

            // 从map构建Unity Objects树
            var unityObjectsTree = new List<TreeNode<UnityObjectsItemData>>(typeIndexToTypeObjectsMap.Count);

            // 添加普通类型的对象
            foreach (var kvp in typeIndexToTypeObjectsMap)
            {
                unityObjectsTree.Add(CreateUnityObjectTypeGroup(snapshot, kvp));
            }

            // 添加需要按Managed Type区分的对象
            foreach (var nativeTypeToManagedTypesKVP in disambiguatedTypeIndexToTypeObjectsMap)
            {
                var nativeUndisambiguatedItems = new KeyValuePair<SourceIndex, DictionaryOrList>(
                    nativeTypeToManagedTypesKVP.Key,
                    nonDisambiguatedTechnicallyManagedTypeItems.ContainsKey(nativeTypeToManagedTypesKVP.Key)
                        ? nonDisambiguatedTechnicallyManagedTypeItems[nativeTypeToManagedTypesKVP.Key] 
                        : null);

                var managedTypesCount = nativeTypeToManagedTypesKVP.Value.Count;
                if (managedTypesCount > 0)
                    unityObjectsTree.Add(CreateManagedUnityObjectTypeGroup(
                        snapshot, nativeTypeToManagedTypesKVP, nativeUndisambiguatedItems));
            }

            return unityObjectsTree;
        }

        /// <summary>
        /// 创建Managed Unity Object Type Group节点
        /// </summary>
        TreeNode<UnityObjectsItemData> CreateManagedUnityObjectTypeGroup(
            CachedSnapshot snapshot,
            KeyValuePair<SourceIndex, Dictionary<SourceIndex, DictionaryOrList>> managedKvp,
            KeyValuePair<SourceIndex, DictionaryOrList> nativeUndisambiguatedItems)
        {
            var children = new List<TreeNode<UnityObjectsItemData>>();
            
            foreach (var kvp2 in managedKvp.Value)
            {
                children.Add(CreateUnityObjectTypeGroup(snapshot, kvp2));
            }

            // 添加未消歧的对象（通常不应该存在，除非是捕获竞态或未捕获Managed Objects）
            if (nativeUndisambiguatedItems.Value != null)
                children.Add(CreateUnityObjectTypeGroup(snapshot, nativeUndisambiguatedItems));

            return CreateGroupNode(
                snapshot.NativeTypes.TypeName[managedKvp.Key.Index], 
                managedKvp.Key, 
                children);
        }

        /// <summary>
        /// 创建Unity Object Type Group节点
        /// </summary>
        TreeNode<UnityObjectsItemData> CreateUnityObjectTypeGroup(
            CachedSnapshot snapshot,
            KeyValuePair<SourceIndex, DictionaryOrList> kvp)
        {
            var typeSource = kvp.Key;
            var dictionaryOrList = kvp.Value;

            // 如果是列表（普通情况）
            if (dictionaryOrList.ListOfObjects != null)
            {
                return CreateGroupNode(
                    typeSource.GetName(snapshot), 
                    typeSource, 
                    dictionaryOrList.ListOfObjects);
            }
            // 如果是字典（按InstanceID消歧的情况）
            else
            {
                var typeGroupList = new List<TreeNode<UnityObjectsItemData>>();
                foreach (var listOfObjectsById in dictionaryOrList.MapOfObjects)
                {
                    typeGroupList.Add(CreateGroupNode(
                        listOfObjectsById.Key, 
                        new SourceIndex(), 
                        listOfObjectsById.Value));
                }
                return CreateGroupNode(
                    typeSource.GetName(snapshot), 
                    typeSource, 
                    typeGroupList);
            }
        }

        /// <summary>
        /// 创建分组节点
        /// </summary>
        TreeNode<UnityObjectsItemData> CreateGroupNode(
            string groupName, 
            SourceIndex sourceIndex, 
            List<TreeNode<UnityObjectsItemData>> items)
        {
            // 计算总大小
            var typeNativeSize = new MemorySize();
            var typeManagedSize = new MemorySize();
            var typeGpuSize = new MemorySize();
            
            foreach (var typeObject in items)
            {
                typeNativeSize += typeObject.Data.NativeSize;
                typeManagedSize += typeObject.Data.ManagedSize;
                typeGpuSize += typeObject.Data.GpuSize;
            }

            // 创建分组节点
            var groupData = new UnityObjectsItemData(
                groupName,
                typeNativeSize,
                typeManagedSize,
                typeGpuSize,
                sourceIndex,
                items.Count);

            var groupNode = new TreeNode<UnityObjectsItemData>(groupData);
            groupNode.Source = sourceIndex; // 设置Source用于Selection

            // 添加子节点
            foreach (var item in items)
            {
                groupNode.AddChild(item);
            }

            return groupNode;
        }

        /// <summary>
        /// Unity Object大小结构（Native + Managed + Graphics）
        /// </summary>
        struct UnityObjectSize
        {
            public MemorySize Native;
            public MemorySize Managed;
            public MemorySize Gfx;

            public static UnityObjectSize operator +(UnityObjectSize l, UnityObjectSize r)
            {
                return new UnityObjectSize() 
                { 
                    Native = l.Native + r.Native, 
                    Managed = l.Managed + r.Managed, 
                    Gfx = l.Gfx + r.Gfx 
                };
            }

            public override string ToString()
            {
                return $"(Native: {Native}, Managed: {Managed}, Graphics: {Gfx})";
            }
        }

        /// <summary>
        /// 累加值到字典
        /// </summary>
        static void AccumulateValue(
            Dictionary<SourceIndex, UnityObjectSize> accumulator, 
            SourceIndex index, 
            MemorySize native, 
            MemorySize managed, 
            MemorySize gpu)
        {
            var sizeValue = new UnityObjectSize() { Native = native, Managed = managed, Gfx = gpu };
            
            if (accumulator.TryGetValue(index, out var storedValue))
                sizeValue += storedValue;

            accumulator[index] = sizeValue;
        }

        /// <summary>
        /// 构建NativeObject索引到大小的映射
        /// </summary>
        Dictionary<SourceIndex, UnityObjectSize> BuildNativeObjectIndexToSize(
            CachedSnapshot snapshot,
            out MemorySize _totalMemoryInSnapshot)
        {
            var nativeObject2Size = new Dictionary<SourceIndex, UnityObjectSize>();

            // 从ProcessedNativeRoots中提取所有native objects及相关数据
            var processedNativeRoots = snapshot.ProcessedNativeRoots;
            for (long i = 0; i < processedNativeRoots.Count; i++)
            {
                var itemIndex = processedNativeRoots.Data[i].NativeObjectOrRootIndex;
                switch (itemIndex.Id)
                {
                    case SourceIndex.SourceId.NativeObject:
                        var size = processedNativeRoots.Data[i].AccumulatedRootSizes;
                        AccumulateValue(nativeObject2Size, itemIndex, 
                            size.NativeSize, size.ManagedSize, size.GfxSize);
                        break;
                    default:
                        break;
                }
            }

            var totalMemoryInSnapshot = processedNativeRoots.TotalMemoryInSnapshot;

            // [Legacy] 如果没有SystemMemoryRegionsInfo，从legacy memory stats获取总值
            // Nb! 如果更改此逻辑，需同步更新AllTrackedMemoryModelBuilder等处的相似代码
            var memoryStats = snapshot.MetaData.TargetMemoryStats;
            if (memoryStats.HasValue && !snapshot.HasSystemMemoryRegionsInfo && 
                (memoryStats.Value.TotalVirtualMemory > 0))
            {
                totalMemoryInSnapshot = new MemorySize(memoryStats.Value.TotalVirtualMemory, 0);
            }

            _totalMemoryInSnapshot = totalMemoryInSnapshot;

            return nativeObject2Size;
        }

        /// <summary>
        /// 构建Type索引到Unity Objects的映射（包括消歧的和未消歧的）
        /// </summary>
        protected void BuildUnityObjectTypeIndexToUnityObjectsMapForSnapshot(
            CachedSnapshot snapshot,
            in UnityObjectsBuildArgs args,
            HashSet<SourceIndex> nativeUnityObjectBaseTypesToDisambiguateByManagedType,
            out Dictionary<SourceIndex, DictionaryOrList> typeIndexToTypeObjectsMap,
            out Dictionary<SourceIndex, Dictionary<SourceIndex, DictionaryOrList>> disambiguatedTypeIndexToTypeObjectsMap,
            out Dictionary<SourceIndex, DictionaryOrList> nonDisambiguatedObjectsOfDisambiguatedNativeTypes,
            out MemorySize totalMemoryInSnapshot)
        {
            // 初始化输出参数
            typeIndexToTypeObjectsMap = new Dictionary<SourceIndex, DictionaryOrList>();
            disambiguatedTypeIndexToTypeObjectsMap = new Dictionary<SourceIndex, Dictionary<SourceIndex, DictionaryOrList>>();
            nonDisambiguatedObjectsOfDisambiguatedNativeTypes = new Dictionary<SourceIndex, DictionaryOrList>();

            // 构建NativeObject到Size的映射
            var nativeObject2Size = BuildNativeObjectIndexToSize(snapshot, out totalMemoryInSnapshot);

            // 按Type分组对象
            var nativeTypes = snapshot.NativeTypes;
            var nativeObjects = snapshot.NativeObjects;

            foreach (var obj in nativeObject2Size)
            {
                // 获取Managed Type信息（如果存在）
                int managedTypeIndex = -1;
                string managedTypeName = null;
                if (nativeObjects.ManagedObjectIndex[obj.Key.Index] >= 0)
                {
                    managedTypeIndex = snapshot.CrawledData.ManagedObjects[
                        nativeObjects.ManagedObjectIndex[obj.Key.Index]].ITypeDescription;
                    
                    // 由于bug PROF-2420，一些Native Objects报告的关联Managed Objects无效
                    // 这里的检查用于缓解此bug
                    if (managedTypeIndex >= 0)
                        managedTypeName = snapshot.TypeDescriptions.TypeDescriptionName[managedTypeIndex];
                }

                // 获取Native Type信息
                var typeIndex = nativeObjects.NativeTypeArrayIndex[obj.Key.Index];
                var nativeObjectName = nativeObjects.ObjectName[obj.Key.Index];

                // 创建Unity Object节点
                var itemName = nativeObjectName;
                var itemData = new UnityObjectsItemData(
                    itemName,
                    obj.Value.Native,
                    obj.Value.Managed,
                    obj.Value.Gfx,
                    obj.Key);

                var item = new TreeNode<UnityObjectsItemData>(itemData);
                item.Source = obj.Key; // 设置Source用于Selection

                // 添加节点到对应type的列表
                var nativeTypeSourceIndex = new SourceIndex(SourceIndex.SourceId.NativeType, typeIndex);
                
                // 如果此类型需要按Managed Type消歧
                if (nativeUnityObjectBaseTypesToDisambiguateByManagedType.Contains(nativeTypeSourceIndex))
                {
                    if (managedTypeIndex >= 0)
                    {
                        // 有Managed Type，添加到消歧map
                        if (!disambiguatedTypeIndexToTypeObjectsMap.ContainsKey(nativeTypeSourceIndex))
                            disambiguatedTypeIndexToTypeObjectsMap[nativeTypeSourceIndex] = 
                                new Dictionary<SourceIndex, DictionaryOrList>();

                        var managedTypeMap = disambiguatedTypeIndexToTypeObjectsMap[nativeTypeSourceIndex];
                        var managedTypeSource = new SourceIndex(SourceIndex.SourceId.ManagedType, managedTypeIndex);
                        AddObjectToTypeMap(managedTypeMap, managedTypeSource, nativeObjectName, item, args);
                    }
                    else
                    {
                        // 没有Managed Type，添加到未消歧map
                        AddObjectToTypeMap(nonDisambiguatedObjectsOfDisambiguatedNativeTypes, 
                            nativeTypeSourceIndex, nativeObjectName, item, args);
                    }
                }
                else
                {
                    // 普通类型，添加到普通map
                    AddObjectToTypeMap(typeIndexToTypeObjectsMap, nativeTypeSourceIndex, 
                        nativeObjectName, item, args);
                }
            }
        }

        /// <summary>
        /// 添加对象到Type Map
        /// </summary>
        void AddObjectToTypeMap(
            Dictionary<SourceIndex, DictionaryOrList> typeIndexToTypeObjectsMap,
            SourceIndex typeIndex,
            string nativeObjectName,
            TreeNode<UnityObjectsItemData> item,
            in UnityObjectsBuildArgs args)
        {
            List<TreeNode<UnityObjectsItemData>> listOfObjects = null;
            
            if (!typeIndexToTypeObjectsMap.ContainsKey(typeIndex))
                typeIndexToTypeObjectsMap[typeIndex] = new DictionaryOrList();

            var typeObjects = typeIndexToTypeObjectsMap[typeIndex];

            // 如果需要按InstanceID消歧
            if (args.DisambiguateByInstanceId)
            {
                typeObjects.MapOfObjects ??= new Dictionary<string, List<TreeNode<UnityObjectsItemData>>>();
                
                if (!typeObjects.MapOfObjects.ContainsKey(nativeObjectName))
                    typeObjects.MapOfObjects[nativeObjectName] = new List<TreeNode<UnityObjectsItemData>>();
                
                listOfObjects = typeObjects.MapOfObjects[nativeObjectName];
                typeObjects.Reason = DictionaryOrList.SplitReason.InstanceIDs;
            }
            else
            {
                typeObjects.ListOfObjects ??= new List<TreeNode<UnityObjectsItemData>>();
                listOfObjects = typeObjects.ListOfObjects;
            }
            
            listOfObjects.Add(item);
        }

        /// <summary>
        /// 过滤map中的潜在重复项（相同type、name和size的对象）
        /// </summary>
        Dictionary<SourceIndex, DictionaryOrList> FilterTypeIndexToTypeObjectsMapForPotentialDuplicatesAndGroup(
            Dictionary<SourceIndex, DictionaryOrList> typeIndexToTypeObjectsMap)
        {
            var filteredTypeIndexToTypeObjectsMap = new Dictionary<SourceIndex, DictionaryOrList>();

            foreach (var typeIndexToTypeObjectsKvp in typeIndexToTypeObjectsMap)
            {
                var potentialDuplicateObjectsMap = new Dictionary<Tuple<string, MemorySize>, DictionaryOrList>();
                
                // 按name & size拆分type objects到不同列表
                System.Diagnostics.Debug.Assert(typeIndexToTypeObjectsKvp.Value.ListOfObjects != null, 
                    "Potential Duplicates filtering can't yet be used together with Instance ID disambiguation");
                    
                var typeObjects = typeIndexToTypeObjectsKvp.Value.ListOfObjects;
                foreach (var typeObject in typeObjects)
                {
                    var data = typeObject.Data;
                    var nameSizeTuple = new Tuple<string, MemorySize>(data.Name, data.TotalSize);
                    
                    if (!potentialDuplicateObjectsMap.ContainsKey(nameSizeTuple))
                        potentialDuplicateObjectsMap[nameSizeTuple] = new DictionaryOrList 
                        { 
                            ListOfObjects = new List<TreeNode<UnityObjectsItemData>>() 
                        };
                    
                    potentialDuplicateObjectsMap[nameSizeTuple].ListOfObjects.Add(typeObject);
                }

                // 为包含多个项（重复项）的列表创建potential duplicate groups
                var potentialDuplicateItems = new List<TreeNode<UnityObjectsItemData>>();
                var typeIndex = typeIndexToTypeObjectsKvp.Key;
                
                foreach (var potentialDuplicateObjectsKvp in potentialDuplicateObjectsMap)
                {
                    var potentialDuplicateObjects = potentialDuplicateObjectsKvp.Value.ListOfObjects;
                    if (potentialDuplicateObjects.Count > 1)
                    {
                        var potentialDuplicateData = potentialDuplicateObjects[0].Data;

                        // 累加所有重复项的大小
                        var potentialDuplicatesNativeSize = new MemorySize();
                        var potentialDuplicatesManagedSize = new MemorySize();
                        var potentialDuplicatesGpuSize = new MemorySize();
                        
                        foreach (var obj in potentialDuplicateObjects)
                        {
                            potentialDuplicatesNativeSize += obj.Data.NativeSize;
                            potentialDuplicatesManagedSize += obj.Data.ManagedSize;
                            potentialDuplicatesGpuSize += obj.Data.GpuSize;
                        }

                        // 创建重复项分组节点
                        var groupData = new UnityObjectsItemData(
                            potentialDuplicateData.Name,
                            potentialDuplicatesNativeSize,
                            potentialDuplicatesManagedSize,
                            potentialDuplicatesGpuSize,
                            potentialDuplicateData.Source,
                            potentialDuplicateObjects.Count);

                        var potentialDuplicateItem = new TreeNode<UnityObjectsItemData>(groupData);
                        potentialDuplicateItem.Source = potentialDuplicateData.Source;

                        // 添加子节点
                        foreach (var child in potentialDuplicateObjects)
                        {
                            potentialDuplicateItem.AddChild(child);
                        }

                        potentialDuplicateItems.Add(potentialDuplicateItem);
                    }
                }

                // 添加包含重复type objects的列表到filtered map
                if (potentialDuplicateItems.Count > 0)
                    filteredTypeIndexToTypeObjectsMap.Add(typeIndex, 
                        new DictionaryOrList() { ListOfObjects = potentialDuplicateItems });
            }

            return filteredTypeIndexToTypeObjectsMap;
        }

        /// <summary>
        /// 辅助类：可以包含List或Dictionary
        /// </summary>
        protected class DictionaryOrList
        {
            public enum SplitReason
            {
                None,
                InstanceIDs,
            }

            public List<TreeNode<UnityObjectsItemData>> ListOfObjects { get; set; }
            public Dictionary<string, List<TreeNode<UnityObjectsItemData>>> MapOfObjects { get; set; }
            public SplitReason Reason { get; set; }
        }
    }

    /// <summary>
    /// UnityObjects Model构建参数
    /// </summary>
    internal readonly struct UnityObjectsBuildArgs
    {
        public UnityObjectsBuildArgs(
            bool flattenHierarchy = false,
            bool potentialDuplicatesFilter = false,
            bool disambiguateByInstanceId = false,
            Action<SourceIndex> selectionProcessor = null)
        {
            FlattenHierarchy = flattenHierarchy;
            PotentialDuplicatesFilter = potentialDuplicatesFilter;
            DisambiguateByInstanceId = disambiguateByInstanceId;
            SelectionProcessor = selectionProcessor;
        }

        /// <summary>
        /// 是否扁平化层次结构为单层（移除所有分组）
        /// </summary>
        public bool FlattenHierarchy { get; }

        /// <summary>
        /// 是否过滤潜在重复项
        /// </summary>
        public bool PotentialDuplicatesFilter { get; }

        /// <summary>
        /// 是否按InstanceID消歧
        /// </summary>
        public bool DisambiguateByInstanceId { get; }

        /// <summary>
        /// 选择处理回调
        /// </summary>
        public Action<SourceIndex> SelectionProcessor { get; }
    }
}

