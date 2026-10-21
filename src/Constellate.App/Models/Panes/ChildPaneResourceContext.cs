namespace Constellate.App;

/// <summary>
/// App-side junction between resource identity metadata and child-pane surface realization.
/// It usefully packages the current resource-to-surface bridge for pane consumers, but it
/// also reflects that part of the narrow binding vocabulary is still mirrored locally
/// instead of being fully converged on one shared contract seam.
/// </summary>
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
