using System;
using System.Linq;
using Avalonia;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing shell-level 2D World layout state:
/// viewport bounds, dock extents, canonical shell layout recomputation, and dock-corner
/// ownership projection helpers. This separates shell geometry/state from pane-body logic.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private double _shellViewportWidth = 1200;
    private double _shellViewportHeight = 760;
    private double _leftDockExtent = 260;
    private double _topDockExtent = 220;
    private double _rightDockExtent = 260;
    private double _bottomDockExtent = 220;
    private WorldShellLayoutResult _currentShellLayout = WorldShellLayoutResult.Empty(new Rect(0, 0, 1200, 760));

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

    public WorldShellLayoutResult CurrentShellLayout
    {
        get => _currentShellLayout;
        private set
        {
            if (Equals(_currentShellLayout, value))
            {
                return;
            }

            _currentShellLayout = value;
            OnPropertyChanged();
        }
    }

    public void UpdateShellViewportBounds(double width, double height)
    {
        var nextWidth = Math.Max(1.0, width);
        var nextHeight = Math.Max(1.0, height);
        if (Math.Abs(_shellViewportWidth - nextWidth) < double.Epsilon &&
            Math.Abs(_shellViewportHeight - nextHeight) < double.Epsilon)
        {
            return;
        }

        _shellViewportWidth = nextWidth;
        _shellViewportHeight = nextHeight;
        RecomputeWorldShellLayout();
    }

    public void UpdateDockExtent(string? edge, double size)
    {
        var clamped = Math.Max(80.0, size);
        var changed = false;

        switch (NormalizeHostId(edge))
        {
            case "left":
                if (Math.Abs(_leftDockExtent - clamped) > double.Epsilon)
                {
                    _leftDockExtent = clamped;
                    changed = true;
                }
                break;
            case "top":
                if (Math.Abs(_topDockExtent - clamped) > double.Epsilon)
                {
                    _topDockExtent = clamped;
                    changed = true;
                }
                break;
            case "right":
                if (Math.Abs(_rightDockExtent - clamped) > double.Epsilon)
                {
                    _rightDockExtent = clamped;
                    changed = true;
                }
                break;
            case "bottom":
                if (Math.Abs(_bottomDockExtent - clamped) > double.Epsilon)
                {
                    _bottomDockExtent = clamped;
                    changed = true;
                }
                break;
        }

        if (changed)
        {
            RecomputeWorldShellLayout();
        }
    }

    private void RecomputeWorldShellLayout()
    {
        var fullBounds = new Rect(0, 0, Math.Max(1.0, _shellViewportWidth), Math.Max(1.0, _shellViewportHeight));
        CurrentShellLayout = WorldShellLayoutEngine.Compute(
            fullBounds,
            ParentPaneModels,
            _leftDockExtent,
            _topDockExtent,
            _rightDockExtent,
            _bottomDockExtent,
            _isTopCornerOwnedByTop,
            _isTopRightCornerOwnedByTop,
            _isBottomLeftCornerOwnedByBottom,
            _isBottomRightCornerOwnedByBottom);

        UpdateLeftOwnershipLayout();
        UpdateRightOwnershipLayout();
        UpdateTopOwnershipLayout();
        UpdateBottomOwnershipLayout();
    }

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
        RecomputeWorldShellLayout();
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
        RecomputeWorldShellLayout();
    }

    // Toggle bottom-left ownership
    public void ToggleBottomLeftCornerOwnership()
    {
        if (ParentPaneModelsBottom.Count == 0 || ParentPaneModelsLeft.Count == 0)
        {
            return;
        }

        _isBottomLeftCornerOwnedByBottom = !_isBottomLeftCornerOwnedByBottom;
        RecomputeWorldShellLayout();
    }

    // Toggle bottom-right ownership
    public void ToggleBottomRightCornerOwnership()
    {
        if (ParentPaneModelsBottom.Count == 0 || ParentPaneModelsRight.Count == 0)
        {
            return;
        }

        _isBottomRightCornerOwnedByBottom = !_isBottomRightCornerOwnedByBottom;
        RecomputeWorldShellLayout();
    }

    // Compute Right host vertical placement from top/bottom “cuts”
    private void UpdateRightOwnershipLayout()
    {
        var rightDock = CurrentShellLayout.RightDock;
        var hasRight = rightDock?.IsVisible ?? false;

        if (!hasRight)
        {
            RightPaneRow = 0;
            RightPaneRowSpan = 3;
            return;
        }

        var topCutsRight = !(rightDock?.OwnsLeadingCorner ?? false);
        var bottomCutsRight = !(rightDock?.OwnsTrailingCorner ?? false);

        if (topCutsRight && bottomCutsRight)
        {
            RightPaneRow = 1;
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
        var leftDock = CurrentShellLayout.LeftDock;
        var hasLeft = leftDock?.IsVisible ?? false;

        if (!hasLeft)
        {
            LeftPaneRow = 0;
            LeftPaneRowSpan = 3;
            return;
        }

        var topCutsLeft = !(leftDock?.OwnsLeadingCorner ?? false);
        var bottomCutsLeft = !(leftDock?.OwnsTrailingCorner ?? false);

        if (topCutsLeft && bottomCutsLeft)
        {
            LeftPaneRow = 1;
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
        var topDock = CurrentShellLayout.TopDock;
        var hasTop = topDock?.IsVisible ?? false;

        if (!hasTop)
        {
            TopPaneColumn = 1;
            TopPaneColumnSpan = 1;
            return;
        }

        var ownsLeft = topDock?.OwnsLeadingCorner ?? false;
        var ownsRight = topDock?.OwnsTrailingCorner ?? false;

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
        var bottomDock = CurrentShellLayout.BottomDock;
        var hasBottom = bottomDock?.IsVisible ?? false;

        if (!hasBottom)
        {
            BottomPaneColumn = 1;
            BottomPaneColumnSpan = 1;
            return;
        }

        var ownsLeft = bottomDock?.OwnsLeadingCorner ?? false;
        var ownsRight = bottomDock?.OwnsTrailingCorner ?? false;

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
}
