namespace Constellate.Renderer.OpenTK.Scene
{
    public sealed record RenderSceneSnapshot(
        RenderNode[] Nodes,
        PanelSurfaceNode[] PanelSurfaces,
        RenderLink[] Links);
}
