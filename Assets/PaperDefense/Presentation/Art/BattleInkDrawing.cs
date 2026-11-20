using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Setup-only drawing vocabulary. Completed shapes are cached by the theme.</summary>
    internal sealed class BattleInkDrawing
    {
        internal NotebookArtShape Shape { get; }
        internal BattleInkDrawing(string id) { Shape = new NotebookArtShape { Id = id }; }
        internal void Path(NotebookInkChannel c, float width, float alpha, params float[] xy)
        {
            Shape.Marks.Add(new NotebookArtMark { Primitive = NotebookArtPrimitive.Polyline,
                Channel = c, Width = width, Alpha = alpha, Points = Points(xy) });
        }
        internal void Fill(NotebookInkChannel c, float alpha, params float[] xy)
        {
            Shape.Marks.Add(new NotebookArtMark { Primitive = NotebookArtPrimitive.Polygon,
                Channel = c, Alpha = alpha, Points = Points(xy) });
        }
        internal void Ellipse(NotebookInkChannel c, float x, float y, float radius, float aspect,
            float width, float alpha = 1, int segments = 16)
        {
            Shape.Marks.Add(new NotebookArtMark { Primitive = width > 0 ? NotebookArtPrimitive.Ring : NotebookArtPrimitive.Circle,
                Channel = c, Center = new Vector2(x, y), Radius = radius, Aspect = aspect,
                Width = width, Alpha = alpha, Segments = segments });
        }
        internal void Line(NotebookInkChannel c, float ax, float ay, float bx, float by, float width = .025f, float alpha = 1)
            => Path(c, width, alpha, ax, ay, bx, by);
        internal void Box(NotebookInkChannel c, float x, float y, float w, float h, float width = .025f, float alpha = 1)
            => Path(c, width, alpha, x, y, x + w, y, x + w, y + h, x, y + h, x, y);
        static Vector2[] Points(float[] xy)
        {
            var result = new Vector2[xy.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return result;
        }
    }
}
