namespace Constellate.Core.Scene
{
    public sealed record NodeAppearance(
        string Primitive = "triangle",
        string FillColor = "#FFFFFF",
        string OutlineColor = "#D7E8FF",
        float Opacity = 1.0f)
    {
        public static NodeAppearance Default { get; } = new();
    }
}
