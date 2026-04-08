using System;
using System.Collections.Generic;
using System.Linq;

namespace Constellate.Core.Scene
{
    public sealed class ShellSceneState
    {
        private readonly EngineScene _scene;

        public ShellSceneState(EngineScene scene)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }

        public SceneSnapshot GetSnapshot() => _scene.GetSnapshot();

        public IReadOnlyList<SceneNode> GetNodes() => _scene.GetSnapshot().Nodes;

        public SceneNode? GetFocusedNode()
        {
            var snapshot = _scene.GetSnapshot();
            return snapshot.FocusedNodeId is { } focusedNodeId
                ? snapshot.Nodes.FirstOrDefault(node => node.Id == focusedNodeId)
                : null;
        }

        public PanelTarget? GetFocusedPanel() => _scene.GetSnapshot().FocusedPanel;

        public PanelTarget? GetFirstPanelTarget()
        {
            var snapshot = _scene.GetSnapshot();
            if (snapshot.PanelAttachments is null)
            {
                return null;
            }

            foreach (var attachment in snapshot.PanelAttachments.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
            {
                return new PanelTarget(attachment.Key, attachment.Value.ViewRef);
            }

            return null;
        }

        public IReadOnlyDictionary<NodeId, PanelAttachment> GetPanelAttachments() =>
            _scene.GetSnapshot().PanelAttachments
            ?? new Dictionary<NodeId, PanelAttachment>();

        public IReadOnlyList<NodeId> GetSelectedNodeIds() =>
            _scene.GetSnapshot().SelectedNodeIds
            ?? Array.Empty<NodeId>();

        public IReadOnlyList<PanelTarget> GetSelectedPanels() =>
            _scene.GetSnapshot().SelectedPanels
            ?? Array.Empty<PanelTarget>();

        public IReadOnlyList<SceneLink> GetLinks() =>
            _scene.GetSnapshot().Links
            ?? Array.Empty<SceneLink>();

        public IReadOnlyList<SceneGroup> GetGroups() =>
            _scene.GetSnapshot().Groups
            ?? Array.Empty<SceneGroup>();

        public IReadOnlyList<SceneBookmark> GetBookmarks() =>
            _scene.GetSnapshot().Bookmarks
            ?? Array.Empty<SceneBookmark>();
    }
}
