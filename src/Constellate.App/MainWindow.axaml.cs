using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.ComponentModel;

namespace Constellate.App
{
    // Thin MainWindow code-behind: constructor, initialization, event wiring, and shared fields.
    // All interaction handlers live in partials:
    //   - MainWindow.ParentPaneDrag.cs
    //   - MainWindow.ParentPaneResize.cs
    //   - MainWindow.ChildPaneDrag.cs
    //   - MainWindow.CornerAffordances.cs
    //   - MainWindow.Intersections.cs (EXTRACT variants retained but unused; the two XAML-wired handlers remain here)
    public partial class MainWindow : Window
    {
        // Shared state for drag/resize flows used by partials
        private bool _isShellPaneDragging;
        private Point _shellDragStartPoint;
        private string? _dragOriginHostId;

        private bool _isPaneResizing;
        private string? _resizeEdge;
        private Point _resizeStartPoint;
        private double _initialLeftWidth;
        private double _initialRightWidth;
        private double _initialTopHeight;
        private double _initialBottomHeight;

        private Grid? _rootGrid;

        private bool _isChildPaneDragging;
        private Point _childDragStartPoint;
        private string? _childDragPaneId;
        private string? _childDragOriginHostId;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _rootGrid = this.FindControl<Grid>("RootGrid");

            // Wire drag gestures on all parent-pane hosts to partial handlers.
            void WireHost(string name)
            {
                var host = this.FindControl<Border>(name);
                if (host is not null)
                {
                    host.PointerPressed += ShellPaneHost_OnPointerPressed;
                    host.PointerReleased += ShellPaneHost_OnPointerReleased;
                    host.PointerMoved += ShellPaneHost_OnPointerMoved;
                }
            }

            WireHost("LeftPaneHost");
            WireHost("TopPaneHost");
            WireHost("RightPaneHost");
            WireHost("BottomPaneHost");
            WireHost("FloatingPaneHost");

            // Wire resize grips (partials implement the handlers).
            void WireGrip(string name, string tag)
            {
                var grip = this.FindControl<Border>(name);
                if (grip is not null)
                {
                    grip.Tag = tag;
                    grip.PointerPressed += PaneResizeGrip_OnPointerPressed;
                    grip.PointerReleased += PaneResizeGrip_OnPointerReleased;
                    grip.PointerMoved += PaneResizeGrip_OnPointerMoved;
                    grip.PointerCaptureLost += PaneResizeGrip_OnPointerCaptureLost;
                }
            }

            WireGrip("LeftPaneResizeGrip", "left");
            WireGrip("RightPaneResizeGrip", "right");
            WireGrip("TopPaneResizeGrip", "top");
            WireGrip("BottomPaneResizeGrip", "bottom");

            // Subscribe to VM property changes so we can collapse grid rows/cols
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += VmOnPropertyChanged;
            }
            // Ensure initial grid sizing matches initial visibility
            AdjustGridForHostVisibility();
        }

        // Keep the two XAML-wired handlers here to preserve existing event names in MainWindow.axaml.
        // Other intersection-related helpers may exist as *_EXTRACT variants in the partial for reference.
        private void OnTopCornerIntersectionDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                var hit = sender as Control;
                var name = hit?.Name ?? string.Empty;
                if (name.Contains("TopRight", System.StringComparison.OrdinalIgnoreCase))
                {
                    vm.ToggleTopRightCornerOwnership();
                }
                else
                {
                    // Default to top-left semantics
                    vm.ToggleTopCornerOwnership();
                }
            }

            e.Handled = true;
        }

        private void OnBottomCornerIntersectionDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                var hit = sender as Control;
                var name = hit?.Name ?? string.Empty;
                if (name.Contains("BottomRight", System.StringComparison.OrdinalIgnoreCase))
                {
                    vm.ToggleBottomRightCornerOwnership();
                }
                else
                {
                    // Default to bottom-left semantics
                    vm.ToggleBottomLeftCornerOwnership();
                }
            }

            e.Handled = true;
        }
        private void OnFloatingParentHeaderDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control header || header.DataContext is not ParentPaneModel parent)
            {
                return;
            }

            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            vm.SetParentPaneMinimized(parent.Id, false);
            e.Handled = true;
        }

        // Public wrappers to allow controls to forward pointer events for child-pane header drags.
        // These simply delegate to the existing private handlers defined on partials, preserving behavior.
        public void ForwardChildHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            OnChildPaneHeaderPointerPressed(sender, e);
        }

        public void ForwardChildHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            OnChildPaneHeaderPointerReleased(sender, e);
        }

        public void ForwardChildHeaderPointerMoved(object? sender, PointerEventArgs e)
        {
            OnChildPaneHeaderPointerMoved(sender, e);
        }

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_rootGrid is null) return;
            if (e.PropertyName is nameof(MainWindowViewModel.IsShellPaneOnLeft) or
                                     nameof(MainWindowViewModel.IsShellPaneOnTop) or
                                     nameof(MainWindowViewModel.IsShellPaneOnRight) or
                                     nameof(MainWindowViewModel.IsShellPaneOnBottom))
            {
                AdjustGridForHostVisibility();
            }
        }

        // Collapse grid rows/columns when a host is not visible so we don't leave blank areas
        private void AdjustGridForHostVisibility()
        {
            if (_rootGrid is null) return;
            if (DataContext is not MainWindowViewModel vm) return;

            // Left column (0)
            if (!vm.IsShellPaneOnLeft)
            {
                _rootGrid.ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Pixel);
            }
            else if (_rootGrid.ColumnDefinitions[0].Width.IsAbsolute && _rootGrid.ColumnDefinitions[0].Width.Value <= 0.1)
            {
                _rootGrid.ColumnDefinitions[0].Width = GridLength.Auto;
            }

            // Right column (2)
            if (!vm.IsShellPaneOnRight)
            {
                _rootGrid.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Pixel);
            }
            else if (_rootGrid.ColumnDefinitions[2].Width.IsAbsolute && _rootGrid.ColumnDefinitions[2].Width.Value <= 0.1)
            {
                _rootGrid.ColumnDefinitions[2].Width = GridLength.Auto;
            }

            // Top row (0)
            if (!vm.IsShellPaneOnTop)
            {
                _rootGrid.RowDefinitions[0].Height = new GridLength(0, GridUnitType.Pixel);
            }
            else if (_rootGrid.RowDefinitions[0].Height.IsAbsolute && _rootGrid.RowDefinitions[0].Height.Value <= 0.1)
            {
                _rootGrid.RowDefinitions[0].Height = GridLength.Auto;
            }

            // Bottom row (2)
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
