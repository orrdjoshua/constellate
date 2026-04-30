using System.Collections.Generic;

namespace Constellate.Core.Storage
{
    public interface IEngineProjectionBindingStore
    {
        void EnsureInitialized();

        IReadOnlyList<EngineProjectionBindingState> ListAll();

        IReadOnlyList<EngineProjectionBindingState> ListByResource(string resourceId);

        bool TryGet(string projectionBindingId, out EngineProjectionBindingState binding);

        void Upsert(EngineProjectionBindingState binding);
    }
}
