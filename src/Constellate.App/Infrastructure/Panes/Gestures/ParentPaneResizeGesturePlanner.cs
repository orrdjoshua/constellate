using System;
using Avalonia;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public sealed record ParentPaneResizePreviewResult(
        double PreviewExtent,
        Rect PreviewBounds);

    public static class ParentPaneResizeGesturePlanner
    {
        public static Rect CreateOriginBounds(
            DockAttachmentModel attachment,
            Rect fullWindowBounds,
            double currentExtent,
            double minExtent = 80.0)
        {
            var extent = Math.Max(minExtent, currentExtent);

            return attachment.Kind switch
            {
                DockAttachmentKind.Left => new Rect(
                    fullWindowBounds.X,
                    fullWindowBounds.Y,
                    extent,
                    fullWindowBounds.Height),

                DockAttachmentKind.Right => new Rect(
                    fullWindowBounds.Right - extent,
                    fullWindowBounds.Y,
                    extent,
                    fullWindowBounds.Height),

                DockAttachmentKind.Top => new Rect(
                    fullWindowBounds.X,
                    fullWindowBounds.Y,
                    fullWindowBounds.Width,
                    extent),

                DockAttachmentKind.Bottom => new Rect(
                    fullWindowBounds.X,
                    fullWindowBounds.Bottom - extent,
                    fullWindowBounds.Width,
                    extent),

                _ => fullWindowBounds
            };
        }

        public static ParentPaneResizePreviewResult ComputePreview(
            DockAttachmentModel attachment,
            Rect fullWindowBounds,
            Rect originBounds,
            Point startPoint,
            Point currentPoint,
            double minExtent = 80.0)
        {
            var dx = currentPoint.X - startPoint.X;
            var dy = currentPoint.Y - startPoint.Y;

            var previewExtent = attachment.Kind switch
            {
                DockAttachmentKind.Left => Math.Max(minExtent, originBounds.Width + dx),
                DockAttachmentKind.Right => Math.Max(minExtent, originBounds.Width - dx),
                DockAttachmentKind.Top => Math.Max(minExtent, originBounds.Height + dy),
                DockAttachmentKind.Bottom => Math.Max(minExtent, originBounds.Height - dy),
                _ => Math.Max(minExtent, Math.Max(originBounds.Width, originBounds.Height))
            };

            var previewBounds = CreateOriginBounds(
                attachment,
                fullWindowBounds,
                previewExtent,
                minExtent);

            return new ParentPaneResizePreviewResult(
                PreviewExtent: previewExtent,
                PreviewBounds: previewBounds);
        }
    }
}
