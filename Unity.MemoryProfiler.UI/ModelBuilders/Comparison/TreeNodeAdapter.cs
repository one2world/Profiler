using System.Collections.Generic;
using Unity.MemoryProfiler.Editor.UI.Models;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.ModelBuilders.Comparison
{
    /// <summary>
    /// TreeNode适配器，将TreeNode<MemoryItemData>或AllTrackedMemoryTreeNode转换为ComparableTreeNode<IComparableItemData>
    /// </summary>
    internal static class TreeNodeAdapter
    {
        /// <summary>
        /// 将AllTrackedMemoryTreeNode转换为ComparableTreeNode<AllTrackedMemoryNodeAdapter>
        /// </summary>
        public static List<ComparableTreeNode<AllTrackedMemoryNodeAdapter>> ConvertAllTrackedMemoryNodes(
            List<AllTrackedMemoryTreeNode> treeNodes)
        {
            var result = new List<ComparableTreeNode<AllTrackedMemoryNodeAdapter>>();

            int id = 0;
            foreach (var node in treeNodes)
            {
                var comparableNode = ConvertAllTrackedMemoryNode(node, id++);
                result.Add(comparableNode);
            }

            return result;
        }

        static ComparableTreeNode<AllTrackedMemoryNodeAdapter> ConvertAllTrackedMemoryNode(AllTrackedMemoryTreeNode node, int id)
        {
            // 适配数据
            var adapter = new AllTrackedMemoryNodeAdapter(node);

            // 递归转换子节点
            var children = new List<ComparableTreeNode<AllTrackedMemoryNodeAdapter>>();
            if (node.Children != null)
            {
                int childId = 0;
                foreach (var child in node.Children)
                {
                    children.Add(ConvertAllTrackedMemoryNode(child, childId++));
                }
            }

            return new ComparableTreeNode<AllTrackedMemoryNodeAdapter>(id, adapter, children);
        }

        /// <summary>
        /// 将UnityObjectTreeNode转换为ComparableTreeNode<UnityObjectNodeAdapter>
        /// </summary>
        public static List<ComparableTreeNode<UnityObjectNodeAdapter>> ConvertUnityObjectNodes(
            List<UnityObjectTreeNode> treeNodes)
        {
            var result = new List<ComparableTreeNode<UnityObjectNodeAdapter>>();

            int id = 0;
            foreach (var node in treeNodes)
            {
                var comparableNode = ConvertUnityObjectNode(node, id++);
                result.Add(comparableNode);
            }

            return result;
        }

        static ComparableTreeNode<UnityObjectNodeAdapter> ConvertUnityObjectNode(UnityObjectTreeNode node, int id)
        {
            // 适配数据
            var adapter = new UnityObjectNodeAdapter(node);

            // 递归转换子节点
            var children = new List<ComparableTreeNode<UnityObjectNodeAdapter>>();
            if (node.Children != null)
            {
                int childId = 0;
                foreach (var child in node.Children)
                {
                    children.Add(ConvertUnityObjectNode(child, childId++));
                }
            }

            return new ComparableTreeNode<UnityObjectNodeAdapter>(id, adapter, children);
        }

        /// <summary>
        /// 将TreeNode<MemoryItemData>转换为ComparableTreeNode<MemoryItemDataAdapter>
        /// </summary>
        public static List<ComparableTreeNode<MemoryItemDataAdapter>> ConvertToComparableNodes(
            IEnumerable<TreeNode<MemoryItemData>> treeNodes)
        {
            var result = new List<ComparableTreeNode<MemoryItemDataAdapter>>();

            foreach (var node in treeNodes)
            {
                var comparableNode = ConvertNode(node, 0);
                result.Add(comparableNode);
            }

            return result;
        }

        static ComparableTreeNode<MemoryItemDataAdapter> ConvertNode(TreeNode<MemoryItemData> node, int id)
        {
            // 适配数据
            var adapter = new MemoryItemDataAdapter(node.Data);

            // 递归转换子节点
            var children = new List<ComparableTreeNode<MemoryItemDataAdapter>>();
            if (node.Children != null)
            {
                int childId = 0;
                foreach (var child in node.Children)
                {
                    children.Add(ConvertNode(child, childId++));
                }
            }

            return new ComparableTreeNode<MemoryItemDataAdapter>(id, adapter, children);
        }

        /// <summary>
        /// UnityObjectTreeNode的适配器，实现IComparableItemData接口
        /// </summary>
        public class UnityObjectNodeAdapter : IComparableItemData
        {
            private readonly UnityObjectTreeNode _node;

            public UnityObjectNodeAdapter(UnityObjectTreeNode node)
            {
                _node = node;
            }

            public string Name => _node?.Name ?? string.Empty;

            public ulong SizeInBytes => _node?.TotalSize ?? 0;
        }

        /// <summary>
        /// AllTrackedMemoryTreeNode的适配器，实现IComparableItemData接口
        /// </summary>
        public class AllTrackedMemoryNodeAdapter : IComparableItemData
        {
            private readonly AllTrackedMemoryTreeNode _node;

            public AllTrackedMemoryNodeAdapter(AllTrackedMemoryTreeNode node)
            {
                _node = node;
            }

            public string Name => _node?.Name ?? string.Empty;

            public ulong SizeInBytes => _node?.AllocatedSize ?? 0;
        }

        /// <summary>
        /// MemoryItemData的适配器，实现IComparableItemData接口
        /// </summary>
        public class MemoryItemDataAdapter : IComparableItemData
        {
            private readonly MemoryItemData _data;

            public MemoryItemDataAdapter(MemoryItemData data)
            {
                _data = data;
            }

            public string Name => _data?.Name ?? string.Empty;

            public ulong SizeInBytes => _data != null ? (ulong)Math.Max(0, _data.Size) : 0;
        }

        /// <summary>
        /// 将 ManagedCallStackNode 转换为 ComparableTreeNode<ManagedCallStackNodeAdapter>
        /// </summary>
        public static List<ComparableTreeNode<ManagedCallStackNodeAdapter>> ConvertManagedObjectsNodes(
            List<ManagedCallStackNode> treeNodes)
        {
            var result = new List<ComparableTreeNode<ManagedCallStackNodeAdapter>>();

            int id = 0;
            foreach (var node in treeNodes)
            {
                var comparableNode = ConvertManagedCallStackNode(node, id++);
                result.Add(comparableNode);
            }

            return result;
        }

        static ComparableTreeNode<ManagedCallStackNodeAdapter> ConvertManagedCallStackNode(ManagedCallStackNode node, int id)
        {
            // 适配数据
            var adapter = new ManagedCallStackNodeAdapter(node);

            // 递归转换子节点
            var children = new List<ComparableTreeNode<ManagedCallStackNodeAdapter>>();
            if (node.Children != null)
            {
                int childId = 0;
                foreach (var child in node.Children)
                {
                    children.Add(ConvertManagedCallStackNode(child, childId++));
                }
            }

            return new ComparableTreeNode<ManagedCallStackNodeAdapter>(id, adapter, children);
        }

        /// <summary>
        /// ManagedCallStackNode 的适配器，实现 IComparableItemData 接口
        /// </summary>
        public class ManagedCallStackNodeAdapter : IComparableItemData
        {
            private readonly ManagedCallStackNode _node;

            public ManagedCallStackNodeAdapter(ManagedCallStackNode node)
            {
                _node = node;
            }

            public string Name => _node?.Description ?? string.Empty;

            public ulong SizeInBytes => _node?.Size ?? 0;
        }
    }
}

