using Avalonia.Input;

namespace Constellate.App.Infrastructure.Panes.Gestures;

internal static class PaneGestureSessionGuard
{
    public static bool HasAnyActiveGesture(
        ParentPaneMoveSession? activeParentMoveSession,
        ParentPaneResizeSession? activeParentResizeSession,
        ChildPaneDragSession? activeChildDragSession)
    {
        return activeParentMoveSession is not null ||
               activeParentResizeSession is not null ||
               activeChildDragSession is not null;
    }

    public static bool CanBeginNewGesture(
        ParentPaneMoveSession? activeParentMoveSession,
        ParentPaneResizeSession? activeParentResizeSession,
        ChildPaneDragSession? activeChildDragSession,
        bool isLeftButtonPressed)
    {
        return isLeftButtonPressed &&
               !HasAnyActiveGesture(
                   activeParentMoveSession,
                   activeParentResizeSession,
                   activeChildDragSession);
    }

    public static bool MatchesPointer(ParentPaneMoveSession? session, PointerEventArgs e)
    {
        return session?.MatchesPointer(e) ?? false;
    }

    public static bool MatchesPointer(ParentPaneResizeSession? session, PointerEventArgs e)
    {
        return session?.MatchesPointer(e) ?? false;
    }

    public static bool MatchesPointer(ChildPaneDragSession? session, PointerEventArgs e)
    {
        return session?.MatchesPointer(e) ?? false;
    }
}
