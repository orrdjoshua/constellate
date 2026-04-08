namespace Constellate.Renderer.OpenTK.Scene
{
    public readonly record struct RenderGroup(
        string Id,
        string Label,
        string[] NodeIds);
}
