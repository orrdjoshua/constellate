using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    // Thin MainWindow code-behind: constructor, initialization, event wiring,
    // wrappers, and shared fields. Heavier shell-layout bridge logic and
    // global gesture routing now live in dedicated partials.
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

        // New architecture bridge: explicit interaction sessions.
        // These coexist with the legacy booleans during cutover, giving us a stable
        // place to move ownership and preview state before the old fields are deleted.
        private ParentPaneMoveSession? _activeParentMoveSession;
        private ParentPaneResizeSession? _activeParentResizeSession;
        private ChildPaneDragSession? _activeChildDragSession;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();

            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += VmOnPropertyChanged;
            }

            AdjustGridForHostVisibility();
            PushBoundsToViewModel();
            ApplyCurrentShellLayoutToGrid();
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

            // Global drag lifecycle routing:
            // pointer press still starts from pane/header/body regions,
            // but active move/release is owned by the window so drags continue
            // after the pointer leaves the initiating control.
            PointerMoved += Window_OnGlobalPointerMoved;
            PointerReleased += Window_OnGlobalPointerReleased;

            Opened += (_, __) =>
            {
                PushBoundsToViewModel();
                ApplyCurrentShellLayoutToGrid();
            };

            SizeChanged += (_, __) =>
            {
                PushBoundsToViewModel();
                ApplyCurrentShellLayoutToGrid();
            };
        }
    }
}
