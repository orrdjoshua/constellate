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
        float Phase);
}
