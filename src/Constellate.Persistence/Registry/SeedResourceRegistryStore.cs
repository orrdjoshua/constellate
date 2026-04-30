using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Resources;
using Constellate.Core.Storage;

namespace Constellate.Persistence.Registry
{
    public sealed class SeedResourceRegistryStore : IResourceRegistryStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SeedResourceRegistryStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public bool IsInitialized { get; private set; }

        public IList<ResourceRegistryRecord> Entries { get; } = new List<ResourceRegistryRecord>();

        public void EnsureInitialized()
        {
            IsInitialized = true;
        }

        public ResourceRegistration Register(ResourceRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(registration);

            var record = ToRecord(registration);

            for (var index = 0; index < Entries.Count; index++)
            {
                if (string.Equals(Entries[index].ResourceId, record.ResourceId, StringComparison.Ordinal))
                {
                    Entries[index] = record;
                    return ToRegistration(record);
                }
            }

            Entries.Add(record);
            return ToRegistration(record);
        }

        public bool TryGet(ResourceId resourceId, out ResourceRegistration registration)
        {
            foreach (var entry in Entries)
            {
                if (string.Equals(entry.ResourceId, resourceId.ToString(), StringComparison.Ordinal))
                {
                    registration = ToRegistration(entry);
                    return true;
                }
            }

            registration = null!;
            return false;
        }

        public IReadOnlyList<ResourceRegistration> ListAll()
        {
            return Entries
                .Select(ToRegistration)
                .OrderBy(registration => registration.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(registration => registration.ResourceId.ToString(), StringComparer.Ordinal)
                .ToArray();
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
