using System;
using System.Collections.Generic;
using System.Globalization;
using Constellate.Core.Resources;
using Constellate.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Constellate.Persistence.Registry
{
    public sealed class SqliteResourceRegistryStore : IResourceRegistryStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SqliteResourceRegistryStore(PersistenceBootstrapResult bootstrapResult)
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

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS resource_registrations (
    resource_id TEXT PRIMARY KEY,
    type_id TEXT NOT NULL,
    family TEXT NOT NULL,
    provider_id TEXT NOT NULL,
    origin TEXT NOT NULL,
    effective_posture TEXT NOT NULL,
    authority_mode TEXT NOT NULL,
    locality TEXT NOT NULL,
    lifecycle_state TEXT NOT NULL,
    display_label TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    primary_payload_locator TEXT NULL
);";
            command.ExecuteNonQuery();
            IsInitialized = true;
        }

        public ResourceRegistration Register(ResourceRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(registration);
            EnsureInitialized();

            var record = ToRecord(registration);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO resource_registrations (
    resource_id,
    type_id,
    family,
    provider_id,
    origin,
    effective_posture,
    authority_mode,
    locality,
    lifecycle_state,
    display_label,
    created_at,
    updated_at,
    primary_payload_locator
)
VALUES (
    $resourceId,
    $typeId,
    $family,
    $providerId,
    $origin,
    $effectivePosture,
    $authorityMode,
    $locality,
    $lifecycleState,
    $displayLabel,
    $createdAt,
    $updatedAt,
    $primaryPayloadLocator
)
ON CONFLICT(resource_id) DO UPDATE SET
    type_id = excluded.type_id,
    family = excluded.family,
    provider_id = excluded.provider_id,
    origin = excluded.origin,
    effective_posture = excluded.effective_posture,
    authority_mode = excluded.authority_mode,
    locality = excluded.locality,
    lifecycle_state = excluded.lifecycle_state,
    display_label = excluded.display_label,
    created_at = excluded.created_at,
    updated_at = excluded.updated_at,
    primary_payload_locator = excluded.primary_payload_locator;";

            command.Parameters.AddWithValue("$resourceId", record.ResourceId);
            command.Parameters.AddWithValue("$typeId", record.TypeId);
            command.Parameters.AddWithValue("$family", record.Family);
            command.Parameters.AddWithValue("$providerId", record.ProviderId);
            command.Parameters.AddWithValue("$origin", record.Origin);
            command.Parameters.AddWithValue("$effectivePosture", record.EffectivePosture);
            command.Parameters.AddWithValue("$authorityMode", record.AuthorityMode);
            command.Parameters.AddWithValue("$locality", record.Locality);
            command.Parameters.AddWithValue("$lifecycleState", record.LifecycleState);
            command.Parameters.AddWithValue("$displayLabel", record.DisplayLabel);
            command.Parameters.AddWithValue("$createdAt", record.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updatedAt", record.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$primaryPayloadLocator", (object?)record.PrimaryPayloadLocator ?? DBNull.Value);
            command.ExecuteNonQuery();

            return ToRegistration(record);
        }

        public bool TryGet(ResourceId resourceId, out ResourceRegistration registration)
        {
            EnsureInitialized();

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    resource_id,
    type_id,
    family,
    provider_id,
    origin,
    effective_posture,
    authority_mode,
    locality,
    lifecycle_state,
    display_label,
    created_at,
    updated_at,
    primary_payload_locator
FROM resource_registrations
WHERE resource_id = $resourceId
LIMIT 1;";
            command.Parameters.AddWithValue("$resourceId", resourceId.ToString());

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                registration = null!;
                return false;
            }

            registration = ToRegistration(ReadRecord(reader));
            return true;
        }

        public IReadOnlyList<ResourceRegistration> ListAll()
        {
            EnsureInitialized();

            var registrations = new List<ResourceRegistration>();

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    resource_id,
    type_id,
    family,
    provider_id,
    origin,
    effective_posture,
    authority_mode,
    locality,
    lifecycle_state,
    display_label,
    created_at,
    updated_at,
    primary_payload_locator
FROM resource_registrations
ORDER BY display_label COLLATE NOCASE, resource_id;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                registrations.Add(ToRegistration(ReadRecord(reader)));
            }

            return registrations;
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={DatabasePath}");
            connection.Open();
            return connection;
        }

        private static ResourceRegistryRecord ReadRecord(SqliteDataReader reader)
        {
            return new ResourceRegistryRecord(
                ReadRequired(reader, "resource_id"),
                ReadRequired(reader, "type_id"),
                ReadRequired(reader, "family"),
                ReadRequired(reader, "provider_id"),
                ReadRequired(reader, "origin"),
                ReadRequired(reader, "effective_posture"),
                ReadRequired(reader, "authority_mode"),
                ReadRequired(reader, "locality"),
                ReadRequired(reader, "lifecycle_state"),
                ReadRequired(reader, "display_label"),
                ReadDateTimeOffset(reader, "created_at"),
                ReadDateTimeOffset(reader, "updated_at"),
                ReadOptional(reader, "primary_payload_locator"));
        }

        private static ResourceRegistryRecord ToRecord(ResourceRegistration registration)
        {
            return new ResourceRegistryRecord(
                NormalizeRequired(registration.ResourceId.ToString()),
                NormalizeRequired(registration.TypeId),
                NormalizeRequired(registration.Family.ToString()),
                NormalizeRequired(registration.ProviderId),
                NormalizeRequired(registration.Origin.ToString()),
                NormalizeRequired(registration.EffectivePosture.ToString()),
                NormalizeRequired(registration.AuthorityMode),
                NormalizeRequired(registration.Locality),
                NormalizeRequired(registration.LifecycleState),
                NormalizeRequired(registration.DisplayLabel),
                registration.CreatedAt,
                registration.UpdatedAt,
                NormalizeOptional(registration.PrimaryPayloadLocator));
        }

        private static ResourceRegistration ToRegistration(ResourceRegistryRecord record)
        {
            if (!ResourceId.TryParse(record.ResourceId, out var resourceId))
            {
                throw new InvalidOperationException($"Invalid resource id '{record.ResourceId}' in registry record.");
            }

            if (!Enum.TryParse<ResourceFamily>(record.Family, ignoreCase: true, out var family) ||
                !Enum.TryParse<ResourceOrigin>(record.Origin, ignoreCase: true, out var origin) ||
                !Enum.TryParse<ResourcePosture>(record.EffectivePosture, ignoreCase: true, out var posture))
            {
                throw new InvalidOperationException($"Invalid resource classification data for resource '{record.ResourceId}'.");
            }

            return new ResourceRegistration(
                resourceId,
                record.TypeId,
                family,
                record.ProviderId,
                origin,
                posture,
                record.AuthorityMode,
                record.Locality,
                record.LifecycleState,
                record.DisplayLabel,
                record.CreatedAt,
                record.UpdatedAt,
                record.PrimaryPayloadLocator);
        }

        private static string ReadRequired(SqliteDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal))
            {
                throw new InvalidOperationException($"Required column '{column}' was null.");
            }

            return reader.GetString(ordinal);
        }

        private static string? ReadOptional(SqliteDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static DateTimeOffset ReadDateTimeOffset(SqliteDataReader reader, string column)
        {
            return DateTimeOffset.Parse(
                ReadRequired(reader, column),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
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
