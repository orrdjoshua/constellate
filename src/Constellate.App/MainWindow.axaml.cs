using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Constellate.Core.Capabilities;
using Constellate.Core.Messaging;
using Constellate.Core.Scene;
using Constellate.SDK;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IDisposable[] _eventSubscriptions;
        private readonly ShellSceneState _shellScene = EngineServices.ShellScene;

        private readonly RelayCommand _focusFirstNodeCommand;
        private readonly RelayCommand _selectFirstNodeCommand;
        private readonly RelayCommand _focusFirstPanelCommand;
        private readonly RelayCommand _selectFirstPanelCommand;
        private readonly RelayCommand _activateNavigateModeCommand;
        private readonly RelayCommand _activateMoveModeCommand;
        private readonly RelayCommand _activateMarqueeModeCommand;
        private readonly RelayCommand _createDemoNodeCommand;
        private readonly RelayCommand _nudgeFocusedLeftCommand;
        private readonly RelayCommand _nudgeFocusedRightCommand;
        private readonly RelayCommand _nudgeFocusedUpCommand;
        private readonly RelayCommand _nudgeFocusedDownCommand;
        private readonly RelayCommand _nudgeFocusedForwardCommand;
        private readonly RelayCommand _nudgeFocusedBackCommand;
        private readonly RelayCommand _growFocusedNodeCommand;
        private readonly RelayCommand _shrinkFocusedNodeCommand;
        private readonly RelayCommand _connectFocusedNodeCommand;
        private readonly RelayCommand _groupSelectionCommand;
        private readonly RelayCommand _unlinkFocusedNodeCommand;
        private readonly RelayCommand _saveBookmarkCommand;
        private readonly RelayCommand _addSelectionToActiveGroupCommand;
        private readonly RelayCommand _removeSelectionFromActiveGroupCommand;
        private readonly RelayCommand _deleteActiveGroupCommand;
        private readonly RelayCommand _restoreLatestBookmarkCommand;
        private readonly RelayCommand _undoLastCommand;
        private readonly RelayCommand _deleteFocusedNodeCommand;
        private readonly RelayCommand _attachDemoPanelCommand;
        private readonly RelayCommand _homeViewCommand;
        private readonly RelayCommand _centerFocusedNodeCommand;
        private readonly RelayCommand _frameSelectionCommand;
        private readonly RelayCommand _clearLinksCommand;
        private readonly RelayCommand _clearSelectionCommand;

        private string _lastActivitySummary = "Last Activity: app started";
        private bool _isCurrentStateSectionExpanded = true;
        private bool _isCommandSurfaceSectionExpanded = true;
        private bool _isSelectionFocusGroupExpanded = true;
        private bool _isLinksGroupExpanded = true;
        private bool _isGroupsGroupExpanded;
        private bool _isHistoryGroupExpanded;
        private bool _isViewGroupExpanded;
        private bool _isEditModesGroupExpanded;
        private bool _isMutationGroupExpanded;
        private bool _isDeveloperReadoutsSectionExpanded;
        private bool _isCapabilitiesSectionExpanded;

        public ObservableCollection<EngineCapability> Capabilities { get; } =
            new(EngineServices.Capabilities.GetAll());

        public ICommand FocusFirstNodeCommand => _focusFirstNodeCommand;
        public ICommand SelectFirstNodeCommand => _selectFirstNodeCommand;
        public ICommand FocusFirstPanelCommand => _focusFirstPanelCommand;
        public ICommand ActivateMoveModeCommand => _activateMoveModeCommand;
        public ICommand ActivateNavigateModeCommand => _activateNavigateModeCommand;
        public ICommand ActivateMarqueeModeCommand => _activateMarqueeModeCommand;
        public ICommand SelectFirstPanelCommand => _selectFirstPanelCommand;
        public ICommand CreateDemoNodeCommand => _createDemoNodeCommand;
        public ICommand NudgeFocusedLeftCommand => _nudgeFocusedLeftCommand;
        public ICommand NudgeFocusedRightCommand => _nudgeFocusedRightCommand;
        public ICommand NudgeFocusedUpCommand => _nudgeFocusedUpCommand;
        public ICommand NudgeFocusedDownCommand => _nudgeFocusedDownCommand;
        public ICommand NudgeFocusedForwardCommand => _nudgeFocusedForwardCommand;
        public ICommand NudgeFocusedBackCommand => _nudgeFocusedBackCommand;
        public ICommand GrowFocusedNodeCommand => _growFocusedNodeCommand;
        public ICommand ShrinkFocusedNodeCommand => _shrinkFocusedNodeCommand;
        public ICommand ConnectFocusedNodeCommand => _connectFocusedNodeCommand;
        public ICommand GroupSelectionCommand => _groupSelectionCommand;
        public ICommand UnlinkFocusedNodeCommand => _unlinkFocusedNodeCommand;
        public ICommand SaveBookmarkCommand => _saveBookmarkCommand;
        public ICommand AddSelectionToActiveGroupCommand => _addSelectionToActiveGroupCommand;
        public ICommand RemoveSelectionFromActiveGroupCommand => _removeSelectionFromActiveGroupCommand;
        public ICommand DeleteActiveGroupCommand => _deleteActiveGroupCommand;
        public ICommand RestoreLatestBookmarkCommand => _restoreLatestBookmarkCommand;
        public ICommand UndoLastCommand => _undoLastCommand;
        public ICommand DeleteFocusedNodeCommand => _deleteFocusedNodeCommand;
        public ICommand AttachDemoPanelCommand => _attachDemoPanelCommand;
        public ICommand HomeViewCommand => _homeViewCommand;
        public ICommand CenterFocusedNodeCommand => _centerFocusedNodeCommand;
        public ICommand FrameSelectionCommand => _frameSelectionCommand;
        public ICommand ClearLinksCommand => _clearLinksCommand;
        public ICommand ClearSelectionCommand => _clearSelectionCommand;

        public bool IsCurrentStateSectionExpanded
        {
            get => _isCurrentStateSectionExpanded;
            set => SetExpansionState(ref _isCurrentStateSectionExpanded, value);
        }

        public bool IsCommandSurfaceSectionExpanded
        {
            get => _isCommandSurfaceSectionExpanded;
            set => SetExpansionState(ref _isCommandSurfaceSectionExpanded, value);
        }

        public bool IsSelectionFocusGroupExpanded
        {
            get => _isSelectionFocusGroupExpanded;
            set => SetExpansionState(ref _isSelectionFocusGroupExpanded, value);
        }

        public bool IsLinksGroupExpanded
        {
            get => _isLinksGroupExpanded;
            set => SetExpansionState(ref _isLinksGroupExpanded, value);
        }

        public bool IsGroupsGroupExpanded
        {
            get => _isGroupsGroupExpanded;
            set => SetExpansionState(ref _isGroupsGroupExpanded, value);
        }

        public bool IsHistoryGroupExpanded
        {
            get => _isHistoryGroupExpanded;
            set => SetExpansionState(ref _isHistoryGroupExpanded, value);
        }

        public bool IsViewGroupExpanded
        {
            get => _isViewGroupExpanded;
            set => SetExpansionState(ref _isViewGroupExpanded, value);
        }

        public bool IsEditModesGroupExpanded
        {
            get => _isEditModesGroupExpanded;
            set => SetExpansionState(ref _isEditModesGroupExpanded, value);
        }

        public bool IsMutationGroupExpanded
        {
            get => _isMutationGroupExpanded;
            set => SetExpansionState(ref _isMutationGroupExpanded, value);
        }

        public bool IsDeveloperReadoutsSectionExpanded
        {
            get => _isDeveloperReadoutsSectionExpanded;
            set => SetExpansionState(ref _isDeveloperReadoutsSectionExpanded, value);
        }

        public bool IsCapabilitiesSectionExpanded
        {
            get => _isCapabilitiesSectionExpanded;
            set => SetExpansionState(ref _isCapabilitiesSectionExpanded, value);
        }

        public MainWindowViewModel()
        {
            _eventSubscriptions =
            [
                SubscribeRefresh(EventNames.CommandInvoked, "command activity"),
                SubscribeRefresh(EventNames.SceneChanged, "scene changed"),
                SubscribeRefresh(EventNames.FocusChanged, "focus changed"),
                SubscribeRefresh(EventNames.PanelFocusChanged, "panel focus changed"),
                SubscribeRefresh(EventNames.SelectionChanged, "selection changed"),
                SubscribeRefresh(EventNames.PanelAttachmentsChanged, "panel attachments changed"),
                SubscribeRefresh(EventNames.InteractionModeChanged, "interaction mode changed"),
                SubscribeRefresh(EventNames.GroupChanged, "group changed")
            ];

            _focusFirstNodeCommand = new RelayCommand(
                _ =>
                {
                    var firstNode = _shellScene.GetNodes().FirstOrDefault();
                    if (firstNode is not null)
                    {
                        SendCommand(
                            CommandNames.Focus,
                            new FocusEntityPayload(firstNode.Id.ToString()));
                    }
                },
                _ => _shellScene.GetNodes().Count > 0);

            _selectFirstNodeCommand = new RelayCommand(
                _ =>
                {
                    var firstNode = _shellScene.GetNodes().FirstOrDefault();
                    if (firstNode is not null)
                    {
                        SendCommand(
                            CommandNames.Select,
                            new SelectEntitiesPayload([firstNode.Id.ToString()]));
                    }
                },
                _ => _shellScene.GetNodes().Count > 0);

            _focusFirstPanelCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFirstPanelTarget() is { } panelTarget)
                    {
                        SendCommand(
                            CommandNames.FocusPanel,
                            new FocusPanelPayload(
                                panelTarget.NodeId.ToString(),
                                panelTarget.ViewRef));
                    }
                },
                _ => _shellScene.GetFirstPanelTarget() is not null);

            _selectFirstPanelCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFirstPanelTarget() is { } panelTarget)
                    {
                        SendCommand(
                            CommandNames.SelectPanel,
                            new SelectPanelPayload(
                                panelTarget.NodeId.ToString(),
                                panelTarget.ViewRef));
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
                var position = new Vector3(
                    MathF.Cos(angle) * radius,
                    MathF.Sin(angle) * radius,
                    0f);

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

            _connectFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var sourceNodeId = _shellScene.GetSelectedNodeIds()
                        .FirstOrDefault(nodeId => nodeId != focusedNode.Id);

                    if (sourceNodeId == default)
                    {
                        return;
                    }

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
                    if (focusedNode is null)
                    {
                        return false;
                    }

                    return _shellScene.GetSelectedNodeIds().Any(nodeId => nodeId != focusedNode.Id);
                });

            _unlinkFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var sourceNodeId = _shellScene.GetSelectedNodeIds()
                        .FirstOrDefault(nodeId => nodeId != focusedNode.Id);

                    if (sourceNodeId == default)
                    {
                        return;
                    }

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
                    if (focusedNode is null)
                    {
                        return false;
                    }

                    var sourceNodeId = _shellScene.GetSelectedNodeIds()
                        .FirstOrDefault(nodeId => nodeId != focusedNode.Id);

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
                    SendCommand(
                        CommandNames.GroupSelection,
                        new GroupSelectionPayload($"Group {(_shellScene.GetGroups().Count + 1)} ({selectedCount} nodes)"));
                },
                _ => _shellScene.GetSelectedNodeIds().Count >= 2);

            _addSelectionToActiveGroupCommand = new RelayCommand(
                _ =>
                {
                    var activeGroup = _shellScene.GetActiveGroup();
                    if (activeGroup is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.AddSelectionToGroup,
                        new GroupMembershipPayload(activeGroup.Id));
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
                    if (activeGroup is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.RemoveSelectionFromGroup,
                        new GroupMembershipPayload(activeGroup.Id));
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
                    SendCommand(
                        CommandNames.BookmarkSave,
                        new BookmarkSavePayload($"Bookmark {index}"));
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
                    var latest = _shellScene.GetBookmarks()
                        .OrderBy(bookmark => bookmark.Name, StringComparer.Ordinal)
                        .LastOrDefault();

                    if (latest is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.BookmarkRestore,
                        new BookmarkRestorePayload(latest.Name));
                },
                _ => _shellScene.GetBookmarks().Count > 0);

            _undoLastCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.Undo, null);
                },
                _ => EngineServices.Scene.CanUndo);

            _homeViewCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.HomeView, null);
                });

            _centerFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFocusedNode() is { } focusedNode)
                    {
                        SendCommand(
                            CommandNames.CenterOnNode,
                            new CenterOnNodePayload(focusedNode.Id.ToString()));
                    }
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _frameSelectionCommand = new RelayCommand(
                _ =>
                {
                    SendCommand(
                        CommandNames.FrameSelection,
                        new FrameSelectionPayload());
                },
                _ => _shellScene.GetSelectedNodeIds().Count > 0 || _shellScene.GetFocusedNode() is not null);

            _deleteFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var targetNodes = GetSelectionOrFocusTargetNodes();
                    if (targetNodes.Length == 0)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.DeleteEntities,
                        new DeleteEntitiesPayload(
                            targetNodes
                                .Select(node => node.Id.ToString())
                                .ToArray()));
                },
                _ => GetSelectionOrFocusTargetNodes().Length > 0);

            _attachDemoPanelCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var attachmentCount = _shellScene.GetPanelAttachments().Count;
                    var viewRef = $"demo.panel.{attachmentCount + 1}";

                    SendCommand(
                        CommandNames.AttachPanel,
                        new AttachPanelPayload(
                            focusedNode.Id.ToString(),
                            viewRef,
                            LocalOffset: new Vector3(0f, 0.18f, 0.15f),
                            Size: new Vector2(1.05f, 0.62f),
                            Anchor: "top",
                            IsVisible: true));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _clearLinksCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.ClearLinks, null);
                },
                _ => _shellScene.GetLinks().Count > 0);

            _clearSelectionCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.ClearSelection, null);
                },
                _ => _shellScene.GetSelectedNodeIds().Count > 0 || _shellScene.GetSelectedPanels().Count > 0);

            RefreshFromEngineState();
        }

        public string FocusSummary
        {
            get
            {
                if (_shellScene.GetFocusedPanel() is { } focusedPanel)
                {
                    return $"Focused Panel: {focusedPanel.ViewRef} on {focusedPanel.NodeId}";
                }

                var focusedNode = _shellScene.GetFocusedNode();
                return focusedNode is not null
                    ? $"Focused Node: {focusedNode.Id}"
                    : "Focused Node: none";
            }
        }

        public string SelectionSummary
        {
            get
            {
                var nodeCount = _shellScene.GetSelectedNodeIds().Count;
                var panelCount = _shellScene.GetSelectedPanels().Count;

                if (nodeCount == 0 && panelCount == 0)
                {
                    return "Selection: none";
                }

                return $"Selection: nodes={nodeCount}, panels={panelCount}";
            }
        }

        public string InteractionModeSummary
        {
            get
            {
                return _shellScene.GetInteractionMode() switch
                {
                    "marquee" => "Interaction Mode: Marquee — left-drag performs viewport box selection through the existing Focus/Select command flow.",
                    "move" => "Interaction Mode: Move — left-drag repositions the focused/selected node set with transient viewport preview and final command-path commit on release. Escape cancels the live preview without committing.",
                    _ => "Interaction Mode: Navigate — default hybrid navigation, click-selection, linking, and orbit posture remains active."
                };
            }
        }

        public string FocusedTransformSummary
        {
            get
            {
                var focusedNode = _shellScene.GetFocusedNode();
                return focusedNode is null
                    ? "Focused Transform: none"
                    : $"Focused Transform: pos={FormatVector3(focusedNode.Transform.Position)} scale={FormatVector3(focusedNode.Transform.Scale)} visual={focusedNode.VisualScale:0.##}";
            }
        }

        public string BookmarkSummary
        {
            get
            {
                var count = _shellScene.GetBookmarks().Count;
                return count == 0
                    ? "Bookmarks: none"
                    : $"Bookmarks: {count}";
            }
        }

        public string ViewSummary
        {
            get
            {
                return _shellScene.TryGetLastView(out var view)
                    ? $"View: yaw={view.Yaw:0.##}, pitch={view.Pitch:0.##}, distance={view.Distance:0.##}, target={FormatVector3(view.Target)}"
                    : "View: no renderer camera sample yet";
            }
        }

        public string ViewDetails
        {
            get
            {
                return _shellScene.TryGetLastView(out var view)
                    ? $"yaw={view.Yaw:0.###}\n" +
                      $"pitch={view.Pitch:0.###}\n" +
                      $"distance={view.Distance:0.###}\n" +
                      $"target={FormatVector3(view.Target)}"
                    : "No renderer camera sample has been published yet.";
            }
        }

        public string BookmarkDetails
        {
            get
            {
                var bookmarks = _shellScene.GetBookmarks();
                if (bookmarks.Count == 0)
                {
                    return "No bookmarks yet.";
                }

                return string.Join(
                    "\n",
                    bookmarks.Select(bookmark =>
                    {
                        var focus = bookmark.FocusedPanel is { } focusedPanel
                            ? $"focus=panel:{focusedPanel.ViewRef}@{focusedPanel.NodeId}"
                            : bookmark.FocusedNodeId is { } focusedNodeId
                                ? $"focus=node:{focusedNodeId}"
                                : "focus=none";

                        return
                            $"{bookmark.Name} => {focus} " +
                            $"selectedNodes={bookmark.SelectedNodeIds.Count} " +
                            $"selectedPanels={bookmark.SelectedPanels.Count}";
                    }));
            }
        }

        public string GroupSummary
        {
            get
            {
                var count = _shellScene.GetGroups().Count;
                var activeGroup = _shellScene.GetActiveGroup();
                return count == 0 ? "Groups: none" : activeGroup is null
                    ? $"Groups: {count}"
                    : $"Groups: {count} • active={activeGroup.Label}";
            }
        }

        public string GroupDetails
        {
            get
            {
                var groups = _shellScene.GetGroups();
                if (groups.Count == 0)
                {
                    return "No groups yet.";
                }

                var activeGroupId = _shellScene.GetActiveGroup()?.Id;
                return string.Join(
                    "\n",
                    groups.Select(group =>
                        $"{(string.Equals(group.Id, activeGroupId, StringComparison.Ordinal) ? "* " : string.Empty)}{group.Label} [{group.Id}] => {string.Join(", ", group.NodeIds.Select(id => id.ToString()))}"));
            }
        }

        public string PanelSummary
        {
            get
            {
                var count = _shellScene.GetPanelAttachments().Count;
                return count == 0
                    ? "Attached Panels: none"
                    : $"Attached Panels: {count}";
            }
        }

        public string LinkSummary
        {
            get
            {
                var count = _shellScene.GetLinks().Count;
                return count == 0
                    ? "Links: none"
                    : $"Links: {count}";
            }
        }

        public string LinkDetails
        {
            get
            {
                var links = _shellScene.GetLinks();
                if (links.Count == 0)
                {
                    return "No links yet.";
                }

                return string.Join(
                    "\n",
                    links.Select(link =>
                        $"{link.SourceId} -> {link.TargetId} kind={link.Kind} weight={link.Weight:0.##}"));
            }
        }

        public string FocusedTransformDetails
        {
            get
            {
                var focusedNode = _shellScene.GetFocusedNode();
                if (focusedNode is null)
                {
                    return "No focused node transform to inspect.";
                }

                return
                    $"id={focusedNode.Id}\n" +
                    $"label={focusedNode.Label}\n" +
                    $"position={FormatVector3(focusedNode.Transform.Position)}\n" +
                    $"rotation={FormatVector3(focusedNode.Transform.RotationEuler)}\n" +
                    $"scale={FormatVector3(focusedNode.Transform.Scale)}\n" +
                    $"visualScale={focusedNode.VisualScale:0.###}\n" +
                    $"phase={focusedNode.Phase:0.###}";
            }
        }

        public string InteractionSemanticsSummary
        {
            get
            {
                var focusedNode = _shellScene.GetFocusedNode();
                var selectedNodeIds = _shellScene.GetSelectedNodeIds();
                var selectedPanels = _shellScene.GetSelectedPanels();
                var activeGroup = _shellScene.GetActiveGroup();

                var linkSource = selectedNodeIds
                    .FirstOrDefault(nodeId => focusedNode is null || nodeId != focusedNode.Id);

                var linkSourceText = linkSource != default
                    ? linkSource.ToString()
                    : focusedNode is not null
                        ? focusedNode.Id.ToString()
                        : "none";

                var targetText = focusedNode is not null
                    ? focusedNode.Id.ToString()
                    : "clicked node";

                var activeGroupText = activeGroup is null
                    ? "none"
                    : $"{activeGroup.Label} ({activeGroup.NodeIds.Count} nodes)";

                return
                    $"Interaction: navigate-mode click=focus/select + left-drag=orbit • move-mode left-drag repositions the focused/selected node set with transient viewport preview and release-time `UpdateEntity` commit, while Escape cancels an active drag preview without committing • marquee-mode left-drag=box-select • shift preserves additive selection in selection flows • ctrl+click/double-click=link {linkSourceText} -> {targetText} in navigate mode • shell unlink removes the matching directed link • ctrl+z=undo • center/frame are explicit navigation tools over the view bridge • clear-links=shell tool • shell mutation group now includes first transform helpers (directional nudge across X/Y/Z + grow/shrink) over the existing update-entity path, and focused transform state is now surfaced in-shell for inspection • shell surface now reflects first-pass toolbar categories with collapsible session-local groups + secondary developer readouts" +
                    $"\nCurrent counts: selectedNodes={selectedNodeIds.Count}, selectedPanels={selectedPanels.Count} • activeGroup={activeGroupText} • viewCommands={(selectedNodeIds.Count > 0 || _shellScene.GetFocusedNode() is not null ? "ready" : "blocked")}";
            }
        }

        public string ActionReadinessSummary
        {
            get
            {
                return string.Join(
                    " • ",
                    [
                        $"focus-node={FormatReady(_focusFirstNodeCommand.CanExecute(null))}",
                        $"select-node={FormatReady(_selectFirstNodeCommand.CanExecute(null))}",
                        $"move-mode={FormatReady(_activateMoveModeCommand.CanExecute(null))}",
                        $"navigate-mode={FormatReady(_activateNavigateModeCommand.CanExecute(null))}",
                        $"marquee-mode={FormatReady(_activateMarqueeModeCommand.CanExecute(null))}",
                        $"focus-panel={FormatReady(_focusFirstPanelCommand.CanExecute(null))}",
                        $"select-panel={FormatReady(_selectFirstPanelCommand.CanExecute(null))}",
                        $"create-node={FormatReady(_createDemoNodeCommand.CanExecute(null))}",
                        $"nudge-left={FormatReady(_nudgeFocusedLeftCommand.CanExecute(null))}",
                        $"nudge-right={FormatReady(_nudgeFocusedRightCommand.CanExecute(null))}",
                        $"nudge-up={FormatReady(_nudgeFocusedUpCommand.CanExecute(null))}",
                        $"nudge-down={FormatReady(_nudgeFocusedDownCommand.CanExecute(null))}",
                        $"nudge-forward={FormatReady(_nudgeFocusedForwardCommand.CanExecute(null))}",
                        $"nudge-back={FormatReady(_nudgeFocusedBackCommand.CanExecute(null))}",
                        $"grow={FormatReady(_growFocusedNodeCommand.CanExecute(null))}",
                        $"shrink={FormatReady(_shrinkFocusedNodeCommand.CanExecute(null))}",
                        $"connect={FormatReady(_connectFocusedNodeCommand.CanExecute(null))}",
                        $"unlink={FormatReady(_unlinkFocusedNodeCommand.CanExecute(null))}",
                        $"group={FormatReady(_groupSelectionCommand.CanExecute(null))}",
                        $"add-to-group={FormatReady(_addSelectionToActiveGroupCommand.CanExecute(null))}",
                        $"remove-from-group={FormatReady(_removeSelectionFromActiveGroupCommand.CanExecute(null))}",
                        $"delete-group={FormatReady(_deleteActiveGroupCommand.CanExecute(null))}",
                        $"save-bookmark={FormatReady(_saveBookmarkCommand.CanExecute(null))}",
                        $"restore-bookmark={FormatReady(_restoreLatestBookmarkCommand.CanExecute(null))}",
                        $"undo={FormatReady(_undoLastCommand.CanExecute(null))}",
                        $"home={FormatReady(_homeViewCommand.CanExecute(null))}",
                        $"center={FormatReady(_centerFocusedNodeCommand.CanExecute(null))}",
                        $"frame={FormatReady(_frameSelectionCommand.CanExecute(null))}",
                        $"clear-links={FormatReady(_clearLinksCommand.CanExecute(null))}",
                        $"delete={FormatReady(_deleteFocusedNodeCommand.CanExecute(null))}",
                        $"attach-panel={FormatReady(_attachDemoPanelCommand.CanExecute(null))}",
                        $"clear={FormatReady(_clearSelectionCommand.CanExecute(null))}"
                    ]);
            }
        }

        public string PaneStructureSummary =>
            "2D Pane Layout: " +
            $"current={FormatExpanded(IsCurrentStateSectionExpanded)} • " +
            $"commands={FormatExpanded(IsCommandSurfaceSectionExpanded)} • " +
            $"developer={FormatExpanded(IsDeveloperReadoutsSectionExpanded)} • " +
            $"capabilities={FormatExpanded(IsCapabilitiesSectionExpanded)}" +
            "\nCommand Groups: " +
            $"selection={FormatExpanded(IsSelectionFocusGroupExpanded)} • " +
            $"links={FormatExpanded(IsLinksGroupExpanded)} • " +
            $"groups={FormatExpanded(IsGroupsGroupExpanded)} • " +
            $"history={FormatExpanded(IsHistoryGroupExpanded)} • " +
            $"view={FormatExpanded(IsViewGroupExpanded)} • " +
            $"editModes={FormatExpanded(IsEditModesGroupExpanded)} • " +
            $"mutation={FormatExpanded(IsMutationGroupExpanded)}";

        public string LastActivitySummary => _lastActivitySummary;

        public string PanelDetails
        {
            get
            {
                var snapshot = _shellScene.GetSnapshot();
                if (snapshot.PanelAttachments is null || snapshot.PanelAttachments.Count == 0)
                {
                    return "No panel attachments yet.";
                }

                var selectedPanels = snapshot.SelectedPanels?
                    .ToHashSet()
                    ?? [];

                return string.Join(
                    "\n",
                    snapshot.PanelAttachments
                        .OrderBy(x => x.Key.ToString(), StringComparer.Ordinal)
                        .Select(x =>
                        {
                            var isFocused = snapshot.FocusedPanel is { } focusedPanel &&
                                            focusedPanel.NodeId == x.Key &&
                                            string.Equals(focusedPanel.ViewRef, x.Value.ViewRef, StringComparison.Ordinal);
                            var isSelected = selectedPanels.Contains(new PanelTarget(x.Key, x.Value.ViewRef));

                            return
                                $"{x.Key} → {x.Value.ViewRef} " +
                                $"anchor={x.Value.Anchor} " +
                                $"offset=({x.Value.LocalOffset.X:0.##},{x.Value.LocalOffset.Y:0.##},{x.Value.LocalOffset.Z:0.##}) " +
                                $"size=({x.Value.Size.X:0.##},{x.Value.Size.Y:0.##}) " +
                                $"visible={x.Value.IsVisible} " +
                                $"focused={isFocused} " +
                                $"selected={isSelected}";
                        }));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Dispose()
        {
            foreach (var subscription in _eventSubscriptions)
            {
                subscription.Dispose();
            }
        }

        private IDisposable SubscribeRefresh(string eventName, string activityLabel)
        {
            return EngineServices.EventBus.Subscribe(eventName, envelope =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateLastActivity(eventName, activityLabel, envelope);
                    RefreshFromEngineState();
                });

                return true;
            });
        }

        private void SendCommand<TPayload>(string commandName, TPayload payload)
        {
            var envelope = new Envelope
            {
                V = "1.0",
                Id = Guid.NewGuid(),
                Ts = DateTimeOffset.UtcNow,
                Type = EnvelopeType.Command,
                Name = commandName,
                Payload = payload is null
                    ? null
                    : JsonSerializer.SerializeToElement(payload, JsonOptions),
                CorrelationId = null
            };

            EngineServices.CommandBus.Send(envelope);
        }

        private RelayCommand CreateSelectionOrFocusTransformCommand(Vector3 positionDelta, float scaleMultiplier)
        {
            return new RelayCommand(
                _ =>
                {
                    var targetNodes = GetSelectionOrFocusTargetNodes();
                    if (targetNodes.Length == 0)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.UpdateEntities,
                        new UpdateEntitiesPayload(
                            targetNodes
                                .Select(node =>
                                {
                                    var currentScale = node.Transform.Scale;
                                    var nextScale = new Vector3(
                                        Math.Clamp(currentScale.X * scaleMultiplier, 0.15f, 2.5f),
                                        Math.Clamp(currentScale.Y * scaleMultiplier, 0.15f, 2.5f),
                                        Math.Clamp(currentScale.Z * scaleMultiplier, 0.15f, 2.5f));
                                    var nextVisualScale = Math.Clamp(node.VisualScale * scaleMultiplier, 0.15f, 2.5f);

                                    return new UpdateEntityPayload(
                                        node.Id.ToString(),
                                        node.Label,
                                        node.Transform.Position + positionDelta,
                                        node.Transform.RotationEuler,
                                        nextScale,
                                        nextVisualScale,
                                        node.Phase);
                                })
                                .ToArray()));
                },
                _ => GetSelectionOrFocusTargetNodes().Length > 0);
        }

        private SceneNode[] GetSelectionOrFocusTargetNodes()
        {
            var snapshot = _shellScene.GetSnapshot();
            var selectedNodeIds = snapshot.SelectedNodeIds?.ToHashSet() ?? [];

            if (selectedNodeIds.Count > 0)
            {
                return snapshot.Nodes
                    .Where(node => selectedNodeIds.Contains(node.Id))
                    .OrderBy(node => node.Label, StringComparer.Ordinal)
                    .ThenBy(node => node.Id.ToString(), StringComparer.Ordinal)
                    .ToArray();
            }

            if (snapshot.FocusedNodeId is { } focusedNodeId)
            {
                return snapshot.Nodes.Where(node => node.Id == focusedNodeId).ToArray();
            }

            return [];
        }

        private void RefreshFromEngineState()
        {
            RefreshCapabilities();
            RaiseSceneStateChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void RefreshCapabilities()
        {
            var latest = EngineServices.Capabilities.GetAll().ToArray();
            Capabilities.Clear();

            foreach (var capability in latest)
            {
                Capabilities.Add(capability);
            }
        }

        private void UpdateLastActivity(string eventName, string activityLabel, Envelope envelope)
        {
            var detail = TryDescribeActivity(envelope);
            _lastActivitySummary = string.IsNullOrWhiteSpace(detail)
                ? $"Last Activity: {activityLabel} ({eventName}) @ {envelope.Ts:HH:mm:ss}"
                : $"Last Activity: {activityLabel} ({eventName}: {detail}) @ {envelope.Ts:HH:mm:ss}";
            OnPropertyChanged(nameof(LastActivitySummary));
        }

        private static string? TryDescribeActivity(Envelope envelope)
        {
            if (envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
            {
                return envelope.Name;
            }

            if (string.Equals(envelope.Name, EventNames.CommandInvoked, StringComparison.Ordinal))
            {
                if (TryGetString(payload, "commandName", out var commandName))
                {
                    return commandName;
                }

                return envelope.Name;
            }

            if (TryGetString(payload, "reason", out var reason))
            {
                return reason;
            }

            if (TryGetString(payload, "label", out var label))
            {
                return label;
            }

            if (TryGetString(payload, "bookmarkName", out var bookmarkName))
            {
                return bookmarkName;
            }

            if (TryGetString(payload, "focusedNodeId", out var focusedNodeId))
            {
                return focusedNodeId;
            }

            if (TryGetString(payload, "viewRef", out var viewRef))
            {
                return viewRef;
            }

            if (TryGetString(payload, "mode", out var mode))
            {
                return mode;
            }

            return envelope.Name;
        }

        private static bool TryGetString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;

            if (!element.TryGetProperty(propertyName, out var propertyValue))
            {
                return false;
            }

            if (propertyValue.ValueKind == JsonValueKind.String)
            {
                var text = propertyValue.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    value = text;
                    return true;
                }
            }

            return false;
        }

        private void RaiseSceneStateChanged()
        {
            OnPropertyChanged(nameof(FocusSummary));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(BookmarkSummary));
            OnPropertyChanged(nameof(ViewSummary));
            OnPropertyChanged(nameof(ViewDetails));
            OnPropertyChanged(nameof(FocusedTransformSummary));
            OnPropertyChanged(nameof(FocusedTransformDetails));
            OnPropertyChanged(nameof(InteractionModeSummary));
            OnPropertyChanged(nameof(BookmarkDetails));
            OnPropertyChanged(nameof(GroupSummary));
            OnPropertyChanged(nameof(GroupDetails));
            OnPropertyChanged(nameof(LinkSummary));
            OnPropertyChanged(nameof(InteractionSemanticsSummary));
            OnPropertyChanged(nameof(LinkDetails));
            OnPropertyChanged(nameof(PanelSummary));
            OnPropertyChanged(nameof(ActionReadinessSummary));
            OnPropertyChanged(nameof(LastActivitySummary));
            OnPropertyChanged(nameof(PanelDetails));
        }

        private void RaiseCommandCanExecuteChanged()
        {
            _focusFirstNodeCommand.RaiseCanExecuteChanged();
            _activateNavigateModeCommand.RaiseCanExecuteChanged();
            _activateMoveModeCommand.RaiseCanExecuteChanged();
            _activateMarqueeModeCommand.RaiseCanExecuteChanged();
            _selectFirstNodeCommand.RaiseCanExecuteChanged();
            _focusFirstPanelCommand.RaiseCanExecuteChanged();
            _selectFirstPanelCommand.RaiseCanExecuteChanged();
            _createDemoNodeCommand.RaiseCanExecuteChanged();
            _nudgeFocusedLeftCommand.RaiseCanExecuteChanged();
            _nudgeFocusedRightCommand.RaiseCanExecuteChanged();
            _nudgeFocusedUpCommand.RaiseCanExecuteChanged();
            _nudgeFocusedDownCommand.RaiseCanExecuteChanged();
            _nudgeFocusedForwardCommand.RaiseCanExecuteChanged();
            _nudgeFocusedBackCommand.RaiseCanExecuteChanged();
            _growFocusedNodeCommand.RaiseCanExecuteChanged();
            _shrinkFocusedNodeCommand.RaiseCanExecuteChanged();
            _connectFocusedNodeCommand.RaiseCanExecuteChanged();
            _unlinkFocusedNodeCommand.RaiseCanExecuteChanged();
            _groupSelectionCommand.RaiseCanExecuteChanged();
            _addSelectionToActiveGroupCommand.RaiseCanExecuteChanged();
            _removeSelectionFromActiveGroupCommand.RaiseCanExecuteChanged();
            _deleteActiveGroupCommand.RaiseCanExecuteChanged();
            _saveBookmarkCommand.RaiseCanExecuteChanged();
            _restoreLatestBookmarkCommand.RaiseCanExecuteChanged();
            _undoLastCommand.RaiseCanExecuteChanged();
            _homeViewCommand.RaiseCanExecuteChanged();
            _centerFocusedNodeCommand.RaiseCanExecuteChanged();
            _frameSelectionCommand.RaiseCanExecuteChanged();
            _deleteFocusedNodeCommand.RaiseCanExecuteChanged();
            _attachDemoPanelCommand.RaiseCanExecuteChanged();
            _clearLinksCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
        }

        private void SetExpansionState(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            if (propertyName is not null)
            {
                OnPropertyChanged(propertyName);
            }

            OnPropertyChanged(nameof(PaneStructureSummary));
        }

        private static string FormatReady(bool ready) => ready ? "ready" : "blocked";
        private static string FormatVector3(Vector3 value) =>
            $"({value.X:0.##}, {value.Y:0.##}, {value.Z:0.##})";

        private static string FormatExpanded(bool expanded) => expanded ? "open" : "collapsed";

        private bool IsInteractionMode(string mode) =>
            string.Equals(
                _shellScene.GetInteractionMode(),
                mode,
                StringComparison.Ordinal);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName is not null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;
            private readonly Func<object?, bool>? _canExecute;

            public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

            public void Execute(object? parameter) => _execute(parameter);

            public void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
