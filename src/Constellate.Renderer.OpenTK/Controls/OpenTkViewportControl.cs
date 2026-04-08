using System;
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
using Constellate.Core.Messaging;
using Constellate.Renderer.OpenTK.Scene;
using Constellate.Core.Scene;
using Constellate.Renderer.OpenTK.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using GLPixelFormat = global::OpenTK.Graphics.OpenGL4.PixelFormat;
using GLPixelType = global::OpenTK.Graphics.OpenGL4.PixelType;
using V2i = global::OpenTK.Mathematics.Vector2i;

namespace Constellate.Renderer.OpenTK.Controls
{
    /// <summary>
    /// OpenTK-backed viewport control (offscreen FBO -> CPU readback -> WriteableBitmap).
    /// GL path is preferred; software fallback remains available.
    /// </summary>
    public class OpenTkViewportControl : Control
    {
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

        private readonly Stopwatch _sw = Stopwatch.StartNew();

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

        private bool _dragging;
        private bool _panning;
        private Point _lastPt;

        public OpenTkViewportControl()
        {
            Focusable = true;
            IsHitTestVisible = true;

            AddHandler(PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
            AddHandler(PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
            AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, handledEventsToo: true);

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
                DrawLinkPlaceholders(ctx, bounds);
                DrawPanelPlaceholders(ctx, bounds);
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
                var info =
                    $"cam yaw={_cam.Yaw:0.00} pitch={_cam.Pitch:0.00} dist={_cam.Distance:0.00}\n" +
                    $"order={order} depth={!_noDepth} clear={!_noClear} {panelSummary} {linkSummary}";

                var ft = new FormattedText(
                    info,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    tf,
                    12,
                    Brushes.White);

                var bg = new Rect(8, 8, 520, 38);
                ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), null, bg);
                ctx.DrawText(ft, new Point(12, 12));
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
out vec4 FragColor;
void main() { FragColor = vec4(1.0, 1.0, 1.0, 1.0); }";
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
                        var model =
                            Matrix4.CreateScale(node.VisualScale) *
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

                    if (_diagVerbose && i == 0)
                    {
                        Debug.WriteLine($"[GLDiag] uMVP loc={_locMvp}, transpose={_transposeUniform}, order={(_forcePvmOrder ? "P*V*M" : "M*V*P")}");
                    }

                    GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
                }
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

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var pt = e.GetPosition(this);
            var props = e.GetCurrentPoint(this).Properties;
            var mods = e.KeyModifiers;

            if (props.IsLeftButtonPressed)
            {
                _dragging = true;
                _lastPt = pt;
                try
                {
                    e.Pointer.Capture(this);
                }
                catch
                {
                }

                e.Handled = true;
                Focus();
            }

            if (props.IsRightButtonPressed || props.IsMiddleButtonPressed ||
                (mods.HasFlag(KeyModifiers.Shift) && props.IsLeftButtonPressed))
            {
                _panning = true;
                _lastPt = pt;
                try
                {
                    e.Pointer.Capture(this);
                }
                catch
                {
                }

                e.Handled = true;
                Focus();
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragging || _panning)
            {
                _dragging = false;
                _panning = false;

                try
                {
                    e.Pointer.Capture(null);
                }
                catch
                {
                }

                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var pt = e.GetPosition(this);
            var dx = pt.X - _lastPt.X;
            var dy = pt.Y - _lastPt.Y;

            if (_panning)
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
            }
            else if (_dragging)
            {
                _cam.Yaw += (float)(dx * 0.01);
                _cam.Pitch += (float)(-dy * 0.01);
                _cam.Clamp();
            }

            _lastPt = pt;
            e.Handled = true;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var delta = e.Delta.Y;
            var factor = (float)Math.Pow(1.1, -delta);
            _cam.Distance *= factor;
            _cam.Clamp();
            e.Handled = true;
        }

        private static RenderNode[] GetRenderNodes()
        {
            var snapshot = EngineServices.Scene.GetSnapshot();
            return CoreSceneAdapter.ToRenderNodes(snapshot);
        }

        private static RenderSceneSnapshot GetRenderSceneSnapshot()
        {
            var snapshot = EngineServices.Scene.GetSnapshot();
            return CoreSceneAdapter.ToRenderSceneSnapshot(snapshot);
        }
    }
}
