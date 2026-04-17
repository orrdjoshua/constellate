using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Constellate.App.Infrastructure.Panes.Floating;

namespace Constellate.App.Controls
{
    internal static class FloatingPaneChromeFactory
    {
        public static Border? CreateChromeForEntry(
            FloatingPaneSurfaceEntry entry,
            IEnumerable<ParentPaneModel>? parents,
            IEnumerable<ChildPaneDescriptor>? children,
            Action<Control> bringToFront,
            Action<Border, bool, ParentPaneModel?, ChildPaneDescriptor?> attachResizeGrips)
        {
            return entry.Kind switch
            {
                FloatingPaneSurfaceEntryKind.Parent => CreateParentChrome(entry.PaneId, parents, bringToFront, attachResizeGrips),
                FloatingPaneSurfaceEntryKind.Child => CreateChildChrome(entry.PaneId, children, bringToFront, attachResizeGrips),
                _ => null
            };
        }

        public static void UpdateChromeDataContext(
            Border chrome,
            FloatingPaneSurfaceEntry entry,
            IEnumerable<ParentPaneModel>? parents,
            IEnumerable<ChildPaneDescriptor>? children)
        {
            object? dataContext = entry.Kind switch
            {
                FloatingPaneSurfaceEntryKind.Parent => FindFloatingParentById(parents, entry.PaneId),
                FloatingPaneSurfaceEntryKind.Child => FindFloatingChildById(children, entry.PaneId),
                _ => null
            };

            if (dataContext is null)
            {
                return;
            }

            chrome.DataContext = dataContext;

            if (chrome.Child is Grid grid)
            {
                foreach (var parentView in grid.Children.OfType<ParentPaneView>())
                {
                    parentView.DataContext = dataContext;
                }

                foreach (var childView in grid.Children.OfType<ChildPaneView>())
                {
                    childView.DataContext = dataContext;
                }
            }
        }

        public static void ApplySurfaceEntry(Border chrome, FloatingPaneSurfaceEntry entry)
        {
            chrome.Width = Math.Max(80, entry.Bounds.Width);
            chrome.Height = Math.Max(80, entry.Bounds.Height);

            Canvas.SetLeft(chrome, Math.Max(0, entry.Bounds.X));
            Canvas.SetTop(chrome, Math.Max(0, entry.Bounds.Y));

            try { chrome.ZIndex = entry.ZIndex; } catch { }
        }

        private static Border? CreateParentChrome(
            string paneId,
            IEnumerable<ParentPaneModel>? parents,
            Action<Control> bringToFront,
            Action<Border, bool, ParentPaneModel?, ChildPaneDescriptor?> attachResizeGrips)
        {
            var parent = FindFloatingParentById(parents, paneId);
            if (parent is null)
            {
                return null;
            }

            var chrome = CreateChromeShell(parent);
            var grid = new Grid();
            grid.Children.Add(new ParentPaneView { DataContext = parent });
            chrome.Child = grid;
            attachResizeGrips(chrome, true, parent, null);
            chrome.PointerPressed += (_, __) => bringToFront(chrome);
            return chrome;
        }

        private static Border? CreateChildChrome(
            string paneId,
            IEnumerable<ChildPaneDescriptor>? children,
            Action<Control> bringToFront,
            Action<Border, bool, ParentPaneModel?, ChildPaneDescriptor?> attachResizeGrips)
        {
            var child = FindFloatingChildById(children, paneId);
            if (child is null)
            {
                return null;
            }

            var chrome = CreateChromeShell(child);
            var grid = new Grid();
            grid.Children.Add(new ChildPaneView { DataContext = child });
            chrome.Child = grid;
            attachResizeGrips(chrome, false, null, child);
            chrome.PointerPressed += (_, __) => bringToFront(chrome);
            return chrome;
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

        private static ParentPaneModel? FindFloatingParentById(IEnumerable<ParentPaneModel>? parents, string paneId)
        {
            return (parents ?? Enumerable.Empty<ParentPaneModel>())
                .FirstOrDefault(parent =>
                    string.Equals(parent.Id, paneId, StringComparison.Ordinal) &&
                    string.Equals(parent.HostId, "floating", StringComparison.OrdinalIgnoreCase));
        }

        private static ChildPaneDescriptor? FindFloatingChildById(IEnumerable<ChildPaneDescriptor>? children, string paneId)
        {
            return (children ?? Enumerable.Empty<ChildPaneDescriptor>())
                .FirstOrDefault(child =>
                    string.Equals(child.Id, paneId, StringComparison.Ordinal) &&
                    child.ParentId is null);
        }
    }
}
