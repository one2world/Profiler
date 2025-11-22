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
                panel.ClearSelection();
                panel.HideReferences();
            }
        }
    }
}
