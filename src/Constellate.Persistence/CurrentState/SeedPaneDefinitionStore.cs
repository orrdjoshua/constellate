using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Storage;

namespace Constellate.Persistence.CurrentState
{
    public sealed class SeedPaneDefinitionStore : IPaneDefinitionStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;
        private readonly Dictionary<string, PaneDefinitionRecord> _records = new(StringComparer.Ordinal);

        public SeedPaneDefinitionStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public void EnsureInitialized()
        {
        }

        public IReadOnlyList<PaneDefinitionDescriptor> ListAll()
        {
            return _records.Values
                .OrderBy(record => record.Descriptor.DisplayLabel, StringComparer.Ordinal)
                .ThenBy(record => record.PaneDefinitionId, StringComparer.Ordinal)
                .Select(record => record.Descriptor)
                .ToArray();
        }

        public bool TryGet(string paneDefinitionId, out PaneDefinitionDescriptor paneDefinition)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(paneDefinitionId);
            var normalizedPaneDefinitionId = paneDefinitionId.Trim();

            if (_records.TryGetValue(normalizedPaneDefinitionId, out var record))
            {
                paneDefinition = record.Descriptor;
                return true;
            }

            paneDefinition = null!;
            return false;
        }

        public void Upsert(PaneDefinitionDescriptor paneDefinition)
        {
            ArgumentNullException.ThrowIfNull(paneDefinition);

            var normalizedPaneDefinitionId = paneDefinition.PaneDefinitionId.Trim();
            var timestamp = DateTimeOffset.UtcNow;
            var createdAt = _records.TryGetValue(normalizedPaneDefinitionId, out var existing)
                ? existing.CreatedAt
                : timestamp;

            _records[normalizedPaneDefinitionId] = new PaneDefinitionRecord(
                normalizedPaneDefinitionId,
                paneDefinition with { PaneDefinitionId = normalizedPaneDefinitionId },
                createdAt,
                timestamp);
        }
    }
}
