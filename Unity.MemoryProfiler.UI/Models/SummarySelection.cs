using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models
{
    internal enum SummarySelectionKind
    {
        MemoryUsage,
        MemoryDistributionCategory,
        ManagedHeapSegment,
        UnityObjectCategory,
        Issue,
        Custom
    }

    internal sealed class SummarySelectionNode : ITreeNode
    {
        public SummarySelectionNode(
            int id,
            SummarySelectionKind kind,
            string title,
            string description,
            IReadOnlyList<SummarySelectionMetric>? metrics = null,
            string? documentationUrl = null)
        {
            Id = id;
            Kind = kind;
            Title = title;
            Description = description;
            Metrics = metrics ?? System.Array.Empty<SummarySelectionMetric>();
            DocumentationUrl = documentationUrl;
        }

        public int Id { get; }

        public SummarySelectionKind Kind { get; }

        public string Title { get; }

        public string Description { get; }

        public IReadOnlyList<SummarySelectionMetric> Metrics { get; }

        public string? DocumentationUrl { get; }

        public IEnumerable<object>? GetChildren() => null;
    }

    internal readonly struct SummarySelectionMetric
    {
        public SummarySelectionMetric(string label, string value, string? tooltip = null, bool selectable = false)
        {
            Label = label;
            Value = value;
            Tooltip = tooltip;
            Selectable = selectable;
        }

        public string Label { get; }

        public string Value { get; }

        public string? Tooltip { get; }

        public bool Selectable { get; }
    }
}
