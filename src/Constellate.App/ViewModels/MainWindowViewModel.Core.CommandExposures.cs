using System.Windows.Input;

namespace Constellate.App;

public sealed partial class MainWindowViewModel
{
    // Public ICommand exposures extracted from Core to this dedicated partial.
    public ICommand FocusFirstNodeCommand => _focusFirstNodeCommand;
    public ICommand SelectFirstNodeCommand => _selectFirstNodeCommand;
    public ICommand FocusFirstPanelCommand => _focusFirstPanelCommand;
    public ICommand SelectFirstPanelCommand => _selectFirstPanelCommand;
    public ICommand ActivateMoveModeCommand => _activateMoveModeCommand;
    public ICommand ActivateNavigateModeCommand => _activateNavigateModeCommand;
    public ICommand ActivateMarqueeModeCommand => _activateMarqueeModeCommand;
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
    public ICommand CreateChildPaneCommand => _createChildPaneCommand;
    public ICommand MinimizeChildPaneCommand => _minimizeChildPaneCommand;
    public ICommand RestoreChildPaneFromTaskbarCommand => _restoreChildPaneFromTaskbarCommand;
    public ICommand MoveChildPaneUpCommand => _moveChildPaneUpCommand;
    public ICommand MoveChildPaneDownCommand => _moveChildPaneDownCommand;
    public ICommand FloatSettingsChildPaneCommand => _floatSettingsChildPaneCommand;
    public ICommand DockSettingsChildPaneCommand => _dockSettingsChildPaneCommand;
    public ICommand DestroyChildPaneCommand => _destroyChildPaneCommand;
    public ICommand MoveChildPaneToLeftHostCommand => _moveChildPaneToLeftHostCommand;
    public ICommand MoveChildPaneToTopHostCommand => _moveChildPaneToTopHostCommand;
    public ICommand MoveChildPaneToRightHostCommand => _moveChildPaneToRightHostCommand;
    public ICommand MoveChildPaneToBottomHostCommand => _moveChildPaneToBottomHostCommand;
    public ICommand MoveChildPaneToFloatingHostCommand => _moveChildPaneToFloatingHostCommand;
    public ICommand DestroyParentPaneCommand => _destroyParentPaneCommand;
    public ICommand CreateOrRestoreParentPaneCommand => _createOrRestoreParentPaneCommand;
    public ICommand SetTopPaneSplitCommand => _setTopPaneSplitCommand;
    public ICommand SetRightPaneSplitCommand => _setRightPaneSplitCommand;
    public ICommand SetBottomPaneSplitCommand => _setBottomPaneSplitCommand;
    public ICommand SlideParentPaneCommand => _slideParentPaneCommand;
    public ICommand SetLeftPaneSplitCommand => _setLeftPaneSplitCommand;

    // New per-parent split/slide controls (ParentPaneView header)
    public ICommand SetParentSplitTo1Command => _setParentSplitTo1Command;
    public ICommand SetParentSplitTo2Command => _setParentSplitTo2Command;
    public ICommand SetParentSplitTo3Command => _setParentSplitTo3Command;

    public ICommand SetParentSlideTo1Command => _setParentSlideTo1Command;
    public ICommand SetParentSlideTo2Command => _setParentSlideTo2Command;
    public ICommand SetParentSlideTo3Command => _setParentSlideTo3Command;

    // Header chrome stubs
    public ICommand RenamePaneCommand => _renamePaneCommand;
    public ICommand AddCommandBarButtonCommand => _addCommandBarButtonCommand;
    public ICommand RemoveCommandBarButtonCommand => _removeCommandBarButtonCommand;
}
