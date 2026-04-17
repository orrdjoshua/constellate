using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Layout
{
    public sealed record ParentBodyLayoutModel(
        string ParentPaneId,
        DockAttachmentModel Attachment,
        IReadOnlyList<SlideLayoutModel> Slides,
        int ActiveSlideIndex,
        int MaxVisibleLanes = 3)
    {
        public bool IsVerticalFlowAttachment =>
            Attachment.Kind == DockAttachmentKind.Left ||
            Attachment.Kind == DockAttachmentKind.Right;

        public bool IsHorizontalFlowAttachment =>
            Attachment.Kind == DockAttachmentKind.Top ||
            Attachment.Kind == DockAttachmentKind.Bottom;

        public SlideLayoutModel? ActiveSlide =>
            Slides.FirstOrDefault(slide => slide.SlideIndex == ActiveSlideIndex) ??
            Slides.FirstOrDefault();

        public static ParentBodyLayoutModel Empty(string parentPaneId, DockAttachmentModel attachment)
        {
            return new ParentBodyLayoutModel(
                ParentPaneId: parentPaneId,
                Attachment: attachment,
                Slides: Array.Empty<SlideLayoutModel>(),
                ActiveSlideIndex: 0,
                MaxVisibleLanes: 3);
        }
    }
}
