using System;
using System.Collections.Generic;
using System.Linq;

namespace Constellate.Core.Scene
{
    public sealed class ShellSceneState
    {
        private readonly EngineScene _scene;
        private string _lastFocusOrigin = "unknown";
        private readonly Queue<ViewParams> _viewHistory = new();

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

        public SceneGroup? GetActiveGroup()
        {
            var snapshot = _scene.GetSnapshot();
            var groups = snapshot.Groups;
            if (groups is null || groups.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.ActiveGroupId))
            {
                return groups.FirstOrDefault(group =>
                    string.Equals(group.Id, snapshot.ActiveGroupId, StringComparison.Ordinal));
            }

            return groups[0];
        }

        public IReadOnlyList<SceneBookmark> GetBookmarks() =>
            _scene.GetSnapshot().Bookmarks
            ?? Array.Empty<SceneBookmark>();

        public NodeId? GetEnteredNodeId() => _scene.EnteredNodeId;

        public bool IsNodeExpanded(NodeId id) => _scene.IsNodeExpanded(id);

        public bool TryGetLastView(out ViewParams view) => _scene.TryGetLastView(out view);

        /// <summary>

        /// <summary>
        /// Returns a snapshot of the recent navigation/view history as recorded from
        /// ViewChanged events. Most recent entries appear last in the returned list.
        /// This is an observability surface for the shell; it does not participate
        /// in Core undo/history semantics.
        /// </summary>
        public IReadOnlyList<ViewParams> GetViewHistory(int maxEntries = 10)
        {
            if (maxEntries <= 0 || _viewHistory.Count == 0)
            {
                return _viewHistory.ToArray();
            }

            // Queue preserves order from oldest -> newest; return a trimmed tail if needed.
            var all = _viewHistory.ToArray();
            if (all.Length <= maxEntries)
            {
                return all;
            }

            var start = all.Length - maxEntries;
            var result = new ViewParams[maxEntries];
            Array.Copy(all, start, result, 0, maxEntries);
            return result;
        }

        public string GetInteractionMode() => _scene.GetSnapshot().InteractionMode;

        /// <summary>
        /// Returns the last reported focus-origin label observed from the engine event bus
        /// (for example "mouse", "keyboard", "command", or "programmatic").
        /// This is an observability hint only; it does not currently drive behavior.
        /// </summary>
        public string GetFocusOrigin() => _lastFocusOrigin;

        /// <summary>
        /// Updates the cached focus-origin label based on published FocusOriginChanged events.
        /// Callers should pass simple, lowercased origin hints such as "mouse", "keyboard",
        /// "command", or "programmatic".
        /// </summary>
        internal void SetFocusOrigin(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                return;
            }

            _lastFocusOrigin = origin.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Append a new view sample into the navigation/view history queue. This is
        /// invoked from EngineServices when ViewChanged events are observed. History
        /// is kept as a small fixed-size tail (default 10) for shell/readout use.
        /// </summary>
        internal void AppendViewHistory(ViewParams view, int maxEntries = 10)
        {
            if (maxEntries <= 0)
            {
                maxEntries = 1;
            }

            _viewHistory.Enqueue(view);

            while (_viewHistory.Count > maxEntries)
            {
                _viewHistory.Dequeue();
            }
        }
    }
}
