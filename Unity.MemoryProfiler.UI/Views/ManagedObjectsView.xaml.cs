using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using DevExpress.Xpf.Grid;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Models.Comparison;
using Unity.MemoryProfiler.UI.ViewModels;

namespace Unity.MemoryProfiler.UI.Views
{
    public partial class ManagedObjectsView : UserControl
    {
        public ManagedObjectsView()
        {
            InitializeComponent();
        }

        private void OnCallStackFocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            var view = sender as TreeListView;
            if (view?.FocusedRowData?.Row is ManagedCallStackNode selectedNode)
            {
                if (DataContext is ManagedObjectsViewModel viewModel)
                {
                    viewModel.SelectedCallStackNode = selectedNode;
                }
            }
        }

        private void OnDetailFocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            var view = sender as TreeListView;
            if (view?.FocusedRowData?.Row is ManagedObjectDetailNode selectedNode)
            {
                if (DataContext is ManagedObjectsViewModel viewModel)
                {
                    viewModel.SelectedDetailNode = selectedNode;
                }
            }
        }

        private void OnAllocationSiteDoubleClick(object sender, RowDoubleClickEventArgs e)
        {
            if (e.HitInfo.RowHandle < 0)
                return;

            var view = sender as TableView;
            if (view?.Grid.GetRow(e.HitInfo.RowHandle) is AllocationSite site)
            {
                // 单快照模式，使用默认源码目录
                OpenInVSCode(site.FilePath, site.LineNumber, isBaseSnapshot: null);
            }
        }

        /// <summary>
        /// 对比主表选中事件
        /// </summary>
        private void OnComparisonFocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            var view = sender as TreeListView;
            if (view?.FocusedRowData?.Row is ComparisonTreeNode selectedNode)
            {
                if (DataContext is ManagedObjectsViewModel viewModel)
                {
                    viewModel.SelectedComparisonNode = selectedNode;
                }
            }
        }

        /// <summary>
        /// Base 子表选中事件
        /// </summary>
        private void OnBaseFocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            var view = sender as TreeListView;
            if (view?.FocusedRowData?.Row is ManagedObjectDetailNode selectedNode)
            {
                if (DataContext is ManagedObjectsViewModel viewModel)
                {
                    viewModel.SelectedBaseNode = selectedNode;
                }
            }
        }

        /// <summary>
        /// Compared 子表选中事件
        /// </summary>
        private void OnComparedFocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            var view = sender as TreeListView;
            if (view?.FocusedRowData?.Row is ManagedObjectDetailNode selectedNode)
            {
                if (DataContext is ManagedObjectsViewModel viewModel)
                {
                    viewModel.SelectedComparedNode = selectedNode;
                }
            }
        }

        /// <summary>
        /// Base AllocationSite 双击事件
        /// </summary>
        private void OnBaseAllocationSiteDoubleClick(object sender, RowDoubleClickEventArgs e)
        {
            if (e.HitInfo.RowHandle < 0)
                return;

            var view = sender as TableView;
            if (view?.Grid.GetRow(e.HitInfo.RowHandle) is AllocationSite site)
            {
                OpenInVSCode(site.FilePath, site.LineNumber, isBaseSnapshot: true);
            }
        }

        /// <summary>
        /// Compared AllocationSite 双击事件
        /// </summary>
        private void OnComparedAllocationSiteDoubleClick(object sender, RowDoubleClickEventArgs e)
        {
            if (e.HitInfo.RowHandle < 0)
                return;

            var view = sender as TableView;
            if (view?.Grid.GetRow(e.HitInfo.RowHandle) is AllocationSite site)
            {
                OpenInVSCode(site.FilePath, site.LineNumber, isBaseSnapshot: false);
            }
        }

        /// <summary>
        /// 使用 VS Code 打开文件并跳转到指定行
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="lineNumber">行号</param>
        /// <param name="isBaseSnapshot">是否为 Base 快照（null 表示单快照模式）</param>
        private void OpenInVSCode(string filePath, int lineNumber, bool? isBaseSnapshot = null)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            // 获取配置的源码目录列表
            // 对比模式下，可以配置两份源码目录（通过 key 区分）
            var sourceDirectories = isBaseSnapshot.HasValue
                ? (isBaseSnapshot.Value 
                    ? Services.ManagedObjectsConfigService.GetSourceDirectories("BaseSourceDirectories")
                    : Services.ManagedObjectsConfigService.GetSourceDirectories("ComparedSourceDirectories"))
                : Services.ManagedObjectsConfigService.GetSourceDirectories();

            // 尝试在配置的源码目录中查找文件
            string? foundPath = null;
            if (File.Exists(filePath))
            {
                foundPath = filePath;
            }
            else
            {
                // 提取文件名
                var fileName = Path.GetFileName(filePath);
                foreach (var dir in sourceDirectories)
                {
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            foundPath = files[0];
                            break;
                        }
                    }
                }
            }

            if (foundPath != null)
            {
                try
                {
                    // 使用 VS Code 的 goto 命令
                    var argument = lineNumber > 0 ? $"-g \"{foundPath}:{lineNumber}\"" : $"\"{foundPath}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "code",
                        Arguments = argument,
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"无法打开 VS Code。请确保 VS Code 已安装并添加到系统 PATH。\n\n错误: {ex.Message}",
                        "错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"无法找到文件: {filePath}\n\n请在配置中添加源码目录。",
                    "文件未找到",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
    }
}
