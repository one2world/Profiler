using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// 富文本块控件 - 支持超链接和格式化文本
    /// 参考: Unity的CustomContentText and RichText functionality
    /// </summary>
    public partial class RichTextBlock : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(RichTextBlock),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public event EventHandler<string>? LinkClicked;

        public RichTextBlock()
        {
            InitializeComponent();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextBlock richTextBlock)
            {
                richTextBlock.UpdateContent();
            }
        }

        private void UpdateContent()
        {
            ContentTextBlock.Inlines.Clear();

            if (string.IsNullOrEmpty(Text))
                return;

            // 解析文本并创建Inlines
            // 支持简单的Markdown风格链接: [text](url)
            var text = Text;
            var startIndex = 0;

            while (startIndex < text.Length)
            {
                // 查找链接标记 [
                var linkStart = text.IndexOf('[', startIndex);
                if (linkStart < 0)
                {
                    // 没有更多链接，添加剩余文本
                    AddNormalText(text.Substring(startIndex));
                    break;
                }

                // 添加链接前的普通文本
                if (linkStart > startIndex)
                {
                    AddNormalText(text.Substring(startIndex, linkStart - startIndex));
                }

                // 查找链接文本结束 ]
                var linkTextEnd = text.IndexOf(']', linkStart + 1);
                if (linkTextEnd < 0)
                {
                    // 没有找到结束标记，添加剩余文本
                    AddNormalText(text.Substring(linkStart));
                    break;
                }

                // 查找链接URL开始 (
                if (linkTextEnd + 1 < text.Length && text[linkTextEnd + 1] == '(')
                {
                    // 查找链接URL结束 )
                    var linkUrlEnd = text.IndexOf(')', linkTextEnd + 2);
                    if (linkUrlEnd < 0)
                    {
                        // 没有找到结束标记，添加剩余文本
                        AddNormalText(text.Substring(linkStart));
                        break;
                    }

                    // 提取链接文本和URL
                    var linkText = text.Substring(linkStart + 1, linkTextEnd - linkStart - 1);
                    var linkUrl = text.Substring(linkTextEnd + 2, linkUrlEnd - linkTextEnd - 2);

                    // 添加超链接
                    AddHyperlink(linkText, linkUrl);

                    startIndex = linkUrlEnd + 1;
                }
                else
                {
                    // 不是完整的链接格式，添加为普通文本
                    AddNormalText(text.Substring(linkStart, linkTextEnd - linkStart + 1));
                    startIndex = linkTextEnd + 1;
                }
            }
        }

        private void AddNormalText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var run = new Run(text);
                ContentTextBlock.Inlines.Add(run);
            }
        }

        private void AddHyperlink(string text, string url)
        {
            var hyperlink = new Hyperlink(new Run(text))
            {
                NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute),
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Material Blue
                TextDecorations = null // 去掉下划线，鼠标悬停时显示
            };

            hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
            hyperlink.MouseEnter += (s, e) =>
            {
                hyperlink.TextDecorations = TextDecorations.Underline;
            };
            hyperlink.MouseLeave += (s, e) =>
            {
                hyperlink.TextDecorations = null;
            };

            ContentTextBlock.Inlines.Add(hyperlink);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var url = e.Uri.ToString();

            // 触发LinkClicked事件，让调用者处理链接导航
            LinkClicked?.Invoke(this, url);

            // 对于http/https链接，使用默认浏览器打开
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to open URL: {url}, Error: {ex.Message}");
                }
            }

            e.Handled = true;
        }

        /// <summary>
        /// 设置纯文本内容（自动换行）
        /// </summary>
        public void SetPlainText(string text)
        {
            ContentTextBlock.Inlines.Clear();
            if (!string.IsNullOrEmpty(text))
            {
                ContentTextBlock.Inlines.Add(new Run(text));
            }
        }

        /// <summary>
        /// 添加加粗文本
        /// </summary>
        public void AddBoldText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var run = new Run(text) { FontWeight = FontWeights.Bold };
                ContentTextBlock.Inlines.Add(run);
            }
        }

        /// <summary>
        /// 添加斜体文本
        /// </summary>
        public void AddItalicText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var run = new Run(text) { FontStyle = FontStyles.Italic };
                ContentTextBlock.Inlines.Add(run);
            }
        }

        /// <summary>
        /// 添加彩色文本
        /// </summary>
        public void AddColoredText(string text, Color color)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var run = new Run(text) { Foreground = new SolidColorBrush(color) };
                ContentTextBlock.Inlines.Add(run);
            }
        }

        /// <summary>
        /// 清空内容
        /// </summary>
        public void Clear()
        {
            ContentTextBlock.Inlines.Clear();
        }
    }
}

