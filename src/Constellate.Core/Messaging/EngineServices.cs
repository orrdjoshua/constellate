using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Constellate.Core.Capabilities;
using Constellate.Core.Scene;
using Constellate.SDK;

namespace Constellate.Core.Messaging
{
    public static class EngineServices
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static ICommandBus CommandBus { get; private set; } = null!;
        public static IEventBus EventBus { get; private set; } = null!;
        public static EngineScene Scene { get; private set; } = null!;
        public static ShellSceneState ShellScene { get; private set; } = null!;
        public static ICapabilityRegistry Capabilities { get; private set; } = null!;

        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;

            var bus = new SimpleInProcBus();
            CommandBus = bus;
            EventBus = bus;
            Scene = new EngineScene();
            ShellScene = new ShellSceneState(Scene);
            Capabilities = new CapabilityRegistry();

            SeedDefaultScene(Scene);
            RegisterCommandHandlers(CommandBus, Scene);
            SeedCapabilities(Capabilities);

            _initialized = true;
        }

        private static void RegisterCommandHandlers(ICommandBus commandBus, EngineScene scene)
        {
            commandBus.Subscribe(CommandNames.CreateEntity, command =>
            {
                if (!TryDeserialize(command.Payload, out CreateEntityPayload? payload) || payload is null)
                {
                    return false;
                }

                var nodeId = ParseOrNewNodeId(payload.Id);
                var position = payload.Position ?? Vector3.Zero;
                var rotation = payload.RotationEuler ?? Vector3.Zero;
                var scale = payload.Scale ?? Vector3.One;
                var label = string.IsNullOrWhiteSpace(payload.Label) ? "Node" : payload.Label!;
                var visualScale = payload.VisualScale ?? MathF.Max(0.0001f, scale.X);
                var phase = payload.Phase ?? 0f;

                scene.Upsert(new SceneNode(
                    nodeId,
                    label,
                    new Transform(position, rotation, scale),
                    visualScale,
                    phase));

                PublishEvent(
                    EventNames.SceneChanged,
                    new { reason = "create_entity", nodeId = nodeId.ToString(), label });

                return true;
            });

            commandBus.Subscribe(CommandNames.UpdateEntity, command =>
            {
                if (!TryDeserialize(command.Payload, out UpdateEntityPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId))
                {
                    return false;
                }

                if (!scene.TryGet(nodeId, out var existing))
                {
                    return false;
                }

                var nextTransform = new Transform(
                    payload.Position ?? existing.Transform.Position,
                    payload.RotationEuler ?? existing.Transform.RotationEuler,
                    payload.Scale ?? existing.Transform.Scale);

                var nextNode = existing with
                {
                    Label = string.IsNullOrWhiteSpace(payload.Label) ? existing.Label : payload.Label!,
                    Transform = nextTransform,
                    VisualScale = payload.VisualScale ?? existing.VisualScale,
                    Phase = payload.Phase ?? existing.Phase
                };

                scene.Upsert(nextNode);

                PublishEvent(
                    EventNames.SceneChanged,
                    new { reason = "update_entity", nodeId = nodeId.ToString(), label = nextNode.Label });

                return true;
            });

            commandBus.Subscribe(CommandNames.Delete, command =>
            {
                if (!TryDeserialize(command.Payload, out DeleteEntityPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId) || !scene.Remove(nodeId))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new { reason = "delete_entity", nodeId = nodeId.ToString() });

                return true;
            });

            commandBus.Subscribe(CommandNames.SetTransform, command =>
            {
                if (!TryDeserialize(command.Payload, out SetTransformPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId))
                {
                    return false;
                }

                if (!scene.TryGet(nodeId, out var existing))
                {
                    return false;
                }

                var nextNode = existing with
                {
                    Transform = new Transform(
                        payload.Position ?? existing.Transform.Position,
                        payload.RotationEuler ?? existing.Transform.RotationEuler,
                        payload.Scale ?? existing.Transform.Scale)
                };

                scene.Upsert(nextNode);

                PublishEvent(
                    EventNames.SceneChanged,
                    new { reason = "set_transform", nodeId = nodeId.ToString() });

                return true;
            });

            commandBus.Subscribe(CommandNames.Connect, command =>
            {
                if (!TryDeserialize(command.Payload, out ConnectEntitiesPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.SourceId, out var sourceId) ||
                    !TryParseNodeId(payload.TargetId, out var targetId) ||
                    !scene.TryConnect(sourceId, targetId, payload.Kind, payload.Weight))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new
                    {
                        reason = "connect_entities",
                        sourceId = sourceId.ToString(),
                        targetId = targetId.ToString(),
                        kind = string.IsNullOrWhiteSpace(payload.Kind) ? "related" : payload.Kind,
                        weight = payload.Weight ?? 1.0f
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.Focus, command =>
            {
                if (!TryDeserialize(command.Payload, out FocusEntityPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId) || !scene.TryFocus(nodeId))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.FocusChanged,
                    new { focusedNodeId = nodeId.ToString() });

                return true;
            });

            commandBus.Subscribe(CommandNames.FocusPanel, command =>
            {
                if (!TryDeserialize(command.Payload, out FocusPanelPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId) || !scene.TryFocusPanel(nodeId, payload.ViewRef))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.PanelFocusChanged,
                    new { focusedNodeId = nodeId.ToString(), viewRef = payload.ViewRef });

                return true;
            });

            commandBus.Subscribe(CommandNames.Select, command =>
            {
                if (!TryDeserialize(command.Payload, out SelectEntitiesPayload? payload) || payload is null)
                {
                    return false;
                }

                var nodeIds = payload.Ids
                    .Select(id => TryParseNodeId(id, out var nodeId) ? nodeId : (NodeId?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToArray();

                var changed = false;

                if (payload.Replace)
                {
                    scene.SetSelection(nodeIds);
                    changed = true;
                }
                else
                {
                    foreach (var nodeId in nodeIds)
                    {
                        changed |= scene.Select(nodeId);
                    }
                }

                if (!changed)
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SelectionChanged,
                    new
                    {
                        selectedNodeIds = scene.GetSnapshot().SelectedNodeIds?
                            .Select(id => id.ToString())
                            .ToArray() ?? []
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.SelectPanel, command =>
            {
                if (!TryDeserialize(command.Payload, out SelectPanelPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId))
                {
                    return false;
                }

                var changed = false;

                if (payload.Replace)
                {
                    scene.SetPanelSelection([new PanelTarget(nodeId, payload.ViewRef)]);
                    changed = true;
                }
                else
                {
                    changed = scene.SelectPanel(nodeId, payload.ViewRef);
                }

                if (!changed)
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SelectionChanged,
                    new
                    {
                        selectedNodeIds = scene.GetSnapshot().SelectedNodeIds?
                            .Select(id => id.ToString())
                            .ToArray() ?? [],
                        selectedPanels = scene.GetSnapshot().SelectedPanels?
                            .Select(panel => new { nodeId = panel.NodeId.ToString(), viewRef = panel.ViewRef })
                            .ToArray() ?? []
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.ClearSelection, _ =>
            {
                scene.ClearSelection();

                PublishEvent(
                    EventNames.SelectionChanged,
                    new
                    {
                        selectedNodeIds = Array.Empty<string>(),
                        selectedPanels = Array.Empty<object>()
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.AttachPanel, command =>
            {
                if (!TryDeserialize(command.Payload, out AttachPanelPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId) ||
                    !scene.TryAttachPanel(
                        nodeId,
                        payload.ViewRef,
                        payload.LocalOffset,
                        payload.Size,
                        payload.Anchor,
                        payload.IsVisible))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.PanelAttachmentsChanged,
                    new
                    {
                        nodeId = nodeId.ToString(),
                        viewRef = payload.ViewRef,
                        anchor = payload.Anchor,
                        isVisible = payload.IsVisible
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.GroupSelection, command =>
            {
                if (!TryDeserialize(command.Payload, out GroupSelectionPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!scene.TryGroupSelection(payload.Label, out var group))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.GroupChanged,
                    new
                    {
                        reason = "group_selection",
                        groupId = group.Id,
                        label = group.Label,
                        nodeIds = group.NodeIds.Select(id => id.ToString()).ToArray()
                    });

                return true;
            });
        }

        private static void PublishEvent(string eventName, object payload)
        {
            if (EventBus is null)
            {
                return;
            }

            EventBus.Publish(new Envelope
            {
                V = "1.0",
                Id = Guid.NewGuid(),
                Ts = DateTimeOffset.UtcNow,
                Type = EnvelopeType.Event,
                Name = eventName,
                Payload = JsonSerializer.SerializeToElement(payload, JsonOptions),
                CorrelationId = null
            });
        }

        private static bool TryDeserialize<T>(JsonElement? payload, out T? value)
        {
            value = default;
            if (payload is null)
            {
                return false;
            }

            try
            {
                value = payload.Value.Deserialize<T>(JsonOptions);
                return value is not null;
            }
            catch
            {
                return false;
            }
        }

        private static NodeId ParseOrNewNodeId(string? rawId)
        {
            return TryParseNodeId(rawId, out var nodeId) ? nodeId : NodeId.New();
        }

        private static bool TryParseNodeId(string? rawId, out NodeId nodeId)
        {
            if (Guid.TryParse(rawId, out var guid))
            {
                nodeId = new NodeId(guid);
                return true;
            }

            nodeId = default;
            return false;
        }

        private static void SeedDefaultScene(EngineScene scene)
        {
            if (!scene.IsEmpty) return;

            var nodeA = new SceneNode(
                NodeId.New(),
                "Node A",
                new Transform(
                    new Vector3(-0.8f, -0.3f, 0.0f),
                    Vector3.Zero,
                    new Vector3(0.6f, 0.6f, 0.6f)),
                VisualScale: 0.6f,
                Phase: 0.0f);

            var nodeB = new SceneNode(
                NodeId.New(),
                "Node B",
                new Transform(
                    new Vector3(0.9f, 0.2f, 0.0f),
                    Vector3.Zero,
                    new Vector3(0.5f, 0.5f, 0.5f)),
                VisualScale: 0.5f,
                Phase: 1.2f);

            var nodeC = new SceneNode(
                NodeId.New(),
                "Node C",
                new Transform(
                    new Vector3(0.0f, 0.7f, 0.0f),
                    Vector3.Zero,
                    new Vector3(0.7f, 0.7f, 0.7f)),
                VisualScale: 0.7f,
                Phase: 2.35f);

            scene.Upsert(nodeA);
            scene.Upsert(nodeB);
            scene.Upsert(nodeC);
            scene.TryConnect(nodeA.Id, nodeB.Id, "baseline", 1.0f);
        }

        private static void SeedCapabilities(ICapabilityRegistry registry)
        {
            registry.Register(new EngineCapability(
                Key: "renderer.opengl.opentk",
                DisplayName: "OpenGL Renderer (OpenTK)",
                Category: "Renderer",
                Provider: "Constellate.Renderer.OpenTK",
                Version: "0.1"));

            registry.Register(new EngineCapability(
                Key: "panelhost.avalonia.mainwindow",
                DisplayName: "Avalonia Main Window Panel Host",
                Category: "PanelHost",
                Provider: "Constellate.App",
                Version: "0.1"));
        }

        private sealed class SimpleInProcBus : ICommandBus, IEventBus, IDisposable
        {
            private readonly object _gate = new();
            private readonly List<Subscription> _commandHandlers = new();
            private readonly List<Subscription> _eventHandlers = new();

            public event Action<Envelope>? Sent;
            public event Action<Envelope>? Published;

            public void Send(Envelope command)
            {
                Subscription[] snapshot;
                lock (_gate) snapshot = _commandHandlers.ToArray();

                var anySucceeded = false;

                foreach (var subscription in snapshot)
                {
                    if (!Matches(subscription.Name, command.Name))
                    {
                        continue;
                    }

                    try
                    {
                        anySucceeded |= subscription.Handler(command);
                    }
                    catch
                    {
                    }
                }

                Sent?.Invoke(command);

                if (anySucceeded)
                {
                    Publish(new Envelope
                    {
                        V = command.V,
                        Id = Guid.NewGuid(),
                        Ts = DateTimeOffset.UtcNow,
                        Type = EnvelopeType.Event,
                        Name = EventNames.CommandInvoked,
                        Payload = JsonSerializer.SerializeToElement(
                            new { commandName = command.Name, commandId = command.Id },
                            JsonOptions),
                        CorrelationId = command.Id
                    });
                }
            }

            public void Publish(Envelope evt)
            {
                Subscription[] snapshot;
                lock (_gate) snapshot = _eventHandlers.ToArray();

                foreach (var subscription in snapshot)
                {
                    if (!Matches(subscription.Name, evt.Name))
                    {
                        continue;
                    }

                    try
                    {
                        subscription.Handler(evt);
                    }
                    catch
                    {
                    }
                }

                Published?.Invoke(evt);
            }

            IDisposable ICommandBus.Subscribe(string name, Func<Envelope, bool> handler)
            {
                var subscription = new Subscription(name, handler);
                lock (_gate) _commandHandlers.Add(subscription);
                return new Unsubscriber(() =>
                {
                    lock (_gate) _commandHandlers.Remove(subscription);
                });
            }

            IDisposable IEventBus.Subscribe(string name, Func<Envelope, bool> handler)
            {
                var subscription = new Subscription(name, handler);
                lock (_gate) _eventHandlers.Add(subscription);
                return new Unsubscriber(() =>
                {
                    lock (_gate) _eventHandlers.Remove(subscription);
                });
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    _commandHandlers.Clear();
                    _eventHandlers.Clear();
                }
            }

            private static bool Matches(string subscriptionName, string envelopeName) =>
                string.Equals(subscriptionName, envelopeName, StringComparison.Ordinal);

            private sealed record Subscription(string Name, Func<Envelope, bool> Handler);

            private sealed class Unsubscriber : IDisposable
            {
                private readonly Action _dispose;
                private bool _done;

                public Unsubscriber(Action dispose) => _dispose = dispose;

                public void Dispose()
                {
                    if (_done) return;
                    _done = true;
                    _dispose();
                }
            }
        }
    }
}
