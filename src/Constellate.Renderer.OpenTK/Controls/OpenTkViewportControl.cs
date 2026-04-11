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
    /// <summary>
    /// OpenTK-backed viewport control (offscreen FBO -> CPU readback -> WriteableBitmap).
    /// GL path is preferred; software fallback remains available.
    /// </summary>
    public class OpenTkViewportControl : Control
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
        private bool _isMarqueeSelecting;
        private Point _marqueeStartPt;
        private Point _marqueeCurrentPt;
        private bool _isMoveDragging;
        private Point _moveDragStartPt;
        private readonly Dictionary<string, Vector3> _moveDragStartPositions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> _moveDragPreviewPositions = new(StringComparer.Ordinal);

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
            ctx.DrawRectangle(Brushes.Black, null, bounds);

            if (_preferGl && !_glFailed && _glBitmap is not null)
            {
                var src = new Rect(0, 0, _glBitmap.PixelSize.Width, _glBitmap.PixelSize.Height);
                ctx.DrawImage(_glBitmap, src, bounds);
            }
            else
            {
                DrawSoftwareTriangle(ctx, bounds);
            }

            if (_preferGl && !_glFailed)
            {
                DrawNodeInteractionOverlays(ctx, bounds);
                DrawLinkPlaceholders(ctx, bounds);
                DrawGroupPlaceholders(ctx, bounds);
                DrawPanelPlaceholders(ctx, bounds);

                if (_isMarqueeSelecting)
                {
                    DrawMarqueeOverlay(ctx);
                }
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
                var groupSummary = $"groups={renderSnapshot.Groups.Length}";
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

                    var radius = Math.Max(12.0, node.VisualScale * 18.0);
                    var fillColor = node.IsFocused
                        ? Color.FromArgb(90, 250, 204, 21)
                        : Color.FromArgb(72, 96, 165, 250);
                    var strokeColor = node.IsFocused
                        ? Color.FromArgb(255, 250, 204, 21)
                        : Color.FromArgb(255, 96, 165, 250);
                    var stateLabel = node.IsFocused && node.IsSelected
                        ? "focused + selected"
                        : node.IsFocused
                            ? "focused"
                            : "selected";

                    ctx.DrawEllipse(
                        new SolidColorBrush(fillColor),
                        new Pen(new SolidColorBrush(strokeColor), node.IsFocused ? 3.0 : 2.0),
                        screenPoint,
                        radius,
                        radius);

                    var label = new FormattedText($"{node.Label} • {stateLabel}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.White);
                    var labelWidth = label.WidthIncludingTrailingWhitespace;
                    var labelHeight = label.Height;
                    var labelRect = new Rect(screenPoint.X - (labelWidth / 2.0) - 6.0, screenPoint.Y - radius - labelHeight - 12.0, Math.Max(46.0, labelWidth + 12.0), Math.Max(18.0, labelHeight + 6.0));
                    ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 12, 20, 28)), new Pen(new SolidColorBrush(strokeColor), 1.0), labelRect, 4);
                    ctx.DrawText(label, new Point(labelRect.X + 6.0, labelRect.Y + 3.0));
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

                    var strokeThickness = Math.Clamp(link.Weight, 1.0f, 3.0f);
                    var strokeBrush = new SolidColorBrush(Color.FromArgb(220, 125, 211, 252));
                    var pen = new Pen(strokeBrush, strokeThickness);

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
                        new Pen(new SolidColorBrush(Color.FromArgb(180, 125, 211, 252)), 1),
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

                    var fill = new SolidColorBrush(Color.FromArgb(50, 20, 180, 120));    // subtle greenish overlay
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
                var renderSnapshot = GetRenderSceneSnapshot();
                if (renderSnapshot.PanelSurfaces.Length == 0 || renderSnapshot.Nodes.Length == 0)
                {
                    return;
                }

                var byId = renderSnapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
                var view = ComputeView();
                var proj = ComputeProjection();

                foreach (var panel in renderSnapshot.PanelSurfaces)
                {
                    if (!panel.IsVisible || !byId.TryGetValue(panel.NodeId, out var node))
                    {
                        continue;
                    }

                    var world = new Vector4(
                        node.Position.X + panel.LocalOffset.X,
                        node.Position.Y + panel.LocalOffset.Y,
                        node.Position.Z + panel.LocalOffset.Z,
                        1f);

                    var clip = world * view * proj;
                    if (clip.W <= 0.0001f)
                    {
                        continue;
                    }

                    var ndc = clip.Xyz / clip.W;
                    if (ndc.Z < -1.2f || ndc.Z > 1.2f)
                    {
                        continue;
                    }

                    var screenX = bounds.X + ((ndc.X + 1f) * 0.5 * bounds.Width);
                    var screenY = bounds.Y + ((1f - (ndc.Y + 1f) * 0.5) * bounds.Height);

                    var width = Math.Max(36.0, panel.Size.X * 60.0);
                    var height = Math.Max(20.0, panel.Size.Y * 36.0);
                    var anchorOffset = GetAnchorOffset(panel.Anchor, width, height);
                    var rect = new Rect(
                        screenX + anchorOffset.X,
                        screenY + anchorOffset.Y,
                        width,
                        height);

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
            catch
            {
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
            {
                SendCommand<object?>(CommandNames.Undo, null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                CycleFocusedNode(!e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                SelectFocusedNode(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (_isMoveDragging)
                {
                    CancelMoveDrag();
                    e.Handled = true;
                    return;
                }

                SendCommand<object?>(CommandNames.ClearSelection, null);
                e.Handled = true;
            }
        }

        private bool TryProjectWorldPoint(
            Vector3 worldPosition,
            Matrix4 view,
            Matrix4 proj,
            Rect bounds,
            out Point screenPoint)
        {
            var world = new Vector4(worldPosition.X, worldPosition.Y, worldPosition.Z, 1f);
            var clip = world * view * proj;

            if (clip.W <= 0.0001f)
            {
                screenPoint = default;
                return false;
            }

            var ndc = clip.Xyz / clip.W;
            if (ndc.Z < -1.2f || ndc.Z > 1.2f)
            {
                screenPoint = default;
                return false;
            }

            screenPoint = new Point(
                bounds.X + ((ndc.X + 1f) * 0.5 * bounds.Width),
                bounds.Y + ((1f - (ndc.Y + 1f) * 0.5) * bounds.Height));
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

        private void DrawMarqueeOverlay(DrawingContext ctx)
        {
            var rect = GetNormalizedRect(_marqueeStartPt, _marqueeCurrentPt);
            if (rect.Width <= 0.5 || rect.Height <= 0.5)
            {
                return;
            }

            ctx.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(45, 125, 211, 252)),
                new Pen(new SolidColorBrush(Color.FromArgb(220, 125, 211, 252)), 1.5),
                rect);
        }

        private void BeginMoveDrag(string clickedNodeId)
        {
            var sceneSnapshot = EngineServices.ShellScene.GetSnapshot();
            var selectedNodeIds = sceneSnapshot.SelectedNodeIds?
                .Select(id => id.ToString())
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);

            var dragNodeIds = selectedNodeIds.Count > 0 && selectedNodeIds.Contains(clickedNodeId)
                ? selectedNodeIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
                : [clickedNodeId];

            if (selectedNodeIds.Contains(clickedNodeId))
            {
                SendCommand(CommandNames.Focus, new FocusEntityPayload(clickedNodeId));
            }
            else
            {
                SendCommand(CommandNames.Focus, new FocusEntityPayload(clickedNodeId));
                SendCommand(CommandNames.Select, new SelectEntitiesPayload([clickedNodeId], true));
                dragNodeIds = [clickedNodeId];
            }

            _moveDragStartPositions.Clear();
            _moveDragPreviewPositions.Clear();

            foreach (var node in sceneSnapshot.Nodes.Where(node => dragNodeIds.Contains(node.Id.ToString(), StringComparer.Ordinal)))
            {
                var nodeId = node.Id.ToString();
                var position = new Vector3(
                    node.Transform.Position.X,
                    node.Transform.Position.Y,
                    node.Transform.Position.Z);

                _moveDragStartPositions[nodeId] = position;
                _moveDragPreviewPositions[nodeId] = position;
            }

            if (_moveDragStartPositions.Count == 0)
            {
                return;
            }

            _isMoveDragging = true;
            _pointerMovedBeyondThreshold = false;
            _pendingAdditiveSelection = false;
            _pressedNodeId = clickedNodeId;
            _moveDragStartPt = _lastPt;
        }

        private Vector3 ComputeMoveDragWorldDelta(Point currentPoint)
        {
            var dx = currentPoint.X - _moveDragStartPt.X;
            var dy = currentPoint.Y - _moveDragStartPt.Y;
            var cy = MathF.Cos(_cam.Yaw);
            var sy = MathF.Sin(_cam.Yaw);
            var cp = MathF.Cos(_cam.Pitch);
            var sp = MathF.Sin(_cam.Pitch);
            var cameraOutward = Vector3.Normalize(new Vector3(
                cy * cp,
                sp,
                sy * cp));
            var right = Vector3.Normalize(Vector3.Cross(cameraOutward, Vector3.UnitY));
            var up = Vector3.Normalize(Vector3.Cross(right, cameraOutward));
            var dragScale = _cam.Distance * 0.0025f;

            return (-right * (float)dx * dragScale) - (up * (float)dy * dragScale);
        }

        private void UpdateMoveDragPreview(Point currentPoint)
        {
            var delta = ComputeMoveDragWorldDelta(currentPoint);
            _moveDragPreviewPositions.Clear();

            foreach (var entry in _moveDragStartPositions)
            {
                _moveDragPreviewPositions[entry.Key] = entry.Value + delta;
            }
        }

        private void CompleteMoveDrag(Point releasePoint)
        {
            if (_pointerMovedBeyondThreshold)
            {
                UpdateMoveDragPreview(releasePoint);
                var latestSnapshot = EngineServices.ShellScene.GetSnapshot();
                var nodesById = latestSnapshot.Nodes.ToDictionary(node => node.Id.ToString(), StringComparer.Ordinal);
                var updates = _moveDragPreviewPositions
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Where(entry => nodesById.ContainsKey(entry.Key))
                    .Select(entry =>
                    {
                        var node = nodesById[entry.Key];
                        return new UpdateEntityPayload(
                            entry.Key,
                            node.Label,
                            new NVec3(entry.Value.X, entry.Value.Y, entry.Value.Z),
                            node.Transform.RotationEuler,
                            node.Transform.Scale,
                            node.VisualScale,
                            node.Phase);
                    })
                    .ToArray();

                if (updates.Length > 0)
                {
                    SendCommand(
                        CommandNames.UpdateEntities,
                        new UpdateEntitiesPayload(updates));
                }
            }

            ClearMoveDragState();
            InvalidateVisual();
        }

        private void ClearMoveDragState()
        {
            _isMoveDragging = false;
            _pointerMovedBeyondThreshold = false;
            _pressedNodeId = null;
            _moveDragStartPositions.Clear();
            _moveDragPreviewPositions.Clear();
        }

        private void CancelMoveDrag()
        {
            ClearMoveDragState();
            _leftPointerPending = false;
            _pendingAdditiveSelection = false;
            _interactionMode = PointerInteractionMode.None;
            InvalidateVisual();
        }

        private void HandleMarqueeRelease(bool additiveSelection)
        {
            var selectionRect = GetNormalizedRect(_marqueeStartPt, _marqueeCurrentPt);
            if (IsPointLikeSelectionRect(selectionRect))
            {
                HandleNodeClick(HitTestProjectedNodeId(_marqueeCurrentPt), additiveSelection);
                return;
            }

            var nodeIds = HitTestProjectedNodeIds(selectionRect);
            if (nodeIds.Length == 0)
            {
                if (!additiveSelection)
                {
                    SendCommand<object?>(CommandNames.ClearSelection, null);
                }

                return;
            }

            SendCommand(CommandNames.Focus, new FocusEntityPayload(nodeIds[0]));
            SendCommand(CommandNames.Select, new SelectEntitiesPayload(nodeIds, !additiveSelection));
        }

        private string[] HitTestProjectedNodeIds(Rect selectionRect)
        {
            var renderSnapshot = GetRenderSceneSnapshot();
            if (renderSnapshot.Nodes.Length == 0)
            {
                return [];
            }

            var view = ComputeView();
            var proj = ComputeProjection();
            var bounds = new Rect(Bounds.Size);

            return renderSnapshot.Nodes
                .Where(node => TryProjectWorldPoint(node.Position, view, proj, bounds, out var screenPoint) &&
                               selectionRect.Contains(screenPoint))
                .OrderBy(node => node.Label, StringComparer.Ordinal)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .Select(node => node.Id)
                .ToArray();
        }

        private static Rect GetNormalizedRect(Point a, Point b) =>
            new(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X),
                Math.Abs(a.Y - b.Y));

        private static bool IsPointLikeSelectionRect(Rect selectionRect) =>
            selectionRect.Width < ClickDragThreshold &&
            selectionRect.Height < ClickDragThreshold;

        private static bool IsMarqueeModeActive() =>
            string.Equals(EngineServices.ShellScene.GetInteractionMode(), "marquee", StringComparison.Ordinal);

        private static bool IsMoveModeActive() =>
            string.Equals(EngineServices.ShellScene.GetInteractionMode(), "move", StringComparison.Ordinal);

        private void HandleNodeClick(string? nodeId, bool additiveSelection)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                if (!additiveSelection)
                {
                    SendCommand<object?>(CommandNames.ClearSelection, null);
                }

                return;
            }

            SendCommand(CommandNames.Focus, new FocusEntityPayload(nodeId));
            SendCommand(CommandNames.Select, new SelectEntitiesPayload([nodeId], !additiveSelection));
        }

        private bool TryLinkInteractionNode(string targetNodeId)
        {
            var renderSnapshot = GetRenderSceneSnapshot();
            var sourceNodeId = renderSnapshot.Nodes
                .Where(node => node.IsSelected && !string.Equals(node.Id, targetNodeId, StringComparison.Ordinal))
                .OrderBy(node => node.Label, StringComparer.Ordinal)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .Select(node => node.Id)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(sourceNodeId))
            {
                sourceNodeId = renderSnapshot.Nodes
                    .Where(node => node.IsFocused && !string.Equals(node.Id, targetNodeId, StringComparison.Ordinal))
                    .Select(node => node.Id)
                    .FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(sourceNodeId) ||
                string.Equals(sourceNodeId, targetNodeId, StringComparison.Ordinal))
            {
                return false;
            }

            SendCommand(CommandNames.Connect, new ConnectEntitiesPayload(sourceNodeId, targetNodeId, Kind: "directed", Weight: 1.0f));
            SendCommand(CommandNames.Focus, new FocusEntityPayload(targetNodeId));
            SendCommand(CommandNames.Select, new SelectEntitiesPayload([targetNodeId], false));
            return true;
        }

        private void CycleFocusedNode(bool forward)
        {
            var nodes = GetOrderedRenderNodes();
            if (nodes.Length == 0)
            {
                return;
            }

            var focusedIndex = Array.FindIndex(nodes, node => node.IsFocused);
            var nextIndex = focusedIndex < 0
                ? (forward ? 0 : nodes.Length - 1)
                : (focusedIndex + (forward ? 1 : -1) + nodes.Length) % nodes.Length;

            SendCommand(CommandNames.Focus, new FocusEntityPayload(nodes[nextIndex].Id));
        }

        private void SelectFocusedNode(bool additiveSelection)
        {
            var nodes = GetOrderedRenderNodes();
            if (nodes.Length == 0)
            {
                return;
            }

            var focused = nodes.FirstOrDefault(node => node.IsFocused);
            var nodeId = string.IsNullOrWhiteSpace(focused.Id) ? nodes[0].Id : focused.Id;
            HandleNodeClick(nodeId, additiveSelection);
        }

        private string? HitTestProjectedNodeId(Point point)
        {
            var renderSnapshot = GetRenderSceneSnapshot();
            if (renderSnapshot.Nodes.Length == 0)
            {
                return null;
            }

            var view = ComputeView();
            var proj = ComputeProjection();
            const double hitRadius = 24.0;
            var hitRadiusSquared = hitRadius * hitRadius;
            var bestDistanceSquared = double.MaxValue;
            string? bestNodeId = null;

            foreach (var node in renderSnapshot.Nodes)
            {
                if (!TryProjectWorldPoint(node.Position, view, proj, new Rect(Bounds.Size), out var screenPoint))
                {
                    continue;
                }

                var dx = screenPoint.X - point.X;
                var dy = screenPoint.Y - point.Y;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared <= hitRadiusSquared && distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestNodeId = node.Id;
                }
            }

            return bestNodeId;
        }

        private RenderNode[] GetOrderedRenderNodes() =>
            GetRenderSceneSnapshot().Nodes
                .OrderBy(node => node.Label, StringComparer.Ordinal)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .ToArray();

        private static void SendCommand<TPayload>(string commandName, TPayload payload) =>
            EngineServices.CommandBus.Send(new Envelope { V = "1.0", Id = Guid.NewGuid(), Ts = DateTimeOffset.UtcNow, Type = EnvelopeType.Command, Name = commandName, Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload, JsonOptions), CorrelationId = null });

        private void EnsureGl(int width, int height)
        {
            if (_glInitialized && width == _texW && height == _texH)
            {
                return;
            }

            if (_glWindow is null)
            {
                var gws = GameWindowSettings.Default;
                var nws = new NativeWindowSettings
                {
                    Title = "Constellate Offscreen",
                    Size = new V2i(Math.Max(1, width), Math.Max(1, height)),
                    StartVisible = false,
                    StartFocused = false,
                    NumberOfSamples = 0,
                    Flags = ContextFlags.ForwardCompatible
                };

                _glWindow = new GameWindow(gws, nws);
                _glWindow.IsVisible = false;
                _glWindow.MakeCurrent();
            }
            else
            {
                _glWindow.MakeCurrent();
            }

            if (_diagVerbose)
            {
                GLDiagnostics.TryEnableDebugOutputOnce();
                GLDiagnostics.CheckError("EnsureGl.MakeCurrent");
            }

            if (_fbo == 0 || _texW != width || _texH != height)
            {
                RecreateFbo(width, height);
            }

            if (_program == 0 || _vao == 0 || (_vbo == 0 && !_selfTest))
            {
                CreateTrianglePipeline();
            }

            _glInitialized = true;
        }

        private void RecreateFbo(int width, int height)
        {
            DestroyFbo();

            _texW = Math.Max(1, width);
            _texH = Math.Max(1, height);

            _fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

            _colorTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _colorTex);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba8,
                _texW,
                _texH,
                0,
                GLPixelFormat.Bgra,
                GLPixelType.UnsignedByte,
                IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                _colorTex,
                0);

            _depthRbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, _texW, _texH);
            GL.FramebufferRenderbuffer(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer,
                _depthRbo);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new InvalidOperationException($"FBO incomplete: {status}");
            }

            var stride = _texW * 4;
            var capacity = stride * _texH;
            _readbackRaw = _readbackRaw is { Length: > 0 } rb && rb.Length >= capacity ? rb : new byte[capacity];
            _readbackFlipped = _readbackFlipped is { Length: > 0 } rf && rf.Length >= capacity ? rf : new byte[capacity];

            _glBitmap?.Dispose();
            _glBitmap = new WriteableBitmap(
                new PixelSize(_texW, _texH),
                new Vector(96, 96),
                AvaloniaPixelFormat.Bgra8888,
                AlphaFormat.Premul);

            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            if (_diagVerbose)
            {
                GLDiagnostics.CheckError("RecreateFbo");
            }
        }

        private void DestroyFbo()
        {
            if (_depthRbo != 0)
            {
                GL.DeleteRenderbuffer(_depthRbo);
                _depthRbo = 0;
            }

            if (_colorTex != 0)
            {
                GL.DeleteTexture(_colorTex);
                _colorTex = 0;
            }

            if (_fbo != 0)
            {
                GL.DeleteFramebuffer(_fbo);
                _fbo = 0;
            }

            _texW = 0;
            _texH = 0;
        }

        private void CreateTrianglePipeline()
        {
            DestroyGlPipeline();

            string vsSrc;
            string fsSrc;

            if (_selfTest)
            {
                vsSrc = @"#version 330 core
 void main() {
     const vec2 pos[3] = vec2[3](vec2(-1.0,-1.0), vec2(3.0,-1.0), vec2(-1.0,3.0));
     gl_Position = vec4(pos[gl_VertexID], 0.0, 1.0);
 }";
                fsSrc = @"#version 330 core
 out vec4 FragColor;
 void main() { FragColor = vec4(1.0, 1.0, 1.0, 1.0); }";
            }
            else
            {
                vsSrc = @"#version 330 core
 layout(location=0) in vec3 aPos;
 uniform mat4 uMVP;
 void main() {
     gl_Position = uMVP * vec4(aPos, 1.0);
 }";
                fsSrc = @"#version 330 core
 uniform vec4 uColor;
 out vec4 FragColor;
 void main() { FragColor = uColor; }";
            }

            var vs = CompileShader(ShaderType.VertexShader, vsSrc);
            var fs = CompileShader(ShaderType.FragmentShader, fsSrc);

            _program = GL.CreateProgram();
            GL.AttachShader(_program, vs);
            GL.AttachShader(_program, fs);
            GL.LinkProgram(_program);
            GL.GetProgram(_program, GetProgramParameterName.LinkStatus, out var linked);

            if (linked == 0)
            {
                var info = GL.GetProgramInfoLog(_program);
                GL.DeleteProgram(_program);
                _program = 0;
                GL.DeleteShader(vs);
                GL.DeleteShader(fs);
                throw new InvalidOperationException($"GL program link failed: {info}");
            }

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            if (_selfTest)
            {
                _vao = GL.GenVertexArray();
                _locMvp = -1;
            }
            else
            {
                float[] vertices =
                {
                    -0.5f, -0.5f, 0f,
                     0.5f, -0.5f, 0f,
                     0.0f,  0.5f, 0f
                };

                _vao = GL.GenVertexArray();
                _vbo = GL.GenBuffer();

                GL.BindVertexArray(_vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

                _locMvp = GL.GetUniformLocation(_program, "uMVP");
                _locColor = GL.GetUniformLocation(_program, "uColor");

                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.BindVertexArray(0);
            }

            if (_diagVerbose)
            {
                GLDiagnostics.CheckError("CreateTrianglePipeline");
                GLDiagnostics.DumpBasicState("After pipeline creation");
                if (!_selfTest && _locMvp < 0)
                {
                    Debug.WriteLine("[GLDiag] Warning: uMVP location < 0; uniform may be optimized out.");
                }
            }
        }

        private static int CompileShader(ShaderType type, string src)
        {
            var shader = GL.CreateShader(type);
            GL.ShaderSource(shader, src);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out var ok);

            if (ok == 0)
            {
                var info = GL.GetShaderInfoLog(shader);
                GL.DeleteShader(shader);
                throw new InvalidOperationException($"{type} compile failed: {info}");
            }

            return shader;
        }

        private void DestroyGlPipeline()
        {
            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }

            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }

            if (_program != 0)
            {
                GL.DeleteProgram(_program);
                _program = 0;
            }

            _locMvp = -1;
            _locColor = -1;
        }

        private void TeardownGl()
        {
            try
            {
                if (_glWindow is not null)
                {
                    _glWindow.MakeCurrent();
                    DestroyGlPipeline();
                    DestroyFbo();
                    _glWindow.Context?.MakeNoneCurrent();
                    _glWindow.Close();
                    _glWindow.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenTkViewportControl] Teardown exception: {ex.Message}");
            }
            finally
            {
                _glWindow = null;
                _glInitialized = false;
            }
        }

        private void RenderGlFrame()
        {
            var w = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
            var h = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
            if (w <= 0 || h <= 0)
            {
                return;
            }

            EnsureGl(w, h);

            _glWindow?.MakeCurrent();

            if (_diagVerbose)
            {
                GLDiagnostics.CheckError("Before FBO bind");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            try
            {
                var bufs = new[] { DrawBuffersEnum.ColorAttachment0 };
                GL.DrawBuffers(1, bufs);
            }
            catch
            {
            }

            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, _texW, _texH);

            GL.Disable(EnableCap.CullFace);
            if (_noDepth)
            {
                GL.Disable(EnableCap.DepthTest);
            }
            else
            {
                GL.Enable(EnableCap.DepthTest);
            }

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(true);
            GL.ClearDepth(1.0);

            var tColor = (_angleDeg % 360.0) / 360.0;
            var r = (float)(0.25 + 0.75 * Math.Abs(Math.Sin(tColor * Math.PI * 2)));
            var g = (float)(0.25 + 0.75 * Math.Abs(Math.Sin((tColor + 0.33) * Math.PI * 2)));
            var b = (float)(0.25 + 0.75 * Math.Abs(Math.Sin((tColor + 0.66) * Math.PI * 2)));

            if (_noClear)
            {
                GL.Clear(ClearBufferMask.DepthBufferBit);
            }
            else
            {
                GL.ClearColor(r, g, b, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            }

            if (_diagVerbose)
            {
                GLDiagnostics.CheckError("After Clear");
            }

            if (_program == 0 || _vao == 0 || (_vbo == 0 && !_selfTest))
            {
                CreateTrianglePipeline();
            }

            GL.UseProgram(_program);
            GL.BindVertexArray(_vao);

            var view = ComputeView();
            var proj = ComputeProjection();
            var time = (float)_sw.Elapsed.TotalSeconds;
            var renderSnapshot = GetRenderSceneSnapshot();
            var renderNodes = renderSnapshot.Nodes;

            if (_selfTest)
            {
                GL.Disable(EnableCap.DepthTest);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }
            else
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                for (var i = 0; i < renderNodes.Length; i++)
                {
                    ref readonly var node = ref renderNodes[i];

                    Matrix4 mvp;
                    if (_forceIdentity)
                    {
                        mvp = Matrix4.Identity;
                    }
                    else
                    {
                        var renderScale = ComputeRenderScale(node);
                        var model =
                            Matrix4.CreateScale(renderScale) *
                            Matrix4.CreateRotationX(node.RotationEuler.X) *
                            Matrix4.CreateRotationY(node.RotationEuler.Y + time + node.Phase) *
                            Matrix4.CreateRotationZ(node.RotationEuler.Z) *
                            Matrix4.CreateTranslation(node.Position);

                        if (_forcePvmOrder)
                        {
                            mvp = model;
                            mvp = view * mvp;
                            mvp = proj * mvp;
                        }
                        else
                        {
                            mvp = model;
                            mvp = mvp * view;
                            mvp = mvp * proj;
                        }
                    }

                    if (_locMvp < 0)
                    {
                        _locMvp = GL.GetUniformLocation(_program, "uMVP");
                    }

                    if (_locMvp >= 0)
                    {
                        GL.UniformMatrix4(_locMvp, _transposeUniform, ref mvp);
                    }

                    if (_locColor >= 0)
                    {
                        var fillColor = ParseAppearanceColor(node.FillColor, node.Opacity);
                        GL.Uniform4(_locColor, fillColor.X, fillColor.Y, fillColor.Z, fillColor.W);
                    }

                    if (_diagVerbose && i == 0)
                    {
                        Debug.WriteLine($"[GLDiag] uMVP loc={_locMvp}, transpose={_transposeUniform}, order={(_forcePvmOrder ? "P*V*M" : "M*V*P")}");
                    }

                    var primitiveVertices = GetPrimitiveVertices(node.Primitive);
                    GL.BufferData(BufferTarget.ArrayBuffer, primitiveVertices.Length * sizeof(float), primitiveVertices, BufferUsageHint.DynamicDraw);
                    GL.DrawArrays(PrimitiveType.Triangles, 0, primitiveVertices.Length / 3);

                    var edgeVertices = GetPrimitiveEdgeVertices(node.Primitive);
                    if (edgeVertices.Length > 0 && _locColor >= 0)
                    {
                        var outlineColor = ParseAppearanceColor(node.OutlineColor, Math.Clamp(node.Opacity * 0.95f, 0.35f, 1.0f));
                        GL.Uniform4(_locColor, outlineColor.X, outlineColor.Y, outlineColor.Z, outlineColor.W);
                        GL.LineWidth(node.IsFocused ? 2.4f : node.IsSelected ? 2.0f : 1.35f);
                        GL.BufferData(BufferTarget.ArrayBuffer, edgeVertices.Length * sizeof(float), edgeVertices, BufferUsageHint.DynamicDraw);
                        GL.DrawArrays(PrimitiveType.Lines, 0, edgeVertices.Length / 3);
                    }
                }

                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }

            if (_scissorMark)
            {
                try
                {
                    GL.Disable(EnableCap.DepthTest);
                    GL.Enable(EnableCap.ScissorTest);
                    var cx = Math.Max(0, _texW / 2 - 10);
                    var cy = Math.Max(0, _texH / 2 - 10);
                    GL.Scissor(cx, cy, Math.Min(20, _texW), Math.Min(20, _texH));
                    GL.ClearColor(1f, 1f, 1f, 1f);
                    GL.Clear(ClearBufferMask.ColorBufferBit);
                }
                catch
                {
                }
                finally
                {
                    GL.Disable(EnableCap.ScissorTest);
                    if (!_noDepth)
                    {
                        GL.Enable(EnableCap.DepthTest);
                    }
                }
            }

            GL.BindVertexArray(0);
            GL.UseProgram(0);

            if (_diagVerbose)
            {
                GLDiagnostics.CheckError("After Draw");
                GLDiagnostics.DumpBasicState("Post draw");
            }

            var stride = _texW * 4;
            if (_readbackRaw is null || _readbackRaw.Length < stride * _texH ||
                _readbackFlipped is null || _readbackFlipped.Length < stride * _texH)
            {
                _readbackRaw = new byte[stride * _texH];
                _readbackFlipped = new byte[stride * _texH];
            }

            GL.ReadPixels(0, 0, _texW, _texH, GLPixelFormat.Bgra, GLPixelType.UnsignedByte, _readbackRaw);

            for (var y = 0; y < _texH; y++)
            {
                System.Buffer.BlockCopy(_readbackRaw, (_texH - 1 - y) * stride, _readbackFlipped, y * stride, stride);
            }

            if (_diagVerbose)
            {
                var center = GLDiagnostics.SampleCenterPixel(_readbackFlipped, _texW, _texH);
                var clear = new GLDiagnostics.Rgba(
                    (byte)(r * 255.0f),
                    (byte)(g * 255.0f),
                    (byte)(b * 255.0f),
                    255);

                var nearClear = GLDiagnostics.Near(center, clear, tol: 5);
                Debug.WriteLine($"[GLDiag] Center pixel {center}; near clear={nearClear}");

                if (nearClear)
                {
                    Debug.WriteLine("[GLDiag] Geometry might not be contributing this frame (center ~ clear). Check MVP/uniforms/VAO/depth.");
                }
            }

            if (_glBitmap is not null)
            {
                using var fb = _glBitmap.Lock();
                var destStride = fb.RowBytes;
                var rowBytes = Math.Min(destStride, stride);

                for (var y = 0; y < _texH; y++)
                {
                    var srcOffset = y * stride;
                    var destPtr = fb.Address + (y * destStride);
                    Marshal.Copy(_readbackFlipped, srcOffset, destPtr, rowBytes);
                }
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private Matrix4 ComputeView()
        {
            var cy = MathF.Cos(_cam.Yaw);
            var sy = MathF.Sin(_cam.Yaw);
            var cp = MathF.Cos(_cam.Pitch);
            var sp = MathF.Sin(_cam.Pitch);

            var radius = _cam.Distance;
            var eye = new Vector3(
                _cam.Target.X + (radius * cp * cy),
                _cam.Target.Y + (radius * sp),
                _cam.Target.Z + (radius * cp * sy));

            var target = _cam.Target;
            var up = Vector3.UnitY;

            return Matrix4.LookAt(eye, target, up);
        }

        private Matrix4 ComputeProjection()
        {
            var fovy = MathHelper.DegreesToRadians(60f);
            var aspect = Math.Max(0.001f, (float)_texW / Math.Max(1f, _texH));
            return Matrix4.CreatePerspectiveFieldOfView(fovy, aspect, 0.1f, 100f);
        }

        private static Vector3 ComputeRenderScale(RenderNode node)
        {
            var sourceScale = new Vector3(
                MathF.Abs(node.Scale.X),
                MathF.Abs(node.Scale.Y),
                MathF.Abs(node.Scale.Z));
            var maxAxis = MathF.Max(sourceScale.X, MathF.Max(sourceScale.Y, sourceScale.Z));
            if (maxAxis <= 0.0001f)
            {
                maxAxis = 1f;
            }

            var baseScale = MathF.Max(node.VisualScale, 0.0001f);
            return new Vector3(
                (sourceScale.X / maxAxis) * baseScale,
                (sourceScale.Y / maxAxis) * baseScale,
                (sourceScale.Z / maxAxis) * baseScale);
        }

        private static float[] GetPrimitiveVertices(string? primitive)
        {
            var normalized = string.IsNullOrWhiteSpace(primitive)
                ? "triangle"
                : primitive.Trim().ToLowerInvariant();

            return normalized switch
            {
                "square" or "quad" => BuildRegularPolygonPrismVertices(4, 0.68f, 0.42f, MathF.PI / 4f),
                "diamond" => BuildRegularPolygonPrismVertices(4, 0.7f, 0.42f, 0f),
                "pentagon" => BuildRegularPolygonPrismVertices(5, 0.72f, 0.44f, -MathF.PI / 2f),
                "hexagon" => BuildRegularPolygonPrismVertices(6, 0.68f, 0.44f, MathF.PI / 6f),
                "cube" => BuildBoxVertices(0.58f),
                "tetrahedron" => BuildTetrahedronVertices(0.82f),
                _ => BuildRegularPolygonPrismVertices(3, 0.74f, 0.44f, -MathF.PI / 2f)
            };
        }

        private static float[] GetPrimitiveEdgeVertices(string? primitive)
        {
            var normalized = string.IsNullOrWhiteSpace(primitive)
                ? "triangle"
                : primitive.Trim().ToLowerInvariant();

            return normalized switch
            {
                "square" or "quad" => BuildRegularPolygonPrismEdges(4, 0.68f, 0.42f, MathF.PI / 4f),
                "diamond" => BuildRegularPolygonPrismEdges(4, 0.7f, 0.42f, 0f),
                "pentagon" => BuildRegularPolygonPrismEdges(5, 0.72f, 0.44f, -MathF.PI / 2f),
                "hexagon" => BuildRegularPolygonPrismEdges(6, 0.68f, 0.44f, MathF.PI / 6f),
                "cube" => BuildBoxEdges(0.58f),
                "tetrahedron" => BuildTetrahedronEdges(0.82f),
                _ => BuildRegularPolygonPrismEdges(3, 0.74f, 0.44f, -MathF.PI / 2f)
            };
        }

        private static float[] BuildRegularPolygonPrismVertices(int sides, float radius, float depth, float angleOffset)
        {
            if (sides < 3)
            {
                sides = 3;
            }

            var halfDepth = depth * 0.5f;
            var step = (MathF.PI * 2f) / sides;
            var vertices = new List<float>(sides * 36);

            for (var i = 0; i < sides; i++)
            {
                var currentAngle = angleOffset + (i * step);
                var nextAngle = angleOffset + ((i + 1) * step);
                var currentFront = new Vector3(MathF.Cos(currentAngle) * radius, MathF.Sin(currentAngle) * radius, halfDepth);
                var nextFront = new Vector3(MathF.Cos(nextAngle) * radius, MathF.Sin(nextAngle) * radius, halfDepth);
                var currentBack = new Vector3(currentFront.X, currentFront.Y, -halfDepth);
                var nextBack = new Vector3(nextFront.X, nextFront.Y, -halfDepth);

                AddTriangle(vertices, new Vector3(0f, 0f, halfDepth), currentFront, nextFront);
                AddTriangle(vertices, new Vector3(0f, 0f, -halfDepth), nextBack, currentBack);
                AddTriangle(vertices, currentFront, currentBack, nextBack);
                AddTriangle(vertices, currentFront, nextBack, nextFront);
            }

            return vertices.ToArray();
        }

        private static float[] BuildRegularPolygonPrismEdges(int sides, float radius, float depth, float angleOffset)
        {
            if (sides < 3)
            {
                sides = 3;
            }

            var halfDepth = depth * 0.5f;
            var step = (MathF.PI * 2f) / sides;
            var vertices = new List<float>(sides * 18);

            for (var i = 0; i < sides; i++)
            {
                var currentAngle = angleOffset + (i * step);
                var nextAngle = angleOffset + ((i + 1) * step);
                var currentFront = new Vector3(MathF.Cos(currentAngle) * radius, MathF.Sin(currentAngle) * radius, halfDepth);
                var nextFront = new Vector3(MathF.Cos(nextAngle) * radius, MathF.Sin(nextAngle) * radius, halfDepth);
                var currentBack = new Vector3(currentFront.X, currentFront.Y, -halfDepth);
                var nextBack = new Vector3(nextFront.X, nextFront.Y, -halfDepth);

                AddLine(vertices, currentFront, nextFront);
                AddLine(vertices, currentBack, nextBack);
                AddLine(vertices, currentFront, currentBack);
            }

            return vertices.ToArray();
        }

        private static float[] BuildBoxVertices(float halfExtent)
        {
            var h = halfExtent;
            var p000 = new Vector3(-h, -h, -h);
            var p001 = new Vector3(-h, -h, h);
            var p010 = new Vector3(-h, h, -h);
            var p011 = new Vector3(-h, h, h);
            var p100 = new Vector3(h, -h, -h);
            var p101 = new Vector3(h, -h, h);
            var p110 = new Vector3(h, h, -h);
            var p111 = new Vector3(h, h, h);

            var vertices = new List<float>(108);
            AddTriangle(vertices, p001, p101, p111);
            AddTriangle(vertices, p001, p111, p011);
            AddTriangle(vertices, p100, p000, p010);
            AddTriangle(vertices, p100, p010, p110);
            AddTriangle(vertices, p000, p001, p011);
            AddTriangle(vertices, p000, p011, p010);
            AddTriangle(vertices, p101, p100, p110);
            AddTriangle(vertices, p101, p110, p111);
            AddTriangle(vertices, p010, p011, p111);
            AddTriangle(vertices, p010, p111, p110);
            AddTriangle(vertices, p000, p100, p101);
            AddTriangle(vertices, p000, p101, p001);
            return vertices.ToArray();
        }

        private static float[] BuildBoxEdges(float halfExtent)
        {
            var h = halfExtent;
            var p000 = new Vector3(-h, -h, -h);
            var p001 = new Vector3(-h, -h, h);
            var p010 = new Vector3(-h, h, -h);
            var p011 = new Vector3(-h, h, h);
            var p100 = new Vector3(h, -h, -h);
            var p101 = new Vector3(h, -h, h);
            var p110 = new Vector3(h, h, -h);
            var p111 = new Vector3(h, h, h);

            var vertices = new List<float>(72);
            AddLine(vertices, p000, p001);
            AddLine(vertices, p001, p011);
            AddLine(vertices, p011, p010);
            AddLine(vertices, p010, p000);
            AddLine(vertices, p100, p101);
            AddLine(vertices, p101, p111);
            AddLine(vertices, p111, p110);
            AddLine(vertices, p110, p100);
            AddLine(vertices, p000, p100);
            AddLine(vertices, p001, p101);
            AddLine(vertices, p011, p111);
            AddLine(vertices, p010, p110);
            return vertices.ToArray();
        }

        private static float[] BuildTetrahedronVertices(float radius)
        {
            var vertices = new List<float>(36);
            var points = new[]
            {
                Vector3.Normalize(new Vector3(1f, 1f, 1f)) * radius,
                Vector3.Normalize(new Vector3(-1f, -1f, 1f)) * radius,
                Vector3.Normalize(new Vector3(-1f, 1f, -1f)) * radius,
                Vector3.Normalize(new Vector3(1f, -1f, -1f)) * radius
            };

            AddTriangle(vertices, points[0], points[1], points[2]);
            AddTriangle(vertices, points[0], points[3], points[1]);
            AddTriangle(vertices, points[0], points[2], points[3]);
            AddTriangle(vertices, points[1], points[3], points[2]);

            return vertices.ToArray();
        }

        private static float[] BuildTetrahedronEdges(float radius)
        {
            var vertices = new List<float>(36);
            var points = new[]
            {
                Vector3.Normalize(new Vector3(1f, 1f, 1f)) * radius,
                Vector3.Normalize(new Vector3(-1f, -1f, 1f)) * radius,
                Vector3.Normalize(new Vector3(-1f, 1f, -1f)) * radius,
                Vector3.Normalize(new Vector3(1f, -1f, -1f)) * radius
            };

            AddLine(vertices, points[0], points[1]);
            AddLine(vertices, points[0], points[2]);
            AddLine(vertices, points[0], points[3]);
            AddLine(vertices, points[1], points[2]);
            AddLine(vertices, points[1], points[3]);
            AddLine(vertices, points[2], points[3]);

            return vertices.ToArray();
        }

        private static void AddTriangle(List<float> vertices, Vector3 a, Vector3 b, Vector3 c)
        {
            vertices.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z]);
        }

        private static void AddLine(List<float> vertices, Vector3 a, Vector3 b)
        {
            vertices.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
        }

        private static Vector4 ParseAppearanceColor(string? hexColor, float opacity)
        {
            var fallback = new Vector4(1f, 1f, 1f, Math.Clamp(opacity, 0.1f, 1.0f));
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                return fallback;
            }

            var trimmed = hexColor.Trim();
            if (trimmed.Length != 7 || trimmed[0] != '#')
            {
                return fallback;
            }

            static float ParseChannel(string hex, int startIndex)
            {
                return Convert.ToInt32(hex.Substring(startIndex, 2), 16) / 255f;
            }

            try
            {
                return new Vector4(
                    ParseChannel(trimmed, 1),
                    ParseChannel(trimmed, 3),
                    ParseChannel(trimmed, 5),
                    Math.Clamp(opacity, 0.1f, 1.0f));
            }
            catch
            {
                return fallback;
            }
        }

        private static void TryGetFloat(JsonElement obj, string name, ref float value)
        {
            if (obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number)
            {
                if (p.TryGetSingle(out var f)) value = f;
                else if (p.TryGetDouble(out var d)) value = (float)d;
            }
        }

        private void PublishViewChanged()
        {
            try
            {
                var payload = new
                {
                    yaw = _cam.Yaw,
                    pitch = _cam.Pitch,
                    distance = _cam.Distance,
                    target = new { x = _cam.Target.X, y = _cam.Target.Y, z = _cam.Target.Z }
                };

                EngineServices.EventBus.Publish(new Envelope
                {
                    V = "1.0",
                    Id = Guid.NewGuid(),
                    Ts = DateTimeOffset.UtcNow,
                    Type = EnvelopeType.Event,
                    Name = EventNames.ViewChanged,
                    Payload = JsonSerializer.SerializeToElement(payload, JsonOptions),
                    CorrelationId = null
                });
            }
            catch
            {
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var pt = e.GetPosition(this);
            var props = e.GetCurrentPoint(this).Properties;
            var mods = e.KeyModifiers;

            if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
            {
                _interactionMode = PointerInteractionMode.Pan;
                _lastPt = pt;
                try
                {
                    e.Pointer.Capture(this);
                }
                catch { }

                e.Handled = true;
                Focus();
                return;
            }

            if (props.IsLeftButtonPressed)
            {
                Focus();

                if (IsMoveModeActive())
                {
                    var moveTargetNodeId = HitTestProjectedNodeId(pt);
                    if (string.IsNullOrWhiteSpace(moveTargetNodeId))
                    {
                        SendCommand<object?>(CommandNames.ClearSelection, null);
                        e.Handled = true;
                        return;
                    }

                    _leftPointerPending = false;
                    _pendingAdditiveSelection = false;
                    _lastPt = pt;
                    BeginMoveDrag(moveTargetNodeId);
                    try
                    {
                        e.Pointer.Capture(this);
                    }
                    catch { }
                    e.Handled = true;
                    return;
                }

                if (IsMarqueeModeActive())
                {
                    _leftPointerPending = false;
                    _pendingAdditiveSelection = mods.HasFlag(KeyModifiers.Shift);
                    _pointerMovedBeyondThreshold = false;
                    _pointerPressedPt = pt;
                    _marqueeStartPt = pt;
                    _marqueeCurrentPt = pt;
                    _pressedNodeId = null;
                    _interactionMode = PointerInteractionMode.None;
                    _isMarqueeSelecting = true;
                    try
                    {
                        e.Pointer.Capture(this);
                    }
                    catch { }
                    e.Handled = true;
                    return;
                }
                var clickedNodeId = HitTestProjectedNodeId(pt);
                if (!string.IsNullOrWhiteSpace(clickedNodeId) &&
                    (mods.HasFlag(KeyModifiers.Control) || e.ClickCount >= 2) &&
                    TryLinkInteractionNode(clickedNodeId))
                {
                    e.Handled = true;
                    return;
                }

                _leftPointerPending = true;
                _pendingAdditiveSelection = mods.HasFlag(KeyModifiers.Shift);
                _pointerMovedBeyondThreshold = false;
                _pointerPressedPt = pt;
                _pressedNodeId = clickedNodeId;
                _lastPt = pt;
                _interactionMode = PointerInteractionMode.None;
                try
                {
                    e.Pointer.Capture(this);
                }
                catch { }

                e.Handled = true;
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isMarqueeSelecting)
            {
                HandleMarqueeRelease(_pendingAdditiveSelection);
                _isMarqueeSelecting = false;
                _pendingAdditiveSelection = false;
                _pointerMovedBeyondThreshold = false;
                _marqueeStartPt = default;
                _marqueeCurrentPt = default;
                _pressedNodeId = null;
                _interactionMode = PointerInteractionMode.None;

                try
                {
                    e.Pointer.Capture(null);
                }
                catch { }

                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (_isMoveDragging)
            {
                CompleteMoveDrag(e.GetPosition(this));
                _interactionMode = PointerInteractionMode.None;

                try
                {
                    e.Pointer.Capture(null);
                }
                catch { }

                _pendingAdditiveSelection = false;
                _leftPointerPending = false;
                _pressedNodeId = null;
                e.Handled = true;
                return;
            }

            if (!_leftPointerPending && _interactionMode == PointerInteractionMode.None)
            {
                try
                {
                    e.Pointer.Capture(null);
                }
                catch { }
                return;
            }

            if (_leftPointerPending && !_pointerMovedBeyondThreshold)
            {
                HandleNodeClick(_pressedNodeId, _pendingAdditiveSelection);
            }

            _leftPointerPending = false;
            _pendingAdditiveSelection = false;
            _pointerMovedBeyondThreshold = false;
            _pressedNodeId = null;
            _interactionMode = PointerInteractionMode.None;

            try
            {
                e.Pointer.Capture(null);
            }
            catch { }

            e.Handled = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var pt = e.GetPosition(this);
            var dx = pt.X - _lastPt.X;
            var dy = pt.Y - _lastPt.Y;
            var cameraChanged = false;

            if (_isMoveDragging)
            {
                var pressDx = pt.X - _moveDragStartPt.X;
                var pressDy = pt.Y - _moveDragStartPt.Y;
                var movementSquared = (pressDx * pressDx) + (pressDy * pressDy);
                if (movementSquared >= ClickDragThreshold * ClickDragThreshold)
                {
                    _pointerMovedBeyondThreshold = true;
                }

                UpdateMoveDragPreview(pt);
                _lastPt = pt;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (_isMarqueeSelecting)
            {
                _marqueeCurrentPt = pt;
                _lastPt = pt;
                _pointerMovedBeyondThreshold = true;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (_leftPointerPending && !_pointerMovedBeyondThreshold)
            {
                var pressDx = pt.X - _pointerPressedPt.X;
                var pressDy = pt.Y - _pointerPressedPt.Y;
                var movementSquared = (pressDx * pressDx) + (pressDy * pressDy);
                if (movementSquared >= ClickDragThreshold * ClickDragThreshold)
                {
                    _pointerMovedBeyondThreshold = true;
                    _interactionMode = _pendingAdditiveSelection
                        ? PointerInteractionMode.Pan
                        : PointerInteractionMode.Orbit;
                }
            }

            if (_interactionMode == PointerInteractionMode.Pan)
            {
                var cy = MathF.Cos(_cam.Yaw);
                var sy = MathF.Sin(_cam.Yaw);
                var forward = new Vector3(
                    cy * MathF.Cos(_cam.Pitch),
                    MathF.Sin(_cam.Pitch),
                    sy * MathF.Cos(_cam.Pitch));

                forward = Vector3.Normalize(forward);
                var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
                var up = Vector3.Normalize(Vector3.Cross(right, forward));

                var panScale = _cam.Distance * 0.002f;
                _cam.Target -= right * (float)dx * panScale;
                _cam.Target += up * (float)dy * panScale;
                cameraChanged = true;
            }
            else if (_interactionMode == PointerInteractionMode.Orbit)
            {
                _cam.Yaw += (float)(dx * 0.01);
                _cam.Pitch += (float)(-dy * 0.01);
                _cam.Clamp();
                cameraChanged = true;
            }

            _lastPt = pt;
            if (cameraChanged)
            {
                PublishViewChanged();
                e.Handled = true;
                return;
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var delta = e.Delta.Y;
            var factor = (float)Math.Pow(1.1, -delta);
            _cam.Distance *= factor;
            _cam.Clamp();
            PublishViewChanged();
            e.Handled = true;
        }

        private static RenderNode[] GetRenderNodes()
        {
            var snapshot = EngineServices.Scene.GetSnapshot();
            return CoreSceneAdapter.ToRenderNodes(snapshot);
        }

        private RenderSceneSnapshot GetRenderSceneSnapshot()
        {
            var snapshot = CoreSceneAdapter.ToRenderSceneSnapshot(EngineServices.Scene.GetSnapshot());
            if (_moveDragPreviewPositions.Count == 0)
            {
                return snapshot;
            }

            var previewNodes = snapshot.Nodes
                .Select(node =>
                    _moveDragPreviewPositions.TryGetValue(node.Id, out var previewPosition)
                        ? node with { Position = previewPosition }
                        : node)
                .ToArray();

            return snapshot with
            {
                Nodes = previewNodes
            };
        }
    }
}
