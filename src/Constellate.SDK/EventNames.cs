namespace Constellate.SDK
{
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
