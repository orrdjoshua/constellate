using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace Constellate.App.Infrastructure.Panes.Floating
{
    public enum FloatingPaneSurfaceEntryKind
    {
        Parent = 0,
        Child = 1
    }

    public sealed record FloatingPaneSurfaceEntry(
        string PaneId,
        FloatingPaneSurfaceEntryKind Kind,
        Rect Bounds,
        int ZIndex,
        bool IsMinimized)
    {
        public string SurfaceKey => $"{Kind}:{PaneId}";
    }

    public sealed class FloatingPaneSurfaceModel
    {
        public FloatingPaneSurfaceModel(IEnumerable<FloatingPaneSurfaceEntry>? entries = null)
        {
            Entries = (entries ?? Array.Empty<FloatingPaneSurfaceEntry>())
                .OrderBy(entry => entry.ZIndex)
                .ThenBy(entry => entry.PaneId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<FloatingPaneSurfaceEntry> Entries { get; }

        public bool HasEntries => Entries.Count > 0;

        public FloatingPaneSurfaceEntry? FindByPaneId(string paneId)
        {
            return Entries.FirstOrDefault(entry =>
                string.Equals(entry.PaneId, paneId, StringComparison.Ordinal));
        }

        public int GetNextZIndex()
        {
            return Entries.Count == 0
                ? 1
                : Entries.Max(entry => entry.ZIndex) + 1;
        }

        public FloatingPaneSurfaceModel BringToFront(string paneId)
        {
            var nextZIndex = GetNextZIndex();
            var updatedEntries = Entries
                .Select(entry => string.Equals(entry.PaneId, paneId, StringComparison.Ordinal)
                    ? entry with { ZIndex = nextZIndex }
                    : entry)
                .ToArray();

            return new FloatingPaneSurfaceModel(updatedEntries);
        }
    }
}
