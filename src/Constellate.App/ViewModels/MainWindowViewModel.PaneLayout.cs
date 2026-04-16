using System;
using System.Linq;
using System.Diagnostics;
using System.ComponentModel;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing pane layout helpers:
/// corner-ownership toggle, drag-shadow setters, slide/split handlers, and
/// per-parent child recomputation. This is a mechanical extraction only.
/// </summary>
public sealed partial class MainWindowViewModel
{
    // Ownership flags
    /// <summary>
    /// Toggle which pane "owns" the top-left/right corners when both Top and Left
    /// parent panes are visible: either Left is full-height and Top is center-only
    /// (default), or Top spans the full top width and Left starts below it.
    /// </summary>
    public void ToggleTopCornerOwnership()
    {
        var hasTop = ParentPaneModelsTop.Count > 0;
        var hasLeft = ParentPaneModelsLeft.Count > 0;

        if (!hasTop || !hasLeft)
        {
            return;
        }

        _isTopCornerOwnedByTop = !_isTopCornerOwnedByTop;
        UpdateTopLeftOwnershipLayout();
    }

    private bool _isTopCornerOwnedByTop;
    private bool _isTopRightCornerOwnedByTop;

    // Right host placement properties to support top-right ownership swap
    private int _rightPaneRow = 0;
    private int _rightPaneRowSpan = 3;
    public int RightPaneRow { get => _rightPaneRow; set { if (_rightPaneRow != value) { _rightPaneRow = value; OnPropertyChanged(); } } }
    public int RightPaneRowSpan { get => _rightPaneRowSpan; set { if (_rightPaneRowSpan != value) { _rightPaneRowSpan = value; OnPropertyChanged(); } } }

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

    private int GetSlideIndexForHost(string hostId)
    {
        var normalized = NormalizeHostId(hostId);
        return normalized switch
        {
            "top" => _topSlideIndex,
            "right" => _rightSlideIndex,
            "bottom" => _bottomSlideIndex,
            _ => _leftSlideIndex
        };
    }

    private void SlideParentPane(string arg)
    {
        var parts = arg.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return;
        }

        var host = NormalizeHostId(parts[0]);
        var delta = string.Equals(parts[1], "next", StringComparison.OrdinalIgnoreCase) ? 1 : -1;

        switch (host)
        {
            case "left":
                _leftSlideIndex = Math.Max(0, _leftSlideIndex + delta);
                foreach (var parent in ParentPaneModels.Where(p =>
                             string.Equals(NormalizeHostId(p.HostId), "left", StringComparison.OrdinalIgnoreCase)))
                {
                    parent.SlideIndex = _leftSlideIndex;
                }
                break;
            case "top":
                _topSlideIndex = Math.Max(0, _topSlideIndex + delta);
                foreach (var parent in ParentPaneModels.Where(p =>
                             string.Equals(NormalizeHostId(p.HostId), "top", StringComparison.OrdinalIgnoreCase)))
                {
                    parent.SlideIndex = _topSlideIndex;
                }
                break;
            case "right":
                _rightSlideIndex = Math.Max(0, _rightSlideIndex + delta);
                foreach (var parent in ParentPaneModels.Where(p =>
                             string.Equals(NormalizeHostId(p.HostId), "right", StringComparison.OrdinalIgnoreCase)))
                {
                    parent.SlideIndex = _rightSlideIndex;
                }
                break;
            case "bottom":
                _bottomSlideIndex = Math.Max(0, _bottomSlideIndex + delta);
                foreach (var parent in ParentPaneModels.Where(p =>
                             string.Equals(NormalizeHostId(p.HostId), "bottom", StringComparison.OrdinalIgnoreCase)))
                {
                    parent.SlideIndex = _bottomSlideIndex;
                }
                break;
        }

        // Recompute per-parent child projections for the new slide index.
        RaiseChildPaneCollectionsChanged();
    }

    private void ApplyChildPaneSplitsForHost(string hostId, int splits)
    {
        var normalizedHost = NormalizeHostId(hostId);
        if (splits <= 0)
        {
            splits = 1;
        }

        splits = Math.Min(splits, 2);

        var targetParentIds = ParentPaneModels
            .Where(parent =>
                string.Equals(
                    NormalizeHostId(parent.HostId),
                    normalizedHost,
                    StringComparison.Ordinal))
            .Select(parent => parent.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (targetParentIds.Count == 0)
        {
            return;
        }

        var ordered = ChildPanes
            .Where(pane =>
                !string.IsNullOrWhiteSpace(pane.ParentId) &&
                targetParentIds.Contains(pane.ParentId))
            .OrderBy(pane => pane.Order)
            .ToArray();

        if (ordered.Length == 0)
        {
            return;
        }

        for (var i = 0; i < ordered.Length; i++)
        {
            var pane = ordered[i];
            var newIndex = i % splits;

            for (var j = 0; j < ChildPanes.Count; j++)
            {
                if (!string.Equals(ChildPanes[j].Id, pane.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                ChildPanes[j] = ChildPanes[j] with { ContainerIndex = newIndex };
                break;
            }
        }

        RaiseChildPaneCollectionsChanged();
    }

    private void RaiseChildPaneCollectionsChanged()
    {
        OnPropertyChanged(nameof(ChildPanesOrdered));
        OnPropertyChanged(nameof(VisibleChildPanesOrdered));
        OnPropertyChanged(nameof(HasMinimizedChildPanes));
        OnPropertyChanged(nameof(MinimizedChildPanes));
        OnPropertyChanged(nameof(IsShellCurrentChildVisible));
        OnPropertyChanged(nameof(IsShellSettingsChildVisible));
        OnPropertyChanged(nameof(IsShellDeveloperChildVisible));
        OnPropertyChanged(nameof(IsShellCapabilitiesChildVisible));
        OnPropertyChanged(nameof(PaneStructureSummary));

        // Update per-parent child lists so each ParentPaneModel exposes
        // its own VisibleChildrenPrimary0/1 and MinimizedChildren for
        // the XAML templates rendered via ParentPaneModels* collections.
        foreach (var parent in ParentPaneModels)
        {
            var parentId = parent.Id;
            var slideIndex = parent.SlideIndex;

            var visibleForParent = ChildPanes
                .Where(pane =>
                    !pane.IsMinimized &&
                    pane.SlideIndex == slideIndex &&
                    string.Equals(pane.ParentId, parentId, StringComparison.Ordinal))
                .OrderBy(pane => pane.Order)
                .ToArray();

            parent.VisibleChildrenPrimary0 = visibleForParent
                .Where(pane => pane.ContainerIndex == 0)
                .ToArray();

            parent.VisibleChildrenPrimary1 = visibleForParent
                .Where(pane => pane.ContainerIndex == 1)
                .ToArray();

            var minimizedForParent = ChildPanes
                .Where(pane =>
                    pane.IsMinimized &&
                    string.Equals(pane.ParentId, parentId, StringComparison.Ordinal))
                .OrderBy(pane => pane.Order)
                .ToArray();

            parent.MinimizedChildren = minimizedForParent;
        }
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
        OnPropertyChanged(nameof(IsShellPaneMinimized));
        OnPropertyChanged(nameof(IsRightPaneHostVisible));
        OnPropertyChanged(nameof(ParentPaneModelsLeft));
        OnPropertyChanged(nameof(ParentPaneModelsTop));
        OnPropertyChanged(nameof(ParentPaneModelsRight));
        OnPropertyChanged(nameof(ParentPaneModelsBottom));
        OnPropertyChanged(nameof(ParentPaneModelsFloating));
        OnPropertyChanged(nameof(HasMinimizedParentLeft));
        OnPropertyChanged(nameof(HasMinimizedParentTop));
        OnPropertyChanged(nameof(HasMinimizedParentRight));
        OnPropertyChanged(nameof(HasMinimizedParentBottom));
        OnPropertyChanged(nameof(PaneStructureSummary));
        // Bump a visible counter and surface current parent count for HUD diagnostics.
        LayoutChangeCount = LayoutChangeCount + 1;
        OnPropertyChanged(nameof(LayoutChangeCount));
        OnPropertyChanged(nameof(ParentPaneCount));
        UpdateTopLeftOwnershipLayout();

        _minimizeShellPaneCommand.RaiseCanExecuteChanged();
        _restoreShellPaneCommand.RaiseCanExecuteChanged();
        _createOrRestoreParentPaneCommand.RaiseCanExecuteChanged();
        _destroyParentPaneCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Top-right intersection toggle (Top vs Right). When both Top and Right are visible, double-clicking
    /// the top-right intersection flips whether Right runs full-height (owns the corner) or starts under Top.
    /// </summary>
    public void ToggleTopRightCornerOwnership()
    {
        var hasTop = ParentPaneModelsTop.Count > 0;
        var hasRight = ParentPaneModelsRight.Count > 0;
        if (!hasTop || !hasRight)
        {
            return;
        }

        _isTopRightCornerOwnedByTop = !_isTopRightCornerOwnedByTop;
        UpdateTopRightOwnershipLayout();
    }

    private void UpdateTopRightOwnershipLayout()
    {
        var hasTop = ParentPaneModelsTop.Count > 0;
        var hasRight = ParentPaneModelsRight.Count > 0;

        // Default: Right occupies full height from row 0..2
        if (!hasTop || !hasRight)
        {
            RightPaneRow = 0;
            RightPaneRowSpan = 3;
            return;
        }

        if (_isTopRightCornerOwnedByTop)
        {
            // Top owns the top-right corner: Right starts below the Top row
            RightPaneRow = 1;
            RightPaneRowSpan = 2;
        }
        else
        {
            // Right owns: full-height
            RightPaneRow = 0;
            RightPaneRowSpan = 3;
        }
    }

    /// <summary>
    /// Returns true if a dock host (left/top/right/bottom) is currently occupied by any parent pane,
    /// including minimized panes. Floating host is not considered a dock and should not be passed here.
    /// </summary>
    public bool IsDockHostOccupied(string hostId)
    {
        var normalized = NormalizeHostId(hostId);
        if (string.Equals(normalized, "floating", StringComparison.Ordinal)) return false;
        return ParentPaneModels.Any(p =>
            string.Equals(NormalizeHostId(p.HostId), normalized, StringComparison.Ordinal));
    }

    private void UpdateTopLeftOwnershipLayout()
    {
        var hasTop = ParentPaneModelsTop.Count > 0;
        var hasLeft = ParentPaneModelsLeft.Count > 0;

        if (!hasTop && !hasLeft)
        {
            LeftPaneRow = 0;
            LeftPaneRowSpan = 3;
            TopPaneColumn = 1;
            TopPaneColumnSpan = 1;
            return;
        }

        if (hasTop && !hasLeft)
        {
            _isTopCornerOwnedByTop = true;
            LeftPaneRow = 1;
            LeftPaneRowSpan = 2;
            TopPaneColumn = 0;
            TopPaneColumnSpan = 2;
            return;
        }

        if (!hasTop && hasLeft)
        {
            _isTopCornerOwnedByTop = false;
            LeftPaneRow = 0;
            LeftPaneRowSpan = 3;
            TopPaneColumn = 1;
            TopPaneColumnSpan = 1;
            return;
        }

        if (_isTopCornerOwnedByTop)
        {
            LeftPaneRow = 1;
            LeftPaneRowSpan = 2;
            TopPaneColumn = 0;
            TopPaneColumnSpan = 2;
        }
        else
        {
            LeftPaneRow = 0;
            LeftPaneRowSpan = 3;
            TopPaneColumn = 1;
            TopPaneColumnSpan = 1;
        }
    }
}
