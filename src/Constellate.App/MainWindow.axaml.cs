using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Constellate.App
{
    // Commentary/parity note:
    // This code-behind is intentionally a thin bootstrap seam for top-level shell initialization.
    // Shared shell/gesture state now lives in the companion partials, and future convergence
    // should preserve that direction rather than letting MainWindow.cs regain broader product
    // or shell-policy ownership.
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();

            InitializeShellLayoutBridge();
            InitializePaneGestureHost();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
