using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Lets the edges of the cover engraving show the map underneath.</summary>
    public sealed class HomeCoverGraphic : RawImage
    {
        static readonly float[] Stops = { 0, .10f, .90f, 1 };

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (texture == null) return;
            var rect = rectTransform.rect;
            for (int row = 0; row < Stops.Length; row++)
            {
                for (int column = 0; column < Stops.Length; column++)
                {
                    var uv = new Vector2(Stops[column], Stops[row]);
                    Color tint = color;
                    if (column == 0 || row == 0 || column == Stops.Length - 1 || row == Stops.Length - 1)
                        tint.a = 0;
                    mesh.AddVert(Rect.NormalizedToPoint(rect, uv), tint, uv);
                    if (column == 0 || row == 0) continue;
                    int end = mesh.currentVertCount - 1;
                    mesh.AddTriangle(end - Stops.Length - 1, end - 1, end);
                    mesh.AddTriangle(end - Stops.Length - 1, end, end - Stops.Length);
                }
            }
        }
    }
}
