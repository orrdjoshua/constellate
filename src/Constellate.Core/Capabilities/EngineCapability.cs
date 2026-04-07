namespace Constellate.Core.Capabilities
{
    public sealed record EngineCapability(
        string Key,
        string DisplayName,
        string Category,
        string Provider,
        string Version);
}
