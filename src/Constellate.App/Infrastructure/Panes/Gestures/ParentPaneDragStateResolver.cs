using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Constellate.App.Infrastructure.Panes;

namespace Constellate.App.Infrastructure.Panes.Gestures;

internal static class ParentPaneDragStateResolver
{
    public static string? ResolveHostIdFromHostControl(Control? control)
    {
        return control?.Name switch
        {
            "LeftPaneHost" => "left",
            "TopPaneHost" => "top",
            "RightPaneHost" => "right",
            "BottomPaneHost" => "bottom",
            "FloatingPaneHost" => "floating",
            _ => null
        };
    }

    public static ParentPaneModel? TryResolveDragParentPane(
        MainWindowViewModel? vm,
        string? originHostOrPaneId)
    {
        if (vm is null || string.IsNullOrWhiteSpace(originHostOrPaneId))
        {
            return null;
        }

        return vm.ParentPaneModels.FirstOrDefault(parent =>
                   string.Equals(parent.Id, originHostOrPaneId, StringComparison.Ordinal)) ??
               vm.ParentPaneModels.FirstOrDefault(parent =>
                   string.Equals(
                       MainWindowViewModel.NormalizeHostId(parent.HostId),
                       MainWindowViewModel.NormalizeHostId(originHostOrPaneId),
                       StringComparison.Ordinal));
    }

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

    public static DockAttachmentModel ResolveOriginAttachment(
        ParentPaneMoveSession? activeSession,
        ParentPaneModel? resolvedParent,
        string? dragOriginHostId)
    {
        if (activeSession is not null)
        {
            return activeSession.OriginAttachment;
        }

        return resolvedParent is not null
            ? DockAttachmentModel.FromHostId(resolvedParent.HostId)
            : DockAttachmentModel.FromHostId(dragOriginHostId);
    }
}
