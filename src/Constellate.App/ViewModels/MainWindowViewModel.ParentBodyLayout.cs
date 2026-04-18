using Constellate.App.Infrastructure.Panes.Layout;

namespace Constellate.App;

public sealed partial class MainWindowViewModel
{
    private void RefreshParentBodyLayoutProjections()
    {
        foreach (var parent in ParentPaneModels)
        {
            var projection = ParentBodyProjectionBuilder.Build(parent, ChildPanes);

            parent.VisibleChildrenPrimary0 = projection.VisibleChildrenPrimary0;
            parent.VisibleChildrenPrimary1 = projection.VisibleChildrenPrimary1;
            parent.MinimizedChildren = projection.MinimizedChildren;
            parent.CurrentBodyLayout = projection.CurrentBodyLayout;
            parent.LanesVisible = projection.LanesVisible;
        }
    }
}
