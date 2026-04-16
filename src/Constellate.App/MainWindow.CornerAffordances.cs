using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        private void AttachCornerTriangleHandlers()
        {
            // Runtime wiring remains as a safety net. With XAML wiring added,
            // a click may call the same handler twice; that’s harmless here,
            // because the VM’s create/restore early-outs if a pane already exists.
            void Wire(string name)
            {
                var triangle = this.FindControl<Polygon>(name);
                if (triangle is not null)
                {
                    triangle.PointerPressed += CornerTriangle_OnPointerPressed;
                }
            }

            Wire("TopLeft_TopTriangle");
            Wire("TopLeft_LeftTriangle");
            Wire("TopRight_TopTriangle");
            Wire("TopRight_RightTriangle");
            Wire("BottomLeft_LeftTriangle");
            Wire("BottomLeft_BottomTriangle");
            Wire("BottomRight_BottomTriangle");
            Wire("BottomRight_RightTriangle");
        }

        private void CornerTriangle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (DataContext is not MainWindowViewModel vm)
            {
                Debug.WriteLine("[CornerAff] Click but DataContext is not MainWindowViewModel");
                return;
            }

            var hostId = (sender as Control)?.Tag as string ?? string.Empty;
            var name = (sender as Control)?.Name ?? "(unnamed)";
            var beforeCount = vm.ParentPaneModels.Count;

            Debug.WriteLine($"[CornerAff] PointerPressed on {name} tag='{hostId}' (before ParentPaneModels={beforeCount})");

            if (!string.IsNullOrWhiteSpace(hostId) &&
                vm.CreateOrRestoreParentPaneCommand.CanExecute(hostId))
            {
                vm.CreateOrRestoreParentPaneCommand.Execute(hostId);
                e.Handled = true;

                var afterCount = vm.ParentPaneModels.Count;
                Debug.WriteLine($"[CornerAff] Executed CreateOrRestore(host='{hostId}'), vmId={vm.VmId} " +
                                $"after ParentPaneModels={afterCount} | Visible: " +
                                $"L={vm.IsShellPaneOnLeft} T={vm.IsShellPaneOnTop} R={vm.IsShellPaneOnRight} B={vm.IsShellPaneOnBottom} F={vm.IsShellPaneFloating}");
            }
            else
            {
                Debug.WriteLine($"[CornerAff] HostId missing/invalid or command not executable. hostId='{hostId}'");
            }
        }
    }
}
