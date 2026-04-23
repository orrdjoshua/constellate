using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        private bool TryBeginParentPaneResize(object? sender, PointerPressedEventArgs e)
        {
            if (!CanBeginPaneGesture(e))
            {
                return false;
            }

            if (sender is not Border { Tag: string edge })
            {
                return false;
            }

            return BeginParentPaneResizeSession(edge, e);
        }

        private bool BeginParentPaneResizeSession(string edge, PointerPressedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return false;
            }

            var startPoint = e.GetPosition(this);
            var parent = vm.GetFirstExpandedParentOnHost(edge);
            var paneId = parent?.Id ?? edge;
            var fullWindowBounds = new Rect(
                0,
                0,
                Math.Max(0.0, Bounds.Width),
                Math.Max(0.0, Bounds.Height));

            var session = PaneGestureSessionFactory.CreateParentPaneResizeSession(
                paneId: paneId,
                resizeHostId: edge,
                pointerId: (long)e.Pointer.Id,
                startPoint: startPoint,
                fullWindowBounds: fullWindowBounds,
                currentDockExtent: GetCurrentDockSize(edge));

            try
            {
                System.Diagnostics.Debug.WriteLine($"[OverlayGrip][ResizeStart] host={edge} paneId={paneId} pointer={e.Pointer.Id} start=({startPoint.X:0.##},{startPoint.Y:0.##}) extent={GetCurrentDockSize(edge):0.##}");
            }
            catch
            {
            }

            return PaneGestureSessionCoordinator.Start(
                ref _activeParentResizeSession,
                session,
                e,
                TryCaptureWindowPointer,
                markHandled: true);
        }

        private void CompleteActiveParentPaneResize(PointerReleasedEventArgs e)
        {
            if (_activeParentResizeSession is null)
            {
                return;
            }

            if (!ActiveParentResizeOwnsPointer(e))
            {
                return;
            }

            ReleaseWindowPointer(e);
            ResetActiveParentPaneResize(commit: true);
            e.Handled = true;
        }

        private void UpdateActiveParentPaneResize(PointerEventArgs e)
        {
            var rootGrid = GetShellRootGrid();
            var session = _activeParentResizeSession;
            if (session is null || rootGrid is null || string.IsNullOrWhiteSpace(session.ResizeHostId))
            {
                return;
            }

            if (!ActiveParentResizeOwnsPointer(e))
            {
                return;
            }

            var current = e.GetPosition(this);
            var fullWindowBounds = new Rect(
                0,
                0,
                Math.Max(0.0, Bounds.Width),
                Math.Max(0.0, Bounds.Height));

            var preview = ParentPaneResizeGesturePlanner.ComputePreview(
                session.Attachment,
                fullWindowBounds,
                session.OriginBounds,
                session.StartPoint,
                current);

            if (DataContext is MainWindowViewModel vm)
            {
                vm.UpdateDockExtent(session.ResizeHostId, preview.PreviewExtent);
            }

            session.UpdatePreview(
                current,
                preview.PreviewBounds);

            e.Handled = true;
        }

        private void ResetActiveParentPaneResize(bool commit)
        {
            PaneGestureSessionCoordinator.Finish(ref _activeParentResizeSession, commit);
        }

        private bool ActiveParentResizeOwnsPointer(PointerEventArgs e)
        {
            return PaneGestureSessionGuard.MatchesPointer(_activeParentResizeSession, e);
        }

        private double GetCurrentDockSize(string edge)
        {
            if (DataContext is not MainWindowViewModel vm) return 0.0;
            var norm = MainWindowViewModel.NormalizeHostId(edge);
            return norm switch
            {
                "left"   => vm.CurrentShellLayout.LeftDock?.Bounds.Width   ?? 0.0,
                "right"  => vm.CurrentShellLayout.RightDock?.Bounds.Width  ?? 0.0,
                "top"    => vm.CurrentShellLayout.TopDock?.Bounds.Height   ?? 0.0,
                "bottom" => vm.CurrentShellLayout.BottomDock?.Bounds.Height?? 0.0,
                _ => 0.0
            };
        }
    }
}
