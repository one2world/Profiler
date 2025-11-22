namespace Unity.MemoryProfiler.UI.ViewModels.SelectionDetails
{
    /// <summary>
    /// 属性项模型 - 用于显示 Label: Value 格式的属性
    /// </summary>
    public class PropertyItem : ViewModelBase
    {
        private string _label = string.Empty;
        private string _value = string.Empty;
        private string? _tooltip;

        /// <summary>
        /// 属性标签（如 "Managed Size"）
        /// </summary>
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        /// <summary>
        /// 属性值（如 "1.5 MB"）
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>
        /// 工具提示（可选）
        /// </summary>
        public string? Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }
    }
}

