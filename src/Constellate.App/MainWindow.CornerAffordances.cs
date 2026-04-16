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
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            if (sender is not Control triangle || triangle.Tag is not string hostId || string.IsNullOrWhiteSpace(hostId))
            {
                return;
            }

            if (vm.CreateOrRestoreParentPaneCommand.CanExecute(hostId))
            {
                vm.CreateOrRestoreParentPaneCommand.Execute(hostId);
                e.Handled = true;
            }
        }
    }
}
