using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;

namespace Constellate.App
{
    public partial class MainWindow
    {
        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_rootGrid is null)
            {
                return;
            }

            if (e.PropertyName is nameof(MainWindowViewModel.IsShellPaneOnLeft) or
                                     nameof(MainWindowViewModel.IsShellPaneOnTop) or
                                     nameof(MainWindowViewModel.IsShellPaneOnRight) or
                                     nameof(MainWindowViewModel.IsShellPaneOnBottom))
            {
                AdjustGridForHostVisibility();
                ApplyCurrentShellLayoutToGrid();
                return;
            }

            if (e.PropertyName == nameof(MainWindowViewModel.CurrentShellLayout))
            {
                ApplyCurrentShellLayoutToGrid();
            }
        }

        private void PushBoundsToViewModel()
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            vm.UpdateShellViewportBounds(Bounds.Width, Bounds.Height);
        }

        private void ApplyCurrentShellLayoutToGrid()
        {
            if (_rootGrid is null || DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var layout = vm.CurrentShellLayout;

            var leftWidth = layout.LeftDock?.Bounds.Width ?? 0.0;
            var rightWidth = layout.RightDock?.Bounds.Width ?? 0.0;
            var topHeight = layout.TopDock?.Bounds.Height ?? 0.0;
            var bottomHeight = layout.BottomDock?.Bounds.Height ?? 0.0;

            if (leftWidth < 0) leftWidth = 0.0;
            if (rightWidth < 0) rightWidth = 0.0;
            if (topHeight < 0) topHeight = 0.0;
            if (bottomHeight < 0) bottomHeight = 0.0;

            _rootGrid.ColumnDefinitions[0].Width = vm.IsShellPaneOnLeft
                ? new GridLength(leftWidth, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);

            _rootGrid.ColumnDefinitions[2].Width = vm.IsShellPaneOnRight
                ? new GridLength(rightWidth, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);

            _rootGrid.RowDefinitions[0].Height = vm.IsShellPaneOnTop
                ? new GridLength(topHeight, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);

            _rootGrid.RowDefinitions[2].Height = vm.IsShellPaneOnBottom
                ? new GridLength(bottomHeight, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);
        }

        private Rect GetShellFloatingSurfaceRect()
        {
            if (DataContext is MainWindowViewModel vm)
            {
                var rect = vm.CurrentShellLayout.FloatingSurfaceRect;
                if (rect.Width > 0 && rect.Height > 0)
                {
                    return rect;
                }
            }

            var centerHost = this.FindControl<Border>("CenterViewportHost");
            if (centerHost is not null && centerHost.IsVisible)
            {
                return centerHost.Bounds;
            }

            return new Rect(0, 0, Bounds.Width, Bounds.Height);
        }

        private Rect GetShellHostRect(string? hostId)
        {
            var normalizedHost = MainWindowViewModel.NormalizeHostId(hostId);

            if (DataContext is MainWindowViewModel vm)
            {
                Rect? rect = normalizedHost switch
                {
                    "left" => vm.CurrentShellLayout.LeftDock?.Bounds,
                    "top" => vm.CurrentShellLayout.TopDock?.Bounds,
                    "right" => vm.CurrentShellLayout.RightDock?.Bounds,
                    "bottom" => vm.CurrentShellLayout.BottomDock?.Bounds,
                    "floating" => vm.CurrentShellLayout.FloatingSurfaceRect,
                    _ => null
                };

                if (rect is { } layoutRect && layoutRect.Width > 0 && layoutRect.Height > 0)
                {
                    return layoutRect;
                }
            }

            var host = normalizedHost switch
            {
                "left" => this.FindControl<Border>("LeftPaneHost"),
                "top" => this.FindControl<Border>("TopPaneHost"),
                "right" => this.FindControl<Border>("RightPaneHost"),
                "bottom" => this.FindControl<Border>("BottomPaneHost"),
                "floating" => this.FindControl<Border>("FloatingPaneHost"),
                _ => null
            };

            if (host is not null)
            {
                return host.Bounds;
            }

            return new Rect(0, 0, Bounds.Width, Bounds.Height);
        }

        // Collapse grid rows/columns when a host is not visible so we don't leave blank areas
        private void AdjustGridForHostVisibility()
        {
            if (_rootGrid is null) return;
            if (DataContext is not MainWindowViewModel vm) return;

            if (!vm.IsShellPaneOnLeft)
            {
                _rootGrid.ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Pixel);
            }
            else if (_rootGrid.ColumnDefinitions[0].Width.IsAbsolute && _rootGrid.ColumnDefinitions[0].Width.Value <= 0.1)
            {
                _rootGrid.ColumnDefinitions[0].Width = GridLength.Auto;
            }

            if (!vm.IsShellPaneOnRight)
            {
                _rootGrid.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Pixel);
            }
            else if (_rootGrid.ColumnDefinitions[2].Width.IsAbsolute && _rootGrid.ColumnDefinitions[2].Width.Value <= 0.1)
            {
                _rootGrid.ColumnDefinitions[2].Width = GridLength.Auto;
            }

            if (!vm.IsShellPaneOnTop)
            {
                _rootGrid.RowDefinitions[0].Height = new GridLength(0, GridUnitType.Pixel);
            }
            else if (_rootGrid.RowDefinitions[0].Height.IsAbsolute && _rootGrid.RowDefinitions[0].Height.Value <= 0.1)
            {
                _rootGrid.RowDefinitions[0].Height = GridLength.Auto;
            }

            if (!vm.IsShellPaneOnBottom)
            {
                _rootGrid.RowDefinitions[2].Height = new GridLength(0, GridUnitType.Pixel);
            }
            else if (_rootGrid.RowDefinitions[2].Height.IsAbsolute && _rootGrid.RowDefinitions[2].Height.Value <= 0.1)
            {
                _rootGrid.RowDefinitions[2].Height = GridLength.Auto;
            }
        }
    }
}
