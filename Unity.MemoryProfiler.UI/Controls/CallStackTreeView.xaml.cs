using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using DevExpress.Xpf.Grid;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// CallStack TreeView 控件
    /// 用于在 SelectionDetailsPanel 中显示 Managed Objects 的分配堆栈
    /// </summary>
    public partial class CallStackTreeView : UserControl
    {
        /// <summary>
        /// 源码目录列表（用于代码跳转）
        /// </summary>
        private List<string>? _sourceDirectories;

        public CallStackTreeView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置数据源
        /// </summary>
        public void SetData(List<CallStackNode> nodes, List<string>? sourceDirectories = null)
        {
            _sourceDirectories = sourceDirectories;
            TreeList.ItemsSource = nodes;
            
            // 默认展开第一层
            if (nodes != null && nodes.Count > 0)
            {
                TreeList.View.ExpandNode(0);
            }
        }

        /// <summary>
        /// 清空数据
        /// </summary>
        public void ClearData()
        {
            TreeList.ItemsSource = null;
            _sourceDirectories = null;
        }

        /// <summary>
        /// 双击行时尝试跳转到源码
        /// </summary>
        private void OnRowDoubleClick(object sender, RowDoubleClickEventArgs e)
        {
            if (e.HitInfo.RowHandle < 0)
                return;

            var node = TreeList.GetRow(e.HitInfo.RowHandle) as CallStackNode;
            if (node == null || string.IsNullOrEmpty(node.FilePath))
                return;

            TryNavigateToSourceCode(node.FilePath, node.LineNumber);
        }

        /// <summary>
        /// 尝试跳转到源码
        /// 参考: ManagedObjectsViewModel.NavigateToSourceCode
        /// </summary>
        private void TryNavigateToSourceCode(string filePath, int lineNumber)
        {
            if (_sourceDirectories == null || _sourceDirectories.Count == 0)
                return;

            // 尝试在源码目录中查找文件
            foreach (var sourceDir in _sourceDirectories)
            {
                var fullPath = System.IO.Path.Combine(sourceDir, filePath);
                if (System.IO.File.Exists(fullPath))
                {
                    // 使用 VS Code 打开文件并跳转到指定行
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "code",
                            Arguments = $"--goto \"{fullPath}:{lineNumber}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        var process = Process.Start(startInfo);
                        if (process != null)
                        {
                            return;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to start VS Code process for {fullPath}:{lineNumber}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to open VS Code: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// CallStack 节点数据模型
    /// </summary>
    public class CallStackNode
    {
        /// <summary>
        /// 描述（函数名）
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 文件路径:行号
        /// </summary>
        public string FileLine { get; set; } = string.Empty;

        /// <summary>
        /// 文件路径（用于跳转）
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 行号（用于跳转）
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// 子节点（用于 TreeList）
        /// </summary>
        public List<CallStackNode>? Children { get; set; }
    }
}

