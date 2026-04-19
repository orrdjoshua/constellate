using System;
using Avalonia;

namespace Constellate.App.Infrastructure.Panes.Gestures;

internal static class ParentPaneDragStateResolver
{
    public static Rect GetParentPaneCurrentBounds(
        ParentPaneModel parent,
        Func<string?, Rect> getShellHostRect,
        Func<Rect> getShellFloatingSurfaceRect)
    {
        if (string.Equals(MainWindowViewModel.NormalizeHostId(parent.HostId), "floating", StringComparison.Ordinal))
        {
            var floatingRect = getShellFloatingSurfaceRect();
            return new Rect(
                floatingRect.X + parent.FloatingX,
                floatingRect.Y + parent.FloatingY,
                parent.FloatingWidth,
                parent.FloatingHeight);
        }

        return getShellHostRect(parent.HostId);
    }
}
