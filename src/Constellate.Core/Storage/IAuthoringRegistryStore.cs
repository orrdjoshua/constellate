using System.Collections.Generic;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Core.Storage
{
    public interface IAuthoringRegistryStore
    {
        IReadOnlyList<PaneAuthoringElementCatalogEntry> ListAuthoringElements();
        IReadOnlyList<PaneAuthoringBundleCatalogEntry> ListAuthoringBundles();
        void EnsureInitialized();
    }
}
