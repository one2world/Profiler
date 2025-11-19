using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Views
{
    /// <summary>
    /// 将SnapshotIssuesModel.IssueLevel转换为WPF Brush（颜色）
    /// 用于在UI中显示不同级别的问题时使用不同颜色
    /// </summary>
    public class IssueLevelToBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length > 0 && values[0] is SnapshotIssuesModel.IssueLevel level)
            {
                return level switch
                {
                    SnapshotIssuesModel.IssueLevel.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    SnapshotIssuesModel.IssueLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                    SnapshotIssuesModel.IssueLevel.Info => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            
            return new SolidColorBrush(Colors.Gray);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

