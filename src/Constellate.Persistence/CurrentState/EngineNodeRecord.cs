using System;

namespace Constellate.Persistence.CurrentState
{
    /// <summary>
    /// Adjacent current-state row shape for world-summary/node-anchor truth.
    /// This helps mark the intended durable node lane, but in the reviewed slice
    /// it still reads more like schema groundwork than a fully clarified store-backed
    /// seam when compared with the cleaner projection-binding lane.
    /// </summary>
    public sealed record EngineNodeRecord(
        string NodeId,
        string? ResourceId,
        string NodeKind,
        string DisplayLabel,
        float PositionX,
        float PositionY,
        float PositionZ,
        float ScaleX,
        float ScaleY,
        float ScaleZ,
        string VisibilityState,
        bool IsCollapsed,
        bool IsExpanded,
        bool IsEnterable,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
