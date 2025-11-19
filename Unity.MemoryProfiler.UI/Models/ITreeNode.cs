using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 树形节点接口，用于TreeListView的通用展开状态保存
    /// </summary>
    public interface ITreeNode
    {
        /// <summary>
        /// 节点唯一标识符
        /// </summary>
        int Id { get; }

        /// <summary>
        /// 子节点列表
        /// </summary>
        IEnumerable<object>? GetChildren();
    }
}

