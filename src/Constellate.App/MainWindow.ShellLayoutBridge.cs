using Avalonia;
using Avalonia.Controls;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App
{
    public partial class MainWindow
    {
        private MainWindowViewModel? GetShellLayoutViewModel()
        {
            return DataContext as MainWindowViewModel;
        }

        private void InitializeShellLayoutBridge()
        {
            _shellLayoutController ??= new MainWindowShellLayoutController(this);
            _shellLayoutController.Initialize(GetShellLayoutViewModel());
        }

        private Rect GetShellFloatingSurfaceRect()
        {
            return _shellLayoutController?.GetFloatingSurfaceRect(GetShellLayoutViewModel())
                ?? new Rect(0, 0, Bounds.Width, Bounds.Height);
        }

        private Rect GetShellHostRect(string? hostId)
        {
            return _shellLayoutController?.GetHostRect(GetShellLayoutViewModel(), hostId)
                ?? new Rect(0, 0, Bounds.Width, Bounds.Height);
        }

        private Grid? GetShellRootGrid()
        {
            return _shellLayoutController?.RootGrid;
        }
    }
}
