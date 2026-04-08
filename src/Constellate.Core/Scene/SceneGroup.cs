using System.Collections.Generic;

namespace Constellate.Core.Scene
{
    public sealed record SceneGroup(
        string Id,
        string Label,
        IReadOnlyList<NodeId> NodeIds);
}
