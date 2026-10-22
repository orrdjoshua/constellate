using System;
using System.Linq;

namespace Constellate.App;

public sealed partial class MainWindowViewModel
{
    // Commentary/parity note:
    // This partial is currently a narrow floating-shell coordination seam focused on
    // z-index normalization across parent and child floating surfaces. That keeps the
    // concern reasonably contained today, but longer-term convergence should let this
    // logic depend more on clearer floating-surface / overlay ownership boundaries and
    // less on centralized ViewModel coordination.

    public void SetFloatingChildZIndex(string id, int zIndex)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var resolvedZIndex = Math.Max(1, zIndex);

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var current = ChildPanes[i];
            if (!string.Equals(current.Id, id, StringComparison.Ordinal))
            {
                continue;
            }

            if (current.ParentId is not null || current.FloatingZIndex == resolvedZIndex)
            {
                return;
            }

            ChildPanes[i] = current with { FloatingZIndex = resolvedZIndex };
            RaiseChildPaneCollectionsChanged();
            return;
        }
    }

    private int GetNextFloatingPaneZIndex()
    {
        var maxParentZIndex = ParentPaneModels
            .Where(parent => string.Equals(NormalizeHostId(parent.HostId), "floating", StringComparison.Ordinal))
            .Select(parent => parent.FloatingZIndex)
            .DefaultIfEmpty(0)
            .Max();

        var maxChildZIndex = ChildPanes
            .Where(child => child.ParentId is null)
            .Select(child => child.FloatingZIndex)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(maxParentZIndex, maxChildZIndex) + 1;
    }
}
