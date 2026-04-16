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
            vm.MoveParentPaneToHost(_dragOriginHostId, targetHost);
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
            ComputeDragShadowRect(
                targetHost,
                width,
                height,
                currentPoint,
                out var left,
                out var top,
                out var shadowWidth,
                out var shadowHeight);

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
