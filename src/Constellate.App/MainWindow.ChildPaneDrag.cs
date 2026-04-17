using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Constellate.App.Controls;
using Constellate.App.Controls.Panes;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        // Auto-slide support while dragging child panes
        private DispatcherTimer? _childAutoSlideTimer;
        private string? _childAutoSlideParentId;
        private int _childAutoSlideDirection; // -1 for previous, +1 for next
        private DateTime _childLastAutoSlideAt;

        private void OnChildPaneHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (sender is not Control originControl || originControl.DataContext is not ChildPaneDescriptor descriptor)
            {
                return;
            }

            if (!CanBeginChildPaneDragFromSender(sender))
            {
                return;
            }

            _isChildPaneDragging = true;
            _childDragStartPoint = e.GetPosition(this);
            _childDragPaneId = descriptor.Id;
            _childDragOriginHostId = DataContext is MainWindowViewModel vm
                ? vm.GetHostIdForChildPane(descriptor.Id)
                : null;

            var originPreviewSize = ResolveChildDragOriginPreviewSize(originControl, descriptor);

            _activeChildDragSession = new ChildPaneDragSession(
                paneId: descriptor.Id,
                pointerId: (long)e.Pointer.Id,
                startPoint: _childDragStartPoint,
                originParentId: descriptor.ParentId,
                originLaneIndex: descriptor.ContainerIndex,
                originSlideIndex: descriptor.SlideIndex,
                originPreviewSize: originPreviewSize);

            try
            {
                e.Pointer.Capture(this);
            }
            catch
            {
            }
        }

        private void OnChildPaneHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isChildPaneDragging)
            {
                return;
            }

            if (!ActiveChildDragOwnsPointer(e))
            {
                return;
            }

            _isChildPaneDragging = false;

            try
            {
                e.Pointer.Capture(null);
            }
            catch
            {
            }

            var paneId = _childDragPaneId;
            if (string.IsNullOrWhiteSpace(paneId) || DataContext is not MainWindowViewModel vm)
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
                vm.MoveChildPaneToHost(paneId, "floating");
                CleanupActiveChildPaneDrag(vm, commit: true);
                return;
            }

            var normalizedOrigin = MainWindowViewModel.NormalizeHostId(_childDragOriginHostId);
            if (!string.Equals(normalizedOrigin, commitResult.TargetHostId, StringComparison.Ordinal))
            {
                vm.MoveChildPaneToHost(paneId, commitResult.TargetHostId);
            }

            if (!string.IsNullOrWhiteSpace(commitResult.TargetParentId))
            {
                vm.PlaceChildInParentLane(
                    paneId,
                    commitResult.TargetParentId,
                    commitResult.TargetLaneIndex,
                    commitResult.TargetInsertIndex);
            }

            CleanupActiveChildPaneDrag(vm, commit: true);
        }

        private void OnChildPaneHeaderPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isChildPaneDragging || DataContext is not MainWindowViewModel vm)
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

            var originPreviewSize = _activeChildDragSession?.OriginPreviewSize ?? new Size(260.0, 160.0);

            var preview = ChildPaneDragGesturePlanner.ComputePreview(
                currentPoint,
                windowSize,
                GetShellFloatingSurfaceRect(),
                originPreviewSize,
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
                _activeChildDragSession?.UpdateFloatingPreview(currentPoint, preview.PreviewBounds);
                StopChildAutoSlide();
                return;
            }

            _activeChildDragSession?.UpdateLaneTarget(
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
                StartChildAutoSlide(preview.TargetParentId, -1);
            }
            else if (autoSlideDirection > 0)
            {
                StartChildAutoSlide(preview.TargetParentId, 1);
            }
            else
            {
                StopChildAutoSlide();
            }
        }

        private void CleanupActiveChildPaneDrag(MainWindowViewModel? vm, bool commit)
        {
            vm?.SetChildPaneDragShadow(false, 0, 0, 0, 0);
            StopChildAutoSlide();

            if (commit)
            {
                _activeChildDragSession?.Commit();
            }
            else
            {
                _activeChildDragSession?.Cancel();
            }

            _activeChildDragSession = null;
            _childDragPaneId = null;
            _childDragOriginHostId = null;
        }

        private bool ActiveChildDragOwnsPointer(PointerEventArgs e)
        {
            return _activeChildDragSession?.MatchesPointer(e) ?? true;
        }

        private static bool CanBeginChildPaneDragFromSender(object? sender)
        {
            var region = PaneChromeInputHelper.ResolveRegion(sender);
            return PaneChromeRegionRules.IsDragOrigin(region);
        }

        private static Size ResolveChildDragOriginPreviewSize(Control originControl, ChildPaneDescriptor descriptor)
        {
            var childView = originControl as ChildPaneView ?? originControl.FindAncestorOfType<ChildPaneView>();
            if (childView is not null)
            {
                var bounds = childView.Bounds;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    return new Size(
                        Math.Max(80.0, bounds.Width),
                        Math.Max(80.0, bounds.Height));
                }
            }

            return new Size(
                Math.Max(80.0, descriptor.FloatingWidth),
                Math.Max(80.0, descriptor.FloatingHeight));
        }

        private void StartChildAutoSlide(string parentId, int direction)
        {
            var now = DateTime.UtcNow;
            if ((now - _childLastAutoSlideAt).TotalMilliseconds < 450 &&
                _childAutoSlideParentId == parentId &&
                _childAutoSlideDirection == direction)
            {
                return;
            }

            _childAutoSlideParentId = parentId;
            _childAutoSlideDirection = direction;
            _childAutoSlideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _childAutoSlideTimer.Tick -= OnChildAutoSlideTick;
            _childAutoSlideTimer.Tick += OnChildAutoSlideTick;
            _childAutoSlideTimer.Stop();
            _childAutoSlideTimer.Start();
        }

        private void StopChildAutoSlide()
        {
            if (_childAutoSlideTimer is null)
            {
                return;
            }

            _childAutoSlideTimer.Stop();
            _childAutoSlideParentId = null;
            _childAutoSlideDirection = 0;
        }

        private void OnChildAutoSlideTick(object? sender, EventArgs e)
        {
            _childAutoSlideTimer?.Stop();
            if (string.IsNullOrWhiteSpace(_childAutoSlideParentId) ||
                _childAutoSlideDirection == 0 ||
                DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var parentId = _childAutoSlideParentId;
            var parent = vm.ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
            if (parent is null)
            {
                return;
            }

            var nextIndex = Math.Clamp(parent.SlideIndex + _childAutoSlideDirection, 0, 2);
            if (nextIndex != parent.SlideIndex)
            {
                vm.SetParentSlideIndex(parent.Id, nextIndex);
                _childLastAutoSlideAt = DateTime.UtcNow;
            }
        }
    }
}
