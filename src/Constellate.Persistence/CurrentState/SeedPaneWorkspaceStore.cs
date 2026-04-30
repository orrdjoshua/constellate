using System;
using System.Collections.Generic;
using System.Linq;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Storage;

namespace Constellate.Persistence.CurrentState
{
    public sealed class SeedPaneWorkspaceStore : IPaneWorkspaceStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;
        private readonly Dictionary<string, PaneWorkspaceRecord> _records = new(StringComparer.Ordinal);

        public SeedPaneWorkspaceStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public void EnsureInitialized()
        {
        }

        public IReadOnlyList<PaneWorkspaceDescriptor> ListAll()
        {
            return _records.Values
                .OrderBy(record => record.Descriptor.DisplayLabel, StringComparer.Ordinal)
                .ThenBy(record => record.WorkspaceId, StringComparer.Ordinal)
                .Select(record => record.Descriptor)
                .ToArray();
        }

        public bool TryGet(string workspaceId, out PaneWorkspaceDescriptor workspaceDefinition)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
            var normalizedWorkspaceId = workspaceId.Trim();

            if (_records.TryGetValue(normalizedWorkspaceId, out var record))
            {
                workspaceDefinition = record.Descriptor;
                return true;
            }

            workspaceDefinition = null!;
            return false;
        }

        public void Upsert(PaneWorkspaceDescriptor workspaceDefinition)
        {
            ArgumentNullException.ThrowIfNull(workspaceDefinition);

            var normalizedWorkspaceId = workspaceDefinition.WorkspaceId.Trim();
            var timestamp = DateTimeOffset.UtcNow;
            var createdAt = _records.TryGetValue(normalizedWorkspaceId, out var existing)
                ? existing.CreatedAt
                : timestamp;

            _records[normalizedWorkspaceId] = new PaneWorkspaceRecord(
                normalizedWorkspaceId,
                workspaceDefinition with { WorkspaceId = normalizedWorkspaceId },
                createdAt,
                timestamp);
        }
    }
}
