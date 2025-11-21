using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Services;
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
            panel.ClearSelection();
            panel.TitleTextBlock.Text = $"Managed Type: {node.Name}";

            // 基本信息
            AddPropertyRow(panel.BasicInfoContent, "Type Name", node.Name);
            AddPropertyRow(panel.BasicInfoContent, "Object Count", node.Count.ToString());

            // 内存信息
            AddPropertyRow(panel.MemoryInfoContent, "Total Size", 
                EditorUtility.FormatBytes((long)node.Size),
                $"{node.Size:N0} B\n\nTotal size of all objects of this type");

            // 显示/隐藏Expander
            panel.BasicInfoExpander.Visibility = Visibility.Visible;
            panel.MemoryInfoExpander.Visibility = Visibility.Visible;
            panel.AdvancedInfoExpander.Visibility = Visibility.Collapsed;

            // 显示详情内容
            panel.NoSelectionMessage.Visibility = Visibility.Collapsed;
            panel.DetailsContent.Visibility = Visibility.Visible;
            
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
                return;
            }

            // 如果没有 Builder，显示基本信息
            PresentBasicInfo(panel, node);
        }

        /// <summary>
        /// 显示基本信息（回退方案）
        /// </summary>
        private void PresentBasicInfo(SelectionDetailsPanel panel, ManagedObjectDetailNode node)
        {
            panel.ClearSelection();
            panel.TitleTextBlock.Text = $"Managed Object: {node.Name}";
            
            AddPropertyRow(panel.BasicInfoContent, "Name", node.Name);
            AddPropertyRow(panel.MemoryInfoContent, "Size", 
                EditorUtility.FormatBytes((long)node.Size),
                $"{node.Size:N0} B");
            
            if (node.Address > 0)
            {
                AddPropertyRow(panel.AdvancedInfoContent, "Address", $"0x{node.Address:X}");
            }

            panel.BasicInfoExpander.Visibility = Visibility.Visible;
            panel.MemoryInfoExpander.Visibility = Visibility.Visible;
            panel.AdvancedInfoExpander.Visibility = node.Address > 0 ? Visibility.Visible : Visibility.Collapsed;
            panel.NoSelectionMessage.Visibility = Visibility.Collapsed;
            panel.DetailsContent.Visibility = Visibility.Visible;
            
            panel.HideReferences();
        }

        /// <summary>
        /// 添加属性行（标签+值）
        /// 参考: SelectionDetailsPanel.AddPropertyRow
        /// </summary>
        private void AddPropertyRow(Panel parent, string label, string value, string tooltip = null)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label + ":",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(tooltip))
            {
                valueBlock.ToolTip = tooltip;
            }

            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            parent.Children.Add(grid);
        }
    }
}
