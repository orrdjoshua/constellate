using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Constellate.App.Controls.Panes
{
    public partial class PaneChrome : UserControl
    {
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
            if (!PaneChromeRegionRules.SupportsDragHover(region))
            {
                if (!isActive)
                {
                    SetDragHover(false);
                }

                return;
            }

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
            _emptyHeaderRegion = this.FindControl<Control>(PaneChromeRegionNames.EmptyHeader);
            _commandBarRegion = this.FindControl<Control>(PaneChromeRegionNames.CommandBar);
            _bodyRegion = this.FindControl<Control>(PaneChromeRegionNames.Body);
        }
    }
}
