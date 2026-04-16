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

        // Header (Label/Empty Header) — start drag
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

        // Double-tap empty header => minimize this child
        private void EmptyHeader_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not ChildPaneDescriptor child)
            {
                e.Handled = true;
                return;
            }

            if (this.VisualRoot is not MainWindow mw || mw.DataContext is not MainWindowViewModel vm)
            {
                e.Handled = true;
                return;
            }

            var cmd = vm.MinimizeChildPaneCommand;
            if (cmd is not null && cmd.CanExecute(child.Id))
            {
                cmd.Execute(child.Id);
            }

            e.Handled = true;
        }

        // Body — allow drag begin from empty content area
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

        // Hover affordances (bright outline when drag could begin)
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
    }
}
