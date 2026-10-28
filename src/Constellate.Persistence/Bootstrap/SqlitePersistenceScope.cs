using System;
using System.IO;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Storage;
using Constellate.Persistence.Archive;
using Constellate.Persistence.CurrentState;
using Constellate.Persistence.NativeRecords;
using Constellate.Persistence.Registry;

namespace Constellate.Persistence.Bootstrap
{
    public sealed class SqlitePersistenceScope : IEnginePersistenceScope
    {
        // This scope is the world-local persistence composition seam. Each
        // IEnginePersistenceScope instance represents the persistence face of one concrete
        // project/world, backed by a single SQLite database file.
        //
        // Phase-5 intent:
        // - EngineStateStore / ProjectionBindingStore / PaneDefinitionStore / PaneWorkspaceStore /
        //   ResourceRegistryStore / NativeRecordStore / ProviderRegistryStore / RuntimeArchiveStore
        //   are SQLite-backed current-state lanes for the selected world database.
        // - Default/new-world seeding remains a separately planned step and must not be performed
        //   silently by this scope on every run.
        // - As new truth-bearing lanes graduate into DB-backed stores, they should be composed here
        //   without changing the higher-level engine contracts, preserving
        //   "one persistence scope per world" as the stable boundary.
        public SqlitePersistenceScope(PersistenceBootstrapResult bootstrapResult)
        {
            BootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
            EngineStateStore = new SqliteEngineStateStore(bootstrapResult);
            ProjectionBindingStore = new SqliteEngineProjectionBindingStore(bootstrapResult);
            PaneDefinitionStore = new SqlitePaneDefinitionStore(bootstrapResult);
            PaneWorkspaceStore = new SqlitePaneWorkspaceStore(bootstrapResult);
            ResourceRegistryStore = new SqliteResourceRegistryStore(bootstrapResult);
            NativeRecordStore = new SqliteNativeRecordStore(bootstrapResult);
            ProviderRegistryStore = new SqliteProviderRegistryStore(bootstrapResult);
            RuntimeArchiveStore = new SqliteRuntimeArchiveStore(bootstrapResult);
            AuthoringRegistryStore = new SqliteAuthoringRegistryStore(
                bootstrapResult,
                seedDefaultsOnInitialize: bootstrapResult.DatabaseFileCreated);

            var engineProjectStore = new SqliteEngineProjectStore(bootstrapResult);
            engineProjectStore.EnsureInitialized();
            if (!engineProjectStore.TryGet(out _))
            {
                engineProjectStore.Upsert(BuildDefaultEngineProjectRecord(bootstrapResult));
            }

            if (bootstrapResult.DatabaseFileCreated)
            {
                SeedDefaultWorldCatalog();
            }
        }

        public PersistenceBootstrapResult BootstrapResult { get; }

        public IAuthoringRegistryStore AuthoringRegistryStore { get; }

        public IEngineStateStore EngineStateStore { get; }

        public IEngineProjectionBindingStore ProjectionBindingStore { get; }

        public IPaneDefinitionStore PaneDefinitionStore { get; }

        public IPaneWorkspaceStore PaneWorkspaceStore { get; }

        public IResourceRegistryStore ResourceRegistryStore { get; }

        public INativeRecordStore NativeRecordStore { get; }

        public IProviderRegistryStore ProviderRegistryStore { get; }

        public IRuntimeArchiveStore RuntimeArchiveStore { get; }

        private void SeedDefaultWorldCatalog()
        {
            var catalog = new SeededPaneCatalog();

            foreach (var definition in catalog.GetPaneDefinitions())
            {
                PaneDefinitionStore.Upsert(definition);
            }

            foreach (var workspace in catalog.GetWorkspaceDefinitions())
            {
                PaneWorkspaceStore.Upsert(workspace);
            }
        }

        private static EngineProjectRecord BuildDefaultEngineProjectRecord(
            PersistenceBootstrapResult bootstrapResult)
        {
            var displayName = Path.GetFileNameWithoutExtension(bootstrapResult.DatabasePath);
            var now = DateTimeOffset.UtcNow;

            return new EngineProjectRecord(
                ProjectId: $"world-{Guid.NewGuid():N}",
                DisplayName: displayName,
                ProjectVersion: SqliteEngineProjectStore.CurrentProjectVersion,
                EngineSchemaVersion: SqliteEngineProjectStore.CurrentEngineSchemaVersion,
                CreatedAt: now,
                UpdatedAt: now);
        }

        public void EnsureInitialized()
        {
            AuthoringRegistryStore.EnsureInitialized();
            ProjectionBindingStore.EnsureInitialized();
            PaneDefinitionStore.EnsureInitialized();
            PaneWorkspaceStore.EnsureInitialized();
            ResourceRegistryStore.EnsureInitialized();
            NativeRecordStore.EnsureInitialized();
            ProviderRegistryStore.EnsureInitialized();
            RuntimeArchiveStore.EnsureInitialized();
        }
    }
}
