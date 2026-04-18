using Avalonia;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Gestures;

internal static class PaneGestureSessionFactory
{
    public static ParentPaneMoveSession CreateParentMoveSession(
        string paneId,
        string originReferenceId,
        string originHostId,
        long pointerId,
        Point startPoint,
        Rect originBounds)
    {
        return new ParentPaneMoveSession(
            paneId: paneId,
            originReferenceId: originReferenceId,
            pointerId: pointerId,
            startPoint: startPoint,
            originAttachment: DockAttachmentModel.FromHostId(originHostId),
            originBounds: originBounds);
    }

    public static ChildPaneDragSession CreateChildDragSession(
        string paneId,
        string? originParentId,
        string? originHostId,
        int originLaneIndex,
        int originSlideIndex,
        Size originPreviewSize,
        long pointerId,
        Point startPoint)
    {
        return new ChildPaneDragSession(
            paneId: paneId,
            pointerId: pointerId,
            startPoint: startPoint,
            originParentId: originParentId,
            originHostId: originHostId,
            originLaneIndex: originLaneIndex,
            originSlideIndex: originSlideIndex,
            originPreviewSize: originPreviewSize);
    }

    public static ParentPaneResizeSession CreateParentPaneResizeSession(
        string paneId,
        string resizeHostId,
        long pointerId,
        Point startPoint,
        Rect fullWindowBounds,
        double currentDockExtent)
    {
        var attachment = DockAttachmentModel.FromHostId(resizeHostId);
        var originBounds = ParentPaneResizeGesturePlanner.CreateOriginBounds(
            attachment,
            fullWindowBounds,
            currentDockExtent);

        return new ParentPaneResizeSession(
            paneId: paneId,
            resizeHostId: resizeHostId,
            pointerId: pointerId,
            startPoint: startPoint,
            attachment: attachment,
            resizeEdge: ResolveResizeEdge(resizeHostId),
            originBounds: originBounds,
            originDockExtent: currentDockExtent);
    }

    private static PaneResizeEdge ResolveResizeEdge(string edge)
    {
        return edge switch
        {
            "left" => PaneResizeEdge.Right,
            "right" => PaneResizeEdge.Left,
            "top" => PaneResizeEdge.Bottom,
            "bottom" => PaneResizeEdge.Top,
            _ => PaneResizeEdge.None
        };
    }
}
