using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed class PlayerPaperGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var pen = new MeshPen(null, 4096); var r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            Color grid = PlayerUi.Grid;
            for (float x = r.xMin; x < r.xMax; x += 28) pen.Line(new Vector2(x, r.yMin), new Vector2(x, r.yMax), .7f, grid);
            for (float y = r.yMin; y < r.yMax; y += 28) pen.Line(new Vector2(r.xMin, y), new Vector2(r.xMax, y), .7f, grid);
            pen.CopyTo(vh, Vector2.zero, 1);
        }
    }

    public sealed class PlayerCardGraphic : MaskableGraphic
    {
        public float Radius = 4;
        public bool NotebookStyle;
        public bool Selected;
        public bool Focused;
        public bool Control;
        public bool Disabled;
        public bool Hovered;
        public bool Pressed;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            if (NotebookStyle && color.a >= .9f && r.width >= 8 && r.height >= 8)
            { NotebookPaperSurface.Draw(vh, r, color, Control, Selected, Focused, Disabled, Hovered, Pressed); return; }
            float radius = Mathf.Min(Radius, r.width * .5f, r.height * .5f);
            vh.AddVert(r.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
                    corner < 2 ? r.yMax - radius : r.yMin + radius);
                for (int i = 0; i <= 8; i++)
                {
                    float angle = (corner * 90 + i * 90f / 8) * Mathf.Deg2Rad;
                    vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                }
            }
            for (int i = 1; i <= 36; i++) vh.AddTriangle(0, i, i == 36 ? 1 : i + 1);
            if (Selected)
            {
                var pen = new MeshPen(null, 64);
                var a = new Vector2(r.xMin + 3, r.yMin + 3); var b = new Vector2(r.xMax - 3, r.yMax - 3);
                pen.Line(a, new Vector2(b.x, a.y), 2, PlayerUi.Ink);
                pen.Line(new Vector2(b.x, a.y), b, 2, PlayerUi.Ink);
                pen.Line(b, new Vector2(a.x, b.y), 2, PlayerUi.Ink);
                pen.Line(new Vector2(a.x, b.y), a, 2, PlayerUi.Ink);
                pen.CopyTo(vh, Vector2.zero, 1);
            }
            if (Focused)
            {
                var pen = new MeshPen(null, 64);
                Color focusInk = color == PlayerUi.Blue ? PlayerUi.Card : PlayerUi.Blue;
                float inset = Selected ? 7 : 3;
                var a = new Vector2(r.xMin + inset, r.yMin + inset);
                var b = new Vector2(r.xMax - inset, r.yMax - inset);
                foreach (var corner in new[] { a, new Vector2(a.x, b.y), b, new Vector2(b.x, a.y) })
                {
                    pen.Line(corner, corner + new Vector2(corner.x == a.x ? 10 : -10, 0), 2, focusInk);
                    pen.Line(corner, corner + new Vector2(0, corner.y == a.y ? 10 : -10), 2, focusInk);
                }
                pen.CopyTo(vh, Vector2.zero, 1);
            }
        }
    }
}
