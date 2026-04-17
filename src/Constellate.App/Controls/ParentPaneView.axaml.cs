using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Constellate.App.Controls.Panes;

namespace Constellate.App.Controls
{
    public partial class ParentPaneView : UserControl
    {
        private ScrollViewer? _commandBarScroll;
        private PaneChrome? _root;

        public ParentPaneView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _commandBarScroll = this.FindControl<ScrollViewer>("CommandBarScroll");
            _root = this.FindControl<PaneChrome>("ParentChrome");
        }

        private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardParentHeaderPointerPressed(sender, e);
            }

            e.Handled = true;
        }

        private void EmptyHeader_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is ParentPaneModel parent &&
                this.VisualRoot is MainWindow mw &&
                mw.DataContext is MainWindowViewModel vm)
            {
                vm.SetParentPaneMinimized(parent.Id, true);
                e.Handled = true;
            }
        }

        private void Header_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            SetDragHoverForSender(sender, true);
        }

        private void Header_OnPointerExited(object? sender, PointerEventArgs e)
        {
            SetDragHoverForSender(sender, false);
        }

        private void OnCommandBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_commandBarScroll is null)
            {
                return;
            }

            var dx = -e.Delta.Y * 24.0;
            _commandBarScroll.Offset = new Vector(_commandBarScroll.Offset.X + dx, 0);
            e.Handled = true;
        }

        private void SetDragHoverForSender(object? sender, bool isActive)
        {
            if (_root is null)
            {
                return;
            }

            var region = _root.ResolveRegion(sender);
            _root.SetDragHover(region, isActive);
        }
    }
}
