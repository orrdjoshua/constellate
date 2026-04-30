using System.Collections.Generic;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Core.Storage
{
    public interface IPaneDefinitionStore
    {
        IReadOnlyList<PaneDefinitionDescriptor> ListAll();
        bool TryGet(string paneDefinitionId, out PaneDefinitionDescriptor paneDefinition);
        void Upsert(PaneDefinitionDescriptor paneDefinition);
        void EnsureInitialized();
    }
}
