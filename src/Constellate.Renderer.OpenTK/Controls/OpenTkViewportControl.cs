using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common; // ContextFlags
using OpenTK.Mathematics;      // Matrix4, Vector3
using V2i = OpenTK.Mathematics.Vector2i;
// Disambiguation aliases
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using GLPixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using GLPixelType = OpenTK.Graphics.OpenGL4.PixelType;
using Constellate.Renderer.OpenTK.Diagnostics; // NEW

namespace Constellate.Renderer.OpenTK.Controls
{
    /// <summary>
    /// OpenTK-backed viewport control (offscreen FBO → CPU readback → WriteableBitmap).
    /// - GL path is preferred; fallback is software triangle.
    /// - Toggle GL off by setting process env CONSTELLATE_GL=0 before launch.
    /// - Enable verbose diagnostics by setting CONSTELLATE_GL_DIAG=1 before launch.
    /// </summary>
    public class OpenTkViewportControl : Control
    {
        private readonly DispatcherTimer _timer;
        private double _angleDeg;

        // Feature flags
        private readonly bool _preferGl =
            !string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL"), "0", StringComparison.OrdinalIgnoreCase);

        private readonly bool _diagVerbose =
            string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL_DIAG"), "1", StringComparison.OrdinalIgnoreCase);

        // GL resources (offscreen)
        private GameWindow? _glWindow;
        private bool _glInitialized;
        private bool _glFailed;

        private int _fbo;
        private int _colorTex;
        private int _depthRbo;
        private int _texW;
        private int _texH;

        private byte[]? _readbackRaw;     // bottom-up from GL
        private byte[]? _readbackFlipped; // top-down for Avalonia
        private WriteableBitmap? _glBitmap;

        // GL pipeline (triangle)
        private int _program;
        private int _vao;
        private int _vbo;
        private int _locMvp;

        // Time (seconds) for per-frame animation
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        // Orbit camera (yaw/pitch/distance)
        private struct OrbitCamera
        {
            public float Yaw;      // radians
            public float Pitch;    // radians
            public float Distance; // units

            public void Clamp()
            {
                Pitch = Math.Clamp(Pitch, -1.2f, 1.2f);
                Distance = Math.Clamp(Distance, 0.25f, 10f);
                // wrap yaw for numeric stability
                if (Yaw > MathF.PI) Yaw -= 2 * MathF.PI;
                if (Yaw < -MathF.PI) Yaw += 2 * MathF.PI;
            }
        }

        private OrbitCamera _cam = new OrbitCamera { Yaw = 0f, Pitch = 0f, Distance = 2.0f };

        // Minimal scene node (position, uniform scale, phase for animation)
        private struct Node
        {
            public Vector3 Position;
            public float Scale;
            public float Phase;
        }

        private Node[] _nodes =
        {
            new Node { Position = new Vector3(-0.8f, -0.3f, 0.0f), Scale = 0.6f, Phase = 0.0f  },
            new Node { Position = new Vector3( 0.9f,  0.2f, 0.0f), Scale = 0.5f, Phase = 1.2f  },
            new Node { Position = new Vector3( 0.0f,  0.7f, 0.0f), Scale = 0.7f, Phase = 2.35f }
        };

        // Pointer state
        private bool _dragging;
        private Point _lastPt;

        public OpenTkViewportControl()
        {
            Focusable = true;
            IsHitTestVisible = true;

            AddHandler(PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
            AddHandler(PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
            AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, handledEventsToo: true);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 FPS
            _timer.Tick += (_, __) =>
            {
                _angleDeg = (_angleDeg + 60.0 / 60.0) % 360.0;

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

            // Background
            ctx.DrawRectangle(Brushes.Black, null, bounds);

            // If GL path is active and produced a bitmap, draw it
            if (_preferGl && !_glFailed && _glBitmap is not null)
            {
                var src = new Rect(0, 0, _glBitmap.PixelSize.Width, _glBitmap.PixelSize.Height);
                ctx.DrawImage(_glBitmap, src, bounds);
                return;
            }

            // Software fallback: rotating triangle
            DrawSoftwareTriangle(ctx, bounds);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            try { _timer.Stop(); } catch { /* ignore */ }
            TeardownGl();
            _glBitmap?.Dispose();
            _glBitmap = null;
        }

        private void DrawSoftwareTriangle(DrawingContext ctx, Rect bounds)
        {
            var rect = bounds.Deflate(20);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            var center = rect.Center;
            var r = Math.Min(rect.Width, rect.Height) / 3.0;

            Point P(double deg)
                => new Point(
                    center.X + Math.Cos(deg * Math.PI / 180.0) * r,
                    center.Y + Math.Sin(deg * Math.PI / 180.0) * r
                );

            var p1 = P(_angleDeg);
            var p2 = P(_angleDeg + 120);
            var p3 = P(_angleDeg + 240);

            var geom = new StreamGeometry();
            using (var gc = geom.Open())
            {
                gc.BeginFigure(p1, true);
                gc.LineTo(p2);
                gc.LineTo(p3);
                gc.EndFigure(true);
            }

            var fill = new SolidColorBrush(Color.FromArgb(255, 34, 139, 34)); // ForestGreen
            var stroke = new Pen(Brushes.White, 2);
            ctx.DrawGeometry(fill, stroke, geom);
        }

        private void EnsureGl(int width, int height)
        {
            if (_glInitialized && width == _texW && height == _texH) return;

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

            if (_program == 0 || _vao == 0 || _vbo == 0)
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
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _texW, _texH, 0, GLPixelFormat.Bgra, GLPixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorTex, 0);

            _depthRbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, _texW, _texH);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthRbo);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new InvalidOperationException($"FBO incomplete: {status}");
            }

            // CPU buffers
            var stride = _texW * 4;
            var capacity = stride * _texH;
            _readbackRaw = _readbackRaw is { Length: > 0 } rb && rb.Length >= capacity ? rb : new byte[capacity];
            _readbackFlipped = _readbackFlipped is { Length: > 0 } rf && rf.Length >= capacity ? rf : new byte[capacity];

            // WriteableBitmap
            _glBitmap?.Dispose();
            _glBitmap = new WriteableBitmap(new PixelSize(_texW, _texH), new Vector(96, 96), AvaloniaPixelFormat.Bgra8888, AlphaFormat.Premul);

            // Unbind
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
            if (_depthRbo != 0) { GL.DeleteRenderbuffer(_depthRbo); _depthRbo = 0; }
            if (_colorTex != 0) { GL.DeleteTexture(_colorTex); _colorTex = 0; }
            if (_fbo != 0) { GL.DeleteFramebuffer(_fbo); _fbo = 0; }
            _texW = _texH = 0;
        }

        private void CreateTrianglePipeline()
        {
            const string vsSrc = @"#version 330 core
layout(location=0) in vec3 aPos;
uniform mat4 uMVP;
void main() {
  gl_Position = uMVP * vec4(aPos, 1.0);
}";
            const string fsSrc = @"#version 330 core
out vec4 FragColor;
void main() { FragColor = vec4(1.0, 1.0, 1.0, 1.0); }";

            int vs = CompileShader(ShaderType.VertexShader, vsSrc);
            int fs = CompileShader(ShaderType.FragmentShader, fsSrc);

            _program = GL.CreateProgram();
            GL.AttachShader(_program, vs);
            GL.AttachShader(_program, fs);
            GL.LinkProgram(_program);
            GL.GetProgram(_program, GetProgramParameterName.LinkStatus, out int linked);
            if (linked == 0)
            {
                string info = GL.GetProgramInfoLog(_program);
                GL.DeleteProgram(_program);
                _program = 0;
                GL.DeleteShader(vs);
                GL.DeleteShader(fs);
                throw new InvalidOperationException($"GL program link failed: {info}");
            }

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            // Triangle geometry (3D positions)
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

            // Unbind
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            if (_diagVerbose)
            {
                GLDiagnostics.CheckError("CreateTrianglePipeline");
                GLDiagnostics.DumpBasicState("After pipeline creation");
                if (_locMvp < 0) Debug.WriteLine("[GLDiag] Warning: uMVP location < 0; uniform may be optimized out.");
            }
        }

        private static int CompileShader(ShaderType type, string src)
        {
            int s = GL.CreateShader(type);
            GL.ShaderSource(s, src);
            GL.CompileShader(s);
            GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0)
            {
                string info = GL.GetShaderInfoLog(s);
                GL.DeleteShader(s);
                throw new InvalidOperationException($"{type} compile failed: {info}");
            }
            return s;
        }

        private void DestroyGlPipeline()
        {
            if (_vbo != 0) { GL.DeleteBuffer(_vbo); _vbo = 0; }
            if (_vao != 0) { GL.DeleteVertexArray(_vao); _vao = 0; }
            if (_program != 0) { GL.DeleteProgram(_program); _program = 0; }
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
            if (w <= 0 || h <= 0) return;

            EnsureGl(w, h);

            // Ensure the GL context is current for this thread every frame
            if (_glWindow is not null)
                _glWindow.MakeCurrent();

            if (_diagVerbose) GLDiagnostics.CheckError("Before FBO bind");

            // Bind offscreen FBO and explicitly select color attachment 0
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, _texW, _texH);

            // Initialize depth/cull state deterministically
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(true);
            GL.ClearDepth(1.0f);

            // Animated clear (sanity check path)
            var tColor = (_angleDeg % 360.0) / 360.0;
            float r = (float)(0.25 + 0.75 * Math.Abs(Math.Sin(tColor * Math.PI * 2)));
            float g = (float)(0.25 + 0.75 * Math.Abs(Math.Sin((tColor + 0.33) * Math.PI * 2)));
            float b = (float)(0.25 + 0.75 * Math.Abs(Math.Sin((tColor + 0.66) * Math.PI * 2)));
            GL.ClearColor(r, g, b, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (_diagVerbose) GLDiagnostics.CheckError("After Clear");

            // Ensure pipeline
            if (_program == 0 || _vao == 0)
            {
                CreateTrianglePipeline();
            }
            GL.UseProgram(_program);

            // View and projection
            var view = ComputeView();
            var proj = ComputeProjection();

            // Time for per-node animation
            float t = (float)_sw.Elapsed.TotalSeconds;

            GL.BindVertexArray(_vao);

            // Draw each node with its own model matrix
            for (int i = 0; i < _nodes.Length; i++)
            {
                ref var node = ref _nodes[i];

                // Compose model: scale → rotate (Y) → translate
                var model =
                      Matrix4.CreateScale(node.Scale)
                    * Matrix4.CreateRotationY(t + node.Phase)
                    * Matrix4.CreateTranslation(node.Position);

                // uMVP = proj * view * model
                var mvp = model;
                mvp = view * mvp;
                mvp = proj * mvp;

                if (_locMvp < 0) _locMvp = GL.GetUniformLocation(_program, "uMVP");
                if (_locMvp >= 0) GL.UniformMatrix4(_locMvp, false, ref mvp);

                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }

            // Unbind
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            if (_diagVerbose)
            {
                GLDiagnostics.CheckError("After Draw");
                GLDiagnostics.DumpBasicState("Post draw");
            }

            // Readback, flip to top-down, upload to WriteableBitmap
            var stride = _texW * 4;
            if (_readbackRaw is null || _readbackRaw.Length < stride * _texH ||
                _readbackFlipped is null || _readbackFlipped.Length < stride * _texH)
            {
                _readbackRaw = new byte[stride * _texH];
                _readbackFlipped = new byte[stride * _texH];
            }

            GL.ReadPixels(0, 0, _texW, _texH, GLPixelFormat.Bgra, GLPixelType.UnsignedByte, _readbackRaw);

            for (int y = 0; y < _texH; y++)
            {
                System.Buffer.BlockCopy(_readbackRaw, (_texH - 1 - y) * stride, _readbackFlipped, y * stride, stride);
            }

            if (_diagVerbose)
            {
                // Probe center pixel vs clear color to detect geometry contribution.
                var center = GLDiagnostics.SampleCenterPixel(_readbackFlipped, _texW, _texH);
                var clear = new GLDiagnostics.Rgba(
                    (byte)(r * 255.0f),
                    (byte)(g * 255.0f),
                    (byte)(b * 255.0f),
                    255
                );

                bool nearClear = GLDiagnostics.Near(center, clear, tol: 5);
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

                for (int y = 0; y < _texH; y++)
                {
                    var srcOffset = y * stride;
                    var destPtr = fb.Address + y * destStride;
                    Marshal.Copy(_readbackFlipped, srcOffset, destPtr, rowBytes);
                }
            }

            // Unbind FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private Matrix4 ComputeView()
        {
            float cy = MathF.Cos(_cam.Yaw);
            float sy = MathF.Sin(_cam.Yaw);
            float cp = MathF.Cos(_cam.Pitch);
            float sp = MathF.Sin(_cam.Pitch);

            var radius = _cam.Distance;
            var eye = new Vector3(
                radius * cp * cy,
                radius * sp,
                radius * cp * sy
            );

            var target = Vector3.Zero;
            var up = Vector3.UnitY;

            return Matrix4.LookAt(eye, target, up);
        }

        private Matrix4 ComputeProjection()
        {
            float fovy = MathHelper.DegreesToRadians(60f);
            float aspect = Math.Max(0.001f, _texW / Math.Max(1f, (float)_texH));
            return Matrix4.CreatePerspectiveFieldOfView(fovy, aspect, 0.1f, 100f);
        }

        // Input handlers
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var pt = e.GetPosition(this);
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                _dragging = true;
                _lastPt = pt;
                try { e.Pointer.Capture(this); } catch { /* ignore */ }
                e.Handled = true;
                Focus();
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                try { e.Pointer.Capture(null); } catch { /* ignore */ }
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragging) return;

            var pt = e.GetPosition(this);
            var dx = pt.X - _lastPt.X;
            var dy = pt.Y - _lastPt.Y;

            _cam.Yaw += (float)(dx * 0.01);
            _cam.Pitch += (float)(-dy * 0.01);
            _cam.Clamp();

            _lastPt = pt;
            e.Handled = true;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var delta = e.Delta.Y;
            var factor = (float)Math.Pow(1.1, -delta); // wheel up → zoom in
            _cam.Distance *= factor;
            _cam.Clamp();
            e.Handled = true;
        }
    }
}
