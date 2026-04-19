using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Constellate.App.Controls.Panes;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    public partial class MainWindow
    {
        private void InitializePaneGestureHost()
        {
            PaneGestureHostBinder.BindGrip(
                this,
                "LeftPaneResizeGrip",
                "left",
                PaneResizeGrip_OnPointerPressed,
                PaneResizeGrip_OnPointerReleased,
                PaneResizeGrip_OnPointerMoved,
                PaneResizeGrip_OnPointerCaptureLost);

            PaneGestureHostBinder.BindGrip(
                this,
                "RightPaneResizeGrip",
                "right",
                PaneResizeGrip_OnPointerPressed,
                PaneResizeGrip_OnPointerReleased,
                PaneResizeGrip_OnPointerMoved,
                PaneResizeGrip_OnPointerCaptureLost);

            PaneGestureHostBinder.BindGrip(
                this,
                "TopPaneResizeGrip",
                "top",
                PaneResizeGrip_OnPointerPressed,
                PaneResizeGrip_OnPointerReleased,
                PaneResizeGrip_OnPointerMoved,
                PaneResizeGrip_OnPointerCaptureLost);

            PaneGestureHostBinder.BindGrip(
                this,
                "BottomPaneResizeGrip",
                "bottom",
                PaneResizeGrip_OnPointerPressed,
                PaneResizeGrip_OnPointerReleased,
                PaneResizeGrip_OnPointerMoved,
                PaneResizeGrip_OnPointerCaptureLost);

            PaneGestureHostBinder.BindWindowGlobalHandlers(
                this,
                Window_OnGlobalPointerMoved,
                Window_OnGlobalPointerReleased);
        }

        internal bool TryBeginPressedPaneDrag(object? sender, object? paneDataContext, PointerPressedEventArgs e)
        {
            return PaneGestureHostBinder.TryBeginPressedPaneDrag(
                paneDataContext,
                sender,
                e,
                TryBeginParentPaneDrag,
                TryBeginChildPaneDrag);
        }

        private bool CanBeginPaneGesture(PointerPressedEventArgs e)
        {
            return PaneGestureSessionGuard.CanBeginNewGesture(
                _activeParentMoveSession,
                _activeParentResizeSession,
                _activeChildDragSession,
                e.GetCurrentPoint(this).Properties.IsLeftButtonPressed);
        }

        private void Window_OnGlobalPointerMoved(object? sender, PointerEventArgs e)
        {
            PaneGestureRoutingHelper.RoutePointerMoved(
                e,
                args => PaneGestureSessionGuard.MatchesPointer(_activeParentResizeSession, args),
                UpdateActiveParentPaneResize,
                args => PaneGestureSessionGuard.MatchesPointer(_activeChildDragSession, args),
                UpdateActiveChildPaneDrag,
                args => PaneGestureSessionGuard.MatchesPointer(_activeParentMoveSession, args),
                args => args.GetPosition(this),
                UpdateActiveParentPaneDragPreview);
        }

        private void Window_OnGlobalPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            PaneGestureRoutingHelper.RoutePointerReleased(
                e,
                args => PaneGestureSessionGuard.MatchesPointer(_activeParentResizeSession, args),
                CompleteActiveParentPaneResize,
                args => PaneGestureSessionGuard.MatchesPointer(_activeChildDragSession, args),
                CompleteActiveChildPaneDrag,
                args => PaneGestureSessionGuard.MatchesPointer(_activeParentMoveSession, args),
                CompleteActiveParentPaneDrag);
        }

        private void PaneResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (TryBeginParentPaneResize(sender, e))
            {
                // Redundant with coordinator’s markHandled but ensures stability across platforms.
                e.Handled = true;
                return;
            }
        }

        private void PaneResizeGrip_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            CompleteActiveParentPaneResize(e);
        }

        private void PaneResizeGrip_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_activeParentResizeSession is null)
            {
                return;
            }

            ResetActiveParentPaneResize(commit: false);
        }

        private void PaneResizeGrip_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            UpdateActiveParentPaneResize(e);
        }

        private void TryCaptureWindowPointer(PointerEventArgs e)
        {
            PaneGestureRoutingHelper.TryCapturePointer(this, e);
        }

        private void ReleaseWindowPointer(PointerEventArgs e)
        {
            PaneGestureRoutingHelper.ReleasePointer(e);
        }

        private void OnFloatingParentHeaderDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control header)
            {
                return;
            }

            PaneChromeInputHelper.TryHandleEmptyHeaderDoubleTap(header, header.DataContext, e);
        }
    }
}
