using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// 信息框控件 - 显示不同级别的提示、警告、错误信息
    /// 参考: Unity.MemoryProfiler.Editor.UI.InfoBox
    /// </summary>
    public partial class InfoBox : UserControl
    {
        /// <summary>
        /// 问题级别枚举
        /// 参考: Unity的IssueLevel
        /// </summary>
        public enum IssueLevel
        {
            Info,
            Warning,
            Error
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(InfoBox),
                new PropertyMetadata(string.Empty, OnMessageChanged));

        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(Level), typeof(IssueLevel), typeof(InfoBox),
                new PropertyMetadata(IssueLevel.Info, OnLevelChanged));

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public IssueLevel Level
        {
            get => (IssueLevel)GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        public InfoBox()
        {
            InitializeComponent();
            UpdateAppearance();
        }

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBox infoBox)
            {
                infoBox.MessageText.Text = e.NewValue?.ToString() ?? string.Empty;
            }
        }

        private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBox infoBox)
            {
                infoBox.UpdateAppearance();
            }
        }

        private void UpdateAppearance()
        {
            switch (Level)
            {
                case IssueLevel.Info:
                    BorderElement.Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)); // #E3F2FD
                    BorderElement.BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // #2196F3
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(13, 71, 161)); // #0D47A1
                    // Info图标 (i)
                    IconPath.Data = Geometry.Parse("M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10,10-4.48,10-10S17.52,2,12,2Zm1,15h-2v-6h2v6Zm0-8h-2V7h2v2Z");
                    break;

                case IssueLevel.Warning:
                    BorderElement.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)); // #FFF3E0
                    BorderElement.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // #FF9800
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0)); // #E65100
                    // Warning图标 (!)
                    IconPath.Data = Geometry.Parse("M1,21h22L12,2L1,21z M13,18h-2v-2h2V18z M13,14h-2v-4h2V14z");
                    break;

                case IssueLevel.Error:
                    BorderElement.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)); // #FFEBEE
                    BorderElement.BorderBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // #F44336
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28)); // #B71C1C
                    // Error图标 (x)
                    IconPath.Data = Geometry.Parse("M12,2C6.47,2,2,6.47,2,12s4.47,10,10,10,10-4.47,10-10S17.53,2,12,2Zm5,13.59L15.59,17,12,13.41,8.41,17,7,15.59,10.59,12,7,8.41,8.41,7,12,10.59,15.59,7,17,8.41,13.41,12,17,15.59Z");
                    break;
            }
        }
    }
}

