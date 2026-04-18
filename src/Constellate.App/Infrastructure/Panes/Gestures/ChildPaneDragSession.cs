using Avalonia;

namespace Constellate.App.Infrastructure.Panes.Gestures
{
    public sealed class ChildPaneDragSession : PaneGestureSession
    {
        public ChildPaneDragSession(
            string paneId,
            long pointerId,
            Point startPoint,
            string? originParentId,
            string? originHostId,
            int originLaneIndex,
            int originSlideIndex,
            Size originPreviewSize)
            : base(PaneGestureKind.ChildDrag, paneId, pointerId, startPoint)
        {
            OriginParentId = originParentId;
            OriginHostId = originHostId;
            OriginLaneIndex = originLaneIndex;
            OriginSlideIndex = originSlideIndex;
            OriginPreviewSize = originPreviewSize;
            TargetParentId = originParentId;
            TargetLaneIndex = originLaneIndex;
            TargetSlideIndex = originSlideIndex;
            TargetInsertIndex = -1;
        }

        public string? OriginParentId { get; }

        public string? OriginHostId { get; }

        public int OriginLaneIndex { get; }

        public int OriginSlideIndex { get; }

        public Size OriginPreviewSize { get; }

        public string? TargetParentId { get; private set; }

        public int TargetLaneIndex { get; private set; }

        public int TargetSlideIndex { get; private set; }

        public int TargetInsertIndex { get; private set; }

        public Rect? FloatingPreviewBounds { get; private set; }

        public bool HasFloatingPreview => FloatingPreviewBounds is not null;

        public void UpdateLaneTarget(
            Point currentPoint,
            string? targetParentId,
            int targetLaneIndex,
            int targetSlideIndex,
            int targetInsertIndex)
        {
            UpdatePointer(currentPoint);
            TargetParentId = targetParentId;
            TargetLaneIndex = targetLaneIndex;
            TargetSlideIndex = targetSlideIndex;
            TargetInsertIndex = targetInsertIndex;
            FloatingPreviewBounds = null;
        }

        public void UpdateFloatingPreview(Point currentPoint, Rect floatingPreviewBounds)
        {
            UpdatePointer(currentPoint);
            FloatingPreviewBounds = floatingPreviewBounds;
        }
    }
}
