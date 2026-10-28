using System;
using System.Collections.Generic;
using System.Globalization;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.CurrentState
{
    public sealed class SqliteEngineProjectionBindingStore : IEngineProjectionBindingStore
    {
        private readonly string _databasePath;

        public SqliteEngineProjectionBindingStore(PersistenceBootstrapResult bootstrapResult)
        {
            if (bootstrapResult is null)
            {
                throw new ArgumentNullException(nameof(bootstrapResult));
            }

            _databasePath = bootstrapResult.DatabasePath;
            EnsureSchema();
        }

        public string DatabasePath => _databasePath;

        public void EnsureInitialized()
        {
            EnsureSchema();
        }

        public IReadOnlyList<EngineProjectionBindingState> ListAll()
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    projection_binding_id,
                    resource_id,
                    resource_type_id,
                    surface_role,
                    view_ref,
                    projection_mode,
                    target_surface_kind,
                    binding_state,
                    target_kind,
                    target_id,
                    created_at_utc,
                    updated_at_utc
                FROM engine_projection_binding
                ORDER BY resource_id, projection_binding_id;
                """;

            var bindings = new List<EngineProjectionBindingState>();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                bindings.Add(ToState(reader));
            }

            return bindings;
        }

        public IReadOnlyList<EngineProjectionBindingState> ListByResource(string resourceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
            var normalizedResourceId = resourceId.Trim();

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    projection_binding_id,
                    resource_id,
                    resource_type_id,
                    surface_role,
                    view_ref,
                    projection_mode,
                    target_surface_kind,
                    binding_state,
                    target_kind,
                    target_id,
                    created_at_utc,
                    updated_at_utc
                FROM engine_projection_binding
                WHERE resource_id = $resourceId
                ORDER BY projection_binding_id;
                """;
            command.Parameters.AddWithValue("$resourceId", normalizedResourceId);

            var bindings = new List<EngineProjectionBindingState>();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                bindings.Add(ToState(reader));
            }

            return bindings;
        }

        public bool TryGet(string projectionBindingId, out EngineProjectionBindingState binding)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectionBindingId);
            var normalizedProjectionBindingId = projectionBindingId.Trim();

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    projection_binding_id,
                    resource_id,
                    resource_type_id,
                    surface_role,
                    view_ref,
                    projection_mode,
                    target_surface_kind,
                    binding_state,
                    target_kind,
                    target_id,
                    created_at_utc,
                    updated_at_utc
                FROM engine_projection_binding
                WHERE projection_binding_id = $projectionBindingId
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$projectionBindingId", normalizedProjectionBindingId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                binding = ToState(reader);
                return true;
            }

            binding = null!;
            return false;
        }

        public void Upsert(EngineProjectionBindingState binding)
        {
            ArgumentNullException.ThrowIfNull(binding);

            var projectionBindingId = NormalizeRequired(binding.ProjectionBindingId);
            var resourceId = NormalizeRequired(binding.ResourceId);
            var resourceTypeId = NormalizeOptional(binding.ResourceTypeId);
            var surfaceRole = NormalizeRequired(binding.SurfaceRole);
            var viewRef = NormalizeRequired(binding.ViewRef);
            var projectionMode = NormalizeRequired(binding.ProjectionMode);
            var targetSurfaceKind = NormalizeRequired(binding.TargetSurfaceKind);
            var bindingState = NormalizeRequired(binding.BindingState);
            var targetKind = NormalizeRequired(binding.TargetKind);
            var targetId = NormalizeOptional(binding.TargetId);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO engine_projection_binding (
                    projection_binding_id,
                    resource_id,
                    resource_type_id,
                    surface_role,
                    view_ref,
                    projection_mode,
                    target_surface_kind,
                    binding_state,
                    target_kind,
                    target_id,
                    created_at_utc,
                    updated_at_utc
                )
                VALUES (
                    $projectionBindingId,
                    $resourceId,
                    $resourceTypeId,
                    $surfaceRole,
                    $viewRef,
                    $projectionMode,
                    $targetSurfaceKind,
                    $bindingState,
                    $targetKind,
                    $targetId,
                    $createdAtUtc,
                    $updatedAtUtc
                )
                ON CONFLICT(projection_binding_id) DO UPDATE SET
                    resource_id = excluded.resource_id,
                    resource_type_id = excluded.resource_type_id,
                    surface_role = excluded.surface_role,
                    view_ref = excluded.view_ref,
                    projection_mode = excluded.projection_mode,
                    target_surface_kind = excluded.target_surface_kind,
                    binding_state = excluded.binding_state,
                    target_kind = excluded.target_kind,
                    target_id = excluded.target_id,
                    created_at_utc = excluded.created_at_utc,
                    updated_at_utc = excluded.updated_at_utc;
                """;

            command.Parameters.AddWithValue("$projectionBindingId", projectionBindingId);
            command.Parameters.AddWithValue("$resourceId", resourceId);
            command.Parameters.AddWithValue("$resourceTypeId", (object?)resourceTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$surfaceRole", surfaceRole);
            command.Parameters.AddWithValue("$viewRef", viewRef);
            command.Parameters.AddWithValue("$projectionMode", projectionMode);
            command.Parameters.AddWithValue("$targetSurfaceKind", targetSurfaceKind);
            command.Parameters.AddWithValue("$bindingState", bindingState);
            command.Parameters.AddWithValue("$targetKind", targetKind);
            command.Parameters.AddWithValue("$targetId", (object?)targetId ?? DBNull.Value);
            command.Parameters.AddWithValue("$createdAtUtc", binding.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updatedAtUtc", binding.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));

            command.ExecuteNonQuery();
        }

        private void EnsureSchema()
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS engine_projection_binding (
                    projection_binding_id TEXT PRIMARY KEY,
                    resource_id TEXT NOT NULL,
                    resource_type_id TEXT NULL,
                    surface_role TEXT NOT NULL,
                    view_ref TEXT NOT NULL,
                    projection_mode TEXT NOT NULL,
                    target_surface_kind TEXT NOT NULL,
                    binding_state TEXT NOT NULL,
                    target_kind TEXT NOT NULL,
                    target_id TEXT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_engine_projection_binding_resource_id
                    ON engine_projection_binding(resource_id);

                CREATE INDEX IF NOT EXISTS idx_engine_projection_binding_binding_state
                    ON engine_projection_binding(binding_state);
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

        private static EngineProjectionBindingState ToState(SqliteDataReader reader)
        {
            return new EngineProjectionBindingState(
                reader.GetString(reader.GetOrdinal("projection_binding_id")),
                reader.GetString(reader.GetOrdinal("resource_id")),
                ReadOptionalString(reader, "resource_type_id"),
                reader.GetString(reader.GetOrdinal("surface_role")),
                reader.GetString(reader.GetOrdinal("view_ref")),
                reader.GetString(reader.GetOrdinal("projection_mode")),
                reader.GetString(reader.GetOrdinal("target_surface_kind")),
                reader.GetString(reader.GetOrdinal("binding_state")),
                reader.GetString(reader.GetOrdinal("target_kind")),
                ReadOptionalString(reader, "target_id"),
                ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("created_at_utc"))),
                ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("updated_at_utc"))));
        }

        private static string? ReadOptionalString(SqliteDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static DateTimeOffset ParseDateTimeOffset(string value)
        {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static string NormalizeRequired(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return value.Trim();
        }

        private static string? NormalizeOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
    }
}
