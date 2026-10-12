using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PaperDefense.Core;
using UnityEngine;

[assembly: InternalsVisibleTo("PaperDefense.Integration.Tests")]

namespace PaperDefense.Presentation
{
    /// <summary>Derived local stroke geometry shared by every copy of an immutable album drawing.</summary>
    public static class AuthoredGeometryCache
    {
        const int MaximumEntries = 128;
        static readonly Dictionary<GeometryKey, AuthoredDrawingGeometry> entries = new Dictionary<GeometryKey, AuthoredDrawingGeometry>();
        static readonly Queue<GeometryKey> order = new Queue<GeometryKey>();
        public static int Count => entries.Count;
        public static int BuildCount { get; private set; }

        public static AuthoredDrawingGeometry For(DrawingSnapshot drawing)
        {
            if (drawing == null) return null;
            var key = new GeometryKey(drawing);
            if (entries.TryGetValue(key, out var geometry)) return geometry;
            var parts = new AuthoredPartGeometry[drawing.Parts.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = Prepare(drawing.Parts[i]);
            geometry = new AuthoredDrawingGeometry(parts); BuildCount++;
            if (entries.Count >= MaximumEntries) entries.Remove(order.Dequeue());
            entries.Add(key, geometry); order.Enqueue(key); return geometry;
        }

        public static void Clear() { entries.Clear(); order.Clear(); BuildCount = 0; }

        static AuthoredPartGeometry Prepare(DrawingPart part)
        {
            var vertices = new List<Vector2>(); var indices = new List<int>();
            foreach (var stroke in part.Strokes)
            {
                if (stroke.Points.Count == 1)
                {
                    var point = stroke.Points[0]; float half = DrawingStroke.Width * .5f;
                    AddQuad(vertices, indices, new Vector2(point.X - half, point.Y - half), new Vector2(point.X + half, point.Y - half),
                        new Vector2(point.X + half, point.Y + half), new Vector2(point.X - half, point.Y + half));
                }
                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    var first = stroke.Points[i - 1]; var second = stroke.Points[i];
                    Vector2 from = new Vector2(first.X, first.Y), to = new Vector2(second.X, second.Y);
                    Vector2 delta = to - from; float length = delta.magnitude;
                    if (length < .000001f) continue;
                    Vector2 side = new Vector2(-delta.y, delta.x) * (DrawingStroke.Width * .5f / length);
                    AddQuad(vertices, indices, from - side, to - side, to + side, from + side);
                }
            }
            return new AuthoredPartGeometry(part.Key, vertices.ToArray(), indices.ToArray());
        }

        static void AddQuad(List<Vector2> vertices, List<int> indices, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }

        readonly struct GeometryKey : IEquatable<GeometryKey>
        {
            readonly string hash; readonly int format, binding;
            public GeometryKey(DrawingSnapshot drawing) { hash = drawing.GeometryHash; format = drawing.FormatVersion; binding = drawing.BindingVersion; }
            public bool Equals(GeometryKey other) => format == other.format && binding == other.binding && hash == other.hash;
            public override bool Equals(object value) => value is GeometryKey other && Equals(other);
            public override int GetHashCode() { unchecked { return ((hash?.GetHashCode() ?? 0) * 397 ^ format) * 397 ^ binding; } }
        }
    }

    public sealed class AuthoredDrawingGeometry
    {
        public IReadOnlyList<AuthoredPartGeometry> Parts { get; }
        internal AuthoredDrawingGeometry(AuthoredPartGeometry[] parts) { Parts = Array.AsReadOnly(parts); }
        public AuthoredPartGeometry GetPart(DrawingPartKey key)
        { for (int i = 0; i < Parts.Count; i++) if (Parts[i].Key == key) return Parts[i]; return null; }
    }

    public sealed class AuthoredPartGeometry
    {
        public DrawingPartKey Key { get; }
        public IReadOnlyList<Vector2> Vertices { get; }
        public IReadOnlyList<int> Indices { get; }
        internal AuthoredPartGeometry(DrawingPartKey key, Vector2[] vertices, int[] indices)
        { Key = key; Vertices = Array.AsReadOnly(vertices); Indices = Array.AsReadOnly(indices); }
    }
}
