using System;

namespace Constellate.Core.Scene
{
    public sealed record PanelSurfaceSemantics(
        string SurfaceKind,
        string PaneletteKind,
        int PaneletteTier)
    {
        public static PanelSurfaceSemantics Default { get; } = new("panel", "none", 0);

        public bool IsPanelette =>
            string.Equals(SurfaceKind, "panelette", StringComparison.Ordinal);

        public bool IsMetadataPanelette =>
            string.Equals(PaneletteKind, "metadata", StringComparison.Ordinal);

        public bool IsLabelPanelette =>
            string.Equals(PaneletteKind, "label", StringComparison.Ordinal);

        public static PanelSurfaceSemantics FromExplicitOrViewRef(
            string? surfaceKind,
            string? paneletteKind,
            int? paneletteTier,
            string? viewRef)
        {
            var normalizedSurfaceKind = string.IsNullOrWhiteSpace(surfaceKind)
                ? null
                : surfaceKind.Trim().ToLowerInvariant();

            if (string.Equals(normalizedSurfaceKind, "panel", StringComparison.Ordinal))
            {
                return Default;
            }

            if (string.Equals(normalizedSurfaceKind, "panelette", StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(paneletteKind) ||
                (paneletteTier ?? 0) > 0)
            {
                var normalizedPaneletteKind = string.IsNullOrWhiteSpace(paneletteKind)
                    ? "generic"
                    : paneletteKind.Trim().ToLowerInvariant();

                return new PanelSurfaceSemantics(
                    "panelette",
                    normalizedPaneletteKind,
                    Math.Max(1, paneletteTier ?? 1));
            }

            return FromViewRef(viewRef);
        }

        public static PanelSurfaceSemantics FromViewRef(string? viewRef)
        {
            if (string.IsNullOrWhiteSpace(viewRef))
            {
                return Default;
            }

            var normalized = viewRef.Trim();

            if (normalized.StartsWith("panelette.meta.", StringComparison.OrdinalIgnoreCase))
            {
                return new PanelSurfaceSemantics("panelette", "metadata", 1);
            }

            if (normalized.StartsWith("panelette.label.", StringComparison.OrdinalIgnoreCase))
            {
                return new PanelSurfaceSemantics("panelette", "label", 1);
            }

            if (normalized.StartsWith("panelette.", StringComparison.OrdinalIgnoreCase))
            {
                return new PanelSurfaceSemantics("panelette", "generic", 1);
            }

            return Default;
        }

        public string DescribeKind()
        {
            if (!IsPanelette)
            {
                return "panel";
            }

            return PaneletteKind switch
            {
                "metadata" => "panelette:metadata",
                "label" => "panelette:label",
                "generic" => "panelette",
                _ => "panelette"
            };
        }

        public string DescribeRoleLabel()
        {
            if (!IsPanelette)
            {
                return "Panel";
            }

            return PaneletteKind switch
            {
                "metadata" => "Panelette • Metadata",
                "label" => "Panelette • Label",
                _ => "Panelette"
            };
        }
    }
}
