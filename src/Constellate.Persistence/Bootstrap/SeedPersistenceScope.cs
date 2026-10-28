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
        // Commentary/parity note:
        // This scope is a useful persistence-lane composition seam because it gathers the
        // engine-facing stores behind one project-level boundary. At the same time, most of
        // the stores currently composed here are still seed-stage implementations, so this
        // class should be read as structural composition ahead of full durable-truth maturity,
        // not as proof that the persistence lanes are already fully realized.
        //
        // Phase-5 intent:
        // - Treat a single IEnginePersistenceScope instance as "the persistence face" of one
        //   project/world, typically backed by a single SQLite database file.
        // - EngineStateStore / ProjectionBindingStore / PaneDefinitionStore / PaneWorkspaceStore /
        //   ResourceRegistryStore / NativeRecordStore / ProviderRegistryStore / RuntimeArchiveStore
        //   are currently wired to seed-* implementations for local development worlds.
        // - As each lane graduates from seed-only to DB-backed current-state truth, new concrete
        //   I*Store implementations should be composed here without changing the higher-level
        //   engine contracts, so that "one persistence scope per world" remains the stable
        //   boundary while durability improves underneath.
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