using System;

namespace Constellate.Persistence.CurrentState
{
    /// <summary>
    /// Current-state row shape for project-level identity and version metadata.
    /// This helps mark the intended durable project-truth lane, but in the reviewed
    /// slice it still reads more like schema groundwork than a fully exercised
    /// store-backed seam.
    /// </summary>
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
