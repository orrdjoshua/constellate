using Avalonia.Controls;
using Constellate.App.Infrastructure.Panes.Floating;

namespace Constellate.App.Controls
{
    internal static class FloatingPaneContentFactory
    {
        public static Grid? CreateChromeContent(FloatingPaneSurfaceEntry entry)
        {
            var content = CreateContent(entry);
            if (content is null)
            {
                return null;
            }

            var grid = new Grid();
            grid.Children.Add(content);
            return grid;
        }

        public static void UpdateChromeContent(
            Border chrome,
            FloatingPaneSurfaceEntry entry)
        {
            if (chrome.Child is not Grid grid)
            {
                var rebuiltGrid = CreateChromeContent(entry);
                if (rebuiltGrid is not null)
                {
                    chrome.Child = rebuiltGrid;
                }

                return;
            }

            var existingContent = grid.Children.Count > 0
                ? grid.Children[0] as Control
                : null;

            if (existingContent is null || !MatchesEntryKind(existingContent, entry.Kind))
            {
                var rebuiltContent = CreateContent(entry);
                if (rebuiltContent is null)
                {
                    return;
                }

                grid.Children.Clear();
                grid.Children.Add(rebuiltContent);
                return;
            }

            existingContent.DataContext = entry.DataContext;
        }

        private static Control? CreateContent(FloatingPaneSurfaceEntry entry)
        {
            return entry switch
            {
                { Kind: FloatingPaneSurfaceEntryKind.Parent, DataContext: ParentPaneModel parent } =>
                    new ParentPaneView { DataContext = parent },
                { Kind: FloatingPaneSurfaceEntryKind.Child, DataContext: ChildPaneDescriptor child } =>
                    new ChildPaneView { DataContext = child },
                _ => null
            };
        }

        private static bool MatchesEntryKind(
            Control content,
            FloatingPaneSurfaceEntryKind kind)
        {
            return kind switch
            {
                FloatingPaneSurfaceEntryKind.Parent => content is ParentPaneView,
                FloatingPaneSurfaceEntryKind.Child => content is ChildPaneView,
                _ => false
            };
        }
    }
}
