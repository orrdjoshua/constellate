using System;
using System.Collections.Generic;
using Constellate.Core.Storage;

namespace Constellate.Persistence.Registry
{
    public sealed class SeedProviderRegistryStore : IProviderRegistryStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SeedProviderRegistryStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public bool IsInitialized { get; private set; }

        public IList<ProviderRegistrationRecord> Entries { get; } = new List<ProviderRegistrationRecord>();

        public void EnsureInitialized()
        {
            IsInitialized = true;
        }
    }
}
