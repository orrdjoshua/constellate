namespace Constellate.App;

/// <summary>
/// App-local mirror of the narrow resource-surface binding vocabulary currently used for
/// child-pane realization. This is a practical near-term seam, but because it closely
/// matches the SDK/Core binding vocabulary it is also a likely later convergence target
/// to reduce duplication and drift.
/// </summary>
public sealed record PaneSurfaceBinding(
    string BindingKind,
    string SurfaceRole,
    string ViewRef,
    string ProjectionMode,
    string TargetSurfaceKind)
{
    public const string ResourceSurfaceBindingKind = "resource_surface";
    public const string ProjectionModeSummary = "summary";
    public const string ProjectionModeDetail = "detail";
    public const string ProjectionModeMonitor = "monitor";
    public const string ProjectionModeCommand = "command";

    public const string TargetSurfaceKindChildPaneBody = "child_pane_body";
    public const string TargetSurfaceKindChildPaneChrome = "child_pane_chrome";

    public string BindingKey =>
        $"{BindingKind}:{ProjectionMode}:{TargetSurfaceKind}:{SurfaceRole}:{ViewRef}";

    public static PaneSurfaceBinding? CreateResourceSurface(
        string? surfaceRole,
        string? viewRef,
        string? projectionMode = ProjectionModeDetail,
        string? targetSurfaceKind = TargetSurfaceKindChildPaneBody)
    {
        var normalizedSurfaceRole = Normalize(surfaceRole);
        var normalizedViewRef = Normalize(viewRef);
        if (string.IsNullOrWhiteSpace(normalizedSurfaceRole) ||
            string.IsNullOrWhiteSpace(normalizedViewRef))
        {
            return null;
        }

        var normalizedProjectionMode = Normalize(projectionMode, ProjectionModeDetail);
        var normalizedTargetSurfaceKind = Normalize(targetSurfaceKind, TargetSurfaceKindChildPaneBody);

        return new PaneSurfaceBinding(
            ResourceSurfaceBindingKind,
            normalizedSurfaceRole,
            normalizedViewRef,
            normalizedProjectionMode,
            normalizedTargetSurfaceKind);
    }

    private static string Normalize(string? value, string fallback = "")
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? fallback
            : normalized;
    }
}
