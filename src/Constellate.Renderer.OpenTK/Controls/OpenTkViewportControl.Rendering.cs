using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Constellate.Core.Messaging;
using Constellate.Core.Scene;
using Constellate.Renderer.OpenTK.Diagnostics;
using Constellate.Renderer.OpenTK.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using GLPixelFormat = global::OpenTK.Graphics.OpenGL4.PixelFormat;
using GLPixelType = global::OpenTK.Graphics.OpenGL4.PixelType;
using V2i = global::OpenTK.Mathematics.Vector2i;

namespace Constellate.Renderer.OpenTK.Controls
{
    public partial class OpenTkViewportControl
    {
        private void EnsureGl(int w, int h)
        {
            if (_glWindow is null)
            {
                var windowSettings = GameWindowSettings.Default;
                var nativeSettings = new NativeWindowSettings
                {
                    StartVisible = false,
                    Size = new V2i(Math.Max(1, w), Math.Max(1, h)),
                    Title = "Constellate.HiddenGL"
                };

                _glWindow = new GameWindow(windowSettings, nativeSettings);
                _glWindow.MakeCurrent();
                CreateTrianglePipeline();
                RecreateFbo(w, h);
                _glInitialized = true;
                return;
            }

            _glWindow.MakeCurrent();

            if (!_glInitialized)
            {
                CreateTrianglePipeline();
                _glInitialized = true;
            }

            if (_fbo == 0 || _colorTex == 0 || _depthRbo == 0 || _texW != w || _texH != h)
            {
                RecreateFbo(w, h);
            }
        }

        private void RecreateFbo(int w, int h)
        {
            DestroyFbo();
            _texW = Math.Max(1, w);
            _texH = Math.Max(1, h);

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
                    // Debug.WriteLine("kag] Warning: uMVP location < 0; uniform may be optimized out.");
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
                // Debug.WriteLine($"[OpenTkViewportControl] Teardown exception: {ex.Message}");
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

            // Clear stencil alongside depth/color so halo masking starts from a clean slate.
            GL.ClearStencil(0);

            var tColor = (_angleDeg % 360.0) / 360.0;
            var r = (float)(0.25 + 0.75 * Math.Abs(Math.Sin(tColor * Math.PI * 2)));
            var g = (float)(0.25 + 0.75 * Math.Abs(Math.Sin((tColor + 0.33) * Math.PI * 2)));
            var b = (float)(0.25 + 0.75 * Math.Abs(Math.Sin((tColor + 0.66) * Math.PI * 2)));

            if (_noClear)
            {
                GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            }
            else
            {
                GL.ClearColor(r, g, b, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
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

            var settings = EngineServices.Settings;
            var haloMode = (settings.NodeHaloMode ?? "2d").Trim().ToLowerInvariant();
            var draw3dHalo = string.Equals(haloMode, "3d", StringComparison.Ordinal) ||
                             string.Equals(haloMode, "both", StringComparison.Ordinal);
            var occlusionMode = (settings.NodeHaloOcclusionMode ?? "hollow").Trim().ToLowerInvariant();
            var focusMul = Math.Clamp(settings.NodeFocusHaloRadiusMultiplier, 0.5f, 3f);
            var selectionMul = Math.Clamp(settings.NodeSelectionHaloRadiusMultiplier, 0.5f, 3f);
            var highlightOpacity = Math.Clamp(settings.NodeHighlightOpacity, 0f, 1f);

            // Set up stencil so node interiors are marked; hollow halos can then skip those fragments.
            if (!_selfTest)
            {
                GL.Enable(EnableCap.StencilTest);
                GL.StencilMask(0xFF);
                GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
                GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
            }

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

                    // Base primitive vertices for this node
                    var primitiveVertices = GetPrimitiveVertices(node.Primitive);
                    GL.BufferData(BufferTarget.ArrayBuffer, primitiveVertices.Length * sizeof(float), primitiveVertices, BufferUsageHint.DynamicDraw);

                    // --- Main node body first (writes depth and stencil=1 where drawn) ---
                    Matrix4 model = BuildRenderNodeModelMatrix(node, time);
                    Matrix4 mvp;
                    if (_forceIdentity)
                    {
                        mvp = Matrix4.Identity;
                    }
                    else if (_forcePvmOrder)
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

                    GL.DrawArrays(PrimitiveType.Triangles, 0, primitiveVertices.Length / 3);

                    // --- 3D halo volume as a node-effect field (optional) ---
                    if (draw3dHalo && (node.IsFocused || node.IsSelected))
                    {
                        var baseScale = ComputeRenderScale(node);
                        double radiusMul;

                        if (node.IsFocused && node.IsSelected)
                        {
                            radiusMul = Math.Max(focusMul, selectionMul);
                        }
                        else if (node.IsFocused)
                        {
                            radiusMul = focusMul;
                        }
                        else
                        {
                            radiusMul = selectionMul;
                        }

                        var haloScale = new Vector3(
                            baseScale.X * (float)radiusMul,
                            baseScale.Y * (float)radiusMul,
                            baseScale.Z * (float)radiusMul);

                        var haloModel =
                            Matrix4.CreateScale(haloScale) *
                            Matrix4.CreateRotationX(node.RotationEuler.X) *
                            Matrix4.CreateRotationY(node.RotationEuler.Y + time + node.Phase) *
                            Matrix4.CreateRotationZ(node.RotationEuler.Z) *
                            Matrix4.CreateTranslation(node.Position);

                        Matrix4 haloMvp;
                        if (_forceIdentity)
                        {
                            haloMvp = Matrix4.Identity;
                        }
                        else if (_forcePvmOrder)
                        {
                            haloMvp = haloModel;
                            haloMvp = view * haloMvp;
                            haloMvp = proj * haloMvp;
                        }
                        else
                        {
                            haloMvp = haloModel;
                            haloMvp = haloMvp * view;
                            haloMvp = haloMvp * proj;
                        }

                        if (_locMvp >= 0)
                        {
                            GL.UniformMatrix4(_locMvp, _transposeUniform, ref haloMvp);
                        }

                        if (_locColor >= 0)
                        {
                            var baseAlpha = node.IsFocused ? 0.75f : 0.6f;
                            var alpha = Math.Clamp(highlightOpacity * baseAlpha, 0.0f, 1.0f);
                            float hr, hg, hb;

                            if (node.IsFocused)
                            {
                                hr = 250f / 255f;
                                hg = 204f / 255f;
                                hb = 21f / 255f;
                            }
                            else
                            {
                                hr = 96f / 255f;
                                hg = 165f / 255f;
                                hb = 250f / 255f;
                            }

                            GL.Uniform4(_locColor, hr, hg, hb, alpha);
                        }

                        var useOccluding = string.Equals(occlusionMode, "occluding", StringComparison.Ordinal);

                        // Halos should not write depth so nodes remain visible; stencil controls where they show.
                        GL.DepthMask(false);

                        if (useOccluding)
                        {
                            // OCCLUDING MODE:
                            // - ignore stencil (overlay everything in front of the framebuffer)
                            // - disable depth test so halo color simply overlays node + neighbors, modulated by alpha.
                            if (!_noDepth)
                            {
                                GL.Disable(EnableCap.DepthTest);
                            }

                            // Disable stencil test for occluding halos.
                            if (!_selfTest)
                            {
                                GL.Disable(EnableCap.StencilTest);
                            }
                        }
                        else
                        {
                            // HOLLOW MODE:
                            // - keep depth test enabled so halo respects depth vs other geometry,
                            // - but only draw halo where stencil == 0 (i.e., where node did NOT draw).
                            if (!_noDepth)
                            {
                                GL.Enable(EnableCap.DepthTest);
                            }

                            if (!_selfTest)
                            {
                                GL.StencilMask(0x00); // do not modify stencil while drawing halos
                                GL.StencilFunc(StencilFunction.Equal, 0, 0xFF);
                            }
                        }

                        GL.DrawArrays(PrimitiveType.Triangles, 0, primitiveVertices.Length / 3);

                        // Restore depth state.
                        if (useOccluding && !_noDepth)
                        {
                            GL.Enable(EnableCap.DepthTest);
                        }
                        GL.DepthMask(true);

                        // Restore stencil state for subsequent nodes and edges.
                        if (!_selfTest)
                        {
                            if (useOccluding)
                            {
                                GL.Enable(EnableCap.StencilTest);
                            }

                            GL.StencilMask(0xFF);
                            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
                            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
                        }
                    }

                    // --- Outline edges (2D-like stroke on top of volumetric body) ---
                    var edgeVertices = GetPrimitiveEdgeVertices(node.Primitive);
                    if (edgeVertices.Length > 0 && _locColor >= 0)
                    {
                        // Edge outlines should not be blocked by stencil; disable stencil around them.
                        if (!_selfTest)
                        {
                            GL.Disable(EnableCap.StencilTest);
                        }

                        var outlineColor = ParseAppearanceColor(
                            node.OutlineColor,
                            Math.Clamp(node.Opacity * 0.95f, 0.35f, 1.0f));

                        GL.Uniform4(_locColor, outlineColor.X, outlineColor.Y, outlineColor.Z, outlineColor.W);
                        GL.LineWidth(node.IsFocused ? 2.4f : node.IsSelected ? 2.0f : 1.35f);
                        GL.BufferData(BufferTarget.ArrayBuffer, edgeVertices.Length * sizeof(float), edgeVertices, BufferUsageHint.DynamicDraw);
                        GL.DrawArrays(PrimitiveType.Lines, 0, edgeVertices.Length / 3);

                        if (!_selfTest)
                        {
                            // Re-enable stencil for subsequent nodes / halos.
                            GL.Enable(EnableCap.StencilTest);
                            GL.StencilMask(0xFF);
                            GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
                            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
                        }
                    }
                }

                // After nodes/halos/outlines, draw 3D group volumes as translucent hulls.
                DrawGroupVolumesGl(renderSnapshot, view, proj);

                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }

            if (_scissorMark)
            {
                try
                {
                    // Scissor debug mark only affects color; depth/stencil state is restored below.
                    GL.Disable(EnableCap.DepthTest);
                    GL.Disable(EnableCap.StencilTest);
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

                    if (!_selfTest)
                    {
                        GL.Enable(EnableCap.StencilTest);
                        GL.StencilMask(0xFF);
                        GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
                        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
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

            // Readback and flip for Avalonia bitmap
            var strideBytes = _texW * 4;
            var capacityBytes = strideBytes * _texH;
            if (_readbackRaw is null || _readbackRaw.Length < capacityBytes ||
                _readbackFlipped is null || _readbackFlipped.Length < capacityBytes)
            {
                _readbackRaw = new byte[capacityBytes];
                _readbackFlipped = new byte[capacityBytes];
            }

            GL.ReadPixels(0, 0, _texW, _texH, GLPixelFormat.Bgra, GLPixelType.UnsignedByte, _readbackRaw);

            for (var y = 0; y < _texH; y++)
            {
                System.Buffer.BlockCopy(
                    _readbackRaw,
                    (_texH - 1 - y) * strideBytes,
                    _readbackFlipped,
                    y * strideBytes,
                    strideBytes);
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
                // Debug.WriteLine($"[GLDiag] Center pixel {center}; near clear={nearClear}");

                if (nearClear)
                {
                    // Debug.WriteLine("[GLDiag] Geometry might not be contributing this frame (center ~ clear). Check MVP/uniforms/VAO/depth.");
                }
            }

            if (_glBitmap is not null)
            {
                using var fb = _glBitmap.Lock();
                var destStride = fb.RowBytes;
                var rowBytes = Math.Min(destStride, strideBytes);

                for (var y = 0; y < _texH; y++)
                {
                    var srcOffset = y * strideBytes;
                    var destPtr = fb.Address + (y * destStride);
                    Marshal.Copy(_readbackFlipped, srcOffset, destPtr, rowBytes);
                }
            }

            // Disable stencil test at the end of the frame so future draws start clean.
            if (!_selfTest)
            {
                GL.Disable(EnableCap.StencilTest);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void DrawGroupVolumesGl(RenderSceneSnapshot snapshot, Matrix4 view, Matrix4 proj)
        {
            if (_selfTest || snapshot.Groups.Length == 0 || snapshot.Nodes.Length == 0)
            {
                return;
            }

            var nodesById = snapshot.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var settings = EngineServices.Settings;

            foreach (var group in snapshot.Groups)
            {
                if (group.NodeIds is null || group.NodeIds.Length == 0)
                {
                    continue;
                }

                var haveAny = false;
                float minX = 0f, minY = 0f, minZ = 0f;
                float maxX = 0f, maxY = 0f, maxZ = 0f;

                foreach (var nodeId in group.NodeIds)
                {
                    if (!nodesById.TryGetValue(nodeId, out var node))
                    {
                        continue;
                    }

                    var p = node.Position;
                    if (!haveAny)
                    {
                        minX = maxX = p.X;
                        minY = maxY = p.Y;
                        minZ = maxZ = p.Z;
                        haveAny = true;
                    }
                    else
                    {
                        if (p.X < minX) minX = p.X;
                        if (p.X > maxX) maxX = p.X;
                        if (p.Y < minY) minY = p.Y;
                        if (p.Y > maxY) maxY = p.Y;
                        if (p.Z < minZ) minZ = p.Z;
                        if (p.Z > maxZ) maxZ = p.Z;
                    }
                }

                if (!haveAny)
                {
                    continue;
                }

                var appearance = group.Appearance ?? GroupAppearance.Default;
                var padding = Math.Max(0f, appearance.Padding);

                minX -= padding;
                maxX += padding;
                minY -= padding;
                maxY += padding;
                minZ -= padding;
                maxZ += padding;

                var center = new Vector3(
                    (minX + maxX) * 0.5f,
                    (minY + maxY) * 0.5f,
                    (minZ + maxZ) * 0.5f);

                var halfSize = new Vector3(
                    Math.Max(0.01f, (maxX - minX) * 0.5f),
                    Math.Max(0.01f, (maxY - minY) * 0.5f),
                    Math.Max(0.01f, (maxZ - minZ) * 0.5f));

                // PrimitiveMeshBuilder box uses halfExtent=0.58f; scale so that 0.58 * scale ~= halfSize.
                const float baseHalfExtent = 0.58f;
                var scale = new Vector3(
                    halfSize.X / baseHalfExtent,
                    halfSize.Y / baseHalfExtent,
                    halfSize.Z / baseHalfExtent);

                var groupModel =
                    Matrix4.CreateScale(scale) *
                    Matrix4.CreateTranslation(center);

                Matrix4 groupMvp;
                if (_forceIdentity)
                {
                    groupMvp = Matrix4.Identity;
                }
                else if (_forcePvmOrder)
                {
                    groupMvp = groupModel;
                    groupMvp = view * groupMvp;
                    groupMvp = proj * groupMvp;
                }
                else
                {
                    groupMvp = groupModel;
                    groupMvp = groupMvp * view;
                    groupMvp = groupMvp * proj;
                }

                if (_locMvp >= 0)
                {
                    GL.UniformMatrix4(_locMvp, _transposeUniform, ref groupMvp);
                }

                var combinedOpacity = Math.Clamp(appearance.Opacity * settings.GroupOverlayOpacity, 0f, 1f);
                var fill = ParseAppearanceColor(appearance.FillColor, combinedOpacity);

                if (_locColor >= 0)
                {
                    GL.Uniform4(_locColor, fill.X, fill.Y, fill.Z, fill.W);
                }

                if (!_noDepth)
                {
                    GL.Enable(EnableCap.DepthTest);
                }
                GL.DepthMask(false);

                if (!_selfTest)
                {
                    // Groups are independent of node stencil; disable stencil while drawing volumes.
                    GL.Disable(EnableCap.StencilTest);
                }

                var boxVerts = PrimitiveMeshBuilder.GetVertices("box");
                GL.BufferData(BufferTarget.ArrayBuffer, boxVerts.Length * sizeof(float), boxVerts, BufferUsageHint.DynamicDraw);
                GL.DrawArrays(PrimitiveType.Triangles, 0, boxVerts.Length / 3);

                // Outline
                var outline = ParseAppearanceColor(appearance.OutlineColor, combinedOpacity);
                if (_locColor >= 0)
                {
                    GL.Uniform4(_locColor, outline.X, outline.Y, outline.Z, outline.W);
                }

                var edgeVerts = PrimitiveMeshBuilder.GetEdgeVertices("box");
                if (edgeVerts.Length > 0)
                {
                    GL.LineWidth(1.5f);
                    GL.BufferData(BufferTarget.ArrayBuffer, edgeVerts.Length * sizeof(float), edgeVerts, BufferUsageHint.DynamicDraw);
                    GL.DrawArrays(PrimitiveType.Lines, 0, edgeVerts.Length / 3);
                }

                GL.DepthMask(true);

                if (!_selfTest)
                {
                    // Restore stencil defaults for subsequent node/halo passes or next frame.
                    GL.Enable(EnableCap.StencilTest);
                    GL.StencilMask(0xFF);
                    GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
                    GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
                }
            }
        }

        private Matrix4 ComputeView()
        {
            return ViewportCameraMath.ComputeView(
                _cam.Yaw,
                _cam.Pitch,
                _cam.Distance,
                _cam.Target);
        }

        private Matrix4 ComputeProjection()
        {
            return ViewportCameraMath.ComputeProjection(_texW, _texH);
        }

        private Matrix4 BuildRenderNodeModelMatrix(RenderNode node, float time)
        {
            return
                Matrix4.CreateScale(ComputeRenderScale(node)) *
                Matrix4.CreateRotationX(node.RotationEuler.X) *
                Matrix4.CreateRotationY(node.RotationEuler.Y + time + node.Phase) *
                Matrix4.CreateRotationZ(node.RotationEuler.Z) *
                Matrix4.CreateTranslation(node.Position);
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

        private static float[] GetPrimitiveVertices(string? primitive) =>
            PrimitiveMeshBuilder.GetVertices(primitive);

        private static float[] GetPrimitiveEdgeVertices(string? primitive) =>
            PrimitiveMeshBuilder.GetEdgeVertices(primitive);

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
    }
}
