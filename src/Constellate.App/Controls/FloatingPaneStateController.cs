using Avalonia.Controls;

namespace Constellate.App.Controls
{
    internal static class FloatingPaneStateController
    {
        public static void SetFloatingGeometry(
            Control? owner,
            object? dataContext,
            double x,
            double y,
            double width,
            double height)
        {
            switch (dataContext)
            {
                case ParentPaneModel parent:
                    parent.FloatingX = x;
                    parent.FloatingY = y;
                    parent.FloatingWidth = width;
                    parent.FloatingHeight = height;
                    break;

                case ChildPaneDescriptor child:
                    SetFloatingChildGeometry(owner, child.Id, x, y, width, height);
                    break;
            }
        }

        public static void SetFloatingChildGeometry(
            Control? owner,
            string id,
            double x,
            double y,
            double width,
            double height)
        {
            var vm = ResolveMainWindowViewModel(owner);
            if (vm is null)
            {
                return;
            }

            vm.SetFloatingChildGeometry(id, x, y, width, height);
        }

        public static void BringToFront(
            Control? owner,
            Control chrome,
            IEnumerable<ParentPaneModel>? parents,
            IEnumerable<ChildPaneDescriptor>? children,
            ref int zCounter)
        {
            var nextZIndex = FloatingPaneSurfaceBuilder.GetNextFloatingZIndex(parents, children, ref zCounter);

            switch (chrome.DataContext)
            {
                case ParentPaneModel parent:
                    parent.FloatingZIndex = nextZIndex;
                    break;

                case ChildPaneDescriptor child:
                    var vm = ResolveMainWindowViewModel(owner);
                    if (vm is not null)
                    {
                        vm.SetFloatingChildZIndex(child.Id, nextZIndex);
                    }
                    break;
            }

            try
            {
                chrome.ZIndex = nextZIndex;
            }
            catch
            {
            }
        }

        private static MainWindowViewModel? ResolveMainWindowViewModel(Control? owner)
        {
            if (TopLevel.GetTopLevel(owner) is MainWindow mw)
            {
                return mw.DataContext as MainWindowViewModel;
            }

            return null;
        }
    }
}
