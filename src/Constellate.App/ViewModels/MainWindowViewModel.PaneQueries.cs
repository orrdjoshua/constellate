using System;
using System.Linq;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing host normalization and pane lookup/query helpers.
/// This keeps common read/query behavior separate from mutation-oriented partials such as
/// host-state, shell-layout state, and pane-placement state.
/// </summary>
public sealed partial class MainWindowViewModel
{
    internal static string NormalizeHostId(string? hostId)
    {
        if (string.IsNullOrWhiteSpace(hostId))
        {
            return "left";
        }

        var normalized = hostId.Trim().ToLowerInvariant();
        return normalized is "left" or "top" or "right" or "bottom" or "floating"
            ? normalized
            : "left";
    }

    /// <summary>
    /// Returns the first expanded parent pane hosted on the given host (left/top/right/bottom),
    /// or null if none exists. Normalizes host id.
    /// </summary>
    public ParentPaneModel? GetFirstExpandedParentOnHost(string hostId)
    {
        var normalized = NormalizeHostId(hostId);
        return ParentPaneModels.FirstOrDefault(p =>
            !p.IsMinimized &&
            string.Equals(NormalizeHostId(p.HostId), normalized, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns the normalized host id for the parent currently owning the specified child pane.
    /// </summary>
    public string GetHostIdForChildPane(string childPaneId)
    {
        if (string.IsNullOrWhiteSpace(childPaneId))
        {
            return "left";
        }

        var child = ChildPanes.FirstOrDefault(pane =>
            string.Equals(pane.Id, childPaneId, StringComparison.Ordinal));
        if (child is null || string.IsNullOrWhiteSpace(child.ParentId))
        {
            return "left";
        }

        var parent = ParentPaneModels.FirstOrDefault(pane =>
            string.Equals(pane.Id, child.ParentId, StringComparison.Ordinal));

        return parent is null ? "left" : NormalizeHostId(parent.HostId);
    }

    /// <summary>
    /// Returns the count of non-minimized children in the specified lane for the parent's current SlideIndex.
    /// </summary>
    public int GetChildrenCountInLaneForCurrentSlide(string parentId, int laneIndex)
    {
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null)
        {
            return 0;
        }

        var slideIndex = parent.SlideIndex;
        return ChildPanes.Count(c =>
            !c.IsMinimized &&
            string.Equals(c.ParentId, parentId, StringComparison.Ordinal) &&
            c.SlideIndex == slideIndex &&
            c.ContainerIndex == laneIndex);
    }

    /// <summary>
    /// Returns the ordered, non-minimized ChildPanes in the specified lane for the parent’s current SlideIndex.
    /// Useful for precise insert-index computation against realized FixedSizePixels during drag preview.
    /// </summary>
    public IReadOnlyList<ChildPaneDescriptor> GetChildrenInLaneForCurrentSlide(string parentId, int laneIndex)
    {
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, StringComparison.Ordinal));
        if (parent is null)
        {
            return Array.Empty<ChildPaneDescriptor>();
        }

        var slideIndex = parent.SlideIndex;
        return ChildPanes
            .Where(c =>
                !c.IsMinimized &&
                string.Equals(c.ParentId, parentId, StringComparison.Ordinal) &&
                c.SlideIndex == slideIndex &&
                c.ContainerIndex == laneIndex)
            .OrderBy(c => c.Order)
            .ToArray();
    }

    /// <summary>
    /// Returns true if a dock host (left/top/right/bottom) is currently occupied by any parent pane,
    /// including minimized panes. Floating host is not considered a dock and should not be passed here.
    /// </summary>
    public bool IsDockHostOccupied(string hostId)
    {
        var normalized = NormalizeHostId(hostId);
        if (string.Equals(normalized, "floating", StringComparison.Ordinal))
        {
            return false;
        }

        return ParentPaneModels.Any(p =>
            string.Equals(NormalizeHostId(p.HostId), normalized, StringComparison.Ordinal));
    }
}
