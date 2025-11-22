using System.Windows;

namespace Unity.MemoryProfiler.UI.ViewModels.SelectionDetails
{
    /// <summary>
    /// Section ViewModel 基类
    /// 所有 Section (Basic, Memory, Description 等) 的基类
    /// </summary>
    public abstract class SectionViewModel : ViewModelBase
    {
        private Visibility _visibility = Visibility.Collapsed;

        /// <summary>
        /// Section 的可见性
        /// </summary>
        public Visibility Visibility
        {
            get => _visibility;
            set => SetProperty(ref _visibility, value);
        }

        /// <summary>
        /// 显示 Section
        /// </summary>
        public void Show()
        {
            Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 隐藏 Section
        /// </summary>
        public void Hide()
        {
            Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 清空 Section 的内容
        /// </summary>
        public abstract void Clear();
    }
}

