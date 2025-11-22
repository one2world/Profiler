using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services.SelectionDetails
{
    internal sealed class UnityObjectsSelectionDetailsPresenter : ISelectionDetailsPresenter
    {
        public bool CanPresent(in SelectionDetailsContext context)
        {
            return context.Origin == SelectionDetailsSource.UnityObjects
                && context.Node is UnityObjectTreeNode unityObjectNode
                && unityObjectNode.Source.Valid;
        }

        public void Present(in SelectionDetailsContext context)
        {
            var panel = context.View;
            var node = (UnityObjectTreeNode)context.Node;

            var builder = panel.DetailsBuilder;
            if (node.Source.Valid && builder != null)
            {
                panel.ClearSelection();
                builder.SetSelection(node.Source);
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
        /// 参考原始实现：SelectionDetailsPanel.ShowUnityObjectDetails
        /// </summary>
        private void ShowFallbackDetails(SelectionDetailsPanel panel, UnityObjectTreeNode node)
        {
            var adapter = panel.Adapter;
            
            adapter.ClearAllGroups();
            adapter.SetItemName(node.Name);

            // 基本信息
            adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Name", node.Name);

            if (node.IsGroupNode)
            {
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Type", "Group Node");
                if (node.ChildCount > 0)
                {
                    adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Child Count", 
                        node.ChildCount.ToString(),
                        $"This group contains {node.ChildCount} child items");
                }
            }

            if (node.InstanceId != 0)
            {
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Instance ID", 
                    node.InstanceId.ToString(),
                    "Unity Internal Instance ID");
            }

            // 内存信息 - 参考Unity的层级显示格式
            var hasMultipleSizes = (node.NativeSize > 0 ? 1 : 0) +
                                  (node.ManagedSize > 0 ? 1 : 0) +
                                  (node.GpuSize > 0 ? 1 : 0) > 1;

            if (hasMultipleSizes)
            {
                var totalSize = node.NativeSize + node.ManagedSize + node.GpuSize;
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMemory, "Total Size", 
                    UnityEditor.EditorUtility.FormatBytes((long)totalSize),
                    $"{totalSize:N0} B");
            }

            if (node.NativeSize > 0)
            {
                var label = hasMultipleSizes ? "├ Native Size" : "Native Size";
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMemory, label, 
                    UnityEditor.EditorUtility.FormatBytes((long)node.NativeSize),
                    $"{node.NativeSize:N0} B");
            }

            if (node.ManagedSize > 0)
            {
                var label = hasMultipleSizes ? 
                    (node.GpuSize > 0 ? "├ Managed Size" : "└ Managed Size") : 
                    "Managed Size";
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMemory, label, 
                    UnityEditor.EditorUtility.FormatBytes((long)node.ManagedSize),
                    $"{node.ManagedSize:N0} B");
            }

            if (node.GpuSize > 0)
            {
                var label = hasMultipleSizes ? "└ GPU Size" : "GPU Size";
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMemory, label, 
                    UnityEditor.EditorUtility.FormatBytes((long)node.GpuSize),
                    $"{node.GpuSize:N0} B");
            }
        }
    }
}
