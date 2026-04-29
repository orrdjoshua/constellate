using Constellate.Core.Resources;
using System.Collections.Generic;

namespace Constellate.Core.Storage
{
    public interface IResourceRegistryStore
    {
        void EnsureInitialized();

        ResourceRegistration Register(ResourceRegistration registration);

        bool TryGet(ResourceId resourceId, out ResourceRegistration registration);

        IReadOnlyList<ResourceRegistration> ListAll();
    }
}
