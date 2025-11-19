using System.Collections.Generic;
using Unity.MemoryProfiler.Editor.UI.Models;

namespace Unity.MemoryProfiler.UI.Utilities
{
    /// <summary>
    /// Tree Model工具类
    /// 提供树形结构的通用操作方法
    /// </summary>
    internal static class TreeModelUtility
    {
        /// <summary>
        /// 获取树的所有叶子节点（扁平化）
        /// 等价于Unity的TreeModelUtility.RetrieveLeafNodesOfTree
        /// </summary>
        public static List<TreeNode<TData>> RetrieveLeafNodesOfTree<TData>(List<TreeNode<TData>> rootNodes)
        {
            var leafNodes = new List<TreeNode<TData>>();
            RetrieveLeafNodesRecursive(rootNodes, leafNodes);
            return leafNodes;
        }

        /// <summary>
        /// 递归获取叶子节点
        /// </summary>
        private static void RetrieveLeafNodesRecursive<TData>(IEnumerable<TreeNode<TData>> nodes, List<TreeNode<TData>> leafNodes)
        {
            foreach (var node in nodes)
            {
                if (node.Children.Count == 0)
                {
                    // 叶子节点
                    leafNodes.Add(node);
                }
                else
                {
                    // 递归处理子节点
                    RetrieveLeafNodesRecursive(node.Children, leafNodes);
                }
            }
        }
    }
}

