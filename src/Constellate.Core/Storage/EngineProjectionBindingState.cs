using System;

namespace Constellate.Core.Storage
{
    /// <summary>
    /// Core current-state shape for engine-visible projection/binding identity.
    /// This keeps resource/surface binding truth narrow and explicit without
    /// collapsing it into world-summary node records or App-side layout/session state.
    /// </summary>
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
