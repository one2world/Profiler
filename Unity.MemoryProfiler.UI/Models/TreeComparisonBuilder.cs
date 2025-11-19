using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// 树对比构建器 - 从两棵树构建对比树
    /// Unity官方实现参考：TreeComparisonBuilder.cs
    /// 核心算法100%等价Unity官方实现
    /// </summary>
    public class TreeComparisonBuilder
    {
        /// <summary>
        /// 从两棵输入树构建对比树
        /// </summary>
        /// <param name="treeA">树A的根节点</param>
        /// <param name="treeB">树B的根节点</param>
        /// <param name="includeUnchanged">是否包含未改变的项</param>
        /// <param name="largestAbsoluteSizeDelta">输出最大绝对大小差异</param>
        /// <returns>对比树的根节点列表</returns>
        public List<ComparisonTreeNode> Build(
            ObservableCollection<TreeNode<MemoryItemData>> treeA,
            ObservableCollection<TreeNode<MemoryItemData>> treeB,
            bool includeUnchanged,
            out long largestAbsoluteSizeDelta)
        {
            // 步骤1：构建中间树（递归合并两棵树）
            var intermediateTree = BuildIntermediateTree(treeA, treeB);

            // 步骤2：从中间树构建最终对比树
            var comparisonTree = BuildComparisonTreeFromIntermediateTree(
                intermediateTree,
                includeUnchanged,
                out largestAbsoluteSizeDelta);

            return comparisonTree;
        }

        /// <summary>
        /// 构建中间树 - 递归合并两棵树
        /// Unity官方算法：使用三个Stack进行深度优先遍历
        /// </summary>
        private static List<IntermediateNode> BuildIntermediateTree(
            ObservableCollection<TreeNode<MemoryItemData>> treeA,
            ObservableCollection<TreeNode<MemoryItemData>> treeB)
        {
            // 创建虚拟根节点用于遍历
            var rootNodeA = new TreeNode<MemoryItemData>(new MemoryItemData { Name = "Root" });
            foreach (var child in treeA)
                rootNodeA.Children.Add(child);

            var rootNodeB = new TreeNode<MemoryItemData>(new MemoryItemData { Name = "Root" });
            foreach (var child in treeB)
                rootNodeB.Children.Add(child);

            // 三个Stack用于深度优先遍历
            var itemStackA = new Stack<TreeNode<MemoryItemData>>();
            itemStackA.Push(rootNodeA);

            var itemStackB = new Stack<TreeNode<MemoryItemData>>();
            itemStackB.Push(rootNodeB);

            var parentOutputNodeStack = new Stack<IntermediateNode>();
            var rootOutputNode = new IntermediateNode(null, new IntermediateNodeData("", 0, 0, 0, 0));
            parentOutputNodeStack.Push(rootOutputNode);

            // 按名称排序比较器
            var sortByNameComparison = new Comparison<TreeNode<MemoryItemData>>(
                (x, y) => string.Compare(x.Data.Name, y.Data.Name, StringComparison.Ordinal));

            // 深度优先遍历
            while (itemStackA.Count > 0 && itemStackB.Count > 0)
            {
                var itemA = itemStackA.Pop();
                var itemB = itemStackB.Pop();
                var parentOutputNode = parentOutputNodeStack.Pop();

                // 获取子节点并按名称排序
                var childrenA = new List<TreeNode<MemoryItemData>>(itemA.Children);
                var childrenB = new List<TreeNode<MemoryItemData>>(itemB.Children);
                childrenA.Sort(sortByNameComparison);
                childrenB.Sort(sortByNameComparison);

                // 匹配相同名称的项
                MatchSortedItems(
                    childrenA,
                    childrenB,
                    (itemsA, itemsB) =>
                    {
                        // 重要假设：只有叶子节点有exclusiveSize，非叶子节点的大小由子节点累加
                        ulong exclusiveSizeInA = 0;
                        uint exclusiveCountInA = 0;
                        foreach (var item in itemsA)
                        {
                            if (item.Children.Count == 0) // 叶子节点
                            {
                                exclusiveSizeInA += (ulong)Math.Max(0, item.Data.Size);
                                exclusiveCountInA++;
                            }
                        }

                        ulong exclusiveSizeInB = 0;
                        uint exclusiveCountInB = 0;
                        foreach (var item in itemsB)
                        {
                            if (item.Children.Count == 0) // 叶子节点
                            {
                                exclusiveSizeInB += (ulong)Math.Max(0, item.Data.Size);
                                exclusiveCountInB++;
                            }
                        }

                        // 获取名称（优先从A，如果A为空则从B）
                        var name = (itemsA.Count > 0) ? itemsA[0].Data.Name : itemsB[0].Data.Name;

                        // 创建中间节点
                        var data = new IntermediateNodeData(
                            name,
                            exclusiveSizeInA,
                            exclusiveSizeInB,
                            exclusiveCountInA,
                            exclusiveCountInB);
                        var outputNode = new IntermediateNode(parentOutputNode, data);
                        parentOutputNode.Children.Add(outputNode);

                        // 如果有子节点，继续递归处理
                        var firstItemA = itemsA.FirstOrDefault();
                        var firstItemB = itemsB.FirstOrDefault();
                        var hasChildren = (firstItemA?.Children.Count > 0) || (firstItemB?.Children.Count > 0);
                        if (hasChildren)
                        {
                            itemStackA.Push(firstItemA ?? new TreeNode<MemoryItemData>(new MemoryItemData { Name = name }));
                            itemStackB.Push(firstItemB ?? new TreeNode<MemoryItemData>(new MemoryItemData { Name = name }));
                            parentOutputNodeStack.Push(outputNode);
                        }
                    });
            }

            return new List<IntermediateNode>(rootOutputNode.Children);
        }

        /// <summary>
        /// 匹配排序后的项 - Unity官方核心算法（双指针合并）
        /// 将两个已排序的列表按名称匹配，调用match回调处理每组匹配
        /// </summary>
        private static void MatchSortedItems(
            List<TreeNode<MemoryItemData>> itemsSortedByNameA,
            List<TreeNode<MemoryItemData>> itemsSortedByNameB,
            Action<List<TreeNode<MemoryItemData>>, List<TreeNode<MemoryItemData>>> match)
        {
            var indexA = 0;
            var indexB = 0;

            // 双指针算法遍历两个已排序列表
            while (indexA < itemsSortedByNameA.Count || indexB < itemsSortedByNameB.Count)
            {
                string? nameA = null;
                if (indexA < itemsSortedByNameA.Count)
                    nameA = itemsSortedByNameA[indexA].Data.Name;

                string? nameB = null;
                if (indexB < itemsSortedByNameB.Count)
                    nameB = itemsSortedByNameB[indexB].Data.Name;

                // 比较名称
                int comparison;
                if (nameA == null)
                    comparison = 1;  // A已结束，处理B
                else if (nameB == null)
                    comparison = -1; // B已结束，处理A
                else
                    comparison = string.Compare(nameA, nameB, StringComparison.Ordinal);

                var itemsWithNameInA = new List<TreeNode<MemoryItemData>>();
                var itemsWithNameInB = new List<TreeNode<MemoryItemData>>();

                // 收集A中所有相同名称的项
                if (comparison <= 0)
                {
                    string? nextNameA;
                    do
                    {
                        var itemA = itemsSortedByNameA[indexA];
                        itemsWithNameInA.Add(itemA);

                        indexA++;
                        nextNameA = (indexA < itemsSortedByNameA.Count) ? itemsSortedByNameA[indexA].Data.Name : null;
                    }
                    while (string.Compare(nameA, nextNameA, StringComparison.Ordinal) == 0);
                }

                // 收集B中所有相同名称的项
                if (comparison >= 0)
                {
                    string? nextNameB;
                    do
                    {
                        var itemB = itemsSortedByNameB[indexB];
                        itemsWithNameInB.Add(itemB);

                        indexB++;
                        nextNameB = (indexB < itemsSortedByNameB.Count) ? itemsSortedByNameB[indexB].Data.Name : null;
                    }
                    while (string.Compare(nameB, nextNameB, StringComparison.Ordinal) == 0);
                }

                // 调用match回调处理这组匹配
                match?.Invoke(itemsWithNameInA, itemsWithNameInB);
            }
        }

        /// <summary>
        /// 从中间树构建最终对比树 - 使用后序遍历计算inclusive size
        /// Unity官方算法：后序遍历累加子节点大小
        /// </summary>
        private static List<ComparisonTreeNode> BuildComparisonTreeFromIntermediateTree(
            List<IntermediateNode> intermediateTree,
            bool includeUnchanged,
            out long largestAbsoluteSizeDelta)
        {
            // 步骤1：先序遍历构建后序遍历栈
            var stack = new Stack<IntermediateNode>(intermediateTree);
            var postOrderStack = new Stack<IntermediateNode>();
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                postOrderStack.Push(node);

                var children = node.Children;
                if (children.Count > 0)
                {
                    foreach (var child in children)
                        stack.Push(child);
                }
            }

            // 步骤2：后序遍历（从叶子到根）计算inclusive size
            largestAbsoluteSizeDelta = 0;
            while (postOrderStack.Count > 0)
            {
                var node = postOrderStack.Pop();

                // 从exclusive size开始（只包含自己）
                ulong inclusiveSizeInA = node.Data.ExclusiveSizeInA;
                ulong inclusiveSizeInB = node.Data.ExclusiveSizeInB;
                uint inclusiveCountInA = node.Data.ExclusiveCountInA;
                uint inclusiveCountInB = node.Data.ExclusiveCountInB;

                // 累加所有子节点的inclusive size
                var children = node.Children;
                if (children.Count > 0)
                {
                    foreach (var child in children)
                    {
                        if (child.OutputItem != null)
                        {
                            inclusiveSizeInA += child.OutputItem.TotalSizeInA;
                            inclusiveSizeInB += child.OutputItem.TotalSizeInB;
                            inclusiveCountInA += child.OutputItem.CountInA;
                            inclusiveCountInB += child.OutputItem.CountInB;
                        }
                    }
                }

                // 创建ComparisonTreeNode
                var comparisonNode = new ComparisonTreeNode(
                    node.Data.Name,
                    inclusiveSizeInA,
                    inclusiveSizeInB,
                    inclusiveCountInA,
                    inclusiveCountInB);

                // 添加子节点
                foreach (var child in children)
                {
                    if (child.OutputItem != null)
                        comparisonNode.Children.Add(child.OutputItem);
                }

                // 过滤掉未改变的项（如果设置了）
                if (!comparisonNode.HasChanged && !includeUnchanged)
                    continue;

                // 存储输出项
                node.OutputItem = comparisonNode;

                // 更新最大差异
                var absoluteSizeDelta = Math.Abs(comparisonNode.SizeDelta);
                largestAbsoluteSizeDelta = Math.Max(absoluteSizeDelta, largestAbsoluteSizeDelta);
            }

            // 步骤3：收集根节点
            var finalTree = new List<ComparisonTreeNode>(intermediateTree.Count);
            foreach (var rootNode in intermediateTree)
            {
                if (rootNode.OutputItem != null)
                    finalTree.Add(rootNode.OutputItem);
            }

            return finalTree;
        }

        /// <summary>
        /// 中间节点 - 用于构建过程中的临时节点
        /// </summary>
        private class IntermediateNode
        {
            public IntermediateNode(IntermediateNode? parent, IntermediateNodeData data)
            {
                Parent = parent;
                Children = new List<IntermediateNode>();
                Data = data;
            }

            public IntermediateNode? Parent { get; }
            public List<IntermediateNode> Children { get; }
            public IntermediateNodeData Data { get; }
            public ComparisonTreeNode? OutputItem { get; set; }
        }

        /// <summary>
        /// 中间节点数据 - 只包含exclusive size/count
        /// </summary>
        private readonly struct IntermediateNodeData
        {
            public IntermediateNodeData(
                string name,
                ulong exclusiveSizeInA,
                ulong exclusiveSizeInB,
                uint exclusiveCountInA,
                uint exclusiveCountInB)
            {
                Name = name;
                ExclusiveSizeInA = exclusiveSizeInA;
                ExclusiveSizeInB = exclusiveSizeInB;
                ExclusiveCountInA = exclusiveCountInA;
                ExclusiveCountInB = exclusiveCountInB;
            }

            public string Name { get; }
            public ulong ExclusiveSizeInA { get; }
            public ulong ExclusiveSizeInB { get; }
            public uint ExclusiveCountInA { get; }
            public uint ExclusiveCountInB { get; }
        }
    }
}

