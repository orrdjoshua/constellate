using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Constellate.App.Infrastructure.Panes.Floating;

namespace Constellate.App.Controls
{
    internal static class FloatingPaneChromeFactory
    {
        public static Border? CreateChromeForEntry(FloatingPaneSurfaceEntry entry)
        {
            var content = FloatingPaneContentFactory.CreateChromeContent(entry);
            if (content is null)
            {
                return null;
            }

            var chrome = CreateChromeShell(entry.DataContext);
            chrome.Child = content;
            return chrome;
        }

        public static void UpdateChromeDataContext(
            Border chrome,
            FloatingPaneSurfaceEntry entry)
        {
            chrome.DataContext = entry.DataContext;
            FloatingPaneContentFactory.UpdateChromeContent(chrome, entry);
        }

        public static void ApplySurfaceEntry(Border chrome, FloatingPaneSurfaceEntry entry)
        {
            chrome.Width = Math.Max(80, entry.Bounds.Width);
            chrome.Height = Math.Max(80, entry.Bounds.Height);

            Canvas.SetLeft(chrome, Math.Max(0, entry.Bounds.X));
            Canvas.SetTop(chrome, Math.Max(0, entry.Bounds.Y));

            try
            {
                chrome.ZIndex = entry.ZIndex;
            }
            catch
            {
            }
        }

        private static Border CreateChromeShell(object dataContext)
        {
            return new Border
            {
                DataContext = dataContext,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }
    }
}
