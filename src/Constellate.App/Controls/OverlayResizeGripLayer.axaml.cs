using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Controls
{
    /// <summary>
    /// Positions four overlay resize grips that align to the free edge of each visible dock.
    /// Names:
    ///  - OverlayLeftResizeGrip   (right edge of Left dock)
    ///  - OverlayRightResizeGrip  (left edge of Right dock)
    ///  - OverlayTopResizeGrip    (bottom edge of Top dock)
    ///  - OverlayBottomResizeGrip (top edge of Bottom dock)
    /// </summary>
    public partial class OverlayResizeGripLayer : UserControl
    {
        private const double Thickness = 8.0;

        private Canvas? _canvas;
        private MainWindowViewModel? _vm;

        private Border? _leftGrip;
        private Border? _rightGrip;
        private Border? _topGrip;
        private Border? _bottomGrip;
        private Border? _cornerTopLeft;
        private Border? _cornerTopRight;
        private Border? _cornerBottomLeft;
        private Border? _cornerBottomRight;

        public OverlayResizeGripLayer()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _canvas = this.FindControl<Canvas>("PART_Canvas");

            _leftGrip = CreateGrip("OverlayLeftResizeGrip", "left", StandardCursorType.SizeWestEast);
            _rightGrip = CreateGrip("OverlayRightResizeGrip", "right", StandardCursorType.SizeWestEast);
            _topGrip = CreateGrip("OverlayTopResizeGrip", "top", StandardCursorType.SizeNorthSouth);
            _bottomGrip = CreateGrip("OverlayBottomResizeGrip", "bottom", StandardCursorType.SizeNorthSouth);

            // Corner hit-targets for intersection toggles (double-tap)
            _cornerTopLeft = CreateCorner("OverlayCornerTopLeft");
            _cornerTopRight = CreateCorner("OverlayCornerTopRight");
            _cornerBottomLeft = CreateCorner("OverlayCornerBottomLeft");
            _cornerBottomRight = CreateCorner("OverlayCornerBottomRight");
            if (_cornerTopLeft is not null) _cornerTopLeft.DoubleTapped += OnCornerDoubleTapped;
            if (_cornerTopRight is not null) _cornerTopRight.DoubleTapped += OnCornerDoubleTapped;
            if (_cornerBottomLeft is not null) _cornerBottomLeft.DoubleTapped += OnCornerDoubleTapped;
            if (_cornerBottomRight is not null) _cornerBottomRight.DoubleTapped += OnCornerDoubleTapped;

            if (_canvas is not null)
            {
                if (_leftGrip is not null) { _leftGrip.ZIndex = 1000; _canvas.Children.Add(_leftGrip); }
                if (_rightGrip is not null) { _rightGrip.ZIndex = 1000; _canvas.Children.Add(_rightGrip); }
                if (_topGrip is not null) { _topGrip.ZIndex = 1000; _canvas.Children.Add(_topGrip); }
                if (_bottomGrip is not null) { _bottomGrip.ZIndex = 1000; _canvas.Children.Add(_bottomGrip); }
                if (_cornerTopLeft is not null) { _cornerTopLeft.ZIndex = 1100; _canvas.Children.Add(_cornerTopLeft); }
                if (_cornerTopRight is not null) { _cornerTopRight.ZIndex = 1100; _canvas.Children.Add(_cornerTopRight); }
                if (_cornerBottomLeft is not null) { _cornerBottomLeft.ZIndex = 1100; _canvas.Children.Add(_cornerBottomLeft); }
                if (_cornerBottomRight is not null) { _cornerBottomRight.ZIndex = 1100; _canvas.Children.Add(_cornerBottomRight); }
            }
        }

        private static Border CreateGrip(string name, string tag, StandardCursorType cursor)
        {
            var b = new Border
            {
                Name = name,
                Tag = tag,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsHitTestVisible = true,
                Cursor = new Cursor(cursor),
            };
            b.ZIndex = 1000;
            ToolTip.SetTip(b, BuildDockResizeTooltip(tag));
            b.PointerEntered += (_, __) =>
            {
                try { System.Diagnostics.Debug.WriteLine($"[OverlayGrip][Hover][ENTER] name={name} tag={tag}"); } catch { }
            };
            b.PointerExited += (_, __) =>
            {
                try { System.Diagnostics.Debug.WriteLine($"[OverlayGrip][Hover][EXIT] name={name} tag={tag}"); } catch { }
            };
            b.PointerPressed += (_, e) =>
            {
                try { System.Diagnostics.Debug.WriteLine($"[OverlayGrip][Press] name={name} tag={tag} pointer={e.Pointer.Id}"); } catch { }
            };
            return b;
        }

        private static Border CreateCorner(string name)
        {
            var b = new Border
            {
                Name = name,
                Width = 22,
                Height = 22,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                IsHitTestVisible = true,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            b.ZIndex = 1100;
            ToolTip.SetTip(b, BuildCornerIntersectionTooltip(name));
            // Add simple hover visuals so intersections are discoverable.
            b.PointerEntered += (_, __) =>
            {
                try { System.Diagnostics.Debug.WriteLine($"[OverlayCorner][Hover][ENTER] name={name}"); } catch { }
                b.Background = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xC4, 0x8A));
                b.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xC4, 0x8A));
                b.BorderThickness = new Thickness(1);
            };
            b.PointerExited += (_, __) =>
            {
                try { System.Diagnostics.Debug.WriteLine($"[OverlayCorner][Hover][EXIT] name={name}"); } catch { }
                b.Background = Brushes.Transparent;
                b.BorderBrush = Brushes.Transparent;
                b.BorderThickness = new Thickness(0);
            };
            b.PointerPressed += (_, e) =>
            {
                try { System.Diagnostics.Debug.WriteLine($"[OverlayCorner][Press] name={name} pointer={e.Pointer.Id}"); } catch { }
            };
            return b;
        }

        private static string BuildDockResizeTooltip(string tag)
        {
            return tag switch
            {
                "left" => "Drag to resize the left docked pane.",
                "right" => "Drag to resize the right docked pane.",
                "top" => "Drag to resize the top docked pane.",
                "bottom" => "Drag to resize the bottom docked pane.",
                _ => "Drag to resize the docked pane."
            };
        }

        private static string BuildCornerIntersectionTooltip(string name)
        {
            return name switch
            {
                "OverlayCornerTopLeft" => "Toggle top-left corner ownership between the Top and Left panes.",
                "OverlayCornerTopRight" => "Toggle top-right corner ownership between the Top and Right panes.",
                "OverlayCornerBottomLeft" => "Toggle bottom-left corner ownership between the Bottom and Left panes.",
                "OverlayCornerBottomRight" => "Toggle bottom-right corner ownership between the Bottom and Right panes.",
                _ => "Toggle dock-corner ownership at this pane intersection."
            };
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            HookVm();
            SyncAll();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            UnhookVm();
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            HookVm();
            SyncAll();
        }

        private void HookVm()
        {
            UnhookVm();
            _vm = DataContext as MainWindowViewModel;
            if (_vm is INotifyPropertyChanged pc)
            {
                pc.PropertyChanged += OnVmPropertyChanged;
            }
        }

        private void UnhookVm()
        {
            if (_vm is INotifyPropertyChanged oldPc)
            {
                oldPc.PropertyChanged -= OnVmPropertyChanged;
            }
            _vm = null;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainWindowViewModel.CurrentShellLayout) or
                                     nameof(MainWindowViewModel.IsShellPaneOnLeft) or
                                     nameof(MainWindowViewModel.IsShellPaneOnTop) or
                                     nameof(MainWindowViewModel.IsShellPaneOnRight) or
                                     nameof(MainWindowViewModel.IsShellPaneOnBottom))
            {
                SyncAll();
            }
        }

        private void SyncAll()
        {
            if (_vm is null || _canvas is null)
            {
                return;
            }

            PositionLeftGrip(_vm.CurrentShellLayout.LeftDock);
            PositionRightGrip(_vm.CurrentShellLayout.RightDock);
            PositionTopGrip(_vm.CurrentShellLayout.TopDock);
            PositionBottomGrip(_vm.CurrentShellLayout.BottomDock);

            PositionCornerHits(_vm.CurrentShellLayout.LeftDock,
                               _vm.CurrentShellLayout.TopDock,
                               _vm.CurrentShellLayout.RightDock,
                               _vm.CurrentShellLayout.BottomDock);
        }

        private void PositionLeftGrip(DockHostLayout? layout)
        {
            if (_leftGrip is null) return;
            if (layout is { IsVisible: true })
            {
                var r = layout.Bounds;
                _leftGrip.IsVisible = true;
                _leftGrip.Width = Thickness;
                _leftGrip.Height = Math.Max(0.0, r.Height);
                Canvas.SetLeft(_leftGrip, r.Right - (Thickness / 2.0));
                Canvas.SetTop(_leftGrip, r.Y);
                try { System.Diagnostics.Debug.WriteLine($"[OverlayGrip][Layout] left x={Canvas.GetLeft(_leftGrip):0.##} y={Canvas.GetTop(_leftGrip):0.##} w={_leftGrip.Width:0.##} h={_leftGrip.Height:0.##}"); } catch { }
            }
            else
            {
                _leftGrip.IsVisible = false;
            }
        }

        private void PositionRightGrip(DockHostLayout? layout)
        {
            if (_rightGrip is null) return;
            if (layout is { IsVisible: true })
            {
                var r = layout.Bounds;
                _rightGrip.IsVisible = true;
                _rightGrip.Width = Thickness;
                _rightGrip.Height = Math.Max(0.0, r.Height);
                Canvas.SetLeft(_rightGrip, r.X - (Thickness / 2.0));
                Canvas.SetTop(_rightGrip, r.Y);
                try { System.Diagnostics.Debug.WriteLine($"[OverlayGrip][Layout] right x={Canvas.GetLeft(_rightGrip):0.##} y={Canvas.GetTop(_rightGrip):0.##} w={_rightGrip.Width:0.##} h={_rightGrip.Height:0.##}"); } catch { }
            }
            else
            {
                _rightGrip.IsVisible = false;
            }
        }

        private void PositionTopGrip(DockHostLayout? layout)
        {
            if (_topGrip is null) return;
            if (layout is { IsVisible: true })
            {
                var r = layout.Bounds;
                _topGrip.IsVisible = true;
                _topGrip.Width = Math.Max(0.0, r.Width);
                _topGrip.Height = Thickness;
                Canvas.SetLeft(_topGrip, r.X);
                Canvas.SetTop(_topGrip, r.Bottom - (Thickness / 2.0));
                try { System.Diagnostics.Debug.WriteLine($"[OverlayGrip][Layout] top x={Canvas.GetLeft(_topGrip):0.##} y={Canvas.GetTop(_topGrip):0.##} w={_topGrip.Width:0.##} h={_topGrip.Height:0.##}"); } catch { }
            }
            else
            {
                _topGrip.IsVisible = false;
            }
        }

        private void PositionBottomGrip(DockHostLayout? layout)
        {
            if (_bottomGrip is null) return;
            if (layout is { IsVisible: true })
            {
                var r = layout.Bounds;
                _bottomGrip.IsVisible = true;
                _bottomGrip.Width = Math.Max(0.0, r.Width);
                _bottomGrip.Height = Thickness;
                Canvas.SetLeft(_bottomGrip, r.X);
                Canvas.SetTop(_bottomGrip, r.Y - (Thickness / 2.0));
                try { System.Diagnostics.Debug.WriteLine($"[OverlayGrip][Layout] bottom x={Canvas.GetLeft(_bottomGrip):0.##} y={Canvas.GetTop(_bottomGrip):0.##} w={_bottomGrip.Width:0.##} h={_bottomGrip.Height:0.##}"); } catch { }
             }
            else
            {
                _bottomGrip.IsVisible = false;
            }
        }

        private void PositionCornerHits(DockHostLayout? left, DockHostLayout? top, DockHostLayout? right, DockHostLayout? bottom)
        {
            // Top-Left intersection: visible when both top and left are visible
            if (_cornerTopLeft is not null)
            {
                if (left is { IsVisible: true } && top is { IsVisible: true })
                {
                    var x = left.Bounds.Right - _cornerTopLeft.Width / 2.0;
                    var y = top.Bounds.Bottom - _cornerTopLeft.Height / 2.0;
                    Canvas.SetLeft(_cornerTopLeft, x);
                    Canvas.SetTop(_cornerTopLeft, y);
                    _cornerTopLeft.IsVisible = true;
                }
                else _cornerTopLeft.IsVisible = false;
            }

            // Top-Right
            if (_cornerTopRight is not null)
            {
                if (right is { IsVisible: true } && top is { IsVisible: true })
                {
                    var x = right.Bounds.X - _cornerTopRight.Width / 2.0;
                    var y = top.Bounds.Bottom - _cornerTopRight.Height / 2.0;
                    Canvas.SetLeft(_cornerTopRight, x);
                    Canvas.SetTop(_cornerTopRight, y);
                    _cornerTopRight.IsVisible = true;
                }
                else _cornerTopRight.IsVisible = false;
            }

            // Bottom-Left
            if (_cornerBottomLeft is not null)
            {
                if (left is { IsVisible: true } && bottom is { IsVisible: true })
                {
                    var x = left.Bounds.Right - _cornerBottomLeft.Width / 2.0;
                    var y = bottom.Bounds.Y - _cornerBottomLeft.Height / 2.0;
                    Canvas.SetLeft(_cornerBottomLeft, x);
                    Canvas.SetTop(_cornerBottomLeft, y);
                    _cornerBottomLeft.IsVisible = true;
                }
                else _cornerBottomLeft.IsVisible = false;
            }

            // Bottom-Right
            if (_cornerBottomRight is not null)
            {
                if (right is { IsVisible: true } && bottom is { IsVisible: true })
                {
                    var x = right.Bounds.X - _cornerBottomRight.Width / 2.0;
                    var y = bottom.Bounds.Y - _cornerBottomRight.Height / 2.0;
                    Canvas.SetLeft(_cornerBottomRight, x);
                    Canvas.SetTop(_cornerBottomRight, y);
                    _cornerBottomRight.IsVisible = true;
                }
                else _cornerBottomRight.IsVisible = false;
            }
        }

        private void OnCornerDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_vm is null || sender is not Border b) return;
            switch (b.Name)
            {
                case "OverlayCornerTopLeft":
                    _vm.ToggleTopCornerOwnership();
                    break;
                case "OverlayCornerTopRight":
                    _vm.ToggleTopRightCornerOwnership();
                    break;
                case "OverlayCornerBottomLeft":
                    _vm.ToggleBottomLeftCornerOwnership();
                    break;
                case "OverlayCornerBottomRight":
                    _vm.ToggleBottomRightCornerOwnership();
                    break;
            }
            e.Handled = true;
        }
    }
}
