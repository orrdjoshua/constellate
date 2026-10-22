using System;
using System.IO;
using Constellate.Core.Storage;
using Constellate.Persistence.Archive;
using Constellate.Persistence.CurrentState;
using Constellate.Persistence.NativeRecords;
using Constellate.Persistence.Registry;

namespace Constellate.Persistence.Bootstrap
{
    public sealed class SeedPersistenceScope : IEnginePersistenceScope
    {
        private readonly IAuthoringRegistryStore _authoringRegistryStore;

        public SeedPersistenceScope(PersistenceBootstrapResult bootstrapResult)
        {
            BootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
            EngineStateStore = new SeedEngineStateStore(bootstrapResult);
            ProjectionBindingStore = new SeedEngineProjectionBindingStore(bootstrapResult);
            PaneDefinitionStore = new SeedPaneDefinitionStore(bootstrapResult);
            PaneWorkspaceStore = new SeedPaneWorkspaceStore(bootstrapResult);
            ResourceRegistryStore = new SeedResourceRegistryStore(bootstrapResult);
            NativeRecordStore = new SeedNativeRecordStore(bootstrapResult);
            ProviderRegistryStore = new SeedProviderRegistryStore(bootstrapResult);
            RuntimeArchiveStore = new SeedRuntimeArchiveStore(bootstrapResult);
            // Phase 2: prefer DB-backed authoring registries when the bootstrap points at a Sqlite DB file.
            // We infer Sqlite by the database file extension since BootstrapResult does not expose a Provider property here.
            var dbPath = bootstrapResult.DatabasePath;
            var isSqlite =
                !string.IsNullOrWhiteSpace(dbPath) &&
                (dbPath.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                 dbPath.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
                 dbPath.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase));
            _authoringRegistryStore = isSqlite
                ? new SqliteAuthoringRegistryStore(bootstrapResult)
                : new SeedAuthoringRegistryStore(bootstrapResult);
        }

        public PersistenceBootstrapResult BootstrapResult { get; }

        public IEngineStateStore EngineStateStore { get; }

        public IEngineProjectionBindingStore ProjectionBindingStore { get; }

        public IPaneDefinitionStore PaneDefinitionStore { get; }

        public IPaneWorkspaceStore PaneWorkspaceStore { get; }

        public IResourceRegistryStore ResourceRegistryStore { get; }

        public INativeRecordStore NativeRecordStore { get; }

        public IProviderRegistryStore ProviderRegistryStore { get; }

        public IRuntimeArchiveStore RuntimeArchiveStore { get; }

        // Phase 1: expose a seed-backed authoring registry store so EngineServices
        // consumes an explicit persistence seam immediately (RFC-118).
        public IAuthoringRegistryStore AuthoringRegistryStore => _authoringRegistryStore;

        public void EnsureInitialized()
        {
            ProjectionBindingStore.EnsureInitialized();
            PaneDefinitionStore.EnsureInitialized();
            PaneWorkspaceStore.EnsureInitialized();
            ResourceRegistryStore.EnsureInitialized();
            NativeRecordStore.EnsureInitialized();
            ProviderRegistryStore.EnsureInitialized();
            RuntimeArchiveStore.EnsureInitialized();
            _authoringRegistryStore.EnsureInitialized();
        }
    }
}
