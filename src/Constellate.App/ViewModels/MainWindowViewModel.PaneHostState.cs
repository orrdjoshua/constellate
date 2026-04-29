using System;
using System.Linq;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing pane host-state and dock/floating
/// transition behavior. This keeps pane relocation, minimization, and floating-geometry
/// mutation separate from query helpers and creation/reorder command logic.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public void MoveChildPaneToHost(string id, string hostId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var normalizedHost = NormalizeHostId(hostId);

        // Compatibility entrypoint only:
        // translate host-directed command/menu actions into the pane-centric child APIs.
        if (string.Equals(normalizedHost, "floating", StringComparison.Ordinal))
        {
            MoveChildPaneToFloating(id);
            return;
        }

        ParentPaneModel? targetParent = ParentPaneModels
            .FirstOrDefault(p =>
                !p.IsMinimized &&
                string.Equals(p.HostId, normalizedHost, StringComparison.OrdinalIgnoreCase));
        targetParent ??= ParentPaneModels.FirstOrDefault();

        if (targetParent is null)
        {
            return;
        }

        const int defaultLaneIndex = 0;
        var defaultInsertIndex = GetChildrenCountInLaneForCurrentSlide(targetParent.Id, defaultLaneIndex);

        DockChildPaneToParent(id, targetParent.Id, defaultLaneIndex, defaultInsertIndex);
    }

    /// <summary>
    /// Pane-centric docking API for children.
    /// A docked ChildPane's authoritative relationship is simply:
    /// ChildPane -> ParentPane.Id + lane/split/order inside that parent.
    /// </summary>
    public void DockChildPaneToParent(string id, string parentId, int laneIndex, int insertIndex)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(parentId))
        {
            return;
        }

        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null || parent.IsMinimized)
        {
            return;
        }

        var clampedLaneIndex = Math.Clamp(laneIndex, 0, Math.Max(0, parent.SplitCount - 1));
        var clampedInsertIndex = Math.Max(0, insertIndex);

        PlaceChildInParentLane(id, parent.Id, clampedLaneIndex, clampedInsertIndex);
    }

    /// <summary>
    /// Pane-centric floating API for children.
    /// Once a child is floated, it has no ParentPane relationship any longer.
    /// Floating geometry is derived from the active drag shadow when present, and
    /// stored relative to the canonical floating surface rect.
    /// </summary>
    public void MoveChildPaneToFloating(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var idx = -1;
        ChildPaneDescriptor? paneCurrent = null;
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var pane = ChildPanes[i];
            if (!string.Equals(pane.Id, id, StringComparison.Ordinal))
            {
                continue;
            }

            idx = i;
            paneCurrent = pane;
            break;
        }

        if (idx < 0 || paneCurrent is null)
        {
            return;
        }

        var surface = CurrentShellLayout.FloatingSurfaceRect;

        var fw = ChildPaneDragShadowWidth > 0
            ? ChildPaneDragShadowWidth
            : paneCurrent.FloatingWidth;
        var fh = ChildPaneDragShadowHeight > 0
            ? ChildPaneDragShadowHeight
            : paneCurrent.FloatingHeight;

        var globalLeft = ChildPaneDragShadowLeft;
        var globalTop = ChildPaneDragShadowTop;

        var relLeft = globalLeft - surface.X;
        var relTop = globalTop - surface.Y;

        relLeft = Math.Clamp(
            relLeft,
            0.0,
            Math.Max(0.0, surface.Width - fw));
        relTop = Math.Clamp(
            relTop,
            0.0,
            Math.Max(0.0, surface.Height - fh));

        var assignedZ = paneCurrent.ParentId is null && paneCurrent.FloatingZIndex > 0
            ? paneCurrent.FloatingZIndex
            : GetNextFloatingPaneZIndex();

        ChildPanes[idx] = paneCurrent with
        {
            ParentId = null,
            ContainerIndex = 0,
            SlideIndex = 0,
            FloatingX = relLeft,
            FloatingY = relTop,
            FloatingWidth = fw,
            FloatingHeight = fh,
            FloatingZIndex = assignedZ
        };

        RaiseChildPaneCollectionsChanged();
    }

    /// <summary>
    /// Set geometry for a floating child pane (ParentId == null). Used by FloatingPaneLayer resize grips.
    /// </summary>
    public void SetFloatingChildGeometry(string id, double x, double y, double width, double height)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var idx = -1;
        ChildPaneDescriptor? current = null;
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (!string.Equals(c.Id, id, StringComparison.Ordinal))
            {
                continue;
            }

            idx = i;
            current = c;
            break;
        }

        if (idx < 0 || current is null)
        {
            return;
        }

        if (current.ParentId is not null)
        {
            return;
        }

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
    /// Commit a move to the floating host for an exact parent pane id. The coordinates (x,y)
    /// must already be relative to the floating surface origin.
    /// </summary>
    public void MoveParentPaneToFloating(string? parentId, double x, double y, double width, double height)
    {
        if (ParentPaneModels.Count == 0 || string.IsNullOrWhiteSpace(parentId))
        {
            return;
        }

        var parentModel = ParentPaneModels.FirstOrDefault(p =>
            string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parentModel is null)
        {
            return;
        }

        try
        {
            Console.WriteLine($"[VM.MoveParentPaneToFloating] parentId={parentModel.Id} x={x:0} y={y:0} w={width:0} h={height:0}");
        }
        catch
        {
        }

        parentModel.HostId = "floating";
        parentModel.IsMinimized = false;
        // Preserve the parent's current SlideIndex; do not force a per-host slide
        // when transitioning to floating so layout remains exactly as last used.

        parentModel.FloatingX = Math.Max(0, x);
        parentModel.FloatingY = Math.Max(0, y);
        parentModel.FloatingWidth = Math.Max(80.0, width);
        parentModel.FloatingHeight = Math.Max(80.0, height);
        parentModel.FloatingZIndex = GetNextFloatingPaneZIndex();

        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
    }

    /// <summary>
    /// Reposition an existing floating parent pane without changing its minimized
    /// state or stored full-size geometry. Used when a parent that is already on
    /// the floating host is being repositioned via drag, including minimized
    /// header-only floating panes.
    /// </summary>
    public void SetFloatingParentPosition(string parentId, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return;
        }

        var parent = ParentPaneModels.FirstOrDefault(p =>
            string.Equals(p.Id, parentId, StringComparison.Ordinal));

        if (parent is null)
        {
            return;
        }

        if (!string.Equals(NormalizeHostId(parent.HostId), "floating", StringComparison.Ordinal))
        {
            // Only reposition parents that are actually hosted on the floating layer.
            return;
        }

        parent.FloatingX = Math.Max(0, x);
        parent.FloatingY = Math.Max(0, y);

        // No changes to IsMinimized, FloatingWidth / FloatingHeight, or the stored
        // full-size geometry; we simply publish the new position.
        RaiseParentPaneLayoutChanged();
    }

    /// <summary>
    /// Move an exact parent pane id to the specified target host.
    /// </summary>
    public void MoveParentPaneToHost(string? parentId, string targetHost)
    {
        if (ParentPaneModels.Count == 0 ||
            string.IsNullOrWhiteSpace(parentId) ||
            string.IsNullOrWhiteSpace(targetHost))
        {
            return;
        }

        var normalizedTarget = NormalizeHostId(targetHost);
        var parentModel = ParentPaneModels.FirstOrDefault(p =>
            string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parentModel is null)
        {
            return;
        }

        if (string.Equals(NormalizeHostId(parentModel.HostId), normalizedTarget, StringComparison.Ordinal) &&
            !parentModel.IsMinimized)
        {
            return;
        }

        if (!string.Equals(normalizedTarget, "floating", StringComparison.Ordinal))
        {
            var occupied = ParentPaneModels.Any(p =>
                !ReferenceEquals(p, parentModel) &&
                string.Equals(NormalizeHostId(p.HostId), normalizedTarget, StringComparison.Ordinal));

            if (occupied)
            {
                return;
            }
        }

        parentModel.HostId = normalizedTarget;
        parentModel.IsMinimized = false;
        // Preserve the parent's current SlideIndex across host transitions
        // so the visible slide/splits remain as the user last left them.

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
            parentModel.FloatingZIndex = GetNextFloatingPaneZIndex();
        }

        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
    }

    /// <summary>
    /// Immediately raise a floating parent to the topmost Z index.
    /// </summary>
    public void BringFloatingParentToFront(string parentId)
    {
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return;
        }

        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null)
        {
            return;
        }

        if (!string.Equals(NormalizeHostId(parent.HostId), "floating", StringComparison.Ordinal))
        {
            return;
        }

        parent.FloatingZIndex = GetNextFloatingPaneZIndex();
        RaiseParentPaneLayoutChanged();
    }

    /// <summary>
    /// Immediately raise a floating child to the topmost Z index.
    /// </summary>
    public void BringFloatingChildToFront(string childId)
    {
        if (string.IsNullOrWhiteSpace(childId))
        {
            return;
        }

        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (!string.Equals(c.Id, childId, StringComparison.Ordinal))
            {
                continue;
            }

            // Only for floating children (ParentId == null).
            if (c.ParentId is not null)
            {
                return;
            }

            ChildPanes[i] = c with { FloatingZIndex = GetNextFloatingPaneZIndex() };
            RaiseChildPaneCollectionsChanged();
            return;
        }
    }

    public void SetChildPaneMinimized(string id, bool minimized)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

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

            var updated = current;

            if (current.ParentId is null)
            {
                const double headerOnlyHeight = 56.0;

                if (minimized)
                {
                    updated = updated with
                    {
                        FloatingWidthFull = current.FloatingWidth,
                        FloatingHeightFull = current.FloatingHeight,
                        FloatingHeight = headerOnlyHeight
                    };
                }
                else
                {
                    updated = updated with
                    {
                        FloatingWidth = current.FloatingWidthFull > 0.0 ? current.FloatingWidthFull : current.FloatingWidth,
                        FloatingHeight = current.FloatingHeightFull > 0.0 ? current.FloatingHeightFull : current.FloatingHeight
                    };
                }
            }

            ChildPanes[i] = updated with
            {
                IsMinimized = minimized
            };

            RaiseChildPaneCollectionsChanged();
            _moveChildPaneUpCommand.RaiseCanExecuteChanged();
            _moveChildPaneDownCommand.RaiseCanExecuteChanged();
            _floatSettingsChildPaneCommand.RaiseCanExecuteChanged();
            _dockSettingsChildPaneCommand.RaiseCanExecuteChanged();
            return;
        }
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

        // For floating parents, snapshot full floating size on minimize and restore it on expand.
        var isFloating = string.Equals(
            NormalizeHostId(parentModel.HostId),
            "floating",
            StringComparison.Ordinal);

        if (isFloating)
        {
            // Transition: expanded -> minimized (header-only chrome)
            if (minimized && !parentModel.IsMinimized)
            {
                // Always snapshot the current full-size geometry so restore reflects the latest expanded size.
                parentModel.FloatingWidthFull = parentModel.FloatingWidth;
                parentModel.FloatingHeightFull = parentModel.FloatingHeight;

                // Keep width as-is for now; a follow-on pass (view layer) will refine width to the true header width.
                const double headerOnlyHeight = 56.0;
                parentModel.FloatingHeight = headerOnlyHeight;
            }
            // Transition: minimized -> expanded (restore previous full geometry)
            else if (!minimized && parentModel.IsMinimized)
            {
                if (parentModel.FloatingWidthFull > 0.0)
                {
                    parentModel.FloatingWidth = parentModel.FloatingWidthFull;
                }

                if (parentModel.FloatingHeightFull > 0.0)
                {
                    parentModel.FloatingHeight = parentModel.FloatingHeightFull;
                }
            }
        }

        parentModel.IsMinimized = minimized;
        RaiseParentPaneLayoutChanged();
    }
}
