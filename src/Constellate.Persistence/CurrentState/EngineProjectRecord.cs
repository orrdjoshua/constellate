using System;

namespace Constellate.Persistence.CurrentState
{
    public sealed record EngineProjectRecord(
        string ProjectId,
        string DisplayName,
        string ProjectVersion,
        string EngineSchemaVersion,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        string? DefaultWorldId = null,
        string? SettingsProfileRef = null);
}
