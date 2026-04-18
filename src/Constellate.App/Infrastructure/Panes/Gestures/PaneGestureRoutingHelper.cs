using System;
using Avalonia;
using Avalonia.Input;

namespace Constellate.App.Infrastructure.Panes.Gestures;

internal static class PaneGestureRoutingHelper
{
    public static bool RoutePointerMoved(
        PointerEventArgs e,
        Func<PointerEventArgs, bool> hasActiveResizeGesture,
        Action<PointerEventArgs> updateResize,
        Func<PointerEventArgs, bool> hasActiveChildDragGesture,
        Action<PointerEventArgs> updateChildDrag,
        Func<PointerEventArgs, bool> hasActiveParentMoveGesture,
        Func<PointerEventArgs, Point> resolveParentMovePoint,
        Action<Point> updateParentMovePreview)
    {
        if (hasActiveResizeGesture(e))
        {
            updateResize(e);
            return true;
        }

        if (hasActiveChildDragGesture(e))
        {
            updateChildDrag(e);
            return true;
        }

        if (!hasActiveParentMoveGesture(e))
        {
            return false;
        }

        updateParentMovePreview(resolveParentMovePoint(e));
        return true;
    }

    public static bool RoutePointerReleased(
        PointerReleasedEventArgs e,
        Func<PointerReleasedEventArgs, bool> hasActiveResizeGesture,
        Action<PointerReleasedEventArgs> completeResize,
        Func<PointerReleasedEventArgs, bool> hasActiveChildDragGesture,
        Action<PointerReleasedEventArgs> completeChildDrag,
        Func<PointerReleasedEventArgs, bool> hasActiveParentMoveGesture,
        Action<PointerReleasedEventArgs> completeParentMove)
    {
        if (hasActiveResizeGesture(e))
        {
            completeResize(e);
            return true;
        }

        if (hasActiveChildDragGesture(e))
        {
            completeChildDrag(e);
            return true;
        }

        if (!hasActiveParentMoveGesture(e))
        {
            return false;
        }

        completeParentMove(e);
        return true;
    }

    public static void TryCapturePointer(IInputElement target, PointerEventArgs e)
    {
        try
        {
            e.Pointer.Capture(target);
        }
        catch
        {
        }
    }

    public static void ReleasePointer(PointerEventArgs e)
    {
        try
        {
            e.Pointer.Capture(null);
        }
        catch
        {
        }
    }
}
