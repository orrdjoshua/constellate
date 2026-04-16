using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;

namespace Constellate.App.Controls
{
    public partial class ParentPaneView : UserControl
    {
        private ScrollViewer? _commandBarScroll;
        private Border? _root;
        public ParentPaneView()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _commandBarScroll = this.FindControl<ScrollViewer>("CommandBarScroll");
            _root = this.FindControl<Border>("ParentRoot");
        }

        // Drag begin from Label or Empty Header -> forward to MainWindow parent-header handlers
        private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardParentHeaderPointerPressed(sender, e);
            }
            e.Handled = true;
        }

        private void Header_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardParentHeaderPointerReleased(sender, e);
            }
        }

        private void Header_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardParentHeaderPointerMoved(sender, e);
            }
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

        // Bright drag-hover outline when cursor is in a drag-start region
        private void Header_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (_root is null) return;
            _root.Classes.Add("dragHover");
        }
        private void Header_OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (_root is null) return;
            _root.Classes.Remove("dragHover");
        }

        // Translate mouse wheel to horizontal scroll on CommandBar
        private void OnCommandBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_commandBarScroll is null) return;
            var dx = -e.Delta.Y * 24.0; // wheel down → scroll right
            _commandBarScroll.Offset = new Vector(_commandBarScroll.Offset.X + dx, 0);
            e.Handled = true;
        }
    }
}
