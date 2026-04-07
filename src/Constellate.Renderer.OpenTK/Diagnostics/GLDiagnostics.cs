using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;

namespace Constellate.Renderer.OpenTK.Diagnostics
{
    public static class GLDiagnostics
    {
        private static bool _triedEnable;
        private static bool _enabled;
        private static DebugProc? _callback;

        public static void TryEnableDebugOutputOnce()
        {
            if (_triedEnable) return;
            _triedEnable = true;

            try
            {
                // Some drivers allow debug output without a debug context; attempt anyway.
                GL.Enable(EnableCap.DebugOutput);
                GL.Enable(EnableCap.DebugOutputSynchronous);

                _callback = OnDebugMessage;
                GL.DebugMessageCallback(_callback, IntPtr.Zero);

                // Accept everything; we can refine filters later.
                GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DontCare, 0, Array.Empty<int>(), true);
                _enabled = true;
                Debug.WriteLine("[GLDiag] KHR_debug enabled.");
            }
            catch (Exception ex)
            {
                _enabled = false;
                Debug.WriteLine($"[GLDiag] KHR_debug not available or failed to enable: {ex.Message}");
            }
        }

        private static void OnDebugMessage(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            try
            {
                string msg = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, length) ?? "(null)";
                Debug.WriteLine($"[GLDiag] {source}/{type}/{severity} (id={id}): {msg}");
            }
            catch
            {
                // best-effort
            }
        }

        public static void CheckError(string where)
        {
            ErrorCode err;
            bool any = false;
            while ((err = GL.GetError()) != ErrorCode.NoError)
            {
                any = true;
                Debug.WriteLine($"[GLDiag] GL.GetError at {where}: {err}");
            }
            if (!any)
            {
                Debug.WriteLine($"[GLDiag] {where}: OK");
            }
        }

        public static void DumpBasicState(string tag = "")
        {
            try
            {
                GL.GetInteger(GetPName.CurrentProgram, out int prog);
                GL.GetInteger(GetPName.VertexArrayBinding, out int vao);
                GL.GetInteger(GetPName.ArrayBufferBinding, out int vbo);
                GL.GetInteger(GetPName.DrawFramebufferBinding, out int drawFbo);
                GL.GetInteger(GetPName.ReadFramebufferBinding, out int readFbo);
                GL.GetInteger(GetPName.Viewport, out int vp0);
                GL.GetInteger(GetPName.DepthFunc, out int depthFunc);
                bool depth = GL.IsEnabled(EnableCap.DepthTest);
                bool cull = GL.IsEnabled(EnableCap.CullFace);

                Debug.WriteLine($"[GLDiag] State {tag} -> prog={prog}, vao={vao}, vbo={vbo}, drawFBO={drawFbo}, readFBO={readFbo}, depth={depth} func={(DepthFunction)depthFunc}, cull={cull}, viewport0={vp0}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GLDiag] DumpBasicState failed: {ex.Message}");
            }
        }

        public readonly record struct Rgba(byte R, byte G, byte B, byte A)
        {
            public override string ToString() => $"rgba({R},{G},{B},{A})";
        }

        public static Rgba SampleCenterPixel(byte[] topDownBgra8888, int width, int height)
        {
            if (width <= 0 || height <= 0) return new Rgba(0, 0, 0, 0);

            int cx = width / 2;
            int cy = height / 2;
            int stride = width * 4;
            int offset = cy * stride + cx * 4;

            // Note: buffer is BGRA in memory.
            byte b = topDownBgra8888[offset + 0];
            byte g = topDownBgra8888[offset + 1];
            byte r = topDownBgra8888[offset + 2];
            byte a = topDownBgra8888[offset + 3];

            return new Rgba(r, g, b, a);
        }

        public static bool Near(Rgba a, Rgba b, int tol = 6)
        {
            return Math.Abs(a.R - b.R) <= tol &&
                   Math.Abs(a.G - b.G) <= tol &&
                   Math.Abs(a.B - b.B) <= tol &&
                   Math.Abs(a.A - b.A) <= tol;
        }
    }
}
