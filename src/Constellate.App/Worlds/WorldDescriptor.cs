using System;

namespace Constellate.App.Worlds
{
    /// <summary>
    /// App-level description of a single Constellate world, regardless of whether it is
    /// currently loaded. This matches the PLAN-129 intent:
    ///
    /// - Each world is one concrete project instance backed by a single SQLite database file.
    /// - Worlds may live under one of several configured DB roots ("world libraries").
    /// - The world-selection UI and in-shell world overlay should reason about worlds through
    ///   this descriptor rather than directly against file paths or raw bootstrap results.
    ///
    /// This type is deliberately App-facing rather than engine-internal:
    ///
    /// - EngineWorldSession / EngineHost expose the runtime "current world" identity.
    /// - WorldDescriptor represents the broader catalogue of known/available worlds that a
    ///   startup selector or overlay can present to the user (including compatibility state).
    ///
    /// Later phases will populate CreatedAt/LastOpenedAt/EngineSchemaVersion/ProjectVersion
    /// from EngineProjectRecord and schema-metadata reads once those lanes are fully wired.
    /// </summary>
    internal sealed record WorldDescriptor(
        /// <summary>
        /// Stable logical identifier for the world (e.g., from EngineProjectRecord.ProjectId).
        /// Not tied to the absolute database file path so worlds can be moved between roots.
        /// </summary>
        string WorldId,
        /// <summary>
        /// User-facing display name for the world (e.g., "Personal Notes" or a client name).
        /// </summary>
        string DisplayName,
        /// <summary>
        /// Absolute path to the SQLite database file backing this world.
        /// </summary>
        string DatabaseFilePath,
        /// <summary>
        /// Identifier for the DB-root this world belongs to (e.g., "default", "personal").
        /// For ad-hoc loaded worlds this may be a synthetic id such as "ad-hoc".
        /// </summary>
        string RootId,
        /// <summary>
        /// Optional extended description for UI surfaces that want longer world summaries.
        /// </summary>
        string? Description,
        /// <summary>
        /// Timestamp when this world was first created, if known.
        /// </summary>
        DateTimeOffset? CreatedAt,
        /// <summary>
        /// Timestamp when this world was last opened, if known.
        /// </summary>
        DateTimeOffset? LastOpenedAt,
        /// <summary>
        /// Engine/schema version the world was created or last migrated to, if recorded.
        /// Used for compatibility checks and upgrade prompts.
        /// </summary>
        string? EngineSchemaVersion,
        /// <summary>
        /// Optional project-level version marker (e.g., for future user-facing versioning).
        /// </summary>
        string? ProjectVersion,
        /// <summary>
        /// Whether this descriptor represents a world that has just been created and has
        /// not yet been opened. Used by the world-selection experience to highlight new
        /// worlds and by persistence to distinguish seeding vs migrate-only behavior.
        /// </summary>
        bool IsNew,
        /// <summary>
        /// Whether the current engine/runtime can safely open this world. Worlds discovered
        /// on disk may be marked incompatible if their schema is too old or too new.
        /// </summary>
        bool IsCompatible,
        /// <summary>
        /// Whether this world should be treated as read-only (e.g., because of file-system
        /// permissions, explicit user choice, or incompatible schema).
        /// </summary>
        bool IsReadOnly);
}
