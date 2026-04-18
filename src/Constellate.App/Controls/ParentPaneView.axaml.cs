using Avalonia;
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
            if (PaneChromeInputHelper.TryBeginPressedPaneDrag(this, sender, e))
            {
                e.Handled = true;
            }
        }

        private void EmptyHeader_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            PaneChromeInputHelper.TryHandleEmptyHeaderDoubleTap(this, DataContext, e);
        }

        private void Header_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, true);
        }

        private void Header_OnPointerExited(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, false);
        }

        private void OnPaneChromePointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Route wheel input over the pane shell (typically header region) to the command bar scroll viewer.
            var scroll = this.FindControl<ScrollViewer>("CommandBarScroll");
            if (scroll is null)
            {
                return;
            }

            if (Math.Abs(e.Delta.Y) > 0.01)
            {
                var current = scroll.Offset;
                // Map vertical wheel to horizontal scroll: positive delta.Y (wheel up) scrolls left, negative scrolls right.
                var deltaX = -e.Delta.Y * 40;
                scroll.Offset = new Vector(current.X + deltaX, current.Y);
                e.Handled = true;
            }
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
    }
}
