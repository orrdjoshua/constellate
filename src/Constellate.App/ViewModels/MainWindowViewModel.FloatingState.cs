using System;
using System.Linq;

namespace Constellate.App;

public sealed partial class MainWindowViewModel
{
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
