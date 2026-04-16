using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

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
            }
        }

        private void ShellPaneHost_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isShellPaneDragging)
            {
                return;
            }

            _isShellPaneDragging = false;

            try { e.Pointer.Capture(null); } catch { }

            if (DataContext is not MainWindowViewModel vm)
            {
                _dragOriginHostId = null;
                return;
            }

            var releasePoint = e.GetPosition(this);
            var width = Bounds.Width;
            var height = Bounds.Height;

            if (width <= 0 || height <= 0 || string.IsNullOrWhiteSpace(_dragOriginHostId))
            {
                vm.SetParentPaneDragShadow(false, 0, 0, 0, 0);
                _dragOriginHostId = null;
                return;
            }

            var targetHost = GetTargetHostForPoint(releasePoint, width, height);
            // Enforce single-pane docks at drop time too: if target != origin and occupied, convert to floating.
            if (!string.Equals(targetHost, _dragOriginHostId, StringComparison.Ordinal) &&
                !string.Equals(targetHost, "floating", StringComparison.Ordinal) &&
                vm.IsDockHostOccupied(targetHost))
            {
                targetHost = "floating";
            }

            if (string.Equals(targetHost, "floating", StringComparison.Ordinal))
            {
                // Convert the final shadow rect from window coordinates to FloatingPaneHost-relative coordinates.
                var floatingHost = this.FindControl<Border>("FloatingPaneHost");
                var hostRect = floatingHost is not null ? floatingHost.Bounds : Bounds;

                // Use the last preview rect held in the VM; if missing, synthesize a reasonable size.
                var leftWin = vm.ParentPaneDragShadowLeft;
                var topWin = vm.ParentPaneDragShadowTop;
                var shadowW = vm.ParentPaneDragShadowWidth;
                var shadowH = vm.ParentPaneDragShadowHeight;

                if (shadowW <= 0 || shadowH <= 0)
                {
                    // Fallback to a 30% square of the free area (relative to the floating host).
                    var side = Math.Min(hostRect.Width, hostRect.Height) * 0.30;
                    side = Math.Max(80.0, side);
                    shadowW = side;
                    shadowH = side;
                    leftWin = releasePoint.X - (shadowW / 2.0);
                    topWin = releasePoint.Y - (shadowH / 2.0);
                }

                // Translate to floating-host-relative coordinates and clamp inside it.
                var relLeft = leftWin - hostRect.X;
                var relTop = topWin - hostRect.Y;
                var minLeft = 0.0;
                var minTop = 0.0;
                var maxLeft = Math.Max(0, hostRect.Width - shadowW);
                var maxTop = Math.Max(0, hostRect.Height - shadowH);
                relLeft = Math.Clamp(relLeft, minLeft, maxLeft);
                relTop = Math.Clamp(relTop, minTop, maxTop);

                try
                {
                    Console.WriteLine($"[FloatingDrop] originHost={_dragOriginHostId} hostRect=({hostRect.X:0},{hostRect.Y:0},{hostRect.Width:0},{hostRect.Height:0}) " +
                                      $"shadowWin=({leftWin:0},{topWin:0},{shadowW:0},{shadowH:0}) rel=({relLeft:0},{relTop:0})");
                }
                catch { }

                vm.MoveParentPaneToFloating(_dragOriginHostId, relLeft, relTop, shadowW, shadowH);
            }
            else
            {
                vm.MoveParentPaneToHost(_dragOriginHostId, targetHost);
            }
            vm.SetParentPaneDragShadow(false, 0, 0, 0, 0);
            _dragOriginHostId = null;
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

            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var currentPoint = e.GetPosition(this);
            var width = Bounds.Width;
            var height = Bounds.Height;

            if (width <= 0 || height <= 0)
            {
                vm.SetParentPaneDragShadow(false, 0, 0, 0, 0);
                return;
            }

            var targetHost = GetTargetHostForPoint(currentPoint, width, height);

            // Do not preview docking onto an occupied host; if target != origin and occupied, switch preview to floating.
            if (!string.Equals(targetHost, _dragOriginHostId, StringComparison.Ordinal) &&
                !string.Equals(targetHost, "floating", StringComparison.Ordinal) &&
                vm.IsDockHostOccupied(targetHost))
            {
                targetHost = "floating";
            }

            double left, top, shadowWidth, shadowHeight;

            if (string.Equals(targetHost, "floating", StringComparison.Ordinal))
            {
                // Use free 3D area: the center viewport host
                var center = this.FindControl<Border>("CenterViewportHost");
                var rect = center is not null && center.IsVisible ? center.Bounds : Bounds;

                // 30% square of the free area
                var side = Math.Min(rect.Width, rect.Height) * 0.30;
                side = Math.Max(80.0, side);
                shadowWidth = side;
                shadowHeight = side;

                // Pointer-centered, clamped to the free 3D rect
                left = currentPoint.X - (shadowWidth / 2.0);
                top = currentPoint.Y - (shadowHeight / 2.0);

                // Clamp within the center viewport rect
                var minLeft = rect.X;
                var minTop = rect.Y;
                var maxLeft = rect.X + Math.Max(0, rect.Width - shadowWidth);
                var maxTop = rect.Y + Math.Max(0, rect.Height - shadowHeight);
                if (left < minLeft) left = minLeft;
                if (top < minTop) top = minTop;
                if (left > maxLeft) left = maxLeft;
                if (top > maxTop) top = maxTop;
            }
            else
            {
                ComputeDragShadowRect(
                    targetHost,
                    width,
                    height,
                    currentPoint,
                    out left,
                    out top,
                    out shadowWidth,
                    out shadowHeight);
            }

            vm.SetParentPaneDragShadow(true, left, top, shadowWidth, shadowHeight);
        }

        private static string GetTargetHostForPoint(Point point, double width, double height)
        {
            if (width <= 0 || height <= 0)
            {
                return "left";
            }

            var leftThreshold = width * 0.15;
            var rightThreshold = width * 0.85;
            var topThreshold = height * 0.15;
            var bottomThreshold = height * 0.85;

            if (point.X <= leftThreshold)
            {
                return "left";
            }

            if (point.X >= rightThreshold)
            {
                return "right";
            }

            if (point.Y <= topThreshold)
            {
                return "top";
            }

            if (point.Y >= bottomThreshold)
            {
                return "bottom";
            }

            return "floating";
        }

        private static void ComputeDragShadowRect(
            string hostId,
            double windowWidth,
            double windowHeight,
            Point pointer,
            out double left,
            out double top,
            out double width,
            out double height)
        {
            var normalized = MainWindowViewModel.NormalizeHostId(hostId);
            windowWidth = Math.Max(1, windowWidth);
            windowHeight = Math.Max(1, windowHeight);

            switch (normalized)
            {
                case "left":
                    width = windowWidth * 0.25;
                    height = windowHeight;
                    left = 0;
                    top = 0;
                    break;
                case "right":
                    width = windowWidth * 0.25;
                    height = windowHeight;
                    left = windowWidth - width;
                    top = 0;
                    break;
                case "top":
                    width = windowWidth * 0.6;
                    height = windowHeight * 0.22;
                    left = (windowWidth - width) / 2.0;
                    top = 0;
                    break;
                case "bottom":
                    width = windowWidth * 0.6;
                    height = windowHeight * 0.22;
                    left = (windowWidth - width) / 2.0;
                    top = windowHeight - height;
                    break;
                case "floating":
                default:
                    width = windowWidth * 0.25;
                    height = windowHeight * 0.25;
                    left = pointer.X - (width / 2.0);
                    top = pointer.Y - (height / 2.0);

                    if (left < 0) left = 0;
                    if (top < 0) top = 0;
                    if (left + width > windowWidth) left = windowWidth - width;
                    if (top + height > windowHeight) top = windowHeight - height;
                    break;
            }
        }
    }
}
