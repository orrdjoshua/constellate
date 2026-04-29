using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Constellate.App;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public sealed record ChildPaneDockParentCandidate(
        ParentPaneModel Parent,
        Rect Bounds);

    public sealed record ChildPaneDragPreviewResult(
        string? TargetParentId,
        int TargetLaneIndex,
        int TargetSlideIndex,
        int TargetInsertIndex,
        Rect PreviewBounds,
        bool IsFloatingPreview);

    public sealed record ChildPaneDragCommitResult(
        string? TargetParentId,
        int TargetLaneIndex,
        int TargetSlideIndex,
        int TargetInsertIndex)
    {
        public bool IsFloating => string.IsNullOrWhiteSpace(TargetParentId);
    }

    public static class ChildPaneDragGesturePlanner
    {
        private const double RedockHitMargin = 24.0;

        public static ChildPaneDragPreviewResult ComputePreview(
            Point currentPoint,
            Rect floatingSurfaceRect,
            Size originPreviewSize,
            IEnumerable<ChildPaneDockParentCandidate> candidateParents,
            Func<string, int, int> getChildrenCountInLaneForCurrentSlide,
            Func<string, int, IReadOnlyList<ChildPaneDescriptor>> getChildrenInLaneForCurrentSlide)
        {
            var target = ResolveTargetParent(currentPoint, candidateParents);
            if (target is null)
            {
                var previewRect = PaneDragPreviewPlanner.ComputeChildFloatingPreviewRect(
                    currentPoint,
                    floatingSurfaceRect,
                    defaultWidth: originPreviewSize.Width,
                    defaultHeight: originPreviewSize.Height);

                return new ChildPaneDragPreviewResult(
                    TargetParentId: null,
                    TargetLaneIndex: 0,
                    TargetSlideIndex: 0,
                    TargetInsertIndex: -1,
                    PreviewBounds: previewRect,
                    IsFloatingPreview: true);
            }

            var placement = ResolvePlacement(
                target.Parent,
                target.Bounds,
                currentPoint,
                getChildrenCountInLaneForCurrentSlide,
                getChildrenInLaneForCurrentSlide);

            var previewRectForDock = ComputeInsertPreviewRect(
                target.Parent,
                target.Bounds,
                placement.LaneIndex,
                placement.InsertIndex,
                getChildrenInLaneForCurrentSlide);

            return new ChildPaneDragPreviewResult(
                TargetParentId: target.Parent.Id,
                TargetLaneIndex: placement.LaneIndex,
                TargetSlideIndex: target.Parent.SlideIndex,
                TargetInsertIndex: placement.InsertIndex,
                PreviewBounds: previewRectForDock,
                IsFloatingPreview: false);
        }

        public static ChildPaneDragCommitResult ComputeCommit(
            Point releasePoint,
            IEnumerable<ChildPaneDockParentCandidate> candidateParents,
            Func<string, int, int> getChildrenCountInLaneForCurrentSlide,
            Func<string, int, IReadOnlyList<ChildPaneDescriptor>> getChildrenInLaneForCurrentSlide)
        {
            var target = ResolveTargetParent(releasePoint, candidateParents);
            if (target is null)
            {
                return new ChildPaneDragCommitResult(
                    TargetParentId: null,
                    TargetLaneIndex: 0,
                    TargetSlideIndex: 0,
                    TargetInsertIndex: -1);
            }

            var placement = ResolvePlacement(
                target.Parent,
                target.Bounds,
                releasePoint,
                getChildrenCountInLaneForCurrentSlide,
                getChildrenInLaneForCurrentSlide);

            return new ChildPaneDragCommitResult(
                TargetParentId: target.Parent.Id,
                TargetLaneIndex: placement.LaneIndex,
                TargetSlideIndex: target.Parent.SlideIndex,
                TargetInsertIndex: placement.InsertIndex);
        }

        public static int ResolveAutoSlideDirection(
            ParentPaneModel parent,
            Rect parentBounds,
            Point currentPoint,
            int maxSlideIndex = 2,
            double edgeFraction = 0.08)
        {
            bool nearPrevEdge;
            bool nearNextEdge;

            if (parent.IsVerticalBodyOrientation)
            {
                var relX = Math.Clamp((currentPoint.X - parentBounds.X) / Math.Max(1.0, parentBounds.Width), 0.0, 1.0);
                nearPrevEdge = relX <= edgeFraction;
                nearNextEdge = relX >= (1.0 - edgeFraction);
            }
            else
            {
                var relY = Math.Clamp((currentPoint.Y - parentBounds.Y) / Math.Max(1.0, parentBounds.Height), 0.0, 1.0);
                nearPrevEdge = relY <= edgeFraction;
                nearNextEdge = relY >= (1.0 - edgeFraction);
            }

            if (nearPrevEdge && parent.SlideIndex > 0)
            {
                return -1;
            }

            if (nearNextEdge && parent.SlideIndex < maxSlideIndex)
            {
                return 1;
            }

            return 0;
        }

        private static ChildPaneDockParentCandidate? ResolveTargetParent(
            Point point,
            IEnumerable<ChildPaneDockParentCandidate> candidateParents)
        {
            var candidates = (candidateParents ?? Enumerable.Empty<ChildPaneDockParentCandidate>())
                .Where(candidate => candidate.Bounds.Width > 0 && candidate.Bounds.Height > 0)
                .ToArray();

            if (candidates.Length == 0)
            {
                return null;
            }

            var directHit = candidates
                .Where(candidate => candidate.Bounds.Contains(point))
                .OrderBy(candidate => DistanceToRectCenterSquared(point, candidate.Bounds))
                .FirstOrDefault();

            if (directHit is not null)
            {
                return directHit;
            }

            return candidates
                .Where(candidate => Inflate(candidate.Bounds, RedockHitMargin).Contains(point))
                .OrderBy(candidate => DistanceToRectCenterSquared(point, candidate.Bounds))
                .FirstOrDefault();
        }

        private static (int LaneIndex, int InsertIndex) ResolvePlacement(
            ParentPaneModel parent,
            Rect parentBounds,
            Point point,
            Func<string, int, int> getChildrenCountInLaneForCurrentSlide,
            Func<string, int, IReadOnlyList<ChildPaneDescriptor>> getChildrenInLaneForCurrentSlide)
        {
            var splitCount = Math.Max(1, Math.Min(3, parent.SplitCount));
            var laneIndex = ResolveLaneIndex(parent.IsVerticalBodyOrientation, parentBounds, point, splitCount);

            // First try realized-sizes approach; fall back to equal-slot when data is not useful.
            var realized = getChildrenInLaneForCurrentSlide(parent.Id, laneIndex) ?? Array.Empty<ChildPaneDescriptor>();
            var insertIndex = ResolveInsertIndexWithRealizedSizes(parent, parentBounds, point, realized);

            if (insertIndex < 0)
            {
                var itemCount = Math.Max(0, getChildrenCountInLaneForCurrentSlide(parent.Id, laneIndex));
                insertIndex = ResolveInsertIndexEqualSlots(parent.IsVerticalBodyOrientation, parentBounds, point, itemCount);
            }

            return (laneIndex, insertIndex);
        }

        private static int ResolveLaneIndex(
            bool isVerticalBodyOrientation,
            Rect parentBounds,
            Point point,
            int splitCount)
        {
            var laneCount = Math.Max(1, splitCount);

            if (isVerticalBodyOrientation)
            {
                var relativeX = Math.Clamp((point.X - parentBounds.X) / Math.Max(1.0, parentBounds.Width), 0.0, 1.0);
                return Math.Clamp((int)Math.Floor(relativeX * laneCount), 0, laneCount - 1);
            }

            var relativeY = Math.Clamp((point.Y - parentBounds.Y) / Math.Max(1.0, parentBounds.Height), 0.0, 1.0);
            return Math.Clamp((int)Math.Floor(relativeY * laneCount), 0, laneCount - 1);
        }

        private static int ResolveInsertIndexEqualSlots(
            bool isVerticalBodyOrientation,
            Rect parentBounds,
            Point point,
            int itemCount)
        {
            var slotCount = Math.Max(1, itemCount + 1);

            if (isVerticalBodyOrientation)
            {
                var relativeY = Math.Clamp((point.Y - parentBounds.Y) / Math.Max(1.0, parentBounds.Height), 0.0, 1.0);
                return Math.Clamp((int)Math.Floor(relativeY * slotCount), 0, itemCount);
            }

            var relativeX = Math.Clamp((point.X - parentBounds.X) / Math.Max(1.0, parentBounds.Width), 0.0, 1.0);
            return Math.Clamp((int)Math.Floor(relativeX * slotCount), 0, itemCount);
        }

        private static int ResolveInsertIndexWithRealizedSizes(
            ParentPaneModel parent,
            Rect parentBounds,
            Point point,
            IReadOnlyList<ChildPaneDescriptor> laneChildren)
        {
            if (laneChildren is null || laneChildren.Count == 0)
            {
                return 0;
            }

            // axis position normalized [0..1] along fixed dimension
            double axisRel;
            if (parent.IsVerticalBodyOrientation)
            {
                axisRel = Math.Clamp((point.Y - parentBounds.Y) / Math.Max(1.0, parentBounds.Height), 0.0, 1.0);
            }
            else
            {
                axisRel = Math.Clamp((point.X - parentBounds.X) / Math.Max(1.0, parentBounds.Width), 0.0, 1.0);
            }

            var sizes = laneChildren.Select(c => Math.Max(1.0, c.FixedSizePixels)).ToArray();
            var total = sizes.Sum();
            if (total <= 0.0)
            {
                return -1; // fall back to equal-slot caller path
            }

            // cumulative boundaries as fractions [0..1]
            var cumul = new List<double>(sizes.Length + 1) { 0.0 };
            double running = 0.0;
            foreach (var s in sizes)
            {
                running += s;
                cumul.Add(running / total);
            }
            // cumul.Count == sizes.Length + 1; items represent child-bottoms; insert slots are between these.
            // Determine nearest boundary slot ahead of axisRel
            for (var i = 0; i < cumul.Count - 1; i++)
            {
                var start = cumul[i];
                var end = cumul[i + 1];
                if (axisRel <= start)
                {
                    return i;
                }
                if (axisRel > start && axisRel <= end)
                {
                    // halfway rule: below midpoint -> before child i, else after it
                    var mid = (start + end) / 2.0;
                    return axisRel <= mid ? i : i + 1;
                }
            }
            return cumul.Count - 1; // after the last child
        }

        private static Rect ComputeInsertPreviewRect(
            ParentPaneModel parent,
            Rect parentBounds,
            int laneIndex,
            int insertIndex,
            Func<string, int, IReadOnlyList<ChildPaneDescriptor>> getChildrenInLaneForCurrentSlide)
        {
            var laneCount = Math.Max(1, Math.Min(3, parent.SplitCount));
            var children = getChildrenInLaneForCurrentSlide(parent.Id, laneIndex) ?? Array.Empty<ChildPaneDescriptor>();

            if (parent.IsVerticalBodyOrientation)
            {
                var laneWidth = Math.Max(1.0, parentBounds.Width / laneCount);
                var laneLeft = parentBounds.X + (laneWidth * laneIndex);

                // Compute cumulative absolute Y from realized pixels scaled to parent height.
                double y = parentBounds.Y;
                var sum = children.Sum(c => Math.Max(1.0, c.FixedSizePixels));
                if (sum > 0 && insertIndex > 0)
                {
                    var frac = children.Take(insertIndex).Sum(c => Math.Max(1.0, c.FixedSizePixels)) / sum;
                    y = parentBounds.Y + (parentBounds.Height * frac);
                }
                else if (insertIndex <= 0)
                {
                    y = parentBounds.Y;
                }
                else
                {
                    y = parentBounds.Bottom;
                }

                return new Rect(
                    laneLeft + 6.0,
                    Math.Clamp(y, parentBounds.Y, parentBounds.Bottom),
                    Math.Max(1.0, laneWidth - 12.0),
                    4.0);
            }
            else
            {
                var laneHeight = Math.Max(1.0, parentBounds.Height / laneCount);
                var laneTop = parentBounds.Y + (laneHeight * laneIndex);

                double x = parentBounds.X;
                var sum = children.Sum(c => Math.Max(1.0, c.FixedSizePixels));
                if (sum > 0 && insertIndex > 0)
                {
                    var frac = children.Take(insertIndex).Sum(c => Math.Max(1.0, c.FixedSizePixels)) / sum;
                    x = parentBounds.X + (parentBounds.Width * frac);
                }
                else if (insertIndex <= 0)
                {
                    x = parentBounds.X;
                }
                else
                {
                    x = parentBounds.Right;
                }

                return new Rect(
                    Math.Clamp(x, parentBounds.X, parentBounds.Right),
                    laneTop + 6.0,
                    4.0,
                    Math.Max(1.0, laneHeight - 12.0));
            }
        }

        private static Rect Inflate(Rect rect, double margin)
        {
            return new Rect(
                rect.X - margin,
                rect.Y - margin,
                rect.Width + (margin * 2.0),
                rect.Height + (margin * 2.0));
        }

        private static double DistanceToRectCenterSquared(Point point, Rect rect)
        {
            var centerX = rect.X + (rect.Width / 2.0);
            var centerY = rect.Y + (rect.Height / 2.0);
            var dx = point.X - centerX;
            var dy = point.Y - centerY;
            return (dx * dx) + (dy * dy);
        }
    }
}
