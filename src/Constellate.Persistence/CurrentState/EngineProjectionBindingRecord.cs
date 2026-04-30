using System;

namespace Constellate.Persistence.CurrentState
{
    /// <summary>
    /// Thin current-state seam for durable resource-surface binding identity.
    /// This intentionally stays separate from:
    /// - EngineNodeRecord, which should remain the world-summary/node anchor
    /// - App-side shell layout snapshots, which are session/UI layout concerns
    ///
    /// The goal is to persist the minimum truthful projection/binding identity
    /// needed for summary/detail/monitor/command surfaces without prematurely
    /// committing to the fuller later pane-definition/workspace schema.
    /// </summary>
    public sealed record EngineProjectionBindingRecord(
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
