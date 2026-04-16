using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Constellate.App.Controls
{
    public partial class ChildPaneView : UserControl
    {
        private Border? _root;
        public ChildPaneView()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _root = this.FindControl<Border>("ChildRoot");
        }


        private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardChildHeaderPointerPressed(sender, e);
            }
            e.Handled = true;
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

        // Forward content (body) presses for child drag begin
        private void Body_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            Header_OnPointerPressed(sender, e);
        }
        private void Body_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardChildHeaderPointerReleased(sender, e);
            }
            e.Handled = true;
        }
        private void Body_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (this.VisualRoot is MainWindow mw)
            {
                mw.ForwardChildHeaderPointerMoved(sender, e);
            }
        }

        // Bright whole-pane outline while hovering potential drag-start regions
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
        private void Body_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (_root is null) return;
            _root.Classes.Add("dragHover");
        }
        private void Body_OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (_root is null) return;
            _root.Classes.Remove("dragHover");
        }
                cmd.Execute(child.Id);
            }
            e.Handled = true;
        }
    }
}
