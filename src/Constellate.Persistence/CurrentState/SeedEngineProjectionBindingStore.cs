using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Storage;

namespace Constellate.Persistence.CurrentState
{
    public sealed class SeedEngineProjectionBindingStore : IEngineProjectionBindingStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;
        private readonly Dictionary<string, EngineProjectionBindingRecord> _records = new(StringComparer.Ordinal);

        public SeedEngineProjectionBindingStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public void EnsureInitialized()
        {
        }

        public IReadOnlyList<EngineProjectionBindingState> ListAll()
        {
            return _records.Values
                .OrderBy(record => record.ResourceId, StringComparer.Ordinal)
                .ThenBy(record => record.ProjectionBindingId, StringComparer.Ordinal)
                .Select(ToState)
                .ToArray();
        }

        public IReadOnlyList<EngineProjectionBindingState> ListByResource(string resourceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
            var normalizedResourceId = resourceId.Trim();

            return _records.Values
                .Where(record => string.Equals(record.ResourceId, normalizedResourceId, StringComparison.Ordinal))
                .OrderBy(record => record.ProjectionBindingId, StringComparer.Ordinal)
                .Select(ToState)
                .ToArray();
        }

        public bool TryGet(string projectionBindingId, out EngineProjectionBindingState binding)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectionBindingId);
            var normalizedProjectionBindingId = projectionBindingId.Trim();

            if (_records.TryGetValue(normalizedProjectionBindingId, out var record))
            {
                binding = ToState(record);
                return true;
            }

            binding = null!;
            return false;
        }

        public void Upsert(EngineProjectionBindingState binding)
        {
            ArgumentNullException.ThrowIfNull(binding);

            var normalizedProjectionBindingId = binding.ProjectionBindingId.Trim();
            var normalizedResourceId = binding.ResourceId.Trim();

            _records[normalizedProjectionBindingId] = new EngineProjectionBindingRecord(
                normalizedProjectionBindingId,
                normalizedResourceId,
                NormalizeOptional(binding.ResourceTypeId),
                NormalizeRequired(binding.SurfaceRole),
                NormalizeRequired(binding.ViewRef),
                NormalizeRequired(binding.ProjectionMode),
                NormalizeRequired(binding.TargetSurfaceKind),
                NormalizeRequired(binding.BindingState),
                NormalizeRequired(binding.TargetKind),
                NormalizeOptional(binding.TargetId),
                binding.CreatedAt,
                binding.UpdatedAt);
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

        private static EngineProjectionBindingState ToState(EngineProjectionBindingRecord record)
        {
            return new EngineProjectionBindingState(
                record.ProjectionBindingId,
                record.ResourceId,
                record.ResourceTypeId,
                record.SurfaceRole,
                record.ViewRef,
                record.ProjectionMode,
                record.TargetSurfaceKind,
                record.BindingState,
                record.TargetKind,
                record.TargetId,
                record.CreatedAt,
                record.UpdatedAt);
        }
    }
}
