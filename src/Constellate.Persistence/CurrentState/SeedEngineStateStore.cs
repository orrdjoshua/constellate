using System;
using Constellate.Core.Scene;
using Constellate.Core.Storage;

namespace Constellate.Persistence.CurrentState
{
    public sealed class SeedEngineStateStore : IEngineStateStore
    {
        // Commentary/parity note:
        // This store is intentionally a seed-stage bridge and currently keeps only an in-memory
        // scene snapshot. It should be treated as a temporary truth placeholder until the
        // engine-state lane is backed by real durable project storage rather than as the final
        // persistence model for scene/current-state truth. A later pass should explicitly decide
        // whether this lane remains a whole-scene bootstrap fallback or yields to more granular
        // durable current-state records.

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
