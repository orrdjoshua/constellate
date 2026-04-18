using Avalonia.Controls;
using Avalonia.Input;
using Constellate.App;

namespace Constellate.App
{
    /// <summary>
    /// MainWindow partial containing corner affordance handlers:
    /// - Corner triangles (create/restore parent panes on a host),
    /// - Corner intersection hit regions (flip dock corner ownership).
    ///
    /// Corner ownership flipping is routed through the unified
    /// MainWindowViewModel.HandleCornerIntersection(ShellCorner) method,
    /// so all four corners share one behavior center.
    /// </summary>
    public partial class MainWindow : Window
    {
        private void CornerTriangle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            if (sender is not Control control)
            {
                return;
            }

            // We only care about left-clicks for create/restore behavior.
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            var hostId = control.Tag as string;
            if (string.IsNullOrWhiteSpace(hostId))
            {
                return;
            }

            var command = vm.CreateOrRestoreParentPaneCommand;
            if (command is null || !command.CanExecute(hostId))
            {
                return;
            }

            command.Execute(hostId);
            e.Handled = true;
        }

        private void OnTopCornerIntersectionDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            if (sender is not Control control)
            {
                return;
            }

            var name = control.Name;
            MainWindowViewModel.ShellCorner corner;

            if (string.Equals(name, "TopLeftIntersectionHit", System.StringComparison.Ordinal))
            {
                corner = MainWindowViewModel.ShellCorner.TopLeft;
            }
            else if (string.Equals(name, "TopRightIntersectionHit", System.StringComparison.Ordinal))
            {
                corner = MainWindowViewModel.ShellCorner.TopRight;
            }
            else
            {
                // Unknown sender; do nothing.
                return;
            }

            vm.HandleCornerIntersection(corner);
            e.Handled = true;
        }

        private void OnBottomCornerIntersectionDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            if (sender is not Control control)
            {
                return;
            }

            var name = control.Name;
            MainWindowViewModel.ShellCorner corner;

            if (string.Equals(name, "BottomLeftIntersectionHit", System.StringComparison.Ordinal))
            {
                corner = MainWindowViewModel.ShellCorner.BottomLeft;
            }
            else if (string.Equals(name, "BottomRightIntersectionHit", System.StringComparison.Ordinal))
            {
                corner = MainWindowViewModel.ShellCorner.BottomRight;
            }
            else
            {
                // Unknown sender; do nothing.
                return;
            }

            vm.HandleCornerIntersection(corner);
            e.Handled = true;
        }
    }
}
