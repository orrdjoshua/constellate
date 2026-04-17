using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.App;

namespace Constellate.App.Infrastructure.Panes.Layout
{
    public static class ParentBodyLayoutBuilder
    {
        public static ParentBodyLayoutModel Build(
            ParentPaneModel parent,
            IEnumerable<ChildPaneDescriptor>? allChildren)
        {
            var attachment = DockAttachmentModel.FromHostId(parent.HostId);
            var isVerticalFlow = attachment.Kind == DockAttachmentKind.Left ||
                                 attachment.Kind == DockAttachmentKind.Right;

            var visibleChildren = (allChildren ?? Enumerable.Empty<ChildPaneDescriptor>())
                .Where(child =>
                    !child.IsMinimized &&
                    string.Equals(child.ParentId, parent.Id, StringComparison.Ordinal))
                .OrderBy(child => child.Order)
                .ToArray();

            var slides = new List<SlideLayoutModel>(capacity: 3);

            for (var slideIndex = 0; slideIndex < 3; slideIndex++)
            {
                var laneCount = Math.Max(1, Math.Min(3, parent.GetSplitCountForSlide(slideIndex)));
                var lanes = new List<LaneLayoutModel>(capacity: laneCount);

                for (var laneIndex = 0; laneIndex < laneCount; laneIndex++)
                {
                    var placements = visibleChildren
                        .Where(child =>
                            child.SlideIndex == slideIndex &&
                            child.ContainerIndex == laneIndex)
                        .Select((child, order) => new ChildPanePlacementModel(
                            ChildPaneId: child.Id,
                            Order: order,
                            PreferredSizeRatio: Math.Clamp(child.PreferredSizeRatio, 0.05, 0.95),
                            IsMinimized: child.IsMinimized))
                        .ToArray();

                    lanes.Add(new LaneLayoutModel(
                        LaneIndex: laneIndex,
                        IsVerticalFlow: isVerticalFlow,
                        Children: placements,
                        AllowsOverflowScroll: true));
                }

                slides.Add(new SlideLayoutModel(
                    SlideIndex: slideIndex,
                    Lanes: lanes,
                    IsScrollableAcrossSlides: true));
            }

            return new ParentBodyLayoutModel(
                ParentPaneId: parent.Id,
                Attachment: attachment,
                Slides: slides,
                ActiveSlideIndex: Math.Clamp(parent.SlideIndex, 0, 2),
                MaxVisibleLanes: 3);
        }

        public static IReadOnlyList<LaneView> BuildActiveLaneViews(
            ParentPaneModel parent,
            ParentBodyLayoutModel bodyLayout,
            IEnumerable<ChildPaneDescriptor>? allChildren)
        {
            var childrenById = (allChildren ?? Enumerable.Empty<ChildPaneDescriptor>())
                .ToDictionary(child => child.Id, StringComparer.Ordinal);

            var activeSlide = bodyLayout.ActiveSlide;
            if (activeSlide is null)
            {
                return Array.Empty<LaneView>();
            }

            var lanes = new List<LaneView>();

            foreach (var lane in activeSlide.Lanes.OrderBy(entry => entry.LaneIndex))
            {
                var resolvedChildren = new List<ChildPaneDescriptor>();
                var resolvedPlacements = new List<ChildPanePlacementModel>();

                foreach (var placement in lane.Children.OrderBy(entry => entry.Order))
                {
                    if (!childrenById.TryGetValue(placement.ChildPaneId, out var child))
                    {
                        continue;
                    }

                    resolvedChildren.Add(child);
                    resolvedPlacements.Add(placement);
                }

                lanes.Add(new LaneView
                {
                    ParentId = parent.Id,
                    LaneIndex = lane.LaneIndex,
                    IsVerticalFlow = lane.IsVerticalFlow,
                    IsVerticalScroll = lane.IsVerticalFlow,
                    Children = resolvedChildren.ToArray(),
                    Ratios = NormalizeRatios(resolvedPlacements)
                });
            }

            return lanes;
        }

        private static IReadOnlyList<double> NormalizeRatios(
            IReadOnlyList<ChildPanePlacementModel> placements)
        {
            if (placements.Count == 0)
            {
                return Array.Empty<double>();
            }

            var raw = placements
                .Select(placement => Math.Clamp(placement.PreferredSizeRatio, 0.05, 0.95))
                .ToArray();

            var sum = raw.Sum();
            if (sum <= 1e-6)
            {
                return Enumerable
                    .Repeat(1.0 / placements.Count, placements.Count)
                    .ToArray();
            }

            return raw.Select(value => value / sum).ToArray();
        }
    }
}
