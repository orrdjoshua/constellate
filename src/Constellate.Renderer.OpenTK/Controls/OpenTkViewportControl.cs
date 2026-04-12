using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Text.Json;
using Constellate.Core.Messaging;
using Constellate.Renderer.OpenTK.Scene;
using Constellate.Core.Scene;
using Constellate.Renderer.OpenTK.Diagnostics;
using Constellate.SDK;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using GLPixelFormat = global::OpenTK.Graphics.OpenGL4.PixelFormat;
using GLPixelType = global::OpenTK.Graphics.OpenGL4.PixelType;
using V2i = global::OpenTK.Mathematics.Vector2i;
using NVec3 = System.Numerics.Vector3;

namespace Constellate.Renderer.OpenTK.Controls
{
    /// OpenTK-backed viewport control (offscreen FBO -> CPU readback -> WriteableBitmap).
    /// GL path is preferred; software fallback remains available.
    public partial class OpenTkViewportControl : Control
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            IncludeFields = true
        };

        private readonly DispatcherTimer _timer;
        private double _angleDeg;

        private readonly bool _preferGl =
            !string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL"), "0", StringComparison.OrdinalIgnoreCase);

#if DEBUG
        private readonly bool _diagVerbose =
            !string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_DIAG"), "0", StringComparison.OrdinalIgnoreCase);
#else
        private readonly bool _diagVerbose =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_DIAG"), "1", StringComparison.OrdinalIgnoreCase);
#endif

        private readonly bool _selfTest =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_SELFTEST"), "1", StringComparison.OrdinalIgnoreCase);

        private readonly bool _noDepth =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_NODEPTH"), "1", StringComparison.OrdinalIgnoreCase);

        private readonly bool _noClear =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_NOCLEAR"), "1", StringComparison.OrdinalIgnoreCase);

        private readonly bool _forceIdentity =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_FORCEIDENTITY"), "1", StringComparison.OrdinalIgnoreCase);

        private readonly bool _scissorMark =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_MARK"), "1", StringComparison.OrdinalIgnoreCase);

        private readonly bool _transposeUniform =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_TRANSPOSE"), "1", StringComparison.OrdinalIgnoreCase);

        // Default path is M*V*P. Setting MULORDER=0 forces diagnostic P*V*M behavior.
        private readonly bool _forcePvmOrder =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_MULORDER"), "0", StringComparison.OrdinalIgnoreCase);

        private GameWindow? _glWindow;
        private bool _glInitialized;
        private bool _glFailed;

        private int _fbo;
        private int _colorTex;
        private int _depthRbo;
        private int _texW;
        private int _texH;

        private byte[]? _readbackRaw;
        private byte[]? _readbackFlipped;
        private WriteableBitmap? _glBitmap;

        private int _program;
        private int _vao;
        private int _vbo;
        private int _locMvp;
        private int _locColor = -1;

        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private IDisposable? _viewSetSubscription;

        private const double ClickDragThreshold = 4.0;

        private enum PointerInteractionMode
        {
            None,
            Orbit,
            Pan
        }

        private struct OrbitCamera
        {
            public float Yaw;
            public float Pitch;
            public float Distance;
            public Vector3 Target;

            public void Clamp()
            {
                Pitch = Math.Clamp(Pitch, -1.2f, 1.2f);
                Distance = Math.Clamp(Distance, 0.25f, 10f);

                if (Yaw > MathF.PI)
                {
                    Yaw -= 2 * MathF.PI;
                }

                if (Yaw < -MathF.PI)
                {
                    Yaw += 2 * MathF.PI;
                }
            }
        }

        private OrbitCamera _cam = new()
        {
            Yaw = MathF.PI / 2f,
            Pitch = 0f,
            Distance = 2.0f,
            Target = Vector3.Zero
        };

        private PointerInteractionMode _interactionMode;
        private bool _leftPointerPending;
        private bool _pendingAdditiveSelection;
        private bool _pointerMovedBeyondThreshold;
        private Point _lastPt;
        private Point _pointerPressedPt;
         private string? _pressedNodeId;
         private string? _pressedGroupId;
        private bool _isMarqueeSelecting;
        private Point _marqueeStartPt;
        private Point _marqueeCurrentPt;
        private bool _isDepthMoveDrag;
        private bool _spaceDepthDragModifier;
        private bool _isMoveDragging;
        private Point _moveDragStartPt;
        private readonly Dictionary<string, Vector3> _moveDragStartPositions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> _moveDragPreviewPositions = new(StringComparer.Ordinal);
        private readonly ActivePanelCommandSurfaceState _activeCommandSurface = new();
        private bool _hasEphemeralNodeCommandSurface;
        private PanelSurfaceNode _ephemeralNodePanel;
        private PanelCommandSurfaceMetadata? _ephemeralNodeMetadata;

        public OpenTkViewportControl()
        {
            Focusable = true;
            IsHitTestVisible = true;
            IsTabStop = true;

            AddHandler(PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
            AddHandler(PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
            AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, handledEventsToo: true);
            AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: true);
            AddHandler(KeyUpEvent, OnKeyUp, handledEventsToo: true);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) =>
            {
                _angleDeg = (_angleDeg + 1.0) % 360.0;

                if (_preferGl && !_glFailed)
                {
                    try
                    {
                        RenderGlFrame();
                    }
                    catch (Exception ex)
                    {
                        _glFailed = true;
                        Debug.WriteLine($"[OpenTkViewportControl] GL path failed: {ex.Message}");
                        TeardownGl();
                    }
                }

                InvalidateVisual();
            };
            _timer.Start();

            // Subscribe to view-set requests (e.g., bookmark restore)
            _viewSetSubscription = EngineServices.EventBus.Subscribe(EventNames.ViewSetRequested, envelope =>
            {
                try
                {
                    if (envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }

                    float yaw = _cam.Yaw, pitch = _cam.Pitch, distance = _cam.Distance;
                    float tx = _cam.Target.X, ty = _cam.Target.Y, tz = _cam.Target.Z;

                    TryGetFloat(payload, "yaw", ref yaw);
                    TryGetFloat(payload, "pitch", ref pitch);
                    TryGetFloat(payload, "distance", ref distance);

                    if (payload.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.Object)
                    {
                        TryGetFloat(t, "x", ref tx);
                        TryGetFloat(t, "y", ref ty);
                        TryGetFloat(t, "z", ref tz);
                    }

                    _cam.Yaw = yaw;
                    _cam.Pitch = pitch;
                    _cam.Distance = distance;
                    _cam.Target = new Vector3(tx, ty, tz);
                    _cam.Clamp();

                    InvalidateVisual();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);

            var bounds = new Rect(Bounds.Size);
            DrawBackground(ctx, bounds);

            if (_preferGl && !_glFailed && _glBitmap is not null)
            {
                var src = new Rect(0, 0, _glBitmap.PixelSize.Width, _glBitmap.PixelSize.Height);
                ctx.DrawImage(_glBitmap, src, bounds);
            }
            else
            {
                DrawSoftwareTriangle(ctx, bounds);
            }
            DrawLinkPlaceholders(ctx, bounds);
            DrawNodeInteractionOverlays(ctx, bounds);
            DrawGroupPlaceholders(ctx, bounds);
            DrawPanelPlaceholders(ctx, bounds);
            DrawBackgroundCommandSurfaceOverlay(ctx, bounds);
            DrawActiveCommandSurfaceOverlay(ctx, bounds);

            if (_isMarqueeSelecting)
            {
                DrawMarqueeOverlay(ctx);
            }
            
            if (_diagVerbose)
            {
                DrawHud(ctx, bounds);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            try
            {
                _timer.Stop();
            }
            catch
            {
            }

            try
            {
                _viewSetSubscription?.Dispose();
            }
            catch
            {
            }
            _viewSetSubscription = null;

            TeardownGl();
            _glBitmap?.Dispose();
            _glBitmap = null;
        }

        private void DrawSoftwareTriangle(DrawingContext ctx, Rect bounds)
        {
            var rect = bounds.Deflate(20);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            var center = rect.Center;
            var radius = Math.Min(rect.Width, rect.Height) / 3.0;

            Point P(double deg)
            {
                var radians = deg * Math.PI / 180.0;
                return new Point(
                    center.X + Math.Cos(radians) * radius,
                    center.Y + Math.Sin(radians) * radius);
            }

            var p1 = P(_angleDeg);
            var p2 = P(_angleDeg + 120);
            var p3 = P(_angleDeg + 240);

            var geometry = new StreamGeometry();
            using (var gc = geometry.Open())
            {
                gc.BeginFigure(p1, isFilled: true);
                gc.LineTo(p2);
                gc.LineTo(p3);
                gc.EndFigure(isClosed: true);
            }

            var fill = new SolidColorBrush(Color.FromArgb(255, 34, 139, 34));
            var stroke = new Pen(Brushes.White, 2);
            ctx.DrawGeometry(fill, stroke, geometry);
        }

        private void DrawBackground(DrawingContext ctx, Rect bounds)
        {
            try
            {
                var settings = EngineServices.Settings;

                var baseColor = ParseHexColorOrDefault(settings.BackgroundBaseColor, Color.FromArgb(255, 5, 9, 17));
                var topColor = ParseHexColorOrDefault(settings.BackgroundTopColor, Color.FromArgb(255, 11, 22, 35));
                var bottomColor = ParseHexColorOrDefault(settings.BackgroundBottomColor, Color.FromArgb(255, 5, 9, 17));

                var mode = (settings.BackgroundMode ?? "gradient").Trim().ToLowerInvariant();
                var animMode = (settings.BackgroundAnimationMode ?? "off").Trim().ToLowerInvariant();
                var speed = Math.Clamp(settings.BackgroundAnimationSpeed, 0f, 4f);

                var animatedTop = topColor;
                var animatedBottom = bottomColor;

                if (animMode is "slowlerp" or "slow" or "pulse")
                {
                    var t = (float)(_sw.Elapsed.TotalSeconds * Math.Max(0.1f, speed));
                    var k = 0.5f * (1f + MathF.Sin(t));
                    animatedTop = LerpColor(topColor, baseColor, k);
                    animatedBottom = LerpColor(bottomColor, baseColor, k);
                }

                if (mode == "solid")
                {
                    ctx.DrawRectangle(new SolidColorBrush(baseColor), null, bounds);
                }
                else
                {
                    var gradient = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops = new GradientStops
                        {
                            new GradientStop(animatedTop, 0),
                            new GradientStop(animatedBottom, 1)
                        }
                    };

                    ctx.DrawRectangle(gradient, null, bounds);
                }
            }
            catch
            {
                ctx.DrawRectangle(Brushes.Black, null, bounds);
            }
        }

        private static Color ParseHexColorOrDefault(string? hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            var s = hex.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal))
            {
                s = s.Substring(1);
            }

            if (s.Length != 6 && s.Length != 8)
            {
                return fallback;
            }

            try
            {
                byte a = 255;
                var offset = 0;
                if (s.Length == 8)
                {
                    a = Convert.ToByte(s.Substring(0, 2), 16);
                    offset = 2;
                }

                var r = Convert.ToByte(s.Substring(offset + 0, 2), 16);
                var g = Convert.ToByte(s.Substring(offset + 2, 2), 16);
                var b = Convert.ToByte(s.Substring(offset + 4, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            catch
            {
                return fallback;
            }
        }

        private static Color LerpColor(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            byte Lerp(byte x, byte y) => (byte)(x + (y - x) * t);

            return Color.FromArgb(
                Lerp(a.A, b.A),
                Lerp(a.R, b.R),
                Lerp(a.G, b.G),
                Lerp(a.B, b.B));
        }

        private void DrawHud(DrawingContext ctx, Rect bounds)
        {
            try
            {
                var order = _forcePvmOrder ? "P*V*M" : "M*V*P";
                var tf = new Typeface("Segoe UI");
                var renderSnapshot = GetRenderSceneSnapshot();
                var panelSummary = renderSnapshot.PanelSurfaces.Length == 0
                    ? "panels=0"
                    : $"panels={renderSnapshot.PanelSurfaces.Length} first={renderSnapshot.PanelSurfaces[0].ViewRef}";
                var linkSummary = $"links={renderSnapshot.Links.Length}";
                var groupSummary = renderSnapshot.ActiveGroupId is { Length: > 0 } activeId
                    ? $"groups={renderSnapshot.Groups.Length} active={activeId}"
                    : $"groups={renderSnapshot.Groups.Length}";
                var info =
                    $"cam yaw={_cam.Yaw:0.00} pitch={_cam.Pitch:0.00} dist={_cam.Distance:0.00}\n" +
                    $"order={order} depth={!_noDepth} clear={!_noClear} {panelSummary} {linkSummary} {groupSummary}";

                var ft = new FormattedText(
                    info,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    tf,
                    12,
                    Brushes.White);

                var bg = new Rect(8, 8, 560, 38);
                ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), null, bg);
                ctx.DrawText(ft, new Point(12, 12));
            }
            catch
            {
            }
        }

        private void DrawNodeInteractionOverlays(DrawingContext ctx, Rect bounds)
        {
            try
            {
                var renderSnapshot = GetRenderSceneSnapshot();
                if (renderSnapshot.Nodes.Length == 0)
                {
                    return;
                }

                var settings = EngineServices.Settings;
                var haloMode = (settings.NodeHaloMode ?? "2d").Trim().ToLowerInvariant();
                var draw2dHalo = string.Equals(haloMode, "2d", StringComparison.Ordinal) ||
                                 string.Equals(haloMode, "both", StringComparison.Ordinal);
                if (!draw2dHalo)
                {
                    return;
                }

                var view = ComputeView();
                var proj = ComputeProjection();

                foreach (var node in renderSnapshot.Nodes)
                {
                    if (!node.IsFocused && !node.IsSelected)
                    {
                        continue;
                    }

                    if (!TryProjectWorldPoint(node.Position, view, proj, bounds, out var screenPoint))
                    {
                        continue;
                    }

                    var baseRadius = Math.Max(12.0, node.VisualScale * 18.0);
                    var focusMul = Math.Clamp(settings.NodeFocusHaloRadiusMultiplier, 0.5f, 3f);
                    var selectionMul = Math.Clamp(settings.NodeSelectionHaloRadiusMultiplier, 0.5f, 3f);

                    var isFocused = node.IsFocused;
                    var isSelected = node.IsSelected;

                    double radius;
                    if (isFocused && isSelected)
                    {
                        radius = baseRadius * Math.Max(focusMul, selectionMul);
                    }
                    else if (isFocused)
                    {
                        radius = baseRadius * focusMul;
                    }
                    else if (isSelected)
                    {
                        radius = baseRadius * selectionMul;
                    }
                    else
                    {
                        radius = baseRadius;
                    }

                    var highlightOpacity = Math.Clamp(settings.NodeHighlightOpacity, 0f, 1f);

                    var baseFillAlpha = isFocused ? 220 : 180;
                    var fillAlpha = (byte)Math.Max(5, highlightOpacity * baseFillAlpha);
                    var baseStrokeAlpha = 255;
                    var strokeAlpha = (byte)Math.Max(5, highlightOpacity * baseStrokeAlpha);

                    Color fillColor;
                    Color strokeColor;

                    if (isFocused)
                    {
                        fillColor = Color.FromArgb(fillAlpha, 250, 204, 21);
                        strokeColor = Color.FromArgb(strokeAlpha, 250, 204, 21);
                    }
                    else
                    {
                        fillColor = Color.FromArgb(fillAlpha, 96, 165, 250);
                        strokeColor = Color.FromArgb(strokeAlpha, 96, 165, 250);
                    }

                    var stateLabel = isFocused && isSelected
                        ? "focused + selected"
                        : isFocused
                            ? "focused"
                            : "selected";

                    ctx.DrawEllipse(
                        new SolidColorBrush(fillColor),
                        new Pen(new SolidColorBrush(strokeColor), isFocused ? 3.0 : 2.0),
                        screenPoint,
                        radius,
                        radius);

                    var label = new FormattedText(
                        $"{node.Label} • {stateLabel}",
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        10,
                        Brushes.White);

                    var labelX = screenPoint.X - (label.WidthIncludingTrailingWhitespace / 2.0);
                    var labelY = screenPoint.Y - radius - label.Height - 6.0;
                    ctx.DrawText(label, new Point(labelX, labelY));
                }
            }
            catch
            {
            }
        }

        private void DrawLinkPlaceholders(DrawingContext ctx, Rect bounds)
        {
            try
            {
                var renderSnapshot = GetRenderSceneSnapshot();
                if (renderSnapshot.Links.Length == 0 || renderSnapshot.Nodes.Length == 0)
                {
                    return;
                }

                var byId = renderSnapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
                var view = ComputeView();
                var proj = ComputeProjection();

                foreach (var link in renderSnapshot.Links)
                {
                    if (!byId.TryGetValue(link.SourceId, out var source) ||
                        !byId.TryGetValue(link.TargetId, out var target))
                    {
                        continue;
                    }

                    if (!TryProjectWorldPoint(source.Position, view, proj, bounds, out var sourcePoint) ||
                        !TryProjectWorldPoint(target.Position, view, proj, bounds, out var targetPoint))
                    {
                        continue;
                    }

                    var appearance = link.Appearance ?? LinkAppearance.Default;
                    var settings = EngineServices.Settings;

                    var baseColor = ParseHexColorOrDefault(
                        appearance.StrokeColor,
                        Color.FromArgb(255, 125, 211, 252));
                    var globalOpacity = Math.Clamp(settings.LinkOpacity, 0f, 1f);
                    var linkOpacity = Math.Clamp(appearance.Opacity, 0f, 1f);
                    var combinedAlpha = (byte)Math.Max(5, 255f * globalOpacity * linkOpacity);
                    var strokeColor = Color.FromArgb(
                        combinedAlpha,
                        baseColor.R,
                        baseColor.G,
                        baseColor.B);

                    var thickness = Math.Max(0.5f, settings.LinkStrokeThickness * Math.Clamp(appearance.StrokeThickness, 0.1f, 4f));
                    var pen = new Pen(new SolidColorBrush(strokeColor), thickness);

                    ctx.DrawLine(pen, sourcePoint, targetPoint);

                    var mid = new Point(
                        (sourcePoint.X + targetPoint.X) * 0.5,
                        (sourcePoint.Y + targetPoint.Y) * 0.5);

                    var label = new FormattedText(
                        link.Kind,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        10,
                        Brushes.White);

                    var labelRect = new Rect(mid.X - 24, mid.Y - 10, 48, 18);
                    ctx.DrawRectangle(
                        new SolidColorBrush(Color.FromArgb(140, 12, 20, 28)),
                        new Pen(new SolidColorBrush(Color.FromArgb(180, strokeColor.R, strokeColor.G, strokeColor.B)), 1),
                        labelRect,
                        4);
                    ctx.DrawText(label, new Point(labelRect.X + 6, labelRect.Y + 2));
                }
            }
            catch
            {
            }
        }

        private void DrawGroupPlaceholders(DrawingContext ctx, Rect bounds)
        {
            try
            {
                var renderSnapshot = GetRenderSceneSnapshot();
                if (renderSnapshot.Groups.Length == 0 || renderSnapshot.Nodes.Length == 0)
                {
                    return;
                }

                var byId = renderSnapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
                var view = ComputeView();
                var proj = ComputeProjection();

                foreach (var group in renderSnapshot.Groups)
                {
                    // Project all nodes in the group; require at least two visible points
                    var points = group.NodeIds
                        .Where(byId.ContainsKey)
                        .Select(id => byId[id])
                        .Select(node =>
                        {
                            return TryProjectWorldPoint(node.Position, view, proj, bounds, out var p)
                                ? (ok: true, p)
                                : (ok: false, p: default(Point));
                        })
                        .Where(t => t.ok)
                        .Select(t => t.p)
                        .ToArray();

                    if (points.Length < 2)
                    {
                        continue;
                    }

                    var minX = points.Min(p => p.X);
                    var minY = points.Min(p => p.Y);
                    var maxX = points.Max(p => p.X);
                    var maxY = points.Max(p => p.Y);

                    // Pad a bit for readability
                    const double pad = 10.0;
                    var rect = new Rect(minX - pad, minY - pad, Math.Max(2, (maxX - minX) + 2 * pad), Math.Max(2, (maxY - minY) + 2 * pad));
                    var groupOpacity = Math.Clamp(EngineServices.Settings.GroupOverlayOpacity, 0f, 1f);
                    var alpha = (byte)Math.Max(5, groupOpacity * 255f);
                    var fill = new SolidColorBrush(Color.FromArgb(alpha, 20, 180, 120));    // subtle greenish overlay
                    var stroke = new Pen(new SolidColorBrush(Color.FromArgb(220, 20, 200, 140)), 1.5); // brighter outline
                    ctx.DrawRectangle(fill, stroke, rect, 6);

                    var label = new FormattedText(
                        group.Label,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        11,
                        Brushes.White);

                    // Label box
                    var labelWidth = label.WidthIncludingTrailingWhitespace;
                    var labelHeight = label.Height;
                    var labelBox = new Rect(rect.X + 8, rect.Y + 6, Math.Max(48, labelWidth + 10), Math.Max(18, labelHeight + 6));
                    ctx.DrawRectangle(
                        new SolidColorBrush(Color.FromArgb(140, 12, 20, 28)),
                        new Pen(new SolidColorBrush(Color.FromArgb(180, 125, 211, 252)), 1),
                        labelBox,
                        4);
                    ctx.DrawText(label, new Point(labelBox.X + 5, labelBox.Y + 2));
                }
            }
            catch
            {
            }
        }

        private void DrawPanelPlaceholders(DrawingContext ctx, Rect bounds)
        {
            try
            {
                ViewportPanelOverlayRenderer.DrawPanelPlaceholders(
                    ctx,
                    GetRenderSceneSnapshot(),
                    bounds,
                    ComputeView(),
                    ComputeProjection(),
                    _activeCommandSurface);
            }
            catch
            {
            }
        }

        private bool TryHitTestPanelSurface(Point point, out PanelSurfaceNode hitPanel, out PanelSurfaceSemantics hitSemantics)
        {
            return ViewportPanelOverlayRenderer.TryHitTestPanelSurface(
                GetRenderSceneSnapshot(),
                point,
                new Rect(Bounds.Size),
                ComputeView(),
                ComputeProjection(),
                out hitPanel,
                out hitSemantics);
        }

        private RenderNode[] GetOrderedRenderNodes() =>
            GetRenderSceneSnapshot().Nodes
                .OrderBy(node => node.Label, StringComparer.Ordinal)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .ToArray();

        private static RenderNode[] GetRenderNodes()
        {
            var snapshot = EngineServices.Scene.GetSnapshot();
            return CoreSceneAdapter.ToRenderNodes(snapshot);
        }

        private RenderSceneSnapshot GetRenderSceneSnapshot()
        {
            var baseSnapshot = CoreSceneAdapter.ToRenderSceneSnapshot(EngineServices.Scene.GetSnapshot());

            var nodes = _moveDragPreviewPositions.Count == 0
                ? baseSnapshot.Nodes
                : baseSnapshot.Nodes
                    .Select(node =>
                        _moveDragPreviewPositions.TryGetValue(node.Id, out var previewPosition)
                            ? node with { Position = previewPosition }
                            : node)
                    .ToArray();

            var panelSurfaces = baseSnapshot.PanelSurfaces;

            if (_hasEphemeralNodeCommandSurface &&
                _activeCommandSurface.HasValue &&
                string.Equals(_ephemeralNodePanel.NodeId, _activeCommandSurface.NodeId, StringComparison.Ordinal) &&
                string.Equals(_ephemeralNodePanel.ViewRef, _activeCommandSurface.ViewRef, StringComparison.Ordinal))
            {
                var extended = new PanelSurfaceNode[panelSurfaces.Length + 1];
                Array.Copy(panelSurfaces, extended, panelSurfaces.Length);
                extended[^1] = _ephemeralNodePanel;
                panelSurfaces = extended;
            }

            return new RenderSceneSnapshot(
                nodes,
                panelSurfaces,
                baseSnapshot.Links,
                baseSnapshot.Groups,
                baseSnapshot.ActiveGroupId);
        }
    }
}
