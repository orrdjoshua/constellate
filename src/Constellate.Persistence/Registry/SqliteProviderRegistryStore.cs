using System;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.Registry
{
    public sealed class SqliteProviderRegistryStore : IProviderRegistryStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SqliteProviderRegistryStore(PersistenceBootstrapResult bootstrapResult)
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
CREATE TABLE IF NOT EXISTS provider_registrations (
    registration_id TEXT PRIMARY KEY,
    provider_id TEXT NOT NULL,
    origin TEXT NOT NULL,
    provider_kind TEXT NOT NULL,
    installed_version TEXT NOT NULL,
    state TEXT NOT NULL,
    compatibility_result TEXT NOT NULL,
    trust_class TEXT NOT NULL,
    enabled INTEGER NOT NULL,
    registered_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    package_id TEXT NULL,
    manifest_hash TEXT NULL
);";
            command.ExecuteNonQuery();
            IsInitialized = true;
        }
    }
}
