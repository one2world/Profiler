using System;
using System.Collections.Generic;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// AllTrackedMemoryModel构建参数
    /// 对应Unity的AllTrackedMemoryModelBuilder.BuildArgs
    /// </summary>
    internal readonly struct AllTrackedMemoryBuildArgs
    {
        public AllTrackedMemoryBuildArgs(
            string pathFilter = null,
            List<string> itemPathFilter = null,
            bool excludeNative = false,
            bool excludeManaged = false,
            bool excludeGraphics = false,
            bool excludeAll = false,
            bool breakdownNativeReserved = false,
            bool breakdownGraphicsResources = true,
            ManagedGroupingMode managedGrouping = ManagedGroupingMode.ByType,
            Action<SourceIndex> selectionProcessor = null)
        {
            PathFilter = pathFilter;
            ItemPathFilter = itemPathFilter;
            ExcludeNative = excludeNative;
            ExcludeManaged = excludeManaged;
            ExcludeGraphics = excludeGraphics;
            ExcludeAll = excludeAll;
            BreakdownNativeReserved = breakdownNativeReserved;
            BreakdownGraphicsResources = breakdownGraphicsResources;
            ManagedGrouping = managedGrouping;
            SelectionProcessor = selectionProcessor;
        }

        /// <summary>
        /// 路径过滤器（用于只显示特定路径下的项）
        /// 例如："/Native/Unity Subsystems/Renderer"
        /// 已废弃，请使用 ItemPathFilter
        /// </summary>
        public string PathFilter { get; }

        /// <summary>
        /// 项路径过滤器（用于只显示特定路径下的项）
        /// 等价于 Unity 的 IEnumerable&lt;ITextFilter&gt; PathFilter
        /// 例如：["Native", "Unity Subsystems", "Renderer"]
        /// 每个字符串对应树的一层，逐层匹配
        /// </summary>
        public List<string> ItemPathFilter { get; }

        /// <summary>
        /// 排除Native分组
        /// </summary>
        public bool ExcludeNative { get; }

        /// <summary>
        /// 排除Managed分组
        /// </summary>
        public bool ExcludeManaged { get; }

        /// <summary>
        /// 排除Graphics分组
        /// </summary>
        public bool ExcludeGraphics { get; }

        /// <summary>
        /// 是否展开Native保留内存的细节
        /// </summary>
        public bool BreakdownNativeReserved { get; }

        /// <summary>
        /// 是否展开Graphics资源的细节
        /// </summary>
        public bool BreakdownGraphicsResources { get; }

        /// <summary>
        /// Managed对象分组模式
        /// </summary>
        public ManagedGroupingMode ManagedGrouping { get; }

        /// <summary>
        /// 选择处理回调（当用户选择某项时调用）
        /// </summary>
        public Action<SourceIndex> SelectionProcessor { get; }

        /// <summary>
        /// 是否排除所有分组（用于空视图）
        /// Unity 逻辑：在 Compare 模式下选中分组节点时，显示空表
        /// </summary>
        public bool ExcludeAll { get; }

        /// <summary>
        /// 默认构建参数（显示所有内容）
        /// </summary>
        public static AllTrackedMemoryBuildArgs Default => new AllTrackedMemoryBuildArgs();
    }

    /// <summary>
    /// Managed对象分组模式
    /// </summary>
    public enum ManagedGroupingMode
    {
        /// <summary>
        /// 按类型分组（默认）
        /// 例如：Managed/UnityEngine.Texture2D/[具体对象]
        /// </summary>
        ByType,

        /// <summary>
        /// 扁平列表（不分组）
        /// 例如：Managed/[所有对象]
        /// </summary>
        Flat,

        /// <summary>
        /// 按命名空间分组
        /// 例如：Managed/UnityEngine/Texture2D/[具体对象]
        /// </summary>
        ByNamespace,

        /// <summary>
        /// 按类型和Native对象名称分组（用于区分同类型的Unity对象）
        /// 例如：Managed/UnityEngine.GameObject/Player/[具体对象]
        /// </summary>
        ByTypeAndNativeName
    }

    /// <summary>
    /// Summary构建参数
    /// </summary>
    public readonly struct SummaryBuildArgs
    {
        public SummaryBuildArgs(
            bool includeGraphics = true,
            bool includeExecutables = true,
            bool includeUntracked = true)
        {
            IncludeGraphics = includeGraphics;
            IncludeExecutables = includeExecutables;
            IncludeUntracked = includeUntracked;
        }

        /// <summary>
        /// 包含Graphics内存
        /// </summary>
        public bool IncludeGraphics { get; }

        /// <summary>
        /// 包含可执行文件内存
        /// </summary>
        public bool IncludeExecutables { get; }

        /// <summary>
        /// 包含未追踪内存
        /// </summary>
        public bool IncludeUntracked { get; }

        public static SummaryBuildArgs Default => new SummaryBuildArgs();
    }

    /// <summary>
    /// UnityObjects构建参数
    /// </summary>
    public readonly struct UnityObjectsBuildArgs
    {
        public UnityObjectsBuildArgs(
            UnityObjectGroupingMode grouping = UnityObjectGroupingMode.ByType,
            bool showLeakedShellsOnly = false,
            bool showEmptyShellsOnly = false,
            string typeFilter = null)
        {
            Grouping = grouping;
            ShowLeakedShellsOnly = showLeakedShellsOnly;
            ShowEmptyShellsOnly = showEmptyShellsOnly;
            TypeFilter = typeFilter;
        }

        /// <summary>
        /// 分组模式
        /// </summary>
        public UnityObjectGroupingMode Grouping { get; }

        /// <summary>
        /// 只显示泄漏的Managed Shell
        /// </summary>
        public bool ShowLeakedShellsOnly { get; }

        /// <summary>
        /// 只显示空的Managed Shell
        /// </summary>
        public bool ShowEmptyShellsOnly { get; }

        /// <summary>
        /// 类型过滤（例如："Texture2D"）
        /// </summary>
        public string TypeFilter { get; }

        public static UnityObjectsBuildArgs Default => new UnityObjectsBuildArgs();
    }

    /// <summary>
    /// Unity对象分组模式
    /// </summary>
    public enum UnityObjectGroupingMode
    {
        /// <summary>
        /// 按类型分组
        /// </summary>
        ByType,

        /// <summary>
        /// 按场景分组
        /// </summary>
        ByScene,

        /// <summary>
        /// 扁平列表
        /// </summary>
        Flat
    }
}

