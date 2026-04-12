namespace Constellate.Core.Scene
{
    /// <summary>
    /// Core appearance descriptor for groups in the scene. Phase A keeps this
    /// minimal and defaults it to values that align with the current overlay
    /// placeholder palette and EngineSettings.GroupOverlayOpacity.
    /// </summary>
    public sealed record GroupAppearance(
        string FillColor = "#14B478",
        string OutlineColor = "#14C88C",
        float Opacity = 0.20f,
        float Padding = 0.10f)
    {
        public static GroupAppearance Default { get; } = new();
    }
}
