namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing pane-layout coordination helpers:
/// drag shadows and centralized collection/layout refresh flow.
/// Shell-level viewport and dock ownership state now lives in MainWindowViewModel.ShellLayoutState.cs,
/// while parent-body mutation helpers now live in MainWindowViewModel.ParentBodyState.cs.
/// </summary>
public sealed partial class MainWindowViewModel
{
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

    /// <summary>
    /// Update the drag-shadow rectangle used to preview child-pane movement across
    /// parent hosts. When <paramref name="visible"/> is false, the rect values are
    /// ignored and the shadow is hidden.
    /// </summary>
    public void SetChildPaneDragShadow(bool visible, double left, double top, double width, double height)
    {
        IsChildPaneDragShadowVisible = visible;

        if (!visible)
        {
            return;
        }

        ChildPaneDragShadowLeft = left;
        ChildPaneDragShadowTop = top;
        ChildPaneDragShadowWidth = width;
        ChildPaneDragShadowHeight = height;
    }

    private void RaiseChildPaneCollectionsChanged()
    {
        OnPropertyChanged(nameof(ChildPanesOrdered));
        OnPropertyChanged(nameof(VisibleChildPanesOrdered));
        OnPropertyChanged(nameof(FloatingChildPanes));
        OnPropertyChanged(nameof(IsFloatingLayerVisible));
        OnPropertyChanged(nameof(HasMinimizedChildPanes));
        OnPropertyChanged(nameof(MinimizedChildPanes));
        OnPropertyChanged(nameof(IsShellCurrentChildVisible));
        OnPropertyChanged(nameof(IsShellSettingsChildVisible));
        OnPropertyChanged(nameof(IsShellDeveloperChildVisible));
        OnPropertyChanged(nameof(IsShellCapabilitiesChildVisible));
        OnPropertyChanged(nameof(PaneStructureSummary));

        RefreshParentBodyLayoutProjections();
    }

    private void RaiseParentPaneLayoutChanged(bool includeChildRefresh = false)
    {
        if (includeChildRefresh)
        {
            RaiseChildPaneCollectionsChanged();
        }

        OnPropertyChanged(nameof(IsShellPaneOnLeft));
        OnPropertyChanged(nameof(IsShellPaneOnTop));
        OnPropertyChanged(nameof(IsShellPaneOnRight));
        OnPropertyChanged(nameof(IsShellPaneOnBottom));
        OnPropertyChanged(nameof(IsShellPaneFloating));
        OnPropertyChanged(nameof(IsFloatingLayerVisible));
        OnPropertyChanged(nameof(IsShellPaneMinimized));
        OnPropertyChanged(nameof(ParentPaneModelsLeft));
        OnPropertyChanged(nameof(ParentPaneModelsTop));
        OnPropertyChanged(nameof(ParentPaneModelsRight));
        OnPropertyChanged(nameof(ParentPaneModelsBottom));
        OnPropertyChanged(nameof(ParentPaneModelsFloating));
        OnPropertyChanged(nameof(ActiveParentPaneLeft));
        OnPropertyChanged(nameof(ActiveParentPaneTop));
        OnPropertyChanged(nameof(ActiveParentPaneRight));
        OnPropertyChanged(nameof(ActiveParentPaneBottom));
        OnPropertyChanged(nameof(HasMinimizedParentLeft));
        OnPropertyChanged(nameof(HasMinimizedParentTop));
        OnPropertyChanged(nameof(HasMinimizedParentRight));
        OnPropertyChanged(nameof(HasMinimizedParentBottom));
        OnPropertyChanged(nameof(PaneStructureSummary));

        LayoutChangeCount = LayoutChangeCount + 1;
        OnPropertyChanged(nameof(LayoutChangeCount));
        OnPropertyChanged(nameof(ParentPaneCount));

        RecomputeWorldShellLayout();
        _minimizeShellPaneCommand.RaiseCanExecuteChanged();
        _restoreShellPaneCommand.RaiseCanExecuteChanged();
        _createOrRestoreParentPaneCommand.RaiseCanExecuteChanged();
        _destroyParentPaneCommand.RaiseCanExecuteChanged();
    }

    // Centralized layout recompute entry remains RaiseParentPaneLayoutChanged; we now call
    // WorldShellLayoutEngine there so host projections derive from canonical shell layout state.
}
