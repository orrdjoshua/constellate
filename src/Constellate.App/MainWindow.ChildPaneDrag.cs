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
            var floatingSurfaceRect = GetShellFloatingSurfaceRect();

            var preview = ChildPaneDragGesturePlanner.ComputePreview(
                currentPoint,
                floatingSurfaceRect,
                session.OriginPreviewSize,
                BuildChildDockParentCandidates(),
                vm.GetChildrenCountInLaneForCurrentSlide,
                vm.GetChildrenInLaneForCurrentSlide);

            vm.SetChildPaneDragShadow(
                true,
                preview.PreviewBounds.X,
                preview.PreviewBounds.Y,
                preview.PreviewBounds.Width,
                preview.PreviewBounds.Height);

            if (preview.IsFloatingPreview)
            {
                // No dock highlight in floating preview
                vm.SetChildDockTargetHighlight(false, 0, 0, 0, 0);
                var alignedRect = preview.PreviewBounds;

                var child = vm.ChildPanes.FirstOrDefault(c => string.Equals(c.Id, session.PaneId, StringComparison.Ordinal));
                if (child is not null && child.ParentId is null)
                {
                    var originTopLeft = new Point(floatingSurfaceRect.X + child.FloatingX, floatingSurfaceRect.Y + child.FloatingY);
                    var offset = new Vector(session.StartPoint.X - originTopLeft.X, session.StartPoint.Y - originTopLeft.Y);
                    var width = Math.Max(1.0, child.FloatingWidth);
                    var height = Math.Max(1.0, child.FloatingHeight);

                    var left = currentPoint.X - offset.X;
                    var top = currentPoint.Y - offset.Y;

                    left = Math.Clamp(left, floatingSurfaceRect.X, Math.Max(floatingSurfaceRect.X, floatingSurfaceRect.Right - width));
                    top = Math.Clamp(top, floatingSurfaceRect.Y, Math.Max(floatingSurfaceRect.Y, floatingSurfaceRect.Bottom - height));

                    alignedRect = new Rect(left, top, width, height);
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
                vm.SetChildDockTargetHighlight(false, 0, 0, 0, 0);
                StopChildAutoSlide();
                return;
            }

            var targetParent = vm.ParentPaneModels.FirstOrDefault(parent =>
                string.Equals(parent.Id, preview.TargetParentId, StringComparison.Ordinal));

            if (targetParent is null)
            {
                vm.SetChildDockTargetHighlight(false, 0, 0, 0, 0);
                StopChildAutoSlide();
                return;
            }

            // Show overlay highlight for the entire target parent body area during re-dock targeting
            var targetBounds = GetParentPaneBounds(targetParent);
            vm.SetChildDockTargetHighlight(
                true,
                targetBounds.X,
                targetBounds.Y,
                targetBounds.Width,
                targetBounds.Height);

            var autoSlideDirection = ChildPaneDragGesturePlanner.ResolveAutoSlideDirection(
                targetParent,
                GetParentPaneBounds(targetParent),
                currentPoint);

            if (autoSlideDirection < 0)
            {
                RequestChildAutoSlide(targetParent.Id, -1);
            }
            else if (autoSlideDirection > 0)
            {
                RequestChildAutoSlide(targetParent.Id, 1);
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

            var commitResult = ChildPaneDragGesturePlanner.ComputeCommit(
                e.GetPosition(this),
                BuildChildDockParentCandidates(),
                vm.GetChildrenCountInLaneForCurrentSlide,
                vm.GetChildrenInLaneForCurrentSlide);

            if (commitResult.IsFloating)
            {
                vm.SetChildDockTargetHighlight(false, 0, 0, 0, 0);
                vm.MoveChildPaneToFloating(session.PaneId);
                CleanupActiveChildPaneDrag(vm, commit: true);
                return;
            }

            if (!string.IsNullOrWhiteSpace(commitResult.TargetParentId))
            {
                vm.DockChildPaneToParent(
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
            vm?.SetChildDockTargetHighlight(false, 0, 0, 0, 0);
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

        private ChildPaneDockParentCandidate[] BuildChildDockParentCandidates()
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return Array.Empty<ChildPaneDockParentCandidate>();
            }

            return vm.ParentPaneModels
                .Where(parent => !parent.IsMinimized)
                .Select(parent => new ChildPaneDockParentCandidate(
                    parent,
                    GetParentPaneBounds(parent)))
                .Where(candidate => candidate.Bounds.Width > 0 && candidate.Bounds.Height > 0)
                .ToArray();
        }

        private Rect GetParentPaneBounds(ParentPaneModel parent)
        {
            return ParentPaneDragStateResolver.GetParentPaneCurrentBounds(
                parent,
                GetShellHostRect,
                GetShellFloatingSurfaceRect);
        }
    }
}
