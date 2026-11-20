using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Cloth-bound campaign journals. Each cover has a different stamped silhouette.</summary>
    public sealed class CampaignNotebookGraphic : NotebookAtlasGraphic
    {
        public int NotebookIndex = 1;
        readonly MeshPen pen = new MeshPen(null, 384);

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            pen.Clear();
            Rect bounds = rectTransform.rect;
            float scale = Mathf.Min(bounds.width / 26, bounds.height / 32);
            if (scale <= 0) return;

            Color cloth = NotebookIndex == 1 ? new Color32(66, 97, 77, 255)
                : NotebookIndex == 2 ? new Color32(59, 85, 103, 255) : new Color32(105, 69, 76, 255);
            Color foil = NotebookIndex == 1 ? new Color32(247, 208, 128, 255)
                : NotebookIndex == 2 ? new Color32(210, 231, 215, 255) : new Color32(248, 204, 173, 255);
            Color shade = Color.Lerp(cloth, FieldMenuSkin.Background, .56f);

            // Page edges are offset below the cover, with the cloth hinge visible on the left.
            pen.Rect(3, 2, 21, 27, shade);
            pen.Rect(3, 3, 20, 26, FieldMenuSkin.PreviewPaper);
            pen.Rect(2, 5.2f, 21, 25, cloth);
            pen.Rect(2, 5.2f, 3.7f, 25, shade);
            pen.Line(new Vector2(6.4f, 6), new Vector2(6.4f, 29.4f), .8f, MeshPen.Alpha(foil, .68f));
            pen.Rect(2, 9, 3.7f, 1.5f, foil);
            pen.Rect(2, 25.5f, 3.7f, 1.5f, foil);
            pen.Rect(9, 7.8f, 11.5f, .75f, MeshPen.Alpha(foil, .62f));

            switch (NotebookIndex)
            {
                case 1:
                    // The keep has a cut-out gate and a swallowtail pennant.
                    pen.Rect(9, 12, 11, 7.6f, foil);
                    for (int i = 0; i < 3; i++) pen.Rect(9 + i * 4.3f, 19, 2.4f, 3.2f, foil);
                    pen.Rect(13, 12, 3, 3.3f, cloth);
                    pen.Disc(new Vector2(14.5f, 15.2f), 1.5f, cloth, 10);
                    pen.Line(new Vector2(14.6f, 20), new Vector2(14.6f, 27.3f), 1, foil);
                    pen.Triangle(new Vector2(15, 27.2f), new Vector2(21, 25.5f), new Vector2(15, 24.3f), foil);
                    break;
                case 2:
                    var pivot = new Vector2(14.8f, 18.2f);
                    pen.Ring(pivot, 5.7f, .9f, foil, 20);
                    pen.Triangle(new Vector2(18.8f, 26), pivot + new Vector2(-2.2f, 1.3f),
                        pivot + new Vector2(2.2f, -1.3f), foil);
                    pen.Triangle(new Vector2(10.8f, 10.4f), pivot + new Vector2(-2.2f, 1.3f),
                        pivot + new Vector2(2.2f, -1.3f), Color.Lerp(foil, cloth, .38f));
                    pen.Disc(pivot, 1.15f, shade, 10);
                    break;
                default:
                    // An engraved fountain-pen nib with a breather hole and slit.
                    pen.Quad(new Vector2(8.8f, 10.6f), new Vector2(17, 13.8f),
                        new Vector2(20.4f, 21.4f), new Vector2(14.6f, 23.8f), foil);
                    pen.Quad(new Vector2(15, 25), new Vector2(20.9f, 22.5f),
                        new Vector2(21.7f, 24.6f), new Vector2(15.9f, 27.1f), foil);
                    pen.Line(new Vector2(8.8f, 10.6f), new Vector2(15, 18.2f), 1, shade);
                    pen.Disc(new Vector2(15.2f, 18.5f), 1.35f, shade, 10);
                    break;
            }
            pen.CopyTo(target, bounds.center - new Vector2(13, 16) * scale, scale);
        }
    }
}
