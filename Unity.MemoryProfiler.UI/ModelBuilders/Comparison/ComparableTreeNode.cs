using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.ModelBuilders.Comparison
{
    /// <summary>
    /// 通用树节点，用于TreeComparisonBuilder的输入
    /// </summary>
    /// <typeparam name="T">实现IComparableItemData的数据类型</typeparam>
    public class ComparableTreeNode<T> where T : IComparableItemData
    {
        public ComparableTreeNode(int id, T data, List<ComparableTreeNode<T>> children = null)
        {
            Id = id;
            Data = data;
            Children = children ?? new List<ComparableTreeNode<T>>();
        }

        /// <summary>
        /// 节点ID（可选，用于保留特定类别ID）
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 节点数据
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<ComparableTreeNode<T>> Children { get; }

        /// <summary>
        /// 是否有子节点
        /// </summary>
        public bool HasChildren => Children != null && Children.Count > 0;
    }
}

