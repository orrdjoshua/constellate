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
        // engine-facing stores behind one project-level boundary. At the same time, most of
        // the stores currently composed here are still seed-stage implementations, so this
        // class should be read as structural composition ahead of full durable-truth maturity,
        // not as proof that the persistence lanes are already fully realized.
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
