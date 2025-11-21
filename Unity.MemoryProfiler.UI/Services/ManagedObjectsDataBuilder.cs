using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Managed;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// Managed Objects 数据构建器
    /// 按调用栈聚合 Managed 对象的内存分配，构建层级树形结构
    /// </summary>
    internal class ManagedObjectsDataBuilder
    {
        private readonly CachedSnapshot _snapshot;

        internal ManagedObjectsDataBuilder(CachedSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <summary>
        /// 构建 Managed Objects 数据（按调用栈层级构建树形结构）
        /// </summary>
        public ManagedObjectsData Build()
        {
            var data = new ManagedObjectsData();
            
            // 第一步：收集所有对象及其调用栈
            var callStackInfos = new List<CallStackAllocationInfo>();
            var managedObjectCount = _snapshot.CrawledData.ManagedObjects.Count;
            
            for (int i = 0; i < managedObjectCount; i++)
            {
                var managedObject = _snapshot.CrawledData.ManagedObjects[i];
                var address = managedObject.PtrObject;
                var size = (ulong)managedObject.Size;

                data.TotalSize += size;
                data.TotalCount++;

                // 获取该对象的堆栈Hash
                var stackHash = _snapshot.ManagedAllocations?.GetStackHashForAddress(address);
                
                if (stackHash.HasValue && stackHash.Value != 0)
                {
                    var callStack = _snapshot.ManagedAllocations?.StackHashToCallStack.GetValueOrDefault(stackHash.Value);
                    if (callStack != null && callStack.Frames.Count > 0)
                    {
                        callStackInfos.Add(new CallStackAllocationInfo
                        {
                            CallStack = callStack,
                            Address = address,
                            Size = size
                        });
                    }
                }
            }

            // 第二步：按调用栈层级构建树形结构
            var rootMap = new Dictionary<string, CallStackTreeNode>();
            int nodeId = 0;

            foreach (var info in callStackInfos)
            {
                CallStackTreeNode? currentParent = null;
                Dictionary<string, CallStackTreeNode> currentLevel = rootMap;

                // 从调用栈底部（最外层）到顶部（分配点）遍历
                for (int depth = info.CallStack.Frames.Count - 1; depth >= 0; depth--)
                {
                    var frame = info.CallStack.Frames[depth];
                    var key = $"{frame.Module}!{frame.Function}";

                    if (!currentLevel.ContainsKey(key))
                    {
                        var newNode = new CallStackTreeNode
                        {
                            FunctionName = frame.Function,
                            Module = frame.Module,
                            FullKey = key,
                            Depth = info.CallStack.Frames.Count - 1 - depth
                        };
                        currentLevel[key] = newNode;
                    }

                    var node = currentLevel[key];
                    node.TotalSize += info.Size;
                    node.Count++;
                    node.ObjectAddresses.Add(info.Address);

                    // 添加文件位置信息（如果有）
                    if (!string.IsNullOrEmpty(frame.FilePath))
                    {
                        // 查找是否已存在相同的文件位置
                        var existingSite = node.AllocationSitesMap
                            .FirstOrDefault(kvp => kvp.Key == $"{frame.FilePath}:{frame.LineNumber}").Value;
                        
                        if (existingSite != null)
                        {
                            // 合并：累加 Size
                            existingSite.Size += info.Size;
                        }
                        else
                        {
                            // 新增
                            var site = new AllocationSite
                            {
                                Description = frame.Function,
                                FilePath = frame.FilePath,
                                LineNumber = frame.LineNumber,
                                Size = info.Size
                            };
                            node.AllocationSitesMap[$"{frame.FilePath}:{frame.LineNumber}"] = site;
                        }
                    }

                    // 准备下一层
                    currentParent = node;
                    currentLevel = node.Children;
                }
            }

            // 第三步：转换为 ManagedCallStackNode 树形结构
            data.RootNodes = ConvertToManagedCallStackNodes(rootMap.Values.OrderByDescending(n => n.TotalSize), ref nodeId, data.TotalSize);

            return data;
        }

        /// <summary>
        /// 构建 Managed Objects 数据（Reversed 模式：从分配点到调用栈底部）
        /// </summary>
        public ManagedObjectsData BuildReversed()
        {
            var data = new ManagedObjectsData();
            
            // 第一步：收集所有对象及其调用栈
            var callStackInfos = new List<CallStackAllocationInfo>();
            var managedObjectCount = _snapshot.CrawledData.ManagedObjects.Count;
            
            for (int i = 0; i < managedObjectCount; i++)
            {
                var managedObject = _snapshot.CrawledData.ManagedObjects[i];
                var address = managedObject.PtrObject;
                var size = (ulong)managedObject.Size;

                data.TotalSize += size;
                data.TotalCount++;

                // 获取该对象的堆栈Hash
                var stackHash = _snapshot.ManagedAllocations?.GetStackHashForAddress(address);
                
                if (stackHash.HasValue && stackHash.Value != 0)
                {
                    var callStack = _snapshot.ManagedAllocations?.StackHashToCallStack.GetValueOrDefault(stackHash.Value);
                    if (callStack != null && callStack.Frames.Count > 0)
                    {
                        callStackInfos.Add(new CallStackAllocationInfo
                        {
                            CallStack = callStack,
                            Address = address,
                            Size = size
                        });
                    }
                }
            }

            // 第二步：按调用栈层级构建树形结构（Reversed：从分配点到底部）
            var rootMap = new Dictionary<string, CallStackTreeNode>();
            int nodeId = 0;

            foreach (var info in callStackInfos)
            {
                CallStackTreeNode? currentParent = null;
                Dictionary<string, CallStackTreeNode> currentLevel = rootMap;

                // 从调用栈顶部（分配点）到底部（最外层）遍历 - REVERSED
                for (int depth = 0; depth < info.CallStack.Frames.Count; depth++)
                {
                    var frame = info.CallStack.Frames[depth];
                    var key = $"{frame.Module}!{frame.Function}";

                    if (!currentLevel.ContainsKey(key))
                    {
                        var newNode = new CallStackTreeNode
                        {
                            FunctionName = frame.Function,
                            Module = frame.Module,
                            FullKey = key,
                            Depth = depth
                        };
                        currentLevel[key] = newNode;
                    }

                    var node = currentLevel[key];
                    node.TotalSize += info.Size;
                    node.Count++;
                    node.ObjectAddresses.Add(info.Address);

                    // 添加文件位置信息（如果有）
                    if (!string.IsNullOrEmpty(frame.FilePath))
                    {
                        // 查找是否已存在相同的文件位置
                        var existingSite = node.AllocationSitesMap
                            .FirstOrDefault(kvp => kvp.Key == $"{frame.FilePath}:{frame.LineNumber}").Value;
                        
                        if (existingSite != null)
                        {
                            // 合并：累加 Size
                            existingSite.Size += info.Size;
                        }
                        else
                        {
                            // 新增
                            var site = new AllocationSite
                            {
                                Description = frame.Function,
                                FilePath = frame.FilePath,
                                LineNumber = frame.LineNumber,
                                Size = info.Size
                            };
                            node.AllocationSitesMap[$"{frame.FilePath}:{frame.LineNumber}"] = site;
                        }
                    }

                    // 准备下一层
                    currentParent = node;
                    currentLevel = node.Children;
                }
            }

            // 第三步：转换为 ManagedCallStackNode 树形结构
            data.RootNodes = ConvertToManagedCallStackNodes(rootMap.Values.OrderByDescending(n => n.TotalSize), ref nodeId, data.TotalSize);

            return data;
        }

        /// <summary>
        /// 递归转换内部树节点为 ManagedCallStackNode
        /// </summary>
        private List<ManagedCallStackNode> ConvertToManagedCallStackNodes(
            IEnumerable<CallStackTreeNode> treeNodes, 
            ref int nodeId, 
            ulong totalSize)
        {
            var result = new List<ManagedCallStackNode>();

            foreach (var treeNode in treeNodes)
            {
                var node = new ManagedCallStackNode
                {
                    Id = nodeId++,
                    Description = treeNode.FunctionName,
                    Module = treeNode.Module,
                    Size = treeNode.TotalSize,
                    Count = treeNode.Count,
                    Percentage = totalSize > 0 ? (treeNode.TotalSize * 100.0 / totalSize) : 0,
                    ObjectAddresses = treeNode.ObjectAddresses,
                    AllocationSites = treeNode.AllocationSitesMap.Values
                        .OrderByDescending(s => s.Size)
                        .ToList()
                };

                // 递归转换子节点
                if (treeNode.Children.Count > 0)
                {
                    node.Children = ConvertToManagedCallStackNodes(
                        treeNode.Children.Values.OrderByDescending(n => n.TotalSize), 
                        ref nodeId, 
                        totalSize);
                }

                result.Add(node);
            }

            return result;
        }

        /// <summary>
        /// 构建选中调用栈的详情数据（按类型分组的对象列表）
        /// </summary>
        public List<ManagedObjectDetailNode> BuildDetailForCallStack(ManagedCallStackNode callStackNode)
        {
            if (callStackNode == null || callStackNode.ObjectAddresses == null || callStackNode.ObjectAddresses.Count == 0)
                return new List<ManagedObjectDetailNode>();

            // 按类型分组
            var typeGroups = new Dictionary<int, TypeGroupInfo>();

            foreach (var address in callStackNode.ObjectAddresses)
            {
                // 查找该地址对应的 Managed 对象
                var managedObjectIndex = FindManagedObjectIndexByAddress(address);
                if (managedObjectIndex < 0)
                    continue;

                var managedObject = _snapshot.CrawledData.ManagedObjects[managedObjectIndex];
                var typeIndex = managedObject.ITypeDescription;
                var size = (ulong)managedObject.Size;

                if (!typeGroups.ContainsKey(typeIndex))
                {
                    typeGroups[typeIndex] = new TypeGroupInfo
                    {
                        TypeIndex = typeIndex,
                        TypeName = _snapshot.TypeDescriptions.TypeDescriptionName[typeIndex]
                    };
                }

                var group = typeGroups[typeIndex];
                group.TotalSize += size;
                group.Count++;
                group.Objects.Add(new ObjectInfo
                {
                    Address = address,
                    Size = size,
                    ManagedObjectIndex = managedObjectIndex
                });
            }

            // 构建树形节点
            var result = new List<ManagedObjectDetailNode>();
            int nodeId = 0;

            foreach (var kvp in typeGroups.OrderByDescending(x => x.Value.TotalSize))
            {
                var typeIndex = kvp.Key;
                var group = kvp.Value;

                var groupNode = new ManagedObjectDetailNode
                {
                    Id = nodeId++,
                    Name = group.TypeName,
                    Size = group.TotalSize,
                    Count = group.Count,
                    TypeIndex = typeIndex,
                    IsGroup = true,
                    Children = new List<ManagedObjectDetailNode>()
                };

                // 添加子对象
                foreach (var obj in group.Objects.OrderByDescending(x => x.Size))
                {
                    var objNode = new ManagedObjectDetailNode
                    {
                        Id = nodeId++,
                        Name = $"0x{obj.Address:X}",
                        Size = obj.Size,
                        Count = 1,
                        Address = obj.Address,
                        TypeIndex = typeIndex,
                        ManagedObjectIndex = obj.ManagedObjectIndex,
                        IsGroup = false
                    };
                    groupNode.Children.Add(objNode);
                }

                result.Add(groupNode);
            }

            return result;
        }

        /// <summary>
        /// 根据地址查找 Managed 对象索引
        /// </summary>
        private int FindManagedObjectIndexByAddress(ulong address)
        {
            var managedObjectCount = _snapshot.CrawledData.ManagedObjects.Count;
            for (int i = 0; i < managedObjectCount; i++)
            {
                if (_snapshot.CrawledData.ManagedObjects[i].PtrObject == address)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 调用栈分配信息
        /// </summary>
        private class CallStackAllocationInfo
        {
            public CallStack CallStack { get; set; } = null!;
            public ulong Address { get; set; }
            public ulong Size { get; set; }
        }

        /// <summary>
        /// 调用栈树节点（内部使用）
        /// </summary>
        private class CallStackTreeNode
        {
            public string FunctionName { get; set; } = string.Empty;
            public string Module { get; set; } = string.Empty;
            public string FullKey { get; set; } = string.Empty;
            public int Depth { get; set; }
            public ulong TotalSize { get; set; }
            public int Count { get; set; }
            public List<ulong> ObjectAddresses { get; set; } = new();
            public Dictionary<string, AllocationSite> AllocationSitesMap { get; set; } = new();
            public Dictionary<string, CallStackTreeNode> Children { get; set; } = new();
        }

        /// <summary>
        /// 类型分组信息
        /// </summary>
        private class TypeGroupInfo
        {
            public int TypeIndex { get; set; }
            public string TypeName { get; set; } = string.Empty;
            public ulong TotalSize { get; set; }
            public int Count { get; set; }
            public List<ObjectInfo> Objects { get; set; } = new();
        }

        /// <summary>
        /// 对象信息
        /// </summary>
        private class ObjectInfo
        {
            public ulong Address { get; set; }
            public ulong Size { get; set; }
            public int ManagedObjectIndex { get; set; }
        }
    }
}
