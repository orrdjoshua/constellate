using System;
using System.Collections.Generic;
using System.Linq;

namespace Constellate.App.Infrastructure.Panes.Layout;

internal sealed class ParentBodyProjectionSnapshot
{
    public IReadOnlyList<ChildPaneDescriptor> VisibleChildrenPrimary0 { get; init; } = Array.Empty<ChildPaneDescriptor>();
    public IReadOnlyList<ChildPaneDescriptor> VisibleChildrenPrimary1 { get; init; } = Array.Empty<ChildPaneDescriptor>();
    public IReadOnlyList<ChildPaneDescriptor> MinimizedChildren { get; init; } = Array.Empty<ChildPaneDescriptor>();
    public ParentBodyLayoutModel? CurrentBodyLayout { get; init; }
    public IReadOnlyList<LaneView> LanesVisible { get; init; } = Array.Empty<LaneView>();
}

internal static class ParentBodyProjectionBuilder
{
    public static ParentBodyProjectionSnapshot Build(
        ParentPaneModel parent,
        IEnumerable<ChildPaneDescriptor> allChildren)
    {
        var childArray = allChildren?.ToArray() ?? Array.Empty<ChildPaneDescriptor>();
        var parentId = parent.Id;
        var slideIndex = parent.SlideIndex;

        var visibleForParent = childArray
            .Where(child =>
                !child.IsMinimized &&
                child.SlideIndex == slideIndex &&
                string.Equals(child.ParentId, parentId, StringComparison.Ordinal))
            .OrderBy(child => child.Order)
            .ToArray();

        var bodyLayout = ParentBodyLayoutBuilder.Build(parent, childArray);

        return new ParentBodyProjectionSnapshot
        {
            VisibleChildrenPrimary0 = visibleForParent
                .Where(child => child.ContainerIndex == 0)
                .ToArray(),
            VisibleChildrenPrimary1 = visibleForParent
                .Where(child => child.ContainerIndex == 1)
                .ToArray(),
            MinimizedChildren = childArray
                .Where(child =>
                    child.IsMinimized &&
                    string.Equals(child.ParentId, parentId, StringComparison.Ordinal))
                .OrderBy(child => child.Order)
                .ToArray(),
            CurrentBodyLayout = bodyLayout,
            LanesVisible = ParentBodyLayoutBuilder.BuildActiveLaneViews(parent, bodyLayout, childArray)
        };
    }
}
