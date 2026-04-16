using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
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
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var targetHost = GetTargetHostForPoint(releasePoint, width, height);

            if (!string.IsNullOrWhiteSpace(originHost) &&
                string.Equals(
                    MainWindowViewModel.NormalizeHostId(originHost),
                    MainWindowViewModel.NormalizeHostId(targetHost),
                    StringComparison.Ordinal))
            {
                return;
            }

            vm.MoveChildPaneToHost(paneId, targetHost);
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
            var width = Bounds.Width;
            var height = Bounds.Height;

            if (width <= 0 || height <= 0)
            {
                vm.SetChildPaneDragShadow(false, 0, 0, 0, 0);
                return;
            }

            var targetHost = GetTargetHostForPoint(currentPoint, width, height);
            ComputeChildPaneDragShadowRect(
                targetHost,
                currentPoint,
                out var left,
                out var top,
                out var shadowWidth,
                out var shadowHeight);

            vm.SetChildPaneDragShadow(true, left, top, shadowWidth, shadowHeight);
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
    }
}
