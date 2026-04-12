using System.Numerics;

namespace Constellate.Core.Scene
{
    public sealed record PanelAttachment(
        NodeId NodeId,
        string ViewRef,
        Vector3 LocalOffset,
        Vector2 Size,
        string Anchor,
        bool IsVisible = true,
        PanelSurfaceSemantics? Semantics = null,
        PanelCommandSurfaceMetadata? CommandSurface = null);

    public readonly record struct PanelTarget(
        NodeId NodeId,
        string ViewRef)
    {
        public override string ToString() => $"{NodeId}:{ViewRef}";
    }
}
