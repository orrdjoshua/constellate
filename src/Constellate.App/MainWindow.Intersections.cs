using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Constellate.App
{
    public partial class MainWindow
    {
        private void OnTopCornerIntersectionDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                var hit = sender as Control;
                var name = hit?.Name ?? string.Empty;
                if (name.Contains("TopRight", StringComparison.OrdinalIgnoreCase))
                {
                    vm.ToggleTopRightCornerOwnership();
                }
                else
                {
                    vm.ToggleTopCornerOwnership();
                }
            }

            e.Handled = true;
        }

        private void OnBottomCornerIntersectionDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                var hit = sender as Control;
                var name = hit?.Name ?? string.Empty;
                if (name.Contains("BottomRight", StringComparison.OrdinalIgnoreCase))
                {
                    vm.ToggleBottomRightCornerOwnership();
                }
                else
                {
                    vm.ToggleBottomLeftCornerOwnership();
                }
            }

            e.Handled = true;
        }
    }
}
