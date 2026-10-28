using System;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.Archive
{
    public sealed class SqliteRuntimeArchiveStore : IRuntimeArchiveStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SqliteRuntimeArchiveStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public bool IsInitialized { get; private set; }

        public void EnsureInitialized()
        {
            if (IsInitialized)
            {
                return;
            }

            using var connection = new SqliteConnection($"Data Source={DatabasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS action_invocations (
    action_invocation_id TEXT PRIMARY KEY,
    action_id TEXT NOT NULL,
    action_class TEXT NOT NULL,
    owner_kind TEXT NOT NULL,
    target_domain TEXT NOT NULL,
    target_ref TEXT NOT NULL,
    invocation_channel TEXT NOT NULL,
    result_state TEXT NOT NULL,
    started_at TEXT NOT NULL,
    completed_at TEXT NULL,
    correlation_id TEXT NULL,
    failure_summary TEXT NULL
);

CREATE TABLE IF NOT EXISTS runtime_stream_events (
    runtime_stream_event_id TEXT PRIMARY KEY,
    runtime_stream_id TEXT NOT NULL,
    sequence_no INTEGER NOT NULL,
    event_kind TEXT NOT NULL,
    severity TEXT NOT NULL,
    emitted_at TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    correlation_id TEXT NULL,
    retention_class TEXT NULL
);";
            command.ExecuteNonQuery();
            IsInitialized = true;
        }
    }
}
