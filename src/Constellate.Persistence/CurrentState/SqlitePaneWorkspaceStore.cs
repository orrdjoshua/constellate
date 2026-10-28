using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.CurrentState;

public sealed class SqlitePaneWorkspaceStore : IPaneWorkspaceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _databasePath;

    public SqlitePaneWorkspaceStore(PersistenceBootstrapResult bootstrapResult)
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

    public IReadOnlyList<PaneWorkspaceDescriptor> ListAll()
    {
        var workspaces = new List<PaneWorkspaceDescriptor>();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                descriptor_json
            FROM pane_workspace;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            workspaces.Add(ReadDescriptor(reader));
        }

        return workspaces
            .OrderBy(workspace => workspace.DisplayLabel, StringComparer.Ordinal)
            .ThenBy(workspace => workspace.WorkspaceId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGet(string workspaceId, out PaneWorkspaceDescriptor workspaceDefinition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        var normalizedWorkspaceId = workspaceId.Trim();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                descriptor_json
            FROM pane_workspace
            WHERE workspace_id = $workspaceId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$workspaceId", normalizedWorkspaceId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            workspaceDefinition = ReadDescriptor(reader);
            return true;
        }

        workspaceDefinition = null!;
        return false;
    }

    public void Upsert(PaneWorkspaceDescriptor workspaceDefinition)
    {
        ArgumentNullException.ThrowIfNull(workspaceDefinition);

        var normalizedWorkspaceId = workspaceDefinition.WorkspaceId.Trim();
        var createdAt = GetCreatedAt(normalizedWorkspaceId) ?? DateTimeOffset.UtcNow;
        var updatedAt = DateTimeOffset.UtcNow;
        var descriptorJson = JsonSerializer.Serialize(
            workspaceDefinition with { WorkspaceId = normalizedWorkspaceId },
            JsonOptions);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pane_workspace (
                workspace_id,
                descriptor_json,
                created_at_utc,
                updated_at_utc
            )
            VALUES (
                $workspaceId,
                $descriptorJson,
                $createdAtUtc,
                $updatedAtUtc
            )
            ON CONFLICT(workspace_id) DO UPDATE SET
                descriptor_json = excluded.descriptor_json,
                created_at_utc = excluded.created_at_utc,
                updated_at_utc = excluded.updated_at_utc;
            """;

        command.Parameters.AddWithValue("$workspaceId", normalizedWorkspaceId);
        command.Parameters.AddWithValue("$descriptorJson", descriptorJson);
        command.Parameters.AddWithValue("$createdAtUtc", createdAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updatedAtUtc", updatedAt.ToString("O", CultureInfo.InvariantCulture));

        command.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS pane_workspace (
                workspace_id TEXT PRIMARY KEY,
                descriptor_json TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private DateTimeOffset? GetCreatedAt(string workspaceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                created_at_utc
            FROM pane_workspace
            WHERE workspace_id = $workspaceId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$workspaceId", workspaceId);

        var value = command.ExecuteScalar();
        return value is string createdAtText
            ? DateTimeOffset.Parse(createdAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;
    }

    private static PaneWorkspaceDescriptor ReadDescriptor(SqliteDataReader reader)
    {
        var descriptorJson = reader.GetString(reader.GetOrdinal("descriptor_json"));
        return JsonSerializer.Deserialize<PaneWorkspaceDescriptor>(descriptorJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored pane workspace descriptor could not be deserialized.");
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
