using System;
using Avalonia;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public static class PaneDragCommitPlanner
    {
        public static string ResolveAvailableParentTargetHost(
            string requestedHostId,
            DockAttachmentModel originAttachment,
            Func<string, bool> isDockHostOccupied)
        {
            var requested = NormalizeHostId(requestedHostId);
            if (string.Equals(requested, "floating", StringComparison.Ordinal))
            {
                return requested;
            }

            var originHost = NormalizeHostId(originAttachment.ToHostId());
            if (string.Equals(requested, originHost, StringComparison.Ordinal))
            {
                return requested;
            }

            return isDockHostOccupied(requested)
                ? "floating"
                : requested;
        }

        public static Rect ComputeRelativeFloatingDropRect(
            Rect floatingSurfaceRect,
            Rect? previewRect,
            Point releasePoint)
        {
            var effectiveRect = previewRect ??
                                PaneDragPreviewPlanner.ComputeCenteredSquareFloatingRect(
                                    releasePoint,
                                    floatingSurfaceRect);

            var relLeft = effectiveRect.X - floatingSurfaceRect.X;
            var relTop = effectiveRect.Y - floatingSurfaceRect.Y;

            relLeft = Math.Clamp(
                relLeft,
                0.0,
                Math.Max(0.0, floatingSurfaceRect.Width - effectiveRect.Width));

            relTop = Math.Clamp(
                relTop,
                0.0,
                Math.Max(0.0, floatingSurfaceRect.Height - effectiveRect.Height));

            return new Rect(relLeft, relTop, effectiveRect.Width, effectiveRect.Height);
        }

        public static (int LaneIndex, int InsertIndex) ResolveChildLanePlacement(
            string hostId,
            Rect hostRect,
            Point point,
            int splitCount,
            Func<int, int> getItemCountForLane)
        {
            var laneIndex = PaneDragPreviewPlanner.ResolveLaneIndex(
                hostId,
                hostRect,
                point,
                splitCount);

            var itemCount = Math.Max(0, getItemCountForLane(laneIndex));

            var insertIndex = PaneDragPreviewPlanner.ResolveInsertIndex(
                hostId,
                hostRect,
                point,
                itemCount);

            return (laneIndex, insertIndex);
        }

        private static string NormalizeHostId(string? hostId)
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
    }
}
