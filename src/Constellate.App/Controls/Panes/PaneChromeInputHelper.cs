using Avalonia.Controls;

namespace Constellate.App.Controls.Panes
{
    internal static class PaneChromeInputHelper
    {
        public static PaneChromeRegion ResolveRegion(object? sender)
        {
            return sender is Control control
                ? ResolveRegion(control.Name)
                : PaneChromeRegion.None;
        }

        public static PaneChromeRegion ResolveRegion(string? controlName)
        {
            return controlName switch
            {
                "ParentLabelArea" or "ChildLabelArea" => PaneChromeRegion.Label,
                "ParentEmptyHeaderArea" or "ChildEmptyHeaderArea" => PaneChromeRegion.EmptyHeader,
                "ChildBodyDragArea" => PaneChromeRegion.BodyEmptySurface,
                _ => PaneChromeRegion.None
            };
        }
    }
}
