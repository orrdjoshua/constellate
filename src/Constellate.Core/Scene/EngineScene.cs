using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Constellate.Core.Scene
{
    public sealed class EngineScene
    {
        private readonly object _gate = new();
        private readonly Dictionary<NodeId, SceneNode> _nodes = new();
        private readonly HashSet<NodeId> _selectedNodeIds = new();
        private readonly Dictionary<NodeId, PanelAttachment> _panelAttachments = new();
        private readonly HashSet<PanelTarget> _selectedPanels = new();
        private readonly Dictionary<string, SceneLink> _links = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SceneGroup> _groups = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SceneBookmark> _bookmarks = new(StringComparer.Ordinal);
        private readonly Stack<SceneSnapshot> _undoStack = new();
        private NodeId? _enteredNodeId;
        private readonly HashSet<NodeId> _expandedNodeIds = new();

        // Last-known renderer-neutral view (yaw/pitch/distance/target), updated by EngineServices on ViewChanged
        private ViewParams? _lastView;

        public NodeId? FocusedNodeId { get; private set; }
        public PanelTarget? FocusedPanel { get; private set; }
        public string? ActiveGroupId { get; private set; }
        public string InteractionMode { get; private set; } = "navigate";

        public void SetLastView(ViewParams view)
        {
            lock (_gate)
            {
                _lastView = view;
            }
        }

        public bool TryGetLastView(out ViewParams view)
        {
            lock (_gate)
            {
                if (_lastView is null)
                {
                    view = default!;
                    return false;
                }

                view = _lastView;
                return true;
            }
        }

        public NodeId? EnteredNodeId
        {
            get
            {
                lock (_gate)
                {
                    return _enteredNodeId;
                }
            }
        }

        public bool CanUndo
        {
            get
            {
                lock (_gate)
                {
                    return _undoStack.Count > 0;
                }
            }
        }

        public void PushUndoSnapshot(SceneSnapshot snapshot)
        {
            lock (_gate)
            {
                _undoStack.Push(snapshot);
            }
        }

        public bool TryUndo()
        {
            lock (_gate)
            {
                if (_undoStack.Count == 0)
                {
                    return false;
                }

                RestoreSnapshotUnderLock(_undoStack.Pop());
                return true;
            }
        }

        public bool ClearLinks()
        {
            lock (_gate)
            {
                if (_links.Count == 0)
                {
                    return false;
                }

                _links.Clear();
                return true;
            }
        }

        public void Upsert(SceneNode node)
        {
            lock (_gate)
            {
                _nodes[node.Id] = node;
            }
        }

        public bool Remove(NodeId id)
        {
            lock (_gate)
            {
                var removed = _nodes.Remove(id);
                if (!removed)
                {
                    return false;
                }

                _selectedNodeIds.Remove(id);
                _panelAttachments.Remove(id);
                _selectedPanels.RemoveWhere(x => x.NodeId == id);

                var linkIdsToRemove = _links.Values
                    .Where(link => link.SourceId == id || link.TargetId == id)
                    .Select(link => link.Id)
                    .ToArray();

                foreach (var linkId in linkIdsToRemove)
                {
                    _links.Remove(linkId);
                }

                var nextGroups = _groups.Values
                    .Select(group =>
                    {
                        var remaining = group.NodeIds
                            .Where(nodeId => nodeId != id)
                            .ToArray();

                        return remaining.Length == 0
                            ? null
                            : group with { NodeIds = remaining };
                    })
                    .Where(group => group is not null)
                    .Cast<SceneGroup>()
                    .ToArray();

                _groups.Clear();
                foreach (var group in nextGroups)
                {
                    _groups[group.Id] = group;
                }

                if (ActiveGroupId is not null && !_groups.ContainsKey(ActiveGroupId))
                {
                    ActiveGroupId = _groups.Keys.LastOrDefault();
                }

                var nextBookmarks = _bookmarks.Values
                    .Select(bookmark =>
                    {
                        var focusedNodeId = bookmark.FocusedNodeId == id ? null : bookmark.FocusedNodeId;
                        var selectedNodeIds = bookmark.SelectedNodeIds
                            .Where(nodeId => nodeId != id)
                            .ToArray();
                        var focusedPanel = bookmark.FocusedPanel is { } focusedPanelBookmark &&
                                          focusedPanelBookmark.NodeId == id
                            ? null
                            : bookmark.FocusedPanel;
                        var selectedPanels = bookmark.SelectedPanels
                            .Where(panel => panel.NodeId != id)
                            .ToArray();

                        return bookmark with
                        {
                            FocusedNodeId = focusedNodeId,
                            SelectedNodeIds = selectedNodeIds,
                            FocusedPanel = focusedPanel,
                            SelectedPanels = selectedPanels
                        };
                    })
                    .ToArray();

                _bookmarks.Clear();
                foreach (var bookmark in nextBookmarks)
                {
                    _bookmarks[bookmark.Name] = bookmark;
                }

                if (FocusedNodeId == id)
                {
                    FocusedNodeId = null;
                }

                if (FocusedPanel is { } focusedPanel && focusedPanel.NodeId == id)
                {
                    FocusedPanel = null;
                }

                if (_enteredNodeId == id)
                {
                    _enteredNodeId = null;
                }

                _expandedNodeIds.Remove(id);

                return true;
            }
        }

        public bool TryGet(NodeId id, out SceneNode node)
        {
            lock (_gate)
            {
                if (_nodes.TryGetValue(id, out var existing))
                {
                    node = existing;
                    return true;
                }

                node = default!;
                return false;
            }
        }

        public bool Contains(NodeId id)
        {
            lock (_gate)
            {
                return _nodes.ContainsKey(id);
            }
        }

        public bool TryFocus(NodeId id)
        {
            lock (_gate)
            {
                if (!_nodes.ContainsKey(id))
                {
                    return false;
                }

                FocusedNodeId = id;
                FocusedPanel = null;
                return true;
            }
        }

        public bool TryFocusPanel(NodeId id, string viewRef)
        {
            lock (_gate)
            {
                if (!TryGetPanelTargetUnderLock(id, viewRef, out var panelTarget))
                {
                    return false;
                }

                FocusedNodeId = id;
                FocusedPanel = panelTarget;
                return true;
            }
        }

        public void ClearFocus()
        {
            lock (_gate)
            {
                FocusedNodeId = null;
                FocusedPanel = null;
            }
        }

        public bool Select(NodeId id)
        {
            lock (_gate)
            {
                if (!_nodes.ContainsKey(id))
                {
                    return false;
                }

                return _selectedNodeIds.Add(id);
            }
        }

        public bool Deselect(NodeId id)
        {
            lock (_gate)
            {
                return _selectedNodeIds.Remove(id);
            }
        }

        public void SetSelection(IEnumerable<NodeId> ids)
        {
            lock (_gate)
            {
                _selectedNodeIds.Clear();
                _selectedPanels.Clear();

                foreach (var id in ids)
                {
                    if (_nodes.ContainsKey(id))
                    {
                        _selectedNodeIds.Add(id);
                    }
                }
            }
        }

        public void ClearSelection()
        {
            lock (_gate)
            {
                _selectedNodeIds.Clear();
                _selectedPanels.Clear();
            }
        }

        public bool SelectPanel(NodeId id, string viewRef)
        {
            lock (_gate)
            {
                return TryGetPanelTargetUnderLock(id, viewRef, out var panelTarget) &&
                       _selectedPanels.Add(panelTarget);
            }
        }

        public void SetPanelSelection(IEnumerable<PanelTarget> panelTargets)
        {
            lock (_gate)
            {
                _selectedPanels.Clear();
                _selectedNodeIds.Clear();

                foreach (var panelTarget in panelTargets)
                {
                    if (TryGetPanelTargetUnderLock(panelTarget.NodeId, panelTarget.ViewRef, out var normalized))
                    {
                        _selectedPanels.Add(normalized);
                    }
                }
            }
        }

        public bool TryAttachPanel(
            NodeId id,
            string viewRef,
            Vector3? localOffset = null,
            Vector2? size = null,
            string? anchor = null,
            bool? isVisible = null,
            string? surfaceKind = null,
            string? paneletteKind = null,
            int? paneletteTier = null,
            PanelCommandSurfaceMetadata? commandSurface = null)
        {
            lock (_gate)
            {
                if (!_nodes.ContainsKey(id) || string.IsNullOrWhiteSpace(viewRef))
                {
                    return false;
                }

                var normalizedAnchor = string.IsNullOrWhiteSpace(anchor) ? "center" : anchor!;
                var normalizedSize = size ?? new Vector2(1.0f, 0.6f);
                if (normalizedSize.X <= 0f || normalizedSize.Y <= 0f)
                {
                    normalizedSize = new Vector2(1.0f, 0.6f);
                }

                var normalizedOffset = localOffset ?? new Vector3(0f, 0f, 0.15f);
                var semantics = PanelSurfaceSemantics.FromExplicitOrViewRef(
                    surfaceKind,
                    paneletteKind,
                    paneletteTier,
                    viewRef);

                _panelAttachments[id] = new PanelAttachment(
                    id,
                    viewRef,
                    normalizedOffset,
                    normalizedSize,
                    normalizedAnchor,
                    isVisible ?? true,
                    semantics,
                    commandSurface);

                return true;
            }
        }

        public bool RemovePanelAttachment(NodeId id)
        {
            lock (_gate)
            {
                var removed = _panelAttachments.Remove(id);
                if (removed)
                {
                    _selectedPanels.RemoveWhere(x => x.NodeId == id);

                    if (FocusedPanel is { } focusedPanel && focusedPanel.NodeId == id)
                    {
                        FocusedPanel = null;
                    }
                }

                return removed;
            }
        }

        public bool TryConnect(
            NodeId sourceId,
            NodeId targetId,
            string? kind = null,
            float? weight = null)
        {
            lock (_gate)
            {
                if (!_nodes.ContainsKey(sourceId) || !_nodes.ContainsKey(targetId))
                {
                    return false;
                }

                var normalizedKind = string.IsNullOrWhiteSpace(kind) ? "related" : kind!;
                var normalizedWeight = weight is > 0f ? weight.Value : 1.0f;
                var linkId = BuildLinkId(sourceId, targetId, normalizedKind);

                _links[linkId] = new SceneLink(
                    linkId,
                    sourceId,
                    targetId,
                    normalizedKind,
                    normalizedWeight);

                return true;
            }
        }

        public bool TryDisconnect(
            NodeId sourceId,
            NodeId targetId,
            string? kind = null)
        {
            lock (_gate)
            {
                if (!_nodes.ContainsKey(sourceId) || !_nodes.ContainsKey(targetId))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(kind))
                {
                    var normalizedKind = kind.Trim();
                    return _links.Remove(BuildLinkId(sourceId, targetId, normalizedKind));
                }

                var linkIds = _links.Values
                    .Where(link => link.SourceId == sourceId && link.TargetId == targetId)
                    .Select(link => link.Id)
                    .ToArray();

                if (linkIds.Length == 0)
                {
                    return false;
                }

                foreach (var linkId in linkIds)
                {
                    _links.Remove(linkId);
                }

                return true;
            }
        }

        public bool TryEnterNode(NodeId id)
        {
            lock (_gate)
            {
                if (!_nodes.ContainsKey(id))
                {
                    return false;
                }

                _enteredNodeId = id;
                return true;
            }
        }

        public bool TryExitNode(NodeId? expectedId, out NodeId? previousId)
        {
            lock (_gate)
            {
                previousId = _enteredNodeId;
                if (_enteredNodeId is null)
                {
                    return false;
                }

                if (expectedId is { } id && _enteredNodeId.Value != id)
                {
                    return false;
                }

                _enteredNodeId = null;
                return previousId is not null;
            }
        }

        public bool TryExpandNode(NodeId id)
        {
            lock (_gate)
            {
                if (!_nodes.ContainsKey(id))
                {
                    return false;
                }

                return _expandedNodeIds.Add(id);
            }
        }

        public bool TryCollapseNode(NodeId id)
        {
            lock (_gate)
            {
                if (!_nodes.ContainsKey(id))
                {
                    return false;
                }

                return _expandedNodeIds.Remove(id);
            }
        }

        public bool IsNodeExpanded(NodeId id)
        {
            lock (_gate)
            {
                return _expandedNodeIds.Contains(id);
            }
        }

        public bool TryGroupSelection(string? label, out SceneGroup group)
        {
            lock (_gate)
            {
                var nodeIds = _selectedNodeIds
                    .Where(_nodes.ContainsKey)
                    .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                    .ToArray();

                if (nodeIds.Length < 2)
                {
                    group = default!;
                    return false;
                }

                var groupId = $"group:{Guid.NewGuid():N}";
                var groupLabel = string.IsNullOrWhiteSpace(label)
                    ? $"Group {(_groups.Count + 1)}"
                    : label.Trim();

                group = new SceneGroup(groupId, groupLabel, nodeIds);
                _groups[groupId] = group;
                ActiveGroupId = groupId;
                return true;
            }
        }

        public bool TryAddSelectionToGroup(string groupId, out SceneGroup group)
        {
            lock (_gate)
            {
                if (string.IsNullOrWhiteSpace(groupId) || !_groups.TryGetValue(groupId, out var existing))
                {
                    group = default!;
                    return false;
                }

                var selectedNodeIds = _selectedNodeIds
                    .Where(_nodes.ContainsKey)
                    .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                    .ToArray();

                if (selectedNodeIds.Length == 0)
                {
                    group = default!;
                    return false;
                }

                var mergedNodeIds = existing.NodeIds
                    .Concat(selectedNodeIds)
                    .Distinct()
                    .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                    .ToArray();

                if (mergedNodeIds.Length == existing.NodeIds.Count)
                {
                    group = default!;
                    return false;
                }

                group = existing with { NodeIds = mergedNodeIds };
                _groups[groupId] = group;
                ActiveGroupId = groupId;
                return true;
            }
        }

        public bool TryRemoveSelectionFromGroup(string groupId, out SceneGroup? group, out bool deletedGroup)
        {
            lock (_gate)
            {
                group = null;
                deletedGroup = false;

                if (string.IsNullOrWhiteSpace(groupId) || !_groups.TryGetValue(groupId, out var existing))
                {
                    return false;
                }

                var remainingNodeIds = existing.NodeIds
                    .Where(nodeId => !_selectedNodeIds.Contains(nodeId))
                    .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                    .ToArray();

                if (remainingNodeIds.Length == existing.NodeIds.Count)
                {
                    return false;
                }

                if (remainingNodeIds.Length == 0)
                {
                    _groups.Remove(groupId);
                    deletedGroup = true;
                    ActiveGroupId = _groups.Keys.LastOrDefault();
                    return true;
                }

                group = existing with { NodeIds = remainingNodeIds };
                _groups[groupId] = group;
                ActiveGroupId = groupId;
                return true;
            }
        }

        public bool TryDeleteGroup(string groupId, out SceneGroup group)
        {
            lock (_gate)
            {
                if (string.IsNullOrWhiteSpace(groupId) || !_groups.TryGetValue(groupId, out var existing))
                {
                    group = default!;
                    return false;
                }

                group = existing;

                _groups.Remove(groupId);

                if (string.Equals(ActiveGroupId, groupId, StringComparison.Ordinal))
                {
                    ActiveGroupId = _groups.Keys.LastOrDefault();
                }

                return true;
            }
        }

        public bool TrySaveBookmark(string name, out SceneBookmark bookmark)
        {
            lock (_gate)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    bookmark = default!;
                    return false;
                }

                var normalizedName = name.Trim();
                bookmark = new SceneBookmark(
                    normalizedName,
                    FocusedNodeId,
                    _selectedNodeIds
                        .Where(_nodes.ContainsKey)
                        .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                        .ToArray(),
                    FocusedPanel is { } focusedPanel &&
                    TryGetPanelTargetUnderLock(focusedPanel.NodeId, focusedPanel.ViewRef, out var normalizedFocusedPanel)
                        ? normalizedFocusedPanel
                        : null,
                    _selectedPanels
                        .Where(panel => TryGetPanelTargetUnderLock(panel.NodeId, panel.ViewRef, out _))
                        .OrderBy(panel => panel.NodeId.ToString(), StringComparer.Ordinal)
                        .ThenBy(panel => panel.ViewRef, StringComparer.Ordinal)
                        .ToArray(),
                    _lastView);

                _bookmarks[normalizedName] = bookmark;
                return true;
            }
        }

        public bool TryRestoreBookmark(string name, out SceneBookmark bookmark)
        {
            lock (_gate)
            {
                if (string.IsNullOrWhiteSpace(name) || !_bookmarks.TryGetValue(name.Trim(), out var stored))
                {
                    bookmark = default!;
                    return false;
                }

                FocusedNodeId = stored.FocusedNodeId is { } focusedNodeId && _nodes.ContainsKey(focusedNodeId)
                    ? focusedNodeId
                    : null;

                _selectedNodeIds.Clear();
                foreach (var nodeId in stored.SelectedNodeIds)
                {
                    if (_nodes.ContainsKey(nodeId))
                    {
                        _selectedNodeIds.Add(nodeId);
                    }
                }

                FocusedPanel = stored.FocusedPanel is { } focusedPanel &&
                               TryGetPanelTargetUnderLock(focusedPanel.NodeId, focusedPanel.ViewRef, out var normalizedFocusedPanel)
                    ? normalizedFocusedPanel
                    : null;

                _selectedPanels.Clear();
                foreach (var panel in stored.SelectedPanels)
                {
                    if (TryGetPanelTargetUnderLock(panel.NodeId, panel.ViewRef, out var normalizedPanel))
                    {
                        _selectedPanels.Add(normalizedPanel);
                    }
                }

                bookmark = new SceneBookmark(
                    stored.Name,
                    FocusedNodeId,
                    _selectedNodeIds
                        .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                        .ToArray(),
                    FocusedPanel,
                    _selectedPanels
                        .OrderBy(panel => panel.NodeId.ToString(), StringComparer.Ordinal)
                        .ThenBy(panel => panel.ViewRef, StringComparer.Ordinal)
                        .ToArray(),
                    stored.View);

                _bookmarks[stored.Name] = bookmark;
                return true;
            }
        }

        public bool TrySetInteractionMode(string? mode)
        {
            lock (_gate)
            {
                var normalizedMode = NormalizeInteractionMode(mode);
                if (string.Equals(InteractionMode, normalizedMode, StringComparison.Ordinal))
                {
                    return false;
                }

                InteractionMode = normalizedMode;
                return true;
            }
        }

        public SceneSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return CreateSnapshotUnderLock();
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (_gate)
                {
                    return _nodes.Count == 0;
                }
            }
        }

        private SceneSnapshot CreateSnapshotUnderLock()
        {
            return new SceneSnapshot(
                _nodes.Values.ToList(),
                FocusedNodeId,
                _selectedNodeIds.ToArray(),
                new Dictionary<NodeId, PanelAttachment>(_panelAttachments),
                FocusedPanel,
                _selectedPanels.ToArray(),
                _links.Values.OrderBy(link => link.Id, StringComparer.Ordinal).ToArray(),
                _groups.Values.OrderBy(group => group.Label, StringComparer.Ordinal).ToArray(),
                _bookmarks.Values.OrderBy(bookmark => bookmark.Name, StringComparer.Ordinal).ToArray(),
                ActiveGroupId,
                InteractionMode);
        }

        private void RestoreSnapshotUnderLock(SceneSnapshot snapshot)
        {
            _nodes.Clear();
            foreach (var node in snapshot.Nodes)
            {
                _nodes[node.Id] = node;
            }

            _selectedNodeIds.Clear();
            if (snapshot.SelectedNodeIds is not null)
            {
                foreach (var nodeId in snapshot.SelectedNodeIds.Where(_nodes.ContainsKey))
                {
                    _selectedNodeIds.Add(nodeId);
                }
            }

            _panelAttachments.Clear();
            if (snapshot.PanelAttachments is not null)
            {
                foreach (var attachment in snapshot.PanelAttachments)
                {
                    _panelAttachments[attachment.Key] = attachment.Value;
                }
            }

            _links.Clear();
            if (snapshot.Links is not null)
            {
                foreach (var link in snapshot.Links)
                {
                    _links[link.Id] = link;
                }
            }

            _groups.Clear();
            if (snapshot.Groups is not null)
            {
                foreach (var group in snapshot.Groups)
                {
                    _groups[group.Id] = group;
                }
            }

            _bookmarks.Clear();
            if (snapshot.Bookmarks is not null)
            {
                foreach (var bookmark in snapshot.Bookmarks)
                {
                    _bookmarks[bookmark.Name] = bookmark;
                }
            }

            ActiveGroupId = snapshot.ActiveGroupId is not null && _groups.ContainsKey(snapshot.ActiveGroupId)
                ? snapshot.ActiveGroupId
                : _groups.Keys.LastOrDefault();

            FocusedNodeId = snapshot.FocusedNodeId is { } focusedNodeId && _nodes.ContainsKey(focusedNodeId)
                ? focusedNodeId
                : null;
            FocusedPanel = snapshot.FocusedPanel is { } focusedPanel &&
                           TryGetPanelTargetUnderLock(focusedPanel.NodeId, focusedPanel.ViewRef, out var normalizedFocusedPanel)
                ? normalizedFocusedPanel
                : null;

            _selectedPanels.Clear();
            if (snapshot.SelectedPanels is not null)
            {
                foreach (var panel in snapshot.SelectedPanels)
                {
                    if (TryGetPanelTargetUnderLock(panel.NodeId, panel.ViewRef, out var normalizedPanel))
                    {
                        _selectedPanels.Add(normalizedPanel);
                    }
                }
            }

            InteractionMode = NormalizeInteractionMode(snapshot.InteractionMode);
        }

        private bool TryGetPanelTargetUnderLock(NodeId id, string viewRef, out PanelTarget panelTarget)
        {
            panelTarget = default;

            if (!_nodes.ContainsKey(id) ||
                !_panelAttachments.TryGetValue(id, out var attachment) ||
                string.IsNullOrWhiteSpace(viewRef) ||
                !string.Equals(attachment.ViewRef, viewRef, StringComparison.Ordinal))
            {
                return false;
            }

            panelTarget = new PanelTarget(id, attachment.ViewRef);
            return true;
        }

        private static string BuildLinkId(NodeId sourceId, NodeId targetId, string kind) =>
            $"{sourceId}->{targetId}:{kind}";

        private static string NormalizeInteractionMode(string? mode)
        {
            if (string.Equals(mode, "move", StringComparison.OrdinalIgnoreCase))
            {
                return "move";
            }

            if (string.Equals(mode, "marquee", StringComparison.OrdinalIgnoreCase))
            {
                return "marquee";
            }

            return "navigate";
        }
    }
}
