using System.Windows.Controls;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 详情面板中的一个分组
    /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.DetailsGroup
    /// </summary>
    public class DetailsGroup
    {
        /// <summary>
        /// 分组名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 根容器（Expander）
        /// </summary>
        public Expander Expander { get; set; } = null!;

        /// <summary>
        /// 内容容器（StackPanel）
        /// </summary>
        public StackPanel Content { get; set; } = null!;

        /// <summary>
        /// 分组是否展开
        /// </summary>
        public bool IsExpanded
        {
            get => Expander?.IsExpanded ?? false;
            set
            {
                if (Expander != null)
                    Expander.IsExpanded = value;
            }
        }

        /// <summary>
        /// 根元素（Expander本身）
        /// </summary>
        public Expander Root => Expander;

        /// <summary>
        /// 清空分组内容（保持展开状态）
        /// </summary>
        public void Clear()
        {
            var expandedState = IsExpanded;
            Content?.Children.Clear();
            IsExpanded = expandedState;
        }

        /// <summary>
        /// 显示分组
        /// </summary>
        public void Show()
        {
            if (Expander != null)
                Expander.Visibility = System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// 隐藏分组
        /// </summary>
        public void Hide()
        {
            if (Expander != null)
                Expander.Visibility = System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// 分组是否可见
        /// </summary>
        public bool IsVisible
        {
            get => Expander?.Visibility == System.Windows.Visibility.Visible;
            set
            {
                if (value)
                    Show();
                else
                    Hide();
            }
        }
    }
}

