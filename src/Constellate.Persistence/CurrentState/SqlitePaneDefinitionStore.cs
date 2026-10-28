using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.CurrentState;

public sealed class SqlitePaneDefinitionStore : IPaneDefinitionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _databasePath;

    public SqlitePaneDefinitionStore(PersistenceBootstrapResult bootstrapResult)
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

    public IReadOnlyList<PaneDefinitionDescriptor> ListAll()
    {
        var definitions = new List<PaneDefinitionDescriptor>();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                descriptor_json
            FROM pane_definition;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            definitions.Add(ReadDescriptor(reader));
        }

        return definitions
            .OrderBy(definition => definition.DisplayLabel, StringComparer.Ordinal)
            .ThenBy(definition => definition.PaneDefinitionId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGet(string paneDefinitionId, out PaneDefinitionDescriptor paneDefinition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paneDefinitionId);
        var normalizedPaneDefinitionId = paneDefinitionId.Trim();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                descriptor_json
            FROM pane_definition
            WHERE pane_definition_id = $paneDefinitionId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$paneDefinitionId", normalizedPaneDefinitionId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            paneDefinition = ReadDescriptor(reader);
            return true;
        }

        paneDefinition = null!;
        return false;
    }

    public void Upsert(PaneDefinitionDescriptor paneDefinition)
    {
        ArgumentNullException.ThrowIfNull(paneDefinition);

        var normalizedPaneDefinitionId = paneDefinition.PaneDefinitionId.Trim();
        var createdAt = GetCreatedAt(normalizedPaneDefinitionId) ?? DateTimeOffset.UtcNow;
        var updatedAt = DateTimeOffset.UtcNow;
        var descriptorJson = JsonSerializer.Serialize(
            paneDefinition with { PaneDefinitionId = normalizedPaneDefinitionId },
            JsonOptions);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pane_definition (
                pane_definition_id,
                descriptor_json,
                created_at_utc,
                updated_at_utc
            )
            VALUES (
                $paneDefinitionId,
                $descriptorJson,
                $createdAtUtc,
                $updatedAtUtc
            )
            ON CONFLICT(pane_definition_id) DO UPDATE SET
                descriptor_json = excluded.descriptor_json,
                created_at_utc = excluded.created_at_utc,
                updated_at_utc = excluded.updated_at_utc;
            """;

        command.Parameters.AddWithValue("$paneDefinitionId", normalizedPaneDefinitionId);
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
            CREATE TABLE IF NOT EXISTS pane_definition (
                pane_definition_id TEXT PRIMARY KEY,
                descriptor_json TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private DateTimeOffset? GetCreatedAt(string paneDefinitionId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                created_at_utc
            FROM pane_definition
            WHERE pane_definition_id = $paneDefinitionId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$paneDefinitionId", paneDefinitionId);

        var value = command.ExecuteScalar();
        return value is string createdAtText
            ? DateTimeOffset.Parse(createdAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;
    }

    private static PaneDefinitionDescriptor ReadDescriptor(SqliteDataReader reader)
    {
        var descriptorJson = reader.GetString(reader.GetOrdinal("descriptor_json"));
        return JsonSerializer.Deserialize<PaneDefinitionDescriptor>(descriptorJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored pane definition descriptor could not be deserialized.");
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
