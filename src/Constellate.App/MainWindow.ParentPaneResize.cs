using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        private void PaneResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_isShellPaneDragging || _isPaneResizing)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (sender is not Border grip || grip.Tag is not string edge || _rootGrid is null)
            {
                return;
            }

            _isPaneResizing = true;
            _resizeEdge = edge;
            _resizeStartPoint = e.GetPosition(this);

            switch (edge)
            {
                case "left":
                    _initialLeftWidth = _rootGrid.ColumnDefinitions[0].ActualWidth;
                    break;
                case "right":
                    _initialRightWidth = _rootGrid.ColumnDefinitions[2].ActualWidth;
                    break;
                case "top":
                    _initialTopHeight = _rootGrid.RowDefinitions[0].ActualHeight;
                    break;
                case "bottom":
                    _initialBottomHeight = _rootGrid.RowDefinitions[2].ActualHeight;
                    break;
            }

            try { e.Pointer.Capture(grip); } catch { }
            e.Handled = true;
        }

        private void PaneResizeGrip_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isPaneResizing)
            {
                return;
            }

            _isPaneResizing = false;
            _resizeEdge = null;

            try { e.Pointer.Capture(null); } catch { }
            e.Handled = true;
        }

        private void PaneResizeGrip_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (!_isPaneResizing)
            {
                return;
            }

            _isPaneResizing = false;
            _resizeEdge = null;
        }

        private void PaneResizeGrip_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPaneResizing || _rootGrid is null || string.IsNullOrWhiteSpace(_resizeEdge))
            {
                return;
            }

            var current = e.GetPosition(this);
            var dx = current.X - _resizeStartPoint.X;
            var dy = current.Y - _resizeStartPoint.Y;
            const double minSize = 80.0;

            switch (_resizeEdge)
            {
                case "left":
                {
                    var newWidth = Math.Max(minSize, _initialLeftWidth + dx);
                    _rootGrid.ColumnDefinitions[0].Width = new GridLength(newWidth, GridUnitType.Pixel);
                    break;
                }
                case "right":
                {
                    var newWidth = Math.Max(minSize, _initialRightWidth - dx);
                    _rootGrid.ColumnDefinitions[2].Width = new GridLength(newWidth, GridUnitType.Pixel);
                    break;
                }
                case "top":
                {
                    var newHeight = Math.Max(minSize, _initialTopHeight + dy);
                    _rootGrid.RowDefinitions[0].Height = new GridLength(newHeight, GridUnitType.Pixel);
                    break;
                }
                case "bottom":
                {
                    var newHeight = Math.Max(minSize, _initialBottomHeight - dy);
                    _rootGrid.RowDefinitions[2].Height = new GridLength(newHeight, GridUnitType.Pixel);
                    break;
                }
            }

            e.Handled = true;
        }
    }
}
