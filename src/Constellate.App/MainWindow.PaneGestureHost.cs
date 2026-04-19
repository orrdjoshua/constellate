using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Constellate.App.Controls;
using Constellate.App.Controls.Panes;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    public partial class MainWindow
    {
        // Tracks the current host-driven hover chrome so we can clear it deterministically.
        private PaneChrome? _activeHostHoverChrome;

        private void InitializePaneGestureHost()
        {
            PaneGestureHostBinder.BindHost(
                this,
                "LeftPaneHost",
                ShellPaneHost_OnPointerPressed,
                ShellPaneHost_OnPointerReleased,
                ShellPaneHost_OnPointerMoved);

            PaneGestureHostBinder.BindHost(
                this,
                "TopPaneHost",
                ShellPaneHost_OnPointerPressed,
                ShellPaneHost_OnPointerReleased,
                ShellPaneHost_OnPointerMoved);

            PaneGestureHostBinder.BindHost(
                this,
                "RightPaneHost",
                ShellPaneHost_OnPointerPressed,
                ShellPaneHost_OnPointerReleased,
                ShellPaneHost_OnPointerMoved);

            PaneGestureHostBinder.BindHost(
                this,
                "BottomPaneHost",
                ShellPaneHost_OnPointerPressed,
                ShellPaneHost_OnPointerReleased,
                ShellPaneHost_OnPointerMoved);

            PaneGestureHostBinder.BindHost(
                this,
                "FloatingPaneHost",
                ShellPaneHost_OnPointerPressed,
                ShellPaneHost_OnPointerReleased,
                ShellPaneHost_OnPointerMoved);

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

        private void ShellPaneHost_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Clear any host hover affordance before attempting to start a gesture.
            ClearActiveHostHover();
            TryBeginHostParentPaneDrag(sender, e);
        }

        private void ShellPaneHost_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            // End-of-host interaction → clear any host hover affordance.
            ClearActiveHostHover();
            if (_activeParentMoveSession is null ||
                !PaneGestureSessionGuard.MatchesPointer(_activeParentMoveSession, e))
            {
                return;
            }

            CompleteActiveParentPaneDrag(e);
        }

        private void ShellPaneHost_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            // If an active parent move is ongoing for this pointer, continue that preview.
            if (_activeParentMoveSession is not null &&
                PaneGestureSessionGuard.MatchesPointer(_activeParentMoveSession, e))
            {
                UpdateActiveParentPaneDragPreview(e.GetPosition(this));
                return;
            }

            // During active resize (or any active gesture), suppress host-level hover.
            if (_activeParentResizeSession is not null ||
                _activeChildDragSession is not null ||
                _activeParentMoveSession is not null)
            {
                ClearActiveHostHover();
                return;
            }

            // No active gesture: update host-driven hover so halo mirrors drag-start eligibility.
            if (sender is Control hostControl)
            {
                UpdateHostDragHover(hostControl, e);
            }
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

        // Host-level hover: light the occupied parent’s shell when a parent move could start
        // at the current pointer location (excluding ChildPaneView and GridSplitter regions).
        private void UpdateHostDragHover(Control hostControl, PointerEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm is null)
            {
                ClearActiveHostHover();
                return;
            }

            var hostId = ParentPaneDragStateResolver.ResolveHostIdFromHostControl(hostControl);
            if (string.IsNullOrWhiteSpace(hostId))
            {
                ClearActiveHostHover();
                return;
            }

            var parent = vm.GetFirstExpandedParentOnHost(hostId);
            if (parent is null)
            {
                ClearActiveHostHover();
                return;
            }

            // Determine the deepest visual under the pointer. If it lives inside a ChildPaneView
            // or a GridSplitter then a parent move would not begin here → no shell-level hover.
            var srcVisual = (e.Source as Visual) ?? (hostControl as Visual);
            if (IsWithinChildOrSplitter(srcVisual))
            {
                ClearActiveHostHover();
                return;
            }

            // Find the active ParentPaneView under the pointer and its PaneChrome.
            var paneView = FindAncestor<ParentPaneView>(srcVisual) ?? FindDescendant<ParentPaneView>(hostControl);
            if (paneView is null)
            {
                ClearActiveHostHover();
                return;
            }

            // Resolve the PaneChrome under this ParentPaneView for lighting the outer halo.
            var chrome = FindDescendant<PaneChrome>(paneView);
            if (chrome is null)
            {
                ClearActiveHostHover();
                return;
            }

            if (!ReferenceEquals(_activeHostHoverChrome, chrome))
            {
                // Switch current hover target
                if (_activeHostHoverChrome is not null)
                {
                    _activeHostHoverChrome.SetDragHover(false);
                }
                _activeHostHoverChrome = chrome;
            }

            // Light the outer shell border (host-level hover) explicitly; avoid InputHelper's region gate.
            // This mirrors “drag can start on empty parent body when not over children/splitters”.
            _activeHostHoverChrome.SetDragHover(true);
        }

        private void ClearActiveHostHover()
        {
            if (_activeHostHoverChrome is not null)
            {
                _activeHostHoverChrome.SetDragHover(false);
                _activeHostHoverChrome = null;
            }
        }

        private static bool IsWithinChildOrSplitter(Visual? v)
        {
            for (var cur = v; cur is not null; cur = cur.GetVisualParent())
            {
                if (cur is ChildPaneView || cur is GridSplitter) return true;
            }
            return false;
        }

        private static TControl? FindAncestor<TControl>(Visual? start) where TControl : class
        {
            for (var cur = start; cur is not null; cur = cur.GetVisualParent())
            {
                if (cur is TControl t) return t;
            }
            return null;
        }

        private static TControl? FindDescendant<TControl>(Visual? root) where TControl : class
        {
            if (root is null) return null;
            foreach (var child in root.GetVisualChildren())
            {
                if (child is TControl t) return t;
                var nested = FindDescendant<TControl>(child);
                if (nested is not null) return nested;
            }
            return null;
        }
    }
}
