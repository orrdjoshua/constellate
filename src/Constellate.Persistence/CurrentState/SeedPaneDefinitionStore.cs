using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Storage;

namespace Constellate.Persistence.CurrentState
{
    // Commentary/parity note:
    // This store is already a meaningful authored-surface persistence seam because the
    // contract and seed implementation align around list/get/upsert responsibilities and
    // preserve id/timestamp continuity. At the same time, the reviewed slice still wraps
    // a broad pane-definition descriptor more directly than the narrower projection-binding
    // lane, so it currently reads as important durable-cutover work that remains slightly behind it.
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
