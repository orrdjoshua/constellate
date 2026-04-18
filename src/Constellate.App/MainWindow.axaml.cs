using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Constellate.App
{
    // Thin MainWindow code-behind: constructor and top-level bridge initialization only.
    // Shared shell/gesture state now lives in MainWindow.SharedState.cs.
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
