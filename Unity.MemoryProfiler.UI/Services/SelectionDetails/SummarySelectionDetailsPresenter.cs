using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services.SelectionDetails
{
    internal sealed class SummarySelectionDetailsPresenter : ISelectionDetailsPresenter
    {
        public bool CanPresent(in SelectionDetailsContext context)
        {
            return context.Origin == SelectionDetailsSource.Summary
                    && context.Node is SummarySelectionNode;
        }

        public void Present(in SelectionDetailsContext context)
        {
            var panel = context.View;
            var adapter = panel.Adapter;
            var node = (SummarySelectionNode)context.Node;

            adapter.ClearAllGroups();
            adapter.SetItemName(node.Title);
            adapter.SetDescription(node.Description);

            if (node.Metrics.Count > 0)
            {
                foreach (var metric in node.Metrics)
                {
                    var options = DynamicElementOptions.ShowTitle;
                    if (metric.Selectable)
                        options |= DynamicElementOptions.SelectableLabel;

                    adapter.AddDynamicElement(
                        SelectionDetailsPanelAdapter.GroupNameBasic,
                        metric.Label,
                        metric.Value,
                        metric.Tooltip,
                        options);
                }
            }

            if (!string.IsNullOrEmpty(node.DocumentationUrl))
            {
                adapter.AddDynamicElement(
                    SelectionDetailsPanelAdapter.GroupNameHelp,
                    "Documentation",
                    node.DocumentationUrl,
                    tooltip: node.DocumentationUrl,
                    options: DynamicElementOptions.SelectableLabel);
            }

            panel.HideManagedObjectInspector();
            panel.HideReferences();
        }
    }
}
