using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing pane layout helpers:
/// corner-ownership toggle, drag-shadow setters, slide/split handlers, and
/// per-parent child recomputation. This is a mechanical extraction only.
/// </summary>
public sealed partial class MainWindowViewModel
{
    // Top-left corner toggle
    public void ToggleTopCornerOwnership()
    {
        var hasTop = ParentPaneModelsTop.Count > 0;
        var hasLeft = ParentPaneModelsLeft.Count > 0;

        if (!hasTop || !hasLeft)
        {
            return;
        }

        _isTopCornerOwnedByTop = !_isTopCornerOwnedByTop;
        UpdateLeftOwnershipLayout();
        UpdateTopOwnershipLayout();
    }
    // Right-side (top-right) ownership
    private bool _isTopRightCornerOwnedByTop;

    // Right host placement properties to support top-right ownership swap
    private int _rightPaneRow = 0;
    private int _rightPaneRowSpan = 3;
    public int RightPaneRow { get => _rightPaneRow; set { if (_rightPaneRow != value) { _rightPaneRow = value; OnPropertyChanged(); } } }
    public int RightPaneRowSpan { get => _rightPaneRowSpan; set { if (_rightPaneRowSpan != value) { _rightPaneRowSpan = value; OnPropertyChanged(); } } }

    // Bottom-corner ownership flags
    private bool _isBottomLeftCornerOwnedByBottom;
    private bool _isBottomRightCornerOwnedByBottom;

    // Bottom host column binding (mirrors Top)
    private int _bottomPaneColumn = 1;
    private int _bottomPaneColumnSpan = 1;
    public int BottomPaneColumn { get => _bottomPaneColumn; set { if (_bottomPaneColumn != value) { _bottomPaneColumn = value; OnPropertyChanged(); } } }
    public int BottomPaneColumnSpan { get => _bottomPaneColumnSpan; set { if (_bottomPaneColumnSpan != value) { _bottomPaneColumnSpan = value; OnPropertyChanged(); } } }

    // Drag shadow update helpers (unchanged)
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

    // Toggle bottom-left ownership
    public void ToggleBottomLeftCornerOwnership()
    {
        if (ParentPaneModelsBottom.Count == 0 || ParentPaneModelsLeft.Count == 0) return;
        _isBottomLeftCornerOwnedByBottom = !_isBottomLeftCornerOwnedByBottom;
        UpdateLeftOwnershipLayout();
        UpdateBottomOwnershipLayout();
    }

    public void SetParentSplitCount(string? parentId, int count)
    {
        if (string.IsNullOrWhiteSpace(parentId)) return;
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null) return;

        parent.SplitCount = Math.Max(1, Math.Min(3, count));
        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
    }

    public void SetParentSlideIndex(string? parentId, int index)
    {
        if (string.IsNullOrWhiteSpace(parentId)) return;
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null) return;

        parent.SlideIndex = Math.Clamp(index, 0, 2);
        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
    }

    /// <summary>
    /// Persist preferred-size ratios for a given lane by child id. Ratios should already be normalized, but we normalize defensively.
    /// </summary>
    public void UpdateLanePreferredRatios(string parentId, int laneIndex, IEnumerable<(string childId, double ratio)> updates)
    {
        var map = updates?.ToDictionary(x => x.childId, x => Math.Max(0.01, x.ratio), StringComparer.Ordinal) ?? new Dictionary<string, double>(StringComparer.Ordinal);
        var sum = map.Values.Sum();
        if (sum <= 1e-6) return;
        foreach (var kv in map.Keys.ToArray())
        {
            map[kv] = map[kv] / sum;
        }
        for (var i = 0; i < ChildPanes.Count; i++)
        {
            var c = ChildPanes[i];
            if (string.Equals(c.ParentId, parentId, StringComparison.Ordinal) && c.ContainerIndex == laneIndex)
            {
                if (map.TryGetValue(c.Id, out var r))
                    ChildPanes[i] = c with { PreferredSizeRatio = Math.Clamp(r, 0.05, 0.95) };
            }
        }
        RaiseChildPaneCollectionsChanged();
    }
    // Toggle top-right ownership (existing behavior expanded to generalized updater)
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
        OnPropertyChanged(nameof(FloatingChildPanes));
        OnPropertyChanged(nameof(IsFloatingLayerVisible));
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

            // PLAN-061: compute orientation-aware lanes per parent
            // Left/Right → vertical child flow (columns side-by-side); Top/Bottom → horizontal child flow (rows stacked)
            var host = NormalizeHostId(parent.HostId);
            var isVerticalFlow = string.Equals(host, "left", StringComparison.Ordinal) || string.Equals(host, "right", StringComparison.Ordinal);
            var splitCount = Math.Max(1, Math.Min(3, parent.SplitCount));

            // Build lanes 0..splitCount-1 for the current slide; extra container indices are ignored in v1 (future: paging lanes)
            var lanes = new List<LaneView>(capacity: splitCount);
            for (int laneIdx = 0; laneIdx < splitCount; laneIdx++)
            {
                var laneChildren = visibleForParent.Where(c => c.ContainerIndex == laneIdx).ToArray();
                // Compute normalized ratios via helper, then construct with ParentId set
                var laneComputed = LaneView.Create(
                    laneIdx,
                    isVerticalFlow,
                    laneChildren,
                    ratioSelector: c => c.PreferredSizeRatio);
                var laneViewFinal = new LaneView
                {
                    ParentId = parentId,
                    LaneIndex = laneIdx,
                    Children = laneChildren,
                    Ratios = laneComputed.Ratios,
                    IsVerticalFlow = isVerticalFlow,
                    IsVerticalScroll = isVerticalFlow
                };
                lanes.Add(laneViewFinal);
            }
            parent.LanesVisible = lanes;
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
        OnPropertyChanged(nameof(IsFloatingLayerVisible));
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
        UpdateLeftOwnershipLayout();
        UpdateRightOwnershipLayout();
        UpdateTopOwnershipLayout();
        UpdateBottomOwnershipLayout();
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
        var hasBottom = ParentPaneModelsBottom.Count > 0;
        if (!hasTop || !hasRight)
        {
            return;
        }

        _isTopRightCornerOwnedByTop = !_isTopRightCornerOwnedByTop;
        UpdateRightOwnershipLayout();
        UpdateTopOwnershipLayout();
    }

    // Toggle bottom-right ownership
    public void ToggleBottomRightCornerOwnership()
    {
        if (ParentPaneModelsBottom.Count == 0 || ParentPaneModelsRight.Count == 0) return;
        _isBottomRightCornerOwnedByBottom = !_isBottomRightCornerOwnedByBottom;
        UpdateRightOwnershipLayout();
        UpdateBottomOwnershipLayout();
    }

    // Compute Right host vertical placement from top/bottom “cuts”
    private void UpdateRightOwnershipLayout()
    {
        var hasTop = ParentPaneModelsTop.Count > 0;
        var hasRight = ParentPaneModelsRight.Count > 0;
        var hasBottom = ParentPaneModelsBottom.Count > 0;

        if (!hasRight)
        {
            RightPaneRow = 0;
            RightPaneRowSpan = 3;
            return;
        }

        var topCutsRight = hasTop && _isTopRightCornerOwnedByTop;
        var bottomCutsRight = hasBottom && _isBottomRightCornerOwnedByBottom;

        if (topCutsRight && bottomCutsRight)
        {
            RightPaneRow = 1;  // middle only
            RightPaneRowSpan = 1;
        }
        else if (topCutsRight && !bottomCutsRight)
        {
            RightPaneRow = 1;
            RightPaneRowSpan = 2;
        }
        else if (!topCutsRight && bottomCutsRight)
        {
            RightPaneRow = 0;
            RightPaneRowSpan = 2;
        }
        else
        {
            RightPaneRow = 0;
            RightPaneRowSpan = 3;
        }
    }

    // Compute Left host vertical placement from top/bottom “cuts” (generalized former UpdateTopLeftOwnershipLayout)
    private void UpdateLeftOwnershipLayout()
    {
        var hasTop = ParentPaneModelsTop.Count > 0;
        var hasLeft = ParentPaneModelsLeft.Count > 0;
        var hasBottom = ParentPaneModelsBottom.Count > 0;

        if (!hasLeft)
        {
            LeftPaneRow = 0;
            LeftPaneRowSpan = 3;
            return;
        }

        var topCutsLeft = hasTop && _isTopCornerOwnedByTop;
        var bottomCutsLeft = hasBottom && _isBottomLeftCornerOwnedByBottom;

        if (topCutsLeft && bottomCutsLeft)
        {
            LeftPaneRow = 1;  // middle only
            LeftPaneRowSpan = 1;
        }
        else if (topCutsLeft && !bottomCutsLeft)
        {
            LeftPaneRow = 1;
            LeftPaneRowSpan = 2;
        }
        else if (!topCutsLeft && bottomCutsLeft)
        {
            LeftPaneRow = 0;
            LeftPaneRowSpan = 2;
        }
        else
        {
            LeftPaneRow = 0;
            LeftPaneRowSpan = 3;
        }
    }

    // Compute Top host horizontal span from left/right ownership
    private void UpdateTopOwnershipLayout()
    {
        var hasTop = ParentPaneModelsTop.Count > 0;
        var hasLeft = ParentPaneModelsLeft.Count > 0;
        var hasRight = ParentPaneModelsRight.Count > 0;

        if (!hasTop)
        {
            TopPaneColumn = 1;
            TopPaneColumnSpan = 1;
            return;
        }

        var ownsLeft = hasLeft && _isTopCornerOwnedByTop;
        var ownsRight = hasRight && _isTopRightCornerOwnedByTop;

        if (ownsLeft && ownsRight)
        {
            TopPaneColumn = 0;
            TopPaneColumnSpan = 3;
        }
        else if (ownsLeft && !ownsRight)
        {
            TopPaneColumn = 0;
            TopPaneColumnSpan = 2;
        }
        else if (!ownsLeft && ownsRight)
        {
            TopPaneColumn = 1;
            TopPaneColumnSpan = 2;
        }
        else
        {
            TopPaneColumn = 1;
            TopPaneColumnSpan = 1;
        }
    }

    // Compute Bottom host horizontal span from left/right ownership
    private void UpdateBottomOwnershipLayout()
    {
        var hasBottom = ParentPaneModelsBottom.Count > 0;
        var hasLeft = ParentPaneModelsLeft.Count > 0;
        var hasRight = ParentPaneModelsRight.Count > 0;

        if (!hasBottom)
        {
            BottomPaneColumn = 1;
            BottomPaneColumnSpan = 1;
            return;
        }

        var ownsLeft = hasLeft && _isBottomLeftCornerOwnedByBottom;
        var ownsRight = hasRight && _isBottomRightCornerOwnedByBottom;

        if (ownsLeft && ownsRight)
        {
            BottomPaneColumn = 0;
            BottomPaneColumnSpan = 3;
        }
        else if (ownsLeft && !ownsRight)
        {
            BottomPaneColumn = 0;
            BottomPaneColumnSpan = 2;
        }
        else if (!ownsLeft && ownsRight)
        {
            BottomPaneColumn = 1;
            BottomPaneColumnSpan = 2;
        }
        else
        {
            BottomPaneColumn = 1;
            BottomPaneColumnSpan = 1;
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

    // Centralized layout recompute entry remains RaiseParentPaneLayoutChanged; we now call all ownership updaters there.
    // (method continues below)
}
