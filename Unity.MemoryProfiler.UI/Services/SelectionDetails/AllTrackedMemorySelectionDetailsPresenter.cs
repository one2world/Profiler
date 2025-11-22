using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Services;

namespace Unity.MemoryProfiler.UI.Services.SelectionDetails
{
    internal sealed class AllTrackedMemorySelectionDetailsPresenter : ISelectionDetailsPresenter
    {
        public bool CanPresent(in SelectionDetailsContext context)
        {
            return context.Origin == SelectionDetailsSource.AllTrackedMemory
                    && context.Node is AllTrackedMemoryTreeNode;
        }

        public void Present(in SelectionDetailsContext context)
        {
            var panel = context.View;
            var node = (AllTrackedMemoryTreeNode)context.Node;

            var builder = panel.DetailsBuilder;
            if (node.Source.Valid && builder != null)
            {
                panel.ClearSelection();
                var description = CategoryDescriptions.GetDescription(node.Category);
                builder.SetSelection(node.Source, node.Name, description, node.ChildCount);
                panel.SetupReferences(node.Source);
            }
            else
            {
                // 回退：显示节点的基本信息（当 Builder 不可用或 Source 无效时）
                ShowFallbackDetails(panel, node);
                panel.HideReferences();
            }
        }

        /// <summary>
        /// 显示回退详情（当 Builder 不可用时）
        /// 参考原始实现：SelectionDetailsPanel.ShowAllTrackedMemoryDetails
        /// </summary>
        private void ShowFallbackDetails(SelectionDetailsPanel panel, AllTrackedMemoryTreeNode node)
        {
            var adapter = panel.Adapter;
            
            adapter.ClearAllGroups();
            adapter.SetItemName(node.Name);

            // 基本信息
            adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Name", node.Name);

            if (node.Category != CategoryType.None)
            {
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Category", node.Category.ToString());
            }

            if (node.IsGroupNode)
            {
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Type", "Group Node");
            }

            // 内存信息
            if (node.AllocatedSize > 0)
            {
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMemory, "Allocated Size", 
                    UnityEditor.EditorUtility.FormatBytes((long)node.AllocatedSize),
                    $"{node.AllocatedSize:N0} B");
            }

            if (node.ResidentSize > 0)
            {
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMemory, "Resident Size", 
                    UnityEditor.EditorUtility.FormatBytes((long)node.ResidentSize),
                    $"{node.ResidentSize:N0} B");
            }

            if (node.ChildCount > 0)
            {
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Child Count", node.ChildCount.ToString());
            }
        }
    }
}
