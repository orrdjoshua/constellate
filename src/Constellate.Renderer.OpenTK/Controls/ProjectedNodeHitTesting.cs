using System;
using System.Collections.Generic;
using Avalonia;
using Constellate.Renderer.OpenTK.Scene;
using OpenTK.Mathematics;

namespace Constellate.Renderer.OpenTK.Controls
{
    internal static class ProjectedNodeHitTesting
    {
        public static string? HitTestProjectedNodeId(
            RenderSceneSnapshot renderSnapshot,
            Point point,
            Matrix4 view,
            Matrix4 proj,
            Rect bounds,
            float time)
        {
            if (renderSnapshot.Nodes.Length == 0)
            {
                return null;
            }

            var bestDepth = float.MaxValue;
            string? bestNodeId = null;

            foreach (var node in renderSnapshot.Nodes)
            {
                if (!TryHitProjectedNodeMesh(point, node, view, proj, bounds, time, out var hitDepth))
                {
                    continue;
                }

                if (hitDepth < bestDepth)
                {
                    bestDepth = hitDepth;
                    bestNodeId = node.Id;
                }
            }

            return bestNodeId;
        }

        public static string[] HitTestProjectedNodeIds(
            RenderSceneSnapshot renderSnapshot,
            Rect selectionRect,
            Matrix4 view,
            Matrix4 proj,
            Rect bounds,
            float time)
        {
            if (renderSnapshot.Nodes.Length == 0)
            {
                return [];
            }

            var hits = new List<RenderNode>();

            foreach (var node in renderSnapshot.Nodes)
            {
                if (DoesProjectedNodeIntersectRect(selectionRect, node, view, proj, bounds, time))
                {
                    hits.Add(node);
                }
            }

            hits.Sort(static (a, b) =>
            {
                var labelCompare = StringComparer.Ordinal.Compare(a.Label, b.Label);
                return labelCompare != 0
                    ? labelCompare
                    : StringComparer.Ordinal.Compare(a.Id, b.Id);
            });

            var nodeIds = new string[hits.Count];
            for (var i = 0; i < hits.Count; i++)
            {
                nodeIds[i] = hits[i].Id;
            }

            return nodeIds;
        }

        private static bool DoesProjectedNodeIntersectRect(
            Rect selectionRect,
            RenderNode node,
            Matrix4 view,
            Matrix4 proj,
            Rect bounds,
            float time)
        {
            var primitiveVertices = PrimitiveMeshBuilder.GetVertices(node.Primitive);
            if (primitiveVertices.Length < 9)
            {
                return false;
            }

            var model = BuildRenderNodeModelMatrix(node, time);

            for (var i = 0; i <= primitiveVertices.Length - 9; i += 9)
            {
                if (!TryProjectLocalPoint(
                        new Vector3(primitiveVertices[i], primitiveVertices[i + 1], primitiveVertices[i + 2]),
                        model,
                        view,
                        proj,
                        bounds,
                        out var a,
                        out _) ||
                    !TryProjectLocalPoint(
                        new Vector3(primitiveVertices[i + 3], primitiveVertices[i + 4], primitiveVertices[i + 5]),
                        model,
                        view,
                        proj,
                        bounds,
                        out var b,
                        out _) ||
                    !TryProjectLocalPoint(
                        new Vector3(primitiveVertices[i + 6], primitiveVertices[i + 7], primitiveVertices[i + 8]),
                        model,
                        view,
                        proj,
                        bounds,
                        out var c,
                        out _))
                {
                    continue;
                }

                if (TriangleIntersectsRect(selectionRect, a, b, c))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryHitProjectedNodeMesh(
            Point point,
            RenderNode node,
            Matrix4 view,
            Matrix4 proj,
            Rect bounds,
            float time,
            out float hitDepth)
        {
            hitDepth = float.MaxValue;
            var primitiveVertices = PrimitiveMeshBuilder.GetVertices(node.Primitive);
            if (primitiveVertices.Length < 9)
            {
                return false;
            }

            var model = BuildRenderNodeModelMatrix(node, time);
            var hit = false;

            for (var i = 0; i <= primitiveVertices.Length - 9; i += 9)
            {
                if (!TryProjectLocalPoint(
                        new Vector3(primitiveVertices[i], primitiveVertices[i + 1], primitiveVertices[i + 2]),
                        model,
                        view,
                        proj,
                        bounds,
                        out var a,
                        out var az) ||
                    !TryProjectLocalPoint(
                        new Vector3(primitiveVertices[i + 3], primitiveVertices[i + 4], primitiveVertices[i + 5]),
                        model,
                        view,
                        proj,
                        bounds,
                        out var b,
                        out var bz) ||
                    !TryProjectLocalPoint(
                        new Vector3(primitiveVertices[i + 6], primitiveVertices[i + 7], primitiveVertices[i + 8]),
                        model,
                        view,
                        proj,
                        bounds,
                        out var c,
                        out var cz))
                {
                    continue;
                }

                if (!IsPointInTriangle(point, a, b, c))
                {
                    continue;
                }

                hit = true;
                var triangleDepth = MathF.Min(az, MathF.Min(bz, cz));
                if (triangleDepth < hitDepth)
                {
                    hitDepth = triangleDepth;
                }
            }

            return hit;
        }

        private static bool TryProjectLocalPoint(
            Vector3 localPoint,
            Matrix4 model,
            Matrix4 view,
            Matrix4 proj,
            Rect bounds,
            out Point screenPoint,
            out float depth)
        {
            var clip = new Vector4(localPoint.X, localPoint.Y, localPoint.Z, 1f) * model * view * proj;
            if (clip.W <= 0.0001f)
            {
                screenPoint = default;
                depth = float.MaxValue;
                return false;
            }

            var ndc = clip.Xyz / clip.W;
            if (ndc.Z < -1.2f || ndc.Z > 1.2f)
            {
                screenPoint = default;
                depth = float.MaxValue;
                return false;
            }

            screenPoint = new Point(
                bounds.X + ((ndc.X + 1f) * 0.5 * bounds.Width),
                bounds.Y + ((1f - (ndc.Y + 1f) * 0.5) * bounds.Height));
            depth = ndc.Z;
            return true;
        }

        private static Matrix4 BuildRenderNodeModelMatrix(RenderNode node, float time)
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

        private static bool TriangleIntersectsRect(Rect rect, Point a, Point b, Point c)
        {
            if (rect.Contains(a) || rect.Contains(b) || rect.Contains(c))
            {
                return true;
            }

            var topLeft = new Point(rect.X, rect.Y);
            var topRight = new Point(rect.Right, rect.Y);
            var bottomLeft = new Point(rect.X, rect.Bottom);
            var bottomRight = new Point(rect.Right, rect.Bottom);

            if (IsPointInTriangle(topLeft, a, b, c) ||
                IsPointInTriangle(topRight, a, b, c) ||
                IsPointInTriangle(bottomLeft, a, b, c) ||
                IsPointInTriangle(bottomRight, a, b, c))
            {
                return true;
            }

            return SegmentsIntersect(a, b, topLeft, topRight) ||
                   SegmentsIntersect(a, b, topRight, bottomRight) ||
                   SegmentsIntersect(a, b, bottomRight, bottomLeft) ||
                   SegmentsIntersect(a, b, bottomLeft, topLeft) ||
                   SegmentsIntersect(b, c, topLeft, topRight) ||
                   SegmentsIntersect(b, c, topRight, bottomRight) ||
                   SegmentsIntersect(b, c, bottomRight, bottomLeft) ||
                   SegmentsIntersect(b, c, bottomLeft, topLeft) ||
                   SegmentsIntersect(c, a, topLeft, topRight) ||
                   SegmentsIntersect(c, a, topRight, bottomRight) ||
                   SegmentsIntersect(c, a, bottomRight, bottomLeft) ||
                   SegmentsIntersect(c, a, bottomLeft, topLeft);
        }

        private static bool SegmentsIntersect(Point a1, Point a2, Point b1, Point b2)
        {
            var d1 = GetTriangleSign(a1, b1, b2);
            var d2 = GetTriangleSign(a2, b1, b2);
            var d3 = GetTriangleSign(b1, a1, a2);
            var d4 = GetTriangleSign(b2, a1, a2);

            var crosses =
                ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));

            if (crosses)
            {
                return true;
            }

            return IsPointOnSegment(a1, b1, b2) ||
                   IsPointOnSegment(a2, b1, b2) ||
                   IsPointOnSegment(b1, a1, a2) ||
                   IsPointOnSegment(b2, a1, a2);
        }

        private static bool IsPointOnSegment(Point point, Point segmentStart, Point segmentEnd)
        {
            const double epsilon = 0.001;

            if (Math.Abs(GetTriangleSign(point, segmentStart, segmentEnd)) > epsilon)
            {
                return false;
            }

            return point.X >= Math.Min(segmentStart.X, segmentEnd.X) - epsilon &&
                   point.X <= Math.Max(segmentStart.X, segmentEnd.X) + epsilon &&
                   point.Y >= Math.Min(segmentStart.Y, segmentEnd.Y) - epsilon &&
                   point.Y <= Math.Max(segmentStart.Y, segmentEnd.Y) + epsilon;
        }

        private static bool IsPointInTriangle(Point point, Point a, Point b, Point c)
        {
            var d1 = GetTriangleSign(point, a, b);
            var d2 = GetTriangleSign(point, b, c);
            var d3 = GetTriangleSign(point, c, a);
            var hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
            var hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNegative && hasPositive);
        }

        private static double GetTriangleSign(Point p1, Point p2, Point p3)
        {
            return ((p1.X - p3.X) * (p2.Y - p3.Y)) - ((p2.X - p3.X) * (p1.Y - p3.Y));
        }
    }
}
