using System;
using System.Collections.Generic;
using Unity.MemoryProfiler.Editor;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// PathsToRoot 构建器 - 使用BFS构建对象引用路径树
    /// 完全匹配Unity官方实现：PathsToRootDetailView.RawDataSearch
    /// </summary>
    internal class PathsToRootBuilder
    {
        private readonly CachedSnapshot _snapshot;
        private readonly int _maxDepth;
        private readonly int _maxProcessedNodes;
        private int _nextNodeId = 0; // 用于生成唯一节点ID

        public PathsToRootBuilder(CachedSnapshot snapshot, int maxDepth = 10, int maxProcessedNodes = 1000)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _maxDepth = maxDepth;
            _maxProcessedNodes = maxProcessedNodes;
        }

        /// <summary>
        /// 构建 Referenced By 路径树 (谁引用了目标对象)
        /// 参考: PathsToRootDetailView.cs:485-509 RawDataSearch方法
        /// </summary>
        public List<ReferencePathNode> BuildReferencedBy(SourceIndex targetIndex)
        {
            var paths = new List<ReferencePathNode>();
            int objectsProcessed = 0;

            // 1. 获取第一层：所有直接引用targetIndex的对象
            var firstLevelReferences = new List<ObjectData>();
            var foundIndices = new HashSet<SourceIndex>();

            ObjectConnection.GetAllReferencingObjects(
                _snapshot,
                targetIndex,
                ref firstLevelReferences,
                foundIndices);

            if (firstLevelReferences.Count == 0)
            {
                return paths;
            }

            // 2. 为第一层的每个引用创建节点并加入paths
            // 参考: PathsToRootDetailView.cs:467-470
            var processingQueue = new Queue<(ReferencePathNode node, int depth)>();

            foreach (var objectData in firstLevelReferences)
            {
                if (objectData.IsUnknownDataType())
                    continue;

                var displayObject = objectData.displayObject;
                var node = CreateNode(displayObject, objectData, 0, null); // 第一层节点没有parent
                paths.Add(node);

                // 将第一层节点加入处理队列
                // 参考: PathsToRootDetailView.cs:472-476
                processingQueue.Enqueue((node, 0));
            }

            // 3. BFS遍历：处理队列中的每个节点
            // 参考: PathsToRootDetailView.cs:485-509
            var referencingObjects = new List<ObjectData>();
            var referenceSearchAccelerator = new HashSet<SourceIndex>();

            while (processingQueue.Count > 0 && objectsProcessed < _maxProcessedNodes)
            {
                var (current, currentDepth) = processingQueue.Dequeue();
                objectsProcessed++;

                // 深度限制检查
                if (currentDepth >= _maxDepth - 1)
                    continue;

                // 获取引用当前节点的所有对象
                // 参考: PathsToRootDetailView.cs:492
                referencingObjects.Clear();
                referenceSearchAccelerator.Clear();

                ObjectConnection.GetAllReferencingObjects(
                    _snapshot,
                    current.SourceIndex,
                    ref referencingObjects,
                    referenceSearchAccelerator);

                if (referencingObjects.Count == 0)
                    continue;

                // 为每个引用者创建子节点
                // 参考: PathsToRootDetailView.cs:495-507
                foreach (var connection in referencingObjects)
                {
                    if (connection.IsUnknownDataType())
                        continue;

                    var displayObject = connection.displayObject;
                    var child = CreateNode(displayObject, connection, currentDepth + 1, current); // 传入current作为parent
                    current.Children.Add(child);

                    // 检测循环引用：child是否在current的祖先路径中
                    // 参考: PathsToRootDetailTreeViewItem.CircularReferenceCheck
                    bool hasCircularReference = CheckCircularReference(current, child);

                    // 只有非循环引用的节点才继续处理
                    // 参考: PathsToRootDetailView.cs:502-505
                    if (!hasCircularReference)
                    {
                        processingQueue.Enqueue((child, currentDepth + 1));
                    }
                }
            }

            return paths;
        }

        /// <summary>
        /// 检测循环引用：检查child是否在potentialParent的祖先路径中
        /// 完全参考: PathsToRootDetailTreeViewItem.CircularReferenceCheck (Line 187-204)
        /// </summary>
        private bool CheckCircularReference(ReferencePathNode potentialParent, ReferencePathNode child)
        {
            // 从potentialParent.Parent开始向上遍历祖先链
            // 参考Unity实现: var current = potentialParent.parent as PathsToRootDetailTreeViewItem;
            var current = potentialParent.Parent;

            while (current != null)
            {
                // 比较SourceIndex是否相等 (Unity用Data.Equals比较)
                if (current.SourceIndex.Equals(child.SourceIndex))
                {
                    // 设置循环引用ID为祖先节点的ID
                    child.CircularRefId = current.Id;
                    return true;
                }

                current = current.Parent;
            }

            // 未发现循环引用
            child.CircularRefId = -1;
            return false;
        }

        /// <summary>
        /// 创建引用路径节点
        /// </summary>
        private ReferencePathNode CreateNode(ObjectData displayObject, ObjectData connectionData, int depth, ReferencePathNode? parent)
        {
            var refIndex = displayObject.GetSourceLink(_snapshot);

            return new ReferencePathNode
            {
                Id = _nextNodeId++, // 生成唯一ID
                SourceIndex = refIndex,
                DisplayObjectData = displayObject, // 保存原始displayObject
                ConnectionData = connectionData, // 保存connection数据
                Name = GetObjectName(displayObject),
                Type = GetObjectType(displayObject),
                FieldName = GetFieldName(connectionData),
                Size = GetObjectSize(displayObject),
                IsGCRoot = IsGCRoot(refIndex, displayObject),
                Depth = depth,
                Parent = parent, // 设置父节点引用
                Children = new List<ReferencePathNode>()
            };
        }

        /// <summary>
        /// 完整的 GC Root 判断
        /// 参考 Unity.MemoryProfiler.Editor.UI.PathsToRoot.PathsToRootDetailTreeViewItem.IsRoot()
        /// 以及相关的根对象检测逻辑
        /// </summary>
        private bool IsGCRoot(SourceIndex index, ObjectData objectData)
        {
            try
            {
                // 1. 检查是否是场景根对象（Scene Root GameObject）
                if (objectData.IsRootGameObject(_snapshot))
                    return true;

                // 2. 检查是否是场景根Transform
                if (objectData.IsRootTransform(_snapshot))
                    return true;

                // 3. 检查是否有有效的 Native Root Reference ID
                // Native objects 和 allocations 都会有 RootReferenceId
                if (index.Id == SourceIndex.SourceId.NativeObject)
                {
                    if (index.Index >= 0 && index.Index < _snapshot.NativeObjects.Count)
                    {
                        var rootRefId = _snapshot.NativeObjects.RootReferenceId[index.Index];
                        // 有效的 RootReferenceId >= 0 表示这是一个根引用
                        if (rootRefId > 0)
                            return true;
                    }
                }

                // 4. 检查是否是 GCHandle 的目标对象
                // GCHandles 持有的对象是 GC Roots
                if (index.Id == SourceIndex.SourceId.ManagedObject)
                {
                    var managedPtr = objectData.hostManagedObjectPtr;
                    if (managedPtr != 0 && _snapshot.CrawledData.ManagedObjects.Count > 0)
                    {
                        // 检查这个地址是否在 GCHandles.Target 数组中
                        for (long i = 0; i < _snapshot.GcHandles.Count; i++)
                        {
                            if (_snapshot.GcHandles.Target[i] == managedPtr)
                                return true;
                        }
                    }
                }

                // 5. 检查是否是 Type 对象（静态类型引用）
                // Type 对象本身是 GC Roots
                if (objectData.dataType == ObjectDataType.Type)
                    return true;

                // 6. 如果没有任何引用者，也认为是 GC Root
                // （这通常表示它是某种系统根或者数据不完整）
                var referencers = new List<ObjectData>();
                var foundIndices = new HashSet<SourceIndex>();
                ObjectConnection.GetAllReferencingObjects(_snapshot, index, ref referencers, foundIndices);

                if (referencers.Count == 0)
                    return true;

                return false;
            }
            catch
            {
                // 如果检查过程出错，保守地返回 false
                return false;
            }
        }

        /// <summary>
        /// 获取对象名称
        /// </summary>
        private string GetObjectName(ObjectData objectData)
        {
            try
            {
                return objectData.GenerateObjectName(_snapshot);
            }
            catch
            {
                return "<Unknown>";
            }
        }

        /// <summary>
        /// 获取对象类型
        /// </summary>
        private string GetObjectType(ObjectData objectData)
        {
            try
            {
                return objectData.GenerateTypeName(_snapshot, truncateTypeName: false);
            }
            catch
            {
                return "<Unknown>";
            }
        }

        /// <summary>
        /// 获取字段名称
        /// </summary>
        private string GetFieldName(ObjectData data)
        {
            try
            {
                if (data.IsField())
                {
                    return data.GetFieldName(_snapshot);
                }
                else if (data.IsArrayItem())
                {
                    return $"[{data.arrayIndex}]";
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取对象大小
        /// </summary>
        private long GetObjectSize(ObjectData objectData)
        {
            try
            {
                if (objectData.dataType == ObjectDataType.NativeObject && objectData.nativeObjectIndex >= 0)
                {
                    if (objectData.nativeObjectIndex < _snapshot.NativeObjects.Count)
                    {
                        return (long)_snapshot.NativeObjects.Size[objectData.nativeObjectIndex];
                    }
                }
                else if (objectData.isManaged)
                {
                    var managedIndex = objectData.GetManagedObjectIndex(_snapshot);
                    if (managedIndex >= 0 && managedIndex < _snapshot.CrawledData.ManagedObjects.Count)
                    {
                        return _snapshot.CrawledData.ManagedObjects[managedIndex].Size;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// 引用路径节点
    /// </summary>
    internal class ReferencePathNode
    {
        public int Id { get; set; } // 唯一节点ID，用于循环引用跳转
        public SourceIndex SourceIndex { get; set; }
        public ObjectData DisplayObjectData { get; set; } // 保存原始的displayObject，用于生成正确的DisplayName
        public ObjectData ConnectionData { get; set; } // 保存connection数据，用于获取字段名等
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string FieldName { get; set; } = "";
        public long Size { get; set; }
        public bool IsGCRoot { get; set; }
        public int Depth { get; set; }
        public ReferencePathNode? Parent { get; set; }
        public List<ReferencePathNode> Children { get; set; } = new List<ReferencePathNode>();
        public int CircularRefId { get; set; } = -1; // ID of the ancestor node this circularly references
    }
}
