using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.ViewModels;

namespace Unity.MemoryProfiler.UI.Views
{
    /// <summary>
    /// 快照管理面板
    /// 基于Unity官方的SnapshotFilesListViewController
    /// </summary>
    public partial class SnapshotManagementPanel : UserControl
    {
        public SnapshotManagementPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 点击快照加载
        /// </summary>
        private void Snapshot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not SnapshotManagementViewModel viewModel)
                return;

            var border = sender as FrameworkElement;
            if (border?.DataContext is not SnapshotFileModel snapshot)
                return;

            // 如果按住Ctrl，则进入对比模式
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                viewModel.CompareSnapshotsCommand.Execute(snapshot);
            }
            else
            {
                viewModel.LoadSnapshotCommand.Execute(snapshot);
            }
            
            e.Handled = true;
        }
    }
}

