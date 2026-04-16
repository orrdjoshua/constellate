using Avalonia.Controls;
using Avalonia.Input;

namespace Constellate.App.Controls
{
    public partial class ChildPaneView : UserControl
    {
        public ChildPaneView()
        {
            InitializeComponent();
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
    }
}
