using System.Collections.Generic;

namespace Constellate.Core.Capabilities
{
    public interface ICapabilityRegistry
    {
        IReadOnlyList<EngineCapability> GetAll();
        IReadOnlyList<EngineCapability> GetCatalogBacked();
        bool TryGet(string key, out EngineCapability capability);
        bool TryGetByCatalogCapabilityId(string capabilityId, out EngineCapability capability);
        void Register(EngineCapability capability);
    }
}
