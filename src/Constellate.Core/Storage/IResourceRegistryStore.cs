using Constellate.Core.Resources;
using System.Collections.Generic;

namespace Constellate.Core.Storage
{
    /// <summary>
    /// Storage seam for the current resource registration/catalog layer. It is a healthy boundary
    /// for identity-bearing resource registry operations, but the broader provider/runtime/resource
    /// substrate still remains less converged than the shell/world side of the codebase.
    /// </summary>
    public interface IResourceRegistryStore
    {
        void EnsureInitialized();

        ResourceRegistration Register(ResourceRegistration registration);

        bool TryGet(ResourceId resourceId, out ResourceRegistration registration);

        IReadOnlyList<ResourceRegistration> ListAll();
    }
}
