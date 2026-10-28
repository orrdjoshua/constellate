using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Constellate.App.Worlds;
using Constellate.Core.Messaging;

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
            WorldAutoSaveCoordinator.Initialize(ProjectPathResolver.ResolveProjectRootPath());
            EngineHost.WorldSessionChanged += MainWindow_OnWorldSessionChanged;

            InitializeShellLayoutBridge();
            InitializePaneGestureHost();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void MainWindow_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            var worldOverlay = new WorldOverlayWindow();
            worldOverlay.ShowDialog(this);
            e.Handled = true;
        }

        private void MainWindow_OnWorldSessionChanged()
        {
            DataContext = new MainWindowViewModel();
            InitializeShellLayoutBridge();
            InitializePaneGestureHost();
        }
    }
}
