using System;
using System.Linq;
using System.Numerics;
using Constellate.Core.Messaging;
using Constellate.Core.Scene;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing user-facing readouts/summaries
/// (focus/selection/view/history/groups/links/panels/appearance) that were previously
/// defined inline in MainWindow.axaml.cs. This is a mechanical extraction only.
/// </summary>
public sealed partial class MainWindowViewModel
{
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

    public string FocusOriginSummary
    {
        get
        {
            var origin = _shellScene.GetFocusOrigin();
            return $"Focus Origin: {FormatFocusOrigin(origin)}";
        }
    }

    public string EnteredNodeSummary
    {
        get
        {
            var enteredId = _shellScene.GetEnteredNodeId();
            return enteredId is null
                ? "Entered Node: none"
                : $"Entered Node: {enteredId}";
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

    public string InteractionModeBadgeLabel =>
        _shellScene.GetInteractionMode() switch
        {
            "marquee" => "Marquee",
            "move" => "Move",
            _ => "Navigate"
        };

    public string FocusedTransformSummary
    {
        get
        {
            var focusedNode = _shellScene.GetFocusedNode();
            var appearance = focusedNode?.Appearance ?? NodeAppearance.Default;
            return focusedNode is null
                ? "Focused Transform: none"
                : $"Focused Transform: pos={FormatVector3(focusedNode.Transform.Position)} scale={FormatVector3(focusedNode.Transform.Scale)} visual={focusedNode.VisualScale:0.##} primitive={appearance.Primitive} fill={appearance.FillColor}";
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
            var attachments = _shellScene.GetPanelAttachments();
            var count = attachments.Count;
            var paneletteCount = attachments.Values.Count(attachment => (attachment.Semantics ?? PanelSurfaceSemantics.FromViewRef(attachment.ViewRef)).IsPanelette);
            var metadataCount = attachments.Values.Count(attachment => (attachment.Semantics ?? PanelSurfaceSemantics.FromViewRef(attachment.ViewRef)).IsMetadataPanelette);
            var labelCount = attachments.Values.Count(attachment => (attachment.Semantics ?? PanelSurfaceSemantics.FromViewRef(attachment.ViewRef)).IsLabelPanelette);
            var commandSurfaceCount = attachments.Values.Count(attachment => attachment.CommandSurface is { HasCommands: true });

            if (count == 0)
            {
                return "Attached Panels: none";
            }

            return paneletteCount > 0
                ? $"Attached Panels: {count} • panelettes={paneletteCount} • metadata={metadataCount} • labels={labelCount} • command-surfaces={commandSurfaceCount}"
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

            var appearance = focusedNode.Appearance ?? NodeAppearance.Default;

            return
                $"id={focusedNode.Id}\n" +
                $"label={focusedNode.Label}\n" +
                $"position={FormatVector3(focusedNode.Transform.Position)}\n" +
                $"rotation={FormatVector3(focusedNode.Transform.RotationEuler)}\n" +
                $"scale={FormatVector3(focusedNode.Transform.Scale)}\n" +
                $"appearance={appearance.Primitive} fill={appearance.FillColor} outline={appearance.OutlineColor} opacity={appearance.Opacity:0.##}\n" +
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
                $"Interaction: navigate-mode click=focus/select + left-drag=orbit • move-mode left-drag repositions the focused/selected node set with transient viewport preview and release-time `UpdateEntity` commit, while Escape cancels an active drag preview without committing • marquee-mode left-drag=box-select • shift preserves additive selection in selection flows • ctrl+click/double-click=link {linkSourceText} -> {targetText} in navigate mode • shell unlink removes the matching directed link • ctrl+z=undo • center/frame are explicit navigation tools over the view bridge • clear-links=shell tool • shell mutation group now includes first transform helpers (directional nudge across X/Y/Z + grow/shrink) over the existing update-entity path, and focused transform state is now surfaced in-shell for inspection • attached `panelette.*` surfaces now expose the first explicit Tier 1 content classes: metadata cards and compact label surfaces • shell surface now reflects first-pass toolbar categories with collapsible session-local groups + secondary developer readouts" +
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
                    $"primitive-triangle={FormatReady(_applyTrianglePrimitiveCommand.CanExecute(null))}",
                    $"primitive-square={FormatReady(_applySquarePrimitiveCommand.CanExecute(null))}",
                    $"primitive-diamond={FormatReady(_applyDiamondPrimitiveCommand.CanExecute(null))}",
                    $"primitive-pentagon={FormatReady(_applyPentagonPrimitiveCommand.CanExecute(null))}",
                    $"primitive-hexagon={FormatReady(_applyHexagonPrimitiveCommand.CanExecute(null))}",
                    $"primitive-cube={FormatReady(_applyCubePrimitiveCommand.CanExecute(null))}",
                    $"primitive-tetrahedron={FormatReady(_applyTetrahedronPrimitiveCommand.CanExecute(null))}",
                    $"primitive-sphere={FormatReady(_applySpherePrimitiveCommand.CanExecute(null))}",
                    $"primitive-box={FormatReady(_applyBoxPrimitiveCommand.CanExecute(null))}",
                    $"appearance-blue={FormatReady(_applyBlueAppearanceCommand.CanExecute(null))}",
                    $"appearance-violet={FormatReady(_applyVioletAppearanceCommand.CanExecute(null))}",
                    $"appearance-green={FormatReady(_applyGreenAppearanceCommand.CanExecute(null))}",
                    $"opacity-up={FormatReady(_increaseOpacityCommand.CanExecute(null))}",
                    $"opacity-down={FormatReady(_decreaseOpacityCommand.CanExecute(null))}",
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
                    $"attach-label={FormatReady(_attachLabelPaneletteCommand.CanExecute(null))}",
                    $"clear={FormatReady(_clearSelectionCommand.CanExecute(null))}"
                ]);
        }
    }

    public string PaneStructureSummary =>
        $"Parent Panes: {(ParentPaneModels.Count == 0
            ? "none"
            : string.Join(", ", ParentPaneModels.Select(parent =>
                $"{NormalizeHostId(parent.HostId)}:{parent.Id}{(parent.IsMinimized ? "(min)" : string.Empty)}")))}" +
        $" • any-visible={FormatExpanded(ParentPaneModels.Any(parent => !parent.IsMinimized))}" +
        "\n2D Pane Layout: " +
        $"current={FormatExpanded(IsCurrentStateSectionExpanded)} • " +
        $"commands={FormatExpanded(IsCommandSurfaceSectionExpanded)} • " +
        $"settings={FormatExpanded(IsSettingsSectionExpanded)} • " +
        $"developer={FormatExpanded(IsDeveloperReadoutsSectionExpanded)} • " +
        $"capabilities={FormatExpanded(IsCapabilitiesSectionExpanded)}" +
        "\nCommand Groups: " +
        $"selection={FormatExpanded(IsSelectionFocusGroupExpanded)} • " +
        $"links={FormatExpanded(IsLinksGroupExpanded)} • " +
        $"groups={FormatExpanded(IsGroupsGroupExpanded)} • " +
        $"history={FormatExpanded(IsHistoryGroupExpanded)} • " +
        $"view={FormatExpanded(IsViewGroupExpanded)} • " +
        $"editModes={FormatExpanded(IsEditModesGroupExpanded)} • " +
        $"mutation={FormatExpanded(IsMutationGroupExpanded)} • " +
        $"appearance={FormatExpanded(IsAppearanceGroupExpanded)}" +
        "\nChild Panes: " +
        (ChildPanesOrdered.Count == 0
            ? "none"
            : string.Join(", ", ChildPanesOrdered.Select(p => p.Id)));

    public string PaneCatalogDefinitionDetails
    {
        get
        {
            if (SeededPaneDefinitions.Count == 0)
            {
                return "No pane definitions available.";
            }

            return string.Join(
                "\n",
                SeededPaneDefinitions
                    .OrderBy(definition => definition.DisplayLabel, StringComparer.Ordinal)
                    .Select(definition =>
                    {
                        var tags = definition.Tags is { Count: > 0 }
                            ? string.Join(", ", definition.Tags)
                            : "none";

                        return $"{definition.DisplayLabel} [{definition.PaneDefinitionId}] · kind={definition.DefinitionKind} · seeded={definition.IsSeeded} · elements={definition.Elements.Count} · tags={tags}";
                    }));
        }
    }

    public string WorkspaceCatalogDetails
    {
        get
        {
            if (SeededPaneWorkspaces.Count == 0)
            {
                return "No workspace definitions available.";
            }

            return string.Join(
                "\n\n",
                SeededPaneWorkspaces
                    .OrderBy(workspace => workspace.DisplayLabel, StringComparer.Ordinal)
                    .Select(workspace =>
                    {
                        var tags = workspace.Tags is { Count: > 0 }
                            ? $" · tags={string.Join(", ", workspace.Tags)}"
                            : string.Empty;
                        var memberLines = workspace.Members.Count == 0
                            ? " - no members"
                            : string.Join(
                                "\n",
                                workspace.Members
                                    .OrderBy(member => member.Ordinal)
                                    .Select(member =>
                                        $" - {ResolveSeededPaneDefinitionLabel(member.PaneDefinitionId)} @ {member.HostHint} lane={member.LaneIndex} slide={member.SlideIndex}"));

                        return $"{workspace.DisplayLabel} [{workspace.WorkspaceId}] · members={workspace.Members.Count}{tags}\n{memberLines}";
                    }));
        }
    }

    public string LastActivitySummary => _lastActivitySummary;

    public string CommandHistorySummary =>
        _commandHistory.Count == 0
            ? "No recent command history."
            : string.Join("\n", _commandHistory);

    public string NavigationHistorySummary
    {
        get
        {
            var views = _shellScene.GetViewHistory();
            if (views.Count == 0)
            {
                return "Navigation history: none";
            }

            var lines = views
                .Select((v, index) =>
                    $"{index + 1}: yaw={v.Yaw:0.##}, pitch={v.Pitch:0.##}, dist={v.Distance:0.##}, target={FormatVector3(v.Target)}");

            return "Navigation history (oldest → newest):\n" + string.Join("\n", lines);
        }
    }

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
                        var semantics = x.Value.Semantics ?? PanelSurfaceSemantics.FromViewRef(x.Value.ViewRef);
                        var isFocused = snapshot.FocusedPanel is { } focusedPanel &&
                                        focusedPanel.NodeId == x.Key &&
                                        string.Equals(focusedPanel.ViewRef, x.Value.ViewRef, StringComparison.Ordinal);
                        var isSelected = selectedPanels.Contains(new PanelTarget(x.Key, x.Value.ViewRef));
                        var commandSurfaceSummary = x.Value.CommandSurface is { } commandSurface
                            ? $" commandSurface={commandSurface.DescribeSummary()}"
                            : string.Empty;

                        return
                            $"{x.Key} → {x.Value.ViewRef} " +
                            $"kind={semantics.DescribeKind()} " +
                            $"tier={semantics.PaneletteTier} " +
                            $"anchor={x.Value.Anchor} " +
                            $"offset=({x.Value.LocalOffset.X:0.##},{x.Value.LocalOffset.Y:0.##},{x.Value.LocalOffset.Z:0.##}) " +
                            $"size=({x.Value.Size.X:0.##},{x.Value.Size.Y:0.##}) " +
                            $"visible={x.Value.IsVisible} " +
                            $"focused={isFocused} " +
                            $"selected={isSelected}" +
                            commandSurfaceSummary;
                    }));
        }
    }
}
