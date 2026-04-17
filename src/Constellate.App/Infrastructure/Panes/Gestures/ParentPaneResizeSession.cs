using System;
using Avalonia;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    [Flags]
    public enum PaneResizeEdge
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8
    }

    public sealed class ParentPaneResizeSession : PaneGestureSession
    {
        public ParentPaneResizeSession(
            string paneId,
            long pointerId,
            Point startPoint,
            DockAttachmentModel attachment,
            PaneResizeEdge resizeEdge,
            Rect originBounds)
            : base(PaneGestureKind.ParentResize, paneId, pointerId, startPoint)
        {
            Attachment = attachment;
            ResizeEdge = resizeEdge;
            OriginBounds = originBounds;
            PreviewBounds = originBounds;
        }

        public DockAttachmentModel Attachment { get; }

        public PaneResizeEdge ResizeEdge { get; }

        public Rect OriginBounds { get; }

        public Rect PreviewBounds { get; private set; }

        public void UpdatePreview(Point currentPoint, Rect previewBounds)
        {
            UpdatePointer(currentPoint);
            PreviewBounds = previewBounds;
        }
    }
}
