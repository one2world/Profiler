using System.Collections.ObjectModel;

namespace Unity.MemoryProfiler.UI.ViewModels.SelectionDetails
{
    /// <summary>
    /// Advanced Information Section ViewModel
    /// 显示高级信息（如 Address, Instance ID, Flags 等）
    /// </summary>
    public class AdvancedInfoViewModel : SectionViewModel
    {
        /// <summary>
        /// 属性列表
        /// </summary>
        public ObservableCollection<PropertyItem> Properties { get; } = new ObservableCollection<PropertyItem>();

        public override void Clear()
        {
            Properties.Clear();
            Hide();
        }

        /// <summary>
        /// 添加属性
        /// </summary>
        public void AddProperty(string label, string value, string? tooltip = null)
        {
            Properties.Add(new PropertyItem
            {
                Label = label,
                Value = value,
                Tooltip = tooltip
            });
            Show();
        }
    }
}

