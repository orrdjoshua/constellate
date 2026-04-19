using System;
using System.Linq;
using Avalonia;

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

        if (string.Equals(normalizedHost, "floating", StringComparison.Ordinal))
        {
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

            // Use the current drag-shadow rect (window coordinates) and the
            // canonical floating surface rect to compute pane-local floating
            // coordinates. This mirrors the parent-floating path, which already
            // uses relative coordinates inside CurrentShellLayout.FloatingSurfaceRect.
            var surface = CurrentShellLayout.FloatingSurfaceRect;

            var fw = ChildPaneDragShadowWidth > 0
                ? ChildPaneDragShadowWidth
                : paneCurrent.FloatingWidth;
            var fh = ChildPaneDragShadowHeight > 0
                ? ChildPaneDragShadowHeight
                : paneCurrent.FloatingHeight;

            var globalLeft = ChildPaneDragShadowLeft;
            var globalTop = ChildPaneDragShadowTop;

            // Convert to coordinates relative to the floating surface origin.
            var relLeft = globalLeft - surface.X;
            var relTop = globalTop - surface.Y;

            // Clamp so the child remains fully inside the floating surface.
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
            return;
        }

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

        ParentPaneModel? parentModel = null;
        if (!string.IsNullOrWhiteSpace(originHostId))
        {
            parentModel = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, originHostId, StringComparison.Ordinal));
            parentModel ??= ParentPaneModels.FirstOrDefault(p =>
                string.Equals(NormalizeHostId(p.HostId), normalizedOrigin, StringComparison.Ordinal));
        }

        parentModel ??= ParentPaneModels.FirstOrDefault();
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
        parentModel.SlideIndex = GetSlideIndexForHost("floating");

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

        ParentPaneModel? parentModel = null;
        if (!string.IsNullOrWhiteSpace(originHostId))
        {
            parentModel = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, originHostId, StringComparison.Ordinal));
            parentModel ??= ParentPaneModels.FirstOrDefault(p =>
                string.Equals(NormalizeHostId(p.HostId), normalizedOrigin, StringComparison.Ordinal));
        }

        parentModel ??= ParentPaneModels.FirstOrDefault();
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
            parentModel.FloatingZIndex = GetNextFloatingPaneZIndex();
        }

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
                // Snapshot current full floating size if we don't already have one.
                if (parentModel.FloatingWidthFull <= 0.0)
                {
                    parentModel.FloatingWidthFull = parentModel.FloatingWidth;
                }

                if (parentModel.FloatingHeightFull <= 0.0)
                {
                    parentModel.FloatingHeightFull = parentModel.FloatingHeight;
                }

                // Keep width as-is to avoid truncating header text; collapse height to a compact header size.
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
