using System;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Core.Storage
{
    /// <summary>
    /// Optional write-path for DB-backed authoring registries.
    /// Implemented by SqliteAuthoringRegistryStore; seed-only stores may omit (or throw).
    /// </summary>
    public interface IAuthoringRegistryWriteStore
    {
        void UpsertAuthoringElement(PaneAuthoringElementCatalogEntry entry);
        void UpsertAuthoringBundle(PaneAuthoringBundleCatalogEntry bundle);
        bool TryGetGenerationVersion(string name, out int version);
    }
}
