using Avalonia.Input;

namespace Constellate.App
{
    public partial class MainWindow
    {
        private bool HasActiveParentMoveGesture(PointerEventArgs e)
        {
            return _activeParentMoveSession is not null
                ? _activeParentMoveSession.MatchesPointer(e)
                : _isShellPaneDragging;
        }

        private bool HasActiveParentResizeGesture(PointerEventArgs e)
        {
            return _activeParentResizeSession is not null
                ? _activeParentResizeSession.MatchesPointer(e)
                : _isPaneResizing;
        }

        private bool HasActiveChildDragGesture(PointerEventArgs e)
        {
            return _activeChildDragSession is not null
                ? _activeChildDragSession.MatchesPointer(e)
                : _isChildPaneDragging;
        }

        private void Window_OnGlobalPointerMoved(object? sender, PointerEventArgs e)
        {
            if (HasActiveParentResizeGesture(e))
            {
                PaneResizeGrip_OnPointerMoved(sender, e);
                return;
            }

            if (HasActiveChildDragGesture(e))
            {
                OnChildPaneHeaderPointerMoved(sender, e);
                return;
            }

            if (!HasActiveParentMoveGesture(e))
            {
                return;
            }

            UpdateActiveParentPaneDragPreview(e.GetPosition(this));
        }

        private void Window_OnGlobalPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (HasActiveParentResizeGesture(e))
            {
                PaneResizeGrip_OnPointerReleased(sender, e);
                return;
            }

            if (HasActiveChildDragGesture(e))
            {
                OnChildPaneHeaderPointerReleased(sender, e);
                return;
            }

            if (!HasActiveParentMoveGesture(e))
            {
                return;
            }

            CompleteActiveParentPaneDrag(e);
        }
    }
}
