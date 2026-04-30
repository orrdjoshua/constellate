using System.Collections.Generic;
using Constellate.Core.Capabilities.Panes;

namespace Constellate.Core.Storage
{
    public interface IPaneWorkspaceStore
    {
        IReadOnlyList<PaneWorkspaceDescriptor> ListAll();
        bool TryGet(string workspaceId, out PaneWorkspaceDescriptor workspaceDefinition);
        void Upsert(PaneWorkspaceDescriptor workspaceDefinition);
        void EnsureInitialized();
    }
}
