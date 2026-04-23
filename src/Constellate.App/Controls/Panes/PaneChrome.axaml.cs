using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Input;
using Constellate.App;

namespace Constellate.App.Controls.Panes
{
    public partial class PaneChrome : UserControl
    {
        private ScrollViewer? _headerScroll;
        public static readonly StyledProperty<object?> LabelContentProperty =
            AvaloniaProperty.Register<PaneChrome, object?>(nameof(LabelContent));

        public static readonly StyledProperty<object?> EmptyHeaderContentProperty =
            AvaloniaProperty.Register<PaneChrome, object?>(nameof(EmptyHeaderContent));

        public static readonly StyledProperty<object?> CommandBarContentProperty =
            AvaloniaProperty.Register<PaneChrome, object?>(nameof(CommandBarContent));

        public static readonly StyledProperty<object?> PreBodyContentProperty =
            AvaloniaProperty.Register<PaneChrome, object?>(nameof(PreBodyContent));

        public static readonly StyledProperty<object?> BodyContentProperty =
            AvaloniaProperty.Register<PaneChrome, object?>(nameof(BodyContent));

        public static readonly StyledProperty<bool> IsBodyVisibleProperty =
            AvaloniaProperty.Register<PaneChrome, bool>(nameof(IsBodyVisible), true);

        private Border? _rootBorder;
        private Border? _headerBorder;
        private Control? _labelRegion;
        private const double TrailingSpacerWidth = 8.0; // matches trailing <Border Width="8"/> in XAML
        private Control? _emptyHeaderRegion;
        private Control? _commandBarRegion;
        private Control? _bodyRegion;

        public PaneChrome()
        {
            InitializeComponent();
        }

        public object? LabelContent
        {
            get => GetValue(LabelContentProperty);
            set => SetValue(LabelContentProperty, value);
        }

        public object? EmptyHeaderContent
        {
            get => GetValue(EmptyHeaderContentProperty);
            set => SetValue(EmptyHeaderContentProperty, value);
        }

        public object? CommandBarContent
        {
            get => GetValue(CommandBarContentProperty);
            set => SetValue(CommandBarContentProperty, value);
        }

        public object? PreBodyContent
        {
            get => GetValue(PreBodyContentProperty);
            set => SetValue(PreBodyContentProperty, value);
        }

        public object? BodyContent
        {
            get => GetValue(BodyContentProperty);
            set => SetValue(BodyContentProperty, value);
        }

        public bool IsBodyVisible
        {
            get => GetValue(IsBodyVisibleProperty);
            set => SetValue(IsBodyVisibleProperty, value);
        }

        public Border? RootBorder => _rootBorder;

        public Border? HeaderBorder => _headerBorder;

        public Control? LabelRegionControl => _labelRegion;

        public Control? EmptyHeaderRegionControl => _emptyHeaderRegion;

        public Control? CommandBarRegionControl => _commandBarRegion;

        public Control? BodyRegionControl => _bodyRegion;

        public PaneChromeRegion ResolveRegion(object? sender)
        {
            return PaneChromeInputHelper.ResolveRegion(sender);
        }

        public void SetDragHover(bool isActive)
        {
            if (isActive)
            {
                Classes.Add("dragHover");
            }
            else
            {
                Classes.Remove("dragHover");
            }
        }

        public void SetDragHover(PaneChromeRegion region, bool isActive)
        {
            // For the current pane-shell usage, any region that is wired as a drag
            // origin should light the outer shell border. The higher-level helpers
            // (PaneChromeInputHelper.ResolveRegion / IsDragOrigin) already decide
            // which senders are considered valid drag-start regions.
            //
            // To avoid stale or overly-restrictive region rules from preventing
            // the visual affordance, we unconditionally delegate to SetDragHover.
            SetDragHover(isActive);
        }

        public Control? GetRegionControl(PaneChromeRegion region)
        {
            return region switch
            {
                PaneChromeRegion.Header => _headerBorder,
                PaneChromeRegion.Label => _labelRegion,
                PaneChromeRegion.EmptyHeader => _emptyHeaderRegion,
                PaneChromeRegion.CommandBar => _commandBarRegion,
                PaneChromeRegion.Body => _bodyRegion,
                PaneChromeRegion.MinimizedChrome => _headerBorder,
                _ => _rootBorder
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _rootBorder = this.FindControl<Border>(PaneChromeRegionNames.Root);
            _headerBorder = this.FindControl<Border>(PaneChromeRegionNames.Header);
            _labelRegion = this.FindControl<Control>(PaneChromeRegionNames.Label);
            _headerScroll = this.FindControl<ScrollViewer>("PART_HeaderScroll");
            _emptyHeaderRegion = this.FindControl<Control>(PaneChromeRegionNames.EmptyHeader);
            _commandBarRegion = this.FindControl<Control>(PaneChromeRegionNames.CommandBar);
            _bodyRegion = this.FindControl<Control>(PaneChromeRegionNames.Body);

            // Bring-to-front on hover for any floating pane (parent or child).
            if (_rootBorder is not null)
            {
                _rootBorder.PointerEntered += OnRootPointerEntered;
            }

            // Wire basic change notifications so the center width recomputes when header viewport
            // or child regions change size.
            if (_headerScroll is not null)
            {
                _headerScroll.PropertyChanged += OnAnyHeaderMetricChanged;
            }

            if (_headerBorder is not null)
            {
                _headerBorder.PropertyChanged += OnAnyHeaderMetricChanged;
            }

            if (_labelRegion is not null)
            {
                _labelRegion.PropertyChanged += OnAnyHeaderMetricChanged;
            }

            if (_commandBarRegion is not null)
            {
                _commandBarRegion.PropertyChanged += OnAnyHeaderMetricChanged;
            }

            // First pass after load
            UpdateHeaderCenterWidth();
        }

        private void OnRootPointerEntered(object? sender, PointerEventArgs e)
        {
            var vm = PaneChromeInputHelper.ResolveMainWindowViewModel(this);
            if (vm is null)
                return;

            switch (DataContext)
            {
                case ParentPaneModel parent:
                    if (string.Equals(MainWindowViewModel.NormalizeHostId(parent.HostId), "floating", System.StringComparison.Ordinal))
                    {
                        vm.BringFloatingParentToFront(parent.Id);
                    }
                    break;
                case ChildPaneDescriptor child:
                    if (child.ParentId is null)
                    {
                        vm.BringFloatingChildToFront(child.Id);
                    }
                    break;
            }
        }

        private void OnAnyHeaderMetricChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            // Recompute on any bounds/viewport changes; cheap and safe.
            if (e.Property == BoundsProperty ||
                e.Property == ScrollViewer.ViewportProperty ||
                e.Property == Layoutable.BoundsProperty)
            {
                UpdateHeaderCenterWidth();
            }

            // Fallback: if we cannot reliably compare properties across versions, just recompute.
            // (This executes rarely enough to be fine.)
            else if (e.Property?.Name == "Bounds" || e.Property?.Name == "Viewport")
            {
                UpdateHeaderCenterWidth();
            }
        }

        private void UpdateHeaderCenterWidth()
        {
            if (_emptyHeaderRegion is null)
                return;

            var viewport = _headerScroll?.Viewport.Width ?? 0.0;
            if (viewport <= 0.0)
            {
                _emptyHeaderRegion.Width = 0.0;
                return;
            }

            var labelW = _labelRegion?.Bounds.Width ?? 0.0;
            var cmdW = _commandBarRegion?.Bounds.Width ?? 0.0;
            var center = viewport - (labelW + cmdW + TrailingSpacerWidth);
            _emptyHeaderRegion.Width = center > 0.0 ? center : 0.0;
        }
    }
}
