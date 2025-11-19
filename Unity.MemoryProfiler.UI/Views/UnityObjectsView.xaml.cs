using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.Grid;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.ViewModels;

namespace Unity.MemoryProfiler.UI.Views
{
    public partial class UnityObjectsView : UserControl
    {
        public UnityObjectsView()
        {
            InitializeComponent();
            // SelectionDetails现在由MainWindow管理
        }

        private void OnFocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            var view = sender as TreeListView;
            if (view?.FocusedRowData?.Row is UnityObjectTreeNode selectedNode)
            {
                if (DataContext is UnityObjectsViewModel viewModel)
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

        // Base 和 Compared 表使用双向绑定，不需要事件处理器
        // 互斥逻辑在 ViewModel 中实现（SelectedBaseNode/SelectedComparedNode 的 setter）
        
        // UpdateDetailsPanelVisibility方法已删除
        // SelectionDetails现在由MainWindow统一管理
    }
}
