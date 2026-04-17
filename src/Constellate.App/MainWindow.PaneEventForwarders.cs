using Avalonia.Controls;
using Avalonia.Input;

namespace Constellate.App
{
    public partial class MainWindow
    {
        private void OnFloatingParentHeaderDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control header || header.DataContext is not ParentPaneModel parent)
            {
                return;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                vm.SetParentPaneMinimized(parent.Id, true);
                e.Handled = true;
            }
        }

        public void ForwardChildHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            OnChildPaneHeaderPointerPressed(sender, e);
        }

        public void ForwardParentHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            OnParentPaneHeaderPointerPressed(sender, e);
        }
    }
}
