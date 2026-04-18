using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Constellate.App.Controls;

namespace Constellate.App.Infrastructure.Panes.Gestures;

internal static class ChildPaneDragStateResolver
{
    public static string? ResolveOriginHostId(
        MainWindowViewModel? vm,
        ChildPaneDescriptor descriptor)
    {
        return vm?.GetHostIdForChildPane(descriptor.Id);
    }

    public static Size ResolveOriginPreviewSize(
        Control originControl,
        ChildPaneDescriptor descriptor)
    {
        var childView = originControl as ChildPaneView ?? originControl.FindAncestorOfType<ChildPaneView>();
        if (childView is not null)
        {
            var bounds = childView.Bounds;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                return new Size(
                    Math.Max(80.0, bounds.Width),
                    Math.Max(80.0, bounds.Height));
            }
        }

        return new Size(
            Math.Max(80.0, descriptor.FloatingWidth),
            Math.Max(80.0, descriptor.FloatingHeight));
    }
}
