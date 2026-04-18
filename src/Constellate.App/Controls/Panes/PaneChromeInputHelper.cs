using Avalonia.Controls;
using Avalonia.Input;

namespace Constellate.App.Controls.Panes
{
    internal static class PaneChromeInputHelper
    {
        public static PaneChromeRegion ResolveRegion(object? sender)
        {
            return sender is Control control
                ? ResolveRegion(control.Name)
                : PaneChromeRegion.None;
        }

        public static PaneChromeRegion ResolveRegion(string? controlName)
        {
            return controlName switch
            {
                "ParentLabelArea" or "ChildLabelArea" => PaneChromeRegion.Label,
                "ParentEmptyHeaderArea" or "ChildEmptyHeaderArea" => PaneChromeRegion.EmptyHeader,
                "ChildBodyDragArea" => PaneChromeRegion.BodyEmptySurface,
                _ => PaneChromeRegion.None
            };
        }

        public static MainWindow? ResolveMainWindow(Control? owner)
        {
            return TopLevel.GetTopLevel(owner) as MainWindow;
        }

        public static MainWindowViewModel? ResolveMainWindowViewModel(Control? owner)
        {
            return ResolveMainWindow(owner)?.DataContext as MainWindowViewModel;
        }

        public static void SetPaneDragHover(PaneChrome? chrome, object? sender, bool isActive)
        {
            if (chrome is null)
            {
                return;
            }

            var region = chrome.ResolveRegion(sender);
            chrome.SetDragHover(region, isActive);
        }

        public static bool TryHandleEmptyHeaderDoubleTap(
            Control? owner,
            object? paneDataContext,
            TappedEventArgs e)
        {
            var vm = ResolveMainWindowViewModel(owner);
            if (vm is null)
            {
                return false;
            }

            switch (paneDataContext)
            {
                case ParentPaneModel parent:
                    vm.SetParentPaneMinimized(parent.Id, true);
                    e.Handled = true;
                    return true;

                case ChildPaneDescriptor child:
                    var cmd = vm.MinimizeChildPaneCommand;
                    if (cmd is null || !cmd.CanExecute(child.Id))
                    {
                        return false;
                    }

                    cmd.Execute(child.Id);
                    e.Handled = true;
                    return true;

                default:
                    return false;
            }
        }

        public static bool TryBeginPressedPaneDrag(
            Control? owner,
            object? sender,
            PointerPressedEventArgs e)
        {
            var mainWindow = ResolveMainWindow(owner);
            if (mainWindow is null)
            {
                return false;
            }

            var region = ResolveRegion(sender);
            if (!PaneChromeRegionRules.IsDragOrigin(region))
            {
                return false;
            }

            var paneDataContext =
                (sender as Control)?.DataContext ??
                owner?.DataContext;

            return mainWindow.TryBeginPressedPaneDrag(sender, paneDataContext, e);
        }
    }
}
