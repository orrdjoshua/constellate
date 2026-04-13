using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Constellate.Core.Capabilities;
using Constellate.Core.Messaging;
using Constellate.Core.Scene;
using Constellate.SDK;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        private bool _isShellPaneDragging;
        private Point _shellDragStartPoint;

        private bool _isPaneResizing;
        private string? _resizeEdge;
        private Point _resizeStartPoint;
        private double _initialLeftWidth;
        private double _initialRightWidth;
        private double _initialTopHeight;
        private double _initialBottomHeight;
        private Grid? _rootGrid;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _rootGrid = this.FindControl<Grid>("RootGrid");

            // Wire drag gestures on all shell parent-pane hosts so that docking
            // and floating behavior can be driven by the shared layout model
            // instead of hardcoded left-only placement.
            var leftHost = this.FindControl<Border>("LeftPaneHost");
            if (leftHost is not null)
            {
                leftHost.PointerPressed += ShellPaneHost_OnPointerPressed;
                leftHost.PointerReleased += ShellPaneHost_OnPointerReleased;
                leftHost.PointerMoved += ShellPaneHost_OnPointerMoved;
            }

            var topHost = this.FindControl<Border>("TopPaneHost");
            if (topHost is not null)
            {
                topHost.PointerPressed += ShellPaneHost_OnPointerPressed;
                topHost.PointerReleased += ShellPaneHost_OnPointerReleased;
                topHost.PointerMoved += ShellPaneHost_OnPointerMoved;
            }

            var rightHost = this.FindControl<Border>("RightPaneHost");
            if (rightHost is not null)
            {
                rightHost.PointerPressed += ShellPaneHost_OnPointerPressed;
                rightHost.PointerReleased += ShellPaneHost_OnPointerReleased;
                rightHost.PointerMoved += ShellPaneHost_OnPointerMoved;
            }

            var bottomHost = this.FindControl<Border>("BottomPaneHost");
            if (bottomHost is not null)
            {
                bottomHost.PointerPressed += ShellPaneHost_OnPointerPressed;
                bottomHost.PointerReleased += ShellPaneHost_OnPointerReleased;
                bottomHost.PointerMoved += ShellPaneHost_OnPointerMoved;
            }

            var floatingHost = this.FindControl<Border>("FloatingPaneHost");
            if (floatingHost is not null)
            {
                floatingHost.PointerPressed += ShellPaneHost_OnPointerPressed;
                floatingHost.PointerReleased += ShellPaneHost_OnPointerReleased;
                floatingHost.PointerMoved += ShellPaneHost_OnPointerMoved;
            }

            // Wire resize grips for parent panes (left/right/top/bottom).
            var leftResizeGrip = this.FindControl<Border>("LeftPaneResizeGrip");
            if (leftResizeGrip is not null)
            {
                leftResizeGrip.Tag = "left";
                leftResizeGrip.PointerPressed += PaneResizeGrip_OnPointerPressed;
                leftResizeGrip.PointerReleased += PaneResizeGrip_OnPointerReleased;
                leftResizeGrip.PointerMoved += PaneResizeGrip_OnPointerMoved;
            }

            var rightResizeGrip = this.FindControl<Border>("RightPaneResizeGrip");
            if (rightResizeGrip is not null)
            {
                rightResizeGrip.Tag = "right";
                rightResizeGrip.PointerPressed += PaneResizeGrip_OnPointerPressed;
                rightResizeGrip.PointerReleased += PaneResizeGrip_OnPointerReleased;
                rightResizeGrip.PointerMoved += PaneResizeGrip_OnPointerMoved;
            }

            var topResizeGrip = this.FindControl<Border>("TopPaneResizeGrip");
            if (topResizeGrip is not null)
            {
                topResizeGrip.Tag = "top";
                topResizeGrip.PointerPressed += PaneResizeGrip_OnPointerPressed;
                topResizeGrip.PointerReleased += PaneResizeGrip_OnPointerReleased;
                topResizeGrip.PointerMoved += PaneResizeGrip_OnPointerMoved;
            }

            var bottomResizeGrip = this.FindControl<Border>("BottomPaneResizeGrip");
            if (bottomResizeGrip is not null)
            {
                bottomResizeGrip.Tag = "bottom";
                bottomResizeGrip.PointerPressed += PaneResizeGrip_OnPointerPressed;
                bottomResizeGrip.PointerReleased += PaneResizeGrip_OnPointerReleased;
                bottomResizeGrip.PointerMoved += PaneResizeGrip_OnPointerMoved;
            }
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
                    width = windowWidth * 0.3;
                    height = windowHeight * 0.3;
                    left = pointer.X - (width / 2.0);
                    top = pointer.Y - (height / 2.0);

                    if (left < 0) left = 0;
                    if (top < 0) top = 0;
                    if (left + width > windowWidth) left = windowWidth - width;
                    if (top + height > windowHeight) top = windowHeight - height;
                    break;
            }
        }

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
            }
        }

        private void ShellPaneHost_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPaneResizing)
            {
                return;
            }

            if (!_isShellPaneDragging)
            {
                return;
            }

            _isShellPaneDragging = false;
            var releasePoint = e.GetPosition(this);

            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            // Dock based on drop region:
            // - near left edge   → left host
            // - near right edge  → right host
            // - near top edge    → top host
            // - near bottom edge → bottom host
            // - interior region  → floating host
            var width = Bounds.Width;
            var height = Bounds.Height;
            if (width <= 0 || height <= 0)
            {
                vm.SetParentPaneDragShadow(false, 0, 0, 0, 0);
                return;
            }

            var targetHost = GetTargetHostForPoint(releasePoint, width, height);
            vm.MoveShellPaneToHost(targetHost);
            vm.SetParentPaneDragShadow(false, 0, 0, 0, 0);
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

            try { e.Pointer.Capture(this); } catch { }
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

    public sealed record PaneHostDescriptor(
        string Id,
        string DisplayName,
        string HostElementName);

    /// <summary>
    /// Minimal descriptor for a logical pane in the 2D World. For v0.1 this is
    /// a simple record that captures identity, title, and host placement
    /// (which parent-pane host it belongs to, whether it is floating, and
    /// whether it is minimized). Future passes can extend this into a richer
    /// ShellLayoutViewModel without changing the initial contract.
    /// </summary>
    public sealed record PaneDescriptor(
        string Id,
        string Title,
        string HostId,
        bool IsFloating = false,
        bool IsMinimized = false);

    public sealed record ChildPaneDescriptor(
        string Id,
        string Title,
        int Order,
        bool IsMinimized = false);

    public sealed record ShellLayoutDescriptor(
        string HostId,
        bool IsMinimized,
        string? SavedHostId = null,
        bool SavedIsMinimized = false);

    public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            IncludeFields = true
        };
        private const string ShellLayoutFileName = "shell-layout.json";
        private readonly IDisposable[] _eventSubscriptions;
        private readonly ShellSceneState _shellScene = EngineServices.ShellScene;

        private readonly RelayCommand _focusFirstNodeCommand;
        private readonly RelayCommand _selectFirstNodeCommand;
        private readonly RelayCommand _focusFirstPanelCommand;
        private readonly RelayCommand _selectFirstPanelCommand;
        private readonly RelayCommand _activateNavigateModeCommand;
        private readonly RelayCommand _activateMoveModeCommand;
        private readonly RelayCommand _activateMarqueeModeCommand;
        private readonly RelayCommand _createDemoNodeCommand;
        private readonly RelayCommand _nudgeFocusedLeftCommand;
        private readonly RelayCommand _nudgeFocusedRightCommand;
        private readonly RelayCommand _nudgeFocusedUpCommand;
        private readonly RelayCommand _nudgeFocusedDownCommand;
        private readonly RelayCommand _nudgeFocusedForwardCommand;
        private readonly RelayCommand _nudgeFocusedBackCommand;
        private readonly RelayCommand _growFocusedNodeCommand;
        private readonly RelayCommand _shrinkFocusedNodeCommand;
        private readonly RelayCommand _applyTrianglePrimitiveCommand;
        private readonly RelayCommand _applySquarePrimitiveCommand;
        private readonly RelayCommand _applyDiamondPrimitiveCommand;
        private readonly RelayCommand _applyPentagonPrimitiveCommand;
        private readonly RelayCommand _applyHexagonPrimitiveCommand;
        private readonly RelayCommand _applyCubePrimitiveCommand;
        private readonly RelayCommand _applyTetrahedronPrimitiveCommand;
        private readonly RelayCommand _applySpherePrimitiveCommand;
        private readonly RelayCommand _applyBoxPrimitiveCommand;
        private readonly RelayCommand _applyBlueAppearanceCommand;
        private readonly RelayCommand _applyVioletAppearanceCommand;
        private readonly RelayCommand _applyGreenAppearanceCommand;
        private readonly RelayCommand _increaseOpacityCommand;
        private readonly RelayCommand _decreaseOpacityCommand;
        private readonly RelayCommand _connectFocusedNodeCommand;
        private readonly RelayCommand _groupSelectionCommand;
        private readonly RelayCommand _unlinkFocusedNodeCommand;
        private readonly RelayCommand _saveBookmarkCommand;
        private readonly RelayCommand _addSelectionToActiveGroupCommand;
        private readonly RelayCommand _removeSelectionFromActiveGroupCommand;
        private readonly RelayCommand _deleteActiveGroupCommand;
        private readonly RelayCommand _restoreLatestBookmarkCommand;
        private readonly RelayCommand _undoLastCommand;
        private readonly RelayCommand _deleteFocusedNodeCommand;
        private readonly RelayCommand _attachDemoPanelCommand;
        private readonly RelayCommand _attachLabelPaneletteCommand;
        private readonly RelayCommand _attachDetailMetadataPaneletteCommand;
        private readonly RelayCommand _homeViewCommand;
        private readonly RelayCommand _centerFocusedNodeCommand;
        private readonly RelayCommand _frameSelectionCommand;
        private readonly RelayCommand _enterFocusedNodeCommand;
        private readonly RelayCommand _exitNodeCommand;
        private readonly RelayCommand _clearLinksCommand;
        private readonly RelayCommand _clearSelectionCommand;
        private readonly RelayCommand _applyBackgroundDeepSpaceCommand;
        private readonly RelayCommand _applyBackgroundDuskCommand;
        private readonly RelayCommand _applyBackgroundPaperCommand;
        private readonly RelayCommand _minimizeShellPaneCommand;
        private readonly RelayCommand _restoreShellPaneCommand;
        private readonly RelayCommand _resetLayoutToDefaultCommand;
        private readonly RelayCommand _saveLayoutPresetCommand;
        private readonly RelayCommand _restoreLayoutPresetCommand;
        private readonly RelayCommand _minimizeChildPaneCommand;
        private readonly RelayCommand _restoreChildPaneFromTaskbarCommand;
        private readonly RelayCommand _moveChildPaneUpCommand;
        private readonly RelayCommand _moveChildPaneDownCommand;
        private readonly RelayCommand _floatSettingsChildPaneCommand;
        private readonly RelayCommand _dockSettingsChildPaneCommand;
        private readonly RelayCommand _destroyParentPaneCommand;
        private readonly RelayCommand _createOrRestoreParentPaneCommand;

        private string _lastActivitySummary = "Last Activity: app started";
        private readonly Queue<string> _commandHistory = new();
        private bool _isCurrentStateSectionExpanded = true;
        private bool _isCommandSurfaceSectionExpanded = true;
        private bool _isSelectionFocusGroupExpanded = true;
        private bool _isLinksGroupExpanded = true;
        private bool _isGroupsGroupExpanded;
        private bool _isHistoryGroupExpanded;
        private bool _isViewGroupExpanded;
        private bool _isEditModesGroupExpanded;
        private bool _isMutationGroupExpanded;
        private bool _isAppearanceGroupExpanded;
        private bool _isDeveloperReadoutsSectionExpanded;
        private bool _isCapabilitiesSectionExpanded;
        private bool _isSettingsSectionExpanded;
        private bool _isSettingsChildFloating;
        private bool _isParentPaneDragShadowVisible;
        private double _parentPaneDragShadowLeft;
        private double _parentPaneDragShadowTop;
        private double _parentPaneDragShadowWidth;
        private double _parentPaneDragShadowHeight;
        private bool _mouseLeaveClearsFocus = EngineServices.Settings.MouseLeaveClearsFocus;
        private float _groupOverlayOpacity = EngineServices.Settings.GroupOverlayOpacity;
        private float _nodeHighlightOpacity = EngineServices.Settings.NodeHighlightOpacity;
        private float _nodeFocusHaloRadiusMultiplier = EngineServices.Settings.NodeFocusHaloRadiusMultiplier;
        private float _nodeSelectionHaloRadiusMultiplier = EngineServices.Settings.NodeSelectionHaloRadiusMultiplier;
        private string _nodeHaloMode = EngineServices.Settings.NodeHaloMode;
        private string _nodeHaloOcclusionMode = EngineServices.Settings.NodeHaloOcclusionMode;
        private float _backgroundAnimationSpeed = EngineServices.Settings.BackgroundAnimationSpeed;
        private float _linkStrokeThickness = EngineServices.Settings.LinkStrokeThickness;
        private float _linkOpacity = EngineServices.Settings.LinkOpacity;
        private float _paneletteBackgroundIntensity = EngineServices.Settings.PaneletteBackgroundIntensity;
        private float _commandSurfaceOverlayOpacity = EngineServices.Settings.CommandSurfaceOverlayOpacity;

        public ObservableCollection<EngineCapability> Capabilities { get; } =
            new(EngineServices.Capabilities.GetAll());

        /// <summary>
        /// Declarative taxonomy of parent-pane hosts in the main window. This is the
        /// first step toward a ShellLayoutViewModel/PaneDescriptor model so that
        /// additional top/right/bottom/floating parents can be added without
        /// restructuring MainWindow.axaml again.
        /// </summary>
        public IReadOnlyList<PaneHostDescriptor> PaneHosts { get; } =
            new[]
            {
                new PaneHostDescriptor("left", "Shell Sidebar", "LeftPaneHost"),
                new PaneHostDescriptor("top", "Viewport Header", "TopPaneHost"),
                new PaneHostDescriptor("right", "Right Sidebar", "RightPaneHost"),
                new PaneHostDescriptor("bottom", "Bottom Pane Host", "BottomPaneHost"),
                new PaneHostDescriptor("center", "Viewport", "CenterViewportHost")
            };

        /// <summary>
        /// Minimal pane layout model for the current 2D World. For v0.31 this
        /// contains a single shell sidebar pane hosted on the left; later D2/D3
        /// slices will extend this collection and bind docking/floating behavior
        /// to these descriptors instead of hardcoding layout in XAML.
        /// </summary>
        public ObservableCollection<PaneDescriptor> Panes { get; } =
            new(
                new[]
                {
                    // Treat this as a generic parent pane descriptor. It starts minimized so
                    // the app launches in pure 3D World mode; corner affordances can move
                    // and restore it onto any edge.
                    new PaneDescriptor("parent.main", "Parent Pane", "left", IsFloating: false, IsMinimized: true)
                });

        public ObservableCollection<ChildPaneDescriptor> ChildPanes { get; } =
            new(
                new[]
                {
                    new ChildPaneDescriptor("shell.current", "Current State & Command Surface", 0),
                    new ChildPaneDescriptor("shell.settings", "Settings", 1),
                    new ChildPaneDescriptor("shell.developer", "Developer Readouts", 2),
                    new ChildPaneDescriptor("shell.capabilities", "Engine Capabilities", 3)
                });

        public IReadOnlyList<ChildPaneDescriptor> ChildPanesOrdered =>
            ChildPanes
                .OrderBy(pane => pane.Order)
                .ToArray();

        public bool HasMinimizedChildPanes =>
            ChildPanes.Any(pane => pane.IsMinimized);

        public IEnumerable<ChildPaneDescriptor> MinimizedChildPanes =>
            ChildPanesOrdered.Where(pane => pane.IsMinimized);

        public bool IsShellCurrentChildVisible => !IsChildPaneMinimized("shell.current");
        public bool IsShellSettingsChildVisible => !IsChildPaneMinimized("shell.settings") && !IsSettingsChildFloating;
        public bool IsShellDeveloperChildVisible => !IsChildPaneMinimized("shell.developer");
        public bool IsShellCapabilitiesChildVisible => !IsChildPaneMinimized("shell.capabilities");

        private bool IsChildPaneMinimized(string id)
        {
            foreach (var pane in ChildPanes)
            {
                if (string.Equals(pane.Id, id, StringComparison.Ordinal))
                {
                    return pane.IsMinimized;
                }
            }

            return false;
        }

        public bool IsShellPaneOnLeft =>
            Panes.Count > 0 &&
            !Panes[0].IsMinimized &&
            string.Equals(Panes[0].HostId, "left", StringComparison.Ordinal);

        public bool IsShellPaneOnTop =>
            Panes.Count > 0 &&
            !Panes[0].IsMinimized &&
            string.Equals(Panes[0].HostId, "top", StringComparison.Ordinal);

        public bool IsShellPaneOnRight =>
            Panes.Count > 0 &&
            !Panes[0].IsMinimized &&
            string.Equals(Panes[0].HostId, "right", StringComparison.Ordinal);

        public bool IsShellPaneOnBottom =>
            Panes.Count > 0 &&
            !Panes[0].IsMinimized &&
            string.Equals(Panes[0].HostId, "bottom", StringComparison.Ordinal);

        public bool IsShellPaneFloating =>
            Panes.Count > 0 &&
            !Panes[0].IsMinimized &&
            string.Equals(Panes[0].HostId, "floating", StringComparison.Ordinal);

        public bool IsShellPaneMinimized =>
            Panes.Count > 0 && Panes[0].IsMinimized;

        public bool IsRightPaneHostVisible =>
            Panes.Any(p =>
                !p.IsMinimized &&
                string.Equals(p.HostId, "right", StringComparison.Ordinal));

        public string[] NodeHaloModeOptions { get; } = new[] { "2d", "3d", "both" };
        public string[] NodeHaloOcclusionModeOptions { get; } = new[] { "hollow", "occluding" };

        public ICommand FocusFirstNodeCommand => _focusFirstNodeCommand;
        public ICommand SelectFirstNodeCommand => _selectFirstNodeCommand;
        public ICommand FocusFirstPanelCommand => _focusFirstPanelCommand;
        public ICommand ActivateMoveModeCommand => _activateMoveModeCommand;
        public ICommand ActivateNavigateModeCommand => _activateNavigateModeCommand;
        public ICommand ActivateMarqueeModeCommand => _activateMarqueeModeCommand;
        public ICommand SelectFirstPanelCommand => _selectFirstPanelCommand;
        public ICommand CreateDemoNodeCommand => _createDemoNodeCommand;
        public ICommand NudgeFocusedLeftCommand => _nudgeFocusedLeftCommand;
        public ICommand NudgeFocusedRightCommand => _nudgeFocusedRightCommand;
        public ICommand NudgeFocusedUpCommand => _nudgeFocusedUpCommand;
        public ICommand NudgeFocusedDownCommand => _nudgeFocusedDownCommand;
        public ICommand NudgeFocusedForwardCommand => _nudgeFocusedForwardCommand;
        public ICommand NudgeFocusedBackCommand => _nudgeFocusedBackCommand;
        public ICommand GrowFocusedNodeCommand => _growFocusedNodeCommand;
        public ICommand ShrinkFocusedNodeCommand => _shrinkFocusedNodeCommand;
        public ICommand ApplyTrianglePrimitiveCommand => _applyTrianglePrimitiveCommand;
        public ICommand ApplySquarePrimitiveCommand => _applySquarePrimitiveCommand;
        public ICommand ApplyDiamondPrimitiveCommand => _applyDiamondPrimitiveCommand;
        public ICommand ApplyPentagonPrimitiveCommand => _applyPentagonPrimitiveCommand;
        public ICommand ApplyHexagonPrimitiveCommand => _applyHexagonPrimitiveCommand;
        public ICommand ApplyCubePrimitiveCommand => _applyCubePrimitiveCommand;
        public ICommand ApplyTetrahedronPrimitiveCommand => _applyTetrahedronPrimitiveCommand;
        public ICommand ApplySpherePrimitiveCommand => _applySpherePrimitiveCommand;
        public ICommand ApplyBoxPrimitiveCommand => _applyBoxPrimitiveCommand;
        public ICommand ApplyBlueAppearanceCommand => _applyBlueAppearanceCommand;
        public ICommand ApplyVioletAppearanceCommand => _applyVioletAppearanceCommand;
        public ICommand ApplyGreenAppearanceCommand => _applyGreenAppearanceCommand;
        public ICommand IncreaseOpacityCommand => _increaseOpacityCommand;
        public ICommand DecreaseOpacityCommand => _decreaseOpacityCommand;
        public ICommand ConnectFocusedNodeCommand => _connectFocusedNodeCommand;
        public ICommand GroupSelectionCommand => _groupSelectionCommand;
        public ICommand UnlinkFocusedNodeCommand => _unlinkFocusedNodeCommand;
        public ICommand SaveBookmarkCommand => _saveBookmarkCommand;
        public ICommand AddSelectionToActiveGroupCommand => _addSelectionToActiveGroupCommand;
        public ICommand RemoveSelectionFromActiveGroupCommand => _removeSelectionFromActiveGroupCommand;
        public ICommand DeleteActiveGroupCommand => _deleteActiveGroupCommand;
        public ICommand RestoreLatestBookmarkCommand => _restoreLatestBookmarkCommand;
        public ICommand UndoLastCommand => _undoLastCommand;
        public ICommand DeleteFocusedNodeCommand => _deleteFocusedNodeCommand;
        public ICommand AttachDemoPanelCommand => _attachDemoPanelCommand;
        public ICommand AttachLabelPaneletteCommand => _attachLabelPaneletteCommand;
        public ICommand AttachDetailMetadataPaneletteCommand => _attachDetailMetadataPaneletteCommand;
        public ICommand HomeViewCommand => _homeViewCommand;
        public ICommand CenterFocusedNodeCommand => _centerFocusedNodeCommand;
        public ICommand FrameSelectionCommand => _frameSelectionCommand;
        public ICommand EnterFocusedNodeCommand => _enterFocusedNodeCommand;
        public ICommand ExitNodeCommand => _exitNodeCommand;
        public ICommand ClearLinksCommand => _clearLinksCommand;
        public ICommand ClearSelectionCommand => _clearSelectionCommand;
        public ICommand ApplyBackgroundDeepSpaceCommand => _applyBackgroundDeepSpaceCommand;
        public ICommand ApplyBackgroundDuskCommand => _applyBackgroundDuskCommand;
        public ICommand ApplyBackgroundPaperCommand => _applyBackgroundPaperCommand;
        public ICommand MinimizeShellPaneCommand => _minimizeShellPaneCommand;
        public ICommand RestoreShellPaneCommand => _restoreShellPaneCommand;
        public ICommand ResetLayoutToDefaultCommand => _resetLayoutToDefaultCommand;
        public ICommand SaveLayoutPresetCommand => _saveLayoutPresetCommand;
        public ICommand RestoreLayoutPresetCommand => _restoreLayoutPresetCommand;
        public ICommand MinimizeChildPaneCommand => _minimizeChildPaneCommand;
        public ICommand RestoreChildPaneFromTaskbarCommand => _restoreChildPaneFromTaskbarCommand;
        public ICommand MoveChildPaneUpCommand => _moveChildPaneUpCommand;
        public ICommand MoveChildPaneDownCommand => _moveChildPaneDownCommand;
        public ICommand FloatSettingsChildPaneCommand => _floatSettingsChildPaneCommand;
        public ICommand DockSettingsChildPaneCommand => _dockSettingsChildPaneCommand;
        public ICommand DestroyParentPaneCommand => _destroyParentPaneCommand;
        public ICommand CreateOrRestoreParentPaneCommand => _createOrRestoreParentPaneCommand;

        public bool IsCurrentStateSectionExpanded
        {
            get => _isCurrentStateSectionExpanded;
            set => SetExpansionState(ref _isCurrentStateSectionExpanded, value);
        }

        public bool IsCommandSurfaceSectionExpanded
        {
            get => _isCommandSurfaceSectionExpanded;
            set => SetExpansionState(ref _isCommandSurfaceSectionExpanded, value);
        }

        public bool IsSettingsSectionExpanded
        {
            get => _isSettingsSectionExpanded;
            set => SetExpansionState(ref _isSettingsSectionExpanded, value);
        }

        public bool IsSelectionFocusGroupExpanded
        {
            get => _isSelectionFocusGroupExpanded;
            set => SetExpansionState(ref _isSelectionFocusGroupExpanded, value);
        }

        public bool IsLinksGroupExpanded
        {
            get => _isLinksGroupExpanded;
            set => SetExpansionState(ref _isLinksGroupExpanded, value);
        }

        public bool IsGroupsGroupExpanded
        {
            get => _isGroupsGroupExpanded;
            set => SetExpansionState(ref _isGroupsGroupExpanded, value);
        }

        public bool IsHistoryGroupExpanded
        {
            get => _isHistoryGroupExpanded;
            set => SetExpansionState(ref _isHistoryGroupExpanded, value);
        }

        public bool IsViewGroupExpanded
        {
            get => _isViewGroupExpanded;
            set => SetExpansionState(ref _isViewGroupExpanded, value);
        }

        public bool IsEditModesGroupExpanded
        {
            get => _isEditModesGroupExpanded;
            set => SetExpansionState(ref _isEditModesGroupExpanded, value);
        }

        public bool IsMutationGroupExpanded
        {
            get => _isMutationGroupExpanded;
            set => SetExpansionState(ref _isMutationGroupExpanded, value);
        }

        public bool IsAppearanceGroupExpanded
        {
            get => _isAppearanceGroupExpanded;
            set => SetExpansionState(ref _isAppearanceGroupExpanded, value);
        }

        public bool IsDeveloperReadoutsSectionExpanded
        {
            get => _isDeveloperReadoutsSectionExpanded;
            set => SetExpansionState(ref _isDeveloperReadoutsSectionExpanded, value);
        }

        public bool IsCapabilitiesSectionExpanded
        {
            get => _isCapabilitiesSectionExpanded;
            set => SetExpansionState(ref _isCapabilitiesSectionExpanded, value);
        }

        public bool IsSettingsChildFloating
        {
            get => _isSettingsChildFloating;
            set
            {
                if (_isSettingsChildFloating == value)
                {
                    return;
                }

                _isSettingsChildFloating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsShellSettingsChildVisible));
            }
        }

        public bool IsParentPaneDragShadowVisible
        {
            get => _isParentPaneDragShadowVisible;
            private set
            {
                if (_isParentPaneDragShadowVisible == value)
                {
                    return;
                }

                _isParentPaneDragShadowVisible = value;
                OnPropertyChanged();
            }
        }

        public double ParentPaneDragShadowLeft
        {
            get => _parentPaneDragShadowLeft;
            private set { if (Math.Abs(_parentPaneDragShadowLeft - value) > double.Epsilon) { _parentPaneDragShadowLeft = value; OnPropertyChanged(); } }
        }

        public double ParentPaneDragShadowTop
        {
            get => _parentPaneDragShadowTop;
            private set { if (Math.Abs(_parentPaneDragShadowTop - value) > double.Epsilon) { _parentPaneDragShadowTop = value; OnPropertyChanged(); } }
        }

        public double ParentPaneDragShadowWidth
        {
            get => _parentPaneDragShadowWidth;
            private set { if (Math.Abs(_parentPaneDragShadowWidth - value) > double.Epsilon) { _parentPaneDragShadowWidth = value; OnPropertyChanged(); } }
        }

        public double ParentPaneDragShadowHeight
        {
            get => _parentPaneDragShadowHeight;
            private set { if (Math.Abs(_parentPaneDragShadowHeight - value) > double.Epsilon) { _parentPaneDragShadowHeight = value; OnPropertyChanged(); } }
        }

        public MainWindowViewModel()
        {
            _eventSubscriptions =
            [
                SubscribeRefresh(EventNames.CommandInvoked, "command activity"),
                SubscribeRefresh(EventNames.SceneChanged, "scene changed"),
                SubscribePanelInteraction(),
                SubscribeRefresh(EventNames.FocusChanged, "focus changed"),
                SubscribeRefresh(EventNames.PanelFocusChanged, "panel focus changed"),
                SubscribeRefresh(EventNames.SelectionChanged, "selection changed"),
                SubscribeRefresh(EventNames.PanelAttachmentsChanged, "panel attachments changed"),
                SubscribeRefresh(EventNames.InteractionModeChanged, "interaction mode changed"),
                SubscribeRefresh(EventNames.GroupChanged, "group changed"),
                SubscribeRefresh(EventNames.FocusOriginChanged, "focus origin changed")
            ];

            _focusFirstNodeCommand = new RelayCommand(
                _ =>
                {
                    var firstNode = _shellScene.GetNodes().FirstOrDefault();
                    if (firstNode is not null)
                    {
                        PublishFocusOrigin("command");
                        SendCommand(
                            CommandNames.Focus,
                            new FocusEntityPayload(firstNode.Id.ToString()));
                    }
                },
                _ => _shellScene.GetNodes().Count > 0);

            _selectFirstNodeCommand = new RelayCommand(
                _ =>
                {
                    var firstNode = _shellScene.GetNodes().FirstOrDefault();
                    if (firstNode is not null)
                    {
                        SendCommand(
                            CommandNames.Select,
                            new SelectEntitiesPayload([firstNode.Id.ToString()]));
                    }
                },
                _ => _shellScene.GetNodes().Count > 0);

            _focusFirstPanelCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFirstPanelTarget() is { } panelTarget)
                    {
                        PublishFocusOrigin("command");
                        SendCommand(
                            CommandNames.FocusPanel,
                            new FocusPanelPayload(
                                panelTarget.NodeId.ToString(),
                                panelTarget.ViewRef));
                    }
                },
                _ => _shellScene.GetFirstPanelTarget() is not null);

            _selectFirstPanelCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFirstPanelTarget() is { } panelTarget)
                    {
                        SendCommand(
                            CommandNames.SelectPanel,
                            new SelectPanelPayload(
                                panelTarget.NodeId.ToString(),
                                panelTarget.ViewRef));
                    }
                },
                _ => _shellScene.GetFirstPanelTarget() is not null);

            _activateNavigateModeCommand = new RelayCommand(
                _ => SendCommand(CommandNames.SetInteractionMode, new SetInteractionModePayload("navigate")),
                _ => !IsInteractionMode("navigate"));

            _activateMoveModeCommand = new RelayCommand(
                _ => SendCommand(CommandNames.SetInteractionMode, new SetInteractionModePayload("move")),
                _ => !IsInteractionMode("move"));

            _activateMarqueeModeCommand = new RelayCommand(
                _ => SendCommand(CommandNames.SetInteractionMode, new SetInteractionModePayload("marquee")),
                _ => !IsInteractionMode("marquee"));

            _createDemoNodeCommand = new RelayCommand(_ =>
            {
                var index = _shellScene.GetNodes().Count + 1;
                var angle = (float)(index * 0.85);
                var radius = 0.55f + (0.08f * (index % 3));
                var position = new Vector3(
                    MathF.Cos(angle) * radius,
                    MathF.Sin(angle) * radius,
                    0f);

                SendCommand(
                    CommandNames.CreateEntity,
                    new CreateEntityPayload(
                        Type: "node",
                        Id: null,
                        Label: $"Demo Node {index}",
                        Position: position,
                        RotationEuler: Vector3.Zero,
                        Scale: new Vector3(0.45f, 0.45f, 0.45f),
                        VisualScale: 0.45f,
                        Phase: index * 0.35f));
            });

            _nudgeFocusedLeftCommand = CreateSelectionOrFocusTransformCommand(new Vector3(-0.12f, 0f, 0f), 1f);
            _nudgeFocusedRightCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0.12f, 0f, 0f), 1f);
            _nudgeFocusedUpCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0f, 0.08f, 0f), 1f);
            _nudgeFocusedDownCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0f, -0.08f, 0f), 1f);
            _nudgeFocusedForwardCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0f, 0f, -0.12f), 1f);
            _nudgeFocusedBackCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0f, 0f, 0.12f), 1f);
            _growFocusedNodeCommand = CreateSelectionOrFocusTransformCommand(Vector3.Zero, 1.15f);
            _shrinkFocusedNodeCommand = CreateSelectionOrFocusTransformCommand(Vector3.Zero, 1f / 1.15f);
            _applyTrianglePrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "triangle");
            _applySquarePrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "square");
            _applyDiamondPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "diamond");
            _applyPentagonPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "pentagon");
            _applyHexagonPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "hexagon");
            _applyCubePrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "cube");
            _applyTetrahedronPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "tetrahedron");
            _applySpherePrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "sphere");
            _applyBoxPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "box");
            _applyBlueAppearanceCommand = CreateSelectionOrFocusAppearanceCommand(fillColor: "#7DCBFF");
            _applyVioletAppearanceCommand = CreateSelectionOrFocusAppearanceCommand(fillColor: "#B69CFF");
            _applyGreenAppearanceCommand = CreateSelectionOrFocusAppearanceCommand(fillColor: "#86E0A5");
            _increaseOpacityCommand = CreateSelectionOrFocusAppearanceCommand(opacityDelta: 0.15f);
            _decreaseOpacityCommand = CreateSelectionOrFocusAppearanceCommand(opacityDelta: -0.15f);

            _connectFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var sourceNodeId = _shellScene.GetSelectedNodeIds()
                        .FirstOrDefault(nodeId => nodeId != focusedNode.Id);

                    if (sourceNodeId == default)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.Connect,
                        new ConnectEntitiesPayload(
                            sourceNodeId.ToString(),
                            focusedNode.Id.ToString(),
                            Kind: "directed",
                            Weight: 1.0f));
                },
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return false;
                    }

                    return _shellScene.GetSelectedNodeIds().Any(nodeId => nodeId != focusedNode.Id);
                });

            _unlinkFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var sourceNodeId = _shellScene.GetSelectedNodeIds()
                        .FirstOrDefault(nodeId => nodeId != focusedNode.Id);

                    if (sourceNodeId == default)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.Unlink,
                        new UnlinkEntitiesPayload(
                            sourceNodeId.ToString(),
                            focusedNode.Id.ToString(),
                            Kind: "directed"));
                },
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return false;
                    }

                    var sourceNodeId = _shellScene.GetSelectedNodeIds()
                        .FirstOrDefault(nodeId => nodeId != focusedNode.Id);

                    return sourceNodeId != default &&
                           _shellScene.GetLinks().Any(link =>
                               link.SourceId == sourceNodeId &&
                               link.TargetId == focusedNode.Id &&
                               string.Equals(link.Kind, "directed", StringComparison.Ordinal));
                });

            _groupSelectionCommand = new RelayCommand(
                _ =>
                {
                    var selectedCount = _shellScene.GetSelectedNodeIds().Count;
                    SendCommand(
                        CommandNames.GroupSelection,
                        new GroupSelectionPayload($"Group {(_shellScene.GetGroups().Count + 1)} ({selectedCount} nodes)"));
                },
                _ => _shellScene.GetSelectedNodeIds().Count >= 2);

            _addSelectionToActiveGroupCommand = new RelayCommand(
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    if (activeGroup is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.AddSelectionToGroup,
                        new GroupMembershipPayload(activeGroup.Id));
                },
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    return activeGroup is not null &&
                           _shellScene.GetSelectedNodeIds().Any(nodeId => !activeGroup.NodeIds.Contains(nodeId));
                });

            _removeSelectionFromActiveGroupCommand = new RelayCommand(
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    if (activeGroup is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.RemoveSelectionFromGroup,
                        new GroupMembershipPayload(activeGroup.Id));
                },
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    return activeGroup is not null &&
                           _shellScene.GetSelectedNodeIds().Any(nodeId => activeGroup.NodeIds.Contains(nodeId));
                });

            _deleteActiveGroupCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetActiveGroup() is { } activeGroup)
                    {
                        SendCommand(CommandNames.DeleteGroup, new DeleteGroupPayload(activeGroup.Id));
                    }
                },
                _ => _shellScene.GetActiveGroup() is not null);

            _saveBookmarkCommand = new RelayCommand(
                _ =>
                {
                    var index = _shellScene.GetBookmarks().Count + 1;
                    SendCommand(
                        CommandNames.BookmarkSave,
                        new BookmarkSavePayload($"Bookmark {index}"));
                },
                _ =>
                {
                    return _shellScene.GetFocusedNode() is not null ||
                           _shellScene.GetFocusedPanel() is not null ||
                           _shellScene.GetSelectedNodeIds().Count > 0 ||
                           _shellScene.GetSelectedPanels().Count > 0;
                });

            _restoreLatestBookmarkCommand = new RelayCommand(
                _ =>
                {
                    var latest = _shellScene.GetBookmarks()
                        .OrderBy(bookmark => bookmark.Name, StringComparer.Ordinal)
                        .LastOrDefault();

                    if (latest is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.BookmarkRestore,
                        new BookmarkRestorePayload(latest.Name));
                },
                _ => _shellScene.GetBookmarks().Count > 0);

            _undoLastCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.Undo, null);
                },
                _ => EngineServices.Scene.CanUndo);

            _homeViewCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.HomeView, null);
                });

            _centerFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFocusedNode() is { } focusedNode)
                    {
                        SendCommand(
                            CommandNames.CenterOnNode,
                            new CenterOnNodePayload(focusedNode.Id.ToString()));
                    }
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _frameSelectionCommand = new RelayCommand(
                _ =>
                {
                    SendCommand(
                        CommandNames.FrameSelection,
                        new FrameSelectionPayload());
                },
                _ => _shellScene.GetSelectedNodeIds().Count > 0 || _shellScene.GetFocusedNode() is not null);

            _enterFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.EnterNode,
                        new EnterNodePayload(focusedNode.Id.ToString()));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _exitNodeCommand = new RelayCommand(
                _ =>
                {
                    var enteredId = _shellScene.GetEnteredNodeId();
                    if (enteredId is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.ExitNode,
                        new ExitNodePayload(enteredId.Value.ToString()));
                },
                _ => _shellScene.GetEnteredNodeId() is not null);

            _deleteFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var targetNodes = GetSelectionOrFocusTargetNodes();
                    if (targetNodes.Length == 0)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.DeleteEntities,
                        new DeleteEntitiesPayload(
                            targetNodes
                                .Select(node => node.Id.ToString())
                                .ToArray()));
                },
                _ => GetSelectionOrFocusTargetNodes().Length > 0);

            _attachDemoPanelCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var attachmentCount = _shellScene.GetPanelAttachments().Count;
                    var viewRef = $"panelette.meta.{attachmentCount + 1}";

                    SendCommand(
                        CommandNames.AttachPanel,
                        new AttachPanelPayload(
                                focusedNode.Id.ToString(),
                            viewRef,
                            LocalOffset: new Vector3(0f, 0.18f, 0.15f),
                            Size: new Vector2(1.05f, 0.62f),
                            Anchor: "top",
                            IsVisible: true,
                            SurfaceKind: "panelette",
                            PaneletteKind: "metadata",
                                PaneletteTier: 1,
                            CommandSurface: new PanelCommandSurfaceMetadataPayload(
                                SurfaceName: "node.quick",
                                SurfaceGroup: "primary",
                                    CommandIds: [CommandNames.Focus, CommandNames.Select, CommandNames.CenterOnNode, "Engine.PromotePaneletteToShell"])));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _attachLabelPaneletteCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var attachmentCount = _shellScene.GetPanelAttachments().Count;
                    var viewRef = $"panelette.label.{attachmentCount + 1}";

                    SendCommand(
                        CommandNames.AttachPanel,
                        new AttachPanelPayload(
                            focusedNode.Id.ToString(),
                            viewRef,
                            LocalOffset: new Vector3(0f, -0.18f, 0.1f),
                            Size: new Vector2(0.92f, 0.28f),
                            Anchor: "bottom",
                            IsVisible: true,
                            SurfaceKind: "panelette",
                            PaneletteKind: "label",
                            PaneletteTier: 1));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _attachDetailMetadataPaneletteCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var attachmentCount = _shellScene.GetPanelAttachments().Count;
                    var viewRef = $"panelette.meta.detail.{attachmentCount + 1}";

                    SendCommand(
                        CommandNames.AttachPanel,
                        new AttachPanelPayload(
                            focusedNode.Id.ToString(),
                            viewRef,
                            LocalOffset: new Vector3(0f, 0.26f, 0.16f),
                            Size: new Vector2(1.35f, 0.82f),
                            Anchor: "top",
                            IsVisible: true,
                            SurfaceKind: "panelette",
                            PaneletteKind: "metadata",
                            PaneletteTier: 2,
                            CommandSurface: new PanelCommandSurfaceMetadataPayload(
                                SurfaceName: "node.detail",
                                SurfaceGroup: "primary",
                                CommandIds: ["Engine.PromotePaneletteToShell"])));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _clearLinksCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.ClearLinks, null);
                },
                _ => _shellScene.GetLinks().Count > 0);

            _clearSelectionCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.ClearSelection, null);
                },
                _ => _shellScene.GetSelectedNodeIds().Count > 0 || _shellScene.GetSelectedPanels().Count > 0);

            _applyBackgroundDeepSpaceCommand = new RelayCommand(
                _ => ApplyBackgroundPreset("DeepSpace"));
            _applyBackgroundDuskCommand = new RelayCommand(
                _ => ApplyBackgroundPreset("Dusk"));
            _applyBackgroundPaperCommand = new RelayCommand(
                _ => ApplyBackgroundPreset("Paper"));

            _minimizeChildPaneCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is string id && !string.IsNullOrWhiteSpace(id))
                    {
                        SetChildPaneMinimized(id, true);
                    }
                });

            _restoreChildPaneFromTaskbarCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is string id && !string.IsNullOrWhiteSpace(id))
                    {
                        SetChildPaneMinimized(id, false);
                    }
                });

            _minimizeShellPaneCommand = new RelayCommand(
                _ => SetShellPaneMinimized(true),
                _ => Panes.Count > 0 && !Panes[0].IsMinimized);

            _restoreShellPaneCommand = new RelayCommand(
                _ => SetShellPaneMinimized(false),
                _ => Panes.Count > 0 && Panes[0].IsMinimized);

            _resetLayoutToDefaultCommand = new RelayCommand(
                _ =>
                {
                    if (Panes.Count == 0)
                    {
                        return;
                    }

                    var current = Panes[0];
                    var normalizedHost = "left";
                    Panes[0] = current with { HostId = normalizedHost, IsMinimized = false };

                    OnPropertyChanged(nameof(IsShellPaneOnLeft));
                    OnPropertyChanged(nameof(IsShellPaneOnTop));
                    OnPropertyChanged(nameof(IsShellPaneOnRight));
                    OnPropertyChanged(nameof(IsShellPaneOnBottom));
                    OnPropertyChanged(nameof(IsShellPaneFloating));
                    OnPropertyChanged(nameof(IsShellPaneMinimized));
                    OnPropertyChanged(nameof(PaneStructureSummary));
                    _minimizeShellPaneCommand.RaiseCanExecuteChanged();
                    _restoreShellPaneCommand.RaiseCanExecuteChanged();
                    SaveShellLayout();
                });

            _saveLayoutPresetCommand = new RelayCommand(
                _ =>
                {
                    try
                    {
                        if (Panes.Count == 0)
                        {
                            return;
                        }

                        var current = Panes[0];
                        var normalizedHost = NormalizeHostId(current.HostId);
                        var isMinimized = current.IsMinimized;

                        ShellLayoutDescriptor descriptor;
                        if (File.Exists(ShellLayoutFileName))
                        {
                            var existingJson = File.ReadAllText(ShellLayoutFileName);
                            var existing = JsonSerializer.Deserialize<ShellLayoutDescriptor>(existingJson, JsonOptions);
                            descriptor = existing is null
                                ? new ShellLayoutDescriptor(normalizedHost, isMinimized, normalizedHost, isMinimized)
                                : existing with { SavedHostId = normalizedHost, SavedIsMinimized = isMinimized };
                        }
                        else
                        {
                            descriptor = new ShellLayoutDescriptor(normalizedHost, isMinimized, normalizedHost, isMinimized);
                        }

                        var json = JsonSerializer.Serialize(descriptor, JsonOptions);
                        File.WriteAllText(ShellLayoutFileName, json);
                    }
                    catch
                    {
                    }
                });

            _restoreLayoutPresetCommand = new RelayCommand(
                _ =>
                {
                    try
                    {
                        if (Panes.Count == 0 || !File.Exists(ShellLayoutFileName))
                        {
                            return;
                        }

                        var json = File.ReadAllText(ShellLayoutFileName);
                        var descriptor = JsonSerializer.Deserialize<ShellLayoutDescriptor>(json, JsonOptions);
                        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.SavedHostId))
                        {
                            return;
                        }

                        var normalizedHost = NormalizeHostId(descriptor.SavedHostId);
                        var current = Panes[0];
                        Panes[0] = current with { HostId = normalizedHost, IsMinimized = descriptor.SavedIsMinimized };

                        OnPropertyChanged(nameof(IsShellPaneOnLeft));
                        OnPropertyChanged(nameof(IsShellPaneOnTop));
                        OnPropertyChanged(nameof(IsShellPaneOnRight));
                        OnPropertyChanged(nameof(IsShellPaneOnBottom));
                        OnPropertyChanged(nameof(IsShellPaneFloating));
                        OnPropertyChanged(nameof(IsShellPaneMinimized));
                        OnPropertyChanged(nameof(PaneStructureSummary));
                        _minimizeShellPaneCommand.RaiseCanExecuteChanged();
                        _restoreShellPaneCommand.RaiseCanExecuteChanged();
                        SaveShellLayout();
                    }
                    catch
                    {
                    }
                });

            _moveChildPaneUpCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is string id && !string.IsNullOrWhiteSpace(id))
                    {
                        MoveChildPane(id, -1);
                    }
                },
                parameter =>
                {
                    if (parameter is not string id || string.IsNullOrWhiteSpace(id))
                    {
                        return false;
                    }

                    return CanMoveChildPane(id, -1);
                });

            _moveChildPaneDownCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is string id && !string.IsNullOrWhiteSpace(id))
                    {
                        MoveChildPane(id, 1);
                    }
                },
                parameter =>
                {
                    if (parameter is not string id || string.IsNullOrWhiteSpace(id))
                    {
                        return false;
                    }

                    return CanMoveChildPane(id, 1);
                });

            _floatSettingsChildPaneCommand = new RelayCommand(
                _ =>
                {
                    // Ensure the settings child is not minimized when floating.
                    SetChildPaneMinimized("shell.settings", false);
                    IsSettingsChildFloating = true;
                },
                _ => !IsSettingsChildFloating);

            _dockSettingsChildPaneCommand = new RelayCommand(
                _ =>
                {
                    IsSettingsChildFloating = false;
                },
                _ => IsSettingsChildFloating);

            _createOrRestoreParentPaneCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is not string hostId || string.IsNullOrWhiteSpace(hostId))
                    {
                        return;
                    }

                    var normalizedHost = NormalizeHostId(hostId);

                    // If no parent pane exists yet (for example after Close was used),
                    // create a new generic parent pane on the requested host.
                    if (Panes.Count == 0)
                    {
                        Panes.Add(new PaneDescriptor(
                            "parent.main",
                            "Parent Pane",
                            normalizedHost,
                            IsFloating: string.Equals(normalizedHost, "floating", StringComparison.Ordinal),
                            IsMinimized: false));

                        OnPropertyChanged(nameof(IsShellPaneOnLeft));
                        OnPropertyChanged(nameof(IsShellPaneOnTop));
                        OnPropertyChanged(nameof(IsShellPaneOnRight));
                        OnPropertyChanged(nameof(IsShellPaneOnBottom));
                        OnPropertyChanged(nameof(IsShellPaneFloating));
                        OnPropertyChanged(nameof(IsShellPaneMinimized));
                        OnPropertyChanged(nameof(IsRightPaneHostVisible));
                        OnPropertyChanged(nameof(PaneStructureSummary));

                        _minimizeShellPaneCommand.RaiseCanExecuteChanged();
                        _restoreShellPaneCommand.RaiseCanExecuteChanged();
                        _createOrRestoreParentPaneCommand.RaiseCanExecuteChanged();
                        SaveShellLayout();
                        return;
                    }

                    var current = Panes[0];

                    // If already on this host and not minimized, nothing to do.
                    if (string.Equals(current.HostId, normalizedHost, StringComparison.Ordinal) &&
                        !current.IsMinimized)
                    {
                        return;
                    }

                    Panes[0] = current with { HostId = normalizedHost, IsMinimized = false };

                    OnPropertyChanged(nameof(IsShellPaneOnLeft));
                    OnPropertyChanged(nameof(IsShellPaneOnTop));
                    OnPropertyChanged(nameof(IsShellPaneOnRight));
                    OnPropertyChanged(nameof(IsShellPaneOnBottom));
                    OnPropertyChanged(nameof(IsShellPaneFloating));
                    OnPropertyChanged(nameof(IsShellPaneMinimized));
                    OnPropertyChanged(nameof(IsRightPaneHostVisible));
                    OnPropertyChanged(nameof(PaneStructureSummary));

                    _minimizeShellPaneCommand.RaiseCanExecuteChanged();
                    _restoreShellPaneCommand.RaiseCanExecuteChanged();
                    SaveShellLayout();
                },
                _ => true);

            _destroyParentPaneCommand = new RelayCommand(
                _ =>
                {
                    if (Panes.Count == 0)
                    {
                        return;
                    }

                    Panes.Clear();

                    OnPropertyChanged(nameof(IsShellPaneOnLeft));
                    OnPropertyChanged(nameof(IsShellPaneOnTop));
                    OnPropertyChanged(nameof(IsShellPaneOnRight));
                    OnPropertyChanged(nameof(IsShellPaneOnBottom));
                    OnPropertyChanged(nameof(IsShellPaneFloating));
                    OnPropertyChanged(nameof(IsShellPaneMinimized));
                    OnPropertyChanged(nameof(IsRightPaneHostVisible));
                    OnPropertyChanged(nameof(PaneStructureSummary));

                    _minimizeShellPaneCommand.RaiseCanExecuteChanged();
                    _restoreShellPaneCommand.RaiseCanExecuteChanged();
                    _createOrRestoreParentPaneCommand.RaiseCanExecuteChanged();
                },
                _ => true);

            LoadShellLayout();
            RefreshFromEngineState();
        }

        /// <summary>
        /// Update the drag-shadow rectangle used to preview parent-pane docking/floating.
        /// When <paramref name="visible"/> is false, the rect values are ignored and the
        /// shadow is hidden.
        /// </summary>
        public void SetParentPaneDragShadow(bool visible, double left, double top, double width, double height)
        {
            IsParentPaneDragShadowVisible = visible;

            if (!visible)
            {
                return;
            }

            ParentPaneDragShadowLeft = left;
            ParentPaneDragShadowTop = top;
            ParentPaneDragShadowWidth = width;
            ParentPaneDragShadowHeight = height;
        }

        private void SetChildPaneMinimized(string id, bool minimized)
        {
            for (var i = 0; i < ChildPanes.Count; i++)
            {
                var current = ChildPanes[i];
                if (!string.Equals(current.Id, id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (current.IsMinimized == minimized)
                {
                    return;
                }

                ChildPanes[i] = current with { IsMinimized = minimized };

                OnPropertyChanged(nameof(ChildPanesOrdered));
                OnPropertyChanged(nameof(HasMinimizedChildPanes));
                OnPropertyChanged(nameof(MinimizedChildPanes));
                OnPropertyChanged(nameof(IsShellCurrentChildVisible));
                OnPropertyChanged(nameof(IsShellSettingsChildVisible));
                OnPropertyChanged(nameof(IsShellDeveloperChildVisible));
                OnPropertyChanged(nameof(IsShellCapabilitiesChildVisible));
                OnPropertyChanged(nameof(PaneStructureSummary));
                return;
            }
        }

        private bool CanMoveChildPane(string id, int delta)
        {
            var ordered = ChildPanesOrdered.ToList();
            var index = ordered.FindIndex(pane => string.Equals(pane.Id, id, StringComparison.Ordinal));
            if (index < 0)
            {
                return false;
            }

            var newIndex = index + delta;
            return newIndex >= 0 && newIndex < ordered.Count;
        }

        private void MoveChildPane(string id, int delta)
        {
            var ordered = ChildPanesOrdered.ToList();
            var index = ordered.FindIndex(pane => string.Equals(pane.Id, id, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            var newIndex = index + delta;
            if (newIndex < 0 || newIndex >= ordered.Count)
            {
                return;
            }

            var a = ordered[index];
            var b = ordered[newIndex];

            for (var i = 0; i < ChildPanes.Count; i++)
            {
                var current = ChildPanes[i];
                if (string.Equals(current.Id, a.Id, StringComparison.Ordinal))
                {
                    ChildPanes[i] = current with { Order = b.Order };
                }
                else if (string.Equals(current.Id, b.Id, StringComparison.Ordinal))
                {
                    ChildPanes[i] = current with { Order = a.Order };
                }
            }

            OnPropertyChanged(nameof(ChildPanesOrdered));
            OnPropertyChanged(nameof(HasMinimizedChildPanes));
            OnPropertyChanged(nameof(MinimizedChildPanes));
            OnPropertyChanged(nameof(IsShellCurrentChildVisible));
            OnPropertyChanged(nameof(IsShellSettingsChildVisible));
            OnPropertyChanged(nameof(IsShellDeveloperChildVisible));
            OnPropertyChanged(nameof(IsShellCapabilitiesChildVisible));
            OnPropertyChanged(nameof(PaneStructureSummary));

            _moveChildPaneUpCommand.RaiseCanExecuteChanged();
            _moveChildPaneDownCommand.RaiseCanExecuteChanged();
            _floatSettingsChildPaneCommand.RaiseCanExecuteChanged();
            _dockSettingsChildPaneCommand.RaiseCanExecuteChanged();
        }

        public string FocusSummary
        {
            get
            {
                if (_shellScene.GetFocusedPanel() is { } focusedPanel)
                {
                    return $"Focused Panel: {focusedPanel.ViewRef} on {focusedPanel.NodeId}";
                }

                var focusedNode = _shellScene.GetFocusedNode();
                return focusedNode is not null
                    ? $"Focused Node: {focusedNode.Id}"
                    : "Focused Node: none";
            }
        }

        public string FocusOriginSummary
        {
            get
            {
                var origin = _shellScene.GetFocusOrigin();
                return $"Focus Origin: {FormatFocusOrigin(origin)}";
            }
        }

        public string EnteredNodeSummary
        {
            get
            {
                var enteredId = _shellScene.GetEnteredNodeId();
                return enteredId is null
                    ? "Entered Node: none"
                    : $"Entered Node: {enteredId}";
            }
        }

        public string SelectionSummary
        {
            get
            {
                var nodeCount = _shellScene.GetSelectedNodeIds().Count;
                var panelCount = _shellScene.GetSelectedPanels().Count;

                if (nodeCount == 0 && panelCount == 0)
                {
                    return "Selection: none";
                }

                return $"Selection: nodes={nodeCount}, panels={panelCount}";
            }
        }

        public string InteractionModeSummary
        {
            get
            {
                return _shellScene.GetInteractionMode() switch
                {
                    "marquee" => "Interaction Mode: Marquee — left-drag performs viewport box selection through the existing Focus/Select command flow.",
                    "move" => "Interaction Mode: Move — left-drag repositions the focused/selected node set with transient viewport preview and final command-path commit on release. Escape cancels the live preview without committing.",
                    _ => "Interaction Mode: Navigate — default hybrid navigation, click-selection, linking, and orbit posture remains active."
                };
            }
        }

        public string FocusedTransformSummary
        {
            get
            {
                var focusedNode = _shellScene.GetFocusedNode();
                var appearance = focusedNode?.Appearance ?? NodeAppearance.Default;
                return focusedNode is null
                    ? "Focused Transform: none"
                    : $"Focused Transform: pos={FormatVector3(focusedNode.Transform.Position)} scale={FormatVector3(focusedNode.Transform.Scale)} visual={focusedNode.VisualScale:0.##} primitive={appearance.Primitive} fill={appearance.FillColor}";
            }
        }

        public string BookmarkSummary
        {
            get
            {
                var count = _shellScene.GetBookmarks().Count;
                return count == 0
                    ? "Bookmarks: none"
                    : $"Bookmarks: {count}";
            }
        }

        public string ViewSummary
        {
            get
            {
                return _shellScene.TryGetLastView(out var view)
                    ? $"View: yaw={view.Yaw:0.##}, pitch={view.Pitch:0.##}, distance={view.Distance:0.##}, target={FormatVector3(view.Target)}"
                    : "View: no renderer camera sample yet";
            }
        }

        public string ViewDetails
        {
            get
            {
                return _shellScene.TryGetLastView(out var view)
                    ? $"yaw={view.Yaw:0.###}\n" +
                      $"pitch={view.Pitch:0.###}\n" +
                      $"distance={view.Distance:0.###}\n" +
                      $"target={FormatVector3(view.Target)}"
                    : "No renderer camera sample has been published yet.";
            }
        }

        public string BookmarkDetails
        {
            get
            {
                var bookmarks = _shellScene.GetBookmarks();
                if (bookmarks.Count == 0)
                {
                    return "No bookmarks yet.";
                }

                return string.Join(
                    "\n",
                    bookmarks.Select(bookmark =>
                    {
                        var focus = bookmark.FocusedPanel is { } focusedPanel
                            ? $"focus=panel:{focusedPanel.ViewRef}@{focusedPanel.NodeId}"
                            : bookmark.FocusedNodeId is { } focusedNodeId
                                ? $"focus=node:{focusedNodeId}"
                                : "focus=none";

                        return
                            $"{bookmark.Name} => {focus} " +
                            $"selectedNodes={bookmark.SelectedNodeIds.Count} " +
                            $"selectedPanels={bookmark.SelectedPanels.Count}";
                    }));
            }
        }

        public string GroupSummary
        {
            get
            {
                var count = _shellScene.GetGroups().Count;
                var activeGroup = _shellScene.GetActiveGroup();
                return count == 0 ? "Groups: none" : activeGroup is null
                    ? $"Groups: {count}"
                    : $"Groups: {count} • active={activeGroup.Label}";
            }
        }

        public string GroupDetails
        {
            get
            {
                var groups = _shellScene.GetGroups();
                if (groups.Count == 0)
                {
                    return "No groups yet.";
                }

                var activeGroupId = _shellScene.GetActiveGroup()?.Id;
                return string.Join(
                    "\n",
                    groups.Select(group =>
                        $"{(string.Equals(group.Id, activeGroupId, StringComparison.Ordinal) ? "* " : string.Empty)}{group.Label} [{group.Id}] => {string.Join(", ", group.NodeIds.Select(id => id.ToString()))}"));
            }
        }

        public string PanelSummary
        {
            get
            {
                var attachments = _shellScene.GetPanelAttachments();
                var count = attachments.Count;
                var paneletteCount = attachments.Values.Count(attachment => (attachment.Semantics ?? PanelSurfaceSemantics.FromViewRef(attachment.ViewRef)).IsPanelette);
                var metadataCount = attachments.Values.Count(attachment => (attachment.Semantics ?? PanelSurfaceSemantics.FromViewRef(attachment.ViewRef)).IsMetadataPanelette);
                var labelCount = attachments.Values.Count(attachment => (attachment.Semantics ?? PanelSurfaceSemantics.FromViewRef(attachment.ViewRef)).IsLabelPanelette);
                var commandSurfaceCount = attachments.Values.Count(attachment => attachment.CommandSurface is { HasCommands: true });

                if (count == 0)
                {
                    return "Attached Panels: none";
                }

                return paneletteCount > 0
                    ? $"Attached Panels: {count} • panelettes={paneletteCount} • metadata={metadataCount} • labels={labelCount} • command-surfaces={commandSurfaceCount}"
                    : $"Attached Panels: {count}";
            }
        }

        public string LinkSummary
        {
            get
            {
                var count = _shellScene.GetLinks().Count;
                return count == 0
                    ? "Links: none"
                    : $"Links: {count}";
            }
        }

        public string LinkDetails
        {
            get
            {
                var links = _shellScene.GetLinks();
                if (links.Count == 0)
                {
                    return "No links yet.";
                }

                return string.Join(
                    "\n",
                    links.Select(link =>
                        $"{link.SourceId} -> {link.TargetId} kind={link.Kind} weight={link.Weight:0.##}"));
            }
        }

        public string FocusedTransformDetails
        {
            get
            {
                var focusedNode = _shellScene.GetFocusedNode();
                if (focusedNode is null)
                {
                    return "No focused node transform to inspect.";
                }

                var appearance = focusedNode.Appearance ?? NodeAppearance.Default;

                return
                    $"id={focusedNode.Id}\n" +
                    $"label={focusedNode.Label}\n" +
                    $"position={FormatVector3(focusedNode.Transform.Position)}\n" +
                    $"rotation={FormatVector3(focusedNode.Transform.RotationEuler)}\n" +
                    $"scale={FormatVector3(focusedNode.Transform.Scale)}\n" +
                    $"appearance={appearance.Primitive} fill={appearance.FillColor} outline={appearance.OutlineColor} opacity={appearance.Opacity:0.##}\n" +
                    $"visualScale={focusedNode.VisualScale:0.###}\n" +
                    $"phase={focusedNode.Phase:0.###}";
            }
        }

        public string InteractionSemanticsSummary
        {
            get
            {
                var focusedNode = _shellScene.GetFocusedNode();
                var selectedNodeIds = _shellScene.GetSelectedNodeIds();
                var selectedPanels = _shellScene.GetSelectedPanels();
                var activeGroup = _shellScene.GetActiveGroup();

                var linkSource = selectedNodeIds
                    .FirstOrDefault(nodeId => focusedNode is null || nodeId != focusedNode.Id);

                var linkSourceText = linkSource != default
                    ? linkSource.ToString()
                    : focusedNode is not null
                        ? focusedNode.Id.ToString()
                        : "none";

                var targetText = focusedNode is not null
                    ? focusedNode.Id.ToString()
                    : "clicked node";

                var activeGroupText = activeGroup is null
                    ? "none"
                    : $"{activeGroup.Label} ({activeGroup.NodeIds.Count} nodes)";

                return
                    $"Interaction: navigate-mode click=focus/select + left-drag=orbit • move-mode left-drag repositions the focused/selected node set with transient viewport preview and release-time `UpdateEntity` commit, while Escape cancels an active drag preview without committing • marquee-mode left-drag=box-select • shift preserves additive selection in selection flows • ctrl+click/double-click=link {linkSourceText} -> {targetText} in navigate mode • shell unlink removes the matching directed link • ctrl+z=undo • center/frame are explicit navigation tools over the view bridge • clear-links=shell tool • shell mutation group now includes first transform helpers (directional nudge across X/Y/Z + grow/shrink) over the existing update-entity path, and focused transform state is now surfaced in-shell for inspection • attached `panelette.*` surfaces now expose the first explicit Tier 1 content classes: metadata cards and compact label surfaces • shell surface now reflects first-pass toolbar categories with collapsible session-local groups + secondary developer readouts" +
                    $"\nCurrent counts: selectedNodes={selectedNodeIds.Count}, selectedPanels={selectedPanels.Count} • activeGroup={activeGroupText} • viewCommands={(selectedNodeIds.Count > 0 || _shellScene.GetFocusedNode() is not null ? "ready" : "blocked")}";
            }
        }

        public string ActionReadinessSummary
        {
            get
            {
                return string.Join(
                    " • ",
                    [
                        $"focus-node={FormatReady(_focusFirstNodeCommand.CanExecute(null))}",
                        $"select-node={FormatReady(_selectFirstNodeCommand.CanExecute(null))}",
                        $"move-mode={FormatReady(_activateMoveModeCommand.CanExecute(null))}",
                        $"navigate-mode={FormatReady(_activateNavigateModeCommand.CanExecute(null))}",
                        $"marquee-mode={FormatReady(_activateMarqueeModeCommand.CanExecute(null))}",
                        $"focus-panel={FormatReady(_focusFirstPanelCommand.CanExecute(null))}",
                        $"select-panel={FormatReady(_selectFirstPanelCommand.CanExecute(null))}",
                        $"create-node={FormatReady(_createDemoNodeCommand.CanExecute(null))}",
                        $"nudge-left={FormatReady(_nudgeFocusedLeftCommand.CanExecute(null))}",
                        $"nudge-right={FormatReady(_nudgeFocusedRightCommand.CanExecute(null))}",
                        $"nudge-up={FormatReady(_nudgeFocusedUpCommand.CanExecute(null))}",
                        $"nudge-down={FormatReady(_nudgeFocusedDownCommand.CanExecute(null))}",
                        $"nudge-forward={FormatReady(_nudgeFocusedForwardCommand.CanExecute(null))}",
                        $"nudge-back={FormatReady(_nudgeFocusedBackCommand.CanExecute(null))}",
                        $"grow={FormatReady(_growFocusedNodeCommand.CanExecute(null))}",
                        $"shrink={FormatReady(_shrinkFocusedNodeCommand.CanExecute(null))}",
                        $"primitive-triangle={FormatReady(_applyTrianglePrimitiveCommand.CanExecute(null))}",
                        $"primitive-square={FormatReady(_applySquarePrimitiveCommand.CanExecute(null))}",
                        $"primitive-diamond={FormatReady(_applyDiamondPrimitiveCommand.CanExecute(null))}",
                        $"primitive-pentagon={FormatReady(_applyPentagonPrimitiveCommand.CanExecute(null))}",
                        $"primitive-hexagon={FormatReady(_applyHexagonPrimitiveCommand.CanExecute(null))}",
                        $"primitive-cube={FormatReady(_applyCubePrimitiveCommand.CanExecute(null))}",
                        $"primitive-tetrahedron={FormatReady(_applyTetrahedronPrimitiveCommand.CanExecute(null))}",
                        $"primitive-sphere={FormatReady(_applySpherePrimitiveCommand.CanExecute(null))}",
                        $"primitive-box={FormatReady(_applyBoxPrimitiveCommand.CanExecute(null))}",
                        $"appearance-blue={FormatReady(_applyBlueAppearanceCommand.CanExecute(null))}",
                        $"appearance-violet={FormatReady(_applyVioletAppearanceCommand.CanExecute(null))}",
                        $"appearance-green={FormatReady(_applyGreenAppearanceCommand.CanExecute(null))}",
                        $"opacity-up={FormatReady(_increaseOpacityCommand.CanExecute(null))}",
                        $"opacity-down={FormatReady(_decreaseOpacityCommand.CanExecute(null))}",
                        $"connect={FormatReady(_connectFocusedNodeCommand.CanExecute(null))}",
                        $"unlink={FormatReady(_unlinkFocusedNodeCommand.CanExecute(null))}",
                        $"group={FormatReady(_groupSelectionCommand.CanExecute(null))}",
                        $"add-to-group={FormatReady(_addSelectionToActiveGroupCommand.CanExecute(null))}",
                        $"remove-from-group={FormatReady(_removeSelectionFromActiveGroupCommand.CanExecute(null))}",
                        $"delete-group={FormatReady(_deleteActiveGroupCommand.CanExecute(null))}",
                        $"save-bookmark={FormatReady(_saveBookmarkCommand.CanExecute(null))}",
                        $"restore-bookmark={FormatReady(_restoreLatestBookmarkCommand.CanExecute(null))}",
                        $"undo={FormatReady(_undoLastCommand.CanExecute(null))}",
                        $"home={FormatReady(_homeViewCommand.CanExecute(null))}",
                        $"center={FormatReady(_centerFocusedNodeCommand.CanExecute(null))}",
                        $"frame={FormatReady(_frameSelectionCommand.CanExecute(null))}",
                        $"clear-links={FormatReady(_clearLinksCommand.CanExecute(null))}",
                        $"delete={FormatReady(_deleteFocusedNodeCommand.CanExecute(null))}",
                        $"attach-panel={FormatReady(_attachDemoPanelCommand.CanExecute(null))}",
                        $"attach-label={FormatReady(_attachLabelPaneletteCommand.CanExecute(null))}",
                        $"clear={FormatReady(_clearSelectionCommand.CanExecute(null))}"
                    ]);
            }
        }

        public string PaneStructureSummary =>
            $"Shell Host: {(Panes.Count > 0 ? Panes[0].HostId : "left")}" +
            $" • minimized={FormatExpanded(!IsShellPaneMinimized)}" +
            "\n2D Pane Layout: " +
            $"current={FormatExpanded(IsCurrentStateSectionExpanded)} • " +
            $"commands={FormatExpanded(IsCommandSurfaceSectionExpanded)} • " +
            $"settings={FormatExpanded(IsSettingsSectionExpanded)} • " +
            $"developer={FormatExpanded(IsDeveloperReadoutsSectionExpanded)} • " +
            $"capabilities={FormatExpanded(IsCapabilitiesSectionExpanded)}" +
            "\nCommand Groups: " +
            $"selection={FormatExpanded(IsSelectionFocusGroupExpanded)} • " +
            $"links={FormatExpanded(IsLinksGroupExpanded)} • " +
            $"groups={FormatExpanded(IsGroupsGroupExpanded)} • " +
            $"history={FormatExpanded(IsHistoryGroupExpanded)} • " +
            $"view={FormatExpanded(IsViewGroupExpanded)} • " +
            $"editModes={FormatExpanded(IsEditModesGroupExpanded)} • " +
            $"mutation={FormatExpanded(IsMutationGroupExpanded)} • " +
            $"appearance={FormatExpanded(IsAppearanceGroupExpanded)}" +
            "\nChild Panes: " +
            (ChildPanesOrdered.Count == 0
                ? "none"
                : string.Join(", ", ChildPanesOrdered.Select(p => p.Id)));

        public string LastActivitySummary => _lastActivitySummary;

        public string CommandHistorySummary =>
            _commandHistory.Count == 0
                ? "No recent command history."
                : string.Join("\n", _commandHistory);

        public string NavigationHistorySummary
        {
            get
            {
                var views = _shellScene.GetViewHistory();
                if (views.Count == 0)
                {
                    return "Navigation history: none";
                }

                var lines = views
                    .Select((v, index) =>
                        $"{index + 1}: yaw={v.Yaw:0.##}, pitch={v.Pitch:0.##}, dist={v.Distance:0.##}, target={FormatVector3(v.Target)}");

                return "Navigation history (oldest → newest):\n" + string.Join("\n", lines);
            }
        }

        public bool MouseLeaveClearsFocus
        {
            get => _mouseLeaveClearsFocus;
            set
            {
                if (_mouseLeaveClearsFocus == value)
                {
                    return;
                }

                _mouseLeaveClearsFocus = value;
                EngineServices.Settings.MouseLeaveClearsFocus = value;
                OnPropertyChanged();
            }
        }

        public float GroupOverlayOpacity
        {
            get => _groupOverlayOpacity;
            set
            {
                var clamped = Math.Clamp(value, 0f, 1f);
                if (Math.Abs(_groupOverlayOpacity - clamped) < 0.0001f)
                {
                    return;
                }

                _groupOverlayOpacity = clamped;
                EngineServices.Settings.GroupOverlayOpacity = clamped;
                OnPropertyChanged();
            }
        }

        public float NodeHighlightOpacity
        {
            get => _nodeHighlightOpacity;
            set
            {
                var clamped = Math.Clamp(value, 0f, 1f);
                if (Math.Abs(_nodeHighlightOpacity - clamped) < 0.0001f)
                {
                    return;
                }

                _nodeHighlightOpacity = clamped;
                EngineServices.Settings.NodeHighlightOpacity = clamped;
                OnPropertyChanged();
            }
        }

        public float NodeFocusHaloRadiusMultiplier
        {
            get => _nodeFocusHaloRadiusMultiplier;
            set
            {
                var clamped = Math.Clamp(value, 0.5f, 3f);
                if (Math.Abs(_nodeFocusHaloRadiusMultiplier - clamped) < 0.0001f)
                {
                    return;
                }

                _nodeFocusHaloRadiusMultiplier = clamped;
                EngineServices.Settings.NodeFocusHaloRadiusMultiplier = clamped;
                OnPropertyChanged();
            }
        }

        public float NodeSelectionHaloRadiusMultiplier
        {
            get => _nodeSelectionHaloRadiusMultiplier;
            set
            {
                var clamped = Math.Clamp(value, 0.5f, 3f);
                if (Math.Abs(_nodeSelectionHaloRadiusMultiplier - clamped) < 0.0001f)
                {
                    return;
                }

                _nodeSelectionHaloRadiusMultiplier = clamped;
                EngineServices.Settings.NodeSelectionHaloRadiusMultiplier = clamped;
                OnPropertyChanged();
            }
        }

        public string NodeHaloMode
        {
            get => _nodeHaloMode;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? "2d"
                    : value.Trim().ToLowerInvariant();

                if (!string.Equals(normalized, "2d", StringComparison.Ordinal) &&
                    !string.Equals(normalized, "3d", StringComparison.Ordinal) &&
                    !string.Equals(normalized, "both", StringComparison.Ordinal))
                {
                    normalized = "2d";
                }

                if (string.Equals(_nodeHaloMode, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _nodeHaloMode = normalized;
                EngineServices.Settings.NodeHaloMode = normalized;
                OnPropertyChanged();
            }
        }

        public string NodeHaloOcclusionMode
        {
            get => _nodeHaloOcclusionMode;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? "hollow"
                    : value.Trim().ToLowerInvariant();

                if (!string.Equals(normalized, "hollow", StringComparison.Ordinal) &&
                    !string.Equals(normalized, "occluding", StringComparison.Ordinal))
                {
                    normalized = "hollow";
                }

                if (string.Equals(_nodeHaloOcclusionMode, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _nodeHaloOcclusionMode = normalized;
                EngineServices.Settings.NodeHaloOcclusionMode = normalized;
                OnPropertyChanged();
            }
        }

        public float BackgroundAnimationSpeed
        {
            get => _backgroundAnimationSpeed;
            set
            {
                var clamped = Math.Clamp(value, 0f, 2f);
                if (Math.Abs(_backgroundAnimationSpeed - clamped) < 0.0001f)
                {
                    return;
                }

                _backgroundAnimationSpeed = clamped;
                EngineServices.Settings.BackgroundAnimationSpeed = clamped;
                OnPropertyChanged();
            }
        }

        public float LinkStrokeThickness
        {
            get => _linkStrokeThickness;
            set
            {
                var clamped = Math.Clamp(value, 0.5f, 4f);
                if (Math.Abs(_linkStrokeThickness - clamped) < 0.0001f)
                {
                    return;
                }

                _linkStrokeThickness = clamped;
                EngineServices.Settings.LinkStrokeThickness = clamped;
                OnPropertyChanged();
            }
        }

        public float LinkOpacity
        {
            get => _linkOpacity;
            set
            {
                var clamped = Math.Clamp(value, 0.1f, 1f);
                if (Math.Abs(_linkOpacity - clamped) < 0.0001f)
                {
                    return;
                }

                _linkOpacity = clamped;
                EngineServices.Settings.LinkOpacity = clamped;
                OnPropertyChanged();
            }
        }

        public float PaneletteBackgroundIntensity
        {
            get => _paneletteBackgroundIntensity;
            set
            {
                var clamped = Math.Clamp(value, 0.25f, 2f);
                if (Math.Abs(_paneletteBackgroundIntensity - clamped) < 0.0001f)
                {
                    return;
                }

                _paneletteBackgroundIntensity = clamped;
                EngineServices.Settings.PaneletteBackgroundIntensity = clamped;
                OnPropertyChanged();
            }
        }

        public float CommandSurfaceOverlayOpacity
        {
            get => _commandSurfaceOverlayOpacity;
            set
            {
                var clamped = Math.Clamp(value, 0.25f, 2f);
                if (Math.Abs(_commandSurfaceOverlayOpacity - clamped) < 0.0001f)
                {
                    return;
                }

                _commandSurfaceOverlayOpacity = clamped;
                EngineServices.Settings.CommandSurfaceOverlayOpacity = clamped;
                OnPropertyChanged();
            }
        }

        public string PanelDetails
        {
            get
            {
                var snapshot = _shellScene.GetSnapshot();
                if (snapshot.PanelAttachments is null || snapshot.PanelAttachments.Count == 0)
                {
                    return "No panel attachments yet.";
                }

                var selectedPanels = snapshot.SelectedPanels?
                    .ToHashSet()
                    ?? [];

                return string.Join(
                    "\n",
                    snapshot.PanelAttachments
                        .OrderBy(x => x.Key.ToString(), StringComparer.Ordinal)
                        .Select(x =>
                        {
                            var semantics = x.Value.Semantics ?? PanelSurfaceSemantics.FromViewRef(x.Value.ViewRef);
                            var isFocused = snapshot.FocusedPanel is { } focusedPanel &&
                                            focusedPanel.NodeId == x.Key &&
                                            string.Equals(focusedPanel.ViewRef, x.Value.ViewRef, StringComparison.Ordinal);
                            var isSelected = selectedPanels.Contains(new PanelTarget(x.Key, x.Value.ViewRef));
                            var commandSurfaceSummary = x.Value.CommandSurface is { } commandSurface
                                ? $" commandSurface={commandSurface.DescribeSummary()}"
                                : string.Empty;

                            return
                                $"{x.Key} → {x.Value.ViewRef} " +
                                $"kind={semantics.DescribeKind()} " +
                                $"tier={semantics.PaneletteTier} " +
                                $"anchor={x.Value.Anchor} " +
                                $"offset=({x.Value.LocalOffset.X:0.##},{x.Value.LocalOffset.Y:0.##},{x.Value.LocalOffset.Z:0.##}) " +
                                $"size=({x.Value.Size.X:0.##},{x.Value.Size.Y:0.##}) " +
                                $"visible={x.Value.IsVisible} " +
                                $"focused={isFocused} " +
                                $"selected={isSelected}" +
                                commandSurfaceSummary;
                        }));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Dispose()
        {
            foreach (var subscription in _eventSubscriptions)
            {
                subscription.Dispose();
            }
        }

        private IDisposable SubscribeRefresh(string eventName, string activityLabel)
        {
            return EngineServices.EventBus.Subscribe(eventName, envelope =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateLastActivity(eventName, activityLabel, envelope);
                    RefreshFromEngineState();
                });

                return true;
            });
        }

        private IDisposable SubscribePanelInteraction()
        {
            return EngineServices.EventBus.Subscribe(EventNames.PanelInteraction, envelope =>
            {
                if (envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!TryGetString(payload, "action", out var action) ||
                    !string.Equals(action, "promote_to_pane", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (Panes.Count == 0)
                    {
                        return;
                    }

                    var current = Panes[0];
                    if (!current.IsMinimized)
                    {
                        return;
                    }

                    Panes[0] = current with { IsMinimized = false };

                    OnPropertyChanged(nameof(IsShellPaneMinimized));
                    OnPropertyChanged(nameof(IsShellPaneOnLeft));
                    OnPropertyChanged(nameof(IsShellPaneOnTop));
                    OnPropertyChanged(nameof(IsShellPaneOnRight));
                    OnPropertyChanged(nameof(IsShellPaneOnBottom));
                    OnPropertyChanged(nameof(IsShellPaneFloating));
                    OnPropertyChanged(nameof(PaneStructureSummary));
                    _minimizeShellPaneCommand.RaiseCanExecuteChanged();
                    _restoreShellPaneCommand.RaiseCanExecuteChanged();
                    SaveShellLayout();
                });

                return true;
            });
        }

        private void SendCommand<TPayload>(string commandName, TPayload payload)
        {
            var envelope = new Envelope
            {
                V = "1.0",
                Id = Guid.NewGuid(),
                Ts = DateTimeOffset.UtcNow,
                Type = EnvelopeType.Command,
                Name = commandName,
                Payload = payload is null
                    ? null
                    : JsonSerializer.SerializeToElement(payload, JsonOptions),
                CorrelationId = null
            };

            EngineServices.CommandBus.Send(envelope);
        }

        private static void PublishFocusOrigin(string origin)
        {
            try
            {
                var envelope = new Envelope
                {
                    V = "1.0",
                    Id = Guid.NewGuid(),
                    Ts = DateTimeOffset.UtcNow,
                    Type = EnvelopeType.Event,
                    Name = EventNames.FocusOriginChanged,
                    Payload = JsonSerializer.SerializeToElement(new { origin }, JsonOptions),
                    CorrelationId = null
                };

                EngineServices.EventBus.Publish(envelope);
            }
            catch
            {
            }
        }

        private RelayCommand CreateSelectionOrFocusTransformCommand(Vector3 positionDelta, float scaleMultiplier)
        {
            return new RelayCommand(
                _ =>
                {
                    var targetNodes = GetSelectionOrFocusTargetNodes();
                    if (targetNodes.Length == 0)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.UpdateEntities,
                        new UpdateEntitiesPayload(
                            targetNodes
                                .Select(node =>
                                {
                                    var currentScale = node.Transform.Scale;
                                    var nextScale = new Vector3(
                                        Math.Clamp(currentScale.X * scaleMultiplier, 0.15f, 2.5f),
                                        Math.Clamp(currentScale.Y * scaleMultiplier, 0.15f, 2.5f),
                                        Math.Clamp(currentScale.Z * scaleMultiplier, 0.15f, 2.5f));
                                    var nextVisualScale = Math.Clamp(node.VisualScale * scaleMultiplier, 0.15f, 2.5f);

                                    return new UpdateEntityPayload(
                                        node.Id.ToString(),
                                        node.Label,
                                        node.Transform.Position + positionDelta,
                                        node.Transform.RotationEuler,
                                        nextScale,
                                        nextVisualScale,
                                        node.Phase);
                                })
                                .ToArray()));
                },
                _ => GetSelectionOrFocusTargetNodes().Length > 0);
        }

        private RelayCommand CreateSelectionOrFocusAppearanceCommand(string? fillColor = null, float opacityDelta = 0f, string? primitive = null)
        {
            return new RelayCommand(
                _ =>
                {
                    var targetNodes = GetSelectionOrFocusTargetNodes();
                    if (targetNodes.Length == 0)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.UpdateEntities,
                        new UpdateEntitiesPayload(
                            targetNodes
                                .Select(node =>
                                {
                                    var currentAppearance = node.Appearance ?? NodeAppearance.Default;
                                    var nextOpacity = Math.Clamp(currentAppearance.Opacity + opacityDelta, 0.15f, 1.0f);

                                    return new UpdateEntityPayload(
                                        node.Id.ToString(),
                                        node.Label,
                                        node.Transform.Position,
                                        node.Transform.RotationEuler,
                                        node.Transform.Scale,
                                        node.VisualScale,
                                        node.Phase,
                                        new NodeAppearancePayload(
                                            Primitive: string.IsNullOrWhiteSpace(primitive) ? null : primitive,
                                            FillColor: string.IsNullOrWhiteSpace(fillColor) ? null : fillColor,
                                            Opacity: MathF.Abs(nextOpacity - currentAppearance.Opacity) > 0.0001f ? nextOpacity : null));
                                })
                                .ToArray()));
                },
                _ => GetSelectionOrFocusTargetNodes().Length > 0);
        }

        private SceneNode[] GetSelectionOrFocusTargetNodes()
        {
            var snapshot = _shellScene.GetSnapshot();
            var selectedNodeIds = snapshot.SelectedNodeIds?.ToHashSet() ?? [];

            if (selectedNodeIds.Count > 0)
            {
                return snapshot.Nodes
                    .Where(node => selectedNodeIds.Contains(node.Id))
                    .OrderBy(node => node.Label, StringComparer.Ordinal)
                    .ThenBy(node => node.Id.ToString(), StringComparer.Ordinal)
                    .ToArray();
            }

            if (snapshot.FocusedNodeId is { } focusedNodeId)
            {
                return snapshot.Nodes.Where(node => node.Id == focusedNodeId).ToArray();
            }

            return [];
        }

        public void MoveShellPaneToHost(string hostId)
        {
            if (Panes.Count == 0 || string.IsNullOrWhiteSpace(hostId))
            {
                return;
            }
            var normalized = NormalizeHostId(hostId);

            var current = Panes[0];
            if (string.Equals(current.HostId, normalized, StringComparison.Ordinal))
            {
                return;
            }

            Panes[0] = current with { HostId = normalized };
            OnPropertyChanged(nameof(IsShellPaneOnLeft));
            OnPropertyChanged(nameof(IsShellPaneOnTop));
            OnPropertyChanged(nameof(IsShellPaneOnRight));
            OnPropertyChanged(nameof(IsShellPaneOnBottom));
            OnPropertyChanged(nameof(IsShellPaneFloating));
            OnPropertyChanged(nameof(PaneStructureSummary));
            _minimizeShellPaneCommand.RaiseCanExecuteChanged();
            _restoreShellPaneCommand.RaiseCanExecuteChanged();
            SaveShellLayout();
        }

        public void SetShellPaneMinimized(bool minimized)
        {
            if (Panes.Count == 0)
            {
                return;
            }

            var current = Panes[0];
            if (current.IsMinimized == minimized)
            {
                return;
            }

            Panes[0] = current with { IsMinimized = minimized };
            OnPropertyChanged(nameof(IsShellPaneMinimized));
            OnPropertyChanged(nameof(IsShellPaneOnLeft));
            OnPropertyChanged(nameof(IsShellPaneOnTop));
            OnPropertyChanged(nameof(IsShellPaneOnRight));
            OnPropertyChanged(nameof(IsShellPaneOnBottom));
            OnPropertyChanged(nameof(IsShellPaneFloating));
            OnPropertyChanged(nameof(PaneStructureSummary));
            _minimizeShellPaneCommand.RaiseCanExecuteChanged();
            _restoreShellPaneCommand.RaiseCanExecuteChanged();
            SaveShellLayout();
        }

        internal static string NormalizeHostId(string? hostId)
        {
            if (string.IsNullOrWhiteSpace(hostId))
            {
                return "left";
            }

            var normalized = hostId.Trim().ToLowerInvariant();
            return normalized is "left" or "top" or "right" or "bottom" or "floating"
                ? normalized
                : "left";
        }

        private void LoadShellLayout()
        {
            try
            {
                if (Panes.Count == 0)
                {
                    return;
                }

                if (!File.Exists(ShellLayoutFileName))
                {
                    return;
                }

                var json = File.ReadAllText(ShellLayoutFileName);
                var descriptor = JsonSerializer.Deserialize<ShellLayoutDescriptor>(json, JsonOptions);
                if (descriptor is null)
                {
                    return;
                }

                var normalizedHost = NormalizeHostId(descriptor.HostId);
                var current = Panes[0];
                Panes[0] = current with { HostId = normalizedHost, IsMinimized = descriptor.IsMinimized };

                OnPropertyChanged(nameof(IsShellPaneOnLeft));
                OnPropertyChanged(nameof(IsShellPaneOnTop));
                OnPropertyChanged(nameof(IsShellPaneOnRight));
                OnPropertyChanged(nameof(IsShellPaneOnBottom));
                OnPropertyChanged(nameof(IsShellPaneFloating));
                OnPropertyChanged(nameof(IsShellPaneMinimized));
                OnPropertyChanged(nameof(PaneStructureSummary));
                _minimizeShellPaneCommand.RaiseCanExecuteChanged();
                _restoreShellPaneCommand.RaiseCanExecuteChanged();
            _destroyParentPaneCommand.RaiseCanExecuteChanged();
            }
            catch
            {
            }
        }

        private void SaveShellLayout()
        {
            try
            {
                if (Panes.Count == 0)
                {
                    return;
                }

                var current = Panes[0];
                ShellLayoutDescriptor? existing = null;

                if (File.Exists(ShellLayoutFileName))
                {
                    try
                    {
                        var existingJson = File.ReadAllText(ShellLayoutFileName);
                        existing = JsonSerializer.Deserialize<ShellLayoutDescriptor>(existingJson, JsonOptions);
                    }
                    catch
                    {
                        existing = null;
                    }
                }

                var normalizedHost = NormalizeHostId(current.HostId);

                var descriptor = existing is null
                    ? new ShellLayoutDescriptor(normalizedHost, current.IsMinimized)
                    : existing with { HostId = normalizedHost, IsMinimized = current.IsMinimized };

                var json = JsonSerializer.Serialize(descriptor, JsonOptions);
                File.WriteAllText(ShellLayoutFileName, json);
            }
            catch
            {
            }
        }

        private void RefreshFromEngineState()
        {
            RefreshCapabilities();
            _mouseLeaveClearsFocus = EngineServices.Settings.MouseLeaveClearsFocus;
            OnPropertyChanged(nameof(MouseLeaveClearsFocus));
            _groupOverlayOpacity = EngineServices.Settings.GroupOverlayOpacity;
            OnPropertyChanged(nameof(GroupOverlayOpacity));
            _nodeHighlightOpacity = EngineServices.Settings.NodeHighlightOpacity;
            OnPropertyChanged(nameof(NodeHighlightOpacity));
            _nodeFocusHaloRadiusMultiplier = EngineServices.Settings.NodeFocusHaloRadiusMultiplier;
            OnPropertyChanged(nameof(NodeFocusHaloRadiusMultiplier));
            _nodeSelectionHaloRadiusMultiplier = EngineServices.Settings.NodeSelectionHaloRadiusMultiplier;
            OnPropertyChanged(nameof(NodeSelectionHaloRadiusMultiplier));
            _nodeHaloMode = EngineServices.Settings.NodeHaloMode;
            OnPropertyChanged(nameof(NodeHaloMode));
            _nodeHaloOcclusionMode = EngineServices.Settings.NodeHaloOcclusionMode;
            OnPropertyChanged(nameof(NodeHaloOcclusionMode));
            _backgroundAnimationSpeed = EngineServices.Settings.BackgroundAnimationSpeed;
            OnPropertyChanged(nameof(BackgroundAnimationSpeed));
            _linkStrokeThickness = EngineServices.Settings.LinkStrokeThickness;
            OnPropertyChanged(nameof(LinkStrokeThickness));
            _linkOpacity = EngineServices.Settings.LinkOpacity;
            OnPropertyChanged(nameof(LinkOpacity));
            _paneletteBackgroundIntensity = EngineServices.Settings.PaneletteBackgroundIntensity;
            OnPropertyChanged(nameof(PaneletteBackgroundIntensity));
            _commandSurfaceOverlayOpacity = EngineServices.Settings.CommandSurfaceOverlayOpacity;
            OnPropertyChanged(nameof(CommandSurfaceOverlayOpacity));
            RaiseSceneStateChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void RefreshCapabilities()
        {
            var latest = EngineServices.Capabilities.GetAll().ToArray();
            Capabilities.Clear();

            foreach (var capability in latest)
            {
                Capabilities.Add(capability);
            }
        }

        private void UpdateLastActivity(string eventName, string activityLabel, Envelope envelope)
        {
            AddHistoryEntry(eventName, envelope);
            var detail = TryDescribeActivity(envelope);
            _lastActivitySummary = string.IsNullOrWhiteSpace(detail)
                ? $"Last Activity: {activityLabel} ({eventName}) @ {envelope.Ts:HH:mm:ss}"
                : $"Last Activity: {activityLabel} ({eventName}: {detail}) @ {envelope.Ts:HH:mm:ss}";
            OnPropertyChanged(nameof(LastActivitySummary));
        }

        private void AddHistoryEntry(string eventName, Envelope envelope)
        {
            if (!string.Equals(eventName, EventNames.CommandInvoked, StringComparison.Ordinal))
            {
                return;
            }

            if (envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!TryGetString(payload, "commandName", out var commandName))
            {
                return;
            }

            var domain = ClassifyCommandDomain(commandName);
            var label = $"{domain}: {commandName}";

            const int maxHistoryEntries = 10;
            if (_commandHistory.Count >= maxHistoryEntries)
            {
                _commandHistory.Dequeue();
            }

            _commandHistory.Enqueue(label);
            OnPropertyChanged(nameof(CommandHistorySummary));
        }

        private static string ClassifyCommandDomain(string commandName)
        {
            if (commandName is
                CommandNames.CreateEntity or
                CommandNames.UpdateEntity or
                CommandNames.UpdateEntities or
                CommandNames.Delete or
                CommandNames.DeleteEntities or
                CommandNames.SetTransform or
                CommandNames.Connect or
                CommandNames.Unlink or
                CommandNames.ClearLinks or
                CommandNames.GroupSelection or
                CommandNames.AddSelectionToGroup or
                CommandNames.RemoveSelectionFromGroup or
                CommandNames.DeleteGroup or
                CommandNames.AttachPanel or
                CommandNames.ClearPanelAttachment or
                CommandNames.Focus or
                CommandNames.FocusPanel or
                CommandNames.Select or
                CommandNames.SelectPanel or
                CommandNames.ClearSelection)
            {
                return "world";
            }

            if (commandName is
                CommandNames.HomeView or
                CommandNames.CenterOnNode or
                CommandNames.FrameSelection or
                CommandNames.BookmarkSave or
                CommandNames.BookmarkRestore or
                CommandNames.SetInteractionMode)
            {
                return "navigation";
            }

            if (commandName is
                CommandNames.SemanticsIndex or
                CommandNames.SemanticsQuerySimilar or
                CommandNames.SemanticsExplain)
            {
                return "semantics";
            }

            return "other";
        }

        private static string? TryDescribeActivity(Envelope envelope)
        {
            if (envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
            {
                return envelope.Name;
            }

            if (string.Equals(envelope.Name, EventNames.CommandInvoked, StringComparison.Ordinal))
            {
                if (TryGetString(payload, "commandName", out var commandName))
                {
                    return commandName;
                }

                return envelope.Name;
            }

            if (TryGetString(payload, "reason", out var reason))
            {
                return reason;
            }

            if (TryGetString(payload, "label", out var label))
            {
                return label;
            }

            if (TryGetString(payload, "bookmarkName", out var bookmarkName))
            {
                return bookmarkName;
            }

            if (TryGetString(payload, "focusedNodeId", out var focusedNodeId))
            {
                return focusedNodeId;
            }

            if (TryGetString(payload, "viewRef", out var viewRef))
            {
                return viewRef;
            }

            if (TryGetString(payload, "mode", out var mode))
            {
                return mode;
            }

            return envelope.Name;
        }

        private static bool TryGetString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;

            if (!element.TryGetProperty(propertyName, out var propertyValue))
            {
                return false;
            }

            if (propertyValue.ValueKind == JsonValueKind.String)
            {
                var text = propertyValue.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    value = text;
                    return true;
                }
            }

            return false;
        }

        private void RaiseSceneStateChanged()
        {
            OnPropertyChanged(nameof(FocusSummary));
            OnPropertyChanged(nameof(FocusOriginSummary));
            OnPropertyChanged(nameof(EnteredNodeSummary));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(BookmarkSummary));
            OnPropertyChanged(nameof(ViewSummary));
            OnPropertyChanged(nameof(ViewDetails));
            OnPropertyChanged(nameof(FocusedTransformSummary));
            OnPropertyChanged(nameof(FocusedTransformDetails));
            OnPropertyChanged(nameof(InteractionModeSummary));
            OnPropertyChanged(nameof(BookmarkDetails));
            OnPropertyChanged(nameof(GroupSummary));
            OnPropertyChanged(nameof(GroupDetails));
            OnPropertyChanged(nameof(LinkSummary));
            OnPropertyChanged(nameof(InteractionSemanticsSummary));
            OnPropertyChanged(nameof(LinkDetails));
            OnPropertyChanged(nameof(PanelSummary));
            OnPropertyChanged(nameof(ActionReadinessSummary));
            OnPropertyChanged(nameof(LastActivitySummary));
            OnPropertyChanged(nameof(NavigationHistorySummary));
            OnPropertyChanged(nameof(PanelDetails));
        }

        private void RaiseCommandCanExecuteChanged()
        {
            _focusFirstNodeCommand.RaiseCanExecuteChanged();
            _activateNavigateModeCommand.RaiseCanExecuteChanged();
            _activateMoveModeCommand.RaiseCanExecuteChanged();
            _activateMarqueeModeCommand.RaiseCanExecuteChanged();
            _selectFirstNodeCommand.RaiseCanExecuteChanged();
            _focusFirstPanelCommand.RaiseCanExecuteChanged();
            _selectFirstPanelCommand.RaiseCanExecuteChanged();
            _createDemoNodeCommand.RaiseCanExecuteChanged();
            _nudgeFocusedLeftCommand.RaiseCanExecuteChanged();
            _nudgeFocusedRightCommand.RaiseCanExecuteChanged();
            _nudgeFocusedUpCommand.RaiseCanExecuteChanged();
            _nudgeFocusedDownCommand.RaiseCanExecuteChanged();
            _nudgeFocusedForwardCommand.RaiseCanExecuteChanged();
            _nudgeFocusedBackCommand.RaiseCanExecuteChanged();
            _growFocusedNodeCommand.RaiseCanExecuteChanged();
            _shrinkFocusedNodeCommand.RaiseCanExecuteChanged();
            _applyTrianglePrimitiveCommand.RaiseCanExecuteChanged();
            _applySquarePrimitiveCommand.RaiseCanExecuteChanged();
            _applyDiamondPrimitiveCommand.RaiseCanExecuteChanged();
            _applyPentagonPrimitiveCommand.RaiseCanExecuteChanged();
            _applyHexagonPrimitiveCommand.RaiseCanExecuteChanged();
            _applyCubePrimitiveCommand.RaiseCanExecuteChanged();
            _applyTetrahedronPrimitiveCommand.RaiseCanExecuteChanged();
            _applySpherePrimitiveCommand.RaiseCanExecuteChanged();
            _applyBoxPrimitiveCommand.RaiseCanExecuteChanged();
            _applyBlueAppearanceCommand.RaiseCanExecuteChanged();
            _applyVioletAppearanceCommand.RaiseCanExecuteChanged();
            _applyGreenAppearanceCommand.RaiseCanExecuteChanged();
            _increaseOpacityCommand.RaiseCanExecuteChanged();
            _decreaseOpacityCommand.RaiseCanExecuteChanged();
            _connectFocusedNodeCommand.RaiseCanExecuteChanged();
            _unlinkFocusedNodeCommand.RaiseCanExecuteChanged();
            _groupSelectionCommand.RaiseCanExecuteChanged();
            _addSelectionToActiveGroupCommand.RaiseCanExecuteChanged();
            _removeSelectionFromActiveGroupCommand.RaiseCanExecuteChanged();
            _deleteActiveGroupCommand.RaiseCanExecuteChanged();
            _saveBookmarkCommand.RaiseCanExecuteChanged();
            _restoreLatestBookmarkCommand.RaiseCanExecuteChanged();
            _undoLastCommand.RaiseCanExecuteChanged();
            _homeViewCommand.RaiseCanExecuteChanged();
            _centerFocusedNodeCommand.RaiseCanExecuteChanged();
            _frameSelectionCommand.RaiseCanExecuteChanged();
            _deleteFocusedNodeCommand.RaiseCanExecuteChanged();
            _attachDemoPanelCommand.RaiseCanExecuteChanged();
            _attachLabelPaneletteCommand.RaiseCanExecuteChanged();
            _attachDetailMetadataPaneletteCommand.RaiseCanExecuteChanged();
            _clearLinksCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
            _minimizeShellPaneCommand.RaiseCanExecuteChanged();
            _restoreShellPaneCommand.RaiseCanExecuteChanged();
            _moveChildPaneUpCommand.RaiseCanExecuteChanged();
            _moveChildPaneDownCommand.RaiseCanExecuteChanged();
            _createOrRestoreParentPaneCommand.RaiseCanExecuteChanged();
            _destroyParentPaneCommand.RaiseCanExecuteChanged();
        }

        private void SetExpansionState(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            if (propertyName is not null)
            {
                OnPropertyChanged(propertyName);
            }

            OnPropertyChanged(nameof(PaneStructureSummary));
        }

        private void ApplyBackgroundPreset(string preset)
        {
            switch (preset)
            {
                case "DeepSpace":
                    EngineServices.Settings.BackgroundMode = "gradient";
                    EngineServices.Settings.BackgroundBaseColor = "#050911";
                    EngineServices.Settings.BackgroundTopColor = "#0B1623";
                    EngineServices.Settings.BackgroundBottomColor = "#050911";
                    EngineServices.Settings.BackgroundAnimationMode = "slowlerp";
                    EngineServices.Settings.BackgroundAnimationSpeed = 0.25f;
                    break;
                case "Dusk":
                    EngineServices.Settings.BackgroundMode = "gradient";
                    EngineServices.Settings.BackgroundBaseColor = "#1A1024";
                    EngineServices.Settings.BackgroundTopColor = "#302046";
                    EngineServices.Settings.BackgroundBottomColor = "#080611";
                    EngineServices.Settings.BackgroundAnimationMode = "slowlerp";
                    EngineServices.Settings.BackgroundAnimationSpeed = 0.35f;
                    break;
                case "Paper":
                    EngineServices.Settings.BackgroundMode = "solid";
                    EngineServices.Settings.BackgroundBaseColor = "#F5F5F2";
                    EngineServices.Settings.BackgroundTopColor = "#F5F5F2";
                    EngineServices.Settings.BackgroundBottomColor = "#F5F5F2";
                    EngineServices.Settings.BackgroundAnimationMode = "off";
                    EngineServices.Settings.BackgroundAnimationSpeed = 0.0f;
                    break;
                default:
                    return;
            }

            RefreshFromEngineState();
        }

        private static string FormatReady(bool ready) => ready ? "ready" : "blocked";
        private static string FormatVector3(Vector3 value) =>
            $"({value.X:0.##}, {value.Y:0.##}, {value.Z:0.##})";

        private static string FormatExpanded(bool expanded) => expanded ? "open" : "collapsed";

        private static string FormatFocusOrigin(string origin) =>
            string.IsNullOrWhiteSpace(origin)
                ? "unknown"
                : origin.Trim().ToLowerInvariant() switch
                {
                    "mouse" => "mouse (viewport)",
                    "keyboard" => "keyboard",
                    "command" => "shell command",
                    "programmatic" => "programmatic (engine/bookmark)",
                    _ => origin.Trim()
                };

        private bool IsInteractionMode(string mode) =>
            string.Equals(
                _shellScene.GetInteractionMode(),
                mode,
                StringComparison.Ordinal);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName is not null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;
            private readonly Func<object?, bool>? _canExecute;

            public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

            public void Execute(object? parameter) => _execute(parameter);

            public void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
