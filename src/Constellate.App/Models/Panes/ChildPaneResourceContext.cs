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

    public ChildPaneResourceContext MergeWith(ChildPaneResourceContext? fallback)
    {
        if (fallback is null)
        {
            return this;
        }

        return this with
        {
            ResourceId = ResolvePreferredValue(ResourceId, fallback.ResourceId),
            ResourceTypeId = ResolvePreferredValue(ResourceTypeId, fallback.ResourceTypeId),
            DisplayLabel = ResolvePreferredValue(DisplayLabel, fallback.DisplayLabel),
            Title = ResolvePreferredValue(Title, fallback.Title),
            ViewRef = ResolvePreferredValue(ViewRef, fallback.ViewRef),
            SurfaceRole = ResolvePreferredValue(SurfaceRole, fallback.SurfaceRole),
            ProjectionMode = ResolvePreferredValue(ProjectionMode, fallback.ProjectionMode),
            TargetSurfaceKind = ResolvePreferredValue(TargetSurfaceKind, fallback.TargetSurfaceKind)
        };
    }

    private static string? ResolvePreferredValue(string? primary, string? fallback)
    {
        return !string.IsNullOrWhiteSpace(primary)
            ? primary.Trim()
            : string.IsNullOrWhiteSpace(fallback)
                ? null
                : fallback.Trim();
    }
}
