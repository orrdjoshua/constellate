using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using V2i = OpenTK.Mathematics.Vector2i;

namespace Constellate.Renderer.OpenTK.Controls
{
    /// <summary>
    /// OpenTK-backed viewport control (Stage 1).
    /// - Preferred path: Render offscreen with OpenGL (hidden GameWindow + FBO), ReadPixels to CPU, upload to WriteableBitmap, and draw.
    /// - Fallback path: Prior software-drawn rotating triangle (always available).
    ///
    /// To disable GL at runtime, set environment variable CONSTELLATE_GL=0 (process env) before launching.
    /// </summary>
    public class OpenTkViewportControl : Control
    {
        private readonly DispatcherTimer _timer;
        private double _angleDeg;

        // Feature flag: prefer GL unless explicitly disabled
        private readonly bool _preferGl = !string.Equals(Environment.GetEnvironmentVariable("CONSTELLATE_GL"), "0", StringComparison.OrdinalIgnoreCase);

        // GL resources (Stage 1 offscreen)
        private GameWindow? _glWindow;
        private bool _glInitialized;
        private bool _glFailed; // once failed, stick to fallback for session

        private int _fbo;
        private int _colorTex;
        private int _depthRbo;
        private int _texW;
        private int _texH;

        private byte[]? _readbackRaw;    // raw from GL (bottom-up)
        private byte[]? _readbackFlipped; // top-down for Avalonia
        private WriteableBitmap? _glBitmap;

        public OpenTkViewportControl()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 FPS
            _timer.Tick += (_, __) =>
            {
                // drive animation (both paths use angle)
                _angleDeg = (_angleDeg + 60.0 / 60.0) % 360.0;

                if (_preferGl && !_glFailed)
                {
                    try
                    {
                        RenderGlFrame();
                    }
                    catch
                    {
                        // If anything goes wrong, disable GL path for stability, keep software fallback
                        _glFailed = true;
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

            // Software fallback: rotating triangle (previous placeholder implementation)
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

            using var geom = new StreamGeometry();
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

            // Create hidden window + context once
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

            // (Re)create FBO resources sized to control
            if (_fbo == 0 || _texW != width || _texH != height)
            {
                RecreateFbo(width, height);
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
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _texW, _texH, 0, PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
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

            // Prepare CPU buffers
            var stride = _texW * 4;
            var capacity = stride * _texH;
            _readbackRaw = _readbackRaw is { Length: > 0 } rb && rb.Length >= capacity ? rb : new byte[capacity];
            _readbackFlipped = _readbackFlipped is { Length: > 0 } rf && rf.Length >= capacity ? rf : new byte[capacity];

            // Prepare WriteableBitmap at matching size (dispose previous to avoid leaks)
            _glBitmap?.Dispose();
            _glBitmap = new WriteableBitmap(new PixelSize(_texW, _texH), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

            // Unbind to avoid accidental state leaks
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void DestroyFbo()
        {
            if (_depthRbo != 0) { GL.DeleteRenderbuffer(_depthRbo); _depthRbo = 0; }
            if (_colorTex != 0) { GL.DeleteTexture(_colorTex); _colorTex = 0; }
            if (_fbo != 0) { GL.DeleteFramebuffer(_fbo); _fbo = 0; }
            _texW = _texH = 0;
        }

        private void TeardownGl()
        {
            try
            {
                if (_glWindow is not null)
                {
                    _glWindow.MakeCurrent();
                    DestroyFbo();
                    _glWindow.Context?.MakeNoneCurrent();
                    _glWindow.Close();
                    _glWindow.Dispose();
                }
            }
            catch { /* ignore shutdown errors */ }
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

            // Bind offscreen FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            GL.Viewport(0, 0, _texW, _texH);

            // Simple animated clear — validates the full GL→CPU→Bitmap→Avalonia path
            var t = (_angleDeg % 360.0) / 360.0;
            float r = (float)(0.25 + 0.75 * Math.Abs(Math.Sin(t * Math.PI * 2)));
            float g = (float)(0.25 + 0.75 * Math.Abs(Math.Sin((t + 0.33) * Math.PI * 2)));
            float b = (float)(0.25 + 0.75 * Math.Abs(Math.Sin((t + 0.66) * Math.PI * 2)));
            GL.ClearColor(r, g, b, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Read back into CPU buffer (bottom-up); then flip to top-down for Avalonia
            var stride = _texW * 4;
            if (_readbackRaw is null || _readbackRaw.Length < stride * _texH ||
                _readbackFlipped is null || _readbackFlipped.Length < stride * _texH)
            {
                // Should not happen due to RecreateFbo, but guard anyway
                _readbackRaw = new byte[stride * _texH];
                _readbackFlipped = new byte[stride * _texH];
            }

            GL.ReadPixels(0, 0, _texW, _texH, PixelFormat.Bgra, PixelType.UnsignedByte, _readbackRaw);

            for (int y = 0; y < _texH; y++)
            {
                Buffer.BlockCopy(_readbackRaw, (_texH - 1 - y) * stride, _readbackFlipped, y * stride, stride);
            }

            // Upload to WriteableBitmap
            if (_glBitmap is not null)
            {
                using var fb = _glBitmap.Lock();
                var destStride = fb.RowBytes;
                var rowBytes = Math.Min(destStride, stride);

                // Copy row-by-row to respect platform stride
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
    }
}
