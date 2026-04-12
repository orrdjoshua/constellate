namespace Constellate.Core.Scene
{
    /// <summary>
    /// Core appearance descriptor for links in the scene. Phase A keeps this
    /// minimal and defaults it to values consistent with the current renderer
    /// placeholder path; later phases may make this settings- or data-driven.
    /// </summary>
    public sealed record LinkAppearance(
        string StrokeColor = "#7DD3FC",
        float StrokeThickness = 1.5f,
        float Opacity = 0.86f)
    {
        public static LinkAppearance Default { get; } = new();
    }
}
