namespace Constellate.App;

public sealed record ChildPaneResourceContext(
    string? ResourceId = null,
    string? ResourceTypeId = null,
    string? DisplayLabel = null,
    string? Title = null,
    string? ViewRef = null,
    string? SurfaceRole = null);
