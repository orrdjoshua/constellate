using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Constellate.Renderer.OpenTK.Controls
{
    internal static class PrimitiveMeshBuilder
    {
        public static float[] GetVertices(string? primitive)
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
                "box" => BuildBoxVertices(0.58f),
                "tetrahedron" => BuildTetrahedronVertices(0.82f),
                "sphere" => BuildSphereVertices(0.7f, 18, 12),
                _ => BuildRegularPolygonPrismVertices(3, 0.74f, 0.44f, -MathF.PI / 2f)
            };
        }

        public static float[] GetEdgeVertices(string? primitive)
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
                "box" => BuildBoxEdges(0.58f),
                "tetrahedron" => BuildTetrahedronEdges(0.82f),
                "sphere" => BuildSphereEdges(0.7f, 12, 6),
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

        private static float[] BuildSphereVertices(float radius, int slices, int stacks)
        {
            slices = Math.Max(8, slices);
            stacks = Math.Max(4, stacks);

            var vertices = new List<float>(slices * stacks * 18);

            for (var stack = 0; stack < stacks; stack++)
            {
                var phi0 = MathF.PI * (stack / (float)stacks - 0.5f);
                var phi1 = MathF.PI * ((stack + 1) / (float)stacks - 0.5f);

                var y0 = MathF.Sin(phi0) * radius;
                var y1 = MathF.Sin(phi1) * radius;
                var r0 = MathF.Cos(phi0) * radius;
                var r1 = MathF.Cos(phi1) * radius;

                for (var slice = 0; slice < slices; slice++)
                {
                    var theta0 = 2f * MathF.PI * (slice / (float)slices);
                    var theta1 = 2f * MathF.PI * ((slice + 1) / (float)slices);

                    var x00 = MathF.Cos(theta0) * r0;
                    var z00 = MathF.Sin(theta0) * r0;
                    var x01 = MathF.Cos(theta1) * r0;
                    var z01 = MathF.Sin(theta1) * r0;

                    var x10 = MathF.Cos(theta0) * r1;
                    var z10 = MathF.Sin(theta0) * r1;
                    var x11 = MathF.Cos(theta1) * r1;
                    var z11 = MathF.Sin(theta1) * r1;

                    var p00 = new Vector3(x00, y0, z00);
                    var p01 = new Vector3(x01, y0, z01);
                    var p10 = new Vector3(x10, y1, z10);
                    var p11 = new Vector3(x11, y1, z11);

                    AddTriangle(vertices, p00, p10, p11);
                    AddTriangle(vertices, p00, p11, p01);
                }
            }

            return vertices.ToArray();
        }

        private static float[] BuildSphereEdges(float radius, int slices, int stacks)
        {
            slices = Math.Max(8, slices);
            stacks = Math.Max(3, stacks);

            var vertices = new List<float>(slices * stacks * 12);

            // Horizontal rings
            for (var stack = 1; stack < stacks; stack++)
            {
                var phi = MathF.PI * (stack / (float)stacks - 0.5f);
                var y = MathF.Sin(phi) * radius;
                var r = MathF.Cos(phi) * radius;

                Vector3? prev = null;
                for (var slice = 0; slice <= slices; slice++)
                {
                    var theta = 2f * MathF.PI * (slice / (float)slices);
                    var p = new Vector3(MathF.Cos(theta) * r, y, MathF.Sin(theta) * r);
                    if (prev is { } prevValue)
                    {
                        AddLine(vertices, prevValue, p);
                    }
                    prev = p;
                }
            }

            // Vertical meridians (a few)
            var meridianCount = Math.Max(4, slices / 4);
            for (var m = 0; m < meridianCount; m++)
            {
                var theta = 2f * MathF.PI * (m / (float)meridianCount);
                Vector3? prev = null;
                for (var stack = 0; stack <= stacks; stack++)
                {
                    var phi = MathF.PI * (stack / (float)stacks - 0.5f);
                    var y = MathF.Sin(phi) * radius;
                    var r = MathF.Cos(phi) * radius;
                    var p = new Vector3(MathF.Cos(theta) * r, y, MathF.Sin(theta) * r);
                    if (prev is { } prevValue)
                    {
                        AddLine(vertices, prevValue, p);
                    }
                    prev = p;
                }
            }

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
    }
}
