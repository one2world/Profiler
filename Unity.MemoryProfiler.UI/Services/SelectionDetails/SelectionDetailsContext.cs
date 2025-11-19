using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services.SelectionDetails
{
    internal enum SelectionDetailsSource
    {
        Unknown = 0,
        UnityObjects,
        AllTrackedMemory,
        Summary
    }

    internal class SelectionDetailsContext
    {
        public SelectionDetailsPanel View { get; }
        public ITreeNode Node { get; }
        public CachedSnapshot Snapshot { get; }
        public SelectionDetailsSource Origin { get; }

        public SelectionDetailsContext(SelectionDetailsPanel view, ITreeNode node, CachedSnapshot snapshot, SelectionDetailsSource origin)
        {
            View = view;
            Node = node;
            Snapshot = snapshot;
            Origin = origin;
        }
    }
}

