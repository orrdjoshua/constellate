using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Constellate.App
{
    /// <summary>
    /// Generalized parent-pane entity for the 2D World. HostId is just an attribute
    /// ("left" | "top" | "right" | "bottom" | "floating"); panes can move between
    /// hosts without changing identity.
    /// 
    /// Implements INotifyPropertyChanged so that changes to child lists and layout
    /// attributes are observable by the XAML bindings inside parent-pane templates.
    /// </summary>
    public sealed class ParentPaneModel : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _title = string.Empty;
        private string _hostId = "left";
        private bool _isMinimized;
        private double _floatingX;
        private double _floatingY;
        private double _floatingWidth = 320;
        private double _floatingHeight = 240;
        private int[] _splitCounts = new int[] { 1, 1, 1 }; // per-slide split counts (SlideIndex 0..2)
        private int _slideIndex;
        private IReadOnlyList<ChildPaneDescriptor> _visibleChildrenPrimary0 = Array.Empty<ChildPaneDescriptor>();
        private IReadOnlyList<ChildPaneDescriptor> _visibleChildrenPrimary1 = Array.Empty<ChildPaneDescriptor>();
        private IReadOnlyList<ChildPaneDescriptor> _minimizedChildren = Array.Empty<ChildPaneDescriptor>();
        private IReadOnlyList<LaneView> _lanesVisible = Array.Empty<LaneView>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id
        {
            get => _id;
            init => _id = value ?? string.Empty;
        }

        public string Title
        {
        get => _title;
            set
            {
                if (string.Equals(_title, value, StringComparison.Ordinal))
                {
                    return;
                }

                _title = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Current host for this parent pane: "left" | "top" | "right" | "bottom" | "floating".
        /// </summary>
        public string HostId
        {
            get => _hostId;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? "left"
                    : value.Trim().ToLowerInvariant();

                if (string.Equals(_hostId, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _hostId = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFloating));
                OnPropertyChanged(nameof(IsHostLeftOrRight));
                OnPropertyChanged(nameof(IsHostTopOrBottom));
            }
        }

        /// <summary>
        /// When true, the parent pane is minimized and has no visible footprint on its host.
        /// Corner affordances/taskbars are responsible for restore behavior.
        /// </summary>
        public bool IsMinimized
        {
            get => _isMinimized;
            set
            {
                if (_isMinimized == value)
                {
                    return;
                }

                _isMinimized = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasExpandedContent));
            }
        }

        /// <summary>
        /// Convenience flag for XAML: true when the parent pane should show its
        /// expanded content area (children, taskbar, etc.). When false, only the
        /// header/chrome should be visible (used for minimize-to-header semantics).
        /// </summary>
        public bool HasExpandedContent => !_isMinimized;

        /// <summary>
        /// Convenience: true when HostId == "floating" (case-insensitive).
        /// </summary>
        public bool IsFloating =>
            string.Equals(HostId, "floating", StringComparison.OrdinalIgnoreCase);

        // Floating geometry (used when HostId == "floating").
        public double FloatingX
        {
            get => _floatingX;
            set
            {
                if (Math.Abs(_floatingX - value) < double.Epsilon)
                {
                    return;
                }

                _floatingX = value;
                OnPropertyChanged();
            }
        }

        public double FloatingY
        {
            get => _floatingY;
            set
            {
                if (Math.Abs(_floatingY - value) < double.Epsilon)
                {
                    return;
                }

                _floatingY = value;
                OnPropertyChanged();
            }
        }

        public double FloatingWidth
        {
            get => _floatingWidth;
            set
            {
                if (Math.Abs(_floatingWidth - value) < double.Epsilon)
                {
                    return;
                }

                _floatingWidth = value;
                OnPropertyChanged();
            }
        }

        public double FloatingHeight
        {
            get => _floatingHeight;
            set
            {
                if (Math.Abs(_floatingHeight - value) < double.Epsilon)
                {
                    return;
                }

                _floatingHeight = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Number of internal split columns/rows in the parent’s AdjustableDimension.
        /// For v1.1, this is stored per slide (SlideIndex 0..2) to allow unique split counts per slide.
        /// </summary>
        public int SplitCount
        {
            get
            {
                var idx = Math.Clamp(SlideIndex, 0, 2);
                return Math.Max(1, Math.Min(3, _splitCounts[idx]));
            }
            set
            {
                var idx = Math.Clamp(SlideIndex, 0, 2);
                var clamped = Math.Max(1, Math.Min(3, value));
                if (_splitCounts[idx] == clamped)
                {
                    return;
                }

                _splitCounts[idx] = clamped;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Discrete slide/page index in the parent’s AdjustableDimension (0-based).
        /// </summary>
        public int SlideIndex
        {
            get => _slideIndex;
            set
            {
                // Support 3 slides: indices 0..2
                var clamped = value;
                if (clamped < 0) clamped = 0;
                if (clamped > 2) clamped = 2;
                if (_slideIndex == clamped)
                {
                    return;
                }

                _slideIndex = clamped;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Visible child panes in the first internal container (column/row 0) for this
        /// parent, after filtering by host/slide and minimization. XAML binds directly
        /// to this list so children are scoped per-parent instead of per-host.
        /// </summary>
        public IReadOnlyList<ChildPaneDescriptor> VisibleChildrenPrimary0
        {
            get => _visibleChildrenPrimary0;
            set
            {
                _visibleChildrenPrimary0 = value ?? Array.Empty<ChildPaneDescriptor>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Visible child panes in the second internal container (column/row 1) for this
        /// parent. Hosts that are currently single-column/row will simply leave this
        /// empty until additional splits are configured.
        /// </summary>
        public IReadOnlyList<ChildPaneDescriptor> VisibleChildrenPrimary1
        {
            get => _visibleChildrenPrimary1;
            set
            {
                _visibleChildrenPrimary1 = value ?? Array.Empty<ChildPaneDescriptor>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// All minimized child panes owned by this parent pane, regardless of slide.
        /// Used by per-parent taskbars in the XAML.
        /// </summary>
        public IReadOnlyList<ChildPaneDescriptor> MinimizedChildren
        {
            get => _minimizedChildren;
            set
            {
                _minimizedChildren = value ?? Array.Empty<ChildPaneDescriptor>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Orientation-aware lane set (columns for Left/Right; rows for Top/Bottom) for the current slide.
        /// Computed by the ViewModel and consumed by ParentPaneView.
        /// </summary>
        public IReadOnlyList<LaneView> LanesVisible
        {
            get => _lanesVisible;
            set
            {
                _lanesVisible = value ?? Array.Empty<LaneView>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Convenience flags for XAML templates deciding lane stacking orientation.
        /// </summary>
        public bool IsHostLeftOrRight =>
            string.Equals(HostId, "left", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(HostId, "right", StringComparison.OrdinalIgnoreCase);

        public bool IsHostTopOrBottom =>
            string.Equals(HostId, "top", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(HostId, "bottom", StringComparison.OrdinalIgnoreCase);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName is null)
            {
                return;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Generalized child-pane entity. Children attach to ParentPaneModel via ParentId
    /// and use SplitIndex/SlideIndex for internal layout within a parent.
    /// 
    /// This model is currently used mainly for future-proofing; the active layout
    /// source of truth during migration remains ChildPaneDescriptor in MainWindowViewModel.
    /// </summary>
    public sealed class ChildPaneModel : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _title = string.Empty;
        private string _parentId = string.Empty;
        private int _order;
        private bool _isMinimized;
        private int _splitIndex;
        private int _slideIndex;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id
        {
            get => _id;
            init => _id = value ?? string.Empty;
        }

        public string Title
        {
            get => _title;
            set
            {
                if (string.Equals(_title, value, StringComparison.Ordinal))
                {
                    return;
                }

                _title = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Id of the owning ParentPaneModel. Host is inferred from the parent.
        /// </summary>
        public string ParentId
        {
            get => _parentId;
            set
            {
                if (string.Equals(_parentId, value, StringComparison.Ordinal))
                {
                    return;
                }

                _parentId = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Ordering index within the parent’s current split/slide container.
        /// </summary>
        public int Order
        {
            get => _order;
            set
            {
                if (_order == value)
                {
                    return;
                }

                _order = value;
                OnPropertyChanged();
            }
        }

        public bool IsMinimized
        {
            get => _isMinimized;
            set
            {
                if (_isMinimized == value)
                {
                    return;
                }

                _isMinimized = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Split index within the parent (0..SplitCount-1), e.g. column or row index.
        /// </summary>
        public int SplitIndex
        {
            get => _splitIndex;
            set
            {
                if (_splitIndex == value)
                {
                    return;
                }

                _splitIndex = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Slide/page index within the parent for this child.
        /// </summary>
        public int SlideIndex
        {
            get => _slideIndex;
            set
            {
                var clamped = Math.Max(0, value);
                if (_slideIndex == clamped)
                {
                    return;
                }

                _slideIndex = clamped;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName is null)
            {
                return;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Projection for one visible lane inside a ParentPane:
    /// - LaneIndex: 0..SplitCount-1
    /// - IsVerticalFlow: true for Left/Right (children stacked top→bottom)
    /// - IsHorizontalFlow: true for Top/Bottom (children stacked left→right)
    /// - IsVerticalScroll / IsHorizontalScroll: per-lane scrollbar orientation
    /// - Children: ordered list of child descriptors in this lane (filtered by SlideIndex and ContainerIndex)
    /// - Ratios: normalized (sum≈1.0) proportional sizes along the free dimension for each child (clamped [0.05..0.95], uniform fallback)
    /// </summary>
    public sealed class LaneView
    {
        public string ParentId { get; init; } = string.Empty;
        public int LaneIndex { get; init; }
        public bool IsVerticalFlow { get; init; }
        public bool IsHorizontalFlow => !IsVerticalFlow;
        public bool IsVerticalScroll { get; init; }
        public bool IsHorizontalScroll => !IsVerticalScroll;
        public IReadOnlyList<ChildPaneDescriptor> Children { get; init; } = Array.Empty<ChildPaneDescriptor>();
        public IReadOnlyList<double> Ratios { get; init; } = Array.Empty<double>();

        public static LaneView Create(
            int laneIndex,
            bool isVerticalFlow,
            IEnumerable<ChildPaneDescriptor> children,
            Func<ChildPaneDescriptor, double> ratioSelector)
        {
            var arr = (children ?? Array.Empty<ChildPaneDescriptor>()).ToArray();
            var raw = arr.Select(ratioSelector).Select(r => Math.Clamp(r, 0.05, 0.95)).ToArray();
            double sum = raw.Sum();
            double[] norm;
            if (sum <= 1e-6 || raw.All(d => d <= 0))
            {
                // Fallback to uniform
                var n = Math.Max(1, arr.Length);
                norm = Enumerable.Repeat(1.0 / n, n).ToArray();
            }
            else
            {
                norm = raw.Select(r => r / sum).ToArray();
            }

            return new LaneView
            {
                ParentId = string.Empty, // will be set by VM when creating lanes
                LaneIndex = laneIndex,
                IsVerticalFlow = isVerticalFlow,
                IsVerticalScroll = isVerticalFlow, // vertical flow → vertical scroll; horizontal flow → horizontal scroll
                Children = arr,
                Ratios = norm
            };
        }
    }
}
