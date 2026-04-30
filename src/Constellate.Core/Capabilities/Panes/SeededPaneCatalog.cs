using System;
using System.Collections.Generic;
using System.Linq;

namespace Constellate.Core.Capabilities.Panes;

public interface IPaneCatalog
{
    IReadOnlyList<PaneCapabilityDescriptor> GetCapabilityDescriptors();
    IReadOnlyList<PaneDefinitionDescriptor> GetPaneDefinitions();
    IReadOnlyList<PaneWorkspaceDescriptor> GetWorkspaceDefinitions();
    PaneCapabilityDescriptor? FindCapability(string capabilityId);
    PaneDefinitionDescriptor? FindPaneDefinition(string paneDefinitionId);
    PaneWorkspaceDescriptor? FindWorkspaceDefinition(string workspaceId);
}

public sealed class SeededPaneCatalog : IPaneCatalog
{
    private static readonly PaneCapabilityDescriptor[] CapabilityDescriptors =
    [
        new(
            "engine.command.home_view",
            "Home View",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Return the current world camera to its canonical home view.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:HomeView"),

        new(
            "engine.command.frame_selection",
            "Frame Selection",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Frame the active world selection in the viewport.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:FrameSelection"),

        new(
            "engine.command.center_focused_node",
            "Center Focused Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Center the viewport target on the currently focused node.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:CenterOnNode"),

        new(
            "engine.command.clear_selection",
            "Clear Selection",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Clear the current node and panel selection state.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ClearSelection"),

        new(
            "engine.command.activate_navigate_mode",
            "Navigate Mode",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Switch interaction posture to the default navigate mode.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:SetInteractionMode:navigate"),

        new(
            "engine.command.activate_move_mode",
            "Move Mode",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Switch interaction posture to move mode for transform-oriented manipulation.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:SetInteractionMode:move"),

        new(
            "engine.command.activate_marquee_mode",
            "Marquee Mode",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Switch interaction posture to marquee selection mode.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:SetInteractionMode:marquee"),

        new(
            "engine.command.create_child_pane",
            "Create Child Pane",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentParentWorkspace,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Create a new child pane inside the current parent-pane workspace.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:CreateChildPane"),

        new(
            "engine.state.current_selection",
            "Current Selection",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Card, PaneProjectionForm.Table, PaneProjectionForm.Badge],
            "Read-only selector for the engine's active world selection.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:CurrentSelection"),

        new(
            "engine.state.focus_summary",
            "Focus Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Card, PaneProjectionForm.Table, PaneProjectionForm.Badge],
            "Read-only summary of the currently focused node or panel target.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:FocusSummary"),

        new(
            "engine.state.interaction_mode",
            "Interaction Mode",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Card, PaneProjectionForm.Badge],
            "Read-only summary of the current interaction posture.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:InteractionMode"),

        new(
            "engine.state.group_summary",
            "Group Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Card, PaneProjectionForm.Table],
            "Read-only summary of active group state and group count.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:GroupSummary"),

        new(
            "engine.state.link_summary",
            "Link Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Card, PaneProjectionForm.Table],
            "Read-only summary of current relationship/link state.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:LinkSummary"),

        new(
            "runtime.feed.active_operations",
            "Active Operations",
            PaneCapabilityKind.RuntimeFeed,
            PaneCapabilitySourceDomain.RuntimeStream,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.ContinuousStream,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Console, PaneProjectionForm.List, PaneProjectionForm.Table],
            "Streaming operational feed for active automation, provider work, and runtime tasks.",
            OwnerKind: "Engine",
            BindingTargetRef: "RuntimeFeed:ActiveOperations"),

        new(
            "archive.view.action_invocations",
            "Action Invocation History",
            PaneCapabilityKind.ArchiveView,
            PaneCapabilitySourceDomain.ArchiveHistory,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.HistoricalArchive,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.List, PaneProjectionForm.Table],
            "Historical archive of action invocations and outcomes.",
            OwnerKind: "Engine",
            BindingTargetRef: "ArchiveView:ActionInvocations"),

        new(
            "browser.command_catalog",
            "Command Catalog",
            PaneCapabilityKind.Browser,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.List, PaneProjectionForm.Table, PaneProjectionForm.Tree],
            "Browse catalog-discoverable commands and related pane capabilities.",
            OwnerKind: "Engine",
            BindingTargetRef: "Browser:CommandCatalog")
    ];

    private static readonly PaneDefinitionDescriptor[] PaneDefinitions =
    [
        new(
            "pane.selection_inspector",
            "Selection Inspector",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "selection.header",
                    PaneElementKind.DefinitionHeader,
                    "Selection Inspector"),
                new(
                    "selection.focus_summary",
                    PaneElementKind.LabelValueField,
                    "Focus Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.focus_summary")),
                new(
                    "selection.current_selection",
                    PaneElementKind.LabelValueField,
                    "Current Selection",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.current_selection")),
                new(
                    "selection.group_summary",
                    PaneElementKind.LabelValueField,
                    "Group Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.group_summary")),
                new(
                    "selection.frame_selection",
                    PaneElementKind.Button,
                    "Frame Selection",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.frame_selection")),
                new(
                    "selection.clear_selection",
                    PaneElementKind.Button,
                    "Clear Selection",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.clear_selection"))
            ],
            "Seeded inspector pane for focus, selection, and basic selection-oriented actions.",
            ["seeded", "inspector", "selection"]),

        new(
            "pane.view_controls",
            "View Controls",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "view.header",
                    PaneElementKind.DefinitionHeader,
                    "View Controls"),
                new(
                    "view.interaction_mode",
                    PaneElementKind.LabelValueField,
                    "Interaction Mode",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.interaction_mode")),
                new(
                    "view.home_view",
                    PaneElementKind.Button,
                    "Home View",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.home_view")),
                new(
                    "view.center_focused_node",
                    PaneElementKind.Button,
                    "Center Focused Node",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.center_focused_node")),
                new(
                    "view.frame_selection",
                    PaneElementKind.Button,
                    "Frame Selection",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.frame_selection")),
                new(
                    "view.navigate_mode",
                    PaneElementKind.Button,
                    "Navigate Mode",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.activate_navigate_mode")),
                new(
                    "view.move_mode",
                    PaneElementKind.Button,
                    "Move Mode",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.activate_move_mode")),
                new(
                    "view.marquee_mode",
                    PaneElementKind.Button,
                    "Marquee Mode",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.activate_marquee_mode"))
            ],
            "Seeded view/navigation pane derived from current hardcoded viewport and interaction commands.",
            ["seeded", "view", "commands"]),

        new(
            "pane.scene_overview",
            "Scene Overview",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "scene.header",
                    PaneElementKind.DefinitionHeader,
                    "Scene Overview"),
                new(
                    "scene.focus_summary",
                    PaneElementKind.LabelValueField,
                    "Focus Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.focus_summary")),
                new(
                    "scene.interaction_mode",
                    PaneElementKind.LabelValueField,
                    "Interaction Mode",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.interaction_mode")),
                new(
                    "scene.group_summary",
                    PaneElementKind.LabelValueField,
                    "Group Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.group_summary")),
                new(
                    "scene.link_summary",
                    PaneElementKind.LabelValueField,
                    "Link Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.link_summary"))
            ],
            "Seeded overview pane derived from current hardcoded scene readouts and summaries.",
            ["seeded", "inspector", "scene"]),

        new(
            "pane.runtime_activity",
            "Runtime Activity",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "runtime.header",
                    PaneElementKind.DefinitionHeader,
                    "Runtime Activity"),
                new(
                    "runtime.active_operations",
                    PaneElementKind.RuntimeActivityPanel,
                    "Active Operations",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.RuntimeFeed,
                        "runtime.feed.active_operations")),
                new(
                    "runtime.history",
                    PaneElementKind.ArchiveBrowser,
                    "Invocation History",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.ArchiveView,
                        "archive.view.action_invocations"))
            ],
            "Seeded monitor pane for current operational activity and recent history.",
            ["seeded", "monitor", "runtime"]),

        new(
            "pane.command_browser",
            "Command Browser",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "command_browser.header",
                    PaneElementKind.DefinitionHeader,
                    "Command Browser"),
                new(
                    "command_browser.catalog",
                    PaneElementKind.CommandBrowser,
                    "Available Commands",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "browser.command_catalog")),
                new(
                    "command_browser.create_child_pane",
                    PaneElementKind.Button,
                    "Create Child Pane",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "engine.command.create_child_pane"))
            ],
            "Seeded browser pane for catalog-backed command discovery and workspace actions.",
            ["seeded", "browser", "commands"])
    ];

    private static readonly PaneWorkspaceDescriptor[] WorkspaceDefinitions =
    [
        new(
            "workspace.seeded.default_toolset",
            "Default Toolset",
            true,
            [
                new("workspace.member.view_controls", "pane.view_controls", 0, "left"),
                new("workspace.member.selection_inspector", "pane.selection_inspector", 1, "right"),
                new("workspace.member.scene_overview", "pane.scene_overview", 2, "top"),
                new("workspace.member.runtime_activity", "pane.runtime_activity", 3, "bottom"),
                new("workspace.member.command_browser", "pane.command_browser", 4, "left")
            ],
            "Seeded workspace arrangement for the first catalog-backed pane toolset derived from current useful hardcoded behavior.",
            ["seeded", "workspace", "default"])
    ];

    public IReadOnlyList<PaneCapabilityDescriptor> GetCapabilityDescriptors() => CapabilityDescriptors;

    public IReadOnlyList<PaneDefinitionDescriptor> GetPaneDefinitions() => PaneDefinitions;

    public IReadOnlyList<PaneWorkspaceDescriptor> GetWorkspaceDefinitions() => WorkspaceDefinitions;

    public PaneCapabilityDescriptor? FindCapability(string capabilityId)
    {
        return CapabilityDescriptors.FirstOrDefault(capability =>
            string.Equals(capability.CapabilityId, capabilityId, StringComparison.Ordinal));
    }

    public PaneDefinitionDescriptor? FindPaneDefinition(string paneDefinitionId)
    {
        return PaneDefinitions.FirstOrDefault(definition =>
            string.Equals(definition.PaneDefinitionId, paneDefinitionId, StringComparison.Ordinal));
    }

    public PaneWorkspaceDescriptor? FindWorkspaceDefinition(string workspaceId)
    {
        return WorkspaceDefinitions.FirstOrDefault(workspace =>
            string.Equals(workspace.WorkspaceId, workspaceId, StringComparison.Ordinal));
    }
}
