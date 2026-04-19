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
                // Child body (explicit drag area in ChildPaneView)
                "ChildBodyDragArea" => PaneChromeRegion.BodyEmptySurface,
                // Parent/Child body presenter region
                "PART_BodyRegion" => PaneChromeRegion.BodyEmptySurface,
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

        // Single rule gate for both hover and press: only these regions advertise “drag to reposition”.
        // Resize grips keep their own visual affordances (edges/corners) and are not handled here.
        private static bool IsDragOriginRegion(PaneChromeRegion region)
        {
            return region is PaneChromeRegion.Label
                or PaneChromeRegion.EmptyHeader
                or PaneChromeRegion.BodyEmptySurface;
        }

        public static void SetPaneDragHover(PaneChrome? chrome, object? sender, bool isActive)
        {
            if (chrome is null)
            {
                return;
            }

            var region = chrome.ResolveRegion(sender);
            SetPaneDragHover(chrome, region, isActive);
        }

        public static void SetPaneDragHover(PaneChrome? chrome, PaneChromeRegion region, bool isActive)
        {
            if (chrome is null)
            {
                return;
            }

            if (!IsDragOriginRegion(region))
            {
                // Ensure we clear any stale halo if a non-origin region is reported “active”.
                if (!isActive)
                {
                    chrome.SetDragHover(false);
                }
                else
                {
                    // Do not light the halo for non-origin regions.
                    chrome.SetDragHover(false);
                }
                return;
            }

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
            if (!IsDragOriginRegion(region))
            {
                return false;
            }

            // Prefer the pane view's DataContext (owner) to avoid ambiguity from nested
            // content presenters inside the body/header. This ensures we always start
            // a session for the correct ParentPane/ChildPane when dragging from empty body.
            var paneDataContext =
                owner?.DataContext ??
                (sender as Control)?.DataContext;

            return mainWindow.TryBeginPressedPaneDrag(sender, paneDataContext, e);
        }
    }
}
