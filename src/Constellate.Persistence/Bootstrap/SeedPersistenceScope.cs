using System;
using Constellate.Core.Storage;
using Constellate.Persistence.Archive;
using Constellate.Persistence.CurrentState;
using Constellate.Persistence.NativeRecords;
using Constellate.Persistence.Registry;

namespace Constellate.Persistence.Bootstrap
{
    public sealed class SeedPersistenceScope : IEnginePersistenceScope
    {
        // Commentary/parity note:
        // This scope is a useful persistence-lane composition seam because it gathers the
        // engine-facing stores behind one project-level boundary. Several truth-bearing
        // lanes are now SQLite-backed while others remain seed-stage, so this class should
        // be read as active durable-truth migration composition rather than a completed
        // persistence backend.
        //
        // Phase-5 intent:
        // - Treat a single IEnginePersistenceScope instance as "the persistence face" of one
        //   project/world, typically backed by a single SQLite database file.
        // - EngineStateStore / ProjectionBindingStore / PaneDefinitionStore / PaneWorkspaceStore
        //   are now SQLite-backed current-state lanes for the selected world database.
        // - ResourceRegistryStore / NativeRecordStore / ProviderRegistryStore / RuntimeArchiveStore
        //   still remain seed-stage for local development worlds.
        // - As each lane graduates from seed-only to DB-backed current-state truth, new concrete
        //   I*Store implementations should be composed here without changing the higher-level
        //   engine contracts, so that "one persistence scope per world" remains the stable
        //   boundary while durability improves underneath.
        public SeedPersistenceScope(PersistenceBootstrapResult bootstrapResult)
        {
            BootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
            EngineStateStore = new SqliteEngineStateStore(bootstrapResult);
            ProjectionBindingStore = new SqliteEngineProjectionBindingStore(bootstrapResult);
            PaneDefinitionStore = new SqlitePaneDefinitionStore(bootstrapResult);
            PaneWorkspaceStore = new SqlitePaneWorkspaceStore(bootstrapResult);
            ResourceRegistryStore = new SeedResourceRegistryStore(bootstrapResult);
            NativeRecordStore = new SeedNativeRecordStore(bootstrapResult);
            ProviderRegistryStore = new SeedProviderRegistryStore(bootstrapResult);
            RuntimeArchiveStore = new SeedRuntimeArchiveStore(bootstrapResult);
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

        public void EnsureInitialized()
        {
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
