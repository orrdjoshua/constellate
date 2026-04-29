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
        public SeedPersistenceScope(PersistenceBootstrapResult bootstrapResult)
        {
            BootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
            EngineStateStore = new SeedEngineStateStore(bootstrapResult);
            ResourceRegistryStore = new SeedResourceRegistryStore(bootstrapResult);
            NativeRecordStore = new SeedNativeRecordStore(bootstrapResult);
            ProviderRegistryStore = new SeedProviderRegistryStore(bootstrapResult);
            RuntimeArchiveStore = new SeedRuntimeArchiveStore(bootstrapResult);
        }

        public PersistenceBootstrapResult BootstrapResult { get; }

        public IEngineStateStore EngineStateStore { get; }

        public IResourceRegistryStore ResourceRegistryStore { get; }

        public INativeRecordStore NativeRecordStore { get; }

        public IProviderRegistryStore ProviderRegistryStore { get; }

        public IRuntimeArchiveStore RuntimeArchiveStore { get; }

        public void EnsureInitialized()
        {
            ResourceRegistryStore.EnsureInitialized();
            NativeRecordStore.EnsureInitialized();
            ProviderRegistryStore.EnsureInitialized();
            RuntimeArchiveStore.EnsureInitialized();
        }
    }
}
