namespace Constellate.Core.Scene
{
    public sealed record SceneLink(
        string Id,
        NodeId SourceId,
        NodeId TargetId,
        string Kind,
        float Weight = 1.0f,
        LinkAppearance? Appearance = null);
}
