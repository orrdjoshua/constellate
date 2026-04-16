using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Constellate.App.Controls
{
    public partial class ChildPaneView : UserControl
    {
        public ChildPaneView()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }


        private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardChildHeaderPointerPressed(sender, e);
            }
        }

        private void Header_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardChildHeaderPointerReleased(sender, e);
            }
        }

        private void Header_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardChildHeaderPointerMoved(sender, e);
            }
        }

        private void EmptyHeader_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not ChildPaneDescriptor child) return;
            if (this.VisualRoot is not MainWindow mw) return;
            if (mw.DataContext is not MainWindowViewModel vm) return;

            var cmd = vm.MinimizeChildPaneCommand;
            if (cmd is not null && cmd.CanExecute(child.Id))
            {
                cmd.Execute(child.Id);
            }
            e.Handled = true;
        }
    }
}
