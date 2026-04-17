using System;
using System.Collections.Generic;

namespace Constellate.App.Infrastructure.Panes.Layout
{
    public sealed record ChildPanePlacementModel(
        string ChildPaneId,
        int Order,
        double PreferredSizeRatio,
        bool IsMinimized = false);

    public sealed record LaneLayoutModel(
        int LaneIndex,
        bool IsVerticalFlow,
        IReadOnlyList<ChildPanePlacementModel> Children,
        bool AllowsOverflowScroll = false)
    {
        public static LaneLayoutModel Empty(int laneIndex, bool isVerticalFlow)
        {
            return new LaneLayoutModel(
                LaneIndex: laneIndex,
                IsVerticalFlow: isVerticalFlow,
                Children: Array.Empty<ChildPanePlacementModel>(),
                AllowsOverflowScroll: false);
        }
    }
}
