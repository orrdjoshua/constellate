using System;

namespace Constellate.Core.Storage
{
    public sealed record EngineProjectionBindingState(
        string ProjectionBindingId,
        string ResourceId,
        string? ResourceTypeId,
        string SurfaceRole,
        string ViewRef,
        string ProjectionMode,
        string TargetSurfaceKind,
        string BindingState,
        string TargetKind,
        string? TargetId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public bool IsActive =>
            string.Equals(BindingState, "active", StringComparison.OrdinalIgnoreCase);
    }
}
