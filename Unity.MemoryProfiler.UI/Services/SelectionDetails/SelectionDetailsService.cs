using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services.SelectionDetails
{
    internal sealed class SelectionDetailsService
    {
        readonly List<ISelectionDetailsPresenter> m_Presenters;

        public SelectionDetailsService(IEnumerable<ISelectionDetailsPresenter> presenters)
        {
            m_Presenters = presenters?.ToList() ?? new List<ISelectionDetailsPresenter>();
        }

        public bool TryPresent(SelectionDetailsPanel view, ITreeNode node, CachedSnapshot snapshot, SelectionDetailsSource origin)
        {
            if (view == null || node == null || snapshot == null)
                return false;

            view.SetSnapshot(snapshot);
            var context = new SelectionDetailsContext(view, node, snapshot, origin);
            foreach (var presenter in m_Presenters)
            {
                if (presenter.CanPresent(context))
                {
                    presenter.Present(context);
                    return true;
                }
            }
            // If no specific presenter handles the node, clear the selection
            view.ClearSelection();
            return false;
        }
    }
}

