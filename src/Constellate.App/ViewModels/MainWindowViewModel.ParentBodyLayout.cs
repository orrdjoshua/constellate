using System;
using System.Linq;
using Constellate.App.Infrastructure.Panes.Layout;

namespace Constellate.App;

public sealed partial class MainWindowViewModel
{
    private void RefreshParentBodyLayoutProjections()
    {
        foreach (var parent in ParentPaneModels)
        {
            var parentId = parent.Id;
            var slideIndex = parent.SlideIndex;

            var visibleForParent = ChildPanes
                .Where(child =>
                    !child.IsMinimized &&
                    child.SlideIndex == slideIndex &&
                    string.Equals(child.ParentId, parentId, StringComparison.Ordinal))
                .OrderBy(child => child.Order)
                .ToArray();

            parent.VisibleChildrenPrimary0 = visibleForParent
                .Where(child => child.ContainerIndex == 0)
                .ToArray();

            parent.VisibleChildrenPrimary1 = visibleForParent
                .Where(child => child.ContainerIndex == 1)
                .ToArray();

            parent.MinimizedChildren = ChildPanes
                .Where(child =>
                    child.IsMinimized &&
                    string.Equals(child.ParentId, parentId, StringComparison.Ordinal))
                .OrderBy(child => child.Order)
                .ToArray();

            var bodyLayout = ParentBodyLayoutBuilder.Build(parent, ChildPanes);
            parent.CurrentBodyLayout = bodyLayout;
            parent.LanesVisible = ParentBodyLayoutBuilder.BuildActiveLaneViews(parent, bodyLayout, ChildPanes);
        }
    }
}
