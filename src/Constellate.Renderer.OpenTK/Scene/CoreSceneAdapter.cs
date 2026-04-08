using System.Linq;
using Constellate.Core.Scene;
using OpenTK.Mathematics;

namespace Constellate.Renderer.OpenTK.Scene
{
    public static class CoreSceneAdapter
    {
        public static RenderSceneSnapshot ToRenderSceneSnapshot(SceneSnapshot snapshot)
        {
            var focusedNodeId = snapshot.FocusedNodeId;
            var selectedNodeIds = snapshot.SelectedNodeIds?.ToHashSet() ?? [];

            var nodes = new RenderNode[snapshot.Nodes.Count];

            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var src = snapshot.Nodes[i];
                var scale = src.VisualScale;
                if (scale <= 0f)
                {
                    scale = src.Transform.Scale.X <= 0f ? 1f : src.Transform.Scale.X;
                }

                nodes[i] = new RenderNode(
                    src.Id.ToString(),
                    src.Label,
                    new Vector3(
                        src.Transform.Position.X,
                        src.Transform.Position.Y,
                        src.Transform.Position.Z),
                    new Vector3(
                        src.Transform.RotationEuler.X,
                        src.Transform.RotationEuler.Y,
                        src.Transform.RotationEuler.Z),
                    new Vector3(
                        src.Transform.Scale.X,
                        src.Transform.Scale.Y,
                        src.Transform.Scale.Z),
                    scale,
                    src.Phase,
                    focusedNodeId == src.Id,
                    selectedNodeIds.Contains(src.Id));
            }

            var focusedPanel = snapshot.FocusedPanel;
            var selectedPanels = snapshot.SelectedPanels?.ToHashSet() ?? [];

            var panelSurfaces = snapshot.PanelAttachments?
                .Values
                .Select(attachment =>
                {
                    var panelTarget = new PanelTarget(attachment.NodeId, attachment.ViewRef);
                    var isFocused = focusedPanel is { } currentFocused &&
                                    currentFocused.NodeId == attachment.NodeId &&
                                    string.Equals(currentFocused.ViewRef, attachment.ViewRef, System.StringComparison.Ordinal);
                    var isSelected = selectedPanels.Contains(panelTarget);

                    return new PanelSurfaceNode(
                        attachment.NodeId.ToString(),
                        attachment.ViewRef,
                        new Vector3(
                            attachment.LocalOffset.X,
                            attachment.LocalOffset.Y,
                            attachment.LocalOffset.Z),
                        new Vector2(
                            attachment.Size.X,
                            attachment.Size.Y),
                        attachment.Anchor,
                        attachment.IsVisible,
                        isFocused,
                        isSelected);
                })
                .ToArray()
                ?? [];

            var links = snapshot.Links?
                .Select(link => new RenderLink(
                    link.Id,
                    link.SourceId.ToString(),
                    link.TargetId.ToString(),
                    link.Kind,
                    link.Weight))
                .ToArray()
                ?? [];

            var groups = snapshot.Groups?
                .Select(group => new RenderGroup(
                    group.Id,
                    group.Label,
                    group.NodeIds.Select(id => id.ToString()).ToArray()))
                .ToArray()
                ?? [];

            return new RenderSceneSnapshot(nodes, panelSurfaces, links, groups);
        }

        public static RenderNode[] ToRenderNodes(SceneSnapshot snapshot)
        {
            return ToRenderSceneSnapshot(snapshot).Nodes;
        }
    }
}
