using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PaperDefense.Presentation
{
    /// <summary>A reusable pen which writes every mark into a single vertex-colored mesh.</summary>
    internal sealed class MeshPen
    {
        readonly List<Vector3> vertices;
        readonly List<Color32> colors;
        readonly List<int> indices;
        readonly Mesh mesh;

        public MeshPen(Mesh target, int capacity = 8192)
        {
            mesh = target;
            vertices = new List<Vector3>(capacity);
            colors = new List<Color32>(capacity);
            indices = new List<int>(capacity * 3 / 2);
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();
        }

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();
        }

        int Vertex(Vector2 p, Color color)
        {
            int index = vertices.Count;
            vertices.Add(new Vector3(p.x, p.y, 0));
            colors.Add(color);
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
            Vector2 side = new Vector2(-delta.y, delta.x) * (width * .5f / length);
            Quad(from - side, to - side, to + side, from + side, color);
        }

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
            mesh.Clear(false);
            if (vertices.Count == 0) return;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0, false);
            mesh.RecalculateBounds();
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
        public static readonly Color Paper = new Color(.974f, .957f, .896f);
        public static readonly Color Navy = new Color(.17f, .22f, .29f);
        public static readonly Color Blue = new Color(.23f, .42f, .68f);
        public static readonly Color Pencil = new Color(.68f, .49f, .23f);
        public static readonly Color Marker = new Color(.55f, .33f, .64f);
        public static readonly Color Red = new Color(.84f, .25f, .25f);
        public static readonly Color Green = new Color(.32f, .53f, .43f);
    }
}
