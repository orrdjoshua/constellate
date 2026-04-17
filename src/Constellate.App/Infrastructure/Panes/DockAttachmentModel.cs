using System;

namespace Constellate.App.Infrastructure.Panes
{
    public enum DockAttachmentKind
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 3,
        Bottom = 4,
        Floating = 5
    }

    public readonly record struct DockAttachmentModel(DockAttachmentKind Kind)
    {
        public bool IsDocked =>
            Kind == DockAttachmentKind.Left ||
            Kind == DockAttachmentKind.Top ||
            Kind == DockAttachmentKind.Right ||
            Kind == DockAttachmentKind.Bottom;

        public bool IsFloating => Kind == DockAttachmentKind.Floating;

        public bool IsNone => Kind == DockAttachmentKind.None;

        public string ToHostId()
        {
            return Kind switch
            {
                DockAttachmentKind.Left => "left",
                DockAttachmentKind.Top => "top",
                DockAttachmentKind.Right => "right",
                DockAttachmentKind.Bottom => "bottom",
                DockAttachmentKind.Floating => "floating",
                _ => "left"
            };
        }

        public static DockAttachmentModel Unspecified => new(DockAttachmentKind.None);

        public static DockAttachmentModel FromHostId(string? hostId)
        {
            var normalized = string.IsNullOrWhiteSpace(hostId)
                ? string.Empty
                : hostId.Trim().ToLowerInvariant();

            return normalized switch
            {
                "left" => new DockAttachmentModel(DockAttachmentKind.Left),
                "top" => new DockAttachmentModel(DockAttachmentKind.Top),
                "right" => new DockAttachmentModel(DockAttachmentKind.Right),
                "bottom" => new DockAttachmentModel(DockAttachmentKind.Bottom),
                "floating" => new DockAttachmentModel(DockAttachmentKind.Floating),
                _ => Unspecified
            };
        }
    }
}
