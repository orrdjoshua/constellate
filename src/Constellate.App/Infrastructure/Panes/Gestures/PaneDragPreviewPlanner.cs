using System;
using Avalonia;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public static class PaneDragPreviewPlanner
    {
        public static string ResolveTargetHost(Point point, Size windowSize)
        {
            var width = Math.Max(1.0, windowSize.Width);
            var height = Math.Max(1.0, windowSize.Height);

            var leftThreshold = width * 0.15;
            var rightThreshold = width * 0.85;
            var topThreshold = height * 0.15;
            var bottomThreshold = height * 0.85;

            if (point.X <= leftThreshold)
            {
                return "left";
            }

            if (point.X >= rightThreshold)
            {
                return "right";
            }

            if (point.Y <= topThreshold)
            {
                return "top";
            }

            if (point.Y >= bottomThreshold)
            {
                return "bottom";
            }

            return "floating";
        }

        public static Rect ComputeParentDockPreviewRect(string hostId, Rect fullBounds)
        {
            var normalized = NormalizeHostId(hostId);
            var width = Math.Max(1.0, fullBounds.Width);
            var height = Math.Max(1.0, fullBounds.Height);

            return normalized switch
            {
                "left" => new Rect(fullBounds.X, fullBounds.Y, width * 0.25, height),
                "right" => new Rect(fullBounds.Right - (width * 0.25), fullBounds.Y, width * 0.25, height),
                "top" => new Rect(
                    fullBounds.X + ((width - (width * 0.60)) / 2.0),
                    fullBounds.Y,
                    width * 0.60,
                    height * 0.22),
                "bottom" => new Rect(
                    fullBounds.X + ((width - (width * 0.60)) / 2.0),
                    fullBounds.Bottom - (height * 0.22),
                    width * 0.60,
                    height * 0.22),
                _ => fullBounds
            };
        }

        public static Rect ComputeCenteredSquareFloatingRect(
            Point pointer,
            Rect surfaceRect,
            double scaleFraction = 0.30,
            double minSide = 80.0)
        {
            var side = Math.Max(minSide, Math.Min(surfaceRect.Width, surfaceRect.Height) * scaleFraction);
            return ComputeCenteredFloatingRect(pointer, surfaceRect, side, side, minSide, minSide);
        }

        public static Rect ComputeParentFloatingPreviewRect(
            Point pointer,
            Rect surfaceRect,
            DockAttachmentModel originAttachment,
            Rect? originBounds,
            double minSide = 80.0)
        {
            if (originAttachment.IsFloating &&
                originBounds is { Width: > 0, Height: > 0 } rect)
            {
                return ComputeCenteredFloatingRect(
                    pointer,
                    surfaceRect,
                    rect.Width,
                    rect.Height,
                    minSide,
                    minSide);
            }

            return ComputeCenteredSquareFloatingRect(pointer, surfaceRect);
        }

        public static Rect ComputeChildFloatingPreviewRect(
            Point pointer,
            Rect surfaceRect,
            double defaultWidth = 260.0,
            double defaultHeight = 160.0,
            double minWidth = 80.0,
            double minHeight = 80.0)
        {
            return ComputeCenteredFloatingRect(pointer, surfaceRect, defaultWidth, defaultHeight, minWidth, minHeight);
        }

        public static Rect ComputeCenteredFloatingRect(
            Point pointer,
            Rect surfaceRect,
            double width,
            double height,
            double minWidth,
            double minHeight)
        {
            var resolvedWidth = Math.Max(minWidth, width);
            var resolvedHeight = Math.Max(minHeight, height);

            var left = pointer.X - (resolvedWidth / 2.0);
            var top = pointer.Y - (resolvedHeight / 2.0);

            var minLeft = surfaceRect.X;
            var minTop = surfaceRect.Y;
            var maxLeft = surfaceRect.X + Math.Max(0.0, surfaceRect.Width - resolvedWidth);
            var maxTop = surfaceRect.Y + Math.Max(0.0, surfaceRect.Height - resolvedHeight);

            left = Math.Clamp(left, minLeft, maxLeft);
            top = Math.Clamp(top, minTop, maxTop);

            return new Rect(left, top, resolvedWidth, resolvedHeight);
        }

        public static Rect ComputeChildDockPreviewRect(
            string hostId,
            Rect hostRect,
            double defaultWidth = 260.0,
            double defaultHeight = 160.0,
            double margin = 12.0)
        {
            var width = Math.Min(defaultWidth, Math.Max(120.0, hostRect.Width - (2 * margin)));
            var height = Math.Min(defaultHeight, Math.Max(80.0, hostRect.Height - (2 * margin)));

            var left = hostRect.X + ((hostRect.Width - width) / 2.0);
            var top = hostRect.Y + margin;

            return new Rect(left, top, width, height);
        }

        public static int ResolveLaneIndex(string hostId, Rect hostRect, Point point, int splitCount)
        {
            var laneCount = Math.Max(1, splitCount);
            if (UsesVerticalFlow(hostId))
            {
                var relX = Math.Clamp((point.X - hostRect.X) / Math.Max(1.0, hostRect.Width), 0.0, 1.0);
                return Math.Clamp((int)Math.Floor(relX * laneCount), 0, laneCount - 1);
            }

            var relY = Math.Clamp((point.Y - hostRect.Y) / Math.Max(1.0, hostRect.Height), 0.0, 1.0);
            return Math.Clamp((int)Math.Floor(relY * laneCount), 0, laneCount - 1);
        }

        public static int ResolveInsertIndex(string hostId, Rect hostRect, Point point, int itemCount)
        {
            var slotCount = Math.Max(0, itemCount) + 1;
            if (UsesVerticalFlow(hostId))
            {
                var relY = Math.Clamp((point.Y - hostRect.Y) / Math.Max(1.0, hostRect.Height), 0.0, 1.0);
                return Math.Clamp((int)Math.Floor(relY * slotCount), 0, Math.Max(0, itemCount));
            }

            var relX = Math.Clamp((point.X - hostRect.X) / Math.Max(1.0, hostRect.Width), 0.0, 1.0);
            return Math.Clamp((int)Math.Floor(relX * slotCount), 0, Math.Max(0, itemCount));
        }

        public static Rect ComputeChildInsertPreviewRect(
            string hostId,
            Rect hostRect,
            int laneIndex,
            int insertIndex,
            int splitCount)
        {
            var laneCount = Math.Max(1, splitCount);
            if (UsesVerticalFlow(hostId))
            {
                var laneWidth = Math.Max(1.0, hostRect.Width / laneCount);
                var laneLeft = hostRect.X + (laneWidth * laneIndex);
                var slotHeight = hostRect.Height / Math.Max(1, insertIndex + 1);

                return new Rect(
                    laneLeft + 6.0,
                    hostRect.Y + (slotHeight * insertIndex),
                    Math.Max(1.0, laneWidth - 12.0),
                    4.0);
            }

            var laneHeight = Math.Max(1.0, hostRect.Height / laneCount);
            var laneTop = hostRect.Y + (laneHeight * laneIndex);
            var slotWidth = hostRect.Width / Math.Max(1, insertIndex + 1);

            return new Rect(
                hostRect.X + (slotWidth * insertIndex),
                laneTop + 6.0,
                4.0,
                Math.Max(1.0, laneHeight - 12.0));
        }

        private static bool UsesVerticalFlow(string hostId)
        {
            var normalized = NormalizeHostId(hostId);
            return string.Equals(normalized, "left", StringComparison.Ordinal) ||
                   string.Equals(normalized, "right", StringComparison.Ordinal);
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
