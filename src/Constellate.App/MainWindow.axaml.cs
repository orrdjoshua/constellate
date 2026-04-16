using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

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

            // Corner affordance triangles (handler defined in partial).
            AttachCornerTriangleHandlers();
        }

        // Keep the two XAML-wired handlers here to preserve existing event names in MainWindow.axaml.
        // Other intersection-related helpers may exist as *_EXTRACT variants in the partial for reference.
        private void OnTopCornerIntersectionDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.ToggleTopCornerOwnership();
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
    }
}
