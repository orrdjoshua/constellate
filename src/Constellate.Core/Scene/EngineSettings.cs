using System;

namespace Constellate.Core.Scene
{
    /// <summary>
    /// Minimal engine-level UI/interaction settings.
    /// This is intentionally small; future designer-mode work can
    /// grow this into a richer configurable model.
    /// </summary>
    public sealed class EngineSettings
    {
        /// <summary>
        /// When true, mouse leaving all meaningful entities in the 3D viewport
        /// (no panel or node hit) will clear focus back to meaningful null/background
        /// focus. When false, the last mouse-focused target is preserved until
        /// explicitly changed by keyboard or other commands.
        /// </summary>
        public bool MouseLeaveClearsFocus { get; set; } = true;

        /// <summary>
        /// Global opacity for group-volume overlays in the renderer. This is a
        /// first appearance-related setting intended for designer-mode tuning of
        /// group visibility, expressed as a 0–1 alpha fraction.
        /// </summary>
        public float GroupOverlayOpacity { get; set; } = 0.20f;

        /// <summary>
        /// Global opacity multiplier for node highlight overlays (focus/selection)
        /// drawn in the viewport. This controls the alpha of the in-world 2D
        /// node overlays rendered on top of volumetric node bodies, expressed as
        /// a 0–1 alpha fraction, where 1.0 preserves the current default and
        /// lower values make the overlays more subtle.
        /// </summary>
        public float NodeHighlightOpacity { get; set; } = 1.0f;

        /// <summary>
        /// Global radius multiplier for focus halos drawn around nodes in the
        /// viewport. Applied on top of the base radius derived from node size.
        /// </summary>
        public float NodeFocusHaloRadiusMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Global radius multiplier for selection halos drawn around nodes in
        /// the viewport. Applied on top of the base radius derived from node
        /// size.
        /// </summary>
        public float NodeSelectionHaloRadiusMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Background rendering mode for the 3D viewport. Currently supports
        /// "solid" and "gradient" (string comparison is case-insensitive).
        /// </summary>
        public string BackgroundMode { get; set; } = "gradient";

        /// <summary>
        /// Global node halo mode for focus/selection highlights. Supported
        /// values are \"2d\", \"3d\", and \"both\" (case-insensitive). \"2d\"
        /// renders only the existing 2D screen-space halos, \"3d\" renders
        /// only volumetric halo meshes around nodes, and \"both\" renders
        /// both 2D overlays and 3D halo volumes together.
        /// </summary>
        public string NodeHaloMode { get; set; } = "2d";

        /// <summary>
        /// Global node halo volume mode when 3D halos are enabled. Supported
        /// values are \"hollow\" and \"occluding\" (case-insensitive). \"hollow\"
        /// keeps the halo volume depth-tested so the node body remains visually
        /// intact with a rim-like field; \"occluding\" disables depth test for
        /// the halo draw so the glow overlays the node and nearby geometry.
        /// </summary>
        public string NodeHaloOcclusionMode { get; set; } = "hollow";

        /// <summary>
        /// Base background color (hex string) used for solid mode and as a
        /// reference color for animated gradients.
        /// </summary>
        public string BackgroundBaseColor { get; set; } = "#050911";

        /// <summary>
        /// Top color for gradient background mode (hex string).
        /// </summary>
        public string BackgroundTopColor { get; set; } = "#0B1623";

        /// <summary>
        /// Bottom color for gradient background mode (hex string).
        /// </summary>
        public string BackgroundBottomColor { get; set; } = "#050911";

        /// <summary>
        /// Background animation mode. Current expected values are "off" and
        /// "slowlerp" (string comparison is case-insensitive); other values
        /// are treated as "off".
        /// </summary>
        public string BackgroundAnimationMode { get; set; } = "off";

        /// <summary>
        /// Speed multiplier for background animation modes. Interpreted as a
        /// non-negative scalar where higher values advance animation phase
        /// faster. Values are typically in the range 0–2.
        /// </summary>
        public float BackgroundAnimationSpeed { get; set; } = 0.25f;

        /// <summary>
        /// Global default link stroke thickness multiplier. Renderers may combine
        /// this with per-link appearance weight or thickness to derive the final
        /// on-screen stroke width.
        /// </summary>
        public float LinkStrokeThickness { get; set; } = 1.5f;

        /// <summary>
        /// Global opacity multiplier for link strokes. Expressed as a 0–1 alpha
        /// fraction and combined with per-link appearance opacity where present.
        /// </summary>
        public float LinkOpacity { get; set; } = 0.86f;

        /// <summary>
        /// Global intensity multiplier for panelette background/outline opacity.
        /// This controls how strong label/metadata panelettes appear in the
        /// viewport overlay layer. Values below 1.0 make them more subtle,
        /// values above 1.0 make them more prominent. Typical range is 0.25–2.0.
        /// </summary>
        public float PaneletteBackgroundIntensity { get; set; } = 1.0f;

        /// <summary>
        /// Global opacity multiplier for in-world command-surface overlays
        /// (background/node/link/group context surfaces). Values below 1.0 make
        /// the overlay and its rows more transparent; values above 1.0 increase
        /// their opacity. Typical range is 0.25–2.0. This does not affect the
        /// underlying node/group/link visuals themselves—only the 2D overlay.
        /// </summary>
        public float CommandSurfaceOverlayOpacity { get; set; } = 1.0f;
    }
}
