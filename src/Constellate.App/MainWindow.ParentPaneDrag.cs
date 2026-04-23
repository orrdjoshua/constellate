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

            // If this parent is already floating, bring it to the front immediately
            // so the drag-preview/commit occurs at the top of the floating stack.
            if (DataContext is MainWindowViewModel vm &&
                string.Equals(MainWindowViewModel.NormalizeHostId(parent.HostId), "floating", StringComparison.Ordinal))
            {
                vm.BringFloatingParentToFront(parent.Id);
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

            if (!TryGetParentDragOriginAttachment(out var originAttachment))
            {
                ResetActiveParentPaneDrag(vm, commit: false);
                return;
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

            if (!TryGetParentDragOriginAttachment(out var originAttachment))
            {
                ResetActiveParentPaneDrag(vm, commit: false);
                return;
            }

            // If the origin pane was already floating, align the preview/shadow to preserve the
            // exact pointer offset from the pane's original top-left instead of re-centering.
            var originHost = MainWindowViewModel.NormalizeHostId(originAttachment.ToHostId());
            if (string.Equals(originHost, "floating", System.StringComparison.Ordinal) && session.OriginBounds.Width > 0 && session.OriginBounds.Height > 0)
            {
                var surface = GetShellFloatingSurfaceRect();
                var originTopLeft = new Point(session.OriginBounds.X, session.OriginBounds.Y);
                var offset = new Vector(session.StartPoint.X - originTopLeft.X, session.StartPoint.Y - originTopLeft.Y);

                var w = session.OriginBounds.Width;
                var h = session.OriginBounds.Height;

                var left = currentPoint.X - offset.X;
                var top = currentPoint.Y - offset.Y;

                // Clamp inside floating surface
                left = Math.Clamp(left, surface.X, Math.Max(surface.X, surface.Right - w));
                top = Math.Clamp(top, surface.Y, Math.Max(surface.Y, surface.Bottom - h));

                var aligned = new Rect(left, top, w, h);
                vm.SetParentPaneDragShadow(true, aligned.X, aligned.Y, aligned.Width, aligned.Height);
                session.UpdatePreview(currentPoint, originAttachment, aligned);
                return;
            }

            var preview = ParentPaneMoveGesturePlanner.ComputePreview(
                currentPoint,
                windowSize,
                GetShellFloatingSurfaceRect(),
                originAttachment,
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

        private bool TryGetParentDragOriginAttachment(out DockAttachmentModel attachment)
        {
            // Parent move is now fully pane-centric: the active session origin is the exact ParentPane.Id
            // captured from a pane-owned drag-origin region (header / empty header / empty body).
            // Resolve origin attachment strictly from that pane id; do not interpret host ids here.
            // If the pane cannot be resolved, fail closed instead of silently defaulting to another host.
            var session = _activeParentMoveSession;
            if (session is null || string.IsNullOrWhiteSpace(session.OriginReferenceId))
            {
                attachment = default;
                return false;
            }

            var vm = DataContext as MainWindowViewModel;
            if (vm is not null)
            {
                var parent = vm.ParentPaneModels.FirstOrDefault(p =>
                    string.Equals(p.Id, session.OriginReferenceId, StringComparison.Ordinal));
                if (parent is not null && !string.IsNullOrWhiteSpace(parent.HostId))
                {
                    attachment = DockAttachmentModel.FromHostId(MainWindowViewModel.NormalizeHostId(parent.HostId));
                    return true;
                }
            }

            attachment = default;
            return false;
        }
    }
}
