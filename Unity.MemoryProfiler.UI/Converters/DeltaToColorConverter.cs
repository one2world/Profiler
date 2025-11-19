using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Unity.MemoryProfiler.UI.Converters
{
    /// <summary>
    /// 将Delta值转换为颜色
    /// 正值（增长）：绿色
    /// 负值（减少）：红色
    /// 零值：灰色
    /// </summary>
    public class DeltaToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long deltaLong)
            {
                if (deltaLong > 0)
                    return Brushes.Green;
                else if (deltaLong < 0)
                    return Brushes.Red;
                else
                    return Brushes.Gray;
            }

            if (value is int deltaInt)
            {
                if (deltaInt > 0)
                    return Brushes.Green;
                else if (deltaInt < 0)
                    return Brushes.Red;
                else
                    return Brushes.Gray;
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

