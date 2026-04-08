using System.Collections.Generic;

namespace Constellate.Core.Scene
{
    public sealed record SceneBookmark(
        string Name,
        NodeId? FocusedNodeId,
        IReadOnlyList<NodeId> SelectedNodeIds,
        PanelTarget? FocusedPanel,
        IReadOnlyList<PanelTarget> SelectedPanels,
        ViewParams? View = null);
}
