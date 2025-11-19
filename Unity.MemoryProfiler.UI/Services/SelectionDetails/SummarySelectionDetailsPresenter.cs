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
            var node = (SummarySelectionNode)context.Node;

            panel.ClearAllGroups();
            panel.SetItemName(node.Title);
            panel.SetDescription(node.Description);

            if (node.Metrics.Count > 0)
            {
                panel.ShowGroup(SelectionDetailsPanel.GroupNameBasic);
                foreach (var metric in node.Metrics)
                {
                    var options = DynamicElementOptions.ShowTitle;
                    if (metric.Selectable)
                        options |= DynamicElementOptions.SelectableLabel;

                    panel.AddDynamicElement(
                        SelectionDetailsPanel.GroupNameBasic,
                        metric.Label,
                        metric.Value,
                        metric.Tooltip,
                        options);
                }
            }

            if (!string.IsNullOrEmpty(node.DocumentationUrl))
            {
                panel.ShowGroup(SelectionDetailsPanel.GroupNameHelp);
                panel.AddDynamicElement(
                    SelectionDetailsPanel.GroupNameHelp,
                    "Documentation",
                    node.DocumentationUrl,
                    tooltip: node.DocumentationUrl,
                    options: DynamicElementOptions.SelectableLabel);
            }

            panel.HideManagedObjectInspector();
            panel.HideMemoryInfo();
            panel.HideReferences();
        }
    }
}
