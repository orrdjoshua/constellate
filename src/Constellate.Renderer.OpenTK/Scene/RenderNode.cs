using OpenTK.Mathematics;

namespace Constellate.Renderer.OpenTK.Scene
{
    public readonly record struct RenderNode(
        string Id,
        string Label,
        Vector3 Position,
        Vector3 RotationEuler,
        Vector3 Scale,
        float VisualScale,
        float Phase,
        bool IsFocused,
        bool IsSelected,
        string Primitive = "triangle",
        string FillColor = "#FFFFFF",
        string OutlineColor = "#D7E8FF",
        float Opacity = 1.0f);
}
