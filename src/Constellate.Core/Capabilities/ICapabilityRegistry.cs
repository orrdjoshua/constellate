using System.Collections.Generic;

namespace Constellate.Core.Capabilities
{
    public interface ICapabilityRegistry
    {
        IReadOnlyList<EngineCapability> GetAll();
        bool TryGet(string key, out EngineCapability capability);
        void Register(EngineCapability capability);
    }
}
