using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Constellate.Core.Interaction;
using Constellate.Core.Messaging;
using Constellate.Renderer.OpenTK.Scene;
using Constellate.SDK;
using OpenTK.Mathematics;
using NVec3 = System.Numerics.Vector3;

namespace Constellate.Renderer.OpenTK.Controls
{
    public partial class OpenTkViewportControl
    {
        private static void SendCommand<TPayload>(string commandName, TPayload payload)
        {
            var payloadElement = payload is null
                ? JsonSerializer.SerializeToElement(new { }, JsonOptions)
                : JsonSerializer.SerializeToElement(payload, JsonOptions);

            EngineServices.CommandBus.Send(new Envelope
            {
                V = "1.0",
                Id = Guid.NewGuid(),
                Ts = DateTimeOffset.UtcNow,
                Type = EnvelopeType.Command,
                Name = commandName,
                Payload = payloadElement,
                CorrelationId = null
            });
        }
        private (Vector3 right, Vector3 up, Vector3 forward) GetCameraBasis()
        {
            var cy = MathF.Cos(_cam.Yaw);
            var sy = MathF.Sin(_cam.Yaw);
            var cp = MathF.Cos(_cam.Pitch);
            var sp = MathF.Sin(_cam.Pitch);

            var forward = Vector3.Normalize(new Vector3(
                cy * cp,
                sp,
                sy * cp));

            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var up = Vector3.Normalize(Vector3.Cross(right, forward));

            return (right, up, forward);
        }

        private void ApplySelectionDelta(Vector3 delta)
        {
            if (delta.LengthSquared <= 1e-8f)
            {
                return;
            }

                 PublishFocusOrigin("mouse");
            var sceneSnapshot = EngineServices.ShellScene.GetSnapshot();

            var selectedNodeIds = sceneSnapshot.SelectedNodeIds?
                .Select(id => id.ToString())
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);

            string[] targetIds;

            if (selectedNodeIds.Count > 0)
            {
                targetIds = selectedNodeIds
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
            }
            else if (sceneSnapshot.FocusedNodeId is { } focusedId)
            {
                targetIds = new[] { focusedId.ToString() };
            }
            else
            {
                return;
            }

            var updates = sceneSnapshot.Nodes
                .Where(node => targetIds.Contains(node.Id.ToString(), StringComparer.Ordinal))
                .Select(node =>
                {
                    var pos = node.Transform.Position;
                    var nextPos = new NVec3(
                        pos.X + delta.X,
                        pos.Y + delta.Y,
                        pos.Z + delta.Z);

                    return new UpdateEntityPayload(
                        node.Id.ToString(),
                        node.Label,
                        nextPos,
                        node.Transform.RotationEuler,
                        node.Transform.Scale,
                        node.VisualScale,
                        node.Phase);
                })
                .ToArray();

            if (updates.Length == 0)
            {
                return;
            }

            SendCommand(CommandNames.UpdateEntities, new UpdateEntitiesPayload(updates));
        }

        private void NudgeSelectionRight(float sign)
        {
            var (right, _, _) = GetCameraBasis();
            if (right.LengthSquared <= 1e-8f)
            {
                return;
            }

            right = Vector3.Normalize(right) * sign;
            var baseStep = MathF.Max(0.01f, _cam.Distance * 0.03f);
            ApplySelectionDelta(right * baseStep);
        }

        private void NudgeSelectionUp(float sign)
        {
            var (_, up, _) = GetCameraBasis();
            if (up.LengthSquared <= 1e-8f)
            {
                return;
            }

            up = Vector3.Normalize(up) * sign;
            var baseStep = MathF.Max(0.01f, _cam.Distance * 0.03f);
            ApplySelectionDelta(up * baseStep);
        }

        private void NudgeSelectionDepth(float sign)
        {
            var (_, _, forward) = GetCameraBasis();
            if (forward.LengthSquared <= 1e-8f)
            {
                return;
            }

            forward = Vector3.Normalize(forward) * sign;
            var baseStep = MathF.Max(0.01f, _cam.Distance * 0.03f);
            ApplySelectionDelta(forward * baseStep);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // Track Space as depth-drag modifier in Navigate mode
            if (e.Key == Key.Space)
            {
                _spaceDepthDragModifier = true;
            }

            if (TryHandleKeybinding(e))
            {
                e.Handled = true;
            }
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                _spaceDepthDragModifier = false;
            }
        }

        private bool TryHandleKeybinding(KeyEventArgs e)
        {
            if (!TryGetGestureSpec(e, out var gesture))
            {
                return false;
            }

            var scopes = new List<string>();

            if (_hasBackgroundCommandSurface || _activeCommandSurface.HasValue)
            {
                scopes.Add("context_surface");
            }

            if (IsMoveModeActive())
            {
                scopes.Add("move_mode");
            }

            scopes.Add("viewport");
            scopes.Add("global");

            KeybindingEntry? match = null;

            foreach (var scope in scopes)
            {
                foreach (var entry in KeybindingModel.GetBindingsForScope(scope))
                {
                    if (!entry.Gesture.Equals(gesture))
                    {
                        continue;
                    }

                    match = entry;
                    break;
                }

                if (match is not null)
                {
                    break;
                }
            }

            if (match is null)
            {
                return false;
            }

            var action = match.Action;

            switch (action)
            {
                case InteractionAction.Escape:
                    if (_hasBackgroundCommandSurface)
                    {
                        ClearBackgroundCommandSurface();
                        return true;
                    }

                    if (_activeCommandSurface.HasValue)
                    {
                        ClearActiveCommandSurface();
                        return true;
                    }

                    if (_isMoveDragging)
                    {
                        CancelMoveDrag();
                        return true;
                    }

                    SendCommand<object?>(CommandNames.ClearSelection, null);
                    return true;

                case InteractionAction.CycleFocusNext:
                    CycleFocusedNode(forward: true);
                    return true;

                case InteractionAction.CycleFocusPrevious:
                    CycleFocusedNode(forward: false);
                    return true;

                case InteractionAction.SelectFocusedReplace:
                    SelectFocusedNode(additiveSelection: false);
                    return true;

                case InteractionAction.SelectFocusedAdd:
                    SelectFocusedNode(additiveSelection: true);
                    return true;

                case InteractionAction.Undo:
                    SendCommand<object?>(CommandNames.Undo, null);
                    return true;

                case InteractionAction.SetModeNavigate:
                    SetInteractionModeFromSurface("navigate");
                    return true;

                case InteractionAction.SetModeMarquee:
                    SetInteractionModeFromSurface("marquee");
                    return true;

                case InteractionAction.SetModeMove:
                    SetInteractionModeFromSurface("move");
                    return true;

                case InteractionAction.ContextSurfaceMoveSelection:
                {
                    var delta = string.Equals(gesture.Key, "Up", StringComparison.Ordinal)
                        ? -1
                        : 1;

                    if (_hasBackgroundCommandSurface &&
                        TryMoveBackgroundCommandSurfaceSelection(delta))
                    {
                        return true;
                    }

                    if (TryMoveActiveCommandSurfaceSelection(delta))
                    {
                        return true;
                    }

                    return false;
                }

                case InteractionAction.ContextSurfaceInvokeSelection:
                    if (_hasBackgroundCommandSurface &&
                        TryInvokeBackgroundCommandSurfaceSelection())
                    {
                        return true;
                    }

                    if (TryInvokeActiveCommandSurfaceSelection())
                    {
                        return true;
                    }

                    return false;

                case InteractionAction.OpenFocusedPaneletteContextSurface:
                    return TryOpenFocusedPanelCommandSurface();

                case InteractionAction.MoveSelectionLeft:
                    NudgeSelectionRight(-1f);
                    return true;

                case InteractionAction.MoveSelectionRight:
                    NudgeSelectionRight(1f);
                    return true;

                case InteractionAction.MoveSelectionUp:
                    NudgeSelectionUp(1f);
                    return true;

                case InteractionAction.MoveSelectionDown:
                    NudgeSelectionUp(-1f);
                    return true;

                case InteractionAction.MoveSelectionDepthForward:
                    NudgeSelectionDepth(1f);
                    return true;

                case InteractionAction.MoveSelectionDepthBackward:
                    NudgeSelectionDepth(-1f);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryGetGestureSpec(KeyEventArgs e, out KeyGestureSpec gesture)
        {
            gesture = default;

            string keyName;
            switch (e.Key)
            {
                case Key.Escape:
                    keyName = "Esc";
                    break;
                case Key.Tab:
                    keyName = "Tab";
                    break;
                case Key.Enter:
                    keyName = "Enter";
                    break;
                case Key.Space:
                    keyName = "Space";
                    break;
                case Key.Left:
                    keyName = "Left";
                    break;
                case Key.Right:
                    keyName = "Right";
                    break;
                case Key.Up:
                    keyName = "Up";
                    break;
                case Key.Down:
                    keyName = "Down";
                    break;
                case Key.PageUp:
                    keyName = "PageUp";
                    break;
                case Key.PageDown:
                    keyName = "PageDown";
                    break;
                case Key.F10:
                    keyName = "F10";
                    break;
                case Key.Z:
                    keyName = "Z";
                    break;
                case Key.N:
                    keyName = "N";
                    break;
                case Key.Q:
                    keyName = "Q";
                    break;
                case Key.M:
                    keyName = "M";
                    break;
                default:
                    return false;
            }

            var mods = e.KeyModifiers;
            var ctrl = mods.HasFlag(KeyModifiers.Control);
            var shift = mods.HasFlag(KeyModifiers.Shift);
            var alt = mods.HasFlag(KeyModifiers.Alt);

            gesture = new KeyGestureSpec(keyName, ctrl, shift, alt);
            return true;
        }

        private bool TryProjectWorldPoint(
            Vector3 worldPosition,
            Matrix4 view,
            Matrix4 proj,
            Rect bounds,
            out Point screenPoint)
        {
            return ViewportCameraMath.TryProjectWorldPoint(worldPosition, view, proj, bounds, out screenPoint);
        }

        private void DrawMarqueeOverlay(DrawingContext ctx)
        {
            var rect = GetNormalizedRect(_marqueeStartPt, _marqueeCurrentPt);
            if (rect.Width <= 0.5 || rect.Height <= 0.5)
            {
                return;
            }

            ctx.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(45, 125, 211, 252)),
                new Pen(new SolidColorBrush(Color.FromArgb(220, 125, 211, 252)), 1.5),
                rect);
        }

        private void BeginMoveDrag(string clickedNodeId, bool depthDrag)
        {
            var sceneSnapshot = EngineServices.ShellScene.GetSnapshot();
            var selectedNodeIds = sceneSnapshot.SelectedNodeIds?
                .Select(id => id.ToString())
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);

            var dragNodeIds = selectedNodeIds.Count > 0 && selectedNodeIds.Contains(clickedNodeId)
                ? selectedNodeIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
                : new[] { clickedNodeId };

            if (selectedNodeIds.Contains(clickedNodeId))
            {
                SendCommand(CommandNames.Focus, new FocusEntityPayload(clickedNodeId));
            }
            else
            {
                 PublishFocusOrigin("mouse");
                SendCommand(CommandNames.Focus, new FocusEntityPayload(clickedNodeId));
                SendCommand(CommandNames.Select, new SelectEntitiesPayload(new[] { clickedNodeId }, true));
                dragNodeIds = new[] { clickedNodeId };
            }

            _isDepthMoveDrag = depthDrag;
            _moveDragStartPositions.Clear();
            _moveDragPreviewPositions.Clear();

            foreach (var node in sceneSnapshot.Nodes.Where(node => dragNodeIds.Contains(node.Id.ToString(), StringComparer.Ordinal)))
            {
                var nodeId = node.Id.ToString();
                var position = new Vector3(
                    node.Transform.Position.X,
                    node.Transform.Position.Y,
                    node.Transform.Position.Z);

                _moveDragStartPositions[nodeId] = position;
                _moveDragPreviewPositions[nodeId] = position;
            }

            if (_moveDragStartPositions.Count == 0)
            {
                return;
            }

            _isMoveDragging = true;
            _pointerMovedBeyondThreshold = false;
            _pendingAdditiveSelection = false;
            _pressedNodeId = clickedNodeId;
            _moveDragStartPt = _lastPt;
        }

        private Vector3 ComputeMoveDragWorldDelta(Point currentPoint)
        {
            return ViewportCameraMath.ComputeMoveDragWorldDelta(
                _moveDragStartPt,
                currentPoint,
                _cam.Yaw,
                _cam.Pitch,
                _cam.Distance);
        }

        private void UpdateMoveDragPreview(Point currentPoint)
        {
            Vector3 delta;

            if (_isDepthMoveDrag)
            {
                var dy = currentPoint.Y - _moveDragStartPt.Y;
                var (_, _, forward) = GetCameraBasis();
                if (forward.LengthSquared <= 1e-8f)
                {
                    delta = Vector3.Zero;
                }
                else
                {
                    forward = Vector3.Normalize(forward);
                    var depthScale = _cam.Distance * 0.004f;
                    delta = forward * (float)(-dy) * depthScale;
                }
            }
            else
            {
                delta = ComputeMoveDragWorldDelta(currentPoint);
            }

            _moveDragPreviewPositions.Clear();

            foreach (var entry in _moveDragStartPositions)
            {
                _moveDragPreviewPositions[entry.Key] = entry.Value + delta;
            }
        }

        private void CompleteMoveDrag(Point releasePoint)
        {
            if (_pointerMovedBeyondThreshold)
            {
                UpdateMoveDragPreview(releasePoint);

                var latestSnapshot = EngineServices.ShellScene.GetSnapshot();
                var nodesById = latestSnapshot.Nodes.ToDictionary(node => node.Id.ToString(), StringComparer.Ordinal);

                var updates = _moveDragPreviewPositions
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Where(entry => nodesById.ContainsKey(entry.Key))
                    .Select(entry =>
                    {
                        var node = nodesById[entry.Key];
                        return new UpdateEntityPayload(
                            entry.Key,
                            node.Label,
                            new NVec3(entry.Value.X, entry.Value.Y, entry.Value.Z),
                            node.Transform.RotationEuler,
                            node.Transform.Scale,
                            node.VisualScale,
                            node.Phase);
                    })
                    .ToArray();

                if (updates.Length > 0)
                {
                    SendCommand(
                        CommandNames.UpdateEntities,
                        new UpdateEntitiesPayload(updates));
                }
            }

            ClearMoveDragState();
            InvalidateVisual();
        }

        private void ClearMoveDragState()
        {
            _isMoveDragging = false;
            _isDepthMoveDrag = false;
            _pointerMovedBeyondThreshold = false;
             _pressedNodeId = null;
             _pressedGroupId = null;
            _moveDragStartPositions.Clear();
            _moveDragPreviewPositions.Clear();
        }

        private void CancelMoveDrag()
        {
            ClearMoveDragState();
            _leftPointerPending = false;
            _pendingAdditiveSelection = false;
            _interactionMode = PointerInteractionMode.None;
            InvalidateVisual();
        }

        private void HandleMarqueeRelease(bool additiveSelection)
        {
            var selectionRect = GetNormalizedRect(_marqueeStartPt, _marqueeCurrentPt);
            if (IsPointLikeSelectionRect(selectionRect))
            {
                HandleNodeClick(HitTestProjectedNodeId(_marqueeCurrentPt), additiveSelection);
                return;
            }

            var nodeIds = HitTestProjectedNodeIds(selectionRect);
            if (nodeIds.Length == 0)
            {
                if (!additiveSelection)
                {
                    SendCommand<object?>(CommandNames.ClearFocus, null);
                    SendCommand<object?>(CommandNames.ClearSelection, null);
                }

                return;
            }

            SendCommand(CommandNames.Focus, new FocusEntityPayload(nodeIds[0]));
            SendCommand(CommandNames.Select, new SelectEntitiesPayload(nodeIds, !additiveSelection));
        }

        private string[] HitTestProjectedNodeIds(Rect selectionRect)
        {
            var renderSnapshot = GetRenderSceneSnapshot();
            if (renderSnapshot.Nodes.Length == 0)
            {
                return Array.Empty<string>();
            }

            return ProjectedNodeHitTesting.HitTestProjectedNodeIds(
                renderSnapshot,
                selectionRect,
                ComputeView(),
                ComputeProjection(),
                new Rect(Bounds.Size),
                (float)_sw.Elapsed.TotalSeconds);
        }

        private static Rect GetNormalizedRect(Point a, Point b) =>
            new(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X),
                Math.Abs(a.Y - b.Y));

        private static bool IsPointLikeSelectionRect(Rect selectionRect) =>
            selectionRect.Width < ClickDragThreshold &&
            selectionRect.Height < ClickDragThreshold;

        private static bool IsMarqueeModeActive() =>
            string.Equals(EngineServices.ShellScene.GetInteractionMode(), "marquee", StringComparison.OrdinalIgnoreCase);

        private static bool IsMoveModeActive() =>
            string.Equals(EngineServices.ShellScene.GetInteractionMode(), "move", StringComparison.OrdinalIgnoreCase);

        private void HandleNodeClick(string? nodeId, bool additiveSelection)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                if (!additiveSelection)
                {
                     PublishFocusOrigin("mouse");
                    SendCommand<object?>(CommandNames.ClearFocus, null);
                    SendCommand<object?>(CommandNames.ClearSelection, null);
                }

                return;
            }

            SendCommand(CommandNames.Focus, new FocusEntityPayload(nodeId));
            SendCommand(CommandNames.Select, new SelectEntitiesPayload(new[] { nodeId }, !additiveSelection));
        }

         private void HandleGroupClick(string groupId, bool additiveSelection)
         {
             if (string.IsNullOrWhiteSpace(groupId))
             {
                 return;
             }

             var snapshot = EngineServices.ShellScene.GetSnapshot();
             var groups = snapshot.Groups;
             if (groups is null || groups.Count == 0)
             {
                 return;
             }

             var group = groups.FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.Ordinal));
             if (group is null || group.NodeIds is null || group.NodeIds.Count == 0)
             {
                 return;
             }

             var memberIds = group.NodeIds
                 .Select(id => id.ToString())
                 .ToArray();

             if (memberIds.Length == 0)
             {
                 return;
             }

              PublishFocusOrigin("mouse");
             SendCommand(CommandNames.Select, new SelectEntitiesPayload(memberIds, !additiveSelection));
             SendCommand(CommandNames.Focus, new FocusEntityPayload(memberIds[0]));
         }

        private void HandlePanelClick(PanelSurfaceNode panel, bool additiveSelection)
        {
            if (string.IsNullOrWhiteSpace(panel.NodeId) || string.IsNullOrWhiteSpace(panel.ViewRef))
            {
                return;
            }

             PublishFocusOrigin("mouse");
            SendCommand(CommandNames.FocusPanel, new FocusPanelPayload(panel.NodeId, panel.ViewRef));
            SendCommand(CommandNames.SelectPanel, new SelectPanelPayload(panel.NodeId, panel.ViewRef, !additiveSelection));
        }

        private bool TryLinkInteractionNode(string targetNodeId)
        {
            var renderSnapshot = GetRenderSceneSnapshot();

            var sourceNodeId = renderSnapshot.Nodes
                .Where(node => node.IsSelected && !string.Equals(node.Id, targetNodeId, StringComparison.Ordinal))
                .OrderBy(node => node.Label, StringComparer.Ordinal)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .Select(node => node.Id)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(sourceNodeId))
            {
                sourceNodeId = renderSnapshot.Nodes
                    .Where(node => node.IsFocused && !string.Equals(node.Id, targetNodeId, StringComparison.Ordinal))
                    .Select(node => node.Id)
                    .FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(sourceNodeId) ||
                string.Equals(sourceNodeId, targetNodeId, StringComparison.Ordinal))
            {
                return false;
            }

             PublishFocusOrigin("mouse");
            SendCommand(CommandNames.Connect, new ConnectEntitiesPayload(sourceNodeId, targetNodeId, Kind: "directed", Weight: 1.0f));
            SendCommand(CommandNames.Focus, new FocusEntityPayload(targetNodeId));
            SendCommand(CommandNames.Select, new SelectEntitiesPayload(new[] { targetNodeId }, false));
            return true;
        }

        private void CycleFocusedNode(bool forward)
        {
            var nodes = GetOrderedRenderNodes();
            if (nodes.Length == 0)
            {
                return;
            }

            var focusedIndex = Array.FindIndex(nodes, node => node.IsFocused);
            var nextIndex = focusedIndex < 0
                ? (forward ? 0 : nodes.Length - 1)
                : (focusedIndex + (forward ? 1 : -1) + nodes.Length) % nodes.Length;

             var nextId = nodes[nextIndex].Id;
             if (string.IsNullOrWhiteSpace(nextId))
             {
                 return;
             }
             PublishFocusOrigin("keyboard");
             SendCommand(CommandNames.Focus, new FocusEntityPayload(nextId));
        }

        private void SelectFocusedNode(bool additiveSelection)
        {
            var nodes = GetOrderedRenderNodes();
            if (nodes.Length == 0)
            {
                return;
            }

            var focused = nodes.FirstOrDefault(node => node.IsFocused);
             var nodeId = string.IsNullOrWhiteSpace(focused.Id) ? nodes[0].Id : focused.Id;
             if (string.IsNullOrWhiteSpace(nodeId))
             {
                 return;
             }

             PublishFocusOrigin("keyboard");
             SendCommand(CommandNames.Focus, new FocusEntityPayload(nodeId));
             SendCommand(
                 CommandNames.Select,
                 new SelectEntitiesPayload(
                     new[] { nodeId },
                     !additiveSelection));
        }

        private string? HitTestProjectedNodeId(Point point)
        {
            var renderSnapshot = GetRenderSceneSnapshot();
            if (renderSnapshot.Nodes.Length == 0)
            {
                return null;
            }

            return ProjectedNodeHitTesting.HitTestProjectedNodeId(
                renderSnapshot,
                point,
                ComputeView(),
                ComputeProjection(),
                new Rect(Bounds.Size),
                (float)_sw.Elapsed.TotalSeconds);
        }

        private void UpdateHoverFocus(Point point)
        {
            // If a command-surface overlay is open, keep hover inside it from retargeting focus.
            if (TryGetActiveCommandSurfaceLayout(out _, out _, out var activePanelRect, out var activeOverlayRect, out _) &&
                (activePanelRect.Contains(point) || activeOverlayRect.Contains(point)))
            {
                return;
            }

            // Prefer panels/panelettes as focus targets
            if (TryHitTestPanelSurface(point, out var hoveredPanel, out _))
            {
                var sceneSnapshot = EngineServices.ShellScene.GetSnapshot();
                if (sceneSnapshot.FocusedPanel is { } focusedPanel &&
                    string.Equals(focusedPanel.NodeId.ToString(), hoveredPanel.NodeId, StringComparison.Ordinal) &&
                    string.Equals(focusedPanel.ViewRef, hoveredPanel.ViewRef, StringComparison.Ordinal))
                {
                    return;
                }

                SendCommand(CommandNames.FocusPanel, new FocusPanelPayload(hoveredPanel.NodeId, hoveredPanel.ViewRef));
                return;
            }

            // Otherwise fall back to node hover
            var hoveredNodeId = HitTestProjectedNodeId(point);
            if (string.IsNullOrWhiteSpace(hoveredNodeId))
            {
                if (!EngineServices.Settings.MouseLeaveClearsFocus)
                {
                    return;
                }

                var focusSnapshot = EngineServices.ShellScene.GetSnapshot();
                if (focusSnapshot.FocusedNodeId is null && focusSnapshot.FocusedPanel is null)
                {
                    return;
                }

                // Only auto-clear focus on blank hover when the last origin was mouse.
                // Keyboard-origin focus (for example from Tab traversal) persists until
                // an actual mouse-driven focus occurs on a real target.
                var origin = EngineServices.ShellScene.GetFocusOrigin();
                if (!string.Equals(origin, "mouse", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SendCommand<object?>(CommandNames.ClearFocus, null);
                return;
            }

            var snapshot = EngineServices.ShellScene.GetSnapshot();
            var focusedNodeId = snapshot.FocusedNodeId?.ToString();
            if (string.Equals(focusedNodeId, hoveredNodeId, StringComparison.Ordinal) &&
                snapshot.FocusedPanel is null)
            {
                return;
            }

             PublishFocusOrigin("mouse");
            SendCommand(CommandNames.Focus, new FocusEntityPayload(hoveredNodeId));
        }

        private static void TryGetFloat(JsonElement obj, string name, ref float value)
        {
            if (obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number)
            {
                if (p.TryGetSingle(out var f)) value = f;
                else if (p.TryGetDouble(out var d)) value = (float)d;
            }
        }

        private void PublishViewChanged()
        {
            try
            {
                var payload = new
                {
                    yaw = _cam.Yaw,
                    pitch = _cam.Pitch,
                    distance = _cam.Distance,
                    target = new { x = _cam.Target.X, y = _cam.Target.Y, z = _cam.Target.Z }
                };

                EngineServices.EventBus.Publish(new Envelope
                {
                    V = "1.0",
                    Id = Guid.NewGuid(),
                    Ts = DateTimeOffset.UtcNow,
                    Type = EnvelopeType.Event,
                    Name = EventNames.ViewChanged,
                    Payload = JsonSerializer.SerializeToElement(payload, JsonOptions),
                    CorrelationId = null
                });
            }
            catch
            {
            }
        }

        private static void PublishFocusOrigin(string origin)
        {
            try
            {
                var envelope = new Envelope
                {
                    V = "1.0",
                    Id = Guid.NewGuid(),
                    Ts = DateTimeOffset.UtcNow,
                    Type = EnvelopeType.Event,
                    Name = EventNames.FocusOriginChanged,
                    Payload = JsonSerializer.SerializeToElement(new { origin }, JsonOptions),
                    CorrelationId = null
                };

                EngineServices.EventBus.Publish(envelope);
            }
            catch
            {
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var pt = e.GetPosition(this);
            var props = e.GetCurrentPoint(this).Properties;
            var mods = e.KeyModifiers;

            // Right-click → context surfaces (panelette, node, background) or pan fallback
            if (props.IsRightButtonPressed)
            {
                Focus();

                if (TryOpenOrAdvancePanelCommandSurface(pt))
                {
                    e.Handled = true;
                    return;
                }

                if (TryOpenNodeContextSurface(pt))
                {
                    e.Handled = true;
                    return;
                }

                if (TryOpenBackgroundContextSurface(pt))
                {
                    e.Handled = true;
                    return;
                }

                _interactionMode = PointerInteractionMode.Pan;
                _lastPt = pt;
                try { e.Pointer.Capture(this); } catch { }

                e.Handled = true;
                return;
            }

            // Middle-drag → pan
            if (props.IsMiddleButtonPressed)
            {
                _interactionMode = PointerInteractionMode.Pan;
                _lastPt = pt;
                try { e.Pointer.Capture(this); } catch { }

                e.Handled = true;
                Focus();
                return;
            }

            // Left button behaviors
            if (props.IsLeftButtonPressed)
            {
                Focus();

                // First let background/active command surfaces consume the click if open
                if (TryHandleBackgroundCommandSurfaceLeftClick(pt))
                {
                    e.Handled = true;
                    return;
                }

                if (TryHandleActiveCommandSurfaceLeftClick(pt))
                {
                    e.Handled = true;
                    return;
                }

                // Ctrl+click or double-click → directed link creation
                var clickedNodeId = HitTestProjectedNodeId(pt);
                if (!string.IsNullOrWhiteSpace(clickedNodeId) &&
                    (mods.HasFlag(KeyModifiers.Control) || e.ClickCount >= 2) &&
                    TryLinkInteractionNode(clickedNodeId))
                {
                 PublishFocusOrigin("mouse");
                    e.Handled = true;
                    return;
                }

                // Panel click (when not in move/marquee)
                if (!IsMoveModeActive() &&
                    !IsMarqueeModeActive() &&
                    TryHitTestPanelSurface(pt, out var clickedPanel, out _))
                {
                    HandlePanelClick(clickedPanel, mods.HasFlag(KeyModifiers.Shift));
                    e.Handled = true;
                    return;
                }

                // Navigate mode: Space+Left-drag → depth move drag over clicked node
                if (!IsMoveModeActive() &&
                    !IsMarqueeModeActive() &&
                    _spaceDepthDragModifier)
                {
                    var moveTargetNodeId = HitTestProjectedNodeId(pt);
                    if (!string.IsNullOrWhiteSpace(moveTargetNodeId))
                    {
                        _leftPointerPending = false;
                        _pendingAdditiveSelection = false;
                        _lastPt = pt;
                        BeginMoveDrag(moveTargetNodeId, depthDrag: true);
                        try { e.Pointer.Capture(this); } catch { }
                        e.Handled = true;
                        return;
                    }
                }

                // Navigate mode: Alt+Left-drag → planar move drag over clicked node
                if (!IsMoveModeActive() &&
                    !IsMarqueeModeActive() &&
                    mods.HasFlag(KeyModifiers.Alt))
                {
                    var moveTargetNodeId = HitTestProjectedNodeId(pt);
                    if (!string.IsNullOrWhiteSpace(moveTargetNodeId))
                    {
                        _leftPointerPending = false;
                        _pendingAdditiveSelection = false;
                        _lastPt = pt;
                        BeginMoveDrag(moveTargetNodeId, depthDrag: false);
                        try { e.Pointer.Capture(this); } catch { }
                        e.Handled = true;
                        return;
                    }
                }

                // Move mode: left-drag over node → move drag
                if (IsMoveModeActive())
                {
                    var moveTargetNodeId = HitTestProjectedNodeId(pt);
                    if (string.IsNullOrWhiteSpace(moveTargetNodeId))
                    {
                        SendCommand<object?>(CommandNames.ClearFocus, null);
                        SendCommand<object?>(CommandNames.ClearSelection, null);
                        e.Handled = true;
                        return;
                    }

                    _leftPointerPending = false;
                    _pendingAdditiveSelection = false;
                    _lastPt = pt;
                    BeginMoveDrag(moveTargetNodeId, depthDrag: false);
                    try { e.Pointer.Capture(this); } catch { }
                    e.Handled = true;
                    return;
                }

                // Marquee mode: start marquee selection
                if (IsMarqueeModeActive())
                {
                    _leftPointerPending = false;
                    _pendingAdditiveSelection = mods.HasFlag(KeyModifiers.Shift);
                    _pointerMovedBeyondThreshold = false;
                    _pointerPressedPt = pt;
                    _marqueeStartPt = pt;
                    _marqueeCurrentPt = pt;
                    _pressedNodeId = null;
                    _interactionMode = PointerInteractionMode.None;
                    _isMarqueeSelecting = true;
                    try { e.Pointer.Capture(this); } catch { }
                    e.Handled = true;
                    return;
                }

                // Default navigate-mode click: potentially orbit/pan vs click-select
                _leftPointerPending = true;
                _pendingAdditiveSelection = mods.HasFlag(KeyModifiers.Shift);
                _pointerMovedBeyondThreshold = false;
                _pointerPressedPt = pt;
                _pressedNodeId = clickedNodeId;
                 _pressedGroupId = null;
                 if (string.IsNullOrWhiteSpace(_pressedNodeId) &&
                     TryHitTestGroupAtPoint(pt, out var groupId) &&
                     !string.IsNullOrWhiteSpace(groupId))
                 {
                     _pressedGroupId = groupId;
                 }
                _lastPt = pt;
                _interactionMode = PointerInteractionMode.None;
                try { e.Pointer.Capture(this); } catch { }

                e.Handled = true;
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            var pt = e.GetPosition(this);

            // Complete marquee selection
            if (_isMarqueeSelecting)
            {
                HandleMarqueeRelease(_pendingAdditiveSelection);
                _isMarqueeSelecting = false;
                _pendingAdditiveSelection = false;
                _pointerMovedBeyondThreshold = false;
                 _marqueeStartPt = default;
                _marqueeCurrentPt = default;
                _pressedNodeId = null;
                 _pressedGroupId = null;
                _interactionMode = PointerInteractionMode.None;

                try { e.Pointer.Capture(null); } catch { }

                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Complete move drag if active
            if (_isMoveDragging)
            {
                CompleteMoveDrag(pt);
                _interactionMode = PointerInteractionMode.None;
                 _pressedGroupId = null;

                try { e.Pointer.Capture(null); } catch { }

                _pendingAdditiveSelection = false;
                _leftPointerPending = false;
                _pressedNodeId = null;
                e.Handled = true;
                return;
            }

            // No left-pointer pending and no interaction mode → just release capture
            if (!_leftPointerPending && _interactionMode == PointerInteractionMode.None)
            {
                try { e.Pointer.Capture(null); } catch { }
                return;
            }

            // Simple click (no drag) → click selection
            if (_leftPointerPending && !_pointerMovedBeyondThreshold)
            {
                 if (!string.IsNullOrWhiteSpace(_pressedNodeId))
                 {
                     HandleNodeClick(_pressedNodeId, _pendingAdditiveSelection);
                 }
                 else if (!string.IsNullOrWhiteSpace(_pressedGroupId))
                 {
                     HandleGroupClick(_pressedGroupId, _pendingAdditiveSelection);
                 }
                 else
                 {
                     HandleNodeClick(null, _pendingAdditiveSelection);
                 }
            }

            _leftPointerPending = false;
            _pendingAdditiveSelection = false;
            _pointerMovedBeyondThreshold = false;
            _pressedNodeId = null;
             _pressedGroupId = null;
            _interactionMode = PointerInteractionMode.None;

            try { e.Pointer.Capture(null); } catch { }

            e.Handled = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var pt = e.GetPosition(this);
            var dx = pt.X - _lastPt.X;
            var dy = pt.Y - _lastPt.Y;
            var cameraChanged = false;

            // Active move drag
            if (_isMoveDragging)
            {
                var pressDx = pt.X - _moveDragStartPt.X;
                var pressDy = pt.Y - _moveDragStartPt.Y;
                var movementSquared = (pressDx * pressDx) + (pressDy * pressDy);
                if (movementSquared >= ClickDragThreshold * ClickDragThreshold)
                {
                    _pointerMovedBeyondThreshold = true;
                }

                UpdateMoveDragPreview(pt);
                _lastPt = pt;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Active marquee drag
            if (_isMarqueeSelecting)
            {
                _marqueeCurrentPt = pt;
                _lastPt = pt;
                _pointerMovedBeyondThreshold = true;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Detect threshold crossing for orbit/pan in navigate mode
            if (_leftPointerPending && !_pointerMovedBeyondThreshold)
            {
                var pressDx = pt.X - _pointerPressedPt.X;
                var pressDy = pt.Y - _pointerPressedPt.Y;
                var movementSquared = (pressDx * pressDx) + (pressDy * pressDy);
                if (movementSquared >= ClickDragThreshold * ClickDragThreshold)
                {
                    _pointerMovedBeyondThreshold = true;
                    _interactionMode = _pendingAdditiveSelection
                        ? PointerInteractionMode.Pan
                        : PointerInteractionMode.Orbit;
                }
            }

            // Pan
            if (_interactionMode == PointerInteractionMode.Pan)
            {
                var cy = MathF.Cos(_cam.Yaw);
                var sy = MathF.Sin(_cam.Yaw);
                var forward = new Vector3(
                    cy * MathF.Cos(_cam.Pitch),
                    MathF.Sin(_cam.Pitch),
                    sy * MathF.Cos(_cam.Pitch));

                forward = Vector3.Normalize(forward);
                var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
                var up = Vector3.Normalize(Vector3.Cross(right, forward));

                var panScale = _cam.Distance * 0.002f;
                _cam.Target -= right * (float)dx * panScale;
                _cam.Target += up * (float)dy * panScale;
                cameraChanged = true;
            }
            // Orbit
            else if (_interactionMode == PointerInteractionMode.Orbit)
            {
                _cam.Yaw += (float)(dx * 0.01);
                _cam.Pitch += (float)(-dy * 0.01);
                _cam.Clamp();
                cameraChanged = true;
            }

            _lastPt = pt;
            if (cameraChanged)
            {
                PublishViewChanged();
                e.Handled = true;
                return;
            }

            // Command-surface hover updates
            if (!_leftPointerPending &&
                _hasBackgroundCommandSurface &&
                TryUpdateBackgroundCommandSurfaceHover(pt))
            {
                e.Handled = true;
                return;
            }

            if (!_leftPointerPending && TryUpdateActiveCommandSurfaceHover(pt))
            {
                e.Handled = true;
                return;
            }

            // Ordinary hover → focus updates
            if (!_leftPointerPending)
            {
                UpdateHoverFocus(pt);
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var delta = e.Delta.Y;
            var factor = (float)Math.Pow(1.1, -delta);
            _cam.Distance *= factor;
            _cam.Clamp();
            PublishViewChanged();
            e.Handled = true;
        }
    }
}
