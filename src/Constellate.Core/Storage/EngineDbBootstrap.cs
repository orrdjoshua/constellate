using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Constellate.Core.Storage
{
    /// <summary>
    /// Centralized schema bootstrap for engine.db.
    /// Applies the SPEC-020 v0.1 schema in an idempotent way using
    /// CREATE TABLE/INDEX IF NOT EXISTS. Later migrations can extend
    /// this helper while keeping the call sites stable.
    /// </summary>
    internal static class EngineDbBootstrap
    {
        public static void EnsureSchema(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path must be non-empty.", nameof(databasePath));

            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var connectionString = $"Data Source={databasePath}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            foreach (var sql in GetSchemaStatements())
            {
                var text = sql?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                using var command = connection.CreateCommand();
                command.CommandText = text;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// SPEC-020 v0.1 schema as a sequence of idempotent statements.
        /// </summary>
        private static string[] GetSchemaStatements() =>
        [
            // Core scene tables
            @"
CREATE TABLE IF NOT EXISTS Nodes (
    Id              TEXT PRIMARY KEY,
    Label           TEXT NOT NULL,

    PosX            REAL NOT NULL,
    PosY            REAL NOT NULL,
    PosZ            REAL NOT NULL,
    RotX            REAL NOT NULL,
    RotY            REAL NOT NULL,
    RotZ            REAL NOT NULL,
    ScaleX          REAL NOT NULL,
    ScaleY          REAL NOT NULL,
    ScaleZ          REAL NOT NULL,

    VisualScale     REAL NOT NULL,
    Phase           REAL NOT NULL,

    Primitive       TEXT NOT NULL,
    FillColor       TEXT NOT NULL,
    OutlineColor    TEXT NOT NULL,
    Opacity         REAL NOT NULL
);",
            @"
CREATE TABLE IF NOT EXISTS Links (
    Id              TEXT PRIMARY KEY,
    SourceId        TEXT NOT NULL,
    TargetId        TEXT NOT NULL,
    Kind            TEXT NOT NULL,
    Weight          REAL NOT NULL,

    StrokeColor     TEXT NOT NULL,
    StrokeThickness REAL NOT NULL,
    Opacity         REAL NOT NULL
);",
            @"
CREATE INDEX IF NOT EXISTS IX_Links_Source ON Links (SourceId);",
            @"
CREATE INDEX IF NOT EXISTS IX_Links_Target ON Links (TargetId);",
            @"
CREATE TABLE IF NOT EXISTS Groups (
    Id      TEXT PRIMARY KEY,
    Label   TEXT NOT NULL
);",
            @"
CREATE TABLE IF NOT EXISTS GroupMembers (
    GroupId TEXT NOT NULL,
    NodeId  TEXT NOT NULL,
    PRIMARY KEY (GroupId, NodeId)
);",
            @"
CREATE INDEX IF NOT EXISTS IX_GroupMembers_Node ON GroupMembers (NodeId);",
            @"
CREATE TABLE IF NOT EXISTS Bookmarks (
    Name                TEXT PRIMARY KEY,
    FocusedNodeId       TEXT NULL,

    FocusedPanelNodeId  TEXT NULL,
    FocusedPanelViewRef TEXT NULL,

    ViewYaw             REAL NULL,
    ViewPitch           REAL NULL,
    ViewDistance        REAL NULL,
    ViewTargetX         REAL NULL,
    ViewTargetY         REAL NULL,
    ViewTargetZ         REAL NULL
);",
            @"
CREATE TABLE IF NOT EXISTS BookmarkSelectedNodes (
    BookmarkName    TEXT NOT NULL,
    NodeId          TEXT NOT NULL,
    PRIMARY KEY (BookmarkName, NodeId)
);",
            @"
CREATE TABLE IF NOT EXISTS BookmarkSelectedPanels (
    BookmarkName    TEXT NOT NULL,
    NodeId          TEXT NOT NULL,
    ViewRef         TEXT NOT NULL,
    PRIMARY KEY (BookmarkName, NodeId, ViewRef)
);",
            @"
CREATE TABLE IF NOT EXISTS PanelAttachments (
    NodeId          TEXT PRIMARY KEY,
    ViewRef         TEXT NOT NULL,
    OffsetX         REAL NOT NULL,
    OffsetY         REAL NOT NULL,
    OffsetZ         REAL NOT NULL,
    SizeX           REAL NOT NULL,
    SizeY           REAL NOT NULL,
    Anchor          TEXT NOT NULL,
    IsVisible       INTEGER NOT NULL,

    SurfaceKind     TEXT NULL,
    PaneletteKind   TEXT NULL,
    PaneletteTier   INTEGER NULL
);",
            // Layouts, appearance profiles, mapping sets
            @"
CREATE TABLE IF NOT EXISTS PaneLayouts (
    Id      TEXT PRIMARY KEY,
    Kind    TEXT NOT NULL,
    Json    TEXT NOT NULL
);",
            @"
CREATE TABLE IF NOT EXISTS AppearanceProfiles (
    Id      TEXT PRIMARY KEY,
    Kind    TEXT NOT NULL,
    Json    TEXT NOT NULL
);",
            @"
CREATE TABLE IF NOT EXISTS MappingSets (
    Id      TEXT PRIMARY KEY,
    Kind    TEXT NOT NULL,
    Json    TEXT NOT NULL
);",
            // Plugin registrations
            @"
CREATE TABLE IF NOT EXISTS Plugins (
    Id              TEXT PRIMARY KEY,
    Version         TEXT NOT NULL,
    Enabled         INTEGER NOT NULL,
    ManifestJson    TEXT NULL,
    SettingsJson    TEXT NULL
);",
            // Small engine state key–value table
            @"
CREATE TABLE IF NOT EXISTS EngineState (
    Key     TEXT PRIMARY KEY,
    Value   TEXT NOT NULL
);"
        ];
    }
}
