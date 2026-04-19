using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
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

            var originAttachment = GetParentDragOriginAttachment();
            var commit = ParentPaneMoveGesturePlanner.ComputeCommit(
                releasePoint,
                windowSize,
                GetShellFloatingSurfaceRect(),
                originAttachment,
                previewBounds,
                vm.IsDockHostOccupied);

            if (commit.IsFloating && commit.RelativeFloatingBounds is { } floatingRect)
            {
                // If the origin host was already floating, we are simply repositioning
                // an existing floating parent (which may be minimized). In that case
                // we should not reset minimized state or overwrite stored full-size
                // geometry; only the position changes.
                var originHost = MainWindowViewModel.NormalizeHostId(originAttachment.ToHostId());
                if (string.Equals(originHost, "floating", StringComparison.Ordinal))
                {
                    vm.SetFloatingParentPosition(
                        session.OriginReferenceId,
                        floatingRect.X,
                        floatingRect.Y);
                }
                else
                {
                    // Dock → floating transition: use the existing helper, which clears
                    // minimization and seeds full floating geometry from the drag shadow.
                    vm.MoveParentPaneToFloating(
                        session.OriginReferenceId,
                        floatingRect.X,
                        floatingRect.Y,
                        floatingRect.Width,
                        floatingRect.Height);
                }
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
            // Session-first rule: derive the origin host strictly from the session start reference.
            // OriginReferenceId is either:
            //   - a dock host id: "left" | "top" | "right" | "bottom" | "floating"
            //   - or a specific ParentPane.Id (when started from a pane header)
            //
            // We never re-resolve pane/host identity ambiguously; we only map the exact reference captured at start.
            var session = _activeParentMoveSession;
            if (session is null || string.IsNullOrWhiteSpace(session.OriginReferenceId))
            {
                return DockAttachmentModel.FromHostId("left");
            }

            var refId = session.OriginReferenceId;
            // If refId is itself a valid host id, use it directly.
            var normalized = MainWindowViewModel.NormalizeHostId(refId);
            if (string.Equals(refId, normalized, StringComparison.Ordinal))
            {
                return DockAttachmentModel.FromHostId(normalized);
            }

            // Otherwise, refId is a specific parent pane Id; resolve that parent's current HostId.
            var vm = DataContext as MainWindowViewModel;
            if (vm is not null)
            {
                var parent = vm.ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, refId, StringComparison.Ordinal));
                if (parent is not null && !string.IsNullOrWhiteSpace(parent.HostId))
                {
                    return DockAttachmentModel.FromHostId(MainWindowViewModel.NormalizeHostId(parent.HostId));
                }
            }

            return DockAttachmentModel.FromHostId("left");
        }
    }
}
