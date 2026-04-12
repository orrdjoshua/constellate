using System;
using System.Collections.Generic;

namespace Constellate.Core.Interaction
{
    /// <summary>
    /// Logical interaction actions used by the engine for keyboard-driven behavior.
    /// This is an engine-level contract and intentionally avoids UI-framework types.
    /// </summary>
    public enum InteractionAction
    {
        // Global/back-out behavior
        Escape,

        // Focus / selection traversal
        CycleFocusNext,
        CycleFocusPrevious,
        SelectFocusedReplace,
        SelectFocusedAdd,

        // History
        Undo,

        // Interaction modes
        SetModeNavigate,
        SetModeMarquee,
        SetModeMove,

        // Context-surface navigation and invocation
        ContextSurfaceMoveSelection,
        ContextSurfaceInvokeSelection,
        OpenFocusedPaneletteContextSurface,

        // Move-mode / transform helpers
        MoveSelectionLeft,
        MoveSelectionRight,
        MoveSelectionUp,
        MoveSelectionDown,
        MoveSelectionDepthForward,
        MoveSelectionDepthBackward
    }

    /// <summary>
    /// Engine-level description of a key gesture (key plus modifier posture).
    /// Keys are named symbolically (\"Esc\", \"Tab\", \"Enter\", \"Left\", \"PageUp\", \"F10\" etc.)
    /// so frontend layers can map from their concrete key enums without Core depending on them.
    /// </summary>
    public readonly record struct KeyGestureSpec(
        string Key,
        bool Ctrl = false,
        bool Shift = false,
        bool Alt = false)
    {
        public override string ToString()
        {
            var parts = new List<string>(4);
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(Key);
            return string.Join("+", parts);
        }
    }

    /// <summary>
    /// One default binding from a logical key gesture to an engine-level interaction action.
    /// Scope indicates the contextual domain where the binding applies
    /// (for example \"global\", \"viewport\", \"move_mode\", \"context_surface\").
    /// </summary>
    public sealed record KeybindingEntry(
        KeyGestureSpec Gesture,
        InteractionAction Action,
        string Scope,
        string Notes);

    /// <summary>
    /// Central catalogue of current default keybindings and modifier semantics.
    /// This captures the behavior described in DESIGN-040 so that viewport/input layers
    /// can consult a single engine-level model instead of hardcoding scattered bindings.
    /// </summary>
    public static class KeybindingModel
    {
        private static readonly KeybindingEntry[] DefaultBindings =
        {
            // 1) Global / back-out behavior (Esc)
            new(
                new KeyGestureSpec("Esc"),
                InteractionAction.Escape,
                "global",
                "Backs out of the deepest active context: closes background/link/group/context surfaces, cancels active move drag, otherwise clears selection."),

            // 2) Focus traversal (Tab / Shift+Tab)
            new(
                new KeyGestureSpec("Tab"),
                InteractionAction.CycleFocusNext,
                "viewport",
                "Cycle focus forward across nodes (order derived from GetOrderedRenderNodes)."),
            new(
                new KeyGestureSpec("Tab", Shift: true),
                InteractionAction.CycleFocusPrevious,
                "viewport",
                "Cycle focus backward across nodes."),

            // 3) Select focused (Enter / Space, with optional Shift additive)
            new(
                new KeyGestureSpec("Enter"),
                InteractionAction.SelectFocusedReplace,
                "viewport",
                "Select the focused node; replace selection when no Shift modifier is pressed."),
            new(
                new KeyGestureSpec("Enter", Shift: true),
                InteractionAction.SelectFocusedAdd,
                "viewport",
                "Select the focused node additively (do not clear existing selection)."),
            new(
                new KeyGestureSpec("Space"),
                InteractionAction.SelectFocusedReplace,
                "viewport",
                "When not used as a depth-drag modifier, behaves like Enter: select focused node with replace semantics."),
            new(
                new KeyGestureSpec("Space", Shift: true),
                InteractionAction.SelectFocusedAdd,
                "viewport",
                "When not used as a depth-drag modifier, behaves like Shift+Enter: additive selection from focus."),

            // 4) Undo
            new(
                new KeyGestureSpec("Z", Ctrl: true),
                InteractionAction.Undo,
                "global",
                "Undo last undoable command via the EngineScene undo stack."),

            // 5) Interaction mode hotkeys (N / Q / M)
            new(
                new KeyGestureSpec("N"),
                InteractionAction.SetModeNavigate,
                "global",
                "Set interaction mode to Navigate via SetInteractionMode(\"navigate\")."),
            new(
                new KeyGestureSpec("Q"),
                InteractionAction.SetModeMarquee,
                "global",
                "Set interaction mode to Marquee via SetInteractionMode(\"marquee\")."),
            new(
                new KeyGestureSpec("M"),
                InteractionAction.SetModeMove,
                "global",
                "Set interaction mode to Move via SetInteractionMode(\"move\")."),

            // 6) Context-surface keyboard navigation (Up/Down + Enter/Space when overlay is open)
            new(
                new KeyGestureSpec("Up"),
                InteractionAction.ContextSurfaceMoveSelection,
                "context_surface",
                "Move active command-row selection up inside an open background or panelette command-surface overlay."),
            new(
                new KeyGestureSpec("Down"),
                InteractionAction.ContextSurfaceMoveSelection,
                "context_surface",
                "Move active command-row selection down inside an open background or panelette command-surface overlay."),
            new(
                new KeyGestureSpec("Enter"),
                InteractionAction.ContextSurfaceInvokeSelection,
                "context_surface",
                "Invoke the currently selected command-row in an open background or panelette command-surface overlay."),
            new(
                new KeyGestureSpec("Space"),
                InteractionAction.ContextSurfaceInvokeSelection,
                "context_surface",
                "Invoke the currently selected command-row in an open background or panelette command-surface overlay."),

            // 7) Keyboard-triggered panelette context surface (Shift+F10)
            new(
                new KeyGestureSpec("F10", Shift: true),
                InteractionAction.OpenFocusedPaneletteContextSurface,
                "viewport",
                "Open the node-associated command surface for the focused metadata panelette using the same overlay pipeline as right-click."),

            // 8) Move-mode keyboard nudges (arrow keys + PageUp/PageDown)
            new(
                new KeyGestureSpec("Left"),
                InteractionAction.MoveSelectionLeft,
                "move_mode",
                "Nudge selected/focused nodes left in the camera view plane (negative right vector) via batch UpdateEntities."),
            new(
                new KeyGestureSpec("Right"),
                InteractionAction.MoveSelectionRight,
                "move_mode",
                "Nudge selected/focused nodes right in the camera view plane (positive right vector) via batch UpdateEntities."),
            new(
                new KeyGestureSpec("Up"),
                InteractionAction.MoveSelectionUp,
                "move_mode",
                "Nudge selected/focused nodes up in the camera view plane (positive up vector) via batch UpdateEntities."),
            new(
                new KeyGestureSpec("Down"),
                InteractionAction.MoveSelectionDown,
                "move_mode",
                "Nudge selected/focused nodes down in the camera view plane (negative up vector) via batch UpdateEntities."),
            new(
                new KeyGestureSpec("PageUp"),
                InteractionAction.MoveSelectionDepthForward,
                "move_mode",
                "Nudge selected/focused nodes toward the camera along the view-forward axis via batch UpdateEntities."),
            new(
                new KeyGestureSpec("PageDown"),
                InteractionAction.MoveSelectionDepthBackward,
                "move_mode",
                "Nudge selected/focused nodes away from the camera along the view-forward axis via batch UpdateEntities.")
        };

        /// <summary>
        /// Returns the current default keybinding set. This is a descriptive catalogue
        /// of how the engine expects keyboard input to behave; frontends can map their
        /// concrete key enums to <see cref=\"KeyGestureSpec\"/> values and then route
        /// actions through the command/event bus using these logical actions.
        /// </summary>
        public static IReadOnlyList<KeybindingEntry> GetDefaultBindings() => DefaultBindings;

        /// <summary>
        /// Enumerate bindings for a particular logical scope (for example \"global\",
        /// \"viewport\", \"move_mode\", \"context_surface\").
        /// </summary>
        public static IEnumerable<KeybindingEntry> GetBindingsForScope(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                yield break;
            }

            var normalized = scope.Trim();
            foreach (var entry in DefaultBindings)
            {
                if (string.Equals(entry.Scope, normalized, StringComparison.Ordinal))
                {
                    yield return entry;
                }
            }
        }
    }
}
