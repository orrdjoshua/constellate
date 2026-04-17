using System;
using Avalonia;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public sealed record ParentPaneMovePreviewResult(
        DockAttachmentModel PreviewAttachment,
        Rect PreviewBounds);

    public sealed record ParentPaneMoveCommitResult(
        string TargetHostId,
        Rect? RelativeFloatingBounds)
    {
        public bool IsFloating =>
            string.Equals(TargetHostId, "floating", StringComparison.Ordinal);
    }

    public static class ParentPaneMoveGesturePlanner
    {
        public static ParentPaneMovePreviewResult ComputePreview(
            Point currentPoint,
            Size windowSize,
            Rect floatingSurfaceRect,
            DockAttachmentModel originAttachment,
            Rect? originBounds,
            Func<string, bool> isDockHostOccupied)
        {
            var targetHost = PaneDragCommitPlanner.ResolveAvailableParentTargetHost(
                PaneDragPreviewPlanner.ResolveTargetHost(currentPoint, windowSize),
                originAttachment,
                isDockHostOccupied);

            var previewAttachment = DockAttachmentModel.FromHostId(targetHost);
            var previewBounds = previewAttachment.IsFloating
                ? PaneDragPreviewPlanner.ComputeParentFloatingPreviewRect(
                    currentPoint,
                    floatingSurfaceRect,
                    originAttachment,
                    originBounds)
                : PaneDragPreviewPlanner.ComputeParentDockPreviewRect(
                    targetHost,
                    new Rect(0, 0, windowSize.Width, windowSize.Height));

            return new ParentPaneMovePreviewResult(
                PreviewAttachment: previewAttachment,
                PreviewBounds: previewBounds);
        }

        public static ParentPaneMoveCommitResult ComputeCommit(
            Point releasePoint,
            Size windowSize,
            Rect floatingSurfaceRect,
            DockAttachmentModel originAttachment,
            Rect? previewBounds,
            Func<string, bool> isDockHostOccupied)
        {
            var targetHost = PaneDragCommitPlanner.ResolveAvailableParentTargetHost(
                PaneDragPreviewPlanner.ResolveTargetHost(releasePoint, windowSize),
                originAttachment,
                isDockHostOccupied);

            if (string.Equals(targetHost, "floating", StringComparison.Ordinal))
            {
                var relativeFloatingRect = PaneDragCommitPlanner.ComputeRelativeFloatingDropRect(
                    floatingSurfaceRect,
                    previewBounds,
                    releasePoint);

                return new ParentPaneMoveCommitResult(
                    TargetHostId: targetHost,
                    RelativeFloatingBounds: relativeFloatingRect);
            }

            return new ParentPaneMoveCommitResult(
                TargetHostId: targetHost,
                RelativeFloatingBounds: null);
        }
    }
}
