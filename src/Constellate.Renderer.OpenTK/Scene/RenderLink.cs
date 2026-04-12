using Constellate.Core.Scene;

namespace Constellate.Renderer.OpenTK.Scene
{
    public readonly record struct RenderLink(
        string Id,
        string SourceId,
        string TargetId,
        string Kind,
        float Weight,
        LinkAppearance? Appearance);
}
