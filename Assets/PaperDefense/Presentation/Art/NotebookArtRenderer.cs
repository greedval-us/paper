using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Prepared triangles share the same resource geometry in a world mesh and a UI canvas.</summary>
    public static class NotebookArtRenderer
    {
        struct Vertex { public Vector2 Point; public NotebookInkChannel Channel; public float Alpha; }
        sealed class Geometry { public readonly List<Vertex> Vertices = new List<Vertex>(); }
        static readonly ConditionalWeakTable<NotebookArtShape, Geometry> cache = new ConditionalWeakTable<NotebookArtShape, Geometry>();
        public static int GeometryBuildCount { get; private set; }

        internal static bool Draw(MeshPen pen, NotebookArtShape shape, Vector2 pivot, float scale = 1,
            float radians = 0, Color? strokeTint = null, float opacity = 1, Vector2? localScale = null, NotebookVisualThemeAsset theme = null)
        {
            if (pen == null || shape == null) return false;
            var geometry = cache.GetValue(shape, Prepare);
            float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            theme = theme ?? NotebookVisualThemeAsset.Default;
            Vector2 stretch = localScale ?? Vector2.one;
            for (int i = 0; i < geometry.Vertices.Count; i += 3)
            {
                var a = geometry.Vertices[i]; var b = geometry.Vertices[i + 1]; var c = geometry.Vertices[i + 2];
                pen.Triangle(Transform(Vector2.Scale(a.Point, stretch), pivot, scale, cosine, sine), Transform(Vector2.Scale(b.Point, stretch), pivot, scale, cosine, sine),
                    Transform(Vector2.Scale(c.Point, stretch), pivot, scale, cosine, sine), Tone(theme, a, strokeTint, opacity));
            }
            return true;
        }
        public static bool DrawUI(VertexHelper target, NotebookArtShape shape, Vector2 center, float scale,
            float radians = 0, Color? strokeTint = null, float opacity = 1, Vector2? localScale = null, NotebookVisualThemeAsset theme = null)
        {
            if (target == null || shape == null) return false;
            var geometry = cache.GetValue(shape, Prepare);
            float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            theme = theme ?? NotebookVisualThemeAsset.Default;
            Vector2 stretch = localScale ?? Vector2.one;
            int offset = target.currentVertCount;
            for (int i = 0; i < geometry.Vertices.Count; i++)
            {
                var v = geometry.Vertices[i];
                target.AddVert(Transform(Vector2.Scale(v.Point, stretch), center, scale, cosine, sine), Tone(theme, v, strokeTint, opacity), Vector2.zero);
            }
            for (int i = 0; i < geometry.Vertices.Count; i += 3) target.AddTriangle(offset + i, offset + i + 1, offset + i + 2);
            return true;
        }
        static Vector2 Transform(Vector2 point, Vector2 center, float scale, float cosine, float sine)
            => center + new Vector2(cosine * point.x - sine * point.y, sine * point.x + cosine * point.y) * scale;
        static Color Tone(NotebookVisualThemeAsset theme, Vertex vertex, Color? tint, float opacity)
        {
            var color = tint.HasValue && (vertex.Channel == NotebookInkChannel.Navy || vertex.Channel == NotebookInkChannel.Pencil)
                ? tint.Value : theme == null ? UnityEngine.Color.white : theme.Color(vertex.Channel);
            color.a *= vertex.Alpha * opacity;
            return color;
        }
        static Geometry Prepare(NotebookArtShape shape)
        {
            GeometryBuildCount++;
            var result = new Geometry();
            foreach (var mark in shape.Marks)
            {
                if (mark.Primitive == NotebookArtPrimitive.Polyline)
                {
                    for (int i = 1; i < mark.Points.Length; i++) Line(result, mark.Points[i - 1], mark.Points[i], mark.Width, mark);
                }
                else if (mark.Primitive == NotebookArtPrimitive.Polygon)
                {
                    for (int i = 2; i < mark.Points.Length; i++) Triangle(result, mark.Points[0], mark.Points[i - 1], mark.Points[i], mark);
                }
                else
                {
                    Vector2 previous = mark.Center + new Vector2(mark.Radius, 0);
                    for (int i = 1; i <= mark.Segments; i++)
                    {
                        float angle = i * Mathf.PI * 2 / mark.Segments;
                        Vector2 next = mark.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * mark.Aspect) * mark.Radius;
                        if (mark.Primitive == NotebookArtPrimitive.Circle) Triangle(result, mark.Center, previous, next, mark);
                        else Line(result, previous, next, mark.Width, mark);
                        previous = next;
                    }
                }
            }
            return result;
        }
        static void Line(Geometry geometry, Vector2 a, Vector2 b, float width, NotebookArtMark mark)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < .0000001f) return;
            var side = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            Triangle(geometry, a - side, b - side, b + side, mark); Triangle(geometry, a - side, b + side, a + side, mark);
        }
        static void Triangle(Geometry geometry, Vector2 a, Vector2 b, Vector2 c, NotebookArtMark mark)
        {
            geometry.Vertices.Add(new Vertex { Point = a, Channel = mark.Channel, Alpha = mark.Alpha });
            geometry.Vertices.Add(new Vertex { Point = b, Channel = mark.Channel, Alpha = mark.Alpha });
            geometry.Vertices.Add(new Vertex { Point = c, Channel = mark.Channel, Alpha = mark.Alpha });
        }
    }
}
