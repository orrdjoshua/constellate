using System.Diagnostics;
using System.Linq;
using Constellate.App.Infrastructure.Panes.Layout;

namespace Constellate.App;

public sealed partial class MainWindowViewModel
{
    public bool UpdateParentBodyViewport(string parentId, double width, double height)
    {
        var parent = ParentPaneModels.FirstOrDefault(p => string.Equals(p.Id, parentId, System.StringComparison.Ordinal));
        if (parent is null)
        {
            return false;
        }

        var changed =
            System.Math.Abs(parent.BodyViewportWidth - width) > double.Epsilon ||
            System.Math.Abs(parent.BodyViewportHeight - height) > double.Epsilon;

        if (!changed)
        {
            return false;
        }

        parent.BodyViewportWidth = width;
        parent.BodyViewportHeight = height;

        Debug.WriteLine(
            $"[ParentBodyViewport][Sync] parent={parent.Id} orientation={(parent.IsVerticalBodyOrientation ? "vertical" : "horizontal")} " +
            $"bodyW={parent.BodyViewportWidth:0.##} bodyH={parent.BodyViewportHeight:0.##} " +
            $"fixed={parent.BodyViewportFixedSize:0.##} adjustable={parent.BodyViewportAdjustableSize:0.##}");

        RefreshParentBodyLayoutProjections();
        return true;
    }

    public void RefreshParentBodyLayoutFromViewportMeasurement()
    {
        RefreshParentBodyLayoutProjections();
    }

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

            if (projection.LanesVisible.Count == 0)
            {
                Debug.WriteLine(
                    $"[ParentBodyProjection] parent={parent.Id} orientation={(parent.IsVerticalBodyOrientation ? "vertical" : "horizontal")} " +
                    $"bodyW={parent.BodyViewportWidth:0.##} bodyH={parent.BodyViewportHeight:0.##} lanes=0 slide={parent.SlideIndex} splits={parent.SplitCount}");
                continue;
            }

            foreach (var lane in projection.LanesVisible)
            {
                Debug.WriteLine(
                    $"[ParentBodyProjection] parent={parent.Id} lane={lane.LaneIndex} flow={(lane.IsVerticalFlow ? "vertical" : "horizontal")} " +
                    $"laneViewportW={lane.ViewportWidth:0.##} laneViewportH={lane.ViewportHeight:0.##} " +
                    $"fixedViewport={lane.FixedViewportSize:0.##} adjustableViewport={lane.AdjustableViewportSize:0.##} " +
                    $"childFixed=[{string.Join(",", lane.Children.Select(c => $"{c.Id}:{c.FixedSizePixels:0.##}"))}] " +
                    $"children={lane.Children.Count} ratios=[{string.Join(",", lane.Ratios.Select(r => r.ToString("0.###")))}]");
            }
        }
    }
}
