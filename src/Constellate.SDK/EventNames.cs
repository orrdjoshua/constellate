namespace Constellate.SDK
{
    /// <summary>
    /// Current SDK event-name boundary for the reviewed engine surface.
    /// It is a meaningful public seam today, but it still reflects the present
    /// engine's mixed scope more than a fully normalized long-horizon contract taxonomy.
    /// Future cleanup should preserve the boundary while tightening scope deliberately.
    /// The current consumer-spread review also shows broad App/Core/renderer
    /// usage through VM subscriptions, Core publish/subscribe flow, and renderer
    /// interaction/view bridges, which makes this a poorer immediate churn target.
    /// </summary>
    public static class EventNames
    {
        public const string SelectionChanged = "SelectionChanged";
        public const string HoverChanged = "HoverChanged";
        public const string PanelInteraction = "PanelInteraction";
        public const string QueryCompleted = "QueryCompleted";
        public const string CommandInvoked = "CommandInvoked";
        public const string FocusChanged = "FocusChanged";
        public const string PanelFocusChanged = "PanelFocusChanged";
        public const string FocusOriginChanged = "FocusOriginChanged";
        public const string PanelAttachmentsChanged = "PanelAttachmentsChanged";
        public const string InteractionModeChanged = "InteractionModeChanged";
        public const string ResourceSurfaceBindingChanged = "ResourceSurfaceBindingChanged";
        public const string SceneChanged = "SceneChanged";
        public const string GroupChanged = "GroupChanged";
        public const string Error = "Error";

        // Renderer/Core view bridge:
        // - ViewChanged: renderer publishes user-driven camera changes (yaw/pitch/distance/target)
        // - ViewSetRequested: Core requests renderer to set a specific view (e.g., bookmark restore)
        public const string ViewChanged = "ViewChanged";
        public const string ViewSetRequested = "ViewSetRequested";

        // Node-interior navigation and expansion events
        public const string NodeEntered = "NodeEntered";
        public const string NodeExited = "NodeExited";
        public const string NodeExpanded = "NodeExpanded";
        public const string NodeCollapsed = "NodeCollapsed";
    }
}
