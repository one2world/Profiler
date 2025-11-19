using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.UI.Models.Comparison;

namespace Unity.MemoryProfiler.UI.ModelBuilders.Comparison
{
    /// <summary>
    /// 树对比构建器，将两棵树按名称匹配并生成对比结果
    /// 等价于Unity的TreeComparisonBuilder
    /// </summary>
    public class TreeComparisonBuilder
    {
        /// <summary>
        /// 构建对比树
        /// </summary>
        /// <typeparam name="T">实现IComparableItemData的数据类型</typeparam>
        /// <param name="treeA">A快照的树</param>
        /// <param name="treeB">B快照的树</param>
        /// <param name="args">构建参数</param>
        /// <param name="largestAbsoluteSizeDelta">输出最大绝对大小差值</param>
        /// <returns>对比树根节点列表</returns>
        public List<ComparisonTreeNode> Build<T>(
            List<ComparableTreeNode<T>> treeA,
            List<ComparableTreeNode<T>> treeB,
            BuildArgs args,
            out long largestAbsoluteSizeDelta)
            where T : IComparableItemData
        {
            // 步骤1：构建中间树（匹配节点，计算exclusive size/count）
            var intermediateTree = BuildIntermediateTree(treeA, treeB);

            // 步骤2：转换为最终的ComparisonTreeNode树（计算inclusive size/count）
            var comparisonTree = BuildFinalTreeFromIntermediateTree(
                intermediateTree,
                args,
                out largestAbsoluteSizeDelta);

            return comparisonTree;
        }

        /// <summary>
        /// 构建中间树：匹配A和B的节点，计算exclusive size/count
        /// </summary>
        static List<IntermediateNode> BuildIntermediateTree<T>(
            IEnumerable<ComparableTreeNode<T>> treeA,
            IEnumerable<ComparableTreeNode<T>> treeB)
            where T : IComparableItemData
        {
            // 使用栈进行深度优先遍历
            var stackA = new Stack<ComparableTreeNode<T>>();
            var stackB = new Stack<ComparableTreeNode<T>>();
            var parentOutputNodeStack = new Stack<IntermediateNode>();

            // 创建虚拟根节点用于遍历
            var rootA = new ComparableTreeNode<T>(-1, default(T), new List<ComparableTreeNode<T>>(treeA));
            var rootB = new ComparableTreeNode<T>(-1, default(T), new List<ComparableTreeNode<T>>(treeB));
            stackA.Push(rootA);
            stackB.Push(rootB);

            var rootOutputNode = new IntermediateNode(null, new IntermediateNodeData(null, 0, 0, 0, 0, -1));
            parentOutputNodeStack.Push(rootOutputNode);

            // 名称排序比较器
            var sortByNameComparison = new Comparison<ComparableTreeNode<T>>(
                (x, y) => string.Compare(x.Data?.Name, y.Data?.Name, StringComparison.Ordinal));

            // 深度优先遍历
            while (stackA.Count > 0 && stackB.Count > 0)
            {
                var itemA = stackA.Pop();
                var itemB = stackB.Pop();
                var parentOutputNode = parentOutputNodeStack.Pop();

                // 获取子节点并排序
                var childrenA = new List<ComparableTreeNode<T>>(itemA.Children ?? new List<ComparableTreeNode<T>>());
                var childrenB = new List<ComparableTreeNode<T>>(itemB.Children ?? new List<ComparableTreeNode<T>>());
                childrenA.Sort(sortByNameComparison);
                childrenB.Sort(sortByNameComparison);

                // 匹配同名子节点
                MatchSortedItems(
                    childrenA,
                    childrenB,
                    (itemsA, itemsB) =>
                    {
                        // 假设：非叶子节点的exclusive size/count为0，只有叶子节点有非零exclusive值
                        var exclusiveSizeInA = 0UL;
                        var exclusiveCountInA = 0U;
                        foreach (var item in itemsA)
                        {
                            if (!item.HasChildren)
                            {
                                exclusiveSizeInA += item.Data.SizeInBytes;
                                exclusiveCountInA++;
                            }
                        }

                        var exclusiveSizeInB = 0UL;
                        var exclusiveCountInB = 0U;
                        foreach (var item in itemsB)
                        {
                            if (!item.HasChildren)
                            {
                                exclusiveSizeInB += item.Data.SizeInBytes;
                                exclusiveCountInB++;
                            }
                        }

                        // 获取名称（从A或B中任一取）
                        var name = (itemsA.Count > 0) ? itemsA[0].Data.Name : itemsB[0].Data.Name;

                        // 检查是否有特殊ID（预定义类别），如果有则保留
                        var treeId = (itemsA.Count > 0) ? itemsA[0].Id : itemsB[0].Id;
                        if (treeId < 0) // 非预定义类别
                            treeId = -1;

                        // 创建中间节点
                        var data = new IntermediateNodeData(
                            name,
                            exclusiveSizeInA,
                            exclusiveSizeInB,
                            exclusiveCountInA,
                            exclusiveCountInB,
                            treeId);
                        var outputNode = new IntermediateNode(parentOutputNode, data);
                        parentOutputNode.Children.Add(outputNode);

                        // 如果有子节点，继续遍历
                        var a = itemsA.FirstOrDefault();
                        var b = itemsB.FirstOrDefault();
                        var hasChildren = (a?.HasChildren ?? false) || (b?.HasChildren ?? false);
                        if (hasChildren)
                        {
                            stackA.Push(a ?? new ComparableTreeNode<T>(-1, default(T)));
                            stackB.Push(b ?? new ComparableTreeNode<T>(-1, default(T)));
                            parentOutputNodeStack.Push(outputNode);
                        }
                    }
                );
            }

            return new List<IntermediateNode>(rootOutputNode.Children);
        }

        /// <summary>
        /// 匹配两个已按名称排序的列表中的同名项
        /// 核心算法：双指针同步遍历
        /// </summary>
        internal static void MatchSortedItems<T>(
            List<ComparableTreeNode<T>> itemsSortedByNameA,
            List<ComparableTreeNode<T>> itemsSortedByNameB,
            Action<List<ComparableTreeNode<T>>, List<ComparableTreeNode<T>>> match)
            where T : IComparableItemData
        {
            int indexA = 0;
            int indexB = 0;

            while (indexA < itemsSortedByNameA.Count || indexB < itemsSortedByNameB.Count)
            {
                string nameA = null;
                if (indexA < itemsSortedByNameA.Count)
                    nameA = itemsSortedByNameA[indexA].Data?.Name;

                string nameB = null;
                if (indexB < itemsSortedByNameB.Count)
                    nameB = itemsSortedByNameB[indexB].Data?.Name;

                // 比较名称
                int comparison;
                if (nameA == null)
                    comparison = 1; // A已遍历完，处理B的剩余项
                else if (nameB == null)
                    comparison = -1; // B已遍历完，处理A的剩余项
                else
                    comparison = string.Compare(nameA, nameB, StringComparison.Ordinal);

                var itemsWithNameInA = new List<ComparableTreeNode<T>>();
                var itemsWithNameInB = new List<ComparableTreeNode<T>>();

                // 收集A中所有同名项
                if (comparison <= 0)
                {
                    string nextNameA;
                    do
                    {
                        var itemA = itemsSortedByNameA[indexA];
                        itemsWithNameInA.Add(itemA);

                        indexA++;
                        nextNameA = (indexA < itemsSortedByNameA.Count) ? itemsSortedByNameA[indexA].Data?.Name : null;
                    }
                    while (string.Compare(nameA, nextNameA, StringComparison.Ordinal) == 0);
                }

                // 收集B中所有同名项
                if (comparison >= 0)
                {
                    string nextNameB;
                    do
                    {
                        var itemB = itemsSortedByNameB[indexB];
                        itemsWithNameInB.Add(itemB);

                        indexB++;
                        nextNameB = (indexB < itemsSortedByNameB.Count) ? itemsSortedByNameB[indexB].Data?.Name : null;
                    }
                    while (string.Compare(nameB, nextNameB, StringComparison.Ordinal) == 0);
                }

                // 调用match回调处理匹配结果
                match?.Invoke(itemsWithNameInA, itemsWithNameInB);
            }
        }

        /// <summary>
        /// 从中间树构建最终的ComparisonTreeNode树
        /// 使用后序深度优先遍历，自底向上计算inclusive size/count
        /// </summary>
        static List<ComparisonTreeNode> BuildFinalTreeFromIntermediateTree(
            List<IntermediateNode> intermediateTree,
            BuildArgs args,
            out long largestAbsoluteSizeDelta)
        {
            // 步骤1：先序遍历构建后序栈（用于后序遍历）
            var stack = new Stack<IntermediateNode>(intermediateTree);
            var postOrderStack = new Stack<IntermediateNode>();
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                postOrderStack.Push(node);

                if (node.Children.Count > 0)
                {
                    foreach (var child in node.Children)
                        stack.Push(child);
                }
            }

            // 步骤2：后序遍历，计算inclusive值并创建ComparisonTreeNode
            largestAbsoluteSizeDelta = 0;
            var nextDynamicId = 10000; // 动态ID起始值（避免与预定义类别冲突）

            while (postOrderStack.Count > 0)
            {
                var node = postOrderStack.Pop();

                // 从子节点累加计算inclusive值
                var inclusiveSizeInA = node.Data.ExclusiveSizeInA;
                var inclusiveSizeInB = node.Data.ExclusiveSizeInB;
                var inclusiveCountInA = node.Data.ExclusiveCountInA;
                var inclusiveCountInB = node.Data.ExclusiveCountInB;

                var childNodes = new List<ComparisonTreeNode>();
                foreach (var child in node.Children)
                {
                    if (child.OutputItem != null)
                    {
                        var childItem = child.OutputItem;
                        inclusiveSizeInA += childItem.Data.TotalSizeInA;
                        inclusiveSizeInB += childItem.Data.TotalSizeInB;
                        inclusiveCountInA += childItem.Data.CountInA;
                        inclusiveCountInB += childItem.Data.CountInB;
                        childNodes.Add(childItem);
                    }
                }

                // 构建ItemPath（从根到当前节点）
                var itemPath = new List<string>();
                var n = node;
                while (n != null)
                {
                    // 忽略虚拟根节点（Parent == null）
                    if (n.Parent == null)
                        break;

                    itemPath.Insert(0, n.Data.Name);
                    n = n.Parent;
                }

                // 创建ComparisonData
                var comparisonData = new ComparisonData(
                    node.Data.Name,
                    inclusiveSizeInA,
                    inclusiveSizeInB,
                    inclusiveCountInA,
                    inclusiveCountInB,
                    itemPath);

                // 过滤unchanged项（如果需要）
                if (!comparisonData.HasChanged && !args.IncludeUnchanged)
                    continue;

                // 创建ComparisonTreeNode并存储到中间节点
                var finalNode = new ComparisonTreeNode(comparisonData, childNodes);
                node.OutputItem = finalNode;

                // 更新largestAbsoluteSizeDelta
                var absoluteSizeDelta = Math.Abs(comparisonData.SizeDelta);
                largestAbsoluteSizeDelta = Math.Max(absoluteSizeDelta, largestAbsoluteSizeDelta);
            }

            // 步骤3：收集根节点
            var finalTree = new List<ComparisonTreeNode>();
            foreach (var rootNode in intermediateTree)
            {
                if (rootNode.OutputItem != null)
                {
                    finalTree.Add(rootNode.OutputItem);
                }
            }

            return finalTree;
        }

        /// <summary>
        /// 构建参数
        /// </summary>
        public readonly struct BuildArgs
        {
            public BuildArgs(bool includeUnchanged)
            {
                IncludeUnchanged = includeUnchanged;
            }

            /// <summary>
            /// 是否包含未变化的项
            /// </summary>
            public bool IncludeUnchanged { get; }
        }

        /// <summary>
        /// 中间节点（用于构建过程）
        /// </summary>
        class IntermediateNode
        {
            public IntermediateNode(IntermediateNode parent, IntermediateNodeData data)
            {
                Parent = parent;
                Data = data;
                Children = new List<IntermediateNode>();
            }

            public IntermediateNode Parent { get; }
            public IntermediateNodeData Data { get; }
            public List<IntermediateNode> Children { get; }
            public ComparisonTreeNode OutputItem { get; set; } // 构建完成后的最终节点
        }

        /// <summary>
        /// 中间节点数据
        /// </summary>
        readonly struct IntermediateNodeData
        {
            public IntermediateNodeData(
                string name,
                ulong exclusiveSizeInA,
                ulong exclusiveSizeInB,
                uint exclusiveCountInA,
                uint exclusiveCountInB,
                int treeNodeId)
            {
                Name = name;
                ExclusiveSizeInA = exclusiveSizeInA;
                ExclusiveSizeInB = exclusiveSizeInB;
                ExclusiveCountInA = exclusiveCountInA;
                ExclusiveCountInB = exclusiveCountInB;
                TreeNodeId = treeNodeId;
            }

            public string Name { get; }
            public ulong ExclusiveSizeInA { get; }
            public ulong ExclusiveSizeInB { get; }
            public uint ExclusiveCountInA { get; }
            public uint ExclusiveCountInB { get; }
            public int TreeNodeId { get; }
        }
    }
}

