using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.UI.Models;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Unity Objects breakdown的数据模型
    /// 等价于Unity官方的UnityObjectsModel
    /// 参考：Editor/UI/Analysis/Breakdowns/UnityObjects/Data/UnityObjectsModel.cs
    /// </summary>
    internal class UnityObjectsModel : TreeModel<UnityObjectsItemData>
    {
        public UnityObjectsModel(
            List<TreeNode<UnityObjectsItemData>> treeRootNodes,
            MemorySize totalSnapshotMemorySize,
            Action<SourceIndex> selectionProcessor)
            : base(new System.Collections.ObjectModel.ObservableCollection<TreeNode<UnityObjectsItemData>>(treeRootNodes))
        {
            TotalSnapshotMemorySize = totalSnapshotMemorySize;
            SelectionProcessor = selectionProcessor;

            // 计算总内存大小
            var totalMemorySize = new MemorySize();
            foreach (var rootItem in treeRootNodes)
                totalMemorySize += rootItem.Data.TotalSize;

            TotalMemorySize = totalMemorySize;

            // Workaround for inflated resident due to fake gfx resources
            // 防止fake gfx resources导致的resident膨胀
            TotalSnapshotMemorySize = MemorySize.Max(totalSnapshotMemorySize, totalMemorySize);
        }

        /// <summary>
        /// breakdown中占用的总内存大小
        /// </summary>
        public MemorySize TotalMemorySize { get; }

        /// <summary>
        /// 原始快照中的总内存大小
        /// </summary>
        public MemorySize TotalSnapshotMemorySize { get; }

        /// <summary>
        /// 选择此项时的回调处理
        /// </summary>
        public Action<SourceIndex> SelectionProcessor { get; }
    }

    /// <summary>
    /// Unity Object树中每个项的数据
    /// 等价于Unity官方的UnityObjectsModel.ItemData
    /// </summary>
    internal readonly struct UnityObjectsItemData
    {
        public UnityObjectsItemData(
            string name,
            MemorySize nativeSize,
            MemorySize managedSize,
            MemorySize gpuSize,
            SourceIndex source,
            int childCount = 0)
        {
            Name = name;
            TotalSize = nativeSize + managedSize + gpuSize;
            NativeSize = nativeSize;
            ManagedSize = managedSize;
            GpuSize = gpuSize;
            Source = source;
            ChildCount = childCount;
        }

        /// <summary>
        /// 项名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 总大小（Native + Managed + GPU）
        /// </summary>
        public MemorySize TotalSize { get; }

        /// <summary>
        /// Native内存大小（包含子项）
        /// </summary>
        public MemorySize NativeSize { get; }

        /// <summary>
        /// Managed内存大小（包含子项）
        /// </summary>
        public MemorySize ManagedSize { get; }

        /// <summary>
        /// GPU内存大小（包含子项）
        /// </summary>
        public MemorySize GpuSize { get; }

        /// <summary>
        /// CachedSnapshot中的索引，用于获取相关数据
        /// </summary>
        public SourceIndex Source { get; }

        /// <summary>
        /// 子项数量
        /// </summary>
        public int ChildCount { get; }

        // 格式化属性，用于UI显示
        public string TotalSizeFormatted => UnityObjectsItemData.FormatMemorySize(TotalSize);
        public string NativeSizeFormatted => UnityObjectsItemData.FormatMemorySize(NativeSize);
        public string ManagedSizeFormatted => UnityObjectsItemData.FormatMemorySize(ManagedSize);
        public string GpuSizeFormatted => UnityObjectsItemData.FormatMemorySize(GpuSize);

        /// <summary>
        /// 格式化内存大小（等价于EditorUtility.FormatBytes）
        /// </summary>
        private static string FormatMemorySize(MemorySize size)
        {
            return MemoryItemData.FormatBytes((long)size.Committed);
        }

        public override string ToString()
        {
            return $"{Name} (Total: {TotalSizeFormatted}, Native: {NativeSizeFormatted}, Managed: {ManagedSizeFormatted}, GPU: {GpuSizeFormatted})";
        }
    }
}

