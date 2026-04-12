using OpenTK.Mathematics;
using Constellate.Core.Scene;

namespace Constellate.Renderer.OpenTK.Scene
{
    public readonly record struct PanelSurfaceNode(
        string NodeId,
        string ViewRef,
        Vector3 LocalOffset,
        Vector2 Size,
        string Anchor,
        bool IsVisible,
        bool IsFocused,
        bool IsSelected,
        PanelSurfaceSemantics? Semantics = null,
        PanelCommandSurfaceMetadata? CommandSurface = null);
}
