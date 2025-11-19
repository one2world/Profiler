using System;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 动态元素选项
    /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDynamicElementOptions
    /// </summary>
    [Flags]
    public enum DynamicElementOptions
    {
        /// <summary>
        /// 无特殊选项
        /// </summary>
        None = 0,

        /// <summary>
        /// 将元素放置在组的最前面
        /// </summary>
        PlaceFirstInGroup = 1 << 0,

        /// <summary>
        /// 标签可选择（用户可以选中文本并复制）
        /// </summary>
        SelectableLabel = 1 << 1,

        /// <summary>
        /// 显示标题（某些元素可能不需要标题）
        /// </summary>
        ShowTitle = 1 << 2,

        /// <summary>
        /// 启用富文本（支持超链接等）
        /// </summary>
        EnableRichText = 1 << 3,

        /// <summary>
        /// 元素类型：按钮
        /// </summary>
        Button = 1 << 5,

        /// <summary>
        /// 元素类型：切换开关
        /// </summary>
        Toggle = 1 << 6,

        /// <summary>
        /// Toggle初始状态为开启
        /// </summary>
        ToggleOn = 1 << 7,

        /// <summary>
        /// 元素类型：子折叠面板（可嵌套的Expander）
        /// </summary>
        SubFoldout = 1 << 8,
    }

    /// <summary>
    /// DynamicElementOptions扩展方法
    /// </summary>
    public static class DynamicElementOptionsExtensions
    {
        /// <summary>
        /// 检查是否包含指定标志
        /// </summary>
        public static bool HasFlag(this DynamicElementOptions options, DynamicElementOptions flag)
        {
            return (options & flag) == flag;
        }

        /// <summary>
        /// 添加标志
        /// </summary>
        public static DynamicElementOptions AddFlag(this DynamicElementOptions options, DynamicElementOptions flag)
        {
            return options | flag;
        }

        /// <summary>
        /// 移除标志
        /// </summary>
        public static DynamicElementOptions RemoveFlag(this DynamicElementOptions options, DynamicElementOptions flag)
        {
            return options & ~flag;
        }
    }
}

