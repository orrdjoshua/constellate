using Avalonia.Controls;
using Constellate.App.Infrastructure.Panes;
using Constellate.App.Infrastructure.Panes.Gestures;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        // Explicit interaction sessions are now the active source of gesture truth during cutover.
        private ParentPaneMoveSession? _activeParentMoveSession;
        private ParentPaneResizeSession? _activeParentResizeSession;
        private ChildPaneDragSession? _activeChildDragSession;

        // Shell-layout bridge state is routed through a dedicated controller instead
        // of leaving RootGrid lookup/subscription/application details in MainWindow.
        private MainWindowShellLayoutController? _shellLayoutController;
    }
}
