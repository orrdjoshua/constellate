using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Windows.Input;
using Constellate.Core.Capabilities;
using Constellate.Core.Messaging;
using Constellate.Core.Scene;
using Constellate.SDK;

namespace Constellate.App
{
    public sealed partial class MainWindowViewModel
    {
        public MainWindowViewModel()
        {
            // Event subscriptions (EngineSync partial provides Subscribe* helpers)
            _eventSubscriptions =
            [
                SubscribeRefresh(EventNames.CommandInvoked, "command activity"),
                SubscribeRefresh(EventNames.SceneChanged, "scene changed"),
                SubscribePanelInteraction(),
                SubscribeRefresh(EventNames.FocusChanged, "focus changed"),
                SubscribeResourceSurfaceBinding(),
                SubscribeRefresh(EventNames.PanelFocusChanged, "panel focus changed"),
                SubscribeRefresh(EventNames.SelectionChanged, "selection changed"),
                SubscribeRefresh(EventNames.PanelAttachmentsChanged, "panel attachments changed"),
                SubscribeRefresh(EventNames.InteractionModeChanged, "interaction mode changed"),
                SubscribeRefresh(EventNames.GroupChanged, "group changed"),
                SubscribeRefresh(EventNames.FocusOriginChanged, "focus origin changed")
            ];

            // Commands (many rely on helpers from SceneCommands/Settings/PaneLayout/PaneCommands partials)
            _focusFirstNodeCommand = new RelayCommand(
                _ =>
                {
                    var firstNode = _shellScene.GetNodes().FirstOrDefault();
                    if (firstNode is null) return;
                    PublishFocusOrigin("command");
                    SendCommand(CommandNames.Focus, new FocusEntityPayload(firstNode.Id.ToString()));
                },
                _ => _shellScene.GetNodes().Count > 0);

            _selectFirstNodeCommand = new RelayCommand(
                _ =>
                {
                    var firstNode = _shellScene.GetNodes().FirstOrDefault();
                    if (firstNode is null) return;
                    SendCommand(CommandNames.Select, new SelectEntitiesPayload([firstNode.Id.ToString()]));
                },
                _ => _shellScene.GetNodes().Count > 0);

            _focusFirstPanelCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFirstPanelTarget() is { } panelTarget)
                    {
                        PublishFocusOrigin("command");
                        SendCommand(CommandNames.FocusPanel, new FocusPanelPayload(panelTarget.NodeId.ToString(), panelTarget.ViewRef));
                    }
                },
                _ => _shellScene.GetFirstPanelTarget() is not null);

            _selectFirstPanelCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFirstPanelTarget() is { } panelTarget)
                    {
                        SendCommand(CommandNames.SelectPanel, new SelectPanelPayload(panelTarget.NodeId.ToString(), panelTarget.ViewRef));
                    }
                },
                _ => _shellScene.GetFirstPanelTarget() is not null);

            _activateNavigateModeCommand = new RelayCommand(
                _ => SendCommand(CommandNames.SetInteractionMode, new SetInteractionModePayload("navigate")),
                _ => !IsInteractionMode("navigate"));

            _activateMoveModeCommand = new RelayCommand(
                _ => SendCommand(CommandNames.SetInteractionMode, new SetInteractionModePayload("move")),
                _ => !IsInteractionMode("move"));

            _activateMarqueeModeCommand = new RelayCommand(
                _ => SendCommand(CommandNames.SetInteractionMode, new SetInteractionModePayload("marquee")),
                _ => !IsInteractionMode("marquee"));

            _createDemoNodeCommand = new RelayCommand(_ =>
            {
                var index = _shellScene.GetNodes().Count + 1;
                var angle = (float)(index * 0.85);
                var radius = 0.55f + (0.08f * (index % 3));
                var position = new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f);

                SendCommand(
                    CommandNames.CreateEntity,
                    new CreateEntityPayload(
                        Type: "node",
                        Id: null,
                        Label: $"Demo Node {index}",
                        Position: position,
                        RotationEuler: Vector3.Zero,
                        Scale: new Vector3(0.45f, 0.45f, 0.45f),
                        VisualScale: 0.45f,
                        Phase: index * 0.35f));
            });

            _nudgeFocusedLeftCommand = CreateSelectionOrFocusTransformCommand(new Vector3(-0.12f, 0f, 0f), 1f);
            _nudgeFocusedRightCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0.12f, 0f, 0f), 1f);
            _nudgeFocusedUpCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0f, 0.08f, 0f), 1f);
            _nudgeFocusedDownCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0f, -0.08f, 0f), 1f);
            _nudgeFocusedForwardCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0f, 0f, -0.12f), 1f);
            _nudgeFocusedBackCommand = CreateSelectionOrFocusTransformCommand(new Vector3(0f, 0f, 0.12f), 1f);
            _growFocusedNodeCommand = CreateSelectionOrFocusTransformCommand(Vector3.Zero, 1.15f);
            _shrinkFocusedNodeCommand = CreateSelectionOrFocusTransformCommand(Vector3.Zero, 1f / 1.15f);
            _applyTrianglePrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "triangle");
            _applySquarePrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "square");
            _applyDiamondPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "diamond");
            _applyPentagonPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "pentagon");
            _applyHexagonPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "hexagon");
            _applyCubePrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "cube");
            _applyTetrahedronPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "tetrahedron");
            _applySpherePrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "sphere");
            _applyBoxPrimitiveCommand = CreateSelectionOrFocusAppearanceCommand(primitive: "box");
            _applyBlueAppearanceCommand = CreateSelectionOrFocusAppearanceCommand(fillColor: "#7DCBFF");
            _applyVioletAppearanceCommand = CreateSelectionOrFocusAppearanceCommand(fillColor: "#B69CFF");
            _applyGreenAppearanceCommand = CreateSelectionOrFocusAppearanceCommand(fillColor: "#86E0A5");
            _increaseOpacityCommand = CreateSelectionOrFocusAppearanceCommand(opacityDelta: 0.15f);
            _decreaseOpacityCommand = CreateSelectionOrFocusAppearanceCommand(opacityDelta: -0.15f);

            _connectFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null) return;

                    var sourceNodeId = _shellScene.GetSelectedNodeIds().FirstOrDefault(nodeId => nodeId != focusedNode.Id);
                    if (sourceNodeId == default) return;

                    SendCommand(
                        CommandNames.Connect,
                        new ConnectEntitiesPayload(
                            sourceNodeId.ToString(),
                            focusedNode.Id.ToString(),
                            Kind: "directed",
                            Weight: 1.0f));
                },
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null) return false;
                    return _shellScene.GetSelectedNodeIds().Any(nodeId => nodeId != focusedNode.Id);
                });

            _unlinkFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null) return;

                    var sourceNodeId = _shellScene.GetSelectedNodeIds().FirstOrDefault(nodeId => nodeId != focusedNode.Id);
                    if (sourceNodeId == default) return;

                    SendCommand(
                        CommandNames.Unlink,
                        new UnlinkEntitiesPayload(
                            sourceNodeId.ToString(),
                            focusedNode.Id.ToString(),
                            Kind: "directed"));
                },
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null) return false;

                    var sourceNodeId = _shellScene.GetSelectedNodeIds().FirstOrDefault(nodeId => nodeId != focusedNode.Id);
                    return sourceNodeId != default &&
                           _shellScene.GetLinks().Any(link =>
                               link.SourceId == sourceNodeId &&
                               link.TargetId == focusedNode.Id &&
                               string.Equals(link.Kind, "directed", StringComparison.Ordinal));
                });

            _groupSelectionCommand = new RelayCommand(
                _ =>
                {
                    var selectedCount = _shellScene.GetSelectedNodeIds().Count;
                    SendCommand(CommandNames.GroupSelection, new GroupSelectionPayload($"Group {(_shellScene.GetGroups().Count + 1)} ({selectedCount} nodes)"));
                },
                _ => _shellScene.GetSelectedNodeIds().Count >= 2);

            _addSelectionToActiveGroupCommand = new RelayCommand(
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    if (activeGroup is null) return;
                    SendCommand(CommandNames.AddSelectionToGroup, new GroupMembershipPayload(activeGroup.Id));
                },
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    return activeGroup is not null &&
                           _shellScene.GetSelectedNodeIds().Any(nodeId => !activeGroup.NodeIds.Contains(nodeId));
                });

            _removeSelectionFromActiveGroupCommand = new RelayCommand(
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    if (activeGroup is null) return;
                    SendCommand(CommandNames.RemoveSelectionFromGroup, new GroupMembershipPayload(activeGroup.Id));
                },
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    return activeGroup is not null &&
                           _shellScene.GetSelectedNodeIds().Any(nodeId => activeGroup.NodeIds.Contains(nodeId));
                });

            _deleteActiveGroupCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetActiveGroup() is { } activeGroup)
                    {
                        SendCommand(CommandNames.DeleteGroup, new DeleteGroupPayload(activeGroup.Id));
                    }
                },
                _ => _shellScene.GetActiveGroup() is not null);

            _saveBookmarkCommand = new RelayCommand(
                _ =>
                {
                    var index = _shellScene.GetBookmarks().Count + 1;
                    SendCommand(CommandNames.BookmarkSave, new BookmarkSavePayload($"Bookmark {index}"));
                },
                _ =>
                {
                    return _shellScene.GetFocusedNode() is not null ||
                           _shellScene.GetFocusedPanel() is not null ||
                           _shellScene.GetSelectedNodeIds().Count > 0 ||
                           _shellScene.GetSelectedPanels().Count > 0;
                });

            _restoreLatestBookmarkCommand = new RelayCommand(
                _ =>
                {
                    var latest = _shellScene.GetBookmarks().OrderBy(b => b.Name, StringComparer.Ordinal).LastOrDefault();
                    if (latest is null) return;
                    SendCommand(CommandNames.BookmarkRestore, new BookmarkRestorePayload(latest.Name));
                },
                _ => _shellScene.GetBookmarks().Count > 0);

            _undoLastCommand = new RelayCommand(_ => SendCommand<object?>(CommandNames.Undo, null), _ => EngineServices.Scene.CanUndo);

            _homeViewCommand = new RelayCommand(_ => SendCommand<object?>(CommandNames.HomeView, null));

            _centerFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFocusedNode() is { } focusedNode)
                    {
                        SendCommand(CommandNames.CenterOnNode, new CenterOnNodePayload(focusedNode.Id.ToString()));
                    }
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _frameSelectionCommand = new RelayCommand(
                _ => SendCommand(CommandNames.FrameSelection, new FrameSelectionPayload()),
                _ => _shellScene.GetSelectedNodeIds().Count > 0 || _shellScene.GetFocusedNode() is not null);

            _enterFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null) return;
                    SendCommand(CommandNames.EnterNode, new EnterNodePayload(focusedNode.Id.ToString()));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _exitNodeCommand = new RelayCommand(
                _ =>
                {
                    var enteredId = _shellScene.GetEnteredNodeId();
                    if (enteredId is null) return;
                    SendCommand(CommandNames.ExitNode, new ExitNodePayload(enteredId.Value.ToString()));
                },
                _ => _shellScene.GetEnteredNodeId() is not null);

            _deleteFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var targets = GetSelectionOrFocusTargetNodes();
                    if (targets.Length == 0) return;

                    SendCommand(
                        CommandNames.DeleteEntities,
                        new DeleteEntitiesPayload(targets.Select(n => n.Id.ToString()).ToArray()));
                },
                _ => GetSelectionOrFocusTargetNodes().Length > 0);

            _attachDemoPanelCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null) return;

                    var attachmentCount = _shellScene.GetPanelAttachments().Count;
                    var viewRef = $"panelette.meta.{attachmentCount + 1}";

                    SendCommand(
                        CommandNames.AttachPanel,
                        new AttachPanelPayload(
                            focusedNode.Id.ToString(),
                            viewRef,
                            LocalOffset: new Vector3(0f, 0.18f, 0.15f),
                            Size: new Vector2(1.05f, 0.62f),
                            Anchor: "top",
                            IsVisible: true,
                            SurfaceKind: "panelette",
                            PaneletteKind: "metadata",
                            PaneletteTier: 1,
                            CommandSurface: new PanelCommandSurfaceMetadataPayload(
                                SurfaceName: "node.quick",
                                SurfaceGroup: "primary",
                                CommandIds: [CommandNames.Focus, CommandNames.Select, CommandNames.CenterOnNode, "Engine.PromotePaneletteToShell"])));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _attachLabelPaneletteCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null) return;

                    var attachmentCount = _shellScene.GetPanelAttachments().Count;
                    var viewRef = $"panelette.label.{attachmentCount + 1}";

                    SendCommand(
                        CommandNames.AttachPanel,
                        new AttachPanelPayload(
                            focusedNode.Id.ToString(),
                            viewRef,
                            LocalOffset: new Vector3(0f, -0.18f, 0.1f),
                            Size: new Vector2(0.92f, 0.28f),
                            Anchor: "bottom",
                            IsVisible: true,
                            SurfaceKind: "panelette",
                            PaneletteKind: "label",
                            PaneletteTier: 1));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _attachDetailMetadataPaneletteCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null) return;

                    var attachmentCount = _shellScene.GetPanelAttachments().Count;
                    var viewRef = $"panelette.meta.detail.{attachmentCount + 1}";

                    SendCommand(
                        CommandNames.AttachPanel,
                        new AttachPanelPayload(
                            focusedNode.Id.ToString(),
                            viewRef,
                            LocalOffset: new Vector3(0f, 0.26f, 0.16f),
                            Size: new Vector2(1.35f, 0.82f),
                            Anchor: "top",
                            IsVisible: true,
                            SurfaceKind: "panelette",
                            PaneletteKind: "metadata",
                            PaneletteTier: 2,
                            CommandSurface: new PanelCommandSurfaceMetadataPayload(
                                SurfaceName: "node.detail",
                                SurfaceGroup: "primary",
                                CommandIds: ["Engine.PromotePaneletteToShell"])));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _clearLinksCommand = new RelayCommand(_ => SendCommand<object?>(CommandNames.ClearLinks, null), _ => _shellScene.GetLinks().Count > 0);
            _clearSelectionCommand = new RelayCommand(_ => SendCommand<object?>(CommandNames.ClearSelection, null), _ => _shellScene.GetSelectedNodeIds().Count > 0 || _shellScene.GetSelectedPanels().Count > 0);

            _applyBackgroundDeepSpaceCommand = new RelayCommand(_ => ApplyBackgroundPreset("DeepSpace"));
            _applyBackgroundDuskCommand = new RelayCommand(_ => ApplyBackgroundPreset("Dusk"));
            _applyBackgroundPaperCommand = new RelayCommand(_ => ApplyBackgroundPreset("Paper"));

            _createChildPaneCommand = new RelayCommand(parameter =>
            {
                var parentOrHost = parameter as string;
                CreateChildPane(parentOrHost);
            });

            _minimizeChildPaneCommand = new RelayCommand(parameter =>
            {
                if (parameter is string id && !string.IsNullOrWhiteSpace(id)) SetChildPaneMinimized(id, true);
            });

            _restoreChildPaneFromTaskbarCommand = new RelayCommand(parameter =>
            {
                if (parameter is string id && !string.IsNullOrWhiteSpace(id)) SetChildPaneMinimized(id, false);
            });

            _minimizeShellPaneCommand = new RelayCommand(
                parameter =>
                {
                    var hostId = parameter as string;
                    SetParentPaneMinimized(hostId, true);
                },
                _ => ParentPaneModels.Any(p => !p.IsMinimized));

            _restoreShellPaneCommand = new RelayCommand(
                parameter =>
                {
                    var hostId = parameter as string;
                    SetParentPaneMinimized(hostId, false);
                },
                _ => ParentPaneModels.Any(p => p.IsMinimized));

            _resetLayoutToDefaultCommand = new RelayCommand(_ =>
            {
                if (ParentPaneModels.Count == 0) return;

                foreach (var parent in ParentPaneModels)
                {
                    parent.HostId = "left";
                    parent.IsMinimized = false;
                    parent.SplitCount = 1;
                    parent.SlideIndex = 0;
                }

                _leftSlideIndex = 0;
                _topSlideIndex = 0;
                _rightSlideIndex = 0;
                _bottomSlideIndex = 0;

                RaiseParentPaneLayoutChanged(includeChildRefresh: true);
            });

            _setLeftPaneSplitCommand = new RelayCommand(parameter =>
            {
                var splits = 1;
                if (parameter is string s && int.TryParse(s, out var parsed) && parsed >= 1)
                    splits = Math.Min(parsed, 3);

                ApplyChildPaneSplitsForHost("left", splits);
            });

            _saveLayoutPresetCommand = new RelayCommand(_ =>
            {
                if (ParentPaneModels.Count == 0) return;

                var parentPanes = ParentPaneModels
                    .Select(p => new ParentPaneLayoutSnapshot(
                        p.Id,
                        p.Title,
                        NormalizeHostId(p.HostId),
                        IsMinimized: p.IsMinimized,
                        FloatingX: p.FloatingX,
                        FloatingY: p.FloatingY,
                        FloatingWidth: p.FloatingWidth,
                        FloatingHeight: p.FloatingHeight,
                        SplitCount: p.SplitCount,
                        SlideIndex: p.SlideIndex))
                    .ToArray();

                var first = ParentPaneModels[0];
                _savedLayout = new ShellLayoutDescriptor(
                    HostId: NormalizeHostId(first.HostId),
                    IsMinimized: first.IsMinimized,
                    SavedHostId: NormalizeHostId(first.HostId),
                    SavedIsMinimized: first.IsMinimized,
                    LeftSlideIndex: _leftSlideIndex,
                    TopSlideIndex: _topSlideIndex,
                    RightSlideIndex: _rightSlideIndex,
                    BottomSlideIndex: _bottomSlideIndex,
                    ParentPanes: parentPanes,
                    ChildPanes: ChildPanes
                        .Where(pane => !IsAutoGeneratedResourceBoundDetailPane(pane))
                        .ToArray());
            });

            _restoreLayoutPresetCommand = new RelayCommand(_ =>
            {
                if (_savedLayout is not ShellLayoutDescriptor descriptor || string.IsNullOrWhiteSpace(descriptor.SavedHostId)) return;

                _leftSlideIndex = descriptor.LeftSlideIndex;
                _topSlideIndex = descriptor.TopSlideIndex;
                _rightSlideIndex = descriptor.RightSlideIndex;
                _bottomSlideIndex = descriptor.BottomSlideIndex;

                ParentPaneModels.Clear();

                if (descriptor.ParentPanes is { Count: > 0 } parents)
                {
                    foreach (var p in parents)
                    {
                        ParentPaneModels.Add(new ParentPaneModel
                        {
                            Id = p.Id,
                            Title = p.Title,
                            HostId = NormalizeHostId(p.HostId),
                            IsMinimized = p.IsMinimized,
                            SplitCount = Math.Max(1, p.SplitCount),
                            SlideIndex = Math.Max(0, p.SlideIndex),
                            FloatingX = p.FloatingX,
                            FloatingY = p.FloatingY,
                            FloatingWidth = p.FloatingWidth,
                            FloatingHeight = p.FloatingHeight
                        });
                    }
                }
                else
                {
                    ParentPaneModels.Add(new ParentPaneModel
                    {
                        Id = "parent.main",
                        Title = "Parent Pane",
                        HostId = NormalizeHostId(descriptor.SavedHostId),
                        IsMinimized = descriptor.SavedIsMinimized,
                        SplitCount = 1,
                        SlideIndex = GetSlideIndexForHost(NormalizeHostId(descriptor.SavedHostId)),
                        FloatingWidth = 320,
                        FloatingHeight = 240
                    });
                }

                ChildPanes.Clear();
                if (descriptor.ChildPanes is { Count: > 0 } savedChildren)
                {
                    foreach (var c in savedChildren) ChildPanes.Add(c);
                }

                TrySyncActiveResourceDetailSurfaceFromPersistence();

                RaiseChildPaneCollectionsChanged();
                RaiseParentPaneLayoutChanged(includeChildRefresh: true);
            });

            _moveChildPaneUpCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is string id && !string.IsNullOrWhiteSpace(id)) MoveChildPane(id, -1);
                },
                parameter =>
                {
                    if (parameter is not string id || string.IsNullOrWhiteSpace(id)) return false;
                    return CanMoveChildPane(id, -1);
                });

            _moveChildPaneDownCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is string id && !string.IsNullOrWhiteSpace(id)) MoveChildPane(id, 1);
                },
                parameter =>
                {
                    if (parameter is not string id || string.IsNullOrWhiteSpace(id)) return false;
                    return CanMoveChildPane(id, 1);
                });

            _destroyChildPaneCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is string id && !string.IsNullOrWhiteSpace(id)) DestroyChildPane(id);
                },
                _ => true);

            _floatSettingsChildPaneCommand = new RelayCommand(
                _ =>
                {
                    SetChildPaneMinimized("shell.settings", false);
                    IsSettingsChildFloating = true;
                },
                _ => !IsSettingsChildFloating);

            _dockSettingsChildPaneCommand = new RelayCommand(_ => IsSettingsChildFloating = false, _ => IsSettingsChildFloating);

            _moveChildPaneToLeftHostCommand = new RelayCommand(parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) MoveChildPaneToHost(id, "left"); });
            _moveChildPaneToTopHostCommand = new RelayCommand(parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) MoveChildPaneToHost(id, "top"); });
            _moveChildPaneToRightHostCommand = new RelayCommand(parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) MoveChildPaneToHost(id, "right"); });
            _moveChildPaneToBottomHostCommand = new RelayCommand(parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) MoveChildPaneToHost(id, "bottom"); });
            _moveChildPaneToFloatingHostCommand = new RelayCommand(parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) MoveChildPaneToHost(id, "floating"); });

            _createOrRestoreParentPaneCommand = new RelayCommand(
                parameter =>
                {
                    if (parameter is not string hostId || string.IsNullOrWhiteSpace(hostId)) return;

                    var normalizedHost = NormalizeHostId(hostId);
                    var hadTopVisible = IsShellPaneOnTop;
                    var hadLeftVisible = IsShellPaneOnLeft;

                    var existingParent = ParentPaneModels.FirstOrDefault(p => string.Equals(NormalizeHostId(p.HostId), normalizedHost, StringComparison.Ordinal));
                    if (existingParent is not null)
                    {
                        // TOGGLE semantics:
                        // - if minimized: restore on same host and sync slide index
                        // - if expanded: minimize it
                        existingParent.HostId = normalizedHost;
                        if (existingParent.IsMinimized)
                        {
                            existingParent.IsMinimized = false;
                            existingParent.SlideIndex = GetSlideIndexForHost(normalizedHost);
                        }
                        else
                        {
                            existingParent.IsMinimized = true;
                        }
                        RaiseParentPaneLayoutChanged(includeChildRefresh: true);
                        return;
                    }

                    ParentPaneModels.Add(CreateParentPaneModel(normalizedHost));

                    var hasTopVisible = IsShellPaneOnTop;
                    var hasLeftVisible = IsShellPaneOnLeft;

                    if (hasTopVisible && hasLeftVisible)
                    {
                        if (string.Equals(normalizedHost, "top", StringComparison.Ordinal) && !hadTopVisible && hadLeftVisible)
                            _isTopCornerOwnedByTop = false;
                        else if (string.Equals(normalizedHost, "left", StringComparison.Ordinal) && !hadLeftVisible && hadTopVisible)
                            _isTopCornerOwnedByTop = true;
                    }

                    RaiseParentPaneLayoutChanged(includeChildRefresh: true);
                },
                _ => true);

            _destroyParentPaneCommand = new RelayCommand(
                parameter =>
                {
                    if (ParentPaneModels.Count == 0) return;

                    var removedParentIds = new HashSet<string>(StringComparer.Ordinal);
                    if (parameter is string arg && !string.IsNullOrWhiteSpace(arg))
                    {
                        if (ParentPaneModels.Any(p => string.Equals(p.Id, arg, StringComparison.Ordinal)))
                        {
                            for (var i = ParentPaneModels.Count - 1; i >= 0; i--)
                            {
                                var pane = ParentPaneModels[i];
                                if (!string.Equals(pane.Id, arg, StringComparison.Ordinal)) continue;
                                removedParentIds.Add(pane.Id);
                                ParentPaneModels.RemoveAt(i);
                                break;
                            }
                        }
                        else
                        {
                            var normalizedHost = NormalizeHostId(arg);
                            for (var i = ParentPaneModels.Count - 1; i >= 0; i--)
                            {
                                var pane = ParentPaneModels[i];
                                if (!string.Equals(NormalizeHostId(pane.HostId), normalizedHost, StringComparison.Ordinal)) continue;
                                removedParentIds.Add(pane.Id);
                                ParentPaneModels.RemoveAt(i);
                            }
                        }
                    }
                    else
                    {
                        foreach (var pane in ParentPaneModels)
                            removedParentIds.Add(pane.Id);
                        ParentPaneModels.Clear();
                    }

                    if (removedParentIds.Count > 0)
                    {
                        for (var i = ChildPanes.Count - 1; i >= 0; i--)
                        {
                            var child = ChildPanes[i];
                            if (removedParentIds.Contains(child.ParentId ?? string.Empty))
                            {
                                ChildPanes.RemoveAt(i);
                            }
                        }

                        RaiseChildPaneCollectionsChanged();
                    }

                    RaiseParentPaneLayoutChanged();
                },
                _ => ParentPaneModels.Count > 0);

            _setTopPaneSplitCommand = new RelayCommand(parameter =>
            {
                var splits = 1;
                if (parameter is string s && int.TryParse(s, out var parsed) && parsed >= 1) splits = Math.Min(parsed, 3);
                ApplyChildPaneSplitsForHost("top", splits);
            });

            _setRightPaneSplitCommand = new RelayCommand(parameter =>
            {
                var splits = 1;
                if (parameter is string s && int.TryParse(s, out var parsed) && parsed >= 1) splits = Math.Min(parsed, 3);
                ApplyChildPaneSplitsForHost("right", splits);
            });

            _setBottomPaneSplitCommand = new RelayCommand(parameter =>
            {
                var splits = 1;
                if (parameter is string s && int.TryParse(s, out var parsed) && parsed >= 1) splits = Math.Min(parsed, 3);
                ApplyChildPaneSplitsForHost("bottom", splits);
            });

            _slideParentPaneCommand = new RelayCommand(parameter =>
            {
                if (parameter is not string arg || string.IsNullOrWhiteSpace(arg)) return;
                SlideParentPane(arg);
            });

            // Per-parent split controls (1..3)
            _setParentSplitTo1Command = new RelayCommand(
                parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) SetParentSplitCount(id, 1); });
            _setParentSplitTo2Command = new RelayCommand(
                parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) SetParentSplitCount(id, 2); });
            _setParentSplitTo3Command = new RelayCommand(
                parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) SetParentSplitCount(id, 3); });

            // Per-parent slide controls (1..3) — indices 0..2
            _setParentSlideTo1Command = new RelayCommand(
                parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) SetParentSlideIndex(id, 0); });
            _setParentSlideTo2Command = new RelayCommand(
                parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) SetParentSlideIndex(id, 1); });
            _setParentSlideTo3Command = new RelayCommand(
                parameter => { if (parameter is string id && !string.IsNullOrWhiteSpace(id)) SetParentSlideIndex(id, 2); });

            // Header chrome stubs (Rename/Add/Remove CommandBar button)
            _renamePaneCommand = new RelayCommand(
                parameter =>
                {
                    var target = parameter as string ?? "(unknown)";
                    _lastActivitySummary = $"Last Activity: Rename Pane… requested for '{target}' @ {DateTimeOffset.Now:HH:mm:ss}";
                    OnPropertyChanged(nameof(LastActivitySummary));
                },
                _ => true);

            _addCommandBarButtonCommand = new RelayCommand(
                parameter =>
                {
                    var target = parameter as string ?? "(unknown)";
                    _lastActivitySummary = $"Last Activity: Add CommandBar Button… requested for '{target}' @ {DateTimeOffset.Now:HH:mm:ss}";
                    OnPropertyChanged(nameof(LastActivitySummary));
                },
                _ => true);

            _removeCommandBarButtonCommand = new RelayCommand(
                parameter =>
                {
                    // For now we only receive the pane id; later we can pass a button id as CommandParameter.
                    var target = parameter as string ?? "(unknown)";
                    _lastActivitySummary = $"Last Activity: Remove CommandBar Button… requested for '{target}' @ {DateTimeOffset.Now:HH:mm:ss}";
                    OnPropertyChanged(nameof(LastActivitySummary));
                },
                _ => true);

            // Initial state refresh after command wiring
            MaterializePersistedResourceDetailSurfacesAtStartup();
            // Ownership depends on visible top/left
            // UpdateTopLeftOwnershipLayout() is invoked as panes come/go via RaiseParentPaneLayoutChanged
        }
    }
}
