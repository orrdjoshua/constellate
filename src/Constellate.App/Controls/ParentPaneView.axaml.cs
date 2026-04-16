using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;

namespace Constellate.App.Controls
{
    public partial class ParentPaneView : UserControl
    {
        public ParentPaneView()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // Drag begin from Label or Empty Header -> forward to MainWindow parent-header handlers
        private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardParentHeaderPointerPressed(sender, e);
            }
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
    }
}
