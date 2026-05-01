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
            "engine.command.focus_first_node",
            "Focus First Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Move shell focus to the first available node.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:FocusFirstNode"),

        new(
            "engine.command.select_first_node",
            "Select First Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Select the first available node.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:SelectFirstNode"),

        new(
            "engine.command.focus_first_panel",
            "Focus First Panel",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Move shell focus to the first available panel attachment.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:FocusFirstPanel"),

        new(
            "engine.command.select_first_panel",
            "Select First Panel",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Select the first available panel attachment.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:SelectFirstPanel"),

        new(
            "engine.command.connect_focused_node",
            "Connect Focused Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Connect another selected node into the currently focused node.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ConnectFocusedNode"),

        new(
            "engine.command.unlink_focused_node",
            "Unlink Focused Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.Destructive,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Remove the matching directed link into the focused node.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:UnlinkFocusedNode"),

        new(
            "engine.command.nudge_focused_left",
            "Nudge Left",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Nudge the focused or selected node set left.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:NudgeFocusedLeft"),

        new(
            "engine.command.nudge_focused_right",
            "Nudge Right",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Nudge the focused or selected node set right.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:NudgeFocusedRight"),

        new(
            "engine.command.nudge_focused_up",
            "Nudge Up",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Nudge the focused or selected node set up.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:NudgeFocusedUp"),

        new(
            "engine.command.nudge_focused_down",
            "Nudge Down",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Nudge the focused or selected node set down.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:NudgeFocusedDown"),

        new(
            "engine.command.nudge_focused_forward",
            "Nudge Forward",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Nudge the focused or selected node set forward.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:NudgeFocusedForward"),

        new(
            "engine.command.nudge_focused_back",
            "Nudge Back",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Nudge the focused or selected node set back.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:NudgeFocusedBack"),

        new(
            "engine.command.grow_focused_node",
            "Grow Focused Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Increase scale of the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:GrowFocusedNode"),

        new(
            "engine.command.shrink_focused_node",
            "Shrink Focused Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Decrease scale of the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ShrinkFocusedNode"),

        new(
            "engine.command.apply_triangle_primitive",
            "Triangle Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the triangle primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyTrianglePrimitive"),

        new(
            "engine.command.apply_square_primitive",
            "Square Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the square primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplySquarePrimitive"),

        new(
            "engine.command.apply_diamond_primitive",
            "Diamond Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the diamond primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyDiamondPrimitive"),

        new(
            "engine.command.apply_pentagon_primitive",
            "Pentagon Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the pentagon primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyPentagonPrimitive"),

        new(
            "engine.command.apply_hexagon_primitive",
            "Hexagon Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the hexagon primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyHexagonPrimitive"),

        new(
            "engine.command.apply_cube_primitive",
            "Cube Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the cube primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyCubePrimitive"),

        new(
            "engine.command.apply_tetrahedron_primitive",
            "Tetrahedron Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the tetrahedron primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyTetrahedronPrimitive"),

        new(
            "engine.command.apply_sphere_primitive",
            "Sphere Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the sphere primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplySpherePrimitive"),

        new(
            "engine.command.apply_box_primitive",
            "Box Primitive",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the box primitive to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyBoxPrimitive"),

        new(
            "engine.command.apply_blue_appearance",
            "Blue Appearance",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the blue appearance preset to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyBlueAppearance"),

        new(
            "engine.command.apply_violet_appearance",
            "Violet Appearance",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the violet appearance preset to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyVioletAppearance"),

        new(
            "engine.command.apply_green_appearance",
            "Green Appearance",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the green appearance preset to the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyGreenAppearance"),

        new(
            "engine.command.increase_opacity",
            "Increase Opacity",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Increase opacity of the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:IncreaseOpacity"),

        new(
            "engine.command.decrease_opacity",
            "Decrease Opacity",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Decrease opacity of the focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:DecreaseOpacity"),

        new(
            "engine.command.apply_background_deep_space",
            "Deep Space Background",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the deep-space renderer background preset.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyBackgroundDeepSpace"),

        new(
            "engine.command.apply_background_dusk",
            "Dusk Background",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the dusk renderer background preset.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyBackgroundDusk"),

        new(
            "engine.command.apply_background_paper",
            "Paper Background",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Apply the paper renderer background preset.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ApplyBackgroundPaper"),

        new(
            "engine.command.attach_demo_panel",
            "Attach Demo Panel",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Attach a demo metadata panel to the focused node.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachDemoPanel"),

        new(
            "engine.command.attach_label_panelette",
            "Attach Label Panelette",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Attach a compact label panelette to the focused node.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachLabelPanelette"),

        new(
            "engine.command.attach_detail_metadata_panelette",
            "Attach Detail Metadata Panelette",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar],
            "Attach a larger metadata panelette to the focused node.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachDetailMetadataPanelette"),

        new(
            "engine.command.pane_save_instance_only",
            "Save Instance Only",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentPaneInstance,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Persist the current pane instance working-copy state without promoting it into reusable definition truth.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:PaneSaveInstanceOnly"),

        new(
            "engine.command.pane_save_as_new_definition",
            "Save As New Definition",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentPaneInstance,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Promote the current pane instance into a new reusable pane definition.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:PaneSaveAsNewDefinition"),

        new(
            "engine.command.pane_detach_from_definition",
            "Detach from Definition",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentPaneInstance,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Detach the current pane instance from reusable definition tracking while preserving local authored state.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:PaneDetachFromDefinition"),

        new(
            "engine.command.pane_revert_to_definition",
            "Revert to Definition",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentPaneInstance,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Discard current local pane-instance overrides and restore reusable definition-backed truth.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:PaneRevertToDefinition"),

        new(
            "engine.command.pane_apply_default_appearance",
            "Apply Default Appearance",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentPaneInstance,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Apply the default pane-instance appearance variant through the shared pane authoring runtime.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:PaneApplyDefaultAppearance"),

        new(
            "engine.command.pane_apply_cool_appearance",
            "Apply Cool Appearance",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentPaneInstance,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Apply the cool pane-instance appearance variant through the shared pane authoring runtime.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:PaneApplyCoolAppearance"),

        new(
            "engine.command.pane_apply_warm_appearance",
            "Apply Warm Appearance",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentPaneInstance,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Apply the warm pane-instance appearance variant through the shared pane authoring runtime.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:PaneApplyWarmAppearance"),

        new(
            "engine.command.pane_reset_appearance",
            "Reset Appearance",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentPaneInstance,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Reset the pane-instance appearance variant back to its current baseline through the shared runtime.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:PaneResetAppearance"),

        new(
            "engine.command.create_demo_node",
            "Create Demo Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Create a new demo node in the current scene.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:CreateEntity"),

        new(
            "engine.command.delete_focused_node",
            "Delete Focused Node",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.Destructive,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Delete the currently focused or selected node set.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:DeleteEntities"),

        new(
            "engine.command.group_selection",
            "Group Selection",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Create a new group from the current selection.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:GroupSelection"),

        new(
            "engine.command.save_bookmark",
            "Save Bookmark",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Save the current focus/selection posture as a bookmark.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:BookmarkSave"),

        new(
            "engine.command.restore_latest_bookmark",
            "Restore Latest Bookmark",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Restore the most recently saved bookmark.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:BookmarkRestore"),

        new(
            "engine.command.clear_links",
            "Clear Links",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.Destructive,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Remove all current link relationships from the scene.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ClearLinks"),

        new(
            "engine.command.clear_focus",
            "Clear Focus",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Clear the current focused node or panel target.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ClearFocus"),

        new(
            "engine.command.create_node_at_pointer",
            "Create Node At Pointer",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.BackgroundContext,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Create a node at the current viewport pointer/context-surface anchor point.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:CreateNodeAtPointer"),

        new(
            "engine.command.exit_node_context",
            "Exit Node Context",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Exit the currently entered node context from a viewport background command surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ExitNodeContext"),

        new(
            "engine.command.focus_node_context_target",
            "Focus Node Context Target",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Focus the node targeted by the active viewport node context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:Focus"),

        new(
            "engine.command.select_node_context_target",
            "Select Node Context Target",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Select the node targeted by the active viewport node context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:Select"),

        new(
            "engine.command.enter_node_context_target",
            "Enter Node Context Target",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Enter the node targeted by the active viewport node context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:EnterNode"),

        new(
            "engine.command.attach_metadata_panelette_for_node",
            "Attach Metadata Panelette",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Attach a metadata panelette for the node targeted by the viewport context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachMetadataPaneletteForNode"),

        new(
            "engine.command.attach_label_panelette_for_node",
            "Attach Label Panelette",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Attach a label panelette for the node targeted by the viewport context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachLabelPaneletteForNode"),

        new(
            "engine.command.attach_detail_metadata_panelette_for_node",
            "Attach Detailed Metadata Panelette",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Attach a higher-detail metadata panelette for the node targeted by the viewport context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachDetailMetadataPaneletteForNode"),

        new(
            "engine.command.clear_panelette_for_node",
            "Remove Panelette",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.Destructive,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.FocusedNode,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Remove panelette attachments from the node targeted by the viewport context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ClearPaneletteForNode"),

        new(
            "engine.command.attach_metadata_panelettes_for_all_nodes",
            "Attach Metadata Panelettes (All Nodes)",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.BackgroundContext,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Attach metadata panelettes across all current nodes from the viewport background context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachMetadataPanelettesForAllNodes"),

        new(
            "engine.command.attach_label_panelettes_for_all_nodes",
            "Attach Label Panelettes (All Nodes)",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.BackgroundContext,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Attach label panelettes across all current nodes from the viewport background context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachLabelPanelettesForAllNodes"),

        new(
            "engine.command.attach_detail_metadata_panelettes_for_all_nodes",
            "Attach Detailed Metadata Panelettes (All Nodes)",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.BackgroundContext,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Attach higher-detail metadata panelettes across all current nodes from the viewport background context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:AttachDetailMetadataPanelettesForAllNodes"),

        new(
            "engine.command.clear_panelettes_for_all_nodes",
            "Remove Panelettes (All Nodes)",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.Destructive,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.BackgroundContext,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Remove panelette attachments across all current nodes from the viewport background context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:ClearPanelettesForAllNodes"),

        new(
            "engine.command.link_select_source",
            "Select Link Source",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Select the source endpoint for the active viewport link context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:LinkSelectSource"),

        new(
            "engine.command.link_select_target",
            "Select Link Target",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Select the target endpoint for the active viewport link context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:LinkSelectTarget"),

        new(
            "engine.command.link_frame_endpoints",
            "Frame Link Endpoints",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Frame the source and target endpoints for the active viewport link context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:LinkFrameEndpoints"),

        new(
            "engine.command.link_unlink_context",
            "Unlink Endpoints",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.Destructive,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Remove the relationship represented by the active viewport link context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:LinkUnlink"),

        new(
            "engine.command.group_select_members",
            "Select Group Nodes",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Select members of the group targeted by the active viewport group context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:GroupSelectMembers"),

        new(
            "engine.command.group_frame",
            "Frame Group",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Frame members of the group targeted by the active viewport group context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:GroupFrame"),

        new(
            "engine.command.group_add_selection_to_context_group",
            "Add Selection To Group",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Add the current selection to the group targeted by the active viewport group context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:GroupAddSelection"),

        new(
            "engine.command.group_remove_selection_from_context_group",
            "Remove Selection From Group",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.EngineStateMutating,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Remove the current selection from the group targeted by the active viewport group context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:GroupRemoveSelection"),

        new(
            "engine.command.group_delete_context_group",
            "Delete Group",
            PaneCapabilityKind.EngineCommand,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.Destructive,
            PaneCapabilityLifetime.OneShot,
            PaneCapabilityContext.CurrentSelection,
            [PaneProjectionForm.Button, PaneProjectionForm.Toolbar, PaneProjectionForm.Menu],
            "Delete the group targeted by the active viewport group context surface.",
            OwnerKind: "Engine",
            BindingTargetRef: "EngineCommand:GroupDelete"),

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
            "engine.state.bookmark_summary",
            "Bookmark Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Card, PaneProjectionForm.Badge],
            "Read-only summary of saved bookmarks.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:BookmarkSummary"),

        new(
            "engine.state.panel_summary",
            "Panel Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Card, PaneProjectionForm.Badge, PaneProjectionForm.Table],
            "Read-only summary of attached panel and panelette state.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:PanelSummary"),

        new(
            "engine.state.navigation_history",
            "Navigation History",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.HistoricalArchive,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.List, PaneProjectionForm.Table, PaneProjectionForm.Card],
            "Recent navigation history entries from the viewport camera bridge.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:NavigationHistory"),

        new(
            "engine.state.command_history",
            "Command History",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.HistoricalArchive,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.List, PaneProjectionForm.Table, PaneProjectionForm.Card],
            "Recent command history surfaced through the shell readout layer.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:CommandHistory"),

        new(
            "engine.state.action_readiness",
            "Action Readiness",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Card, PaneProjectionForm.Table],
            "Read-only summary of currently executable direct shell actions.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:ActionReadiness"),

        new(
            "engine.state.interaction_semantics",
            "Interaction Semantics",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.CurrentWorld,
            [PaneProjectionForm.Card, PaneProjectionForm.Table],
            "Long-form summary of current interaction semantics and shell command posture.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:InteractionSemantics"),

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
            BindingTargetRef: "Browser:CommandCatalog"),

        new(
            "browser.pane_catalog",
            "Pane Catalog",
            PaneCapabilityKind.Browser,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.List, PaneProjectionForm.Table, PaneProjectionForm.Tree],
            "Browse seeded and user-authored pane definitions available to the current project.",
            OwnerKind: "Engine",
            BindingTargetRef: "Browser:PaneCatalog"),

        new(
            "browser.workspace_catalog",
            "Workspace Catalog",
            PaneCapabilityKind.Browser,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.List, PaneProjectionForm.Table, PaneProjectionForm.Tree],
            "Browse seeded and user-authored workspace definitions available to the current project.",
            OwnerKind: "Engine",
            BindingTargetRef: "Browser:WorkspaceCatalog"),

        new(
            "engine.state.visual_semantics_settings_summary",
            "Visual Semantics Settings Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary of current hardcoded halo/focus/selection visual-semantics settings.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:VisualSemanticsSettingsSummary"),

        new(
            "engine.state.render_surface_settings_summary",
            "Render Surface Settings Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.EngineState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary of current hardcoded renderer/panelette/overlay surface settings.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:RenderSurfaceSettingsSummary"),

        new(
            "engine.state.settings_surface_audit_summary",
            "Settings Surface Audit Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Audit/readout summary of settings surfaces that remain hardcoded in the current viewmodel layer.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:SettingsSurfaceAuditSummary"),

        new(
            "engine.state.parent_shell_control_audit_summary",
            "Parent Shell Control Audit Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Audit/readout summary of parent-pane shell controls that remain hardcoded in pane chrome.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:ParentShellControlAuditSummary"),

        new(
            "engine.state.main_window_shell_chrome_audit_summary",
            "Main Window Shell Chrome Audit Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Audit/readout summary of shell-level MainWindow chrome that remains hardcoded outside the pane catalog path.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:MainWindowShellChromeAuditSummary"),

        new(
            "engine.state.hardcoded_surface_audit_next_targets_summary",
            "Hardcoded Surface Audit Next Targets",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only next-target summary for the current hardcoded-surface migration lane.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:HardcodedSurfaceAuditNextTargetsSummary"),

        new(
            "engine.state.viewport_command_surface_audit_summary",
            "Viewport Command Surface Audit Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary of renderer-local viewport command-surface inventories that still sit outside the reusable pane registry.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:ViewportCommandSurfaceAuditSummary"),

        new(
            "engine.state.renderer_viewport_registry_gap_summary",
            "Renderer Viewport Registry Gap Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary of the current gap between renderer-owned command surfaces and reusable pane capability registration.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:RendererViewportRegistryGapSummary"),

        new(
            "engine.state.shell_native_chrome_boundary_summary",
            "Shell Native Chrome Boundary Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary of renderer/shell mechanics that currently look like final shell-native chrome rather than reusable pane-definition truth.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:ShellNativeChromeBoundarySummary"),

        new(
            "engine.state.renderer_parity_next_targets_summary",
            "Renderer Parity Next Targets Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary of the next renderer/helper files and parity targets for the current registry-expansion wave.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:RendererParityNextTargetsSummary"),

        new(
            "engine.state.active_panel_command_surface_state_summary",
            "Active Panel Command Surface State Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary classifying the renderer-local active command-surface state helper.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:ActivePanelCommandSurfaceStateSummary"),

        new(
            "engine.state.renderer_halo_and_group_effect_summary",
            "Renderer Halo And Group Effect Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary of renderer-native halo, group-volume, and visual-effect ownership.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:RendererHaloAndGroupEffectSummary"),

        new(
            "engine.state.projected_hit_testing_boundary_summary",
            "Projected Hit-Testing Boundary Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary classifying projected node hit-testing as shell-native interaction math.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:ProjectedHitTestingBoundarySummary"),

        new(
            "engine.state.renderer_migration_boundary_summary",
            "Renderer Migration Boundary Summary",
            PaneCapabilityKind.StateSelector,
            PaneCapabilitySourceDomain.WorkspaceState,
            PaneCapabilityAuthority.ReadOnly,
            PaneCapabilityLifetime.CurrentSnapshot,
            PaneCapabilityContext.GlobalProject,
            [PaneProjectionForm.Card, PaneProjectionForm.Editor],
            "Read-only summary of which renderer parity gaps are migratable next versus shell-native for now.",
            OwnerKind: "Engine",
            BindingTargetRef: "StateSelector:RendererMigrationBoundarySummary")
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
            ["seeded", "browser", "commands"]),

        new(
            "pane.scene_actions",
            "Scene Actions",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "scene_actions.header",
                    PaneElementKind.DefinitionHeader,
                    "Scene Actions"),
                new(
                    "scene_actions.mode_badge",
                    PaneElementKind.StatusBadge,
                    "Mode",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.interaction_mode_badge")),
                new(
                    "scene_actions.command_bar",
                    PaneElementKind.CommandBar,
                    "Scene Actions",
                    null,
                    [
                        new(
                            "scene_actions.create_demo_node",
                            PaneElementKind.Button,
                            "Create Demo Node",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.create_demo_node")),
                        new(
                            "scene_actions.group_selection",
                            PaneElementKind.Button,
                            "Group Selection",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.group_selection")),
                        new(
                            "scene_actions.delete_focused_node",
                            PaneElementKind.Button,
                            "Delete Focused Node",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.delete_focused_node")),
                        new(
                            "scene_actions.clear_links",
                            PaneElementKind.Button,
                            "Clear Links",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.clear_links"))
                    ]),
                new(
                    "scene_actions.semantic_summary",
                    PaneElementKind.TextBlock,
                    "Interaction Semantics",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.interaction_semantics"))
            ],
            "Seeded action pane migrating the next scene-mutation and relationship controls into the shared catalog-backed runtime path.",
            ["seeded", "actions", "scene"]),

        new(
            "pane.history_overview",
            "History Overview",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "history_overview.header",
                    PaneElementKind.DefinitionHeader,
                    "History Overview"),
                new(
                    "history_overview.bookmark_badge",
                    PaneElementKind.StatusBadge,
                    "Bookmarks",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.bookmark_summary")),
                new(
                    "history_overview.bookmark_summary",
                    PaneElementKind.LabelValueField,
                    "Bookmark Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.bookmark_summary")),
                new(
                    "history_overview.panel_summary",
                    PaneElementKind.LabelValueField,
                    "Panel Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.panel_summary")),
                new(
                    "history_overview.history_actions",
                    PaneElementKind.CommandBar,
                    "History Actions",
                    null,
                    [
                        new(
                            "history_overview.save_bookmark",
                            PaneElementKind.Button,
                            "Save Bookmark",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.save_bookmark")),
                        new(
                            "history_overview.restore_latest_bookmark",
                            PaneElementKind.Button,
                            "Restore Latest Bookmark",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.restore_latest_bookmark"))
                    ]),
                new(
                    "history_overview.navigation_history",
                    PaneElementKind.TextBlock,
                    "Navigation History",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.navigation_history")),
                new(
                    "history_overview.command_history",
                    PaneElementKind.TextBlock,
                    "Command History",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.command_history"))
            ],
            "Seeded history/status pane migrating bookmark and shell-history readouts through the shared realization path.",
            ["seeded", "history", "status"]),

        new(
            "pane.developer_diagnostics",
            "Developer Diagnostics",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "developer_diagnostics.header",
                    PaneElementKind.DefinitionHeader,
                    "Developer Diagnostics"),
                new(
                    "developer_diagnostics.filter",
                    PaneElementKind.FilterBar,
                    "Filter diagnostics"),
                new(
                    "developer_diagnostics.focus_origin_badge",
                    PaneElementKind.StatusBadge,
                    "Focus Origin",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.focus_origin")),
                new(
                    "developer_diagnostics.projection_status",
                    PaneElementKind.ProjectionStatusView,
                    "Pane / Shell Structure",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.pane_structure")),
                new(
                    "developer_diagnostics.view_metrics",
                    PaneElementKind.MetricsReadout,
                    "View Metrics",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.view_summary")),
                new(
                    "developer_diagnostics.view_details",
                    PaneElementKind.PropertyEditor,
                    "View Details",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.view_details")),
                new(
                    "developer_diagnostics.focused_transform",
                    PaneElementKind.PropertyEditor,
                    "Focused Transform",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.focused_transform_details")),
                new(
                    "developer_diagnostics.group_details",
                    PaneElementKind.TableBrowser,
                    "Group Details",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.group_details")),
                new(
                    "developer_diagnostics.panel_details",
                    PaneElementKind.TreeBrowser,
                    "Panel Details",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.panel_details")),
                new(
                    "developer_diagnostics.link_details",
                    PaneElementKind.TextEditor,
                    "Link Details",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.link_details"))
            ],
            "Seeded developer-facing pane that migrates detailed shell/readout diagnostics into the shared runtime path instead of leaving them only in direct hardcoded readout sections.",
            ["seeded", "developer", "diagnostics"]),

        new(
            "pane.mutation_studio",
            "Mutation Studio",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "mutation_studio.header",
                    PaneElementKind.DefinitionHeader,
                    "Mutation Studio"),
                new(
                    "mutation_studio.action_readiness",
                    PaneElementKind.TaskMonitor,
                    "Action Readiness",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.action_readiness")),
                new(
                    "mutation_studio.transform_bar",
                    PaneElementKind.CommandBar,
                    "Transform Actions",
                    null,
                    [
                        new(
                            "mutation_studio.nudge_left",
                            PaneElementKind.Button,
                            "Left",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.nudge_focused_left")),
                        new(
                            "mutation_studio.nudge_right",
                            PaneElementKind.Button,
                            "Right",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.nudge_focused_right")),
                        new(
                            "mutation_studio.nudge_up",
                            PaneElementKind.Button,
                            "Up",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.nudge_focused_up")),
                        new(
                            "mutation_studio.nudge_down",
                            PaneElementKind.Button,
                            "Down",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.nudge_focused_down")),
                        new(
                            "mutation_studio.nudge_forward",
                            PaneElementKind.Button,
                            "Forward",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.nudge_focused_forward")),
                        new(
                            "mutation_studio.nudge_back",
                            PaneElementKind.Button,
                            "Back",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.nudge_focused_back")),
                        new(
                            "mutation_studio.grow",
                            PaneElementKind.Button,
                            "Grow",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.grow_focused_node")),
                        new(
                            "mutation_studio.shrink",
                            PaneElementKind.Button,
                            "Shrink",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.shrink_focused_node"))
                    ]),
                new(
                    "mutation_studio.appearance_bar",
                    PaneElementKind.CommandBar,
                    "Appearance Actions",
                    null,
                    [
                        new(
                            "mutation_studio.triangle",
                            PaneElementKind.Button,
                            "Triangle",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_triangle_primitive")),
                        new(
                            "mutation_studio.square",
                            PaneElementKind.Button,
                            "Square",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_square_primitive")),
                        new(
                            "mutation_studio.diamond",
                            PaneElementKind.Button,
                            "Diamond",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_diamond_primitive")),
                        new(
                            "mutation_studio.cube",
                            PaneElementKind.Button,
                            "Cube",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_cube_primitive")),
                        new(
                            "mutation_studio.blue",
                            PaneElementKind.Button,
                            "Blue",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_blue_appearance")),
                        new(
                            "mutation_studio.violet",
                            PaneElementKind.Button,
                            "Violet",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_violet_appearance")),
                        new(
                            "mutation_studio.green",
                            PaneElementKind.Button,
                            "Green",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_green_appearance")),
                        new(
                            "mutation_studio.opacity_up",
                            PaneElementKind.Button,
                            "Opacity +",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.increase_opacity")),
                        new(
                            "mutation_studio.opacity_down",
                            PaneElementKind.Button,
                            "Opacity -",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.decrease_opacity"))
                    ]),
                new(
                    "mutation_studio.background_bar",
                    PaneElementKind.CommandBar,
                    "Background Presets",
                    null,
                    [
                        new(
                            "mutation_studio.deep_space",
                            PaneElementKind.Button,
                            "Deep Space",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_background_deep_space")),
                        new(
                            "mutation_studio.dusk",
                            PaneElementKind.Button,
                            "Dusk",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_background_dusk")),
                        new(
                            "mutation_studio.paper",
                            PaneElementKind.Button,
                            "Paper",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.apply_background_paper"))
                    ]),
                new(
                    "mutation_studio.panel_attachment_bar",
                    PaneElementKind.CommandBar,
                    "Panel Attachments",
                    null,
                    [
                        new(
                            "mutation_studio.attach_demo_panel",
                            PaneElementKind.Button,
                            "Demo Panel",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.attach_demo_panel")),
                        new(
                            "mutation_studio.attach_label_panelette",
                            PaneElementKind.Button,
                            "Label Panelette",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.attach_label_panelette")),
                        new(
                            "mutation_studio.attach_detail_metadata_panelette",
                            PaneElementKind.Button,
                            "Detail Panelette",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.attach_detail_metadata_panelette"))
                    ])
            ],
            "Seeded mutation-and-appearance pane that migrates transform, primitive, appearance, background, and panel-attachment actions through the shared catalog-backed runtime path.",
            ["seeded", "mutation", "appearance"]),

        new(
            "pane.pane_catalog_browser",
            "Pane Catalog Browser",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "pane_catalog_browser.header",
                    PaneElementKind.DefinitionHeader,
                    "Pane Catalog Browser"),
                new(
                    "pane_catalog_browser.summary",
                    PaneElementKind.LabelValueField,
                    "Catalog Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.seeded_pane_catalog_summary")),
                new(
                    "pane_catalog_browser.primary_label",
                    PaneElementKind.StatusBadge,
                    "Primary Workspace",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.seeded_pane_catalog_primary_label")),
                new(
                    "pane_catalog_browser.definitions",
                    PaneElementKind.TableBrowser,
                    "Pane Definitions",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "browser.pane_catalog")),
                new(
                    "pane_catalog_browser.commands",
                    PaneElementKind.CommandBrowser,
                    "Capability Browser",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "browser.command_catalog"))
            ],
            "Seeded admin/browser pane surfacing pane-definition and capability catalog truth through the shared runtime path.",
            ["seeded", "browser", "catalog", "admin"]),

        new(
            "pane.workspace_admin",
            "Workspace Admin",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "workspace_admin.header",
                    PaneElementKind.DefinitionHeader,
                    "Workspace Admin"),
                new(
                    "workspace_admin.catalog_summary",
                    PaneElementKind.LabelValueField,
                    "Workspace Summary",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.seeded_pane_catalog_summary")),
                new(
                    "workspace_admin.workspaces",
                    PaneElementKind.TreeBrowser,
                    "Workspace Definitions",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.Capability,
                        "browser.workspace_catalog")),
                new(
                    "workspace_admin.structure",
                    PaneElementKind.ProjectionStatusView,
                    "Current Shell Structure",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.pane_structure")),
                new(
                    "workspace_admin.actions",
                    PaneElementKind.CommandBar,
                    "Workspace Actions",
                    null,
                    [
                        new(
                            "workspace_admin.create_child_pane",
                            PaneElementKind.Button,
                            "Create Child Pane",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.create_child_pane")),
                        new(
                            "workspace_admin.focus_first_panel",
                            PaneElementKind.Button,
                            "Focus First Panel",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.focus_first_panel")),
                        new(
                            "workspace_admin.select_first_panel",
                            PaneElementKind.Button,
                            "Select First Panel",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "engine.command.select_first_panel"))
                    ])
            ],
            "Seeded workspace-management pane surfacing workspace catalog, live shell structure, and first workspace-oriented actions through the shared runtime path.",
            ["seeded", "workspace", "admin"]),

        new(
            "pane.catalog_detail_inspector",
            "Catalog Detail Inspector",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "catalog_detail_inspector.header",
                    PaneElementKind.DefinitionHeader,
                    "Catalog Detail Inspector"),
                new(
                    "catalog_detail_inspector.summary_section",
                    PaneElementKind.Section,
                    "Catalog Summary",
                    null,
                    [
                        new(
                            "catalog_detail_inspector.summary",
                            PaneElementKind.LabelValueField,
                            "Catalog Summary",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.seeded_pane_catalog_summary")),
                        new(
                            "catalog_detail_inspector.primary",
                            PaneElementKind.StatusBadge,
                            "Primary Workspace",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.seeded_pane_catalog_primary_label")),
                        new(
                            "catalog_detail_inspector.structure",
                            PaneElementKind.ProjectionStatusView,
                            "Current Shell Structure",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.pane_structure"))
                    ]),
                new(
                    "catalog_detail_inspector.browser_section",
                    PaneElementKind.Section,
                    "Catalog Browsers",
                    null,
                    [
                        new(
                            "catalog_detail_inspector.panes",
                            PaneElementKind.TableBrowser,
                            "Pane Definitions",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "browser.pane_catalog")),
                        new(
                            "catalog_detail_inspector.workspaces",
                            PaneElementKind.TreeBrowser,
                            "Workspace Definitions",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "browser.workspace_catalog"))
                    ])
            ],
            "Seeded catalog-detail pane that uses the shared section/container runtime plus stronger browser and projection-status semantics for pane/workspace administration.",
            ["seeded", "catalog", "admin", "inspector"]),

        new(
            "pane.pane_management_overview",
            "Pane Management Overview",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "pane_management_overview.header",
                    PaneElementKind.DefinitionHeader,
                    "Pane Management Overview"),
                new(
                    "pane_management_overview.state_section",
                    PaneElementKind.Section,
                    "Pane / Workspace State",
                    null,
                    [
                        new(
                            "pane_management_overview.structure",
                            PaneElementKind.ProjectionStatusView,
                            "Live Pane Structure",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.pane_structure")),
                        new(
                            "pane_management_overview.catalog_summary",
                            PaneElementKind.MetricsReadout,
                            "Catalog Summary",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.seeded_pane_catalog_summary")),
                        new(
                            "pane_management_overview.action_readiness",
                            PaneElementKind.TaskMonitor,
                            "Action Readiness",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.action_readiness"))
                    ]),
                new(
                    "pane_management_overview.action_section",
                    PaneElementKind.Section,
                    "Management Actions",
                    null,
                    [
                        new(
                            "pane_management_overview.actions",
                            PaneElementKind.CommandBar,
                            "Pane Actions",
                            null,
                            [
                                new(
                                    "pane_management_overview.create_child",
                                    PaneElementKind.Button,
                                    "Create Child Pane",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "engine.command.create_child_pane")),
                                new(
                                    "pane_management_overview.focus_first_node",
                                    PaneElementKind.Button,
                                    "Focus First Node",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "engine.command.focus_first_node")),
                                new(
                                    "pane_management_overview.focus_first_panel",
                                    PaneElementKind.Button,
                                    "Focus First Panel",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "engine.command.focus_first_panel")),
                                new(
                                    "pane_management_overview.select_first_panel",
                                    PaneElementKind.Button,
                                    "Select First Panel",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "engine.command.select_first_panel")),
                                new(
                                    "pane_management_overview.clear_selection",
                                    PaneElementKind.Button,
                                    "Clear Selection",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "engine.command.clear_selection"))
                            ]),
                        new(
                            "pane_management_overview.workspace_browser",
                            PaneElementKind.TreeBrowser,
                            "Workspace Catalog",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.Capability,
                                "browser.workspace_catalog"))
                    ])
            ],
            "Seeded pane-management pane that pushes the shared runtime toward stronger sectioned admin surfaces and direct pane/workspace management actions.",
            ["seeded", "pane-management", "workspace", "admin"]),

        new(
            "pane.catalog_navigation_studio",
            "Catalog Navigation Studio",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "catalog_navigation_studio.header",
                    PaneElementKind.DefinitionHeader,
                    "Catalog Navigation Studio"),
                new(
                    "catalog_navigation_studio.filter",
                    PaneElementKind.FilterBar,
                    "Filter catalog surfaces",
                    new PaneElementBindingDescriptor(
                        PaneElementBindingTargetKind.StateSelector,
                        "engine.state.seeded_pane_catalog_summary"),
                    BehaviorSettings: new Dictionary<string, string>
                    {
                        ["placeholder"] = "Search/filter catalog surfaces (future)"
                    }),
                new(
                    "catalog_navigation_studio.tabs",
                    PaneElementKind.TabsHost,
                    "Catalog Studio Tabs",
                    null,
                    [
                        new(
                            "catalog_navigation_studio.panes_section",
                            PaneElementKind.Section,
                            "Pane Catalog",
                            null,
                            [
                                new(
                                    "catalog_navigation_studio.primary_workspace",
                                    PaneElementKind.StatusBadge,
                                    "Primary Workspace",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.seeded_pane_catalog_primary_label")),
                                new(
                                    "catalog_navigation_studio.pane_catalog",
                                    PaneElementKind.TableBrowser,
                                    "Pane Definitions",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "browser.pane_catalog"))
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Browse seeded and user-authored pane definitions through the shared runtime surface."
                            }),
                        new(
                            "catalog_navigation_studio.workspaces_section",
                            PaneElementKind.Section,
                            "Workspace Catalog",
                            null,
                            [
                                new(
                                    "catalog_navigation_studio.workspace_catalog",
                                    PaneElementKind.TreeBrowser,
                                    "Workspace Definitions",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "browser.workspace_catalog")),
                                new(
                                    "catalog_navigation_studio.structure_status",
                                    PaneElementKind.ProjectionStatusView,
                                    "Current Shell Structure",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.pane_structure"))
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Compare reusable workspace truth with current shell structure."
                            }),
                        new(
                            "catalog_navigation_studio.capabilities_section",
                            PaneElementKind.Section,
                            "Capability Catalog",
                            null,
                            [
                                new(
                                    "catalog_navigation_studio.capability_catalog",
                                    PaneElementKind.CommandBrowser,
                                    "Capabilities",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "browser.command_catalog")),
                                new(
                                    "catalog_navigation_studio.catalog_summary",
                                    PaneElementKind.MetricsReadout,
                                    "Catalog Summary",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.seeded_pane_catalog_summary"))
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Browse command/capability surfaces and compare them against the current seeded catalog summary."
                            })
                    ],
                    BehaviorSettings: new Dictionary<string, string>
                    {
                        ["description"] = "Tabbed browser surface for pane, workspace, and capability catalog exploration."
                    })
            ],
            "Seeded catalog-navigation pane validating tabs, sections, and filter-aware browser semantics through the shared pane runtime.",
            ["seeded", "catalog", "browser", "admin"]),

        new(
            "pane.shell_workspace_studio",
            "Shell Workspace Studio",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "shell_workspace_studio.header",
                    PaneElementKind.DefinitionHeader,
                    "Shell Workspace Studio"),
                new(
                    "shell_workspace_studio.split",
                    PaneElementKind.SplitHost,
                    "Workspace Studio Split",
                    null,
                    [
                        new(
                            "shell_workspace_studio.state_group",
                            PaneElementKind.InspectorGroup,
                            "Workspace State",
                            null,
                            [
                                new(
                                    "shell_workspace_studio.structure",
                                    PaneElementKind.ProjectionStatusView,
                                    "Live Pane Structure",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.pane_structure")),
                                new(
                                    "shell_workspace_studio.action_readiness",
                                    PaneElementKind.TaskMonitor,
                                    "Action Readiness",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.action_readiness")),
                                new(
                                    "shell_workspace_studio.command_history",
                                    PaneElementKind.PropertyEditor,
                                    "Command History",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.command_history"))
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Read-only operational state, pane structure, and recent command history for the current shell workspace."
                            }),
                        new(
                            "shell_workspace_studio.action_group",
                            PaneElementKind.InspectorGroup,
                            "Workspace Actions",
                            null,
                            [
                                new(
                                    "shell_workspace_studio.panel_summary",
                                    PaneElementKind.LabelValueField,
                                    "Panel Summary",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.panel_summary")),
                                new(
                                    "shell_workspace_studio.actions",
                                    PaneElementKind.CommandBar,
                                    "Workspace Actions",
                                    null,
                                    [
                                        new(
                                            "shell_workspace_studio.create_child",
                                            PaneElementKind.Button,
                                            "Create Child Pane",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.create_child_pane")),
                                        new(
                                            "shell_workspace_studio.focus_first_node",
                                            PaneElementKind.Button,
                                            "Focus First Node",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.focus_first_node")),
                                        new(
                                            "shell_workspace_studio.select_first_node",
                                            PaneElementKind.Button,
                                            "Select First Node",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.select_first_node")),
                                        new(
                                            "shell_workspace_studio.focus_first_panel",
                                            PaneElementKind.Button,
                                            "Focus First Panel",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.focus_first_panel")),
                                        new(
                                            "shell_workspace_studio.select_first_panel",
                                            PaneElementKind.Button,
                                            "Select First Panel",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.select_first_panel")),
                                        new(
                                            "shell_workspace_studio.clear_selection",
                                            PaneElementKind.Button,
                                            "Clear Selection",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.clear_selection"))
                                    ]),
                                new(
                                    "shell_workspace_studio.workspace_browser",
                                    PaneElementKind.TreeBrowser,
                                    "Workspace Catalog",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.Capability,
                                        "browser.workspace_catalog"))
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Workspace-management actions and live catalog browsing through the same shared runtime path."
                            })
                    ],
                    BehaviorSettings: new Dictionary<string, string>
                    {
                        ["description"] = "Split-layout admin surface for live workspace inspection and manageability.",
                        ["orientation"] = "horizontal"
                    })
            ],
            "Seeded shell/workspace pane validating split-host and inspector-group semantics for the next admin/workspace migration layer.",
            ["seeded", "workspace", "admin", "studio"]),

        new(
            "pane.pane_authoring_studio",
            "Pane Authoring Studio",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "pane_authoring_studio.header",
                    PaneElementKind.DefinitionHeader,
                    "Pane Authoring Studio"),
                new(
                    "pane_authoring_studio.tabs",
                    PaneElementKind.TabsHost,
                    "Pane Authoring Tabs",
                    null,
                    [
                        new(
                            "pane_authoring_studio.working_copy_section",
                            PaneElementKind.Section,
                            "Working Copy",
                            null,
                            [
                                new(
                                    "pane_authoring_studio.source_summary",
                                    PaneElementKind.LabelValueField,
                                    "Source Posture",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.source_summary")),
                                new(
                                    "pane_authoring_studio.working_copy_status",
                                    PaneElementKind.LabelValueField,
                                    "Working Copy Status",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.working_copy_status")),
                                new(
                                    "pane_authoring_studio.definition_sync",
                                    PaneElementKind.LabelValueField,
                                    "Definition Sync",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.definition_sync_summary")),
                                new(
                                    "pane_authoring_studio.local_state",
                                    PaneElementKind.LabelValueField,
                                    "Local State",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.local_state_summary"))
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Pane-instance working-copy posture and source/definition relationship surfaced through pane-local selector binding."
                            }),
                        new(
                            "pane_authoring_studio.authored_values_section",
                            PaneElementKind.Section,
                            "Authored Values",
                            null,
                            [
                                new(
                                    "pane_authoring_studio.current_values",
                                    PaneElementKind.PropertyEditor,
                                    "Current Authored Values",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.current_authored_summary"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Current pane-local authored values surfaced read-only through the shared configuration/editor runtime."
                                    }),
                                new(
                                    "pane_authoring_studio.baseline_values",
                                    PaneElementKind.PropertyEditor,
                                    "Baseline Authored Values",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.baseline_authored_summary"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Definition/local baseline values against which working-copy overrides are computed."
                                    }),
                                new(
                                    "pane_authoring_studio.title_editor",
                                    PaneElementKind.TextEditor,
                                    "Local Title",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.title_summary"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["editorKind"] = "pane_title",
                                        ["description"] = "Edit the pane-instance title through the shared runtime instead of the old ChildPaneView-only authoring seam.",
                                        ["placeholder"] = "Pane title"
                                    }),
                                new(
                                    "pane_authoring_studio.description_editor",
                                    PaneElementKind.TextEditor,
                                    "Local Description",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.description_summary"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["editorKind"] = "pane_description",
                                        ["description"] = "Edit the pane-instance description through the shared runtime instead of the old ChildPaneView-only authoring seam.",
                                        ["placeholder"] = "Pane description"
                                    }),
                                new(
                                    "pane_authoring_studio.override_summary",
                                    PaneElementKind.TextEditor,
                                    "Override Summary",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.override_summary"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Read-only override/configuration summary for the current pane instance."
                                    })
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Configuration/editor-style readouts for current and baseline authored values."
                            }),
                        new(
                            "pane_authoring_studio.appearance_section",
                            PaneElementKind.Section,
                            "Appearance Configuration",
                            null,
                            [
                                new(
                                    "pane_authoring_studio.appearance_current",
                                    PaneElementKind.LabelValueField,
                                    "Current Appearance",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.appearance_current_summary")),
                                new(
                                    "pane_authoring_studio.appearance_baseline",
                                    PaneElementKind.LabelValueField,
                                    "Baseline Appearance",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.appearance_baseline_summary")),
                                new(
                                    "pane_authoring_studio.appearance_actions",
                                    PaneElementKind.CommandBar,
                                    "Appearance Actions",
                                    null,
                                    [
                                        new(
                                            "pane_authoring_studio.appearance_default",
                                            PaneElementKind.Button,
                                            "Default",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.pane_apply_default_appearance")),
                                        new(
                                            "pane_authoring_studio.appearance_cool",
                                            PaneElementKind.Button,
                                            "Cool",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.pane_apply_cool_appearance")),
                                        new(
                                            "pane_authoring_studio.appearance_warm",
                                            PaneElementKind.Button,
                                            "Warm",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.pane_apply_warm_appearance")),
                                        new(
                                            "pane_authoring_studio.appearance_reset",
                                            PaneElementKind.Button,
                                            "Reset Appearance",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.pane_reset_appearance"))
                                    ])
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "First shared-runtime authored-value configuration slice for pane-local appearance."
                            }),
                        new(
                            "pane_authoring_studio.lifecycle_section",
                            PaneElementKind.Section,
                            "Lifecycle Guidance",
                            null,
                            [
                                new(
                                    "pane_authoring_studio.definition_summary",
                                    PaneElementKind.LabelValueField,
                                    "Definition Source",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.definition_summary")),
                                new(
                                    "pane_authoring_studio.definition_action_summary",
                                    PaneElementKind.TextBlock,
                                    "Definition Action Summary",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.definition_action_summary")),
                                new(
                                    "pane_authoring_studio.lifecycle_summary",
                                    PaneElementKind.PropertyEditor,
                                    "Lifecycle Guidance",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "pane.instance.lifecycle_summary"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Current instance/definition lifecycle posture, promotion guidance, and reset expectations."
                                    })
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Authoring-oriented summary of how this pane relates to reusable pane-definition truth."
                            }),
                        new(
                            "pane_authoring_studio.lifecycle_actions_section",
                            PaneElementKind.Section,
                            "Lifecycle Actions",
                            null,
                            [
                                new(
                                    "pane_authoring_studio.primary_lifecycle_actions",
                                    PaneElementKind.CommandBar,
                                    "Pane Lifecycle Actions",
                                    null,
                                    [
                                        new(
                                            "pane_authoring_studio.save_instance_only",
                                            PaneElementKind.Button,
                                            "Save Instance Only",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.pane_save_instance_only")),
                                        new(
                                            "pane_authoring_studio.save_as_new_definition",
                                            PaneElementKind.Button,
                                            "Save As New Definition",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.pane_save_as_new_definition"))
                                    ]),
                                new(
                                    "pane_authoring_studio.secondary_lifecycle_actions",
                                    PaneElementKind.CommandBar,
                                    "Definition Relationship Actions",
                                    null,
                                    [
                                        new(
                                            "pane_authoring_studio.detach_from_definition",
                                            PaneElementKind.Button,
                                            "Detach from Definition",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.pane_detach_from_definition")),
                                        new(
                                            "pane_authoring_studio.revert_to_definition",
                                            PaneElementKind.Button,
                                            "Revert to Definition",
                                            new PaneElementBindingDescriptor(
                                                PaneElementBindingTargetKind.Capability,
                                                "engine.command.pane_revert_to_definition"))
                                    ])
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "First shared-runtime command-backed lifecycle actions for the current pane instance."
                            })
                    ],
                    BehaviorSettings: new Dictionary<string, string>
                    {
                        ["description"] = "Tabbed pane-authoring surface using pane-local selectors through the same generic runtime path."
                    })
            ],
            "Seeded pane-authoring studio that validates pane-local selector binding plus richer read-only configuration/editor semantics through the shared runtime path.",
            ["seeded", "authoring", "pane", "studio"]),

        new(
            "pane.workspace_authoring_studio",
            "Workspace Authoring Studio",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "workspace_authoring_studio.header",
                    PaneElementKind.DefinitionHeader,
                    "Workspace Authoring Studio"),
                new(
                    "workspace_authoring_studio.split",
                    PaneElementKind.SplitHost,
                    "Workspace Authoring Split",
                    null,
                    [
                        new(
                            "workspace_authoring_studio.definition_group",
                            PaneElementKind.InspectorGroup,
                            "Definitions and Workspaces",
                            null,
                            [
                                new(
                                    "workspace_authoring_studio.pane_definitions",
                                    PaneElementKind.PropertyEditor,
                                    "Pane Definitions",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.pane_catalog_definition_details"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Read-only definition inventory for the current seeded pane catalog."
                                    }),
                                new(
                                    "workspace_authoring_studio.workspace_definitions",
                                    PaneElementKind.PropertyEditor,
                                    "Workspace Definitions",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.workspace_catalog_details"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Read-only workspace-definition inventory and membership breakdown."
                                    })
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Definition/configuration posture for reusable panes and reusable workspace collections."
                            }),
                        new(
                            "workspace_authoring_studio.monitor_group",
                            PaneElementKind.InspectorGroup,
                            "Operations and Structure",
                            null,
                            [
                                new(
                                    "workspace_authoring_studio.action_readiness",
                                    PaneElementKind.TaskMonitor,
                                    "Action Readiness",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.action_readiness"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Operational readiness summary for the current command surface."
                                    }),
                                new(
                                    "workspace_authoring_studio.runtime_activity",
                                    PaneElementKind.RuntimeActivityPanel,
                                    "Runtime Activity",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.RuntimeFeed,
                                        "runtime.feed.active_operations"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Monitor/feed grouping surface for current runtime/admin activity."
                                    }),
                                new(
                                    "workspace_authoring_studio.command_history",
                                    PaneElementKind.TextEditor,
                                    "Command History",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.command_history"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Recent command chronology for the current shell workspace."
                                    }),
                                new(
                                    "workspace_authoring_studio.structure",
                                    PaneElementKind.ProjectionStatusView,
                                    "Current Shell Structure",
                                    new PaneElementBindingDescriptor(
                                        PaneElementBindingTargetKind.StateSelector,
                                        "engine.state.pane_structure"),
                                    BehaviorSettings: new Dictionary<string, string>
                                    {
                                        ["description"] = "Live shell/workspace structure shown alongside authoring-oriented catalog truth."
                                    })
                            ],
                            BehaviorSettings: new Dictionary<string, string>
                            {
                                ["description"] = "Grouped monitor/feed and structure surfaces for workspace-oriented administration."
                            })
                    ],
                    BehaviorSettings: new Dictionary<string, string>
                    {
                        ["description"] = "Workspace authoring/admin surface combining reusable definition detail with grouped monitor/feed posture.",
                        ["orientation"] = "horizontal"
                    })
            ],
            "Seeded workspace-authoring studio that deepens configuration/editor and monitor/feed grouping semantics through the shared runtime.",
            ["seeded", "authoring", "workspace", "studio"])
            ,

        new(
            "pane.visual_semantics_audit",
            "Visual Semantics Audit",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "visual_semantics_audit.header",
                    PaneElementKind.DefinitionHeader,
                    "Visual Semantics Audit"),
                new(
                    "visual_semantics_audit.summary_section",
                    PaneElementKind.Section,
                    "Current Hardcoded Settings",
                    null,
                    [
                        new(
                            "visual_semantics_audit.visual_settings",
                            PaneElementKind.LabelValueField,
                            "Visual Semantics",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.visual_semantics_settings_summary")),
                        new(
                            "visual_semantics_audit.render_settings",
                            PaneElementKind.PropertyEditor,
                            "Render Surface Settings",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.render_surface_settings_summary")),
                        new(
                            "visual_semantics_audit.settings_audit",
                            PaneElementKind.TextEditor,
                            "Settings Audit",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.settings_surface_audit_summary")),
                        new(
                            "visual_semantics_audit.next_targets",
                            PaneElementKind.TextEditor,
                            "Next Targets",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.hardcoded_surface_audit_next_targets_summary"))
                    ],
                    BehaviorSettings: new Dictionary<string, string>
                    {
                        ["description"] = "First shared-runtime audit surface for halo/focus/selection semantics plus the still-hardcoded settings/viewmodel layer."
                    })
            ],
            "Seeded audit pane that surfaces still-hardcoded visual-semantics and render settings through the generic pane runtime instead of only narrative docs.",
            ["seeded", "audit", "settings", "visual-semantics"]),

        new(
            "pane.shell_hardcoded_surface_audit",
            "Shell Hardcoded Surface Audit",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "shell_hardcoded_surface_audit.header",
                    PaneElementKind.DefinitionHeader,
                    "Shell Hardcoded Surface Audit"),
                new(
                    "shell_hardcoded_surface_audit.audit_section",
                    PaneElementKind.Section,
                    "Shell Audit Findings",
                    null,
                    [
                        new(
                            "shell_hardcoded_surface_audit.parent_controls",
                            PaneElementKind.PropertyEditor,
                            "Parent Shell Controls",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.parent_shell_control_audit_summary")),
                        new(
                            "shell_hardcoded_surface_audit.main_window_chrome",
                            PaneElementKind.PropertyEditor,
                            "Main Window Shell Chrome",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.main_window_shell_chrome_audit_summary")),
                        new(
                            "shell_hardcoded_surface_audit.structure",
                            PaneElementKind.ProjectionStatusView,
                            "Current Shell Structure",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.pane_structure")),
                        new(
                            "shell_hardcoded_surface_audit.next_targets",
                            PaneElementKind.TextEditor,
                            "Next Targets",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.hardcoded_surface_audit_next_targets_summary"))
                    ],
                    BehaviorSettings: new Dictionary<string, string>
                    {
                        ["description"] = "Catalog-backed audit surface for the shell chrome and parent-pane controls that still live outside the reusable pane/workspace model."
                    })
            ],
            "Seeded audit pane that makes shell-level hardcoded surface findings visible through the shared pane runtime.",
            ["seeded", "audit", "shell", "chrome"])
            ,

        new(
            "pane.viewport_command_surface_audit",
            "Viewport Command Surface Audit",
            PaneDefinitionKind.ChildPane,
            true,
            [
                new(
                    "viewport_command_surface_audit.header",
                    PaneElementKind.DefinitionHeader,
                    "Viewport Command Surface Audit"),
                new(
                    "viewport_command_surface_audit.renderer_section",
                    PaneElementKind.Section,
                    "Renderer / Viewport Findings",
                    null,
                    [
                        new(
                            "viewport_command_surface_audit.command_inventory",
                            PaneElementKind.PropertyEditor,
                            "Viewport Command Surface Inventory",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.viewport_command_surface_audit_summary")),
                        new(
                            "viewport_command_surface_audit.registry_gap",
                            PaneElementKind.PropertyEditor,
                            "Registry Gap",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.renderer_viewport_registry_gap_summary")),
                        new(
                            "viewport_command_surface_audit.shell_boundary",
                            PaneElementKind.PropertyEditor,
                            "Shell-Native Boundary",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.shell_native_chrome_boundary_summary")),
                        new(
                            "viewport_command_surface_audit.next_targets",
                            PaneElementKind.TextEditor,
                            "Next Targets",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.renderer_parity_next_targets_summary"))
                    ]),
                new(
                    "viewport_command_surface_audit.helper_section",
                    PaneElementKind.Section,
                    "Helper / Boundary Findings",
                    null,
                    [
                        new(
                            "viewport_command_surface_audit.active_state",
                            PaneElementKind.PropertyEditor,
                            "Active Command Surface State",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.active_panel_command_surface_state_summary")),
                        new(
                            "viewport_command_surface_audit.halo_effects",
                            PaneElementKind.PropertyEditor,
                            "Renderer Halo / Group Effects",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.renderer_halo_and_group_effect_summary")),
                        new(
                            "viewport_command_surface_audit.hit_testing",
                            PaneElementKind.PropertyEditor,
                            "Projected Hit-Testing Boundary",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.projected_hit_testing_boundary_summary")),
                        new(
                            "viewport_command_surface_audit.migration_boundary",
                            PaneElementKind.TextEditor,
                            "Migration Boundary",
                            new PaneElementBindingDescriptor(
                                PaneElementBindingTargetKind.StateSelector,
                                "engine.state.renderer_migration_boundary_summary"))
                    ])
            ],
            "Seeded audit pane that surfaces the first concrete renderer/viewport capability-parity findings through the shared pane runtime.",
            ["seeded", "audit", "renderer", "viewport"])
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
                new("workspace.member.command_browser", "pane.command_browser", 4, "left"),
                new("workspace.member.scene_actions", "pane.scene_actions", 5, "right"),
                new("workspace.member.history_overview", "pane.history_overview", 6, "bottom"),
                new("workspace.member.developer_diagnostics", "pane.developer_diagnostics", 7, "top"),
                new("workspace.member.mutation_studio", "pane.mutation_studio", 8, "left"),
                new("workspace.member.pane_catalog_browser", "pane.pane_catalog_browser", 9, "right"),
                new("workspace.member.workspace_admin", "pane.workspace_admin", 10, "bottom"),
                new("workspace.member.catalog_detail_inspector", "pane.catalog_detail_inspector", 11, "right"),
                new("workspace.member.pane_management_overview", "pane.pane_management_overview", 12, "bottom"),
                new("workspace.member.catalog_navigation_studio", "pane.catalog_navigation_studio", 13, "top"),
                new("workspace.member.shell_workspace_studio", "pane.shell_workspace_studio", 14, "left"),
                new("workspace.member.pane_authoring_studio", "pane.pane_authoring_studio", 15, "right"),
                new("workspace.member.workspace_authoring_studio", "pane.workspace_authoring_studio", 16, "bottom"),
                new("workspace.member.visual_semantics_audit", "pane.visual_semantics_audit", 17, "left"),
                new("workspace.member.shell_hardcoded_surface_audit", "pane.shell_hardcoded_surface_audit", 18, "top"),
                new("workspace.member.viewport_command_surface_audit", "pane.viewport_command_surface_audit", 19, "right")
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
