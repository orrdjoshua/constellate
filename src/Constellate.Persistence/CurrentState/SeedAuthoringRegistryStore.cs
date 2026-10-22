using System;
using System.Collections.Generic;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Storage;

namespace Constellate.Persistence.CurrentState
{
    /// <summary>
    /// Minimal Phase-1 seed-backed registry store for authoring entries.
    /// Proxies the in-code PaneAuthoringCatalog so EngineServices consumes
    /// an explicit persistence seam immediately (RFC-118 Phase 1).
    /// </summary>
    public sealed class SeedAuthoringRegistryStore : IAuthoringRegistryStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SeedAuthoringRegistryStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public void EnsureInitialized()
        {
            // No-op for the in-memory seed store.
        }

        public IReadOnlyList<PaneAuthoringElementCatalogEntry> ListAuthoringElements()
        {
            // Phase 1: proxy to in-code catalog (no DB yet).
            return PaneAuthoringCatalog.GetElementEntries();
        }

        public IReadOnlyList<PaneAuthoringBundleCatalogEntry> ListAuthoringBundles()
        {
            // Phase 1: proxy to in-code catalog (no DB yet).
            return PaneAuthoringCatalog.GetBundleEntries();
        }
    }
}
