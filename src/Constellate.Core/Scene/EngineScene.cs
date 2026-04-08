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

        public NodeId? FocusedNodeId { get; private set; }
        public PanelTarget? FocusedPanel { get; private set; }

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
                if (removed)
                {
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

                    if (FocusedNodeId == id)
                    {
                        FocusedNodeId = null;
                    }

                    if (FocusedPanel is { } focusedPanel && focusedPanel.NodeId == id)
                    {
                        FocusedPanel = null;
                    }
                }

                return removed;
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
            bool? isVisible = null)
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

                _panelAttachments[id] = new PanelAttachment(
                    id,
                    viewRef,
                    normalizedOffset,
                    normalizedSize,
                    normalizedAnchor,
                    isVisible ?? true);

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
                return true;
            }
        }

        public SceneSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new SceneSnapshot(
                    _nodes.Values.ToList(),
                    FocusedNodeId,
                    _selectedNodeIds.ToArray(),
                    new Dictionary<NodeId, PanelAttachment>(_panelAttachments),
                    FocusedPanel,
                    _selectedPanels.ToArray(),
                    _links.Values.OrderBy(link => link.Id, StringComparer.Ordinal).ToArray(),
                    _groups.Values.OrderBy(group => group.Label, StringComparer.Ordinal).ToArray());
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
    }
}
