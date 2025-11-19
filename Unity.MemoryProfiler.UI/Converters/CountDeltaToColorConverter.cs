using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Unity.MemoryProfiler.UI.Converters
{
    /// <summary>
    /// Count Delta转换为颜色：正数=红色，负数=绿色，零=灰色
    /// </summary>
    public class CountDeltaToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length > 0 && values[0] is int delta)
            {
                if (delta > 0)
                    return new SolidColorBrush(Color.FromRgb(211, 47, 47)); // 红色 #D32F2F (增加)
                if (delta < 0)
                    return new SolidColorBrush(Color.FromRgb(56, 142, 60)); // 绿色 #388E3C (减少)
                return new SolidColorBrush(Color.FromRgb(117, 117, 117)); // 灰色 #757575 (无变化)
            }
            return new SolidColorBrush(Color.FromRgb(117, 117, 117)); // 默认灰色
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

