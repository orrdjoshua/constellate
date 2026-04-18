using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Constellate.App.Infrastructure.Panes.Floating;

namespace Constellate.App.Controls
{
    internal static class FloatingPaneSurfaceBuilder
    {
        public static FloatingPaneSurfaceModel BuildSurfaceModel(
            IEnumerable<ParentPaneModel>? parents,
            IEnumerable<ChildPaneDescriptor>? children,
            ref int zCounter)
        {
            var entries = new List<FloatingPaneSurfaceEntry>();
            var nextZ = 1;

            foreach (var child in children ?? Enumerable.Empty<ChildPaneDescriptor>())
            {
                if (child.ParentId is not null)
                {
                    continue;
                }

                var zIndex = ResolveSurfaceEntryZIndex(child.FloatingZIndex, ref nextZ);

                entries.Add(new FloatingPaneSurfaceEntry(
                    PaneId: child.Id,
                    Kind: FloatingPaneSurfaceEntryKind.Child,
                    Bounds: new Rect(
                        Math.Max(0, child.FloatingX),
                        Math.Max(0, child.FloatingY),
                        Math.Max(80, child.FloatingWidth),
                        Math.Max(80, child.FloatingHeight)),
                    ZIndex: zIndex,
                    IsMinimized: child.IsMinimized,
                    DataContext: child));
            }

            foreach (var parent in parents ?? Enumerable.Empty<ParentPaneModel>())
            {
                if (!string.Equals(parent.HostId, "floating", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var zIndex = ResolveSurfaceEntryZIndex(parent.FloatingZIndex, ref nextZ);

                entries.Add(new FloatingPaneSurfaceEntry(
                    PaneId: parent.Id,
                    Kind: FloatingPaneSurfaceEntryKind.Parent,
                    Bounds: new Rect(
                        Math.Max(0, parent.FloatingX),
                        Math.Max(0, parent.FloatingY),
                        Math.Max(80, parent.FloatingWidth),
                        Math.Max(80, parent.FloatingHeight)),
                    ZIndex: zIndex,
                    IsMinimized: parent.IsMinimized,
                    DataContext: parent));
            }

            zCounter = Math.Max(zCounter, nextZ);
            return new FloatingPaneSurfaceModel(entries);
        }

        public static int GetNextFloatingZIndex(
            IEnumerable<ParentPaneModel>? parents,
            IEnumerable<ChildPaneDescriptor>? children,
            ref int zCounter)
        {
            var maxParentZIndex = (parents ?? Enumerable.Empty<ParentPaneModel>())
                .Where(parent => string.Equals(parent.HostId, "floating", StringComparison.OrdinalIgnoreCase))
                .Select(parent => parent.FloatingZIndex)
                .DefaultIfEmpty(0)
                .Max();

            var maxChildZIndex = (children ?? Enumerable.Empty<ChildPaneDescriptor>())
                .Where(child => child.ParentId is null)
                .Select(child => child.FloatingZIndex)
                .DefaultIfEmpty(0)
                .Max();

            var nextZIndex = Math.Max(zCounter, Math.Max(maxParentZIndex, maxChildZIndex) + 1);
            zCounter = nextZIndex + 1;
            return nextZIndex;
        }

        private static int ResolveSurfaceEntryZIndex(int storedZIndex, ref int nextZ)
        {
            var resolvedZIndex = storedZIndex > 0 ? storedZIndex : nextZ;
            nextZ = Math.Max(nextZ, resolvedZIndex + 1);
            return resolvedZIndex;
        }
    }
}
