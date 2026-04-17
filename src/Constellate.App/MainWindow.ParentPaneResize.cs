using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Constellate.App.Infrastructure.Panes;
using Constellate.App.Infrastructure.Panes.Gestures;

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

            if (DataContext is MainWindowViewModel vm)
            {
                var parent = vm.GetFirstExpandedParentOnHost(edge);
                var paneId = parent?.Id ?? edge;
                var attachment = DockAttachmentModel.FromHostId(edge);
                var fullWindowBounds = new Rect(
                    0,
                    0,
                    Math.Max(0.0, Bounds.Width),
                    Math.Max(0.0, Bounds.Height));
                var originBounds = ParentPaneResizeGesturePlanner.CreateOriginBounds(
                    attachment,
                    fullWindowBounds,
                    GetCurrentDockSize(edge));

                _activeParentResizeSession = new ParentPaneResizeSession(
                    paneId: paneId,
                    pointerId: (long)e.Pointer.Id,
                    startPoint: _resizeStartPoint,
                    attachment: attachment,
                    resizeEdge: edge switch
                    {
                        "left" => PaneResizeEdge.Right,
                        "right" => PaneResizeEdge.Left,
                        "top" => PaneResizeEdge.Bottom,
                        "bottom" => PaneResizeEdge.Top,
                        _ => PaneResizeEdge.None
                    },
                    originBounds: originBounds);
            }
            else
            {
                _activeParentResizeSession = null;
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

            if (!ActiveParentResizeOwnsPointer(e))
            {
                return;
            }

            _isPaneResizing = false;
            _resizeEdge = null;

            _activeParentResizeSession?.Commit();
            _activeParentResizeSession = null;

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

            _activeParentResizeSession?.Cancel();
            _activeParentResizeSession = null;
        }

        private void PaneResizeGrip_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPaneResizing || _rootGrid is null || string.IsNullOrWhiteSpace(_resizeEdge))
            {
                return;
            }

            if (!ActiveParentResizeOwnsPointer(e))
            {
                return;
            }

            var current = e.GetPosition(this);
            var attachment = DockAttachmentModel.FromHostId(_resizeEdge);
            var fullWindowBounds = new Rect(
                0,
                0,
                Math.Max(0.0, Bounds.Width),
                Math.Max(0.0, Bounds.Height));

            var originBounds = _activeParentResizeSession?.OriginBounds
                ?? ParentPaneResizeGesturePlanner.CreateOriginBounds(
                    attachment,
                    fullWindowBounds,
                    GetCurrentDockSize(_resizeEdge));

            var preview = ParentPaneResizeGesturePlanner.ComputePreview(
                attachment,
                fullWindowBounds,
                originBounds,
                _resizeStartPoint,
                current);

            if (DataContext is MainWindowViewModel vm)
            {
                vm.UpdateDockExtent(_resizeEdge, preview.PreviewExtent);
            }

            _activeParentResizeSession?.UpdatePreview(
                current,
                preview.PreviewBounds);

            e.Handled = true;
        }

        private bool ActiveParentResizeOwnsPointer(PointerEventArgs e)
        {
            return _activeParentResizeSession?.MatchesPointer(e) ?? true;
        }

        private double GetCurrentDockSize(string edge)
        {
            if (_rootGrid is null)
            {
                return 0.0;
            }

            return edge switch
            {
                "left" => _rootGrid.ColumnDefinitions[0].ActualWidth,
                "right" => _rootGrid.ColumnDefinitions[2].ActualWidth,
                "top" => _rootGrid.RowDefinitions[0].ActualHeight,
                "bottom" => _rootGrid.RowDefinitions[2].ActualHeight,
                _ => 0.0
            };
        }
    }
}
