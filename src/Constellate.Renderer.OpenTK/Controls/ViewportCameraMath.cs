using System;
using Avalonia;
using OpenTK.Mathematics;

namespace Constellate.Renderer.OpenTK.Controls
{
    internal static class ViewportCameraMath
    {
        public static Vector3 ComputeMoveDragWorldDelta(
            Point dragStart,
            Point currentPoint,
            float yaw,
            float pitch,
            float distance)
        {
            var dx = currentPoint.X - dragStart.X;
            var dy = currentPoint.Y - dragStart.Y;
            var cy = MathF.Cos(yaw);
            var sy = MathF.Sin(yaw);
            var cp = MathF.Cos(pitch);
            var sp = MathF.Sin(pitch);
            var cameraOutward = Vector3.Normalize(new Vector3(
                cy * cp,
                sp,
                sy * cp));
            var right = Vector3.Normalize(Vector3.Cross(cameraOutward, Vector3.UnitY));
            var up = Vector3.Normalize(Vector3.Cross(right, cameraOutward));
            var dragScale = distance * 0.0025f;

            return (-right * (float)dx * dragScale) - (up * (float)dy * dragScale);
        }

         public static bool TryProjectScreenPointToPlane(
             Point screenPoint,
             Rect bounds,
             Matrix4 view,
             Matrix4 proj,
             Vector3 planePoint,
             Vector3 planeNormal,
             out Vector3 hitPoint)
         {
             hitPoint = default;

             if (bounds.Width <= 0 || bounds.Height <= 0)
             {
                 return false;
             }

             var xNdc = (float)(((screenPoint.X - bounds.X) / bounds.Width) * 2.0 - 1.0);
             var yNdc = (float)(1.0 - ((screenPoint.Y - bounds.Y) / bounds.Height) * 2.0);

             var invViewProj = Matrix4.Invert(view * proj);

             var near = new global::OpenTK.Mathematics.Vector4(xNdc, yNdc, -1f, 1f);
             var far = new global::OpenTK.Mathematics.Vector4(xNdc, yNdc, 1f, 1f);

             var nearWorld = invViewProj * near;
             nearWorld /= nearWorld.W;
             var farWorld = invViewProj * far;
             farWorld /= farWorld.W;

             var origin = nearWorld.Xyz;
             var dir = Vector3.Normalize(farWorld.Xyz - origin);
             var denom = Vector3.Dot(dir, planeNormal);

             if (MathF.Abs(denom) < 1e-4f)
             {
                 return false;
             }

             var t = Vector3.Dot(planePoint - origin, planeNormal) / denom;
             if (t <= 0f) return false;

             hitPoint = origin + (dir * t);
             return true;
         }

        public static bool TryProjectWorldPoint(
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

        public static Matrix4 ComputeView(float yaw, float pitch, float distance, Vector3 target)
        {
            var cy = MathF.Cos(yaw);
            var sy = MathF.Sin(yaw);
            var cp = MathF.Cos(pitch);
            var sp = MathF.Sin(pitch);

            var eye = new Vector3(
                target.X + (distance * cp * cy),
                target.Y + (distance * sp),
                target.Z + (distance * cp * sy));

            return Matrix4.LookAt(eye, target, Vector3.UnitY);
        }

        public static Matrix4 ComputeProjection(int texW, int texH)
        {
            var fovy = MathHelper.DegreesToRadians(60f);
            var aspect = Math.Max(0.001f, (float)texW / Math.Max(1f, texH));
            return Matrix4.CreatePerspectiveFieldOfView(fovy, aspect, 0.1f, 100f);
        }
    }
}
