namespace Constellate.Core.Storage
{
    public interface IEnginePersistenceScope
    {
        PersistenceBootstrapResult BootstrapResult { get; }

        IEngineStateStore EngineStateStore { get; }

        IEngineProjectionBindingStore ProjectionBindingStore { get; }

        IPaneDefinitionStore PaneDefinitionStore { get; }

        IPaneWorkspaceStore PaneWorkspaceStore { get; }

        IResourceRegistryStore ResourceRegistryStore { get; }

        INativeRecordStore NativeRecordStore { get; }

        IProviderRegistryStore ProviderRegistryStore { get; }

        IRuntimeArchiveStore RuntimeArchiveStore { get; }

        void EnsureInitialized();
    }
}
