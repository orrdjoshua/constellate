using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Windows.Input;
using Constellate.Core.Capabilities;
using Constellate.Core.Messaging;
using Constellate.SDK;
using Constellate.Core.Scene;

namespace Constellate.App;

/// <summary>
/// Core spine of the MainWindowViewModel:
/// - field declarations (subscriptions, command fields, settings/layout/shadow/expansion backers)
/// - constructor (moved to FieldsAndCtor partial)
/// - ICommand property exposures
/// - pane collections/projections and shell visibility booleans
/// - layout row/column bindings, drag-shadow properties, and expansion props
///
/// Behavioral helpers (EngineSync, SceneCommands, Settings, Readouts, PaneLayout, PaneCommands)
/// are provided by the already-extracted partials in this folder.
/// </summary>
public sealed partial class MainWindowViewModel
{
    // Json + engine bridge
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { IncludeFields = true };
    private readonly IDisposable[] _eventSubscriptions;
    private readonly ShellSceneState _shellScene = EngineServices.ShellScene;

    // Corner-ownership for Top/Left intersection
    private bool _isTopCornerOwnedByTop;

    // RelayCommand fields
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

    // Pane + layout command surface
    private readonly RelayCommand _minimizeShellPaneCommand;
    private readonly RelayCommand _restoreShellPaneCommand;
    private readonly RelayCommand _resetLayoutToDefaultCommand;
    private readonly RelayCommand _saveLayoutPresetCommand;
    private readonly RelayCommand _restoreLayoutPresetCommand;
    private readonly RelayCommand _createChildPaneCommand;
    private readonly RelayCommand _minimizeChildPaneCommand;
    private readonly RelayCommand _restoreChildPaneFromTaskbarCommand;
    private readonly RelayCommand _moveChildPaneUpCommand;
    private readonly RelayCommand _moveChildPaneDownCommand;
    private readonly RelayCommand _floatSettingsChildPaneCommand;
    private readonly RelayCommand _dockSettingsChildPaneCommand;
    private readonly RelayCommand _moveChildPaneToLeftHostCommand;
    private readonly RelayCommand _moveChildPaneToTopHostCommand;
    private readonly RelayCommand _moveChildPaneToRightHostCommand;
    private readonly RelayCommand _moveChildPaneToBottomHostCommand;
    private readonly RelayCommand _moveChildPaneToFloatingHostCommand;
    private readonly RelayCommand _destroyParentPaneCommand;
    private readonly RelayCommand _createOrRestoreParentPaneCommand;
    private readonly RelayCommand _setTopPaneSplitCommand;
    private readonly RelayCommand _setRightPaneSplitCommand;
    private readonly RelayCommand _setBottomPaneSplitCommand;
    private readonly RelayCommand _slideParentPaneCommand;
    private readonly RelayCommand _setLeftPaneSplitCommand;

    // Activity + history
    private string _lastActivitySummary = "Last Activity: app started";
    private readonly Queue<string> _commandHistory = new();

    // Optional saved layout (session-local)
    private ShellLayoutDescriptor? _savedLayout;

    // Expansion states (session-local)
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

    // Settings child float state
    private bool _isSettingsChildFloating;

    // Drag shadows (parent + child)
    private bool _isParentPaneDragShadowVisible;
    private double _parentPaneDragShadowLeft;
    private double _parentPaneDragShadowTop;
    private double _parentPaneDragShadowWidth;
    private double _parentPaneDragShadowHeight;

    private bool _isChildPaneDragShadowVisible;
    private double _childPaneDragShadowLeft;
    private double _childPaneDragShadowTop;
    private double _childPaneDragShadowWidth;
    private double _childPaneDragShadowHeight;

    // Settings backers
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

    // Grid placement for Left/Top ownership layout toggling
    private int _leftPaneRow = 0;
    private int _leftPaneRowSpan = 3;
    private int _topPaneColumn = 1;
    private int _topPaneColumnSpan = 1;

    // Slide indices per host
    private int _leftSlideIndex;
    private int _topSlideIndex;
    private int _rightSlideIndex;
    private int _bottomSlideIndex;
    private int _layoutChangeCount;

    // Collections + capability list
    public ObservableCollection<EngineCapability> Capabilities { get; } = new(EngineServices.Capabilities.GetAll());
    public ObservableCollection<ChildPaneDescriptor> ChildPanes { get; } = new();
    public ObservableCollection<ParentPaneModel> ParentPaneModels { get; } = new();
    public ObservableCollection<ChildPaneModel> ChildPaneModels { get; } = new();

    // Declarative taxonomy of parent-pane hosts
    public IReadOnlyList<PaneHostDescriptor> PaneHosts { get; } =
    [
        new PaneHostDescriptor("left", "Shell Sidebar", "LeftPaneHost"),
        new PaneHostDescriptor("top", "Viewport Header", "TopPaneHost"),
        new PaneHostDescriptor("right", "Right Sidebar", "RightPaneHost"),
        new PaneHostDescriptor("bottom", "Bottom Pane Host", "BottomPaneHost"),
        new PaneHostDescriptor("center", "Viewport", "CenterViewportHost")
    ];

    // Parent projections per host
    public IReadOnlyList<ParentPaneModel> ParentPaneModelsLeft =>
        ParentPaneModels.Where(p => !p.IsMinimized && string.Equals(p.HostId, "left", StringComparison.OrdinalIgnoreCase)).ToArray();

    public IReadOnlyList<ParentPaneModel> ParentPaneModelsTop =>
        ParentPaneModels.Where(p => !p.IsMinimized && string.Equals(p.HostId, "top", StringComparison.OrdinalIgnoreCase)).ToArray();

    public IReadOnlyList<ParentPaneModel> ParentPaneModelsRight =>
        ParentPaneModels.Where(p => !p.IsMinimized && string.Equals(p.HostId, "right", StringComparison.OrdinalIgnoreCase)).ToArray();

    public IReadOnlyList<ParentPaneModel> ParentPaneModelsBottom =>
        ParentPaneModels.Where(p => !p.IsMinimized && string.Equals(p.HostId, "bottom", StringComparison.OrdinalIgnoreCase)).ToArray();

    public IReadOnlyList<ParentPaneModel> ParentPaneModelsFloating =>
        ParentPaneModels.Where(p => string.Equals(p.HostId, "floating", StringComparison.OrdinalIgnoreCase)).ToArray();

    // Sorted child lists and minimized taskbar
    public IReadOnlyList<ChildPaneDescriptor> ChildPanesOrdered =>
        ChildPanes.OrderBy(p => p.Order).ToArray();

    public IReadOnlyList<ChildPaneDescriptor> VisibleChildPanesOrdered =>
        ChildPanes.Where(p => !p.IsMinimized).OrderBy(p => p.Order).ToArray();

    public bool HasMinimizedChildPanes =>
        ChildPanes.Any(p => p.IsMinimized);

    public IEnumerable<ChildPaneDescriptor> MinimizedChildPanes =>
        ChildPanesOrdered.Where(p => p.IsMinimized);

    // Host visibility booleans
    public bool IsShellPaneOnLeft => ParentPaneModelsLeft.Count > 0;
    public bool IsShellPaneOnTop => ParentPaneModelsTop.Count > 0;
    public bool IsShellPaneOnRight => ParentPaneModelsRight.Count > 0;
    public bool IsShellPaneOnBottom => ParentPaneModelsBottom.Count > 0;
    public bool IsShellPaneFloating => ParentPaneModelsFloating.Count > 0;
    public bool IsRightPaneHostVisible => ParentPaneModelsRight.Count > 0;
    public bool IsShellPaneMinimized => ParentPaneModels.Any(p => p.IsMinimized);

    // Per-host minimized flags (used by PaneLayout partial)
    public bool HasMinimizedParentLeft =>
        ParentPaneModels.Any(p => p.IsMinimized && string.Equals(NormalizeHostId(p.HostId), "left", StringComparison.Ordinal));

    public bool HasMinimizedParentTop =>
        ParentPaneModels.Any(p => p.IsMinimized && string.Equals(NormalizeHostId(p.HostId), "top", StringComparison.Ordinal));

    public bool HasMinimizedParentRight =>
        ParentPaneModels.Any(p => p.IsMinimized && string.Equals(NormalizeHostId(p.HostId), "right", StringComparison.Ordinal));

    public bool HasMinimizedParentBottom =>
        ParentPaneModels.Any(p => p.IsMinimized && string.Equals(NormalizeHostId(p.HostId), "bottom", StringComparison.Ordinal));

    // Node halo option lists for UI
    public string[] NodeHaloModeOptions { get; } = new[] { "2d", "3d", "both" };
    public string[] NodeHaloOcclusionModeOptions { get; } = new[] { "hollow", "occluding" };

    // Layout row/column bindings used by XAML
    public int LeftPaneRow
    {
        get => _leftPaneRow;
        set { if (_leftPaneRow != value) { _leftPaneRow = value; OnPropertyChanged(); } }
    }

    public int LeftPaneRowSpan
    {
        get => _leftPaneRowSpan;
        set { if (_leftPaneRowSpan != value) { _leftPaneRowSpan = value; OnPropertyChanged(); } }
    }

    public int TopPaneColumn
    {
        get => _topPaneColumn;
        set { if (_topPaneColumn != value) { _topPaneColumn = value; OnPropertyChanged(); } }
    }

    public int TopPaneColumnSpan
    {
        get => _topPaneColumnSpan;
        set { if (_topPaneColumnSpan != value) { _topPaneColumnSpan = value; OnPropertyChanged(); } }
    }

    // Visibility helpers for special children in shell
    public bool IsShellCurrentChildVisible => !IsChildPaneMinimized("shell.current");
    public bool IsShellSettingsChildVisible => !IsChildPaneMinimized("shell.settings") && !IsSettingsChildFloating;
    public bool IsShellDeveloperChildVisible => !IsChildPaneMinimized("shell.developer");
    public bool IsShellCapabilitiesChildVisible => !IsChildPaneMinimized("shell.capabilities");

    private bool IsChildPaneMinimized(string id)
    {
        foreach (var pane in ChildPanes)
        {
            if (string.Equals(pane.Id, id, StringComparison.Ordinal))
                return pane.IsMinimized;
        }
        return false;
    }

    // Drag shadow properties (parent)
    public bool IsParentPaneDragShadowVisible
    {
        get => _isParentPaneDragShadowVisible;
        private set { if (_isParentPaneDragShadowVisible != value) { _isParentPaneDragShadowVisible = value; OnPropertyChanged(); } }
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

    // Drag shadow properties (child)
    public bool IsChildPaneDragShadowVisible
    {
        get => _isChildPaneDragShadowVisible;
        private set { if (_isChildPaneDragShadowVisible != value) { _isChildPaneDragShadowVisible = value; OnPropertyChanged(); } }
    }

    public double ChildPaneDragShadowLeft
    {
        get => _childPaneDragShadowLeft;
        private set { if (Math.Abs(_childPaneDragShadowLeft - value) > double.Epsilon) { _childPaneDragShadowLeft = value; OnPropertyChanged(); } }
    }

    public double ChildPaneDragShadowTop
    {
        get => _childPaneDragShadowTop;
        private set { if (Math.Abs(_childPaneDragShadowTop - value) > double.Epsilon) { _childPaneDragShadowTop = value; OnPropertyChanged(); } }
    }

    public double ChildPaneDragShadowWidth
    {
        get => _childPaneDragShadowWidth;
        private set { if (Math.Abs(_childPaneDragShadowWidth - value) > double.Epsilon) { _childPaneDragShadowWidth = value; OnPropertyChanged(); } }
    }

    public double ChildPaneDragShadowHeight
    {
        get => _childPaneDragShadowHeight;
        private set { if (Math.Abs(_childPaneDragShadowHeight - value) > double.Epsilon) { _childPaneDragShadowHeight = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Increments on each RaiseParentPaneLayoutChanged to verify UI-binding and change-notification flow.
    /// </summary>
    public int LayoutChangeCount
    {
        get => _layoutChangeCount;
        private set
        {
            if (_layoutChangeCount == value) return;
            _layoutChangeCount = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// A stable (per-instance) VM identifier for quick visual/log correlation.
    /// </summary>
    public string VmId => GetHashCode().ToString("X");

    /// <summary>
    /// Current number of parent panes in this VM instance.
    /// </summary>
    public int ParentPaneCount => ParentPaneModels.Count;
    // Settings child floating flag
    public bool IsSettingsChildFloating
    {
        get => _isSettingsChildFloating;
        set
        {
            if (_isSettingsChildFloating == value) return;
            _isSettingsChildFloating = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsShellSettingsChildVisible));
        }
    }

    // Expansion states (backed by SetExpansionState helper in monolith; will be co-located on final overwrite)
    public bool IsCurrentStateSectionExpanded { get => _isCurrentStateSectionExpanded; set => SetExpansionState(ref _isCurrentStateSectionExpanded, value); }
    public bool IsCommandSurfaceSectionExpanded { get => _isCommandSurfaceSectionExpanded; set => SetExpansionState(ref _isCommandSurfaceSectionExpanded, value); }
    public bool IsSettingsSectionExpanded { get => _isSettingsSectionExpanded; set => SetExpansionState(ref _isSettingsSectionExpanded, value); }
    public bool IsSelectionFocusGroupExpanded { get => _isSelectionFocusGroupExpanded; set => SetExpansionState(ref _isSelectionFocusGroupExpanded, value); }
    public bool IsLinksGroupExpanded { get => _isLinksGroupExpanded; set => SetExpansionState(ref _isLinksGroupExpanded, value); }
    public bool IsGroupsGroupExpanded { get => _isGroupsGroupExpanded; set => SetExpansionState(ref _isGroupsGroupExpanded, value); }
    public bool IsHistoryGroupExpanded { get => _isHistoryGroupExpanded; set => SetExpansionState(ref _isHistoryGroupExpanded, value); }
    public bool IsViewGroupExpanded { get => _isViewGroupExpanded; set => SetExpansionState(ref _isViewGroupExpanded, value); }
    public bool IsEditModesGroupExpanded { get => _isEditModesGroupExpanded; set => SetExpansionState(ref _isEditModesGroupExpanded, value); }
    public bool IsMutationGroupExpanded { get => _isMutationGroupExpanded; set => SetExpansionState(ref _isMutationGroupExpanded, value); }
    public bool IsAppearanceGroupExpanded { get => _isAppearanceGroupExpanded; set => SetExpansionState(ref _isAppearanceGroupExpanded, value); }
    public bool IsDeveloperReadoutsSectionExpanded { get => _isDeveloperReadoutsSectionExpanded; set => SetExpansionState(ref _isDeveloperReadoutsSectionExpanded, value); }
    public bool IsCapabilitiesSectionExpanded { get => _isCapabilitiesSectionExpanded; set => SetExpansionState(ref _isCapabilitiesSectionExpanded, value); }

    // ICommand exposures are declared in MainWindowViewModel.Core.CommandExposures.cs
    #if false
    // (intentionally no-op here)
    #endif
}
