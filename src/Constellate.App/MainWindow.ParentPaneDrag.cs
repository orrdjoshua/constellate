using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Constellate.App.Infrastructure.Panes;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        private bool TryBeginHostParentPaneDrag(object? sender, PointerPressedEventArgs e)
        {
            if (!CanBeginPaneGesture(e))
            {
                return false;
            }

            var dragOriginHostId = ParentPaneDragStateResolver.ResolveHostIdFromHostControl(sender as Control);
            if (string.IsNullOrWhiteSpace(dragOriginHostId))
            {
                return false;
            }

            var parent = ParentPaneDragStateResolver.TryResolveDragParentPane(
                DataContext as MainWindowViewModel,
                dragOriginHostId);

            if (parent is null)
            {
                ResetActiveParentPaneDrag(vm: null, commit: false);
                return false;
            }

            return BeginParentPaneMoveSession(
                parent,
                dragOriginHostId,
                e,
                markHandled: false);
        }

        private bool TryBeginParentPaneDrag(object? sender, PointerPressedEventArgs e)
        {
            if (!CanBeginPaneGesture(e))
            {
                return false;
            }

            if (sender is not Control originControl || originControl.DataContext is not ParentPaneModel parent)
            {
                return false;
            }

            return BeginParentPaneMoveSession(
                parent,
                parent.Id,
                e,
                markHandled: true);
        }

        private bool BeginParentPaneMoveSession(
            ParentPaneModel parent,
            string dragOriginId,
            PointerPressedEventArgs e,
            bool markHandled)
        {
            var startPoint = e.GetPosition(this);

            var originBounds = ParentPaneDragStateResolver.GetParentPaneCurrentBounds(
                parent,
                GetShellHostRect,
                GetShellFloatingSurfaceRect);

            var session = PaneGestureSessionFactory.CreateParentMoveSession(
                paneId: parent.Id,
                originReferenceId: dragOriginId,
                originHostId: parent.HostId,
                pointerId: (long)e.Pointer.Id,
                startPoint: startPoint,
                originBounds: originBounds);

            return PaneGestureSessionCoordinator.Start(
                ref _activeParentMoveSession,
                session,
                e,
                TryCaptureWindowPointer,
                markHandled);
        }

        private void CompleteActiveParentPaneDrag(PointerReleasedEventArgs e)
        {
            var session = _activeParentMoveSession;
            if (session is null)
            {
                return;
            }

            ReleaseWindowPointer(e);

            if (DataContext is not MainWindowViewModel vm)
            {
                ResetActiveParentPaneDrag(vm: null, commit: false);
                return;
            }

            var releasePoint = e.GetPosition(this);
            var windowSize = new Size(Bounds.Width, Bounds.Height);
            if (windowSize.Width <= 0 || windowSize.Height <= 0 || string.IsNullOrWhiteSpace(session.OriginReferenceId))
            {
                ResetActiveParentPaneDrag(vm, commit: false);
                return;
            }

            Rect? previewBounds = null;
            if (session.PreviewBounds.Width > 0 &&
                session.PreviewBounds.Height > 0)
            {
                previewBounds = session.PreviewBounds;
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
                GetParentDragOriginAttachment(),
                previewBounds,
                vm.IsDockHostOccupied);

            if (commit.IsFloating && commit.RelativeFloatingBounds is { } floatingRect)
            {
                vm.MoveParentPaneToFloating(
                    session.OriginReferenceId,
                    floatingRect.X,
                    floatingRect.Y,
                    floatingRect.Width,
                    floatingRect.Height);
            }
            else
            {
                vm.MoveParentPaneToHost(session.OriginReferenceId, commit.TargetHostId);
            }

            ResetActiveParentPaneDrag(vm, commit: true);
        }

        private void UpdateActiveParentPaneDragPreview(Point currentPoint)
        {
            var session = _activeParentMoveSession;
            if (session is null || DataContext is not MainWindowViewModel vm)
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
                session.OriginBounds,
                vm.IsDockHostOccupied);

            vm.SetParentPaneDragShadow(
                true,
                preview.PreviewBounds.X,
                preview.PreviewBounds.Y,
                preview.PreviewBounds.Width,
                preview.PreviewBounds.Height);

            session.UpdatePreview(
                currentPoint,
                preview.PreviewAttachment,
                preview.PreviewBounds);
        }

        private void ResetActiveParentPaneDrag(MainWindowViewModel? vm, bool commit)
        {
            vm?.SetParentPaneDragShadow(false, 0, 0, 0, 0);
            PaneGestureSessionCoordinator.Finish(ref _activeParentMoveSession, commit);
        }

        private bool ActiveParentMoveOwnsPointer(PointerEventArgs e)
        {
            return PaneGestureSessionGuard.MatchesPointer(_activeParentMoveSession, e);
        }

        private DockAttachmentModel GetParentDragOriginAttachment()
        {
            var originReferenceId = _activeParentMoveSession?.OriginReferenceId;
            var resolvedParent = ParentPaneDragStateResolver.TryResolveDragParentPane(
                DataContext as MainWindowViewModel,
                originReferenceId);

            return ParentPaneDragStateResolver.ResolveOriginAttachment(
                _activeParentMoveSession,
                resolvedParent,
                originReferenceId);
        }
    }
}
