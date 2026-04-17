using System;
using Avalonia;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public sealed record ChildPaneDragPreviewResult(
        string TargetHostId,
        string? TargetParentId,
        int TargetLaneIndex,
        int TargetSlideIndex,
        int TargetInsertIndex,
        Rect PreviewBounds,
        bool IsFloatingPreview);

    public sealed record ChildPaneDragCommitResult(
        string TargetHostId,
        string? TargetParentId,
        int TargetLaneIndex,
        int TargetSlideIndex,
        int TargetInsertIndex)
    {
        public bool IsFloating =>
            string.Equals(TargetHostId, "floating", StringComparison.Ordinal);
    }

    public static class ChildPaneDragGesturePlanner
    {
        public static ChildPaneDragPreviewResult ComputePreview(
            Point currentPoint,
            Size windowSize,
            Rect floatingSurfaceRect,
            Size originPreviewSize,
            Func<string, Rect> getHostRect,
            Func<string, ParentPaneModel?> getExpandedParentOnHost,
            Func<string, int, int> getChildrenCountInLaneForCurrentSlide)
        {
            var targetHostId = PaneDragPreviewPlanner.ResolveTargetHost(currentPoint, windowSize);
            var parent = getExpandedParentOnHost(targetHostId);

            if (parent is null)
            {
                var previewRect = string.Equals(targetHostId, "floating", StringComparison.Ordinal)
                    ? PaneDragPreviewPlanner.ComputeChildFloatingPreviewRect(
                        currentPoint,
                        floatingSurfaceRect,
                        defaultWidth: originPreviewSize.Width,
                        defaultHeight: originPreviewSize.Height)
                    : PaneDragPreviewPlanner.ComputeChildDockPreviewRect(
                        targetHostId,
                        getHostRect(targetHostId));

                return new ChildPaneDragPreviewResult(
                    TargetHostId: targetHostId,
                    TargetParentId: null,
                    TargetLaneIndex: 0,
                    TargetSlideIndex: 0,
                    TargetInsertIndex: -1,
                    PreviewBounds: previewRect,
                    IsFloatingPreview: true);
            }

            var hostRect = getHostRect(targetHostId);
            var splitCount = Math.Max(1, Math.Min(3, parent.SplitCount));

            var placement = PaneDragCommitPlanner.ResolveChildLanePlacement(
                targetHostId,
                hostRect,
                currentPoint,
                splitCount,
                laneIndex => getChildrenCountInLaneForCurrentSlide(parent.Id, laneIndex));

            var insertPreviewRect = PaneDragPreviewPlanner.ComputeChildInsertPreviewRect(
                targetHostId,
                hostRect,
                placement.LaneIndex,
                placement.InsertIndex,
                splitCount);

            return new ChildPaneDragPreviewResult(
                TargetHostId: targetHostId,
                TargetParentId: parent.Id,
                TargetLaneIndex: placement.LaneIndex,
                TargetSlideIndex: parent.SlideIndex,
                TargetInsertIndex: placement.InsertIndex,
                PreviewBounds: insertPreviewRect,
                IsFloatingPreview: false);
        }

        public static ChildPaneDragCommitResult? ComputeCommit(
            Point releasePoint,
            Size windowSize,
            Func<string, Rect> getHostRect,
            Func<string, ParentPaneModel?> getExpandedParentOnHost,
            Func<string, int, int> getChildrenCountInLaneForCurrentSlide)
        {
            var targetHostId = PaneDragPreviewPlanner.ResolveTargetHost(releasePoint, windowSize);
            if (string.Equals(targetHostId, "floating", StringComparison.Ordinal))
            {
                return new ChildPaneDragCommitResult(
                    TargetHostId: targetHostId,
                    TargetParentId: null,
                    TargetLaneIndex: 0,
                    TargetSlideIndex: 0,
                    TargetInsertIndex: -1);
            }

            var parent = getExpandedParentOnHost(targetHostId);
            if (parent is null)
            {
                return null;
            }

            var hostRect = getHostRect(targetHostId);
            var splitCount = Math.Max(1, Math.Min(3, parent.SplitCount));

            var placement = PaneDragCommitPlanner.ResolveChildLanePlacement(
                targetHostId,
                hostRect,
                releasePoint,
                splitCount,
                laneIndex => getChildrenCountInLaneForCurrentSlide(parent.Id, laneIndex));

            return new ChildPaneDragCommitResult(
                TargetHostId: targetHostId,
                TargetParentId: parent.Id,
                TargetLaneIndex: placement.LaneIndex,
                TargetSlideIndex: parent.SlideIndex,
                TargetInsertIndex: placement.InsertIndex);
        }

        public static int ResolveAutoSlideDirection(
            string hostId,
            Rect hostRect,
            Point currentPoint,
            int currentSlideIndex,
            int maxSlideIndex = 2,
            double edgeFraction = 0.08)
        {
            bool nearPrevEdge;
            bool nearNextEdge;
            var isVerticalFlow =
                string.Equals(hostId, "left", StringComparison.Ordinal) ||
                string.Equals(hostId, "right", StringComparison.Ordinal);

            if (isVerticalFlow)
            {
                var relX = Math.Clamp((currentPoint.X - hostRect.X) / Math.Max(1.0, hostRect.Width), 0.0, 1.0);
                nearPrevEdge = relX <= edgeFraction;
                nearNextEdge = relX >= (1.0 - edgeFraction);
            }
            else
            {
                var relY = Math.Clamp((currentPoint.Y - hostRect.Y) / Math.Max(1.0, hostRect.Height), 0.0, 1.0);
                nearPrevEdge = relY <= edgeFraction;
                nearNextEdge = relY >= (1.0 - edgeFraction);
            }

            if (nearPrevEdge && currentSlideIndex > 0)
            {
                return -1;
            }

            if (nearNextEdge && currentSlideIndex < maxSlideIndex)
            {
                return 1;
            }

            return 0;
        }
    }
}
