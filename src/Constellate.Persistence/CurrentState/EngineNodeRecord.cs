using System;

namespace Constellate.Persistence.CurrentState
{
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
