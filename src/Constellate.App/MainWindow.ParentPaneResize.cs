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
            var rootGrid = GetShellRootGrid();
            if (rootGrid is null || DataContext is not MainWindowViewModel vm)
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
                // Live-apply the extent to the grid so the pane resizes immediately during drag.
                // The ViewModel recompute/apply path will follow and re-assert the same sizes.
                ApplyDockExtentToGrid(rootGrid, session.ResizeHostId, preview.PreviewExtent);
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
            var rootGrid = GetShellRootGrid();
            if (rootGrid is null)
            {
                return 0.0;
            }

            return edge switch
            {
                "left" => rootGrid.ColumnDefinitions[0].ActualWidth,
                "right" => rootGrid.ColumnDefinitions[2].ActualWidth,
                "top" => rootGrid.RowDefinitions[0].ActualHeight,
                "bottom" => rootGrid.RowDefinitions[2].ActualHeight,
                _ => 0.0
            };
        }

        private static void ApplyDockExtentToGrid(Grid rootGrid, string edge, double extent)
        {
            var clamped = Math.Max(80.0, extent);
            switch (edge)
            {
                case "left":
                    if (rootGrid.ColumnDefinitions.Count >= 1)
                        rootGrid.ColumnDefinitions[0].Width = new GridLength(clamped, GridUnitType.Pixel);
                    break;
                case "right":
                    if (rootGrid.ColumnDefinitions.Count >= 3)
                        rootGrid.ColumnDefinitions[2].Width = new GridLength(clamped, GridUnitType.Pixel);
                    break;
                case "top":
                    if (rootGrid.RowDefinitions.Count >= 1)
                        rootGrid.RowDefinitions[0].Height = new GridLength(clamped, GridUnitType.Pixel);
                    break;
                case "bottom":
                    if (rootGrid.RowDefinitions.Count >= 3)
                        rootGrid.RowDefinitions[2].Height = new GridLength(clamped, GridUnitType.Pixel);
                    break;
            }
        }
    }
}
