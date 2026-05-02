using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Constellate.Core.Scene;
using Constellate.Core.Messaging;
using Constellate.Renderer.OpenTK.Scene;
using Constellate.SDK;
using OpenTK.Mathematics;
using NVec3 = System.Numerics.Vector3;

namespace Constellate.Renderer.OpenTK.Controls
{
    public partial class OpenTkViewportControl
    {
        private const string SurfaceCommand_AttachMetadataPaneletteForNode = "Engine.AttachMetadataPaneletteForNode";
        private const string SurfaceCommand_AttachLabelPaneletteForNode = "Engine.AttachLabelPaneletteForNode";
        private const string SurfaceCommand_ClearPaneletteForNode = "Engine.ClearPaneletteForNode";
        private const string SurfaceCommand_AttachMetadataPanelettesForAllNodes = "Engine.AttachMetadataPanelettesForAllNodes";
        private const string SurfaceCommand_AttachLabelPanelettesForAllNodes = "Engine.AttachLabelPanelettesForAllNodes";
        private const string SurfaceCommand_ClearPanelettesForAllNodes = "Engine.ClearPanelettesForAllNodes";
        private const string SurfaceCommand_AttachDetailMetadataPaneletteForNode = "Engine.AttachDetailMetadataPaneletteForNode";
        private const string SurfaceCommand_AttachDetailMetadataPanelettesForAllNodes = "Engine.AttachDetailMetadataPanelettesForAllNodes";
        private const string SurfaceCommand_PromotePaneletteToShell = "Engine.PromotePaneletteToShell";
        private const string SurfaceCommand_CreateNodeAtPointer = "Engine.CreateNodeAtPointer";
        private const string SurfaceCommand_ExitNodeContext = "Engine.Node.ExitContext";
        private const string SurfaceCommand_SetModeNavigate = "Engine.SetInteractionMode.Navigate";
        private const string SurfaceCommand_SetModeMarquee = "Engine.SetInteractionMode.Marquee";
        private const string SurfaceCommand_SetModeMove = "Engine.SetInteractionMode.Move";
        private const string SurfaceCommand_SaveBookmark = "Engine.View.SaveBookmark";
        private const string SurfaceCommand_RestoreBookmark = "Engine.View.RestoreBookmark";
        private const string SurfaceCommand_NudgeNodeLeft = "Engine.Transform.NudgeNodeLeft";
        private const string SurfaceCommand_NudgeNodeRight = "Engine.Transform.NudgeNodeRight";
        private const string SurfaceCommand_NudgeNodeUp = "Engine.Transform.NudgeNodeUp";
        private const string SurfaceCommand_NudgeNodeDown = "Engine.Transform.NudgeNodeDown";
        private const string SurfaceCommand_NodeGrow = "Engine.Transform.GrowNode";
        private const string SurfaceCommand_NodeShrink = "Engine.Transform.ShrinkNode";

        private const string SurfaceCommand_LinkSelectSource = "Engine.Link.SelectSource";
        private const string SurfaceCommand_LinkSelectTarget = "Engine.Link.SelectTarget";
        private const string SurfaceCommand_LinkFrameEndpoints = "Engine.Link.FrameEndpoints";
        private const string SurfaceCommand_LinkUnlink = "Engine.Link.Unlink";

        private const string SurfaceCommand_GroupSelectMembers = "Engine.Group.SelectMembers";
        private const string SurfaceCommand_GroupFrame = "Engine.Group.Frame";
        private const string SurfaceCommand_GroupAddSelection = "Engine.Group.AddSelection";
        private const string SurfaceCommand_GroupRemoveSelection = "Engine.Group.RemoveSelection";
        private const string SurfaceCommand_GroupDelete = "Engine.Group.Delete";

        private enum BackgroundSurfaceKind
        {
            None,
            Background,
            Link,
            Group
        }

        private bool _hasBackgroundCommandSurface;
        private PanelCommandSurfaceMetadata? _backgroundCommandSurfaceMetadata;
        private Rect _backgroundCommandOverlayRect;
        private Rect[] _backgroundCommandCommandRects = Array.Empty<Rect>();
        private int _backgroundCommandIndex;
        private Point _backgroundCommandAnchorPoint;
        private BackgroundSurfaceKind _backgroundSurfaceKind = BackgroundSurfaceKind.None;
        private string? _activeLinkSourceId;
        private string? _activeLinkTargetId;
        private string? _activeGroupId;

        private static PanelCommandDescriptor CreateCommandDescriptor(string commandId, string displayLabel, string catalogCapabilityId)
        {
            return PanelCommandDescriptor.Create(commandId, displayLabel, catalogCapabilityId)
                ?? throw new InvalidOperationException($"Unable to create panel command descriptor for '{commandId}'.");
        }

        private static PanelCommandDescriptor[] BuildNodeTransformCommandDescriptors()
        {
            return
            [
                CreateCommandDescriptor(SurfaceCommand_NudgeNodeLeft, "Nudge Left", "engine.command.nudge_focused_left"),
                CreateCommandDescriptor(SurfaceCommand_NudgeNodeRight, "Nudge Right", "engine.command.nudge_focused_right"),
                CreateCommandDescriptor(SurfaceCommand_NudgeNodeUp, "Nudge Up", "engine.command.nudge_focused_up"),
                CreateCommandDescriptor(SurfaceCommand_NudgeNodeDown, "Nudge Down", "engine.command.nudge_focused_down"),
                CreateCommandDescriptor(SurfaceCommand_NodeGrow, "Grow Node", "engine.command.grow_focused_node"),
                CreateCommandDescriptor(SurfaceCommand_NodeShrink, "Shrink Node", "engine.command.shrink_focused_node")
            ];
        }

        private static PanelCommandDescriptor[] BuildAllNodePaneletteCommandDescriptors()
        {
            return
            [
                CreateCommandDescriptor(SurfaceCommand_AttachMetadataPanelettesForAllNodes, "Attach Metadata Panelettes (All Nodes)", "engine.command.attach_metadata_panelettes_for_all_nodes"),
                CreateCommandDescriptor(SurfaceCommand_AttachDetailMetadataPanelettesForAllNodes, "Attach Detailed Metadata Panelettes (All Nodes)", "engine.command.attach_detail_metadata_panelettes_for_all_nodes"),
                CreateCommandDescriptor(SurfaceCommand_AttachLabelPanelettesForAllNodes, "Attach Label Panelettes (All Nodes)", "engine.command.attach_label_panelettes_for_all_nodes"),
                CreateCommandDescriptor(SurfaceCommand_ClearPanelettesForAllNodes, "Remove Panelettes (All Nodes)", "engine.command.clear_panelettes_for_all_nodes")
            ];
        }

        private static PanelCommandDescriptor[] BuildInteractionModeCommandDescriptors()
        {
            return
            [
                CreateCommandDescriptor(SurfaceCommand_SetModeNavigate, "Set Mode: Navigate", "engine.command.activate_navigate_mode"),
                CreateCommandDescriptor(SurfaceCommand_SetModeMarquee, "Set Mode: Marquee", "engine.command.activate_marquee_mode"),
                CreateCommandDescriptor(SurfaceCommand_SetModeMove, "Set Mode: Move", "engine.command.activate_move_mode")
            ];
        }

        private static PanelCommandDescriptor[] BuildNodeContextCommandDescriptors()
        {
            var commands = new List<PanelCommandDescriptor>
            {
                CreateCommandDescriptor(CommandNames.CenterOnNode, "Center on Node", "engine.command.center_focused_node"),
                CreateCommandDescriptor(CommandNames.Select, "Select", "engine.command.select_node_context_target"),
                CreateCommandDescriptor(CommandNames.Focus, "Focus", "engine.command.focus_node_context_target"),
                CreateCommandDescriptor(CommandNames.EnterNode, "Enter Node", "engine.command.enter_node_context_target"),
                CreateCommandDescriptor(SurfaceCommand_AttachMetadataPaneletteForNode, "Attach Metadata Panelette", "engine.command.attach_metadata_panelette_for_node"),
                CreateCommandDescriptor(SurfaceCommand_AttachLabelPaneletteForNode, "Attach Label Panelette", "engine.command.attach_label_panelette_for_node"),
                CreateCommandDescriptor(SurfaceCommand_AttachDetailMetadataPaneletteForNode, "Attach Detailed Metadata Panelette", "engine.command.attach_detail_metadata_panelette_for_node"),
                CreateCommandDescriptor(SurfaceCommand_ClearPaneletteForNode, "Remove Panelette", "engine.command.clear_panelette_for_node")
            };

            commands.AddRange(BuildNodeTransformCommandDescriptors());
            return commands.ToArray();
        }

        private static PanelCommandDescriptor[] BuildLinkContextCommandDescriptors()
        {
            var commands = new List<PanelCommandDescriptor>
            {
                CreateCommandDescriptor(SurfaceCommand_LinkSelectSource, "Select Source", "engine.command.link_select_source"),
                CreateCommandDescriptor(SurfaceCommand_LinkSelectTarget, "Select Target", "engine.command.link_select_target"),
                CreateCommandDescriptor(SurfaceCommand_LinkFrameEndpoints, "Frame Endpoints", "engine.command.link_frame_endpoints")
            };

            commands.AddRange(BuildNodeTransformCommandDescriptors());
            commands.Add(CreateCommandDescriptor(SurfaceCommand_LinkUnlink, "Unlink", "engine.command.link_unlink_context"));
            return commands.ToArray();
        }

        private static PanelCommandDescriptor[] BuildGroupContextCommandDescriptors()
        {
            return
            [
                CreateCommandDescriptor(SurfaceCommand_GroupSelectMembers, "Select Group Nodes", "engine.command.group_select_members"),
                CreateCommandDescriptor(SurfaceCommand_GroupFrame, "Frame Group", "engine.command.group_frame"),
                CreateCommandDescriptor(SurfaceCommand_GroupAddSelection, "Add Selection to Group", "engine.command.group_add_selection_to_context_group"),
                CreateCommandDescriptor(SurfaceCommand_GroupRemoveSelection, "Remove Selection from Group", "engine.command.group_remove_selection_from_context_group"),
                CreateCommandDescriptor(SurfaceCommand_GroupDelete, "Delete Group", "engine.command.group_delete_context_group")
            ];
        }

        private static PanelCommandDescriptor[] BuildBackgroundContextCommandDescriptors(bool hasEnteredNode)
        {
            var commands = new List<PanelCommandDescriptor>
            {
                CreateCommandDescriptor(SurfaceCommand_CreateNodeAtPointer, "Create Node Here", "engine.command.create_node_at_pointer"),
                CreateCommandDescriptor(CommandNames.ClearFocus, "Clear Focus", "engine.command.clear_focus"),
                CreateCommandDescriptor(CommandNames.ClearSelection, "Clear Selection", "engine.command.clear_selection"),
                CreateCommandDescriptor(CommandNames.HomeView, "Home View", "engine.command.home_view"),
                CreateCommandDescriptor(CommandNames.FrameSelection, "Frame Selection", "engine.command.frame_selection")
            };

            if (hasEnteredNode)
            {
                commands.Add(CreateCommandDescriptor(SurfaceCommand_ExitNodeContext, "Exit Node Context", "engine.command.exit_node_context"));
            }

            commands.Add(CreateCommandDescriptor(SurfaceCommand_SaveBookmark, "Save Bookmark", "engine.command.save_bookmark"));
            commands.Add(CreateCommandDescriptor(SurfaceCommand_RestoreBookmark, "Restore Bookmark", "engine.command.restore_latest_bookmark"));
            commands.AddRange(BuildInteractionModeCommandDescriptors());
            commands.AddRange(BuildAllNodePaneletteCommandDescriptors());
            return commands.ToArray();
        }

        private static PanelCommandSurfaceMetadata CreateContextSurfaceMetadata(
            string surfaceName,
            string surfaceGroup,
            PanelCommandDescriptor[] commands)
        {
            return new PanelCommandSurfaceMetadata(
                SurfaceName: surfaceName,
                SurfaceGroup: surfaceGroup,
                Commands: commands,
                SurfaceSource: "engine");
        }

        private void OpenBackgroundCommandSurface(
            Point point,
            BackgroundSurfaceKind kind,
            PanelCommandSurfaceMetadata metadata,
            string? activeLinkSourceId = null,
            string? activeLinkTargetId = null,
            string? activeGroupId = null)
        {
            ClearBackgroundCommandSurface(invalidate: false);

            _backgroundSurfaceKind = kind;
            _activeLinkSourceId = activeLinkSourceId;
            _activeLinkTargetId = activeLinkTargetId;
            _activeGroupId = activeGroupId;
            _backgroundCommandAnchorPoint = point;

            LayoutBackgroundCommandSurface(
                point,
                metadata,
                new Rect(Bounds.Size),
                out _backgroundCommandOverlayRect,
                out _backgroundCommandCommandRects);

            _backgroundCommandSurfaceMetadata = metadata;
            _backgroundCommandIndex = 0;
            _hasBackgroundCommandSurface = true;
            InvalidateVisual();
        }

        private bool TryOpenOrAdvancePanelCommandSurface(Point point)
        {
            if (!TryHitTestPanelSurface(point, out var panel, out var semantics) ||
                !semantics.IsMetadataPanelette ||
                panel.CommandSurface is not { HasCommands: true } commandSurface)
            {
                return false;
            }

            SendCommand(CommandNames.FocusPanel, new FocusPanelPayload(panel.NodeId, panel.ViewRef));

            if (IsActiveCommandSurface(panel))
            {
                _activeCommandSurface.Advance(1, commandSurface.Commands.Count);
                InvalidateVisual();
                return true;
            }

            SetActiveCommandSurface(panel, commandSurface);
            return true;
        }

        private bool TryOpenNodeContextSurface(Point point)
        {
            var nodeId = HitTestProjectedNodeId(point);
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            ClearBackgroundCommandSurface(invalidate: false);

            var commandDescriptors = BuildNodeContextCommandDescriptors();

            if (commandDescriptors.Length == 0)
            {
                return false;
            }

            var metadata = new PanelCommandSurfaceMetadata(
                SurfaceName: "node.primary",
                SurfaceGroup: "node",
                Commands: commandDescriptors,
                SurfaceSource: "engine");

            // Ephemeral panel-surface node anchored to the clicked node; used only for overlay layout/drawing.
            var ephemeralPanel = new PanelSurfaceNode(
                NodeId: nodeId,
                ViewRef: "__node_context__",
                LocalOffset: new Vector3(0f, 0.15f, 0f),
                Size: new Vector2(1f, 1f),
                Anchor: "bottom",
                IsVisible: true,
                IsFocused: false,
                IsSelected: false,
                Semantics: new PanelSurfaceSemantics("panelette", "metadata", 1),
                CommandSurface: metadata);

             PublishFocusOrigin("mouse");
             SendCommand(CommandNames.Focus, new FocusEntityPayload(nodeId));

            if (IsActiveCommandSurface(ephemeralPanel))
            {
                _activeCommandSurface.Advance(1, metadata.Commands.Count);
                InvalidateVisual();
                return true;
            }

            _hasEphemeralNodeCommandSurface = true;
            _ephemeralNodePanel = ephemeralPanel;
            _ephemeralNodeMetadata = metadata;

            SetActiveCommandSurface(ephemeralPanel, metadata);
            return true;
        }

        private bool TryOpenLinkContextSurface(Point point)
        {
            var snapshot = GetRenderSceneSnapshot();
            if (snapshot.Links.Length == 0 || snapshot.Nodes.Length == 0)
            {
                return false;
            }

            var byId = snapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var view = ComputeView();
            var proj = ComputeProjection();
            var bounds = new Rect(Bounds.Size);

            string? bestSourceId = null;
            string? bestTargetId = null;
            double bestDistance = double.MaxValue;

            foreach (var link in snapshot.Links)
            {
                if (!byId.TryGetValue(link.SourceId, out var source) ||
                    !byId.TryGetValue(link.TargetId, out var target))
                {
                    continue;
                }

                if (!TryProjectWorldPoint(source.Position, view, proj, bounds, out var sp) ||
                    !TryProjectWorldPoint(target.Position, view, proj, bounds, out var tp))
                {
                    continue;
                }

                var dist = DistancePointToSegment(point, sp, tp);
                if (dist < 8.0 && dist < bestDistance)
                {
                    bestDistance = dist;
                    bestSourceId = link.SourceId;
                    bestTargetId = link.TargetId;
                }
            }

            if (bestSourceId is null || bestTargetId is null)
            {
                return false;
            }

            ClearBackgroundCommandSurface(invalidate: false);

            _backgroundSurfaceKind = BackgroundSurfaceKind.Link;
            _activeLinkSourceId = bestSourceId;
            _activeLinkTargetId = bestTargetId;
            _activeGroupId = null;

            var commandDescriptors = BuildLinkContextCommandDescriptors();

            if (commandDescriptors.Length == 0)
            {
                return false;
            }

            var metadata = new PanelCommandSurfaceMetadata(
                SurfaceName: "link.primary",
                SurfaceGroup: "link",
                Commands: commandDescriptors,
                SurfaceSource: "engine");

            _backgroundCommandAnchorPoint = point;

            LayoutBackgroundCommandSurface(
                point,
                metadata,
                new Rect(Bounds.Size),
                out _backgroundCommandOverlayRect,
                out _backgroundCommandCommandRects);

            _backgroundCommandSurfaceMetadata = metadata;
            _backgroundCommandIndex = 0;
            _hasBackgroundCommandSurface = true;
            InvalidateVisual();
            return true;
        }

        private bool TryHitTestGroupAtPoint(Point point, out string? bestGroupId)
        {
            bestGroupId = null;

            var snapshot = GetRenderSceneSnapshot();
            if (snapshot.Groups.Length == 0 || snapshot.Nodes.Length == 0)
            {
                return false;
            }

            var byId = snapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var view = ComputeView();
            var proj = ComputeProjection();
            var bounds = new Rect(Bounds.Size);

            bool hasBest = false;
            double bestArea = double.MaxValue;

            foreach (var group in snapshot.Groups)
            {
                var points = group.NodeIds
                    .Where(byId.ContainsKey)
                    .Select(id =>
                    {
                        var node = byId[id];
                        return TryProjectWorldPoint(node.Position, view, proj, bounds, out var p)
                            ? (ok: true, p)
                            : (ok: false, p: default(Point));
                    })
                    .Where(t => t.ok)
                    .Select(t => t.p)
                    .ToArray();

                if (points.Length < 2)
                {
                    continue;
                }

                var minX = points.Min(p => p.X);
                var minY = points.Min(p => p.Y);
                var maxX = points.Max(p => p.X);
                var maxY = points.Max(p => p.Y);

                const double pad = 10.0;
                var rect = new Rect(
                    minX - pad,
                    minY - pad,
                    Math.Max(2, (maxX - minX) + 2 * pad),
                    Math.Max(2, (maxY - minY) + 2 * pad));

                if (!rect.Contains(point))
                {
                    continue;
                }

                var area = rect.Width * rect.Height;
                if (area < bestArea)
                {
                    bestArea = area;
                    bestGroupId = group.Id;
                    hasBest = true;
                }
            }

            return hasBest && !string.IsNullOrWhiteSpace(bestGroupId);
        }

        private bool TryOpenGroupContextSurface(Point point)
        {
            var snapshot = GetRenderSceneSnapshot();
            if (snapshot.Groups.Length == 0 || snapshot.Nodes.Length == 0)
            {
                return false;
            }

            var byId = snapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var view = ComputeView();
            var proj = ComputeProjection();
            var bounds = new Rect(Bounds.Size);

            bool hasBest = false;
            string? bestGroupId = null;
            double bestArea = double.MaxValue;

            foreach (var group in snapshot.Groups)
            {
                var points = group.NodeIds
                    .Where(byId.ContainsKey)
                    .Select(id =>
                    {
                        var node = byId[id];
                        return TryProjectWorldPoint(node.Position, view, proj, bounds, out var p)
                            ? (ok: true, p)
                            : (ok: false, p: default(Point));
                    })
                    .Where(t => t.ok)
                    .Select(t => t.p)
                    .ToArray();

                if (points.Length < 2)
                {
                    continue;
                }

                var minX = points.Min(p => p.X);
                var minY = points.Min(p => p.Y);
                var maxX = points.Max(p => p.X);
                var maxY = points.Max(p => p.Y);

                const double pad = 10.0;
                var rect = new Rect(
                    minX - pad,
                    minY - pad,
                    Math.Max(2, (maxX - minX) + 2 * pad),
                    Math.Max(2, (maxY - minY) + 2 * pad));

                if (!rect.Contains(point))
                {
                    continue;
                }

                var area = rect.Width * rect.Height;
                if (area < bestArea)
                {
                    bestArea = area;
                    bestGroupId = group.Id;
                    hasBest = true;
                }
            }

            if (!hasBest || string.IsNullOrWhiteSpace(bestGroupId))
            {
                return false;
            }

            var commandDescriptors = BuildGroupContextCommandDescriptors();

            if (commandDescriptors.Length == 0)
            {
                return false;
            }

            var metadata = CreateContextSurfaceMetadata("group.primary", "group", commandDescriptors);
            OpenBackgroundCommandSurface(
                point,
                BackgroundSurfaceKind.Group,
                metadata,
                activeGroupId: bestGroupId);
            return true;
        }

        private bool TryOpenFocusedPanelCommandSurface()
        {
            var focusedPanel = GetRenderSceneSnapshot().PanelSurfaces
                .FirstOrDefault(panel => panel.IsFocused);

            if (string.IsNullOrWhiteSpace(focusedPanel.NodeId) ||
                string.IsNullOrWhiteSpace(focusedPanel.ViewRef))
            {
                return false;
            }

            var semantics = focusedPanel.Semantics ?? PanelSurfaceSemantics.FromViewRef(focusedPanel.ViewRef);
            if (!semantics.IsMetadataPanelette ||
                focusedPanel.CommandSurface is not { HasCommands: true } commandSurface)
            {
                return false;
            }

            ClearBackgroundCommandSurface(invalidate: false);

            if (IsActiveCommandSurface(focusedPanel))
            {
                _activeCommandSurface.Advance(1, commandSurface.Commands.Count);
                InvalidateVisual();
                return true;
            }

            SetActiveCommandSurface(focusedPanel, commandSurface);
            return true;
        }

        private bool TryExecuteCommandSurfaceCommand(PanelSurfaceNode panel, string commandId)
        {
            switch (commandId)
            {
                case CommandNames.CenterOnNode:
                    SendCommand(CommandNames.CenterOnNode, new CenterOnNodePayload(panel.NodeId));
                    return true;
                case CommandNames.Select:
                    SendCommand(CommandNames.Focus, new FocusEntityPayload(panel.NodeId));
                    SendCommand(CommandNames.Select, new SelectEntitiesPayload([panel.NodeId], true));
                    return true;
                case CommandNames.Focus:
                    SendCommand(CommandNames.Focus, new FocusEntityPayload(panel.NodeId));
                    return true;
                case CommandNames.EnterNode:
                    SendCommand(CommandNames.EnterNode, new EnterNodePayload(panel.NodeId));
                    return true;
                case SurfaceCommand_AttachMetadataPaneletteForNode:
                    AttachMetadataPaneletteForNode(panel.NodeId);
                    return true;
                case SurfaceCommand_AttachLabelPaneletteForNode:
                    AttachLabelPaneletteForNode(panel.NodeId);
                    return true;
                case SurfaceCommand_ClearPaneletteForNode:
                    ClearPaneletteForNode(panel.NodeId);
                    return true;
                case SurfaceCommand_NudgeNodeLeft:
                    ApplyNodeTransformFromContextSurface(panel.NodeId, new NVec3(-0.12f, 0f, 0f), 1f);
                    return true;
                case SurfaceCommand_NudgeNodeRight:
                    ApplyNodeTransformFromContextSurface(panel.NodeId, new NVec3(0.12f, 0f, 0f), 1f);
                    return true;
                case SurfaceCommand_NudgeNodeUp:
                    ApplyNodeTransformFromContextSurface(panel.NodeId, new NVec3(0f, 0.08f, 0f), 1f);
                    return true;
                case SurfaceCommand_NudgeNodeDown:
                    ApplyNodeTransformFromContextSurface(panel.NodeId, new NVec3(0f, -0.08f, 0f), 1f);
                    return true;
                case SurfaceCommand_NodeGrow:
                    ApplyNodeTransformFromContextSurface(panel.NodeId, NVec3.Zero, 1.15f);
                    return true;
                case SurfaceCommand_NodeShrink:
                    ApplyNodeTransformFromContextSurface(panel.NodeId, NVec3.Zero, 1f / 1.15f);
                    return true;
                case SurfaceCommand_PromotePaneletteToShell:
                    PublishPanelInteraction(panel.NodeId, panel.ViewRef, "promote_to_pane");
                    return true;
                case SurfaceCommand_AttachMetadataPanelettesForAllNodes:
                    AttachPanelettesForAllNodes("metadata");
                    return true;
                case SurfaceCommand_AttachDetailMetadataPanelettesForAllNodes:
                    AttachDetailMetadataPanelettesForAllNodes();
                    return true;
                case SurfaceCommand_AttachLabelPanelettesForAllNodes:
                    AttachPanelettesForAllNodes("label");
                    return true;
                case SurfaceCommand_ClearPanelettesForAllNodes:
                    ClearPanelettesForAllNodes();
                    return true;
                case CommandNames.FocusPanel:
                    SendCommand(CommandNames.FocusPanel, new FocusPanelPayload(panel.NodeId, panel.ViewRef));
                    return true;
                case CommandNames.SelectPanel:
                    SendCommand(CommandNames.SelectPanel, new SelectPanelPayload(panel.NodeId, panel.ViewRef, true));
                    return true;
                default:
                    return false;
            }
        }

        private bool IsActiveCommandSurface(PanelSurfaceNode panel)
        {
            return _activeCommandSurface.Matches(panel);
        }

        private void SetActiveCommandSurface(
            PanelSurfaceNode panel,
            PanelCommandSurfaceMetadata metadata,
            int commandIndex = 0)
        {
            _activeCommandSurface.Set(panel, commandIndex, metadata.Commands.Count);
            InvalidateVisual();
        }

        private void ClearActiveCommandSurface(bool invalidate = true)
        {
            _activeCommandSurface.Clear();
            _hasEphemeralNodeCommandSurface = false;
            _ephemeralNodeMetadata = null;

            if (invalidate)
            {
                InvalidateVisual();
            }
        }

        private bool TryMoveActiveCommandSurfaceSelection(int delta)
        {
            if (!TryGetActiveCommandSurfaceLayout(out _, out var metadata, out _, out _, out _))
            {
                return false;
            }

            _activeCommandSurface.Advance(delta, metadata.CommandIds.Count);
            InvalidateVisual();
            return true;
        }

        private bool TryHandleActiveCommandSurfaceLeftClick(Point point)
        {
            if (!TryGetActiveCommandSurfaceLayout(out var panel, out var metadata, out var panelRect, out var overlayRect, out var commandRects))
            {
                return false;
            }

            if (overlayRect.Contains(point))
            {
                for (var i = 0; i < commandRects.Length; i++)
                {
                    if (!commandRects[i].Contains(point))
                    {
                        continue;
                    }

                    _activeCommandSurface.Set(panel, i, metadata.Commands.Count);
                    _ = TryInvokeActiveCommandSurfaceSelection();
                    return true;
                }

                return true;
            }

            if (panelRect.Contains(point))
            {
                return true;
            }

            ClearActiveCommandSurface();
            return false;
        }

        private bool TryUpdateActiveCommandSurfaceHover(Point point)
        {
            if (!TryGetActiveCommandSurfaceLayout(out var panel, out var metadata, out var panelRect, out var overlayRect, out var commandRects))
            {
                return false;
            }

            if (overlayRect.Contains(point))
            {
                for (var i = 0; i < commandRects.Length; i++)
                {
                    if (!commandRects[i].Contains(point))
                    {
                        continue;
                    }

                    if (i != _activeCommandSurface.CommandIndex)
                    {
                        _activeCommandSurface.Set(panel, i, metadata.Commands.Count);
                        InvalidateVisual();
                    }

                    return true;
                }

                return true;
            }

            return panelRect.Contains(point);
        }

        private bool TryInvokeActiveCommandSurfaceSelection()
        {
            if (!TryGetActiveCommandSurfaceLayout(out var panel, out var metadata, out _, out _, out _))
            {
                return false;
            }

            var selectedCommand = metadata.Commands[Math.Clamp(
                _activeCommandSurface.CommandIndex,
                0,
                metadata.Commands.Count - 1)];
            var commandId = selectedCommand.CommandId;

            if (string.IsNullOrWhiteSpace(commandId))
            {
                return false;
            }

            var handled = TryExecuteCommandSurfaceCommand(panel, commandId);
            if (handled)
            {
                ClearActiveCommandSurface();
            }

            return handled;
        }

        private static AttachPanelPayload CreateMetadataPanelettePayloadForNode(string nodeId)
        {
            return new AttachPanelPayload(
                Id: nodeId,
                ViewRef: "panelette.meta.node",
                LocalOffset: new System.Numerics.Vector3(0f, 0.22f, 0.14f),
                Size: new System.Numerics.Vector2(1.25f, 0.72f),
                Anchor: "top",
                IsVisible: true,
                SurfaceKind: "panelette",
                PaneletteKind: "metadata",
                PaneletteTier: 1,
                CommandSurface: null);
        }

        private static AttachPanelPayload CreateLabelPanelettePayloadForNode(string nodeId)
        {
            return new AttachPanelPayload(
                Id: nodeId,
                ViewRef: "panelette.label.node",
                LocalOffset: new System.Numerics.Vector3(0f, -0.18f, 0.1f),
                Size: new System.Numerics.Vector2(0.92f, 0.28f),
                Anchor: "bottom",
                IsVisible: true,
                SurfaceKind: "panelette",
                PaneletteKind: "label",
                PaneletteTier: 1,
                CommandSurface: null);
        }

        private void AttachMetadataPaneletteForNode(string nodeId)
        {
            var payload = CreateMetadataPanelettePayloadForNode(nodeId);
            SendCommand(CommandNames.AttachPanel, payload);
        }

        private void AttachLabelPaneletteForNode(string nodeId)
        {
            var payload = CreateLabelPanelettePayloadForNode(nodeId);
            SendCommand(CommandNames.AttachPanel, payload);
        }

        private void ClearPaneletteForNode(string nodeId)
        {
            SendCommand(CommandNames.ClearPanelAttachment, new ClearPanelAttachmentPayload(nodeId));
        }

        private void AttachPanelettesForAllNodes(string paneletteKind)
        {
            var snapshot = GetRenderSceneSnapshot();

            foreach (var node in snapshot.Nodes)
            {
                AttachPanelPayload payload = paneletteKind switch
                {
                    "label" => CreateLabelPanelettePayloadForNode(node.Id),
                    _ => CreateMetadataPanelettePayloadForNode(node.Id)
                };
                SendCommand(CommandNames.AttachPanel, payload);
            }
        }
        

        private static AttachPanelPayload CreateDetailMetadataPanelettePayloadForNode(string nodeId)
        {
            return new AttachPanelPayload(
                Id: nodeId,
                ViewRef: "panelette.meta.detail.node",
                LocalOffset: new System.Numerics.Vector3(0f, 0.26f, 0.16f),
                Size: new System.Numerics.Vector2(1.35f, 0.82f),
                Anchor: "top",
                IsVisible: true,
                SurfaceKind: "panelette",
                PaneletteKind: "metadata",
                PaneletteTier: 2,
                CommandSurface: null);
        }

        private void ClearPanelettesForAllNodes()
        {
            var snapshot = GetRenderSceneSnapshot();
            foreach (var node in snapshot.Nodes)
            {
                SendCommand(CommandNames.ClearPanelAttachment, new ClearPanelAttachmentPayload(node.Id));
            }
        }

        private void AttachDetailMetadataPaneletteForNode(string nodeId)
        {
            var payload = CreateDetailMetadataPanelettePayloadForNode(nodeId);
            SendCommand(CommandNames.AttachPanel, payload);
        }

        private void AttachDetailMetadataPanelettesForAllNodes()
        {
            var snapshot = GetRenderSceneSnapshot();

            foreach (var node in snapshot.Nodes)
            {
                var payload = CreateDetailMetadataPanelettePayloadForNode(node.Id);
                SendCommand(CommandNames.AttachPanel, payload);
            }
        }

        private bool TryOpenBackgroundContextSurface(Point point)
        {
            var hasEnteredNode = EngineServices.ShellScene.GetEnteredNodeId() is not null;

            var commandDescriptors = BuildBackgroundContextCommandDescriptors(hasEnteredNode);

            if (commandDescriptors.Length == 0)
            {
                return false;
            }

            var metadata = CreateContextSurfaceMetadata("background.primary", "background", commandDescriptors);
            OpenBackgroundCommandSurface(point, BackgroundSurfaceKind.Background, metadata);
            return true;
        }

        private static void LayoutBackgroundCommandSurface(
            Point anchorPoint,
            PanelCommandSurfaceMetadata metadata,
            Rect bounds,
            out Rect overlayRect,
            out Rect[] commandRects)
        {
            const double overlayWidth = 264.0;
            const double headerHeight = 48.0;
            const double itemHeight = 26.0;
            const double overlayInset = 8.0;

            var overlayHeight = headerHeight + (metadata.Commands.Count * itemHeight) + overlayInset;

            var x = anchorPoint.X + 12.0;
            var y = anchorPoint.Y + 12.0;

            if (x + overlayWidth > bounds.Right - overlayInset)
            {
                x = bounds.Right - overlayWidth - overlayInset;
            }

            if (x < bounds.X + overlayInset)
            {
                x = bounds.X + overlayInset;
            }

            if (y + overlayHeight > bounds.Bottom - overlayInset)
            {
                y = bounds.Bottom - overlayHeight - overlayInset;
            }

            if (y < bounds.Y + overlayInset)
            {
                y = bounds.Y + overlayInset;
            }

            overlayRect = new Rect(x, y, overlayWidth, overlayHeight);

            var commandRectWidth = overlayWidth - (overlayInset * 2.0);
            commandRects = new Rect[metadata.Commands.Count];

            for (var index = 0; index < metadata.Commands.Count; index++)
            {
                commandRects[index] = new Rect(
                    overlayRect.X + overlayInset,
                    overlayRect.Y + headerHeight + (index * itemHeight),
                    commandRectWidth,
                    itemHeight - 4.0);
            }
        }

        private void ClearBackgroundCommandSurface(bool invalidate = true)
        {
            _hasBackgroundCommandSurface = false;
            _backgroundCommandSurfaceMetadata = null;
            _backgroundCommandOverlayRect = default;
            _backgroundCommandCommandRects = Array.Empty<Rect>();
            _backgroundCommandIndex = 0;
            _backgroundSurfaceKind = BackgroundSurfaceKind.None;
            _activeLinkSourceId = null;
            _activeLinkTargetId = null;
            _activeGroupId = null;

            if (invalidate)
            {
                InvalidateVisual();
            }
        }

        private bool TryMoveBackgroundCommandSurfaceSelection(int delta)
        {
            if (!_hasBackgroundCommandSurface ||
                _backgroundCommandSurfaceMetadata is not { HasCommands: true } metadata)
            {
                return false;
            }

            var count = metadata.Commands.Count;
            if (count <= 0)
            {
                _backgroundCommandIndex = 0;
                return false;
            }

            _backgroundCommandIndex = (_backgroundCommandIndex + delta + count) % count;
            InvalidateVisual();
            return true;
        }

        private bool TryHandleBackgroundCommandSurfaceLeftClick(Point point)
        {
            if (!_hasBackgroundCommandSurface ||
                _backgroundCommandSurfaceMetadata is not { HasCommands: true } metadata)
            {
                return false;
            }

            if (_backgroundCommandOverlayRect.Contains(point))
            {
                for (var i = 0; i < _backgroundCommandCommandRects.Length; i++)
                {
                    if (!_backgroundCommandCommandRects[i].Contains(point))
                    {
                        continue;
                    }

                    _backgroundCommandIndex = Math.Clamp(i, 0, metadata.Commands.Count - 1);
                    _ = TryInvokeBackgroundCommandSurfaceSelection();
                    return true;
                }

                return true;
            }

            ClearBackgroundCommandSurface();
            return false;
        }

        private bool TryUpdateBackgroundCommandSurfaceHover(Point point)
        {
            if (!_hasBackgroundCommandSurface ||
                _backgroundCommandSurfaceMetadata is not { HasCommands: true } metadata)
            {
                return false;
            }

            if (!_backgroundCommandOverlayRect.Contains(point))
            {
                return false;
            }

            for (var i = 0; i < _backgroundCommandCommandRects.Length; i++)
            {
                if (!_backgroundCommandCommandRects[i].Contains(point))
                {
                    continue;
                }

                var clamped = Math.Clamp(i, 0, metadata.Commands.Count - 1);
                if (clamped != _backgroundCommandIndex)
                {
                    _backgroundCommandIndex = clamped;
                    InvalidateVisual();
                }

                return true;
            }

            return true;
        }

        private bool TryInvokeBackgroundCommandSurfaceSelection()
        {
            if (!_hasBackgroundCommandSurface ||
                _backgroundCommandSurfaceMetadata is not { HasCommands: true } metadata)
            {
                return false;
            }

            if (metadata.Commands.Count == 0)
            {
                return false;
            }

            var selectedCommand = metadata.Commands[Math.Clamp(
                _backgroundCommandIndex,
                0,
                metadata.Commands.Count - 1)];

            var commandId = selectedCommand.CommandId;
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return false;
            }

            var handled = TryExecuteBackgroundCommandSurfaceCommand(commandId);
            if (handled)
            {
                ClearBackgroundCommandSurface();
            }

            return handled;
        }

        private bool TryExecuteBackgroundCommandSurfaceCommand(string commandId)
        {
            // Link-scoped context surface
            if (_backgroundSurfaceKind == BackgroundSurfaceKind.Link)
            {
                return TryExecuteLinkContextCommand(commandId);
            }

            // Group-scoped context surface
            if (_backgroundSurfaceKind == BackgroundSurfaceKind.Group)
            {
                return TryExecuteGroupContextCommand(commandId);
            }

            // Background/null-focus context surface
            switch (commandId)
            {
                case SurfaceCommand_CreateNodeAtPointer:
                    CreateNodeAtBackgroundAnchorPoint();
                    return true;
                case CommandNames.ClearFocus:
                      PublishFocusOrigin("mouse");
                      SendCommand<object?>(CommandNames.ClearFocus, null);
                    return true;
                case CommandNames.ClearSelection:
                    SendCommand<object?>(CommandNames.ClearSelection, null);
                    return true;
                case CommandNames.HomeView:
                    SendCommand<object?>(CommandNames.HomeView, null);
                    return true;
                case CommandNames.FrameSelection:
                    SendCommand(
                        CommandNames.FrameSelection,
                        new FrameSelectionPayload());
                    return true;
                case SurfaceCommand_ExitNodeContext:
                {
                    var enteredId = EngineServices.ShellScene.GetEnteredNodeId();
                    if (enteredId is null)
                    {
                        return false;
                    }

                    SendCommand(
                        CommandNames.ExitNode,
                        new ExitNodePayload(enteredId.Value.ToString()));
                    return true;
                }
                case SurfaceCommand_SaveBookmark:
                {
                    var bookmarks = EngineServices.ShellScene.GetBookmarks();
                    var index = bookmarks.Count + 1;
                    var name = $"Quick Bookmark {index}";
                    SendCommand(
                        CommandNames.BookmarkSave,
                        new BookmarkSavePayload(name));
                    return true;
                }
                case SurfaceCommand_RestoreBookmark:
                {
                    var bookmarks = EngineServices.ShellScene.GetBookmarks();
                    if (bookmarks.Count == 0)
                    {
                        return false;
                    }
                    var latest = bookmarks
                        .OrderBy(b => b.Name, StringComparer.Ordinal)
                        .LastOrDefault();
                    if (latest is null)
                    {
                        return false;
                    }
                    SendCommand(
                        CommandNames.BookmarkRestore,
                        new BookmarkRestorePayload(latest.Name));
                    return true;
                }
                case SurfaceCommand_SetModeNavigate:
                    SetInteractionModeFromSurface("navigate");
                    return true;
                case SurfaceCommand_SetModeMarquee:
                    SetInteractionModeFromSurface("marquee");
                    return true;
                case SurfaceCommand_SetModeMove:
                    SetInteractionModeFromSurface("move");
                    return true;
                case SurfaceCommand_AttachMetadataPanelettesForAllNodes:
                    AttachPanelettesForAllNodes("metadata");
                    return true;
                case SurfaceCommand_AttachLabelPanelettesForAllNodes:
                    AttachPanelettesForAllNodes("label");
                    return true;
                case SurfaceCommand_ClearPanelettesForAllNodes:
                    ClearPanelettesForAllNodes();
                    return true;
                default:
                    return false;
            }
        }

        private bool TryExecuteLinkContextCommand(string commandId)
        {
            if (_activeLinkSourceId is null || _activeLinkTargetId is null)
            {
                return false;
            }

            switch (commandId)
            {
                case SurfaceCommand_LinkSelectSource:
                     PublishFocusOrigin("mouse");
                     SendCommand(CommandNames.Focus, new FocusEntityPayload(_activeLinkSourceId));
                    SendCommand(CommandNames.Select, new SelectEntitiesPayload([_activeLinkSourceId], true));
                    return true;

                case SurfaceCommand_LinkSelectTarget:
                     PublishFocusOrigin("mouse");
                     SendCommand(CommandNames.Focus, new FocusEntityPayload(_activeLinkTargetId));
                    SendCommand(CommandNames.Select, new SelectEntitiesPayload([_activeLinkTargetId], true));
                    return true;

                case SurfaceCommand_LinkFrameEndpoints:
                    SendCommand(
                        CommandNames.FrameSelection,
                        new FrameSelectionPayload([_activeLinkSourceId, _activeLinkTargetId], 1.35f));
                    return true;

                case SurfaceCommand_NudgeNodeLeft:
                    return TryApplyLinkEndpointTransformFromContextSurface(new NVec3(-0.12f, 0f, 0f), 1f);

                case SurfaceCommand_NudgeNodeRight:
                    return TryApplyLinkEndpointTransformFromContextSurface(new NVec3(0.12f, 0f, 0f), 1f);

                case SurfaceCommand_NudgeNodeUp:
                    return TryApplyLinkEndpointTransformFromContextSurface(new NVec3(0f, 0.08f, 0f), 1f);

                case SurfaceCommand_NudgeNodeDown:
                    return TryApplyLinkEndpointTransformFromContextSurface(new NVec3(0f, -0.08f, 0f), 1f);

                case SurfaceCommand_NodeGrow:
                    return TryApplyLinkEndpointTransformFromContextSurface(NVec3.Zero, 1.15f);

                case SurfaceCommand_NodeShrink:
                    return TryApplyLinkEndpointTransformFromContextSurface(NVec3.Zero, 1f / 1.15f);

                case SurfaceCommand_LinkUnlink:
                    SendCommand(
                        CommandNames.Unlink,
                        new UnlinkEntitiesPayload(_activeLinkSourceId, _activeLinkTargetId, "directed"));
                    return true;

                default:
                    return false;
            }
        }

        private bool TryExecuteGroupContextCommand(string commandId)
        {
            if (string.IsNullOrWhiteSpace(_activeGroupId))
            {
                return false;
            }

            var snapshot = GetRenderSceneSnapshot();
            var group = snapshot.Groups
                .FirstOrDefault(g => string.Equals(g.Id, _activeGroupId, StringComparison.Ordinal));

            if (string.IsNullOrWhiteSpace(group.Id))
            {
                return false;
            }

            var memberIds = group.NodeIds ?? Array.Empty<string>();
            switch (commandId)
            {
                case SurfaceCommand_GroupSelectMembers:
                    if (memberIds.Length == 0)
                    {
                        return false;
                    }

                     PublishFocusOrigin("mouse");
                    SendCommand(CommandNames.Select, new SelectEntitiesPayload(memberIds, true));
                    SendCommand(CommandNames.Focus, new FocusEntityPayload(memberIds[0]));
                    return true;

                case SurfaceCommand_GroupFrame:
                    if (memberIds.Length == 0)
                    {
                        return false;
                    }

                    SendCommand(
                        CommandNames.FrameSelection,
                        new FrameSelectionPayload(memberIds, 1.35f));
                    return true;

                case SurfaceCommand_GroupAddSelection:
                    SendCommand(CommandNames.AddSelectionToGroup, new GroupMembershipPayload(_activeGroupId));
                    return true;

                case SurfaceCommand_GroupRemoveSelection:
                    SendCommand(CommandNames.RemoveSelectionFromGroup, new GroupMembershipPayload(_activeGroupId));
                    return true;

                case SurfaceCommand_GroupDelete:
                    SendCommand(CommandNames.DeleteGroup, new DeleteGroupPayload(_activeGroupId));
                    return true;

                default:
                    return false;
            }
        }

        private void DrawBackgroundCommandSurfaceOverlay(DrawingContext ctx, Rect bounds)
        {
            if (!_hasBackgroundCommandSurface ||
                _backgroundCommandSurfaceMetadata is not { HasCommands: true } metadata)
            {
                return;
            }

            var layout = new ActivePanelCommandSurfaceLayoutInfo(
                new PanelSurfaceNode(
                    "__background__",
                    "__background__",
                    new Vector3(0f, 0f, 0f),
                    new Vector2(0f, 0f),
                    "center",
                    IsVisible: true,
                    IsFocused: false,
                    IsSelected: false,
                    Semantics: null,
                    CommandSurface: metadata),
                metadata,
                PanelRect: default,
                OverlayRect: _backgroundCommandOverlayRect,
                CommandRects: _backgroundCommandCommandRects);

            ViewportPanelOverlayRenderer.DrawActiveCommandSurfaceOverlay(
                ctx,
                layout,
                _backgroundCommandIndex);
        }

        private bool TryGetActiveCommandSurfaceLayout(
            out PanelSurfaceNode panel,
            out PanelCommandSurfaceMetadata metadata,
            out Rect panelRect,
            out Rect overlayRect,
            out Rect[] commandRects)
        {
            panel = default;
            metadata = default!;
            panelRect = default;
            overlayRect = default;
            commandRects = Array.Empty<Rect>();

            if (!ViewportPanelOverlayRenderer.TryGetActiveCommandSurfaceLayout(
                    GetRenderSceneSnapshot(),
                    _activeCommandSurface,
                    new Rect(Bounds.Size),
                    ComputeView(),
                    ComputeProjection(),
                    out var layout))
            {
                return false;
            }

            panel = layout.Panel;
            metadata = layout.Metadata;
            panelRect = layout.PanelRect;
            overlayRect = layout.OverlayRect;
            commandRects = layout.CommandRects;
            return true;
        }

        private void DrawActiveCommandSurfaceOverlay(DrawingContext ctx, Rect bounds)
        {
            if (!ViewportPanelOverlayRenderer.TryGetActiveCommandSurfaceLayout(
                    GetRenderSceneSnapshot(),
                    _activeCommandSurface,
                    bounds,
                    ComputeView(),
                    ComputeProjection(),
                    out var layout))
            {
                return;
            }

            ViewportPanelOverlayRenderer.DrawActiveCommandSurfaceOverlay(
                ctx,
                layout,
                _activeCommandSurface.CommandIndex);
        }

        private static void ApplyNodeTransformFromContextSurface(
            string nodeId,
            NVec3 positionDelta,
            float scaleMultiplier)
        {
            var snapshot = EngineServices.ShellScene.GetSnapshot();
            var node = snapshot.Nodes
                .FirstOrDefault(n => string.Equals(n.Id.ToString(), nodeId, StringComparison.Ordinal));

            if (node is null)
            {
                return;
            }

            var pos = node.Transform.Position;
            var nextPos = new NVec3(
                pos.X + positionDelta.X,
                pos.Y + positionDelta.Y,
                pos.Z + positionDelta.Z);

            var currentScale = node.Transform.Scale;
            var nextScale = new NVec3(
                Math.Clamp(currentScale.X * scaleMultiplier, 0.15f, 2.5f),
                Math.Clamp(currentScale.Y * scaleMultiplier, 0.15f, 2.5f),
                Math.Clamp(currentScale.Z * scaleMultiplier, 0.15f, 2.5f));

            var nextVisualScale = Math.Clamp(node.VisualScale * scaleMultiplier, 0.15f, 2.5f);

            var update = new UpdateEntityPayload(
                node.Id.ToString(),
                node.Label,
                nextPos,
                node.Transform.RotationEuler,
                nextScale,
                nextVisualScale,
                node.Phase,
                null);

            SendCommand(
                CommandNames.UpdateEntities,
                new UpdateEntitiesPayload(new[] { update }));
        }

        private bool TryApplyLinkEndpointTransformFromContextSurface(NVec3 positionDelta, float scaleMultiplier)
        {
            if (string.IsNullOrWhiteSpace(_activeLinkSourceId) ||
                string.IsNullOrWhiteSpace(_activeLinkTargetId))
            {
                return false;
            }

            ApplyNodeTransformFromContextSurface(_activeLinkSourceId, positionDelta, scaleMultiplier);
            if (!string.Equals(_activeLinkSourceId, _activeLinkTargetId, StringComparison.Ordinal))
            {
                ApplyNodeTransformFromContextSurface(_activeLinkTargetId, positionDelta, scaleMultiplier);
            }
            return true;
        }

        private void CreateNodeAtBackgroundAnchorPoint()
        {
            var bounds = new Rect(Bounds.Size);
            var view = ComputeView();
            var proj = ComputeProjection();

            // Intersect click ray with a simple Z=0 plane as an initial create-at/near-pointer behavior.
            var planePoint = new Vector3(0f, 0f, 0f);
            var planeNormal = new Vector3(0f, 0f, 1f);

            if (!ViewportCameraMath.TryProjectScreenPointToPlane(
                    _backgroundCommandAnchorPoint,
                    bounds,
                    view,
                    proj,
                    planePoint,
                    planeNormal,
                    out var worldHit))
            {
                // Fallback: create at current camera target if ray-plane intersection fails.
                worldHit = _cam.Target;
            }

            var position = new System.Numerics.Vector3(worldHit.X, worldHit.Y, worldHit.Z);

            var payload = new CreateEntityPayload(
                Type: "node",
                Id: null,
                Label: null,
                Position: position,
                RotationEuler: null,
                Scale: null,
                VisualScale: null,
                Phase: null,
                Appearance: null);

            SendCommand(CommandNames.CreateEntity, payload);
        }

        private void SetInteractionModeFromSurface(string mode)
        {
            SendCommand(CommandNames.SetInteractionMode, new SetInteractionModePayload(mode));
        }

        private static double DistancePointToSegment(Point p, Point a, Point b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = p.X - a.X;
            var wy = p.Y - a.Y;

            var lenSq = (vx * vx) + (vy * vy);
            if (lenSq <= double.Epsilon)
            {
                var dx0 = wx;
                var dy0 = wy;
                return Math.Sqrt(dx0 * dx0 + dy0 * dy0);
            }

            var t = (wx * vx + wy * vy) / lenSq;
            t = Math.Clamp(t, 0.0, 1.0);
            var projX = a.X + t * vx;
            var projY = a.Y + t * vy;
            var dx = p.X - projX;
            var dy = p.Y - projY;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
