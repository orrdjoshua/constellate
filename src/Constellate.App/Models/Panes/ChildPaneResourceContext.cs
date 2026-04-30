namespace Constellate.App;

public sealed record ChildPaneResourceContext(
    string? ResourceId = null,
    string? ResourceTypeId = null,
    string? DisplayLabel = null,
    string? Title = null,
    string? ViewRef = null,
    string? SurfaceRole = null,
    string? ProjectionMode = PaneSurfaceBinding.ProjectionModeDetail,
    string? TargetSurfaceKind = PaneSurfaceBinding.TargetSurfaceKindChildPaneBody)
{
    public PaneSurfaceBinding? SurfaceBinding =>
        PaneSurfaceBinding.CreateResourceSurface(
            SurfaceRole,
            ViewRef,
            ProjectionMode,
            TargetSurfaceKind);

    public string BindingKey => SurfaceBinding?.BindingKey ?? string.Empty;
}
