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
    private static bool HostUsesHorizontalChildFlow(string hostId)
    {
        var normalizedHost = NormalizeHostId(hostId);
        return string.Equals(normalizedHost, "top", StringComparison.Ordinal) ||
               string.Equals(normalizedHost, "bottom", StringComparison.Ordinal);
    }

    private static double GetDefaultChildPanePreferredSizeRatio(string hostId)
    {
        // Top/bottom parents size children along horizontal flow; left/right parents
        // size children along vertical flow. The v1 default is still 25% of the
        // parent’s free dimension in either case, but the orientation helper is now
        // explicit so later render/resize logic can branch cleanly by dock side.
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

    /// <summary>
    /// Set geometry for a floating child pane (ParentId == null). Used by FloatingPaneLayer resize grips.
    /// </summary>
    public void SetFloatingChildGeometry(string id, double x, double y, double width, double height)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var idx = -1;
        ChildPaneDescriptor? current = null;
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (!string.Equals(c.Id, id, StringComparison.Ordinal)) continue;
            idx = i;
            current = c;
            break;
        }
        if (idx < 0 || current is null) return;
        // Only adjust if floating (ParentId == null)
        if (current.ParentId is not null) return;

        var fx = Math.Max(0, x);
        var fy = Math.Max(0, y);
        var fw = Math.Max(80.0, width);
        var fh = Math.Max(80.0, height);

        ChildPanes[idx] = current with
        {
            FloatingX = fx,
            FloatingY = fy,
            FloatingWidth = fw,
            FloatingHeight = fh
        };

        RaiseChildPaneCollectionsChanged();
    }

    /// <summary>
    /// Returns the first expanded parent pane hosted on the given host (left/top/right/bottom),
    /// or null if none exists. Normalizes host id.
    /// </summary>
    public ParentPaneModel? GetFirstExpandedParentOnHost(string hostId)
    {
        var normalized = NormalizeHostId(hostId);
        return ParentPaneModels.FirstOrDefault(p =>
            !p.IsMinimized &&
            string.Equals(NormalizeHostId(p.HostId), normalized, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns the count of non-minimized children in the specified lane for the parent's current SlideIndex.
    /// </summary>
    public int GetChildrenCountInLaneForCurrentSlide(string parentId, int laneIndex)
    {
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null) return 0;
        var slideIndex = parent.SlideIndex;
        return ChildPanes.Count(c =>
            !c.IsMinimized &&
            string.Equals(c.ParentId, parentId, StringComparison.Ordinal) &&
            c.SlideIndex == slideIndex &&
            c.ContainerIndex == laneIndex);
    }

    /// <summary>
    /// Place a child into a specific parent/lane at a target insert index for the parent's current SlideIndex.
    /// If the child belongs to a different parent or slide, it is moved accordingly. All visible children
    /// (non-minimized) for that parent/slide are then reindexed with sequential Orders lane-major to ensure stability.
    /// </summary>
    public void PlaceChildInParentLane(string childId, string parentId, int laneIndex, int insertIndex)
    {
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null) return;
        var slideIndex = parent.SlideIndex;

        // Snapshot visible children for this parent/slide (exclude minimized)
        var visible = ChildPanes
            .Where(c => !c.IsMinimized &&
                        string.Equals(c.ParentId, parentId, StringComparison.Ordinal) &&
                        c.SlideIndex == slideIndex)
            .OrderBy(c => c.Order)
            .ToList();

        // Find (or synthesize) the moving child in the full collection
        var idxGlobal = -1;
        ChildPaneDescriptor? moving = null;
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (string.Equals(c.Id, childId, StringComparison.Ordinal))
            {
                idxGlobal = i;
                moving = c;
                break;
            }
        }
        if (idxGlobal < 0 || moving is null)
        {
            return;
        }

        // If child belongs to a different parent/slide, project it here first
        if (!string.Equals(moving.ParentId, parentId, StringComparison.Ordinal) || moving.SlideIndex != slideIndex)
        {
            moving = moving with
            {
                ParentId = parentId,
                SlideIndex = slideIndex
            };
        }

        // Remove from visible snapshot if present
        visible.RemoveAll(c => string.Equals(c.Id, childId, StringComparison.Ordinal));

        // Build per-lane lists (0..SplitCount-1), keep existing order within each lane
        var splitCount = Math.Max(1, Math.Min(3, parent.SplitCount));
        var lanes = new List<List<ChildPaneDescriptor>>(capacity: splitCount);
        for (int li = 0; li < splitCount; li++)
        {
            lanes.Add(visible.Where(c => c.ContainerIndex == li).OrderBy(c => c.Order).ToList());
        }

        // Clamp insert index to [0..count] for the target lane and insert
        var targetLane = Math.Clamp(laneIndex, 0, splitCount - 1);
        var laneList = lanes[targetLane];
        var clampedInsert = Math.Clamp(insertIndex, 0, laneList.Count);

        moving = moving with { ContainerIndex = targetLane };
        laneList.Insert(clampedInsert, moving);

        // Reindex Orders lane-major across all lanes to ensure unique sequential ordering
        var reindexed = new List<ChildPaneDescriptor>();
        for (int li = 0; li < splitCount; li++)
        {
            foreach (var c in lanes[li])
            {
                if (c.ContainerIndex != li)
                {
                    reindexed.Add(c with { ContainerIndex = li });
                }
                else
                {
                    reindexed.Add(c);
                }
            }
        }

        // Write back updated children for this parent/slide (visible ones). Preserve minimized and other parents intact.
        int nextOrder = 0;
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (!string.Equals(c.ParentId, parentId, StringComparison.Ordinal) || c.SlideIndex != slideIndex || c.IsMinimized)
            {
                continue;
            }

            // If this is the moving child, its new values are in 'reindexed' list; otherwise find updated peer
            var updated = reindexed.FirstOrDefault(x => string.Equals(x.Id, c.Id, StringComparison.Ordinal));
            if (updated is not null)
            {
                ChildPanes[i] = updated with { Order = nextOrder++ };
            }
        }

        // If the child was not previously visible on this parent/slide (moved from elsewhere), ensure it's in the global list
        if (!ChildPanes.Any(c => string.Equals(c.Id, moving.Id, StringComparison.Ordinal)))
        {
            ChildPanes.Add(moving with { Order = nextOrder++ });
        }

        RaiseChildPaneCollectionsChanged();
    }

    /// <summary>
    /// Commit a move to the floating host with explicit geometry. The coordinates (x,y)
    /// must already be relative to the CenterViewportHost (the floating layer’s Canvas origin).
    /// </summary>
    public void MoveParentPaneToFloating(string? originHostId, double x, double y, double width, double height)
    {
        if (ParentPaneModels.Count == 0)
        {
            return;
        }

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

        try
        {
            Console.WriteLine($"[VM.MoveParentPaneToFloating] parentId={parentModel.Id} x={x:0} y={y:0} w={width:0} h={height:0}");
        }
        catch { }

        parentModel.HostId = "floating";
        parentModel.IsMinimized = false;
        // Preserve current slide index semantics (not used by floating host, but kept for consistency)
        parentModel.SlideIndex = GetSlideIndexForHost("floating");

        parentModel.FloatingX = Math.Max(0, x);
        parentModel.FloatingY = Math.Max(0, y);
        parentModel.FloatingWidth = Math.Max(80.0, width);
        parentModel.FloatingHeight = Math.Max(80.0, height);

        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
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
        // The floating conversion (with correct coordinate transform) is handled in code-behind where container bounds are known.
        if (!string.Equals(normalizedTarget, "floating", StringComparison.Ordinal))
        {
            var occupied = ParentPaneModels.Any(p =>
                !ReferenceEquals(p, parentModel) &&
                string.Equals(NormalizeHostId(p.HostId), normalizedTarget, StringComparison.Ordinal));

            if (occupied)
            {
                // Cancel VM-side docking change. Code-behind already converted this case to floating with proper geometry.
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
