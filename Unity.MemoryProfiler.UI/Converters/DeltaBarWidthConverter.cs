using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Unity.MemoryProfiler.UI.Converters
{
    /// <summary>
    /// 将Delta值转换为进度条宽度
    /// </summary>
    public class DeltaBarWidthConverter : IValueConverter
    {
        private const double MaxBarWidth = 100.0; // 最大条形宽度（像素）

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long deltaLong)
            {
                // 获取DataContext以访问LargestAbsoluteSizeDelta
                // 简化实现：使用固定的最大值比例
                // 在实际应用中，应该从DataContext获取LargestAbsoluteSizeDelta

                // 暂时使用简单的比例计算
                var absoluteDelta = Math.Abs(deltaLong);
                
                // 假设10MB为最大值
                const long maxDelta = 10 * 1024 * 1024;
                var ratio = Math.Min(1.0, (double)absoluteDelta / maxDelta);
                
                return MaxBarWidth * ratio;
            }

            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

