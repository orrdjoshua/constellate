using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Constellate.App.Controls.Panes;

namespace Constellate.App.Controls
{
    public partial class ChildPaneView : UserControl
    {
        private PaneChrome? _root;

        public ChildPaneView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _root = this.FindControl<PaneChrome>("ChildChrome");
        }

        private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardChildHeaderPointerPressed(sender, e);
            }

            e.Handled = true;
        }

        private void EmptyHeader_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not ChildPaneDescriptor child)
            {
                return;
            }

            if (this.VisualRoot is not MainWindow mw)
            {
                return;
            }

            if (mw.DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var cmd = vm.MinimizeChildPaneCommand;
            if (cmd is not null && cmd.CanExecute(child.Id))
            {
                cmd.Execute(child.Id);
            }

            e.Handled = true;
        }

        private void Body_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            Header_OnPointerPressed(sender, e);
        }

        private void Header_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            SetDragHoverForSender(sender, true);
        }

        private void Header_OnPointerExited(object? sender, PointerEventArgs e)
        {
            SetDragHoverForSender(sender, false);
        }

        private void Body_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            SetDragHoverForSender(sender, true);
        }

        private void Body_OnPointerExited(object? sender, PointerEventArgs e)
        {
            SetDragHoverForSender(sender, false);
        }

        private void SetDragHoverForSender(object? sender, bool isActive)
        {
            if (_root is null)
            {
                return;
            }

            var region = _root.ResolveRegion(sender);
            _root.SetDragHover(region, isActive);
        }
    }
}
