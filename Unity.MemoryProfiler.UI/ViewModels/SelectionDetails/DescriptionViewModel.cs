namespace Unity.MemoryProfiler.UI.ViewModels.SelectionDetails
{
    /// <summary>
    /// Description Section ViewModel
    /// 显示描述文本
    /// </summary>
    public class DescriptionViewModel : SectionViewModel
    {
        private string _text = string.Empty;

        /// <summary>
        /// 描述文本
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (SetProperty(ref _text, value))
                {
                    // 自动显示/隐藏
                    if (string.IsNullOrEmpty(value))
                        Hide();
                    else
                        Show();
                }
            }
        }

        public override void Clear()
        {
            Text = string.Empty;
            Hide();
        }
    }
}

