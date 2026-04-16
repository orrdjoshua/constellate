using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        private void CornerTriangle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var hostId = (sender as Control)?.Tag as string ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(hostId) &&
                vm.CreateOrRestoreParentPaneCommand.CanExecute(hostId))
            {
                vm.CreateOrRestoreParentPaneCommand.Execute(hostId);
                e.Handled = true;
            }
        }
    }
}
