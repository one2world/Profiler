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
                return;
            }

            panel.ShowUnityObjectDetails(node);
            panel.HideReferences();
        }
    }
}
