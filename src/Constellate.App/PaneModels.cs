using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private int _splitCount = 1;
        private int _slideIndex;
        private IReadOnlyList<ChildPaneDescriptor> _visibleChildrenPrimary0 = Array.Empty<ChildPaneDescriptor>();
        private IReadOnlyList<ChildPaneDescriptor> _visibleChildrenPrimary1 = Array.Empty<ChildPaneDescriptor>();
        private IReadOnlyList<ChildPaneDescriptor> _minimizedChildren = Array.Empty<ChildPaneDescriptor>();

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
            }
        }

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
        /// Currently 1 or 2 for v1; capped higher in future if needed.
        /// </summary>
        public int SplitCount
        {
            get => _splitCount;
            set
            {
                var clamped = Math.Max(1, value);
                if (_splitCount == clamped)
                {
                    return;
                }

                _splitCount = clamped;
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
                var clamped = Math.Max(0, value);
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
}
