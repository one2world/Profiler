using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// WPF树模型基类，提供树结构的通用功能
    /// 支持INotifyPropertyChanged，适配WPF MVVM模式
    /// 对应Unity的TreeModel<T>，但为WPF优化
    /// </summary>
    /// <typeparam name="TItemData">树节点数据类型</typeparam>
    public abstract class TreeModel<TItemData> : INotifyPropertyChanged
    {
        private ObservableCollection<TreeNode<TItemData>> _rootNodes;

        protected TreeModel()
        {
            _rootNodes = new ObservableCollection<TreeNode<TItemData>>();
        }

        protected TreeModel(ObservableCollection<TreeNode<TItemData>> rootNodes)
        {
            _rootNodes = rootNodes ?? new ObservableCollection<TreeNode<TItemData>>();
        }

        /// <summary>
        /// 树的根节点集合
        /// </summary>
        public ObservableCollection<TreeNode<TItemData>> RootNodes
        {
            get => _rootNodes;
            protected set => SetProperty(ref _rootNodes, value);
        }

        /// <summary>
        /// 获取扁平化的节点列表（深度优先遍历）
        /// 用于虚拟化TreeView绑定
        /// </summary>
        public List<TreeNode<TItemData>> GetFlattenedList()
        {
            var result = new List<TreeNode<TItemData>>();
            foreach (var rootNode in RootNodes)
            {
                AddNodeAndDescendants(rootNode, result);
            }
            return result;
        }

        /// <summary>
        /// 获取扁平化的可见节点列表（只包含展开节点的子节点）
        /// </summary>
        public List<TreeNode<TItemData>> GetFlattenedVisibleList()
        {
            var result = new List<TreeNode<TItemData>>();
            foreach (var rootNode in RootNodes)
            {
                AddNodeAndVisibleDescendants(rootNode, result);
            }
            return result;
        }

        private void AddNodeAndDescendants(TreeNode<TItemData> node, List<TreeNode<TItemData>> list)
        {
            if (node == null)
                return;

            list.Add(node);
            foreach (var child in node.Children)
            {
                AddNodeAndDescendants(child, list);
            }
        }

        private void AddNodeAndVisibleDescendants(TreeNode<TItemData> node, List<TreeNode<TItemData>> list)
        {
            if (node == null || !node.IsVisible)
                return;

            list.Add(node);
            if (node.IsExpanded)
            {
                foreach (var child in node.Children)
                {
                    AddNodeAndVisibleDescendants(child, list);
                }
            }
        }

        /// <summary>
        /// 排序树节点（递归）
        /// </summary>
        public void Sort(Comparison<TreeNode<TItemData>> comparison)
        {
            if (comparison == null)
                return;

            // 使用List临时存储以便排序
            var sortedRoots = RootNodes.OrderBy(x => x, Comparer<TreeNode<TItemData>>.Create(comparison)).ToList();
            RootNodes.Clear();
            foreach (var node in sortedRoots)
            {
                RootNodes.Add(node);
            }

            // 递归排序子节点
            var stack = new Stack<TreeNode<TItemData>>(RootNodes);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node.HasChildren)
                {
                    var sortedChildren = node.Children.OrderBy(x => x, Comparer<TreeNode<TItemData>>.Create(comparison)).ToList();
                    node.Children.Clear();
                    foreach (var child in sortedChildren)
                    {
                        node.Children.Add(child);
                        stack.Push(child);
                    }
                }
            }

            OnPropertyChanged(nameof(RootNodes));
        }

        /// <summary>
        /// 查找满足条件的第一个节点
        /// </summary>
        public TreeNode<TItemData> FindFirst(Predicate<TreeNode<TItemData>> predicate)
        {
            foreach (var rootNode in RootNodes)
            {
                if (predicate(rootNode))
                    return rootNode;

                var found = rootNode.FindDescendant(predicate);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// 查找所有满足条件的节点
        /// </summary>
        public List<TreeNode<TItemData>> FindAll(Predicate<TreeNode<TItemData>> predicate)
        {
            var results = new List<TreeNode<TItemData>>();
            foreach (var rootNode in RootNodes)
            {
                if (predicate(rootNode))
                    results.Add(rootNode);

                results.AddRange(rootNode.FindDescendants(predicate));
            }
            return results;
        }

        /// <summary>
        /// 展开所有节点到指定深度
        /// </summary>
        public void ExpandToDepth(int depth)
        {
            foreach (var rootNode in RootNodes)
            {
                rootNode.ExpandToDepth(depth);
            }
            OnPropertyChanged(nameof(RootNodes));
        }

        /// <summary>
        /// 全部展开
        /// </summary>
        public void ExpandAll()
        {
            foreach (var rootNode in RootNodes)
            {
                rootNode.ExpandAll();
            }
            OnPropertyChanged(nameof(RootNodes));
        }

        /// <summary>
        /// 全部折叠
        /// </summary>
        public void CollapseAll()
        {
            foreach (var rootNode in RootNodes)
            {
                rootNode.CollapseAll();
            }
            OnPropertyChanged(nameof(RootNodes));
        }

        /// <summary>
        /// 清空所有节点
        /// </summary>
        public void Clear()
        {
            RootNodes.Clear();
            OnPropertyChanged(nameof(RootNodes));
        }

        /// <summary>
        /// 获取树的统计信息
        /// </summary>
        public TreeStatistics GetStatistics()
        {
            int totalNodes = 0;
            int maxDepth = 0;
            int leafNodes = 0;

            foreach (var rootNode in RootNodes)
            {
                CalculateStatistics(rootNode, 0, ref totalNodes, ref maxDepth, ref leafNodes);
            }

            return new TreeStatistics
            {
                TotalNodes = totalNodes,
                MaxDepth = maxDepth,
                LeafNodes = leafNodes,
                RootNodes = RootNodes.Count
            };
        }

        private void CalculateStatistics(TreeNode<TItemData> node, int currentDepth, ref int totalNodes, ref int maxDepth, ref int leafNodes)
        {
            totalNodes++;
            maxDepth = Math.Max(maxDepth, currentDepth);

            if (!node.HasChildren)
            {
                leafNodes++;
            }
            else
            {
                foreach (var child in node.Children)
                {
                    CalculateStatistics(child, currentDepth + 1, ref totalNodes, ref maxDepth, ref leafNodes);
                }
            }
        }

        /// <summary>
        /// 过滤树（保留满足条件的节点及其路径）
        /// </summary>
        public void Filter(Predicate<TreeNode<TItemData>> predicate)
        {
            if (predicate == null)
            {
                // 重置过滤：显示所有节点
                foreach (var node in GetFlattenedList())
                {
                    node.IsVisible = true;
                }
            }
            else
            {
                // 首先隐藏所有节点
                foreach (var node in GetFlattenedList())
                {
                    node.IsVisible = false;
                }

                // 显示匹配节点及其路径
                var matchedNodes = FindAll(predicate);
                foreach (var node in matchedNodes)
                {
                    // 显示匹配节点
                    node.IsVisible = true;

                    // 显示其所有祖先节点（确保路径可见）
                    var current = node.Parent;
                    while (current != null)
                    {
                        current.IsVisible = true;
                        current.IsExpanded = true; // 展开以显示匹配的子节点
                        current = current.Parent;
                    }

                    // 显示其所有后代节点（如果需要）
                    foreach (var descendant in node.GetDescendants())
                    {
                        descendant.IsVisible = true;
                    }
                }
            }

            OnPropertyChanged(nameof(RootNodes));
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// 树统计信息
    /// </summary>
    public struct TreeStatistics
    {
        public int TotalNodes;
        public int RootNodes;
        public int MaxDepth;
        public int LeafNodes;

        public override string ToString()
        {
            return $"Total: {TotalNodes}, Roots: {RootNodes}, MaxDepth: {MaxDepth}, Leaves: {LeafNodes}";
        }
    }
}

