using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.Grid;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.ViewModels;

namespace Unity.MemoryProfiler.UI.Views
{
    public partial class AllTrackedMemoryView : UserControl
    {
        public AllTrackedMemoryView()
        {
            InitializeComponent();
            // SelectionDetails现在由MainWindow管理
        }

        private void OnFocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            var view = sender as TreeListView;
            if (view?.FocusedRowData?.Row is AllTrackedMemoryTreeNode selectedNode)
            {
                if (DataContext is AllTrackedMemoryViewModel viewModel)
                {
                    viewModel.SelectedNode = selectedNode;
                }

                // SelectionDetails 由 MainWindow 统一管理，通过 ViewModel 的 PropertyChanged 事件自动更新
            }
            else
            {
                // SelectionDetails 由 MainWindow 统一管理，通过 ViewModel 的 PropertyChanged 事件自动更新
            }
        }

        // UpdateDetailsPanelVisibility方法已删除
        // SelectionDetails现在由MainWindow管理
    }
}

