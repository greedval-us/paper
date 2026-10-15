using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed class PlayerPaperGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var pen = new MeshPen(null, 4096); var r = rectTransform.rect;
            Color grid = new Color(.24f, .4f, .58f, .18f);
            for (float x = r.xMin; x < r.xMax; x += 32) pen.Line(new Vector2(x, r.yMin), new Vector2(x, r.yMax), .8f, grid);
            for (float y = r.yMin; y < r.yMax; y += 32) pen.Line(new Vector2(r.xMin, y), new Vector2(r.xMax, y), .8f, grid);
            pen.Line(new Vector2(r.xMin + 96, r.yMin), new Vector2(r.xMin + 96, r.yMax), 1, new Color(.7f, .3f, .24f, .14f));
            pen.CopyTo(vh, Vector2.zero, 1);
        }
    }

    public sealed class PlayerCardGraphic : MaskableGraphic
    {
        public float Radius = 14;
        public bool NotebookStyle;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            if (NotebookStyle) { NotebookPaperSurface.Draw(vh, r, color); return; }
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
        }
    }
}
