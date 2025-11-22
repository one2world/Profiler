using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.Editor;
using UnityEditor;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Services.SelectionDetails
{
    /// <summary>
    /// Managed Objects 选择详情展示器
    /// 处理 ManagedObjectDetailNode 的详情显示
    /// </summary>
    internal sealed class ManagedObjectsSelectionDetailsPresenter : ISelectionDetailsPresenter
    {
        public bool CanPresent(in SelectionDetailsContext context)
        {
            return context.Origin == SelectionDetailsSource.ManagedObjects
                    && context.Node is ManagedObjectDetailNode;
        }

        public void Present(in SelectionDetailsContext context)
        {
            var panel = context.View;
            var node = (ManagedObjectDetailNode)context.Node;
            var snapshot = context.Snapshot;

            // 如果是 Group 节点，显示类型信息
            if (node.IsGroup)
            {
                PresentGroupNode(panel, node);
                return;
            }

            // 如果是具体对象节点，使用 SelectedItemDetailsBuilder
            if (node.ManagedObjectIndex >= 0)
            {
                PresentManagedObject(panel, node, snapshot);
                return;
            }

            // 回退：显示基本信息
            PresentBasicInfo(panel, node);
        }

        /// <summary>
        /// 显示 Group 节点（类型分组）
        /// </summary>
        private void PresentGroupNode(SelectionDetailsPanel panel, ManagedObjectDetailNode node)
        {
            var adapter = panel.Adapter;
            
            adapter.ClearAllGroups();
            adapter.SetItemName($"Managed Type: {node.Name}");

            // 基本信息
            adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Type Name", node.Name);
            adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Object Count", node.Count.ToString());

            // 内存信息
            adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMemory, "Total Size", 
                EditorUtility.FormatBytes((long)node.Size),
                $"{node.Size:N0} B\n\nTotal size of all objects of this type");
            
            panel.HideReferences();
        }

        /// <summary>
        /// 显示 Managed Object（使用 SelectedItemDetailsBuilder）
        /// </summary>
        private void PresentManagedObject(SelectionDetailsPanel panel, ManagedObjectDetailNode node, CachedSnapshot snapshot)
        {
            var builder = panel.DetailsBuilder;
            if (builder != null)
            {
                panel.ClearSelection();
                
                // 创建 SourceIndex 指向 Managed Object
                var source = new SourceIndex(SourceIndex.SourceId.ManagedObject, node.ManagedObjectIndex);
                
                builder.SetSelection(source, node.Name, "Managed Object");
                panel.SetupReferences(source);
            }
            else
            {
                // 如果没有 Builder，显示基本信息
                PresentBasicInfo(panel, node);
            }
        }

        /// <summary>
        /// 显示基本信息（回退方案）
        /// </summary>
        private void PresentBasicInfo(SelectionDetailsPanel panel, ManagedObjectDetailNode node)
        {
            var adapter = panel.Adapter;
            
            adapter.ClearAllGroups();
            adapter.SetItemName($"Managed Object: {node.Name}");
            
            // 基本信息
            adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Name", node.Name);
            
            // 内存信息
            adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMemory, "Size", 
                EditorUtility.FormatBytes((long)node.Size),
                $"{node.Size:N0} B");
            
            // 高级信息
            if (node.Address > 0)
            {
                adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Address", $"0x{node.Address:X}");
            }
            
            panel.HideReferences();
        }
    }
}
