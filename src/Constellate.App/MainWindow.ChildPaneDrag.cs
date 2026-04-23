using System;
using Avalonia;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        private readonly ChildPaneAutoSlideController _childAutoSlideController = new();

        private bool TryBeginChildPaneDrag(object? sender, PointerPressedEventArgs e)
        {
            if (!CanBeginPaneGesture(e))
            {
                return false;
            }

            if (sender is not Control originControl || originControl.DataContext is not ChildPaneDescriptor descriptor)
            {
                return false;
            }

            // If the child is already floating, bring it to front before starting drag.
            if (DataContext is MainWindowViewModel vm && descriptor.ParentId is null)
            {
                vm.BringFloatingChildToFront(descriptor.Id);
            }

            return BeginChildPaneDragSession(originControl, descriptor, e);
        }

        private bool BeginChildPaneDragSession(
            Control originControl,
            ChildPaneDescriptor descriptor,
            PointerPressedEventArgs e)
        {
            var startPoint = e.GetPosition(this);
            var originHostId = ChildPaneDragStateResolver.ResolveOriginHostId(
                DataContext as MainWindowViewModel,
                descriptor);

            var originPreviewSize = ChildPaneDragStateResolver.ResolveOriginPreviewSize(
                originControl,
                descriptor);

            var session = PaneGestureSessionFactory.CreateChildDragSession(
                paneId: descriptor.Id,
                originParentId: descriptor.ParentId,
                originHostId: originHostId,
                originLaneIndex: descriptor.ContainerIndex,
                originSlideIndex: descriptor.SlideIndex,
                originPreviewSize: originPreviewSize,
                pointerId: (long)e.Pointer.Id,
                startPoint: startPoint);

            return PaneGestureSessionCoordinator.Start(
                ref _activeChildDragSession,
                session,
                e,
                TryCaptureWindowPointer);
        }

        private void UpdateActiveChildPaneDrag(PointerEventArgs e)
        {
            var session = _activeChildDragSession;
            if (session is null || DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            if (!ActiveChildDragOwnsPointer(e))
            {
                return;
            }

            var currentPoint = e.GetPosition(this);
            var windowSize = new Size(Bounds.Width, Bounds.Height);
            if (windowSize.Width <= 0 || windowSize.Height <= 0)
            {
                vm.SetChildPaneDragShadow(false, 0, 0, 0, 0);
                StopChildAutoSlide();
                return;
            }

            var preview = ChildPaneDragGesturePlanner.ComputePreview(
                currentPoint,
                windowSize,
                GetShellFloatingSurfaceRect(),
                session.OriginPreviewSize,
                GetShellHostRect,
                vm.GetFirstExpandedParentOnHost,
                vm.GetChildrenCountInLaneForCurrentSlide);

            vm.SetChildPaneDragShadow(
                true,
                preview.PreviewBounds.X,
                preview.PreviewBounds.Y,
                preview.PreviewBounds.Width,
                preview.PreviewBounds.Height);

            if (preview.IsFloatingPreview)
            {
                // Align the floating child preview to preserve the pointer's offset from the child's
                // original top-left instead of re-centering the preview under the cursor.
                var surface = GetShellFloatingSurfaceRect();
                Rect alignedRect = preview.PreviewBounds;

                // We can compute the child's origin absolute rect from VM (paneId captured in session)
                // using current floating geometry.
                var child = vm.ChildPanes.FirstOrDefault(c => string.Equals(c.Id, session.PaneId, StringComparison.Ordinal));
                if (child is not null)
                {
                    var originTopLeft = new Point(surface.X + child.FloatingX, surface.Y + child.FloatingY);
                    var offset = new Vector(session.StartPoint.X - originTopLeft.X, session.StartPoint.Y - originTopLeft.Y);
                    var w = Math.Max(1.0, child.FloatingWidth);
                    var h = Math.Max(1.0, child.FloatingHeight);

                    var left = currentPoint.X - offset.X;
                    var top = currentPoint.Y - offset.Y;

                    left = Math.Clamp(left, surface.X, Math.Max(surface.X, surface.Right - w));
                    top = Math.Clamp(top, surface.Y, Math.Max(surface.Y, surface.Bottom - h));

                    alignedRect = new Rect(left, top, w, h);
                }

                vm.SetChildPaneDragShadow(true, alignedRect.X, alignedRect.Y, alignedRect.Width, alignedRect.Height);
                session.UpdateFloatingPreview(currentPoint, alignedRect);
                StopChildAutoSlide();
                return;
            }

            session.UpdateLaneTarget(
                currentPoint,
                preview.TargetParentId,
                preview.TargetLaneIndex,
                preview.TargetSlideIndex,
                preview.TargetInsertIndex);

            if (string.IsNullOrWhiteSpace(preview.TargetParentId))
            {
                StopChildAutoSlide();
                return;
            }

            var autoSlideDirection = ChildPaneDragGesturePlanner.ResolveAutoSlideDirection(
                preview.TargetHostId,
                GetShellHostRect(preview.TargetHostId),
                currentPoint,
                preview.TargetSlideIndex);

            if (autoSlideDirection < 0)
            {
                RequestChildAutoSlide(preview.TargetParentId, -1);
            }
            else if (autoSlideDirection > 0)
            {
                RequestChildAutoSlide(preview.TargetParentId, 1);
            }
            else
            {
                StopChildAutoSlide();
            }
        }

        private void CompleteActiveChildPaneDrag(PointerReleasedEventArgs e)
        {
            var session = _activeChildDragSession;
            if (session is null)
            {
                return;
            }

            if (!ActiveChildDragOwnsPointer(e))
            {
                return;
            }

            ReleaseWindowPointer(e);

            if (string.IsNullOrWhiteSpace(session.PaneId) || DataContext is not MainWindowViewModel vm)
            {
                CleanupActiveChildPaneDrag(vm: null, commit: false);
                return;
            }

            var windowSize = new Size(Bounds.Width, Bounds.Height);
            if (windowSize.Width <= 0 || windowSize.Height <= 0)
            {
                CleanupActiveChildPaneDrag(vm, commit: false);
                return;
            }

            var commitResult = ChildPaneDragGesturePlanner.ComputeCommit(
                e.GetPosition(this),
                windowSize,
                GetShellHostRect,
                vm.GetFirstExpandedParentOnHost,
                vm.GetChildrenCountInLaneForCurrentSlide);

            if (commitResult is null)
            {
                CleanupActiveChildPaneDrag(vm, commit: false);
                return;
            }

            if (commitResult.IsFloating)
            {
                vm.MoveChildPaneToHost(session.PaneId, "floating");
                CleanupActiveChildPaneDrag(vm, commit: true);
                return;
            }

            var normalizedOrigin = MainWindowViewModel.NormalizeHostId(session.OriginHostId);
            if (!string.Equals(normalizedOrigin, commitResult.TargetHostId, StringComparison.Ordinal))
            {
                vm.MoveChildPaneToHost(session.PaneId, commitResult.TargetHostId);
            }

            if (!string.IsNullOrWhiteSpace(commitResult.TargetParentId))
            {
                vm.PlaceChildInParentLane(
                    session.PaneId,
                    commitResult.TargetParentId,
                    commitResult.TargetLaneIndex,
                    commitResult.TargetInsertIndex);
            }

            CleanupActiveChildPaneDrag(vm, commit: true);
        }

        private void CleanupActiveChildPaneDrag(MainWindowViewModel? vm, bool commit)
        {
            vm?.SetChildPaneDragShadow(false, 0, 0, 0, 0);
            StopChildAutoSlide();
            PaneGestureSessionCoordinator.Finish(ref _activeChildDragSession, commit);
        }

        private bool ActiveChildDragOwnsPointer(PointerEventArgs e)
        {
            return PaneGestureSessionGuard.MatchesPointer(_activeChildDragSession, e);
        }

        private void RequestChildAutoSlide(string parentId, int direction)
        {
            _childAutoSlideController.Request(
                parentId,
                direction,
                AdvanceChildAutoSlide);
        }

        private void StopChildAutoSlide()
        {
            _childAutoSlideController.Stop();
        }

        private void AdvanceChildAutoSlide(string parentId, int direction)
        {
            if (string.IsNullOrWhiteSpace(parentId) ||
                direction == 0 ||
                DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var parent = vm.ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
            if (parent is null)
            {
                return;
            }

            var nextIndex = Math.Clamp(parent.SlideIndex + direction, 0, 2);
            if (nextIndex != parent.SlideIndex)
            {
                vm.SetParentSlideIndex(parent.Id, nextIndex);
            }
        }
    }
}
