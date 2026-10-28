using System;
using System.Globalization;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.CurrentState;

public sealed class SqliteEngineProjectStore
{
    public const string CurrentProjectVersion = "0.1";
    public const string CurrentEngineSchemaVersion = "1.0";

    private readonly string _databasePath;

    public SqliteEngineProjectStore(PersistenceBootstrapResult bootstrapResult)
    {
        ArgumentNullException.ThrowIfNull(bootstrapResult);
        _databasePath = bootstrapResult.DatabasePath;
        EnsureSchema();
    }

    public string DatabasePath => _databasePath;

    public void EnsureInitialized()
    {
        EnsureSchema();
    }

    public EngineProjectRecord CreateNew(string displayName, string? projectId = null)
    {
        var now = DateTimeOffset.UtcNow;

        var record = new EngineProjectRecord(
            ProjectId: string.IsNullOrWhiteSpace(projectId)
                ? $"world-{Guid.NewGuid():N}"
                : projectId.Trim(),
            DisplayName: string.IsNullOrWhiteSpace(displayName)
                ? "Untitled World"
                : displayName.Trim(),
            ProjectVersion: CurrentProjectVersion,
            EngineSchemaVersion: CurrentEngineSchemaVersion,
            CreatedAt: now,
            UpdatedAt: now);

        Upsert(record);
        return record;
    }

    public void Upsert(EngineProjectRecord projectRecord)
    {
        ArgumentNullException.ThrowIfNull(projectRecord);

        var createdAt = GetCreatedAt(projectRecord.ProjectId) ?? projectRecord.CreatedAt;
        var updatedAt = DateTimeOffset.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO engine_project (
                project_id,
                display_name,
                project_version,
                engine_schema_version,
                default_world_id,
                settings_profile_ref,
                created_at_utc,
                updated_at_utc
            )
            VALUES (
                $projectId,
                $displayName,
                $projectVersion,
                $engineSchemaVersion,
                $defaultWorldId,
                $settingsProfileRef,
                $createdAtUtc,
                $updatedAtUtc
            )
            ON CONFLICT(project_id) DO UPDATE SET
                display_name = excluded.display_name,
                project_version = excluded.project_version,
                engine_schema_version = excluded.engine_schema_version,
                default_world_id = excluded.default_world_id,
                settings_profile_ref = excluded.settings_profile_ref,
                created_at_utc = excluded.created_at_utc,
                updated_at_utc = excluded.updated_at_utc;
            """;

        command.Parameters.AddWithValue("$projectId", projectRecord.ProjectId);
        command.Parameters.AddWithValue("$displayName", projectRecord.DisplayName);
        command.Parameters.AddWithValue("$projectVersion", projectRecord.ProjectVersion);
        command.Parameters.AddWithValue("$engineSchemaVersion", projectRecord.EngineSchemaVersion);
        command.Parameters.AddWithValue("$defaultWorldId", (object?)projectRecord.DefaultWorldId ?? DBNull.Value);
        command.Parameters.AddWithValue("$settingsProfileRef", (object?)projectRecord.SettingsProfileRef ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", createdAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updatedAtUtc", updatedAt.ToString("O", CultureInfo.InvariantCulture));

        command.ExecuteNonQuery();
    }

    public bool TryGet(out EngineProjectRecord projectRecord)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                project_id,
                display_name,
                project_version,
                engine_schema_version,
                default_world_id,
                settings_profile_ref,
                created_at_utc,
                updated_at_utc
            FROM engine_project
            LIMIT 1;
            """;

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            projectRecord = ReadRecord(reader);
            return true;
        }

        projectRecord = null!;
        return false;
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS engine_project (
                project_id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                project_version TEXT NOT NULL,
                engine_schema_version TEXT NOT NULL,
                default_world_id TEXT NULL,
                settings_profile_ref TEXT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private DateTimeOffset? GetCreatedAt(string projectId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                created_at_utc
            FROM engine_project
            WHERE project_id = $projectId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$projectId", projectId);

        var value = command.ExecuteScalar();
        return value is string createdAtText
            ? DateTimeOffset.Parse(createdAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;
    }

    private static EngineProjectRecord ReadRecord(SqliteDataReader reader)
    {
        return new EngineProjectRecord(
            ProjectId: reader.GetString(reader.GetOrdinal("project_id")),
            DisplayName: reader.GetString(reader.GetOrdinal("display_name")),
            ProjectVersion: reader.GetString(reader.GetOrdinal("project_version")),
            EngineSchemaVersion: reader.GetString(reader.GetOrdinal("engine_schema_version")),
            CreatedAt: DateTimeOffset.Parse(
                reader.GetString(reader.GetOrdinal("created_at_utc")),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt: DateTimeOffset.Parse(
                reader.GetString(reader.GetOrdinal("updated_at_utc")),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            DefaultWorldId: ReadNullableString(reader, "default_world_id"),
            SettingsProfileRef: ReadNullableString(reader, "settings_profile_ref"));
    }

    private static string? ReadNullableString(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private SqliteConnection OpenConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}