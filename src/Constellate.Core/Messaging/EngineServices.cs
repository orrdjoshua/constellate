using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Constellate.Core.Capabilities;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Resources;
using Constellate.Core.Scene;
using Constellate.Core.Storage;
using Constellate.SDK;

namespace Constellate.Core.Messaging
{
    public static partial class EngineServices
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            IncludeFields = true
        };

        private const string DefaultResourceSummarySurfaceRole = "resource.summary";
        private const string DefaultResourceSummaryViewRefPrefix = "resource.summary";

        public static ICommandBus CommandBus { get; private set; } = null!;
        public static IEventBus EventBus { get; private set; } = null!;
        public static EngineScene Scene { get; private set; } = null!;
        public static ShellSceneState ShellScene { get; private set; } = null!;
        public static ICapabilityRegistry Capabilities { get; private set; } = null!;
        public static IPaneCatalog PaneCatalog { get; private set; } = null!;
        public static EngineSettings Settings { get; private set; } = null!;
        public static IEnginePersistenceScope? PersistenceScope { get; private set; }
        public static PersistenceBootstrapResult? PersistenceBootstrapResult => PersistenceScope?.BootstrapResult;

        public static IReadOnlyList<PaneCapabilityDescriptor> ListPaneCapabilities()
        {
            EnsureInitialized();
            return PaneCatalog.GetCapabilityDescriptors();
        }

        public static IReadOnlyList<PaneDefinitionDescriptor> ListPaneDefinitions()
        {
            EnsureInitialized();
            return BuildPaneDefinitionSnapshot();
        }

        public static IReadOnlyList<PaneWorkspaceDescriptor> ListPaneWorkspaceDefinitions()
        {
            EnsureInitialized();
            return BuildPaneWorkspaceSnapshot();
        }

        public static bool TryGetPaneDefinition(string paneDefinitionId, out PaneDefinitionDescriptor paneDefinition)
        {
            EnsureInitialized();
            paneDefinition = BuildPaneDefinitionSnapshot()
                .FirstOrDefault(definition =>
                    string.Equals(definition.PaneDefinitionId, paneDefinitionId, StringComparison.Ordinal))!;
            return paneDefinition is not null;
        }

        public static bool TryGetPaneWorkspaceDefinition(string workspaceId, out PaneWorkspaceDescriptor workspaceDefinition)
        {
            EnsureInitialized();
            workspaceDefinition = BuildPaneWorkspaceSnapshot()
                .FirstOrDefault(workspace =>
                    string.Equals(workspace.WorkspaceId, workspaceId, StringComparison.Ordinal))!;
            return workspaceDefinition is not null;
        }

        public static void SavePaneDefinition(PaneDefinitionDescriptor paneDefinition)
        {
            ArgumentNullException.ThrowIfNull(paneDefinition);

            EnsureInitialized();

            var paneDefinitionStore = PersistenceScope?.PaneDefinitionStore
                ?? throw new InvalidOperationException("Pane definition store is not available.");

            paneDefinitionStore.Upsert(paneDefinition);
        }

        private static IReadOnlyList<PaneDefinitionDescriptor> BuildPaneDefinitionSnapshot()
        {
            var definitionsById = new Dictionary<string, PaneDefinitionDescriptor>(StringComparer.Ordinal);

            foreach (var definition in PaneCatalog.GetPaneDefinitions())
            {
                definitionsById[definition.PaneDefinitionId] = definition;
            }

            if (PersistenceScope?.PaneDefinitionStore is not null)
            {
                foreach (var definition in PersistenceScope.PaneDefinitionStore.ListAll())
                {
                    definitionsById[definition.PaneDefinitionId] = definition;
                }
            }

            return definitionsById.Values
                .OrderBy(definition => definition.DisplayLabel, StringComparer.Ordinal)
                .ThenBy(definition => definition.PaneDefinitionId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<PaneWorkspaceDescriptor> BuildPaneWorkspaceSnapshot()
        {
            var workspacesById = new Dictionary<string, PaneWorkspaceDescriptor>(StringComparer.Ordinal);

            foreach (var workspace in PaneCatalog.GetWorkspaceDefinitions())
            {
                workspacesById[workspace.WorkspaceId] = workspace;
            }

            if (PersistenceScope?.PaneWorkspaceStore is not null)
            {
                foreach (var workspace in PersistenceScope.PaneWorkspaceStore.ListAll())
                {
                    workspacesById[workspace.WorkspaceId] = workspace;
                }
            }

            return workspacesById.Values
                .OrderBy(workspace => workspace.DisplayLabel, StringComparer.Ordinal)
                .ThenBy(workspace => workspace.WorkspaceId, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool _initialized;

        public static void ConfigurePersistence(IEnginePersistenceScope persistenceScope)
        {
            ArgumentNullException.ThrowIfNull(persistenceScope);

            if (_initialized)
            {
                return;
            }

            PersistenceScope = persistenceScope;
        }

        public static void EnsureInitialized()
        {
            if (_initialized) return;

            var bus = new SimpleInProcBus();
            CommandBus = bus;
            EventBus = bus;
            Scene = new EngineScene();
            ShellScene = new ShellSceneState(Scene);
            var paneCatalog = new SeededPaneCatalog();
            PaneCatalog = paneCatalog;
            Capabilities = new CapabilityRegistry(paneCatalog);
            Settings = new EngineSettings();

            InitializePersistence();

            if (!TryRestorePersistedScene(Scene))
            {
                SeedDefaultScene(Scene);
                PersistCurrentSceneSnapshot();
            }

            RegisterCommandHandlers(CommandBus, Scene);
            RegisterEventHandlers(EventBus, Scene);
            SeedCapabilities(Capabilities);

            _initialized = true;
        }

        public static void PersistCurrentSceneSnapshot()
        {
            if (PersistenceScope?.EngineStateStore is null || Scene is null)
            {
                return;
            }

            PersistenceScope.EngineStateStore.SaveSnapshot(Scene.GetSnapshot());
        }

        public static MarkdownRecordResourceDescriptor CreateMarkdownRecordResource(string title, string? displayLabel = null)
        {
            EnsureInitialized();

            var resourceRegistryStore = PersistenceScope?.ResourceRegistryStore
                ?? throw new InvalidOperationException("Resource registry store is not available.");
            var nativeRecordStore = PersistenceScope?.NativeRecordStore
                ?? throw new InvalidOperationException("Native record store is not available.");

            var descriptor = MarkdownRecordResourceDescriptor.Create(title, displayLabel);
            var timestamp = DateTimeOffset.UtcNow;
            var registration = descriptor.CreateRegistration(timestamp);
            var initialState = descriptor.CreateInitialState(timestamp: timestamp);
            var initialRevision = descriptor.CreateInitialRevision(timestamp: timestamp);

            resourceRegistryStore.Register(registration);
            nativeRecordStore.Create(initialState, initialRevision);
            PublishEvent(
                EventNames.SceneChanged,
                new
                {
                    reason = "create_markdown_record_resource",
                    resourceId = descriptor.ResourceId.ToString(),
                    resourceTypeId = descriptor.TypeId,
                    resourceDisplayLabel = descriptor.DisplayLabel,
                    resourceTitle = descriptor.Title,
                    viewRef = descriptor.DetailViewRef,
                    surfaceRole = "detail",
                    worldAssignmentState = "unassigned"
                });
            PublishResourceSurfaceBindingChanged(
                descriptor.ResourceId,
                descriptor.TypeId,
                descriptor.DisplayLabel,
                descriptor.Title,
                MarkdownRecordResourceDescriptor.DefaultDetailSurfaceRole,
                descriptor.DetailViewRef,
                ResourceSurfaceBindingPayload.ProjectionModeDetail,
                ResourceSurfaceBindingPayload.TargetSurfaceKindChildPaneBody,
                bindingState: "active",
                targetKind: "surface",
                worldAssignmentState: "unassigned");
            return descriptor;
        }

        public static bool TryGetRegisteredResource(ResourceId resourceId, out ResourceRegistration registration)
        {
            if (PersistenceScope?.ResourceRegistryStore is null)
            {
                registration = null!;
                return false;
            }

            return PersistenceScope.ResourceRegistryStore.TryGet(resourceId, out registration);
        }

        public static IReadOnlyList<ResourceInspectionEntry> ListResourceInspectionEntries(string? typeId = null)
        {
            EnsureInitialized();

            if (PersistenceScope?.ResourceRegistryStore is null)
            {
                return Array.Empty<ResourceInspectionEntry>();
            }

            var snapshot = Scene.GetSnapshot();
            var assignedNodeIdsByResource = new Dictionary<ResourceId, List<string>>();

            foreach (var node in snapshot.Nodes)
            {
                if (node.ResourceId is not { } resourceId)
                {
                    continue;
                }

                if (!assignedNodeIdsByResource.TryGetValue(resourceId, out var assignedNodeIds))
                {
                    assignedNodeIds = new List<string>();
                    assignedNodeIdsByResource[resourceId] = assignedNodeIds;
                }

                assignedNodeIds.Add(node.Id.ToString());
            }

            var normalizedTypeId = string.IsNullOrWhiteSpace(typeId)
                ? null
                : typeId.Trim();

            return PersistenceScope.ResourceRegistryStore
                .ListAll()
                .Where(registration => normalizedTypeId is null ||
                                       string.Equals(registration.TypeId, normalizedTypeId, StringComparison.Ordinal))
                .Select(registration =>
                {
                    var assignedNodeIds = assignedNodeIdsByResource.TryGetValue(registration.ResourceId, out var nodeIds)
                        ? (IReadOnlyList<string>)nodeIds
                            .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
                            .ToArray()
                        : Array.Empty<string>();

                    return new ResourceInspectionEntry(
                        registration,
                        ResolveNativeRecordResourceTitle(registration.ResourceId),
                        assignedNodeIds.Count > 0,
                        assignedNodeIds);
                })
                .OrderBy(entry => entry.Registration.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Registration.ResourceId.ToString(), StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<ResourceInspectionEntry> ListUnassignedResourceInspectionEntries(string? typeId = null)
        {
            return ListResourceInspectionEntries(typeId)
                .Where(entry => entry.IsUnassigned)
                .ToArray();
        }

        public static IReadOnlyList<ResourceInspectionEntry> ListMarkdownRecordInspectionEntries()
        {
            return ListResourceInspectionEntries(MarkdownRecordResourceDescriptor.DefaultTypeId);
        }

        public static IReadOnlyList<ResourceInspectionEntry> ListUnassignedMarkdownRecordInspectionEntries()
        {
            return ListUnassignedResourceInspectionEntries(MarkdownRecordResourceDescriptor.DefaultTypeId);
        }
        
        private static string? ResolveNativeRecordResourceTitle(string? resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId) ||
                !ResourceId.TryParse(resourceId.Trim(), out var parsedResourceId))
            {
                return null;
            }

            return ResolveNativeRecordResourceTitle(parsedResourceId);
        }

        private static string? ResolveNativeRecordResourceTitle(ResourceId resourceId)
        {
            var nativeRecordStore = PersistenceScope?.NativeRecordStore;
            if (nativeRecordStore is null ||
                !nativeRecordStore.TryGet(resourceId, out var record))
            {
                return null;
            }

            return record.ResourceTitle;
        }

        public static SceneNode AssignResourceToNewNode(
            ResourceId resourceId,
            Vector3? position = null,
            string? displayLabel = null,
            NodeAppearance? appearance = null)
        {
            EnsureInitialized();

            if (!TryGetRegisteredResource(resourceId, out var registration))
            {
                throw new InvalidOperationException($"Resource '{resourceId}' is not registered.");
            }

            var assignedNode = CreateAssignedResourceNode(
                resourceId,
                registration,
                position,
                displayLabel,
                appearance);

            Scene.Upsert(assignedNode);
            PersistCurrentSceneSnapshot();

            PublishEvent(
                EventNames.SceneChanged,
                new
                {
                    reason = "assign_resource_to_new_node",
                    resourceId = resourceId.ToString(),
                    resourceTypeId = registration.TypeId,
                    resourceDisplayLabel = registration.DisplayLabel,
                    nodeId = assignedNode.Id.ToString(),
                    label = assignedNode.Label,
                    worldAssignmentState = "assigned",
                    bindingKind = "node"
                });
            PublishResourceSurfaceBindingChanged(
                resourceId,
                registration.TypeId,
                registration.DisplayLabel,
                registration.DisplayLabel,
                DefaultResourceSummarySurfaceRole,
                BuildResourceSummaryViewRef(resourceId),
                ResourceSurfaceBindingPayload.ProjectionModeSummary,
                ResourceSurfaceBindingPayload.TargetSurfaceKindWorldNode,
                bindingState: "active",
                targetId: assignedNode.Id.ToString(),
                targetKind: "node",
                worldAssignmentState: "assigned");

            return assignedNode;
        }

        public static bool TryAssignResourceToExistingNode(
            ResourceId resourceId,
            NodeId nodeId,
            out SceneNode assignedNode,
            bool allowReplaceExistingResource = false)
        {
            EnsureInitialized();
            assignedNode = default!;

            if (!TryGetRegisteredResource(resourceId, out var registration) ||
                !Scene.TryGet(nodeId, out var existingNode))
            {
                return false;
            }

            if (existingNode.ResourceId is { } existingResourceId)
            {
                if (existingResourceId == resourceId)
                {
                    return false;
                }

                if (!allowReplaceExistingResource)
                {
                    return false;
                }
            }

            assignedNode = existingNode with { ResourceId = resourceId };
            Scene.Upsert(assignedNode);
            PersistCurrentSceneSnapshot();

            PublishEvent(
                EventNames.SceneChanged,
                new
                {
                    reason = "assign_resource_to_existing_node",
                    resourceId = resourceId.ToString(),
                    resourceTypeId = registration.TypeId,
                    resourceDisplayLabel = registration.DisplayLabel,
                    nodeId = assignedNode.Id.ToString(),
                    label = assignedNode.Label,
                    worldAssignmentState = "assigned",
                    bindingKind = "node"
                });
            PublishResourceSurfaceBindingChanged(
                resourceId,
                registration.TypeId,
                registration.DisplayLabel,
                registration.DisplayLabel,
                DefaultResourceSummarySurfaceRole,
                BuildResourceSummaryViewRef(resourceId),
                ResourceSurfaceBindingPayload.ProjectionModeSummary,
                ResourceSurfaceBindingPayload.TargetSurfaceKindWorldNode,
                bindingState: "active",
                targetId: assignedNode.Id.ToString(),
                targetKind: "node",
                worldAssignmentState: "assigned");

            return true;
        }

        public static bool TryUnassignResourceFromNode(NodeId nodeId, out SceneNode updatedNode)
        {
            EnsureInitialized();
            updatedNode = default!;

            if (!Scene.TryGet(nodeId, out var existingNode) ||
                existingNode.ResourceId is not { } resourceId)
            {
                return false;
            }

            string? resourceDisplayLabel = null;
            string? resourceTypeId = null;

            if (TryGetRegisteredResource(resourceId, out var registration))
            {
                resourceDisplayLabel = registration.DisplayLabel;
                resourceTypeId = registration.TypeId;
            }

            updatedNode = existingNode with { ResourceId = null };
            Scene.Upsert(updatedNode);
            PersistCurrentSceneSnapshot();

            PublishEvent(
                EventNames.SceneChanged,
                new
                {
                    reason = "unassign_resource_from_node",
                    resourceId = resourceId.ToString(),
                    resourceTypeId,
                    resourceDisplayLabel,
                    nodeId = updatedNode.Id.ToString(),
                    label = updatedNode.Label,
                    worldAssignmentState = "unassigned",
                    bindingKind = "node"
                });
            PublishResourceSurfaceBindingChanged(
                resourceId,
                resourceTypeId,
                resourceDisplayLabel,
                resourceDisplayLabel,
                DefaultResourceSummarySurfaceRole,
                BuildResourceSummaryViewRef(resourceId),
                ResourceSurfaceBindingPayload.ProjectionModeSummary,
                ResourceSurfaceBindingPayload.TargetSurfaceKindWorldNode,
                bindingState: "inactive",
                targetId: updatedNode.Id.ToString(),
                targetKind: "node",
                worldAssignmentState: "unassigned");

            return true;
        }

        private static SceneNode CreateAssignedResourceNode(
            ResourceId resourceId,
            ResourceRegistration registration,
            Vector3? position,
            string? displayLabel,
            NodeAppearance? appearance)
        {
            var snapshot = Scene.GetSnapshot();
            var anchorIndex = snapshot.Nodes.Count;
            var anchorPosition = position ?? new Vector3(
                ((anchorIndex % 4) - 1.5f) * 0.9f,
                ((anchorIndex / 4) * 0.8f) - 0.35f,
                0.0f);
            var resolvedLabel = string.IsNullOrWhiteSpace(displayLabel)
                ? registration.DisplayLabel
                : displayLabel.Trim();

            return new SceneNode(
                NodeId.New(),
                resolvedLabel,
                new Transform(
                    anchorPosition,
                    Vector3.Zero,
                    new Vector3(0.62f, 0.62f, 0.62f)),
                VisualScale: 0.62f,
                Phase: (anchorIndex % 12) * 0.2f,
                Appearance: appearance ?? new NodeAppearance("triangle", "#F2B366", "#FFF2DE", 1.0f),
                ResourceId: resourceId);
        }

        private static string BuildResourceSummaryViewRef(ResourceId resourceId)
        {
            return $"{DefaultResourceSummaryViewRefPrefix}:{resourceId}";
        }

        private static void PublishResourceSurfaceBindingChanged(
            ResourceId resourceId,
            string? resourceTypeId,
            string? resourceDisplayLabel,
            string? resourceTitle,
            string? surfaceRole,
            string? viewRef,
            string? projectionMode,
            string? targetSurfaceKind,
            string bindingState,
            string? targetId = null,
            string? targetKind = null,
            string? worldAssignmentState = null)
        {
            var binding = ResourceSurfaceBindingPayload.Create(
                surfaceRole,
                viewRef,
                projectionMode,
                targetSurfaceKind);
            if (binding is null)
            {
                return;
            }

            PersistResourceSurfaceBinding(
                resourceId,
                resourceTypeId,
                binding,
                bindingState,
                targetKind,
                targetId);

            PublishEvent(
                EventNames.ResourceSurfaceBindingChanged,
                new ResourceSurfaceBindingChangedPayload(
                    resourceId.ToString(),
                    resourceTypeId,
                    resourceDisplayLabel,
                    resourceTitle,
                    binding,
                    bindingState,
                    targetId,
                    worldAssignmentState));
        }

        private static void InitializePersistence()
        {
            PersistenceScope?.EnsureInitialized();
        }

        private static bool TryRestorePersistedScene(EngineScene scene)
        {
            if (PersistenceScope?.EngineStateStore is null ||
                !PersistenceScope.EngineStateStore.HasPersistedSnapshot())
            {
                return false;
            }

            var snapshot = PersistenceScope.EngineStateStore.LoadSnapshot();
            if (snapshot is null)
            {
                return false;
            }

            scene.LoadSnapshot(snapshot);
            return true;
        }

        private static void RegisterEventHandlers(IEventBus eventBus, EngineScene scene)
        {
            // Renderer publishes current camera on interaction; store for next BookmarkSave
            eventBus.Subscribe(EventNames.ViewChanged, envelope =>
            {
                if (!TryDeserialize(envelope.Payload, out ViewChangedPayload? p) || p is null)
                {
                    return false;
                }

                var view = new ViewParams(p.Yaw, p.Pitch, p.Distance, p.Target);
                scene.SetLastView(view);

                // ShellScene tracks a short view-history tail for navigation observability.
                ShellScene?.AppendViewHistory(view);
                return true;
            });

            // Viewport/shell publish focus-origin hints; keep last origin in ShellSceneState for observability.
            eventBus.Subscribe(EventNames.FocusOriginChanged, envelope =>
            {
                if (ShellScene is null || envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!payload.TryGetProperty("origin", out var originElement) ||
                    originElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                var origin = originElement.GetString();
                if (string.IsNullOrWhiteSpace(origin))
                {
                    return false;
                }

                ShellScene.SetFocusOrigin(origin);
                return true;
            });
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
                var appearance = NormalizeAppearance(payload.Appearance);

                scene.Upsert(new SceneNode(
                    nodeId,
                    label,
                    new Transform(position, rotation, scale),
                    visualScale,
                    phase,
                    appearance));

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

                if (!TryApplyUpdateEntity(scene, payload, out var updatedNode))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new { reason = "update_entity", nodeId = updatedNode.Id.ToString(), label = updatedNode.Label });

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

            commandBus.Subscribe(CommandNames.DeleteEntities, command =>
            {
                if (!TryDeserialize(command.Payload, out DeleteEntitiesPayload? payload) ||
                    payload is null ||
                    payload.Ids.Count == 0)
                {
                    return false;
                }

                var deletedNodeIds = new List<string>();

                foreach (var rawId in payload.Ids.Distinct(StringComparer.Ordinal))
                {
                    if (TryParseNodeId(rawId, out var nodeId) && scene.Remove(nodeId))
                    {
                        deletedNodeIds.Add(nodeId.ToString());
                    }
                }

                if (deletedNodeIds.Count == 0)
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new { reason = "delete_entities", count = deletedNodeIds.Count, nodeIds = deletedNodeIds.ToArray() });

                return true;
            });

            commandBus.Subscribe(CommandNames.UpdateEntities, command =>
            {
                if (!TryDeserialize(command.Payload, out UpdateEntitiesPayload? payload) ||
                    payload is null ||
                    payload.Entities.Count == 0)
                {
                    return false;
                }

                var updatedNodeIds = new List<string>();

                foreach (var entityUpdate in payload.Entities)
                {
                    if (TryApplyUpdateEntity(scene, entityUpdate, out var updatedNode))
                    {
                        updatedNodeIds.Add(updatedNode.Id.ToString());
                    }
                }

                if (updatedNodeIds.Count == 0)
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new
                    {
                        reason = "update_entities",
                        count = updatedNodeIds.Count,
                        nodeIds = updatedNodeIds.ToArray()
                    });

                return true;
            });

             commandBus.Subscribe(CommandNames.ClearPanelAttachment, command =>
             {
                 if (!TryDeserialize(command.Payload, out ClearPanelAttachmentPayload? payload) || payload is null)
                 {
                     return false;
                 }

                 if (!TryParseNodeId(payload.Id, out var nodeId) ||
                     !scene.RemovePanelAttachment(nodeId))
                 {
                     return false;
                 }

                 PublishEvent(
                     EventNames.PanelAttachmentsChanged,
                     new
                     {
                         nodeId = nodeId.ToString(),
                         viewRef = (string?)null,
                         anchor = (string?)null,
                         isVisible = false,
                         surfaceKind = (string?)null,
                         paneletteKind = (string?)null,
                         paneletteTier = (int?)null,
                         commandSurfaceName = (string?)null,
                         commandSurfaceGroup = (string?)null,
                         commandSurfaceSource = (string?)null,
                         commandCount = 0
                     });

                 return true;
             });

            static bool TryApplyUpdateEntity(EngineScene scene, UpdateEntityPayload payload, out SceneNode updatedNode)
            {
                updatedNode = default!;

                if (!TryParseNodeId(payload.Id, out var nodeId) || !scene.TryGet(nodeId, out var existing))
                {
                    return false;
                }

                var nextTransform = new Transform(
                    payload.Position ?? existing.Transform.Position,
                    payload.RotationEuler ?? existing.Transform.RotationEuler,
                    payload.Scale ?? existing.Transform.Scale);

                updatedNode = existing with
                {
                    Label = string.IsNullOrWhiteSpace(payload.Label) ? existing.Label : payload.Label!,
                    Transform = nextTransform,
                    VisualScale = payload.VisualScale ?? existing.VisualScale,
                    Phase = payload.Phase ?? existing.Phase,
                    Appearance = payload.Appearance is null ? existing.Appearance : NormalizeAppearance(payload.Appearance, existing.Appearance)
                };

                scene.Upsert(updatedNode);
                return true;
            }

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

            commandBus.Subscribe(CommandNames.Unlink, command =>
            {
                if (!TryDeserialize(command.Payload, out UnlinkEntitiesPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.SourceId, out var sourceId) ||
                    !TryParseNodeId(payload.TargetId, out var targetId) ||
                    !scene.TryDisconnect(sourceId, targetId, payload.Kind))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new
                    {
                        reason = "unlink_entities",
                        sourceId = sourceId.ToString(),
                        targetId = targetId.ToString(),
                        kind = string.IsNullOrWhiteSpace(payload.Kind) ? null : payload.Kind
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.ClearLinks, _ =>
            {
                if (!scene.ClearLinks())
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new
                    {
                        reason = "clear_links"
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.Undo, _ =>
            {
                if (!scene.TryUndo())
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new
                    {
                        reason = "undo"
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

            commandBus.Subscribe(CommandNames.ClearFocus, _ =>
            {
                var snapshot = scene.GetSnapshot();
                if (snapshot.FocusedNodeId is null && snapshot.FocusedPanel is null)
                {
                    return false;
                }

                scene.ClearFocus();
                PublishEvent(EventNames.FocusChanged, new { focusedNodeId = (string?)null });
                PublishEvent(EventNames.PanelFocusChanged, new { focusedNodeId = (string?)null, viewRef = (string?)null });

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

                PublishSelectionChanged(scene);
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

                PublishSelectionChanged(scene);
                return true;
            });

            commandBus.Subscribe(CommandNames.ClearSelection, _ =>
            {
                scene.ClearSelection();
                PublishSelectionChanged(scene);
                return true;
            });

            commandBus.Subscribe(CommandNames.AttachPanel, command =>
            {
                if (!TryDeserialize(command.Payload, out AttachPanelPayload? payload) || payload is null)
                {
                    return false;
                }

                var commandDescriptors = payload.CommandSurface?.Commands?
                    .Select(command => PanelCommandDescriptor.Create(command.CommandId, command.DisplayLabel))
                    .Where(command => command is not null)
                    .Select(command => command!)
                    .ToArray();

                var commandSurface = payload.CommandSurface is null
                    ? null
                    : PanelCommandSurfaceMetadata.FromPayload(
                        payload.CommandSurface.SurfaceName,
                        payload.CommandSurface.SurfaceGroup,
                        payload.CommandSurface.CommandIds,
                        payload.CommandSurface.SurfaceSource,
                        commandDescriptors);

                if (!TryParseNodeId(payload.Id, out var nodeId) ||
                    !scene.TryAttachPanel(
                        nodeId,
                        payload.ViewRef,
                        payload.LocalOffset,
                        payload.Size,
                        payload.Anchor,
                        payload.IsVisible,
                        payload.SurfaceKind,
                        payload.PaneletteKind,
                        payload.PaneletteTier,
                        commandSurface))
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
                        isVisible = payload.IsVisible,
                        surfaceKind = payload.SurfaceKind,
                        paneletteKind = payload.PaneletteKind,
                        paneletteTier = payload.PaneletteTier,
                        commandSurfaceName = commandSurface?.SurfaceName,
                        commandSurfaceGroup = commandSurface?.SurfaceGroup,
                        commandSurfaceSource = commandSurface?.SurfaceSource,
                        commandCount = commandSurface?.Commands.Count ?? 0
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

            commandBus.Subscribe(CommandNames.AddSelectionToGroup, command =>
            {
                if (!TryDeserialize(command.Payload, out GroupMembershipPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!scene.TryAddSelectionToGroup(payload.GroupId, out var group))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.GroupChanged,
                    new
                    {
                        reason = "add_selection_to_group",
                        groupId = group.Id,
                        label = group.Label,
                        nodeIds = group.NodeIds.Select(id => id.ToString()).ToArray()
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.RemoveSelectionFromGroup, command =>
            {
                if (!TryDeserialize(command.Payload, out GroupMembershipPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!scene.TryRemoveSelectionFromGroup(payload.GroupId, out var group, out var deletedGroup))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.GroupChanged,
                    new
                    {
                        reason = deletedGroup ? "remove_selection_from_group_deleted" : "remove_selection_from_group",
                        groupId = payload.GroupId,
                        label = group?.Label,
                        deletedGroup,
                        nodeIds = group?.NodeIds.Select(id => id.ToString()).ToArray() ?? []
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.DeleteGroup, command =>
            {
                if (!TryDeserialize(command.Payload, out DeleteGroupPayload? payload) || payload is null)
                {
                    return false;
                }

                if (!scene.TryDeleteGroup(payload.GroupId, out var group))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.GroupChanged,
                    new { reason = "delete_group", groupId = group.Id, label = group.Label });

                return true;
            });

            commandBus.Subscribe(CommandNames.HomeView, _ =>
            {
                PublishEvent(
                    EventNames.ViewSetRequested,
                    new
                    {
                        yaw = MathF.PI / 2f,
                        pitch = 0f,
                        distance = 2f,
                        target = new
                        {
                            x = 0f,
                            y = 0f,
                            z = 0f
                        }
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.CenterOnNode, command =>
            {
                if (!TryDeserialize(command.Payload, out CenterOnNodePayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId) || !scene.TryGet(nodeId, out var node))
                {
                    return false;
                }

                var view = scene.TryGetLastView(out var lastView)
                    ? lastView
                    : new ViewParams(MathF.PI / 2f, 0f, 2f, Vector3.Zero);

                var distance = payload.Distance
                    ?? MathF.Max(1.25f, MathF.Max(view.Distance * 0.75f, node.VisualScale * 4f));

                PublishEvent(
                    EventNames.ViewSetRequested,
                    new
                    {
                        yaw = view.Yaw,
                        pitch = view.Pitch,
                        distance,
                        target = new
                        {
                            x = node.Transform.Position.X,
                            y = node.Transform.Position.Y,
                            z = node.Transform.Position.Z
                        }
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.FrameSelection, command =>
            {
                if (!TryDeserialize(command.Payload, out FrameSelectionPayload? payload) || payload is null)
                {
                    return false;
                }

                var snapshot = scene.GetSnapshot();
                var selectedIds = (payload.Ids ?? [])
                    .Select(id => TryParseNodeId(id, out var nodeId) ? nodeId : (NodeId?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToHashSet();

                var framedNodes = snapshot.Nodes
                    .Where(node => selectedIds.Count == 0
                        ? snapshot.SelectedNodeIds?.Contains(node.Id) == true
                        : selectedIds.Contains(node.Id))
                    .ToArray();

                if (framedNodes.Length == 0 && snapshot.FocusedNodeId is { } focusedNodeId)
                {
                    framedNodes = snapshot.Nodes.Where(node => node.Id == focusedNodeId).ToArray();
                }

                if (framedNodes.Length == 0)
                {
                    return false;
                }

                var target = new Vector3(
                    framedNodes.Average(node => node.Transform.Position.X),
                    framedNodes.Average(node => node.Transform.Position.Y),
                    framedNodes.Average(node => node.Transform.Position.Z));

                var radius = framedNodes
                    .Select(node => Vector3.Distance(node.Transform.Position, target) + MathF.Max(node.VisualScale, 0.2f))
                    .DefaultIfEmpty(0.5f)
                    .Max();

                var padding = payload.Padding <= 0f ? 1.35f : payload.Padding;
                var distance = MathF.Max(1.5f, radius * 3.25f * padding);
                var view = scene.TryGetLastView(out var lastView)
                    ? lastView
                    : new ViewParams(MathF.PI / 2f, 0f, distance, target);

                PublishEvent(
                    EventNames.ViewSetRequested,
                    new
                    {
                        yaw = view.Yaw,
                        pitch = view.Pitch,
                        distance,
                        target = new { x = target.X, y = target.Y, z = target.Z }
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.SetInteractionMode, command =>
            {
                if (!TryDeserialize(command.Payload, out SetInteractionModePayload? payload) || payload is null)
                {
                    return false;
                }

                if (!scene.TrySetInteractionMode(payload.Mode))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.InteractionModeChanged,
                    new { mode = scene.GetSnapshot().InteractionMode });
                return true;
            });

            commandBus.Subscribe(CommandNames.BookmarkSave, command =>
            {
                if (!TryDeserialize(command.Payload, out BookmarkSavePayload? payload) || payload is null)
                {
                    return false;
                }

                if (!scene.TrySaveBookmark(payload.Name, out var bookmark))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new
                    {
                        reason = "bookmark_saved",
                        bookmarkName = bookmark.Name
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.BookmarkRestore, command =>
            {
                if (!TryDeserialize(command.Payload, out BookmarkRestorePayload? payload) || payload is null)
                {
                    return false;
                }

                if (!scene.TryRestoreBookmark(payload.Name, out var bookmark))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.SceneChanged,
                    new
                    {
                        reason = "bookmark_restored",
                        bookmarkName = bookmark.Name
                    });
                PublishSelectionChanged(scene);

                if (bookmark.FocusedPanel is { } focusedPanel)
                {
                    PublishEvent(
                        EventNames.PanelFocusChanged,
                        new { focusedNodeId = focusedPanel.NodeId.ToString(), viewRef = focusedPanel.ViewRef });
                }
                else if (bookmark.FocusedNodeId is { } focusedNodeId)
                {
                    PublishEvent(
                        EventNames.FocusChanged,
                        new { focusedNodeId = focusedNodeId.ToString() });
                }

                // If bookmark persisted a view, request renderer to set it
                if (bookmark.View is { } v)
                {
                    PublishEvent(
                        EventNames.ViewSetRequested,
                        new
                        {
                            yaw = v.Yaw,
                            pitch = v.Pitch,
                            distance = v.Distance,
                            target = new { x = v.Target.X, y = v.Target.Y, z = v.Target.Z }
                        });
                }

                PublishEvent(
                    EventNames.FocusOriginChanged,
                    new
                    {
                        origin = "programmatic"
                    });
                return true;
            });

            commandBus.Subscribe(CommandNames.EnterNode, command =>
            {
                if (!TryDeserialize(command.Payload, out EnterNodePayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId) || !scene.TryEnterNode(nodeId))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.NodeEntered,
                    new
                    {
                        nodeId = nodeId.ToString(),
                        mode = string.IsNullOrWhiteSpace(payload.Mode)
                            ? "default"
                            : payload.Mode.Trim().ToLowerInvariant()
                    });

                // v0.1 view behavior: treat EnterNode as a view macro that centers and zooms on the node,
                // using the same basic heuristic as CenterOnNode.
                if (scene.TryGet(nodeId, out var node))
                {
                    var view = scene.TryGetLastView(out var lastView)
                        ? lastView
                        : new ViewParams(MathF.PI / 2f, 0f, 2f, Vector3.Zero);

                    var distance = MathF.Max(1.25f, MathF.Max(view.Distance * 0.75f, node.VisualScale * 4f));

                    PublishEvent(
                        EventNames.ViewSetRequested,
                        new
                        {
                            yaw = view.Yaw,
                            pitch = view.Pitch,
                            distance,
                            target = new
                            {
                                x = node.Transform.Position.X,
                                y = node.Transform.Position.Y,
                                z = node.Transform.Position.Z
                            }
                        });
                }

                return true;
            });

            commandBus.Subscribe(CommandNames.ExitNode, command =>
            {
                if (!TryDeserialize(command.Payload, out ExitNodePayload? payload) || payload is null)
                {
                    return false;
                }

                NodeId? expectedId = null;
                if (!string.IsNullOrWhiteSpace(payload.Id))
                {
                    if (!TryParseNodeId(payload.Id, out var parsedId))
                    {
                        return false;
                    }

                    expectedId = parsedId;
                }

                if (!scene.TryExitNode(expectedId, out var previousId) || previousId is null)
                {
                    return false;
                }

                PublishEvent(
                    EventNames.NodeExited,
                    new
                    {
                        previousNodeId = previousId.Value.ToString()
                    });

                // v0.1 view behavior: treat ExitNode as returning to a simple Home View posture.
                PublishEvent(
                    EventNames.ViewSetRequested,
                    new
                    {
                        yaw = MathF.PI / 2f,
                        pitch = 0f,
                        distance = 2f,
                        target = new
                        {
                            x = 0f,
                            y = 0f,
                            z = 0f
                        }
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.ExpandNode, command =>
            {
                if (!TryDeserialize(command.Payload, out ExpandNodePayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId) || !scene.TryExpandNode(nodeId))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.NodeExpanded,
                    new
                    {
                        nodeId = nodeId.ToString()
                    });

                return true;
            });

            commandBus.Subscribe(CommandNames.CollapseNode, command =>
            {
                if (!TryDeserialize(command.Payload, out CollapseNodePayload? payload) || payload is null)
                {
                    return false;
                }

                if (!TryParseNodeId(payload.Id, out var nodeId) || !scene.TryCollapseNode(nodeId))
                {
                    return false;
                }

                PublishEvent(
                    EventNames.NodeCollapsed,
                    new
                    {
                        nodeId = nodeId.ToString()
                    });

                return true;
            });
        }

        private static void PublishSelectionChanged(EngineScene scene)
        {
            var snapshot = scene.GetSnapshot();

            PublishEvent(
                EventNames.SelectionChanged,
                new
                {
                    selectedNodeIds = snapshot.SelectedNodeIds?
                        .Select(id => id.ToString())
                        .ToArray() ?? [],
                    selectedPanels = snapshot.SelectedPanels?
                        .Select(panel => new { nodeId = panel.NodeId.ToString(), viewRef = panel.ViewRef })
                        .ToArray() ?? []
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

        private static NodeAppearance NormalizeAppearance(NodeAppearancePayload? payload, NodeAppearance? fallback = null)
        {
            var baseline = fallback ?? NodeAppearance.Default;
            var primitive = string.IsNullOrWhiteSpace(payload?.Primitive)
                ? baseline.Primitive
                : payload!.Primitive!.Trim().ToLowerInvariant();
            var fillColor = NormalizeHexColor(payload?.FillColor, baseline.FillColor);
            var outlineColor = NormalizeHexColor(payload?.OutlineColor, baseline.OutlineColor);
            var opacity = payload?.Opacity is float opacityValue
                ? Math.Clamp(opacityValue, 0.1f, 1.0f)
                : baseline.Opacity;

            return new NodeAppearance(
                primitive,
                fillColor,
                outlineColor,
                opacity);
        }

        private static string NormalizeHexColor(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var trimmed = value.Trim();
            return trimmed.Length == 7 && trimmed[0] == '#' && trimmed.Skip(1).All(Uri.IsHexDigit)
                ? trimmed.ToUpperInvariant()
                : fallback;
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
                Phase: 0.0f,
                Appearance: new NodeAppearance("triangle", "#7DCBFF", "#EAF6FF", 1.0f));

            var nodeB = new SceneNode(
                NodeId.New(),
                "Node B",
                new Transform(
                    new Vector3(0.9f, 0.2f, 0.0f),
                    Vector3.Zero,
                    new Vector3(0.5f, 0.5f, 0.5f)),
                VisualScale: 0.5f,
                Phase: 1.2f,
                Appearance: new NodeAppearance("triangle", "#B69CFF", "#F3EEFF", 1.0f));

            var nodeC = new SceneNode(
                NodeId.New(),
                "Node C",
                new Transform(
                    new Vector3(0.0f, 0.7f, 0.0f),
                    Vector3.Zero,
                    new Vector3(0.7f, 0.7f, 0.7f)),
                VisualScale: 0.7f,
                Phase: 2.35f,
                Appearance: new NodeAppearance("triangle", "#86E0A5", "#ECFFF3", 1.0f));

            scene.Upsert(nodeA);
            scene.Upsert(nodeB);
            scene.Upsert(nodeC);
            scene.TryConnect(nodeA.Id, nodeB.Id, "baseline", 1.0f);
            scene.TryAttachPanel(
                nodeA.Id,
                "panelette.label.seed",
                new Vector3(0f, -0.18f, 0.1f),
                new Vector2(0.92f, 0.28f),
                "bottom",
                true);
            scene.TryAttachPanel(
                nodeC.Id,
                "panelette.meta.seed",
                new Vector3(0f, 0.22f, 0.14f),
                new Vector2(1.25f, 0.72f),
                "top",
                true,
                "panelette",
                "metadata",
                1,
                new PanelCommandSurfaceMetadata(
                    "node.quick",
                    "primary",
                    [
                        new PanelCommandDescriptor(CommandNames.CenterOnNode, "Center On Node"),
                        new PanelCommandDescriptor(CommandNames.Select, "Select Node"),
                        new PanelCommandDescriptor(CommandNames.Focus, "Focus Node"),
                        new PanelCommandDescriptor("Engine.PromotePaneletteToShell", "Promote to Pane")
                    ],
                    "engine"));
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

        private static bool IsUndoableCommandName(string commandName)
        {
            return commandName is
                CommandNames.CreateEntity or
                CommandNames.UpdateEntity or
                CommandNames.UpdateEntities or
                CommandNames.Delete or
                CommandNames.DeleteEntities or
                CommandNames.SetTransform or
                CommandNames.Connect or
                CommandNames.Unlink or
                CommandNames.ClearLinks or
                CommandNames.Focus or
                CommandNames.ClearFocus or
                CommandNames.FocusPanel or
                CommandNames.Select or
                CommandNames.SelectPanel or
                CommandNames.ClearSelection or
                CommandNames.AttachPanel or
                 CommandNames.ClearPanelAttachment or
                CommandNames.AddSelectionToGroup or
                CommandNames.RemoveSelectionFromGroup or
                CommandNames.DeleteGroup or
                CommandNames.GroupSelection or
                CommandNames.BookmarkSave or
                CommandNames.BookmarkRestore;
        }

        private static bool ShouldPersistSceneAfterCommand(string commandName)
        {
            return IsUndoableCommandName(commandName) ||
                   commandName is CommandNames.Undo or
                   CommandNames.SetInteractionMode or
                   CommandNames.EnterNode or
                   CommandNames.ExitNode or
                   CommandNames.ExpandNode or
                   CommandNames.CollapseNode;
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
                EngineScene? scene = Scene;
                var shouldTrackUndo = IsUndoableCommandName(command.Name) &&
                                      !string.Equals(command.Name, CommandNames.Undo, StringComparison.Ordinal) &&
                                      scene is not null;
                SceneSnapshot? undoSnapshot = null;

                if (shouldTrackUndo)
                {
                    if (scene is null)
                    {
                        return;
                    }
                    undoSnapshot = scene.GetSnapshot();
                }

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
                    var shouldPersistScene = scene is not null &&
                                             ShouldPersistSceneAfterCommand(command.Name);

                    if (undoSnapshot is not null && scene is not null)
                    {
                        scene.PushUndoSnapshot(undoSnapshot);
                    }

                    if (shouldPersistScene)
                    {
                        PersistCurrentSceneSnapshot();
                    }

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
