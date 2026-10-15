using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>A reusable pen which writes every mark into a single vertex-colored mesh.</summary>
    internal sealed class MeshPen
    {
        readonly List<Vector3> vertices;
        readonly List<Color32> colors;
        readonly List<Vector3> strokeData;
        readonly List<int> indices;
        readonly Mesh mesh;
        // Opt-in for world rendering. UI and geometry inspection keep the exact authored widths.
        internal float WorldUnitsPerPixel { get; set; }
        internal float MinimumStrokePixels { get; set; } = 1.25f;
        // UI previews use a solid line floor, without the world shader's antialiasing fringe.
        internal float MinimumLineWidth { get; set; }

        // VertexHelper rejects 65,000 vertices. Keep each slice triangle-aligned and strictly below it.
        internal const int MaximumUiBatchVertices = 64998;
        internal int UiBatchCount => indices.Count == 0 ? 0 : (indices.Count - 1) / MaximumUiBatchVertices + 1;

        public MeshPen(Mesh target, int capacity = 8192)
        {
            mesh = target;
            vertices = new List<Vector3>(capacity);
            colors = new List<Color32>(capacity);
            strokeData = new List<Vector3>(capacity);
            indices = new List<int>(capacity * 3 / 2);
            if (mesh != null) { mesh.indexFormat = IndexFormat.UInt32; mesh.MarkDynamic(); }
        }

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            strokeData.Clear();
            indices.Clear();
        }

        internal int VertexCount => vertices.Count;
        internal void ScaleFrom(int firstVertex, Vector2 pivot, float scale)
        {
            for (int i = firstVertex; i < vertices.Count; i++)
            {
                vertices[i] = (Vector3)pivot + (vertices[i] - (Vector3)pivot) * scale;
                var data = strokeData[i]; data.x *= Mathf.Abs(scale); data.y *= Mathf.Abs(scale); strokeData[i] = data;
            }
        }

        int Vertex(Vector2 p, Color color)
        {
            int index = vertices.Count;
            vertices.Add(new Vector3(p.x, p.y, 0));
            colors.Add(color);
            strokeData.Add(Vector3.zero);
            return index;
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vertices.Count;
            Vertex(a, color); Vertex(b, color); Vertex(c, color);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vertices.Count;
            Vertex(a, color); Vertex(b, color); Vertex(c, color); Vertex(d, color);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }

        public void Rect(float x, float y, float width, float height, Color color)
        {
            Quad(new Vector2(x, y), new Vector2(x + width, y),
                 new Vector2(x + width, y + height), new Vector2(x, y + height), color);
        }

        public void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < .0001f) return;
            float halfWidth = Mathf.Max(Mathf.Max(width, MinimumLineWidth), MinimumStrokePixels * WorldUnitsPerPixel) * .5f;
            float extent = halfWidth + WorldUnitsPerPixel;
            Vector2 side = new Vector2(-delta.y, delta.x) * (extent / length);
            int first = vertices.Count;
            Quad(from - side, to - side, to + side, from + side, color);
            if (WorldUnitsPerPixel > 0)
            {
                strokeData[first] = strokeData[first + 1] = new Vector3(halfWidth, -extent, 1);
                strokeData[first + 2] = strokeData[first + 3] = new Vector3(halfWidth, extent, 1);
            }
        }

        /// <summary>Append cached author lines with only a rigid transform; no wobble or silhouette replacement.</summary>
        public void AppendRevealed(AuthoredPartGeometry geometry, Vector2 pivot, float radians, Color color, float progress)
        {
            if (geometry == null || progress <= 0) return;
            if (progress >= 1) { Append(geometry, pivot, radians, color); return; }
            float remaining = geometry.Vertices.Count / 4f * Mathf.Clamp01(progress);
            float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            for (int i = 0; i < geometry.Vertices.Count && remaining > 0; i += 4, remaining--)
            {
                Vector2 a = (geometry.Vertices[i] + geometry.Vertices[i + 3]) * .5f;
                Vector2 b = (geometry.Vertices[i + 1] + geometry.Vertices[i + 2]) * .5f;
                float width = Vector2.Distance(geometry.Vertices[i], geometry.Vertices[i + 3]);
                Line(pivot + Rotate(a, cosine, sine), pivot + Rotate(Vector2.Lerp(a, b, Mathf.Min(1, remaining)), cosine, sine), width, color);
            }
        }

        public void Append(AuthoredPartGeometry geometry, Vector2 pivot, float radians, Color color)
        {
            if (geometry == null) return;
            int start = vertices.Count; float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            if (WorldUnitsPerPixel > 0 || MinimumLineWidth > 0)
            {
                // Cached authored geometry consists of one quad per stroke segment (or point).
                // Keep endpoints/pivots unchanged while making its ink readable on screen.
                for (int i = 0; i < geometry.Vertices.Count; i += 4)
                {
                    Vector2 a = (geometry.Vertices[i] + geometry.Vertices[i + 3]) * .5f;
                    Vector2 b = (geometry.Vertices[i + 1] + geometry.Vertices[i + 2]) * .5f;
                    float width = Vector2.Distance(geometry.Vertices[i], geometry.Vertices[i + 3]);
                    Line(pivot + Rotate(a, cosine, sine), pivot + Rotate(b, cosine, sine), width, color);
                }
                return;
            }
            for (int i = 0; i < geometry.Vertices.Count; i++)
            {
                var point = geometry.Vertices[i];
                Vertex(pivot + new Vector2(cosine * point.x - sine * point.y, sine * point.x + cosine * point.y), color);
            }
            for (int i = 0; i < geometry.Indices.Count; i++) indices.Add(start + geometry.Indices[i]);
        }

        static Vector2 Rotate(Vector2 point, float cosine, float sine)
            => new Vector2(cosine * point.x - sine * point.y, sine * point.x + cosine * point.y);

        public void Stroke(Vector2 from, Vector2 to, float width, Color color, int seed = 0, float wobble = .018f)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < .001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x) / length;
            int parts = Mathf.Clamp(Mathf.CeilToInt(length / .55f), 2, 8);
            Vector2 previous = from;
            for (int i = 1; i <= parts; i++)
            {
                float t = i / (float)parts;
                float jitter = i == parts ? 0 : (Hash(seed * 113 + i * 73) - .5f) * wobble * 2;
                Vector2 next = Vector2.Lerp(from, to, t) + normal * jitter;
                Line(previous, next, width * (.91f + Hash(seed + i * 17) * .18f), color);
                previous = next;
            }
        }

        public void Box(Vector2 center, Vector2 size, Color fill, Color edge, float width, int seed = 0)
        {
            Vector2 a = center - size * .5f;
            Vector2 b = new Vector2(a.x + size.x, a.y);
            Vector2 c = a + size;
            Vector2 d = new Vector2(a.x, a.y + size.y);
            Rect(a.x, a.y, size.x, size.y, fill);
            Stroke(a, b, width, edge, seed);
            Stroke(b, c, width, edge, seed + 1);
            Stroke(c, d, width, edge, seed + 2);
            Stroke(d, a, width, edge, seed + 3);
        }

        public void Disc(Vector2 center, float radius, Color fill, int segments = 16, float aspect = 1)
        {
            Vector2 previous = center + new Vector2(radius, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * aspect);
                Triangle(center, previous, next, fill);
                previous = next;
            }
        }

        public void Ring(Vector2 center, float radius, float width, Color color, int segments = 24,
                         float aspect = 1, int seed = 0, bool dashed = false)
        {
            Vector2 previous = center + new Vector2(radius, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float r = radius + (Hash(seed + i * 13) - .5f) * .025f;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * aspect);
                if (!dashed || i % 3 != 0) Line(previous, next, width, color);
                previous = next;
            }
        }

        public void Star(Vector2 center, float radius, Color color, float width, int rays = 7, float rotation = 0)
        {
            for (int i = 0; i < rays; i++)
            {
                float angle = rotation + i * Mathf.PI * 2 / rays;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Line(center + direction * radius * .35f, center + direction * radius, width, color);
            }
        }

        public void Upload()
        {
            if (mesh == null) return;
            mesh.Clear(false);
            if (vertices.Count == 0) return;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, strokeData);
            mesh.SetTriangles(indices, 0, false);
            mesh.RecalculateBounds();
        }

        /// <summary>The UI uses the same composition, primitives and ordering as the world batch.</summary>
        public void CopyTo(VertexHelper target, Vector2 origin, float scale)
        {
            int start = target.currentVertCount;
            for (int i = 0; i < vertices.Count; i++)
                target.AddVert(origin + new Vector2(vertices[i].x, vertices[i].y) * scale, colors[i], Vector2.zero);
            for (int i = 0; i < indices.Count; i += 3)
                target.AddTriangle(start + indices[i], start + indices[i + 1], start + indices[i + 2]);
        }

        /// <summary>
        /// Copy one consecutive triangle slice without changing winding or draw order. Expanding corners
        /// also handles triangles sharing vertices across a boundary, without per-frame remap buffers.
        /// </summary>
        internal void CopyUiBatchTo(VertexHelper target, Vector2 origin, float scale, int batchIndex)
        {
            int first = batchIndex * MaximumUiBatchVertices;
            int end = Math.Min(first + MaximumUiBatchVertices, indices.Count);
            for (int i = first; i < end; i += 3)
            {
                int start = target.currentVertCount;
                for (int corner = 0; corner < 3; corner++)
                {
                    int source = indices[i + corner];
                    target.AddVert(origin + new Vector2(vertices[source].x, vertices[source].y) * scale,
                        colors[source], Vector2.zero);
                }
                target.AddTriangle(start, start + 1, start + 2);
            }
        }

        public static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
        public static float Hash(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
                return (x & 0xffff) / 65535f;
            }
        }
    }

    internal static class NotebookPalette
    {
        public static readonly Color Paper = new Color32(249, 244, 230, 255);
        public static readonly Color Navy = new Color(.17f, .22f, .29f);
        public static readonly Color Blue = new Color(.23f, .42f, .68f);
        public static readonly Color Pencil = new Color(.68f, .49f, .23f);
        public static readonly Color Marker = new Color(.55f, .33f, .64f);
        public static readonly Color Red = new Color(.84f, .25f, .25f);
        public static readonly Color Green = new Color(.32f, .53f, .43f);
    }
}
