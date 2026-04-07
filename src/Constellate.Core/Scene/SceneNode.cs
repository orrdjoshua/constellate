namespace Constellate.Core.Scene
{
    public sealed record SceneNode(
        NodeId Id,
        string Label,
        Transform Transform,
        float VisualScale = 1.0f,
        float Phase = 0.0f);
}
