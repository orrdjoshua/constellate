using System;
using System.Collections.Generic;
using Constellate.Core.Storage;

namespace Constellate.Persistence.Archive
{
    public sealed class SeedRuntimeArchiveStore : IRuntimeArchiveStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;

        public SeedRuntimeArchiveStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public bool IsInitialized { get; private set; }

        public IList<ActionInvocationRecord> ActionInvocations { get; } = new List<ActionInvocationRecord>();

        public IList<RuntimeStreamEventRecord> RuntimeStreamEvents { get; } = new List<RuntimeStreamEventRecord>();

        public void EnsureInitialized()
        {
            IsInitialized = true;
        }
    }
}
