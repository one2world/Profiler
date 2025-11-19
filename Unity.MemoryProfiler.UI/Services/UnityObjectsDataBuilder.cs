using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// Unity Objects 数据构建器 - 严格基于 Unity 官方逻辑
    /// 参考: UnityObjectsModelBuilder
    /// 
    /// 核心逻辑：
    /// 1. 每个对象都是一个独立的 TreeViewItem 节点
    /// 2. 类型分组是父节点，包含该类型的所有对象作为子节点
    /// 3. 可以有多级嵌套（类型 -> 对象，或 类型 -> Managed类型 -> 对象）
    /// 4. Flatten 时，只显示叶子节点（对象节点）
    /// </summary>
    internal class UnityObjectsDataBuilder
    {
        private readonly CachedSnapshot _snapshot;
        private int _nextId = 1;

        public UnityObjectsDataBuilder(CachedSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <summary>
        /// 构建参数
        /// </summary>
        public class BuildOptions
        {
            /// <summary>
            /// 是否扁平化层次结构（只显示对象节点，不显示类型分组）
            /// </summary>
            public bool FlattenHierarchy { get; set; }

            /// <summary>
            /// 是否只显示潜在重复项
            /// </summary>
            public bool ShowPotentialDuplicatesOnly { get; set; }

            /// <summary>
            /// 是否按 Instance ID 消歧（相同名称的对象按名称分组）
            /// </summary>
            public bool DisambiguateByInstanceId { get; set; }
        }

        public UnityObjectsData Build(BuildOptions? options = null)
        {
            options ??= new BuildOptions();
            var result = new UnityObjectsData();

            // 1. 构建 Native 对象索引到大小的映射
            var nativeObjectToSize = BuildNativeObjectToSizeMap(out var totalMemoryInSnapshot);

            // 2. 获取需要按 Managed 类型消歧的 Native 类型
            var disambiguateTypes = GetDisambiguateNativeTypes();

            // 3. 按类型分组对象
            var (typeIndexToTypeObjectsMap, disambiguatedTypeIndexToTypeObjectsMap, nonDisambiguatedObjects) =
                GroupObjectsByType(nativeObjectToSize, disambiguateTypes, options);

            // 4. 如果启用 Potential Duplicates 过滤
            if (options.ShowPotentialDuplicatesOnly)
            {
                typeIndexToTypeObjectsMap = FilterForPotentialDuplicates(typeIndexToTypeObjectsMap);
                
                var newDisambiguated = new Dictionary<int, Dictionary<int, DictionaryOrList>>();
                foreach (var kvp in disambiguatedTypeIndexToTypeObjectsMap)
                {
                    var filtered = new Dictionary<int, DictionaryOrList>();
                    foreach (var kvp2 in kvp.Value)
                    {
                        var filteredList = FilterForPotentialDuplicates(new Dictionary<int, DictionaryOrList> { { kvp2.Key, kvp2.Value } });
                        if (filteredList.Count > 0)
                            filtered[kvp2.Key] = filteredList.First().Value;
                    }
                    if (filtered.Count > 0)
                        newDisambiguated[kvp.Key] = filtered;
                }
                disambiguatedTypeIndexToTypeObjectsMap = newDisambiguated;
                
                nonDisambiguatedObjects = FilterForPotentialDuplicates(nonDisambiguatedObjects);
            }

            // 5. 构建树形结构
            var rootNodes = new List<UnityObjectTreeNode>();

            // 添加普通类型
            foreach (var kvp in typeIndexToTypeObjectsMap)
            {
                var typeNode = CreateUnityObjectTypeGroup(kvp);
                rootNodes.Add(typeNode);
            }

            // 添加消歧类型（MonoBehaviour/ScriptableObject）
            foreach (var nativeTypeKvp in disambiguatedTypeIndexToTypeObjectsMap)
            {
                var managedTypeNode = CreateManagedUnityObjectTypeGroup(nativeTypeKvp, nonDisambiguatedObjects);
                rootNodes.Add(managedTypeNode);
            }

            // 6. 如果启用扁平化，只保留叶子节点
            if (options.FlattenHierarchy)
            {
                rootNodes = FlattenHierarchy(rootNodes);
            }

            // 7. 排序
            rootNodes = rootNodes.OrderByDescending(n => n.TotalSize).ToList();

            // 8. 计算表格中的总内存（所有根节点的总和）
            ulong totalMemoryInTable = 0;
            foreach (var node in rootNodes)
            {
                totalMemoryInTable += node.TotalSize;
            }

            // 9. 计算百分比（基于表格内存，而不是快照总内存）
            if (totalMemoryInTable > 0)
            {
                CalculatePercentages(rootNodes, totalMemoryInTable);
            }

            result.TotalSnapshotMemory = totalMemoryInSnapshot;
            result.TotalMemoryInTable = totalMemoryInTable;
            result.RootNodes = rootNodes;

            return result;
        }

        /// <summary>
        /// 获取需要按 Managed 类型消歧的 Native 类型
        /// 参考: BuildListOfNativeUnityObjectBaseTypesToDisambiguateByManagedType
        /// </summary>
        private HashSet<int> GetDisambiguateNativeTypes()
        {
            var types = new HashSet<int>();

            if (_snapshot.NativeTypes.MonoBehaviourIdx >= 0)
                types.Add(_snapshot.NativeTypes.MonoBehaviourIdx);

            if (_snapshot.NativeTypes.ScriptableObjectIdx >= 0)
                types.Add(_snapshot.NativeTypes.ScriptableObjectIdx);

            if (_snapshot.NativeTypes.EditorScriptableObjectIdx >= 0)
                types.Add(_snapshot.NativeTypes.EditorScriptableObjectIdx);

            return types;
        }

        /// <summary>
        /// 构建 Native 对象到大小的映射
        /// 参考: BuildNativeObjectIndexToSize
        /// </summary>
        private Dictionary<long, ObjectSize> BuildNativeObjectToSizeMap(out ulong totalMemoryInSnapshot)
        {
            var nativeObjectToSize = new Dictionary<long, ObjectSize>();

            var processedNativeRoots = _snapshot.ProcessedNativeRoots;
            for (long i = 0; i < processedNativeRoots.Count; i++)
            {
                ref readonly var data = ref processedNativeRoots.Data[i];
                
                if (data.NativeObjectOrRootIndex.Id == CachedSnapshot.SourceIndex.SourceId.NativeObject)
                {
                    var objectIndex = data.NativeObjectOrRootIndex.Index;
                    var sizes = data.AccumulatedRootSizes;

                    nativeObjectToSize[objectIndex] = new ObjectSize
                    {
                        NativeSize = sizes.NativeSize.Committed,
                        ManagedSize = sizes.ManagedSize.Committed,
                        GpuSize = sizes.GfxSize.Committed
                    };
                }
            }

            totalMemoryInSnapshot = (ulong)processedNativeRoots.TotalMemoryInSnapshot.Committed;

            return nativeObjectToSize;
        }

        /// <summary>
        /// 按类型分组对象
        /// 参考: BuildUnityObjectTypeIndexToUnityObjectsMapForSnapshot
        /// </summary>
        private (Dictionary<int, DictionaryOrList>, Dictionary<int, Dictionary<int, DictionaryOrList>>, Dictionary<int, DictionaryOrList>)
            GroupObjectsByType(
                Dictionary<long, ObjectSize> nativeObjectToSize,
                HashSet<int> disambiguateTypes,
                BuildOptions options)
        {
            var nativeObjects = _snapshot.NativeObjects;
            var nativeTypes = _snapshot.NativeTypes;

            // 普通类型的对象映射
            var typeIndexToTypeObjectsMap = new Dictionary<int, DictionaryOrList>();
            
            // 需要消歧的类型的对象映射: NativeTypeIndex -> ManagedTypeIndex -> Objects
            var disambiguatedTypeIndexToTypeObjectsMap = new Dictionary<int, Dictionary<int, DictionaryOrList>>();
            
            // 需要消歧但没有 Managed 类型的对象
            var nonDisambiguatedObjects = new Dictionary<int, DictionaryOrList>();

            foreach (var kvp in nativeObjectToSize)
            {
                var objectIndex = kvp.Key;
                var size = kvp.Value;

                // 获取类型索引
                var nativeTypeIndex = nativeObjects.NativeTypeArrayIndex[objectIndex];
                if (nativeTypeIndex < 0 || nativeTypeIndex >= nativeTypes.TypeName.Length)
                    continue;

                // 获取 Managed 类型索引
                int managedTypeIndex = -1;
                if (nativeObjects.ManagedObjectIndex[objectIndex] >= 0)
                {
                    var managedObjectIndex = nativeObjects.ManagedObjectIndex[objectIndex];
                    if (managedObjectIndex < _snapshot.CrawledData.ManagedObjects.Count)
                    {
                        managedTypeIndex = _snapshot.CrawledData.ManagedObjects[managedObjectIndex].ITypeDescription;
                    }
                }

                // 创建对象节点
                var objectName = nativeObjects.ObjectName[objectIndex];
                // 如果名称为空，使用 "<No Name>"
                if (string.IsNullOrEmpty(objectName))
                    objectName = "<No Name>";
                    
                var instanceId = nativeObjects.InstanceId[objectIndex];
                var objectNode = new UnityObjectTreeNode
                {
                    Id = _nextId++,
                    Name = objectName,
                    NativeSize = size.NativeSize,
                    ManagedSize = size.ManagedSize,
                    GpuSize = size.GpuSize,
                    ObjectIndex = objectIndex,
                    InstanceId = (int)(ulong)instanceId,
                    NativeTypeIndex = nativeTypeIndex,
                    ManagedTypeIndex = managedTypeIndex,
                    Source = new CachedSnapshot.SourceIndex(CachedSnapshot.SourceIndex.SourceId.NativeObject, objectIndex)
                };

                // 判断是否需要按 Managed 类型消歧
                if (disambiguateTypes.Contains(nativeTypeIndex))
                {
                    if (managedTypeIndex >= 0)
                    {
                        // 有 Managed 类型，按 Managed 类型分组
                        if (!disambiguatedTypeIndexToTypeObjectsMap.ContainsKey(nativeTypeIndex))
                            disambiguatedTypeIndexToTypeObjectsMap[nativeTypeIndex] = new Dictionary<int, DictionaryOrList>();

                        AddObjectToTypeMap(
                            disambiguatedTypeIndexToTypeObjectsMap[nativeTypeIndex],
                            managedTypeIndex,
                            objectName,
                            objectNode,
                            options);
                    }
                    else
                    {
                        // 没有 Managed 类型
                        AddObjectToTypeMap(nonDisambiguatedObjects, nativeTypeIndex, objectName, objectNode, options);
                    }
                }
                else
                {
                    // 普通类型
                    AddObjectToTypeMap(typeIndexToTypeObjectsMap, nativeTypeIndex, objectName, objectNode, options);
                }
            }

            return (typeIndexToTypeObjectsMap, disambiguatedTypeIndexToTypeObjectsMap, nonDisambiguatedObjects);
        }

        /// <summary>
        /// 添加对象到类型映射
        /// 参考: AddObjectToTypeMap
        /// </summary>
        private void AddObjectToTypeMap(
            Dictionary<int, DictionaryOrList> typeMap,
            int typeIndex,
            string objectName,
            UnityObjectTreeNode objectNode,
            BuildOptions options)
        {
            if (!typeMap.ContainsKey(typeIndex))
                typeMap[typeIndex] = new DictionaryOrList();

            var typeObjects = typeMap[typeIndex];

            if (options.DisambiguateByInstanceId)
            {
                // 按对象名称分组
                typeObjects.MapOfObjects ??= new Dictionary<string, List<UnityObjectTreeNode>>();
                if (!typeObjects.MapOfObjects.ContainsKey(objectName))
                    typeObjects.MapOfObjects[objectName] = new List<UnityObjectTreeNode>();
                typeObjects.MapOfObjects[objectName].Add(objectNode);
            }
            else
            {
                // 直接添加到列表
                typeObjects.ListOfObjects ??= new List<UnityObjectTreeNode>();
                typeObjects.ListOfObjects.Add(objectNode);
            }
        }

        /// <summary>
        /// 创建类型分组节点
        /// 参考: CreateUnityObjectTypeGroup
        /// </summary>
        private UnityObjectTreeNode CreateUnityObjectTypeGroup(KeyValuePair<int, DictionaryOrList> kvp)
        {
            var typeIndex = kvp.Key;
            var dictionaryOrList = kvp.Value;
            var typeName = _snapshot.NativeTypes.TypeName[typeIndex];

            if (dictionaryOrList.ListOfObjects != null)
            {
                // 直接列表
                return CreateGroupNode(typeName, typeIndex, -1, dictionaryOrList.ListOfObjects);
            }
            else
            {
                // 按名称分组
                var childGroups = new List<UnityObjectTreeNode>();
                foreach (var kvp2 in dictionaryOrList.MapOfObjects!)
                {
                    var groupNode = CreateGroupNode(kvp2.Key, -1, -1, kvp2.Value);
                    childGroups.Add(groupNode);
                }
                return CreateGroupNode(typeName, typeIndex, -1, childGroups);
            }
        }

        /// <summary>
        /// 创建 Managed 类型分组节点（MonoBehaviour/ScriptableObject）
        /// 参考: CreateManagedUnityObjectTypeGroup
        /// </summary>
        private UnityObjectTreeNode CreateManagedUnityObjectTypeGroup(
            KeyValuePair<int, Dictionary<int, DictionaryOrList>> managedKvp,
            Dictionary<int, DictionaryOrList> nonDisambiguatedObjects)
        {
            var nativeTypeIndex = managedKvp.Key;
            var managedTypeMap = managedKvp.Value;
            var nativeTypeName = _snapshot.NativeTypes.TypeName[nativeTypeIndex];

            var children = new List<UnityObjectTreeNode>();

            // 添加子节点（按 Managed 类型）
            foreach (var kvp in managedTypeMap)
            {
                var managedTypeIndex = kvp.Key;
                var dictionaryOrList = kvp.Value;
                var managedTypeName = _snapshot.TypeDescriptions.TypeDescriptionName[managedTypeIndex];

                UnityObjectTreeNode childNode;
                if (dictionaryOrList.ListOfObjects != null)
                {
                    childNode = CreateGroupNode(managedTypeName, nativeTypeIndex, managedTypeIndex, dictionaryOrList.ListOfObjects);
                }
                else
                {
                    var grandChildren = new List<UnityObjectTreeNode>();
                    foreach (var kvp2 in dictionaryOrList.MapOfObjects!)
                    {
                        var groupNode = CreateGroupNode(kvp2.Key, -1, -1, kvp2.Value);
                        grandChildren.Add(groupNode);
                    }
                    childNode = CreateGroupNode(managedTypeName, nativeTypeIndex, managedTypeIndex, grandChildren);
                }
                children.Add(childNode);
            }

            // 添加没有 Managed 类型的对象
            if (nonDisambiguatedObjects.TryGetValue(nativeTypeIndex, out var nonDisambiguated))
            {
                UnityObjectTreeNode childNode;
                if (nonDisambiguated.ListOfObjects != null)
                {
                    childNode = CreateGroupNode(nativeTypeName, nativeTypeIndex, -1, nonDisambiguated.ListOfObjects);
                }
                else
                {
                    var grandChildren = new List<UnityObjectTreeNode>();
                    foreach (var kvp2 in nonDisambiguated.MapOfObjects!)
                    {
                        var groupNode = CreateGroupNode(kvp2.Key, -1, -1, kvp2.Value);
                        grandChildren.Add(groupNode);
                    }
                    childNode = CreateGroupNode(nativeTypeName, nativeTypeIndex, -1, grandChildren);
                }
                children.Add(childNode);
            }

            // 创建父节点
            return CreateGroupNode(nativeTypeName, nativeTypeIndex, -1, children);
        }

        /// <summary>
        /// 创建分组节点
        /// 参考: CreateGroupNode
        /// </summary>
        private UnityObjectTreeNode CreateGroupNode(
            string groupName,
            int nativeTypeIndex,
            int managedTypeIndex,
            List<UnityObjectTreeNode> items)
        {
            ulong totalNativeSize = 0;
            ulong totalManagedSize = 0;
            ulong totalGpuSize = 0;

            foreach (var item in items)
            {
                totalNativeSize += item.NativeSize;
                totalManagedSize += item.ManagedSize;
                totalGpuSize += item.GpuSize;
            }

            return new UnityObjectTreeNode
            {
                Id = _nextId++,
                Name = groupName,
                NativeSize = totalNativeSize,
                ManagedSize = totalManagedSize,
                GpuSize = totalGpuSize,
                ChildCount = items.Count,
                Children = items.OrderByDescending(n => n.TotalSize).ToList(),
                NativeTypeIndex = nativeTypeIndex,
                ManagedTypeIndex = managedTypeIndex
            };
        }

        /// <summary>
        /// 过滤潜在重复项
        /// 参考: FilterTypeIndexToTypeObjectsMapForPotentialDuplicatesAndGroup
        /// </summary>
        private Dictionary<int, DictionaryOrList> FilterForPotentialDuplicates(Dictionary<int, DictionaryOrList> typeMap)
        {
            var filtered = new Dictionary<int, DictionaryOrList>();

            foreach (var kvp in typeMap)
            {
                var typeIndex = kvp.Key;
                var dictionaryOrList = kvp.Value;

                if (dictionaryOrList.ListOfObjects == null)
                    continue; // 暂不支持 MapOfObjects

                var potentialDuplicatesMap = new Dictionary<(string, ulong), List<UnityObjectTreeNode>>();

                // 按名称和大小分组
                foreach (var obj in dictionaryOrList.ListOfObjects)
                {
                    var key = (obj.Name, obj.TotalSize);
                    if (!potentialDuplicatesMap.ContainsKey(key))
                        potentialDuplicatesMap[key] = new List<UnityObjectTreeNode>();
                    potentialDuplicatesMap[key].Add(obj);
                }

                // 只保留有重复的
                var duplicateGroups = new List<UnityObjectTreeNode>();
                foreach (var kvp2 in potentialDuplicatesMap)
                {
                    if (kvp2.Value.Count > 1)
                    {
                        var groupNode = CreateGroupNode(
                            kvp2.Value[0].Name,
                            -1,
                            -1,
                            kvp2.Value);
                        duplicateGroups.Add(groupNode);
                    }
                }

                if (duplicateGroups.Count > 0)
                {
                    filtered[typeIndex] = new DictionaryOrList
                    {
                        ListOfObjects = duplicateGroups
                    };
                }
            }

            return filtered;
        }

        /// <summary>
        /// 扁平化层次结构，只保留叶子节点
        /// 参考: TreeModelUtility.RetrieveLeafNodesOfTree
        /// </summary>
        private List<UnityObjectTreeNode> FlattenHierarchy(List<UnityObjectTreeNode> nodes)
        {
            var flatList = new List<UnityObjectTreeNode>();

            foreach (var node in nodes)
            {
                if (node.Children != null && node.Children.Count > 0)
                {
                    // 递归展平子节点
                    flatList.AddRange(FlattenHierarchy(node.Children));
                }
                else
                {
                    // 叶子节点
                    flatList.Add(node);
                }
            }

            return flatList;
        }

        /// <summary>
        /// 计算百分比
        /// </summary>
        private void CalculatePercentages(List<UnityObjectTreeNode> nodes, ulong totalMemory)
        {
            foreach (var node in nodes)
            {
                node.Percentage = (double)node.TotalSize / totalMemory;

                if (node.Children != null)
                {
                    CalculatePercentages(node.Children, totalMemory);
                }
            }
        }

        /// <summary>
        /// 对象大小结构
        /// </summary>
        private struct ObjectSize
        {
            public ulong NativeSize;
            public ulong ManagedSize;
            public ulong GpuSize;
        }

        /// <summary>
        /// DictionaryOrList 结构（用于支持两种分组方式）
        /// 参考: UnityObjectsModelBuilder.DictionaryOrList
        /// </summary>
        private class DictionaryOrList
        {
            public Dictionary<string, List<UnityObjectTreeNode>>? MapOfObjects;
            public List<UnityObjectTreeNode>? ListOfObjects;
        }
    }
}
