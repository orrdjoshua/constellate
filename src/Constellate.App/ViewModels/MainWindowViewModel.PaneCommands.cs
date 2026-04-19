using System;
using System.Linq;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing pane and child-pane command methods
/// centered on creation, default sizing, and simple reorder behavior. Host/dock/floating
/// transition logic now lives in MainWindowViewModel.PaneHostState.cs.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private void SetChildPaneMinimized(string id, bool minimized)
    {
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var current = ChildPanes[i];
            if (!string.Equals(current.Id, id, StringComparison.Ordinal))
            {
                continue;
            }

            if (current.IsMinimized == minimized)
            {
                return;
            }

            ChildPanes[i] = current with { IsMinimized = minimized };
            RaiseChildPaneCollectionsChanged();
            return;
        }
    }

    /// <summary>
    /// Create a new generic child pane. The parameter may be either:
    /// - a ParentPaneModel.Id (preferred), in which case the child is owned by that parent, or
    /// - a host id ("left"|"top"|"right"|"bottom"|"floating"), in which case we pick a parent
    ///   on that host (or fall back to the first parent) and attach the child there.
    /// </summary>
    private static bool HostUsesHorizontalChildFlow(string hostId)
    {
        var normalizedHost = NormalizeHostId(hostId);
        return string.Equals(normalizedHost, "top", StringComparison.Ordinal) ||
               string.Equals(normalizedHost, "bottom", StringComparison.Ordinal);
    }

    private static double GetDefaultChildPanePreferredSizeRatio(string hostId)
    {
        return HostUsesHorizontalChildFlow(hostId) ? 0.25 : 0.25;
    }

    private static double ResolveChildPanePreferredSizeRatio(string hostId, double? preferredSizeRatio)
    {
        var resolved = preferredSizeRatio ?? GetDefaultChildPanePreferredSizeRatio(hostId);
        return Math.Clamp(resolved, 0.05, 0.95);
    }

    private void CreateChildPane(string? parentOrHost)
    {
        CreateChildPane(parentOrHost, preferredSizeRatio: null);
    }

    private void CreateChildPane(string? parentOrHost, double? preferredSizeRatio)
    {
        ParentPaneModel? parent = null;
        string normalizedHost;

        if (!string.IsNullOrWhiteSpace(parentOrHost))
        {
            parent = ParentPaneModels
                .FirstOrDefault(p => string.Equals(p.Id, parentOrHost, StringComparison.Ordinal));
        }

        if (parent is not null)
        {
            normalizedHost = NormalizeHostId(parent.HostId);
        }
        else
        {
            normalizedHost = NormalizeHostId(parentOrHost);

            if (!string.IsNullOrWhiteSpace(parentOrHost))
            {
                parent = ParentPaneModels
                    .FirstOrDefault(p =>
                        !p.IsMinimized &&
                        string.Equals(p.HostId, normalizedHost, StringComparison.OrdinalIgnoreCase));
            }
        }

        parent ??= ParentPaneModels.FirstOrDefault();
        if (parent is null)
        {
            return;
        }

        var parentId = parent.Id;
        var slideIndex = parent.SlideIndex;
        var resolvedPreferredSizeRatio =
            ResolveChildPanePreferredSizeRatio(normalizedHost, preferredSizeRatio);

        var nextOrder = ChildPanes.Count == 0
            ? 0
            : ChildPanes.Max(pane => pane.Order) + 1;

        // Generate a globally-unique child id of the form "child.N" by scanning
        // existing children instead of relying on ChildPanes.Count. This prevents
        // id reuse after deletions and avoids dictionary-key collisions in layout.
        var id = GenerateNextChildId();
        var labelIndex = ChildPanes.Count + 1;
        var title = $"Pane {labelIndex}";

        ChildPanes.Add(new ChildPaneDescriptor(
            id,
            title,
            nextOrder,
            ContainerIndex: 0,
            IsMinimized: false,
            SlideIndex: slideIndex,
            PreferredSizeRatio: resolvedPreferredSizeRatio,
            ParentId: parentId));

        // Rebalance lane ratios so the new child defaults to ~25% of the lane’s
        // BodySecondary while existing siblings share the remaining 75% proportionally.
        // This operates purely in the parent-body frame (host-agnostic); actual on-screen
        // orientation is handled by the layout projection layer.
        ApplyDefaultChildSizeForNewLaneMember(parentId, laneIndex: 0, newChildId: id);

        RaiseChildPaneCollectionsChanged();

        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Generate a new child id of the form "child.N" where N is one greater than
    /// any existing numeric suffix on child ids. This avoids reusing ids after
    /// deletions or other reordering operations.
    /// </summary>
    private string GenerateNextChildId()
    {
        var maxOrdinal = 0;

        foreach (var pane in ChildPanes)
        {
            if (!pane.Id.StartsWith("child.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = pane.Id.Substring("child.".Length);
            if (int.TryParse(suffix, out var n) && n > maxOrdinal)
            {
                maxOrdinal = n;
            }
        }

        return $"child.{maxOrdinal + 1}";
    }

    private bool CanMoveChildPane(string id, int delta)
    {
        var ordered = ChildPanesOrdered.ToList();
        var index = ordered.FindIndex(pane => string.Equals(pane.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        var newIndex = index + delta;
        return newIndex >= 0 && newIndex < ordered.Count;
    }

    private void MoveChildPane(string id, int delta)
    {
        var ordered = ChildPanesOrdered.ToList();
        var index = ordered.FindIndex(pane => string.Equals(pane.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var newIndex = index + delta;
        if (newIndex < 0 || newIndex >= ordered.Count)
        {
            return;
        }

        var a = ordered[index];
        var b = ordered[newIndex];

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var current = ChildPanes[i];
            if (string.Equals(current.Id, a.Id, StringComparison.Ordinal))
            {
                ChildPanes[i] = current with { Order = b.Order };
            }
            else if (string.Equals(current.Id, b.Id, StringComparison.Ordinal))
            {
                ChildPanes[i] = current with { Order = a.Order };
            }
        }

        RaiseChildPaneCollectionsChanged();

        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
        _floatSettingsChildPaneCommand.RaiseCanExecuteChanged();
        _dockSettingsChildPaneCommand.RaiseCanExecuteChanged();
    }

    private ParentPaneModel CreateParentPaneModel(string hostId)
    {
        var normalizedHost = NormalizeHostId(hostId);
        var nextOrdinal = ParentPaneModels.Count(parent =>
            string.Equals(NormalizeHostId(parent.HostId), normalizedHost, StringComparison.Ordinal)) + 1;
        // Simple global ordinal for visibility during QA; note this is
        // present-count based, so duplicates can occur if panes are removed/re-added.
        // Good enough for test labeling; can be replaced later with a persistent counter.
        var globalOrdinal = ParentPaneModels.Count + 1;

        return new ParentPaneModel
        {
            Id = $"parent.{normalizedHost}.{nextOrdinal}",
            Title = $"Parent Pane #{globalOrdinal}",
            HostId = normalizedHost,
            IsMinimized = false,
            SplitCount = 1,
            SlideIndex = GetSlideIndexForHost(normalizedHost),
            FloatingWidth = 320,
            FloatingHeight = 240
        };
    }
}
