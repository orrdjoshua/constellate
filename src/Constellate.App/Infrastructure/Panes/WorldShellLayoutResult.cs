using Avalonia;

namespace Constellate.App.Infrastructure.Panes
{
    public sealed record DockHostLayout(
        DockAttachmentModel Attachment,
        string? ParentPaneId,
        Rect Bounds,
        bool IsVisible,
        bool OwnsLeadingCorner,
        bool OwnsTrailingCorner);

    public sealed record WorldShellLayoutResult(
        Rect FullBounds,
        Rect ResidualViewportRect,
        Rect FloatingSurfaceRect,
        DockHostLayout? LeftDock,
        DockHostLayout? TopDock,
        DockHostLayout? RightDock,
        DockHostLayout? BottomDock)
    {
        public bool HasAnyDockedPanes =>
            (LeftDock?.IsVisible ?? false) ||
            (TopDock?.IsVisible ?? false) ||
            (RightDock?.IsVisible ?? false) ||
            (BottomDock?.IsVisible ?? false);

        public static WorldShellLayoutResult Empty(Rect fullBounds)
        {
            return new WorldShellLayoutResult(
                FullBounds: fullBounds,
                ResidualViewportRect: fullBounds,
                FloatingSurfaceRect: fullBounds,
                LeftDock: null,
                TopDock: null,
                RightDock: null,
                BottomDock: null);
        }
    }
}
