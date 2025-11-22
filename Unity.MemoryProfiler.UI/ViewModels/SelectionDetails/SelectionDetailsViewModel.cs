using System.Windows;

namespace Unity.MemoryProfiler.UI.ViewModels.SelectionDetails
{
    /// <summary>
    /// Selection Details 根 ViewModel
    /// 管理所有 Section ViewModels
    /// </summary>
    public class SelectionDetailsViewModel : ViewModelBase
    {
        private string _title = "No Selection";
        private Visibility _noSelectionMessageVisibility = Visibility.Visible;
        private Visibility _detailsContentVisibility = Visibility.Collapsed;

        /// <summary>
        /// 标题
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// "No Selection" 消息的可见性
        /// </summary>
        public Visibility NoSelectionMessageVisibility
        {
            get => _noSelectionMessageVisibility;
            set => SetProperty(ref _noSelectionMessageVisibility, value);
        }

        /// <summary>
        /// 详情内容的可见性
        /// </summary>
        public Visibility DetailsContentVisibility
        {
            get => _detailsContentVisibility;
            set => SetProperty(ref _detailsContentVisibility, value);
        }

        // Section ViewModels
        public BasicInfoViewModel BasicInfo { get; } = new BasicInfoViewModel();
        public MemoryInfoViewModel MemoryInfo { get; } = new MemoryInfoViewModel();
        public DescriptionViewModel Description { get; } = new DescriptionViewModel();
        public MetaDataViewModel MetaData { get; } = new MetaDataViewModel();
        public HelpViewModel Help { get; } = new HelpViewModel();
        public AdvancedInfoViewModel AdvancedInfo { get; } = new AdvancedInfoViewModel();
        public CallStacksViewModel CallStacks { get; } = new CallStacksViewModel();
        
        // TODO: ManagedFieldsViewModel 和 ReferencesViewModel 需要特殊处理

        /// <summary>
        /// 清空所有内容
        /// </summary>
        public void Clear()
        {
            Title = "No Selection";
            NoSelectionMessageVisibility = Visibility.Visible;
            DetailsContentVisibility = Visibility.Collapsed;

            BasicInfo.Clear();
            MemoryInfo.Clear();
            Description.Clear();
            MetaData.Clear();
            Help.Clear();
            AdvancedInfo.Clear();
            CallStacks.Clear();
        }

        /// <summary>
        /// 显示详情内容（隐藏 "No Selection" 消息）
        /// </summary>
        public void ShowDetails()
        {
            NoSelectionMessageVisibility = Visibility.Collapsed;
            DetailsContentVisibility = Visibility.Visible;
        }
    }
}

