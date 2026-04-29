using System;
using Constellate.Core.Scene;
using Constellate.Core.Storage;

namespace Constellate.Persistence.CurrentState
{
    public sealed class SeedEngineStateStore : IEngineStateStore
    {
        private readonly PersistenceBootstrapResult _bootstrapResult;
        private SceneSnapshot? _snapshot;

        public SeedEngineStateStore(PersistenceBootstrapResult bootstrapResult)
        {
            _bootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }

        public string DatabasePath => _bootstrapResult.DatabasePath;

        public bool HasPersistedSnapshot() => _snapshot is not null;

        public SceneSnapshot? LoadSnapshot() => _snapshot;

        public void SaveSnapshot(SceneSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _snapshot = snapshot;
        }
    }
}
