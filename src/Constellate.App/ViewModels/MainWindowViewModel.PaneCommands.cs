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

        var labelIndex = ChildPanes.Count + 1;
        var id = $"child.{labelIndex}";
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

        RaiseChildPaneCollectionsChanged();

        _moveChildPaneUpCommand.RaiseCanExecuteChanged();
        _moveChildPaneDownCommand.RaiseCanExecuteChanged();
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

        return new ParentPaneModel
        {
            Id = $"parent.{normalizedHost}.{nextOrdinal}",
            Title = "Parent Pane",
            HostId = normalizedHost,
            IsMinimized = false,
            SplitCount = 1,
            SlideIndex = GetSlideIndexForHost(normalizedHost),
            FloatingWidth = 320,
            FloatingHeight = 240
        };
    }
}
