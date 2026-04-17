using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace Constellate.App.Infrastructure.Panes
{
    public static class WorldShellLayoutEngine
    {
        public static WorldShellLayoutResult Compute(
            Rect fullBounds,
            IEnumerable<ParentPaneModel>? parents,
            double leftDockWidth,
            double topDockHeight,
            double rightDockWidth,
            double bottomDockHeight,
            bool isTopLeftCornerOwnedByTop,
            bool isTopRightCornerOwnedByTop,
            bool isBottomLeftCornerOwnedByBottom,
            bool isBottomRightCornerOwnedByBottom)
        {
            if (fullBounds.Width <= 0 || fullBounds.Height <= 0)
            {
                return WorldShellLayoutResult.Empty(fullBounds);
            }

            var expandedParents = (parents ?? Array.Empty<ParentPaneModel>())
                .Where(parent => !parent.IsMinimized)
                .ToArray();

            var leftParent = FindExpandedParentOnHost(expandedParents, "left");
            var topParent = FindExpandedParentOnHost(expandedParents, "top");
            var rightParent = FindExpandedParentOnHost(expandedParents, "right");
            var bottomParent = FindExpandedParentOnHost(expandedParents, "bottom");

            var leftWidth = leftParent is null ? 0.0 : Math.Clamp(leftDockWidth, 0.0, fullBounds.Width);
            var rightWidth = rightParent is null ? 0.0 : Math.Clamp(rightDockWidth, 0.0, fullBounds.Width);
            var topHeight = topParent is null ? 0.0 : Math.Clamp(topDockHeight, 0.0, fullBounds.Height);
            var bottomHeight = bottomParent is null ? 0.0 : Math.Clamp(bottomDockHeight, 0.0, fullBounds.Height);

            if (leftWidth + rightWidth > fullBounds.Width)
            {
                var scale = fullBounds.Width / Math.Max(1.0, leftWidth + rightWidth);
                leftWidth *= scale;
                rightWidth *= scale;
            }

            if (topHeight + bottomHeight > fullBounds.Height)
            {
                var scale = fullBounds.Height / Math.Max(1.0, topHeight + bottomHeight);
                topHeight *= scale;
                bottomHeight *= scale;
            }

            var residualRect = new Rect(
                fullBounds.X + leftWidth,
                fullBounds.Y + topHeight,
                Math.Max(0.0, fullBounds.Width - leftWidth - rightWidth),
                Math.Max(0.0, fullBounds.Height - topHeight - bottomHeight));

            var leftRect = CreateVerticalDockRect(
                fullBounds,
                leftWidth,
                topHeight,
                bottomHeight,
                topParent is not null && isTopLeftCornerOwnedByTop,
                bottomParent is not null && isBottomLeftCornerOwnedByBottom);

            var rightRect = CreateVerticalDockRect(
                new Rect(fullBounds.Right - rightWidth, fullBounds.Y, rightWidth, fullBounds.Height),
                rightWidth,
                topHeight,
                bottomHeight,
                topParent is not null && isTopRightCornerOwnedByTop,
                bottomParent is not null && isBottomRightCornerOwnedByBottom);

            var topRect = CreateHorizontalDockRect(
                fullBounds,
                topHeight,
                leftWidth,
                rightWidth,
                leftParent is not null && isTopLeftCornerOwnedByTop,
                rightParent is not null && isTopRightCornerOwnedByTop);

            var bottomRect = CreateHorizontalDockRect(
                new Rect(fullBounds.X, fullBounds.Bottom - bottomHeight, fullBounds.Width, bottomHeight),
                bottomHeight,
                leftWidth,
                rightWidth,
                leftParent is not null && isBottomLeftCornerOwnedByBottom,
                rightParent is not null && isBottomRightCornerOwnedByBottom);

            return new WorldShellLayoutResult(
                FullBounds: fullBounds,
                ResidualViewportRect: residualRect,
                FloatingSurfaceRect: residualRect,
                LeftDock: leftParent is null
                    ? null
                    : new DockHostLayout(
                        Attachment: DockAttachmentModel.FromHostId("left"),
                        ParentPaneId: leftParent.Id,
                        Bounds: leftRect,
                        IsVisible: leftRect.Width > 0 && leftRect.Height > 0,
                        OwnsLeadingCorner: !isTopLeftCornerOwnedByTop,
                        OwnsTrailingCorner: !isBottomLeftCornerOwnedByBottom),
                TopDock: topParent is null
                    ? null
                    : new DockHostLayout(
                        Attachment: DockAttachmentModel.FromHostId("top"),
                        ParentPaneId: topParent.Id,
                        Bounds: topRect,
                        IsVisible: topRect.Width > 0 && topRect.Height > 0,
                        OwnsLeadingCorner: isTopLeftCornerOwnedByTop,
                        OwnsTrailingCorner: isTopRightCornerOwnedByTop),
                RightDock: rightParent is null
                    ? null
                    : new DockHostLayout(
                        Attachment: DockAttachmentModel.FromHostId("right"),
                        ParentPaneId: rightParent.Id,
                        Bounds: rightRect,
                        IsVisible: rightRect.Width > 0 && rightRect.Height > 0,
                        OwnsLeadingCorner: !isTopRightCornerOwnedByTop,
                        OwnsTrailingCorner: !isBottomRightCornerOwnedByBottom),
                BottomDock: bottomParent is null
                    ? null
                    : new DockHostLayout(
                        Attachment: DockAttachmentModel.FromHostId("bottom"),
                        ParentPaneId: bottomParent.Id,
                        Bounds: bottomRect,
                        IsVisible: bottomRect.Width > 0 && bottomRect.Height > 0,
                        OwnsLeadingCorner: isBottomLeftCornerOwnedByBottom,
                        OwnsTrailingCorner: isBottomRightCornerOwnedByBottom));
        }

        private static ParentPaneModel? FindExpandedParentOnHost(IEnumerable<ParentPaneModel> parents, string hostId)
        {
            return parents.FirstOrDefault(parent =>
                string.Equals(parent.HostId, hostId, StringComparison.OrdinalIgnoreCase));
        }

        private static Rect CreateVerticalDockRect(
            Rect fullOrAnchoredBounds,
            double width,
            double topHeight,
            double bottomHeight,
            bool topIsCutAway,
            bool bottomIsCutAway)
        {
            if (width <= 0 || fullOrAnchoredBounds.Height <= 0)
            {
                return new Rect(fullOrAnchoredBounds.X, fullOrAnchoredBounds.Y, 0, 0);
            }

            var y = fullOrAnchoredBounds.Y + (topIsCutAway ? topHeight : 0.0);
            var height = fullOrAnchoredBounds.Height
                - (topIsCutAway ? topHeight : 0.0)
                - (bottomIsCutAway ? bottomHeight : 0.0);

            return new Rect(
                fullOrAnchoredBounds.X,
                y,
                width,
                Math.Max(0.0, height));
        }

        private static Rect CreateHorizontalDockRect(
            Rect fullOrAnchoredBounds,
            double height,
            double leftWidth,
            double rightWidth,
            bool leftCornerOwnedByThisDock,
            bool rightCornerOwnedByThisDock)
        {
            if (height <= 0 || fullOrAnchoredBounds.Width <= 0)
            {
                return new Rect(fullOrAnchoredBounds.X, fullOrAnchoredBounds.Y, 0, 0);
            }

            var x = fullOrAnchoredBounds.X + (leftCornerOwnedByThisDock ? 0.0 : leftWidth);
            var width = fullOrAnchoredBounds.Width
                - (leftCornerOwnedByThisDock ? 0.0 : leftWidth)
                - (rightCornerOwnedByThisDock ? 0.0 : rightWidth);

            return new Rect(
                x,
                fullOrAnchoredBounds.Y,
                Math.Max(0.0, width),
                height);
        }
    }
}
