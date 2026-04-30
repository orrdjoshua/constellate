using System;

namespace Constellate.SDK
{
    public sealed record ResourceSurfaceBindingPayload(
        string BindingKind,
        string SurfaceRole,
        string ViewRef,
        string ProjectionMode,
        string TargetSurfaceKind)
    {
        public const string BindingKindResourceSurface = "resource_surface";

        public const string ProjectionModeSummary = "summary";
        public const string ProjectionModeDetail = "detail";
        public const string ProjectionModeMonitor = "monitor";
        public const string ProjectionModeCommand = "command";

        public const string TargetSurfaceKindWorldNode = "world_node";
        public const string TargetSurfaceKindChildPaneBody = "child_pane_body";
        public const string TargetSurfaceKindChildPaneChrome = "child_pane_chrome";

        public string BindingKey =>
            $"{BindingKind}:{ProjectionMode}:{TargetSurfaceKind}:{SurfaceRole}:{ViewRef}";

        public static ResourceSurfaceBindingPayload? Create(
            string? surfaceRole,
            string? viewRef,
            string? projectionMode,
            string? targetSurfaceKind,
            string bindingKind = BindingKindResourceSurface)
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

            return new ResourceSurfaceBindingPayload(
                bindingKind,
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

    public sealed record ResourceSurfaceBindingChangedPayload(
        string ResourceId,
        string? ResourceTypeId,
        string? ResourceDisplayLabel,
        string? ResourceTitle,
        ResourceSurfaceBindingPayload Binding,
        string BindingState = "active",
        string? TargetId = null,
        string? WorldAssignmentState = null)
    {
        public string BindingKey => Binding.BindingKey;

        public bool IsActive =>
            string.Equals(BindingState, "active", StringComparison.OrdinalIgnoreCase);
    }
}
