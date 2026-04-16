using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

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

            if (sender is not Control header || header.DataContext is not ChildPaneDescriptor descriptor)
            {
                return;
            }

            _isChildPaneDragging = true;
            _childDragStartPoint = e.GetPosition(this);
            _childDragPaneId = descriptor.Id;
            _childDragOriginHostId = DataContext is MainWindowViewModel vm
                ? vm.GetHostIdForChildPane(descriptor.Id)
                : null;

            try { e.Pointer.Capture(this); } catch { }
        }

        private void OnChildPaneHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isChildPaneDragging)
            {
                return;
            }

            _isChildPaneDragging = false;

            try { e.Pointer.Capture(null); } catch { }

            var paneId = _childDragPaneId;
            var originHost = _childDragOriginHostId;
            _childDragPaneId = null;
            _childDragOriginHostId = null;

            if (string.IsNullOrWhiteSpace(paneId) || DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var releasePoint = e.GetPosition(this);
            var width = Bounds.Width;
            var height = Bounds.Height;
            var normalizedTarget = MainWindowViewModel.NormalizeHostId(GetTargetHostForPoint(releasePoint, width, height));
            if (width <= 0 || height <= 0)
            {
                return;
            }

            // Floating: keep existing behavior
            if (string.Equals(normalizedTarget, "floating", StringComparison.Ordinal))
            {
                vm.MoveChildPaneToHost(paneId, "floating");
                return;
            }

            // Find the active parent on the drop host
            var parent = vm.GetFirstExpandedParentOnHost(normalizedTarget);
            if (parent is null)
            {
                // No parent available; nothing to do
                return;
            }

            // Determine lane index based on host orientation and pointer position
            // For Left/Right: lanes are columns side-by-side → use X to pick lane.
            // For Top/Bottom: lanes are rows stacked → use Y to pick lane.
            var host = normalizedTarget switch
            {
                "left" => this.FindControl<Border>("LeftPaneHost"),
                "top" => this.FindControl<Border>("TopPaneHost"),
                "right" => this.FindControl<Border>("RightPaneHost"),
                "bottom" => this.FindControl<Border>("BottomPaneHost"),
                _ => null
            };
            var hostRect = host is not null ? host.Bounds : Bounds;
            var splitCount = Math.Max(1, Math.Min(3, parent.SplitCount));
            int laneIndex;
            if (string.Equals(normalizedTarget, "left", StringComparison.Ordinal) ||
                string.Equals(normalizedTarget, "right", StringComparison.Ordinal))
            {
                var relX = Math.Clamp((releasePoint.X - hostRect.X) / Math.Max(1.0, hostRect.Width), 0.0, 1.0);
                laneIndex = Math.Clamp((int)Math.Floor(relX * splitCount), 0, splitCount - 1);
            }
            else
            {
                var relY = Math.Clamp((releasePoint.Y - hostRect.Y) / Math.Max(1.0, hostRect.Height), 0.0, 1.0);
                laneIndex = Math.Clamp((int)Math.Floor(relY * splitCount), 0, splitCount - 1);
            }

            // Determine insertion index along the free dimension
            // Left/Right → vertical flow; Top/Bottom → horizontal flow
            int insertIndex;
            if (string.Equals(normalizedTarget, "left", StringComparison.Ordinal) ||
                string.Equals(normalizedTarget, "right", StringComparison.Ordinal))
            {
                var relY = Math.Clamp((releasePoint.Y - hostRect.Y) / Math.Max(1.0, hostRect.Height), 0.0, 1.0);
                var count = vm.GetChildrenCountInLaneForCurrentSlide(parent.Id, laneIndex);
                insertIndex = Math.Clamp((int)Math.Floor(relY * (count + 1)), 0, Math.Max(0, count));
            }
            else
            {
                var relX = Math.Clamp((releasePoint.X - hostRect.X) / Math.Max(1.0, hostRect.Width), 0.0, 1.0);
                var count = vm.GetChildrenCountInLaneForCurrentSlide(parent.Id, laneIndex);
                insertIndex = Math.Clamp((int)Math.Floor(relX * (count + 1)), 0, Math.Max(0, count));
            }

            // If the child originated on a different host, attach it first, then place it into the lane.
            var normalizedOrigin = MainWindowViewModel.NormalizeHostId(originHost);
            if (!string.Equals(normalizedOrigin, normalizedTarget, StringComparison.Ordinal))
            {
                vm.MoveChildPaneToHost(paneId, normalizedTarget);
            }

            vm.PlaceChildInParentLane(paneId, parent.Id, laneIndex, insertIndex);

            // Clear insertion preview and stop any pending auto-slide cadence
            vm.SetChildPaneDragShadow(false, 0, 0, 0, 0);
            StopChildAutoSlide();
        }

        private void OnChildPaneHeaderPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isChildPaneDragging)
            {
                return;
            }

            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var currentPoint = e.GetPosition(this);
            var windowWidth = Bounds.Width;
            var windowHeight = Bounds.Height;
            if (windowWidth <= 0 || windowHeight <= 0)
            {
                vm.SetChildPaneDragShadow(false, 0, 0, 0, 0);
                StopChildAutoSlide();
                return;
            }

            // Determine target host under pointer
            var targetHost = GetTargetHostForPoint(currentPoint, windowWidth, windowHeight);

            // Determine parent on target host (expanded), lane index, and insertion index
            var parent = vm.GetFirstExpandedParentOnHost(targetHost);
            if (parent is null)
            {
                // No expanded parent on this host; fall back to generic host preview
                ComputeChildPaneDragShadowRect(targetHost, currentPoint, out var l, out var t, out var w, out var h);
                vm.SetChildPaneDragShadow(true, l, t, w, h);
                StopChildAutoSlide();
                return;
            }

            var splitCount = Math.Max(1, Math.Min(3, parent.SplitCount));
            var hostBorder = targetHost switch
            {
                "left" => this.FindControl<Border>("LeftPaneHost"),
                "top" => this.FindControl<Border>("TopPaneHost"),
                "right" => this.FindControl<Border>("RightPaneHost"),
                "bottom" => this.FindControl<Border>("BottomPaneHost"),
                _ => null
            };
            var hostRect = hostBorder is not null ? hostBorder.Bounds : Bounds;

            // Lane index by orientation
            int laneIndex;
            bool isVerticalFlow = string.Equals(targetHost, "left", StringComparison.Ordinal) ||
                                  string.Equals(targetHost, "right", StringComparison.Ordinal);
            if (isVerticalFlow)
            {
                var relX = Math.Clamp((currentPoint.X - hostRect.X) / Math.Max(1.0, hostRect.Width), 0.0, 1.0);
                laneIndex = Math.Clamp((int)Math.Floor(relX * splitCount), 0, splitCount - 1);
            }
            else
            {
                var relY = Math.Clamp((currentPoint.Y - hostRect.Y) / Math.Max(1.0, hostRect.Height), 0.0, 1.0);
                laneIndex = Math.Clamp((int)Math.Floor(relY * splitCount), 0, splitCount - 1);
            }

            // Compute insertion index using simple proportional mapping (deck-of-cards approximation)
            var countInLane = vm.GetChildrenCountInLaneForCurrentSlide(parent.Id, laneIndex);
            int insertIndex;
            if (isVerticalFlow)
            {
                var relY = Math.Clamp((currentPoint.Y - hostRect.Y) / Math.Max(1.0, hostRect.Height), 0.0, 1.0);
                insertIndex = Math.Clamp((int)Math.Floor(relY * (countInLane + 1)), 0, Math.Max(0, countInLane));
            }
            else
            {
                var relX = Math.Clamp((currentPoint.X - hostRect.X) / Math.Max(1.0, hostRect.Width), 0.0, 1.0);
                insertIndex = Math.Clamp((int)Math.Floor(relX * (countInLane + 1)), 0, Math.Max(0, countInLane));
            }

            // Build a thin preview bar at the computed insertion point inside the lane
            ComputeChildInsertPreviewRect(targetHost, hostRect, laneIndex, insertIndex, splitCount, out var left, out var top, out var width, out var height);
            vm.SetChildPaneDragShadow(true, left, top, width, height);

            // Auto-slide on edge-hover with dwell delay (500ms)
            const double edgeFrac = 0.08; // 8% near edge triggers
            var canSlidePrev = parent.SlideIndex > 0;
            var canSlideNext = parent.SlideIndex < 2;
            bool nearPrevEdge, nearNextEdge;
            if (isVerticalFlow)
            {
                var relX = Math.Clamp((currentPoint.X - hostRect.X) / Math.Max(1.0, hostRect.Width), 0.0, 1.0);
                nearPrevEdge = relX <= edgeFrac;
                nearNextEdge = relX >= (1.0 - edgeFrac);
            }
            else
            {
                var relY = Math.Clamp((currentPoint.Y - hostRect.Y) / Math.Max(1.0, hostRect.Height), 0.0, 1.0);
                nearPrevEdge = relY <= edgeFrac;
                nearNextEdge = relY >= (1.0 - edgeFrac);
            }

            if (nearPrevEdge && canSlidePrev)
            {
                StartChildAutoSlide(parent.Id, -1);
            }
            else if (nearNextEdge && canSlideNext)
            {
                StartChildAutoSlide(parent.Id, +1);
            }
            else
            {
                StopChildAutoSlide();
            }
        }

        private void ComputeChildPaneDragShadowRect(
            string hostId,
            Point pointer,
            out double left,
            out double top,
            out double width,
            out double height)
        {
            var normalized = MainWindowViewModel.NormalizeHostId(hostId);
            var windowBounds = Bounds;
            var windowWidth = windowBounds.Width;
            var windowHeight = windowBounds.Height;

            const double defaultWidth = 260.0;
            const double defaultHeight = 160.0;
            const double margin = 12.0;

            Rect hostRect;

            Border? host = null;
            switch (normalized)
            {
                case "left":
                    host = this.FindControl<Border>("LeftPaneHost");
                    break;
                case "top":
                    host = this.FindControl<Border>("TopPaneHost");
                    break;
                case "right":
                    host = this.FindControl<Border>("RightPaneHost");
                    break;
                case "bottom":
                    host = this.FindControl<Border>("BottomPaneHost");
                    break;
                case "floating":
                    // Drag preview for floating children uses free 3D area (center viewport)
                    host = this.FindControl<Border>("CenterViewportHost");
                    break;
            }

            if (host is not null && host.IsVisible)
            {
                hostRect = host.Bounds;
            }
            else
            {
                hostRect = new Rect(0, 0, windowWidth, windowHeight);
            }

            if (string.Equals(normalized, "floating", StringComparison.Ordinal))
            {
                width = defaultWidth;
                height = defaultHeight;
                left = pointer.X - (width / 2.0);
                top = pointer.Y - (height / 2.0);

                // Clamp within free 3D rect
                var minLeft = hostRect.X;
                var minTop = hostRect.Y;
                var maxLeft = hostRect.X + Math.Max(0, hostRect.Width - width);
                var maxTop = hostRect.Y + Math.Max(0, hostRect.Height - height);
                if (left < minLeft) left = minLeft;
                if (top < minTop) top = minTop;
                if (left > maxLeft) left = maxLeft;
                if (top > maxTop) top = maxTop;
                return;
            }

            width = Math.Min(defaultWidth, Math.Max(120.0, hostRect.Width - (2 * margin)));
            height = Math.Min(defaultHeight, Math.Max(80.0, hostRect.Height - (2 * margin)));

            left = hostRect.X + ((hostRect.Width - width) / 2.0);
            top = hostRect.Y + margin;

            left = Math.Clamp(left, 0, Math.Max(0, windowWidth - width));
            top = Math.Clamp(top, 0, Math.Max(0, windowHeight - height));
        }

        // Compute a thin insertion preview rect inside the given hostRect for a lane/index.
        // Approximate by splitting hostRect into 'splitCount' lanes (columns for L/R; rows for T/B)
        // and then subdividing the lane’s free dimension evenly by (count+1) slots.
        private void ComputeChildInsertPreviewRect(
            string hostId,
            Rect hostRect,
            int laneIndex,
            int insertIndex,
            int splitCount,
            out double left,
            out double top,
            out double width,
            out double height)
        {
            var normalized = MainWindowViewModel.NormalizeHostId(hostId);
            bool isVerticalFlow = string.Equals(normalized, "left", StringComparison.Ordinal) ||
                                  string.Equals(normalized, "right", StringComparison.Ordinal);

            // Lane rect
            if (isVerticalFlow)
            {
                var laneWidth = Math.Max(1.0, hostRect.Width / Math.Max(1, splitCount));
                var laneLeft = hostRect.X + (laneWidth * laneIndex);
                var slotCount = Math.Max(1, insertIndex + 1); // ensure at least 1 slot
                // Even slot height
                var barY = hostRect.Y + (hostRect.Height * (insertIndex / Math.Max(1.0, (double)(insertIndex + 1))));
                // Use a thin horizontal bar across the entire lane
                left = laneLeft + 6.0;
                width = Math.Max(1.0, laneWidth - 12.0);
                height = 4.0; // thin bar
                // Position proportionally within the lane (even distribution)
                var slotHeight = hostRect.Height / Math.Max(1, (insertIndex + 1));
                top = hostRect.Y + (slotHeight * insertIndex);
            }
            else
            {
                var laneHeight = Math.Max(1.0, hostRect.Height / Math.Max(1, splitCount));
                var laneTop = hostRect.Y + (laneHeight * laneIndex);
                var slotCount = Math.Max(1, insertIndex + 1);
                // Even slot width
                var barX = hostRect.X + (hostRect.Width * (insertIndex / Math.Max(1.0, (double)(insertIndex + 1))));
                // Use a thin vertical bar across the entire lane
                top = laneTop + 6.0;
                height = Math.Max(1.0, laneHeight - 12.0);
                width = 4.0; // thin bar
                var slotWidth = hostRect.Width / Math.Max(1, (insertIndex + 1));
                left = hostRect.X + (slotWidth * insertIndex);
            }
        }

        private void StartChildAutoSlide(string parentId, int direction)
        {
            // Throttle repeated slides (cooldown 450ms)
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
            if (_childAutoSlideTimer is null) return;
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

            // Advance slide for the target parent
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
