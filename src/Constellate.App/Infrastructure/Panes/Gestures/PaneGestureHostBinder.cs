using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Constellate.App.Infrastructure.Panes.Gestures;

internal static class PaneGestureHostBinder
{
    public static void BindWindowGlobalHandlers(
        Window window,
        EventHandler<PointerEventArgs> onMoved,
        EventHandler<PointerReleasedEventArgs> onReleased)
    {
        window.PointerMoved -= onMoved;
        window.PointerMoved += onMoved;

        window.PointerReleased -= onReleased;
        window.PointerReleased += onReleased;
    }

    public static void BindGrip(
        Window window,
        string name,
        string tag,
        EventHandler<PointerPressedEventArgs> onPressed,
        EventHandler<PointerReleasedEventArgs> onReleased,
        EventHandler<PointerEventArgs> onMoved,
        EventHandler<PointerCaptureLostEventArgs> onCaptureLost)
    {
        var grip = window.FindControl<Border>(name);
        if (grip is null)
        {
            return;
        }

        grip.Tag = tag;

        grip.PointerPressed -= onPressed;
        grip.PointerPressed += onPressed;

        grip.PointerReleased -= onReleased;
        grip.PointerReleased += onReleased;

        grip.PointerMoved -= onMoved;
        grip.PointerMoved += onMoved;

        grip.PointerCaptureLost -= onCaptureLost;
        grip.PointerCaptureLost += onCaptureLost;
    }

    public static bool TryBeginPressedPaneDrag(
        object? paneDataContext,
        object? sender,
        PointerPressedEventArgs e,
        Func<object?, PointerPressedEventArgs, bool> tryBeginParentDrag,
        Func<object?, PointerPressedEventArgs, bool> tryBeginChildDrag)
    {
        return paneDataContext switch
        {
            ParentPaneModel => tryBeginParentDrag(sender, e),
            ChildPaneDescriptor => tryBeginChildDrag(sender, e),
            _ => false
        };
    }
}
