using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;

namespace Constellate.App.Infrastructure.Panes
{
    internal sealed class MainWindowShellLayoutController
    {
        private readonly MainWindow _window;
        private Grid? _rootGrid;

        public MainWindowShellLayoutController(MainWindow window)
        {
            _window = window;
        }

        public Grid? RootGrid => _rootGrid;

        public void Initialize(MainWindowViewModel? vm)
        {
            _rootGrid = _window.FindControl<Grid>("RootGrid");

            if (vm is not null)
            {
                vm.PropertyChanged -= VmOnPropertyChanged;
                vm.PropertyChanged += VmOnPropertyChanged;
            }

            _window.Opened -= Window_OnShellOpened;
            _window.Opened += Window_OnShellOpened;

            _window.SizeChanged -= Window_OnShellSizeChanged;
            _window.SizeChanged += Window_OnShellSizeChanged;

            AdjustGridForHostVisibility(vm);
            PushBoundsToViewModel(vm);
            ApplyCurrentShellLayoutToGrid(vm);
        }

        public Rect GetFloatingSurfaceRect(MainWindowViewModel? vm)
        {
            if (vm is not null)
            {
                var rect = vm.CurrentShellLayout.FloatingSurfaceRect;
                if (rect.Width > 0 && rect.Height > 0)
                {
                    return rect;
                }
            }

            var centerHost = _window.FindControl<Border>("CenterViewportHost");
            if (centerHost is not null && centerHost.IsVisible)
            {
                return centerHost.Bounds;
            }

            return new Rect(0, 0, _window.Bounds.Width, _window.Bounds.Height);
        }

        public Rect GetHostRect(MainWindowViewModel? vm, string? hostId)
        {
            var normalizedHost = MainWindowViewModel.NormalizeHostId(hostId);

            if (vm is not null)
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
                "left" => _window.FindControl<Border>("LeftPaneHost"),
                "top" => _window.FindControl<Border>("TopPaneHost"),
                "right" => _window.FindControl<Border>("RightPaneHost"),
                "bottom" => _window.FindControl<Border>("BottomPaneHost"),
                "floating" => _window.FindControl<Border>("FloatingPaneHost"),
                _ => null
            };

            if (host is not null)
            {
                return host.Bounds;
            }

            return new Rect(0, 0, _window.Bounds.Width, _window.Bounds.Height);
        }

        private void Window_OnShellOpened(object? sender, EventArgs e)
        {
            var vm = _window.DataContext as MainWindowViewModel;
            PushBoundsToViewModel(vm);
            ApplyCurrentShellLayoutToGrid(vm);
        }

        private void Window_OnShellSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            var vm = _window.DataContext as MainWindowViewModel;
            PushBoundsToViewModel(vm);
            ApplyCurrentShellLayoutToGrid(vm);
        }

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_rootGrid is null)
            {
                return;
            }

            var vm = sender as MainWindowViewModel ?? _window.DataContext as MainWindowViewModel;
            if (vm is null)
            {
                return;
            }

            if (e.PropertyName is nameof(MainWindowViewModel.IsShellPaneOnLeft) or
                                     nameof(MainWindowViewModel.IsShellPaneOnTop) or
                                     nameof(MainWindowViewModel.IsShellPaneOnRight) or
                                     nameof(MainWindowViewModel.IsShellPaneOnBottom))
            {
                AdjustGridForHostVisibility(vm);
                ApplyCurrentShellLayoutToGrid(vm);
                return;
            }

            if (e.PropertyName == nameof(MainWindowViewModel.CurrentShellLayout))
            {
                ApplyCurrentShellLayoutToGrid(vm);
            }
        }

        private void PushBoundsToViewModel(MainWindowViewModel? vm)
        {
            vm?.UpdateShellViewportBounds(_window.Bounds.Width, _window.Bounds.Height);
        }

        private void ApplyCurrentShellLayoutToGrid(MainWindowViewModel? vm)
        {
            if (_rootGrid is null || vm is null)
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

        private void AdjustGridForHostVisibility(MainWindowViewModel? vm)
        {
            if (_rootGrid is null || vm is null)
            {
                return;
            }

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
