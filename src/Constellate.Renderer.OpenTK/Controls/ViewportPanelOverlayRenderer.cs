using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Constellate.Core.Scene;
using Constellate.Core.Messaging;
using Constellate.Renderer.OpenTK.Scene;
using OpenTK.Mathematics;

namespace Constellate.Renderer.OpenTK.Controls
{
    internal readonly record struct ActivePanelCommandSurfaceLayoutInfo(
        PanelSurfaceNode Panel,
        PanelCommandSurfaceMetadata Metadata,
        Rect PanelRect,
        Rect OverlayRect,
        Rect[] CommandRects);

    internal static class ViewportPanelOverlayRenderer
    {
        public static void DrawPanelPlaceholders(
            DrawingContext ctx,
            RenderSceneSnapshot renderSnapshot,
            Rect bounds,
            Matrix4 view,
            Matrix4 proj,
            ActivePanelCommandSurfaceState activeCommandSurface)
        {
            if (renderSnapshot.PanelSurfaces.Length == 0 || renderSnapshot.Nodes.Length == 0)
            {
                return;
            }

                var byId = renderSnapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

            foreach (var panel in renderSnapshot.PanelSurfaces)
            {
                if (string.Equals(panel.ViewRef, "__node_context__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!panel.IsVisible || !byId.TryGetValue(panel.NodeId, out var node))
                {
                    continue;
                }

                if (!TryGetProjectedPanelRect(panel, node, bounds, view, proj, out var rect, out var semantics))
                {
                    continue;
                }

                var isCommandSurfaceActive = activeCommandSurface.Matches(panel);
                if (semantics.IsPanelette)
                {
                    DrawPanelettePlaceholder(ctx, rect, panel, semantics, node.Label, isCommandSurfaceActive);
                    continue;
                }

                var fillColor = panel.IsFocused
                    ? Color.FromArgb(110, 250, 204, 21)
                    : panel.IsSelected
                        ? Color.FromArgb(92, 96, 165, 250)
                        : Color.FromArgb(72, 125, 211, 252);
                var strokeColor = panel.IsFocused
                    ? Color.FromArgb(255, 250, 204, 21)
                    : panel.IsSelected
                        ? Color.FromArgb(255, 96, 165, 250)
                        : Color.FromArgb(220, 125, 211, 252);
                var strokeThickness = panel.IsFocused ? 2.5 : (panel.IsSelected ? 2.0 : 1.5);
                var labelBrush = panel.IsFocused
                    ? Brushes.Black
                    : Brushes.White;

                ctx.DrawRectangle(
                    new SolidColorBrush(fillColor),
                    new Pen(new SolidColorBrush(strokeColor), strokeThickness),
                    rect,
                    4);

                var label = new FormattedText(
                    panel.ViewRef,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10,
                    labelBrush);

                ctx.DrawText(label, new Point(rect.X + 6, rect.Y + 4));
            }
        }

        public static bool TryHitTestPanelSurface(
            RenderSceneSnapshot renderSnapshot,
            Point point,
            Rect bounds,
            Matrix4 view,
            Matrix4 proj,
            out PanelSurfaceNode hitPanel,
            out PanelSurfaceSemantics hitSemantics)
        {
            hitPanel = default;
            hitSemantics = PanelSurfaceSemantics.Default;

            if (renderSnapshot.PanelSurfaces.Length == 0 || renderSnapshot.Nodes.Length == 0)
            {
                return false;
            }

            var byId = renderSnapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

            for (var i = renderSnapshot.PanelSurfaces.Length - 1; i >= 0; i--)
            {
                var panel = renderSnapshot.PanelSurfaces[i];
                if (!panel.IsVisible ||
                    string.Equals(panel.ViewRef, "__node_context__", StringComparison.Ordinal) ||
                    !byId.TryGetValue(panel.NodeId, out var node))
                {
                    continue;
                }

                if (!TryGetProjectedPanelRect(panel, node, bounds, view, proj, out var rect, out var semantics) ||
                    !rect.Contains(point))
                {
                    continue;
                }

                hitPanel = panel;
                hitSemantics = semantics;
                return true;
            }

            return false;
        }

        public static bool TryGetActiveCommandSurfaceLayout(
            RenderSceneSnapshot renderSnapshot,
            ActivePanelCommandSurfaceState activeCommandSurface,
            Rect bounds,
            Matrix4 view,
            Matrix4 proj,
            out ActivePanelCommandSurfaceLayoutInfo layout)
        {
            layout = default;

            if (!activeCommandSurface.HasValue ||
                renderSnapshot.PanelSurfaces.Length == 0 ||
                renderSnapshot.Nodes.Length == 0 ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return false;
            }

            var byId = renderSnapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

            foreach (var candidate in renderSnapshot.PanelSurfaces)
            {
                if (!string.Equals(candidate.NodeId, activeCommandSurface.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(candidate.ViewRef, activeCommandSurface.ViewRef, StringComparison.Ordinal) ||
                    !candidate.IsVisible ||
                    candidate.CommandSurface is not { HasCommands: true } candidateMetadata ||
                    !byId.TryGetValue(candidate.NodeId, out var node) ||
                    !TryGetProjectedPanelRect(candidate, node, bounds, view, proj, out var candidateRect, out var semantics) ||
                    !semantics.IsMetadataPanelette)
                {
                    continue;
                }

                const double overlayWidth = 264.0;
                const double headerHeight = 48.0;
                const double itemHeight = 26.0;
                const double overlayMargin = 10.0;
                const double overlayInset = 8.0;

                var overlayHeight = headerHeight + (candidateMetadata.Commands.Count * itemHeight) + overlayInset;
                var overlayX = candidateRect.Right + overlayMargin;
                if (overlayX + overlayWidth > bounds.Right - overlayInset)
                {
                    overlayX = candidateRect.X - overlayWidth - overlayMargin;
                }

                overlayX = Math.Max(bounds.X + overlayInset, overlayX);
                var maxOverlayY = Math.Max(bounds.Y + overlayInset, bounds.Bottom - overlayHeight - overlayInset);
                var overlayY = Math.Clamp(candidateRect.Y, bounds.Y + overlayInset, maxOverlayY);

                var overlayRect = new Rect(overlayX, overlayY, overlayWidth, overlayHeight);
                var commandRectWidth = overlayWidth - (overlayInset * 2.0);
                var commandRects = new Rect[candidateMetadata.Commands.Count];

                for (var index = 0; index < candidateMetadata.Commands.Count; index++)
                {
                    commandRects[index] = new Rect(
                        overlayX + overlayInset,
                        overlayY + headerHeight + (index * itemHeight),
                        commandRectWidth,
                        itemHeight - 4.0);
                }

                activeCommandSurface.Clamp(candidateMetadata.Commands.Count);
                layout = new ActivePanelCommandSurfaceLayoutInfo(
                    candidate,
                    candidateMetadata,
                    candidateRect,
                    overlayRect,
                    commandRects);
                return true;
            }

            activeCommandSurface.Clear();
            return false;
        }

        public static void DrawActiveCommandSurfaceOverlay(
            DrawingContext ctx,
            ActivePanelCommandSurfaceLayoutInfo layout,
            int selectedIndex)
        {
            GetCommandSurfaceOverlayPalette(
                out var overlayFillColor,
                out var overlayStrokeColor,
                out var titleBrush,
                out var identityBrush,
                out var instructionsBrush,
                out var selectedRowFillColor,
                out var selectedRowStrokeColor,
                out var normalRowFillColor,
                out var normalRowStrokeColor);

            ctx.DrawRectangle(
                new SolidColorBrush(overlayFillColor),
                new Pen(new SolidColorBrush(overlayStrokeColor), 1.4),
                layout.OverlayRect,
                6);

            var title = new FormattedText(
                "Context Surface",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                titleBrush);
            var identity = new FormattedText(
                layout.Metadata.DescribeIdentity(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                identityBrush);
            var instructions = new FormattedText(
                "Right-click cycles • Enter/Space runs • Esc closes",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9,
                instructionsBrush);

            ctx.DrawText(title, new Point(layout.OverlayRect.X + 8, layout.OverlayRect.Y + 7));
            ctx.DrawText(identity, new Point(layout.OverlayRect.X + 8, layout.OverlayRect.Y + 19));
            ctx.DrawText(instructions, new Point(layout.OverlayRect.X + 8, layout.OverlayRect.Y + 32));

            var clampedSelectedIndex = Math.Clamp(selectedIndex, 0, layout.Metadata.Commands.Count - 1);

            for (var i = 0; i < layout.CommandRects.Length; i++)
            {
                var isSelected = i == clampedSelectedIndex;
                var rowRect = layout.CommandRects[i];

                ctx.DrawRectangle(
                    new SolidColorBrush(isSelected ? selectedRowFillColor : normalRowFillColor),
                    new Pen(new SolidColorBrush(isSelected ? selectedRowStrokeColor : normalRowStrokeColor), isSelected ? 1.5 : 1.0),
                    rowRect,
                    4);

                var commandText = new FormattedText(
                    layout.Metadata.Commands[i].DisplayLabel,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10,
                    Brushes.White);

                ctx.DrawText(commandText, new Point(rowRect.X + 8, rowRect.Y + 4));
            }
        }

        private static bool TryGetProjectedPanelRect(
            PanelSurfaceNode panel,
            RenderNode node,
            Rect bounds,
            Matrix4 view,
            Matrix4 proj,
            out Rect rect,
            out PanelSurfaceSemantics semantics)
        {
            rect = default;
            semantics = panel.Semantics ?? PanelSurfaceSemantics.FromViewRef(panel.ViewRef);

            var world = new Vector4(
                node.Position.X + panel.LocalOffset.X,
                node.Position.Y + panel.LocalOffset.Y,
                node.Position.Z + panel.LocalOffset.Z,
                1f);

            var clip = world * view * proj;
            if (clip.W <= 0.0001f)
            {
                return false;
            }

            var ndc = clip.Xyz / clip.W;
            if (ndc.Z < -1.2f || ndc.Z > 1.2f)
            {
                return false;
            }

            var screenX = bounds.X + ((ndc.X + 1f) * 0.5 * bounds.Width);
            var screenY = bounds.Y + ((1f - (ndc.Y + 1f) * 0.5) * bounds.Height);

            var width = Math.Max(36.0, panel.Size.X * 60.0);
            var height = Math.Max(20.0, panel.Size.Y * 36.0);
            if (semantics.IsMetadataPanelette)
            {
                width = Math.Max(width, 132.0);
                height = Math.Max(height, 58.0);
            }
            else if (semantics.IsLabelPanelette)
            {
                width = Math.Max(width, 104.0);
                height = Math.Max(height, 28.0);
            }

            var anchorOffset = GetAnchorOffset(panel.Anchor, width, height);
            rect = new Rect(
                screenX + anchorOffset.X,
                screenY + anchorOffset.Y,
                width,
                height);

            return true;
        }

        private static Point GetAnchorOffset(string? anchor, double width, double height)
        {
            var normalized = string.IsNullOrWhiteSpace(anchor)
                ? "center"
                : anchor.Trim().ToLowerInvariant();

            return normalized switch
            {
                "center" => new Point(-(width / 2.0), -(height / 2.0)),
                "top" => new Point(-(width / 2.0), 0.0),
                "bottom" => new Point(-(width / 2.0), -height),
                "left" => new Point(0.0, -(height / 2.0)),
                "right" => new Point(-width, -(height / 2.0)),
                "top-left" => new Point(0.0, 0.0),
                "topleft" => new Point(0.0, 0.0),
                "top-right" => new Point(-width, 0.0),
                "topright" => new Point(-width, 0.0),
                "bottom-left" => new Point(0.0, -height),
                "bottomleft" => new Point(0.0, -height),
                "bottom-right" => new Point(-width, -height),
                "bottomright" => new Point(-width, -height),
                _ => new Point(-(width / 2.0), -(height / 2.0))
            };
        }

        private static void DrawPanelettePlaceholder(
            DrawingContext ctx,
            Rect rect,
            PanelSurfaceNode panel,
            PanelSurfaceSemantics semantics,
            string nodeLabel,
            bool isCommandSurfaceActive)
        {
            if (semantics.IsLabelPanelette)
            {
                GetLabelPanelettePalette(panel.IsFocused, panel.IsSelected, out var labelFillColor, out var labelStrokeColor, out var labelTextBrush);

                var labelText = new FormattedText(
                    nodeLabel,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10,
                    labelTextBrush);

                ctx.DrawRectangle(
                    new SolidColorBrush(labelFillColor),
                    new Pen(new SolidColorBrush(labelStrokeColor), panel.IsFocused ? 2.1 : 1.4),
                    rect,
                    14);

                ctx.DrawText(
                    labelText,
                    new Point(
                        rect.X + Math.Max(8.0, (rect.Width - labelText.WidthIncludingTrailingWhitespace) / 2.0),
                        rect.Y + Math.Max(5.0, (rect.Height - labelText.Height) / 2.0 - 1.0)));
                return;
            }

            GetMetadataPanelettePalette(panel.IsFocused, panel.IsSelected, out var fillColor, out var strokeColor, out var accentColor, out var headerBrush, out var bodyBrush);

            ctx.DrawRectangle(
                new SolidColorBrush(fillColor),
                new Pen(new SolidColorBrush(strokeColor), panel.IsFocused ? 2.4 : 1.6),
                rect,
                6);

            var accentRect = new Rect(rect.X, rect.Y, rect.Width, 5);
            ctx.DrawRectangle(new SolidColorBrush(accentColor), null, accentRect, 6);

            var roleText = new FormattedText(
                semantics.DescribeRoleLabel(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9,
                headerBrush);

            var titleText = new FormattedText(
                nodeLabel,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                headerBrush);

            var detailLine = panel.CommandSurface is { HasCommands: true } commandSurface
                ? isCommandSurfaceActive
                    ? $"Context Surface Open • {commandSurface.DescribeIdentity()} • {commandSurface.Commands.Count} commands"
                    : $"Commands • {commandSurface.SurfaceName}/{commandSurface.SurfaceGroup} • {commandSurface.SurfaceSource} • {commandSurface.DescribeCommandsSummary(2)}"
                : panel.IsFocused
                    ? "Focused in-world metadata surface"
                    : panel.IsSelected
                        ? "Selected in-world metadata surface"
                        : "Tier 1 panelette placeholder";

            var detailText = new FormattedText(
                detailLine,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9,
                bodyBrush);

            ctx.DrawText(roleText, new Point(rect.X + 8, rect.Y + 9));
            ctx.DrawText(titleText, new Point(rect.X + 8, rect.Y + 23));
            ctx.DrawText(detailText, new Point(rect.X + 8, rect.Y + 39));
        }

        private static void GetLabelPanelettePalette(
            bool isFocused,
            bool isSelected,
            out Color fill,
            out Color stroke,
            out IBrush textBrush)
        {
            var settings = EngineServices.Settings;
            var intensity = Math.Clamp(settings.PaneletteBackgroundIntensity, 0.25f, 2f);

            static byte ScaleAlpha(byte baseAlpha, float intensity)
            {
                var scaled = (int)(baseAlpha * intensity);
                return (byte)Math.Clamp(scaled, 5, 255);
            }

            if (isFocused)
            {
                fill = Color.FromArgb(ScaleAlpha(226, intensity), 245, 208, 96);
                stroke = Color.FromArgb(ScaleAlpha(255, intensity), 250, 204, 21);
                textBrush = Brushes.Black;
                return;
            }

            if (isSelected)
            {
                fill = Color.FromArgb(ScaleAlpha(214, intensity), 72, 109, 158);
                stroke = Color.FromArgb(ScaleAlpha(255, intensity), 125, 211, 252);
                textBrush = Brushes.White;
                return;
            }

            fill = Color.FromArgb(ScaleAlpha(206, intensity), 31, 51, 66);
            stroke = Color.FromArgb(ScaleAlpha(228, intensity), 152, 184, 208);
            textBrush = Brushes.White;
        }

        private static void GetMetadataPanelettePalette(
            bool isFocused,
            bool isSelected,
            out Color fill,
            out Color stroke,
            out Color accent,
            out IBrush headerBrush,
            out IBrush bodyBrush)
        {
            var settings = EngineServices.Settings;
            var intensity = Math.Clamp(settings.PaneletteBackgroundIntensity, 0.25f, 2f);

            static byte ScaleAlpha(byte baseAlpha, float intensity)
            {
                var scaled = (int)(baseAlpha * intensity);
                return (byte)Math.Clamp(scaled, 5, 255);
            }

            if (isFocused)
            {
                fill = Color.FromArgb(ScaleAlpha(218, intensity), 245, 208, 96);
                stroke = Color.FromArgb(ScaleAlpha(255, intensity), 250, 204, 21);
                accent = Color.FromArgb(ScaleAlpha(255, intensity), 250, 204, 21);
                headerBrush = Brushes.Black;
                bodyBrush = Brushes.Black;
                return;
            }

            if (isSelected)
            {
                fill = Color.FromArgb(ScaleAlpha(206, intensity), 72, 109, 158);
                stroke = Color.FromArgb(ScaleAlpha(255, intensity), 125, 211, 252);
                accent = Color.FromArgb(ScaleAlpha(255, intensity), 125, 211, 252);
                headerBrush = Brushes.White;
                bodyBrush = new SolidColorBrush(Color.FromArgb(230, 219, 230, 241));
                return;
            }

            fill = Color.FromArgb(ScaleAlpha(198, intensity), 25, 34, 44);
            stroke = Color.FromArgb(ScaleAlpha(220, intensity), 138, 162, 184);
            accent = Color.FromArgb(ScaleAlpha(255, intensity), 122, 208, 167);
            headerBrush = Brushes.White;
            bodyBrush = new SolidColorBrush(Color.FromArgb(230, 219, 230, 241));
        }

        private static void GetCommandSurfaceOverlayPalette(
            out Color overlayFill,
            out Color overlayStroke,
            out IBrush titleBrush,
            out IBrush identityBrush,
            out IBrush instructionsBrush,
            out Color selectedRowFill,
            out Color selectedRowStroke,
            out Color normalRowFill,
            out Color normalRowStroke)
        {
            var settings = EngineServices.Settings;
            var opacity = Math.Clamp(settings.CommandSurfaceOverlayOpacity, 0.25f, 2f);

            static byte ScaleAlpha(byte baseAlpha, float opacity)
            {
                var scaled = (int)(baseAlpha * opacity);
                return (byte)Math.Clamp(scaled, 15, 255);
            }

            overlayFill = Color.FromArgb(ScaleAlpha(236, opacity), 15, 21, 30);
            overlayStroke = Color.FromArgb(220, 125, 211, 252);
            titleBrush = Brushes.White;
            identityBrush = new SolidColorBrush(Color.FromArgb(255, 190, 225, 255));
            instructionsBrush = new SolidColorBrush(Color.FromArgb(220, 184, 198, 213));
            selectedRowFill = Color.FromArgb(ScaleAlpha(172, opacity), 72, 109, 158);
            selectedRowStroke = Color.FromArgb(255, 125, 211, 252);
            normalRowFill = Color.FromArgb(ScaleAlpha(118, opacity), 26, 34, 44);
            normalRowStroke = Color.FromArgb(180, 90, 112, 136);
        }
    }
}
