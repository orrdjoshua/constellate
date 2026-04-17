using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Constellate.App.Controls.Panes;
using Constellate.App.Infrastructure.Panes;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        private void ShellPaneHost_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_isPaneResizing)
            {
                return;
            }

            if (!ActiveParentMoveOwnsPointer(e))
            {
                return;
            }

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isShellPaneDragging = true;
                _shellDragStartPoint = e.GetPosition(this);

                if (sender is Control control)
                {
                    _dragOriginHostId = control.Name switch
                    {
                        "LeftPaneHost" => "left",
                        "TopPaneHost" => "top",
                        "RightPaneHost" => "right",
                        "BottomPaneHost" => "bottom",
                        "FloatingPaneHost" => "floating",
                        _ => null
                    };
                }

                var parent = TryResolveDragParentPane(_dragOriginHostId);
                if (parent is not null)
                {
                    _activeParentMoveSession = new ParentPaneMoveSession(
                        paneId: parent.Id,
                        pointerId: (long)e.Pointer.Id,
                        startPoint: _shellDragStartPoint,
                        originAttachment: DockAttachmentModel.FromHostId(parent.HostId),
                        originBounds: GetParentPaneCurrentBounds(parent));
                }
                else
                {
                    _activeParentMoveSession = null;
                }
            }
        }

        private void ShellPaneHost_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isShellPaneDragging)
            {
                return;
            }

            if (!ActiveParentMoveOwnsPointer(e))
            {
                return;
            }

            CompleteActiveParentPaneDrag(e);
        }

        private void OnParentPaneHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_isPaneResizing)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (sender is not Control header || header.DataContext is not ParentPaneModel parent)
            {
                return;
            }

            if (!CanBeginParentPaneDragFromSender(sender))
            {
                return;
            }

            _isShellPaneDragging = true;
            _shellDragStartPoint = e.GetPosition(this);
            _dragOriginHostId = parent.Id;
            _activeParentMoveSession = new ParentPaneMoveSession(
                paneId: parent.Id,
                pointerId: (long)e.Pointer.Id,
                startPoint: _shellDragStartPoint,
                originAttachment: DockAttachmentModel.FromHostId(parent.HostId),
                originBounds: GetParentPaneCurrentBounds(parent));

            try
            {
                e.Pointer.Capture(this);
            }
            catch
            {
            }

            e.Handled = true;
        }

        private void OnParentPaneHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isShellPaneDragging)
            {
                return;
            }

            if (!ActiveParentMoveOwnsPointer(e))
            {
                return;
            }

            CompleteActiveParentPaneDrag(e);
            e.Handled = true;
        }

        private void OnParentPaneHeaderPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isPaneResizing)
            {
                return;
            }

            if (!_isShellPaneDragging)
            {
                return;
            }

            UpdateActiveParentPaneDragPreview(e.GetPosition(this));
            e.Handled = true;
        }

        private void ShellPaneHost_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isPaneResizing)
            {
                return;
            }

            if (!_isShellPaneDragging)
            {
                return;
            }

            if (!ActiveParentMoveOwnsPointer(e))
            {
                return;
            }

            UpdateActiveParentPaneDragPreview(e.GetPosition(this));
        }

        private void CompleteActiveParentPaneDrag(PointerReleasedEventArgs e)
        {
            _isShellPaneDragging = false;

            try
            {
                e.Pointer.Capture(null);
            }
            catch
            {
            }

            if (DataContext is not MainWindowViewModel vm)
            {
                _activeParentMoveSession?.Cancel();
                _activeParentMoveSession = null;
                _dragOriginHostId = null;
                return;
            }

            var releasePoint = e.GetPosition(this);
            var windowSize = new Size(Bounds.Width, Bounds.Height);

            if (windowSize.Width <= 0 || windowSize.Height <= 0 || string.IsNullOrWhiteSpace(_dragOriginHostId))
            {
                vm.SetParentPaneDragShadow(false, 0, 0, 0, 0);
                _activeParentMoveSession?.Cancel();
                _activeParentMoveSession = null;
                _dragOriginHostId = null;
                return;
            }

            var originAttachment = GetParentDragOriginAttachment();

            Rect? previewBounds = null;
            if (_activeParentMoveSession is not null &&
                _activeParentMoveSession.PreviewBounds.Width > 0 &&
                _activeParentMoveSession.PreviewBounds.Height > 0)
            {
                previewBounds = _activeParentMoveSession.PreviewBounds;
            }
            else if (vm.ParentPaneDragShadowWidth > 0 && vm.ParentPaneDragShadowHeight > 0)
            {
                previewBounds = new Rect(
                    vm.ParentPaneDragShadowLeft,
                    vm.ParentPaneDragShadowTop,
                    vm.ParentPaneDragShadowWidth,
                    vm.ParentPaneDragShadowHeight);
            }

            var commit = ParentPaneMoveGesturePlanner.ComputeCommit(
                releasePoint,
                windowSize,
                GetShellFloatingSurfaceRect(),
                originAttachment,
                previewBounds,
                vm.IsDockHostOccupied);

            if (commit.IsFloating && commit.RelativeFloatingBounds is { } floatingRect)
            {
                try
                {
                    Console.WriteLine(
                        $"[FloatingDrop] originHost={_dragOriginHostId} rel=({floatingRect.X:0},{floatingRect.Y:0},{floatingRect.Width:0},{floatingRect.Height:0})"
                    );
                }
                catch
                {
                }

                vm.MoveParentPaneToFloating(
                    _dragOriginHostId,
                    floatingRect.X,
                    floatingRect.Y,
                    floatingRect.Width,
                    floatingRect.Height);
            }
            else
            {
                vm.MoveParentPaneToHost(_dragOriginHostId, commit.TargetHostId);
            }

            vm.SetParentPaneDragShadow(false, 0, 0, 0, 0);
            _activeParentMoveSession?.Commit();
            _activeParentMoveSession = null;
            _dragOriginHostId = null;
        }

        private void UpdateActiveParentPaneDragPreview(Point currentPoint)
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var windowSize = new Size(Bounds.Width, Bounds.Height);
            if (windowSize.Width <= 0 || windowSize.Height <= 0)
            {
                vm.SetParentPaneDragShadow(false, 0, 0, 0, 0);
                return;
            }

            var preview = ParentPaneMoveGesturePlanner.ComputePreview(
                currentPoint,
                windowSize,
                GetShellFloatingSurfaceRect(),
                GetParentDragOriginAttachment(),
                _activeParentMoveSession?.OriginBounds,
                vm.IsDockHostOccupied);

            vm.SetParentPaneDragShadow(
                true,
                preview.PreviewBounds.X,
                preview.PreviewBounds.Y,
                preview.PreviewBounds.Width,
                preview.PreviewBounds.Height);

            _activeParentMoveSession?.UpdatePreview(
                currentPoint,
                preview.PreviewAttachment,
                preview.PreviewBounds);
        }

        private bool ActiveParentMoveOwnsPointer(PointerEventArgs e)
        {
            return _activeParentMoveSession?.MatchesPointer(e) ?? true;
        }

        private static bool CanBeginParentPaneDragFromSender(object? sender)
        {
            var region = PaneChromeInputHelper.ResolveRegion(sender);
            return PaneChromeRegionRules.IsDragOrigin(region);
        }

        private ParentPaneModel? TryResolveDragParentPane(string? originHostOrPaneId)
        {
            if (DataContext is not MainWindowViewModel vm || string.IsNullOrWhiteSpace(originHostOrPaneId))
            {
                return null;
            }

            return vm.ParentPaneModels.FirstOrDefault(parent =>
                       string.Equals(parent.Id, originHostOrPaneId, StringComparison.Ordinal)) ??
                   vm.ParentPaneModels.FirstOrDefault(parent =>
                       string.Equals(MainWindowViewModel.NormalizeHostId(parent.HostId), MainWindowViewModel.NormalizeHostId(originHostOrPaneId), StringComparison.Ordinal));
        }

        private Rect GetParentPaneCurrentBounds(ParentPaneModel parent)
        {
            if (string.Equals(MainWindowViewModel.NormalizeHostId(parent.HostId), "floating", StringComparison.Ordinal))
            {
                var floatingRect = GetShellFloatingSurfaceRect();
                return new Rect(
                    floatingRect.X + parent.FloatingX,
                    floatingRect.Y + parent.FloatingY,
                    parent.FloatingWidth,
                    parent.FloatingHeight);
            }

            return GetShellHostRect(parent.HostId);
        }

        private DockAttachmentModel GetParentDragOriginAttachment()
        {
            if (_activeParentMoveSession is not null)
            {
                return _activeParentMoveSession.OriginAttachment;
            }

            var parent = TryResolveDragParentPane(_dragOriginHostId);
            return parent is not null
                ? DockAttachmentModel.FromHostId(parent.HostId)
                : DockAttachmentModel.FromHostId(_dragOriginHostId);
        }
    }
}
