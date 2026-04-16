using System;
using System.Linq;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing pane and child-pane command methods:
/// creation/movement/minimization, host moves, and related helpers. Pure extraction; behavior unchanged.
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
    private void CreateChildPane(string? parentOrHost)
    {
        ParentPaneModel? parent = null;
        string normalizedHost;

        // First, treat the argument as a potential parent Id.
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
            // Fall back to treating it as a host id and selecting a parent on that host.
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

    public void MoveChildPaneToHost(string id, string hostId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var normalizedHost = NormalizeHostId(hostId);

        // Floating child panes (ParentId == null) — use VM drag shadow for geometry and keep ContainerIndex/Split independent
        if (string.Equals(normalizedHost, "floating", StringComparison.Ordinal))
        {
            var idx = -1;
            ChildPaneDescriptor? paneCurrent = null;
            for (var i = 0; i < ChildPanes.Count; i++)
            {
                var pane = ChildPanes[i];
                if (!string.Equals(pane.Id, id, StringComparison.Ordinal))
                    continue;
                idx = i;
                paneCurrent = pane;
                break;
            }
            if (idx < 0 || paneCurrent is null) return;

            var fx = ChildPaneDragShadowLeft;
            var fy = ChildPaneDragShadowTop;
            var fw = ChildPaneDragShadowWidth > 0 ? ChildPaneDragShadowWidth : paneCurrent.FloatingWidth;
            var fh = ChildPaneDragShadowHeight > 0 ? ChildPaneDragShadowHeight : paneCurrent.FloatingHeight;

            // Always update geometry for floating children, even if they were already floating
            ChildPanes[idx] = paneCurrent with
            {
                ParentId = null,
                ContainerIndex = 0,
                SlideIndex = 0,
                FloatingX = fx,
                FloatingY = fy,
                FloatingWidth = fw,
                FloatingHeight = fh
            };

            RaiseChildPaneCollectionsChanged();
            return;
        }

        // Choose a parent pane entity for the target host from ParentPaneModels.
        ParentPaneModel? parent = ParentPaneModels
            .FirstOrDefault(p =>
                !p.IsMinimized &&
                string.Equals(p.HostId, normalizedHost, StringComparison.OrdinalIgnoreCase));
        parent ??= ParentPaneModels.FirstOrDefault();
        if (parent is null)
        {
            return;
        }

        var parentId = parent.Id;
        var slideIndex = parent.SlideIndex;

        var index = -1;
        ChildPaneDescriptor? current = null;

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var pane = ChildPanes[i];
            if (!string.Equals(pane.Id, id, StringComparison.Ordinal))
            {
                continue;
            }

            index = i;
            current = pane;
            break;
        }

        if (index < 0)
        {
            return;
        }

        if (current is not null && string.Equals(current.ParentId, parentId, StringComparison.Ordinal))
        {
            return;
        }

        var nextOrder = ChildPanes
            .Where(pane => string.Equals(pane.ParentId, parentId, StringComparison.Ordinal))
            .Select(pane => pane.Order)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        ChildPanes[index] = current! with
        {
            Order = nextOrder,
            ContainerIndex = 0,
            SlideIndex = slideIndex,
            ParentId = parentId
        };

        RaiseChildPaneCollectionsChanged();
    }

    public void MoveParentPaneToHost(string hostId)
    {
        MoveParentPaneToHost(null, hostId);
    }

    /// <summary>
    /// Move the parent pane hosted on <paramref name="originHostId"/> (or the first pane
    /// if originHostId is null/unknown) to the <paramref name="targetHost"/>.
    /// This is the host-aware variant used by drag gestures.
    /// </summary>
    public void MoveParentPaneToHost(string? originHostId, string targetHost)
    {
        if (ParentPaneModels.Count == 0 || string.IsNullOrWhiteSpace(targetHost))
        {
            return;
        }

        var normalizedTarget = NormalizeHostId(targetHost);
        var normalizedOrigin = NormalizeHostId(originHostId);

        // Prefer the pane currently on the origin host; fall back to the first parent.
        ParentPaneModel? parentModel = !string.IsNullOrWhiteSpace(originHostId)
            ? ParentPaneModels.FirstOrDefault(p =>
                string.Equals(NormalizeHostId(p.HostId), normalizedOrigin, StringComparison.Ordinal))
            : ParentPaneModels.FirstOrDefault();

        if (parentModel is null)
        {
            return;
        }

        if (string.Equals(NormalizeHostId(parentModel.HostId), normalizedTarget, StringComparison.Ordinal) &&
            !parentModel.IsMinimized)
        {
            return;
        }

        // Enforce single-pane dock: if another parent already occupies the target dock host (minimized or not),
        // do NOT dock a second pane there. Choose a safe fallback:
        // - if we have a valid drag-shadow rect, convert this move to floating at that geometry
        // - otherwise, cancel the move (stay in origin)
        if (!string.Equals(normalizedTarget, "floating", StringComparison.Ordinal))
        {
            var occupied = ParentPaneModels.Any(p =>
                !ReferenceEquals(p, parentModel) &&
                string.Equals(NormalizeHostId(p.HostId), normalizedTarget, StringComparison.Ordinal));

            if (occupied)
            {
                // Use drag shadow as a fallback to floating when available
                var left = ParentPaneDragShadowLeft;
                var top = ParentPaneDragShadowTop;
                var width = ParentPaneDragShadowWidth;
                var height = ParentPaneDragShadowHeight;

                if (width > 0 && height > 0)
                {
                    normalizedTarget = "floating";
                    parentModel.HostId = normalizedTarget;
                    parentModel.IsMinimized = false;
                    parentModel.SlideIndex = GetSlideIndexForHost(normalizedTarget);
                    parentModel.FloatingX = left;
                    parentModel.FloatingY = top;
                    parentModel.FloatingWidth = width;
                    parentModel.FloatingHeight = height;
                    RaiseParentPaneLayoutChanged(includeChildRefresh: true);
                }
                return;
            }
        }

        parentModel.HostId = normalizedTarget;
        parentModel.IsMinimized = false;
        parentModel.SlideIndex = GetSlideIndexForHost(normalizedTarget);

        if (string.Equals(normalizedTarget, "floating", StringComparison.Ordinal))
        {
            var left = ParentPaneDragShadowLeft;
            var top = ParentPaneDragShadowTop;
            var width = ParentPaneDragShadowWidth;
            var height = ParentPaneDragShadowHeight;

            if (width <= 0 || height <= 0)
            {
                width = parentModel.FloatingWidth;
                height = parentModel.FloatingHeight;
            }

            parentModel.FloatingX = left;
            parentModel.FloatingY = top;
            parentModel.FloatingWidth = width;
            parentModel.FloatingHeight = height;
        }

        // Notify host projections and visibility booleans so UI re-renders the new state
        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
    }

    public void SetShellPaneMinimized(bool minimized)
    {
        if (ParentPaneModels.Count == 0)
        {
            return;
        }

        var current = ParentPaneModels[0];
        if (current.IsMinimized == minimized)
        {
            return;
        }

        current.IsMinimized = minimized;
        RaiseParentPaneLayoutChanged();
    }

    /// <summary>
    /// Minimize helper that can accept either a parent pane Id or a host id.
    /// If the argument matches a ParentPaneModel.Id, only that parent is toggled;
    /// otherwise it is treated as a host id and the first pane on that host is toggled.
    /// </summary>
    public void SetParentPaneMinimized(string? idOrHost, bool minimized)
    {
        if (ParentPaneModels.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(idOrHost))
        {
            SetShellPaneMinimized(minimized);
            return;
        }

        // First, try to treat the argument as a parent-pane Id.
        var parentModel = ParentPaneModels.FirstOrDefault(p =>
            string.Equals(p.Id, idOrHost, StringComparison.Ordinal));

        if (parentModel is null)
        {
            var normalizedHost = NormalizeHostId(idOrHost);
            parentModel = ParentPaneModels.FirstOrDefault(p =>
                string.Equals(NormalizeHostId(p.HostId), normalizedHost, StringComparison.Ordinal));
        }

        if (parentModel is null || parentModel.IsMinimized == minimized)
        {
            return;
        }

        parentModel.IsMinimized = minimized;
        RaiseParentPaneLayoutChanged();
    }

    internal static string NormalizeHostId(string? hostId)
    {
        if (string.IsNullOrWhiteSpace(hostId))
        {
            return "left";
        }

        var normalized = hostId.Trim().ToLowerInvariant();
        return normalized is "left" or "top" or "right" or "bottom" or "floating"
            ? normalized
            : "left";
    }

    public string GetHostIdForChildPane(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return "left";
        }

        var child = ChildPanes.FirstOrDefault(pane =>
            string.Equals(pane.Id, childPaneId, StringComparison.Ordinal));
        if (child is null || string.IsNullOrWhiteSpace(child.ParentId))
        {
            return "left";
        }

        var parent = ParentPaneModels.FirstOrDefault(pane =>
            string.Equals(pane.Id, child.ParentId, StringComparison.Ordinal));

        return parent is null ? "left" : NormalizeHostId(parent.HostId);
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
