using System.Collections.Generic;

namespace Constellate.Core.Storage
{
    /// <summary>
    /// Storage seam for engine-visible projection/binding current-state truth.
    /// The contract is already fairly clean around binding identity, resource lookup,
    /// and upsert/query responsibilities, which makes this lane a stronger early
    /// durable-cutover candidate than some broader persistence families. Its main
    /// remaining weakness is durable backing, not lack of state vocabulary.
    /// </summary>
    public interface IEngineProjectionBindingStore
    {
        void EnsureInitialized();

        IReadOnlyList<EngineProjectionBindingState> ListAll();

        IReadOnlyList<EngineProjectionBindingState> ListByResource(string resourceId);

        bool TryGet(string projectionBindingId, out EngineProjectionBindingState binding);

        void Upsert(EngineProjectionBindingState binding);
    }
}
