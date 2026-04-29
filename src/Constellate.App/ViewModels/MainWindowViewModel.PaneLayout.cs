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

    // Overlay highlight for cross-parent child re-dock targeting
    private bool _isChildDockTargetHighlightVisible;
    private double _childDockTargetLeft;
    private double _childDockTargetTop;
    private double _childDockTargetWidth;
    private double _childDockTargetHeight;

    public bool IsChildDockTargetHighlightVisible
    {
        get => _isChildDockTargetHighlightVisible;
        private set { if (_isChildDockTargetHighlightVisible != value) { _isChildDockTargetHighlightVisible = value; OnPropertyChanged(); } }
    }
    public double ChildDockTargetLeft
    {
        get => _childDockTargetLeft;
        private set { if (System.Math.Abs(_childDockTargetLeft - value) > double.Epsilon) { _childDockTargetLeft = value; OnPropertyChanged(); } }
    }
    public double ChildDockTargetTop
    {
        get => _childDockTargetTop;
        private set { if (System.Math.Abs(_childDockTargetTop - value) > double.Epsilon) { _childDockTargetTop = value; OnPropertyChanged(); } }
    }
    public double ChildDockTargetWidth
    {
        get => _childDockTargetWidth;
        private set { if (System.Math.Abs(_childDockTargetWidth - value) > double.Epsilon) { _childDockTargetWidth = value; OnPropertyChanged(); } }
    }
    public double ChildDockTargetHeight
    {
        get => _childDockTargetHeight;
        private set { if (System.Math.Abs(_childDockTargetHeight - value) > double.Epsilon) { _childDockTargetHeight = value; OnPropertyChanged(); } }
    }

    public void SetChildDockTargetHighlight(bool visible, double left, double top, double width, double height)
    {
        IsChildDockTargetHighlightVisible = visible;
        if (!visible)
        {
            return;
        }
        ChildDockTargetLeft = left;
        ChildDockTargetTop = top;
        ChildDockTargetWidth = width;
        ChildDockTargetHeight = height;
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
        OnPropertyChanged(nameof(CanonicalRecordDetailPrimaryPaneId));
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
