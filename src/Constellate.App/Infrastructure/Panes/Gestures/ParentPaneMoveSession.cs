using Avalonia;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public sealed class ParentPaneMoveSession : PaneGestureSession
    {
        public ParentPaneMoveSession(
            string paneId,
            string originReferenceId,
            long pointerId,
            Point startPoint,
            DockAttachmentModel originAttachment,
            Rect originBounds)
            : base(PaneGestureKind.ParentMove, paneId, pointerId, startPoint)
        {
            OriginReferenceId = string.IsNullOrWhiteSpace(originReferenceId)
                ? paneId
                : originReferenceId;
            OriginAttachment = originAttachment;
            OriginBounds = originBounds;
            PreviewAttachment = originAttachment;
            PreviewBounds = originBounds;
        }

        public string OriginReferenceId { get; }

        public DockAttachmentModel OriginAttachment { get; }

        public Rect OriginBounds { get; }

        public DockAttachmentModel PreviewAttachment { get; private set; }

        public Rect PreviewBounds { get; private set; }

        public bool IsFloatingPreview => PreviewAttachment.IsFloating;

        public void UpdatePreview(
            Point currentPoint,
            DockAttachmentModel previewAttachment,
            Rect previewBounds)
        {
            UpdatePointer(currentPoint);
            PreviewAttachment = previewAttachment;
            PreviewBounds = previewBounds;
        }
    }
}
