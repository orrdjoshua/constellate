using System;
using System.Text.Json;
using Constellate.Core.Scene;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.CurrentState
{
    public sealed class SqliteEngineStateStore : IEngineStateStore
    {
        private const int SingletonSnapshotId = 1;

        private readonly string _databasePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public SqliteEngineStateStore(PersistenceBootstrapResult bootstrapResult)
        {
            if (bootstrapResult is null)
            {
                throw new ArgumentNullException(nameof(bootstrapResult));
            }

            _databasePath = bootstrapResult.DatabasePath;
            EnsureSchema();
        }

        public string DatabasePath => _databasePath;

        public bool HasPersistedSnapshot()
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS(
                    SELECT 1
                    FROM engine_snapshot
                    WHERE id = $id
                    LIMIT 1
                );
                """;
            command.Parameters.AddWithValue("$id", SingletonSnapshotId);

            var result = command.ExecuteScalar();
            return Convert.ToInt64(result) == 1L;
        }

        public SceneSnapshot? LoadSnapshot()
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT snapshot_json
                FROM engine_snapshot
                WHERE id = $id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$id", SingletonSnapshotId);

            var json = command.ExecuteScalar() as string;
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SceneSnapshot>(json, _jsonOptions);
        }

        public void SaveSnapshot(SceneSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO engine_snapshot (
                    id,
                    snapshot_json,
                    updated_at_utc
                )
                VALUES (
                    $id,
                    $snapshotJson,
                    $updatedAtUtc
                )
                ON CONFLICT(id) DO UPDATE SET
                    snapshot_json = excluded.snapshot_json,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$id", SingletonSnapshotId);
            command.Parameters.AddWithValue("$snapshotJson", json);
            command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }

        private void EnsureSchema()
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS engine_snapshot (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    snapshot_json TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
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
}
