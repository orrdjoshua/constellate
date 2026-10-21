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

    public string ReadoutTitle =>
        !string.IsNullOrWhiteSpace(Title)
            ? Title!
            : !string.IsNullOrWhiteSpace(DisplayLabel)
                ? DisplayLabel!
                : "Bound Resource";

    public string ReadoutSubtitle
    {
        get
        {
            var subtitle = string.Empty;

            if (!string.IsNullOrWhiteSpace(SurfaceRole))
            {
                subtitle = SurfaceRole!;
            }

            if (!string.IsNullOrWhiteSpace(ViewRef))
            {
                subtitle = string.IsNullOrWhiteSpace(subtitle)
                    ? ViewRef!
                    : $"{subtitle} · {ViewRef}";
            }

            return string.IsNullOrWhiteSpace(subtitle)
                ? "Pane is bound to a resource context."
                : subtitle;
        }
    }

    public static ChildPaneResourceContext? CreateDetailChildPaneBody(
        string? surfaceRole,
        string? viewRef,
        string? displayLabel = null,
        string? title = null,
        string? resourceId = null,
        string? resourceTypeId = null,
        string? projectionMode = PaneSurfaceBinding.ProjectionModeDetail,
        string? targetSurfaceKind = PaneSurfaceBinding.TargetSurfaceKindChildPaneBody)
    {
        var surfaceBinding = PaneSurfaceBinding.CreateResourceSurface(
            surfaceRole,
            viewRef,
            projectionMode,
            targetSurfaceKind);

        if (surfaceBinding is null &&
            string.IsNullOrWhiteSpace(resourceId) &&
            string.IsNullOrWhiteSpace(resourceTypeId) &&
            string.IsNullOrWhiteSpace(displayLabel) &&
            string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new ChildPaneResourceContext(
            ResourceId: NormalizeOptional(resourceId),
            ResourceTypeId: NormalizeOptional(resourceTypeId),
            DisplayLabel: NormalizeOptional(displayLabel),
            Title: NormalizeOptional(title),
            ViewRef: surfaceBinding?.ViewRef ?? NormalizeOptional(viewRef),
            SurfaceRole: surfaceBinding?.SurfaceRole ?? NormalizeOptional(surfaceRole),
            ProjectionMode: surfaceBinding?.ProjectionMode ?? NormalizeOptional(projectionMode) ?? PaneSurfaceBinding.ProjectionModeDetail,
            TargetSurfaceKind: surfaceBinding?.TargetSurfaceKind ?? NormalizeOptional(targetSurfaceKind) ?? PaneSurfaceBinding.TargetSurfaceKindChildPaneBody);
    }

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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
