using System.Collections.Generic;

namespace Constellate.Core.Scene
{
    public sealed record SceneSnapshot(
        IReadOnlyList<SceneNode> Nodes,
        NodeId? FocusedNodeId = null,
        IReadOnlyList<NodeId>? SelectedNodeIds = null,
        IReadOnlyDictionary<NodeId, PanelAttachment>? PanelAttachments = null,
        PanelTarget? FocusedPanel = null,
        IReadOnlyList<PanelTarget>? SelectedPanels = null,
        IReadOnlyList<SceneLink>? Links = null,
        IReadOnlyList<SceneGroup>? Groups = null);
}
