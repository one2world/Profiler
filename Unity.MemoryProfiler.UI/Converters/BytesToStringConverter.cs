using System;
using System.Globalization;
using System.Windows.Data;

namespace Unity.MemoryProfiler.UI.Converters
{
    /// <summary>
    /// 将字节数转换为可读的字符串
    /// 注意：此转换器已过时，仅用于编译旧的AllTrackedMemoryComparisonView.xaml
    /// 该文件将在阶段8删除
    /// </summary>
    [Obsolete("此转换器已过时，将在阶段8删除")]
    public class BytesToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ulong bytes)
                return FormatBytes((long)bytes);
            if (value is long longBytes)
                return FormatBytes(longBytes);
            if (value is int intBytes)
                return FormatBytes(intBytes);
            
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0)
                return $"-{FormatBytes(-bytes)}";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F2} {sizes[order]}";
        }
    }
}

