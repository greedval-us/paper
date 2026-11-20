using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>The field report bears either a hanging service medal or a broken wax seal.</summary>
    public sealed class MissionSealGraphic : NotebookAtlasGraphic
    {
        public bool Victory;
        readonly MeshPen pen = new MeshPen(null, 1024);
        static readonly Color Wax = new Color32(134, 74, 60, 255);
        static readonly Color WaxRim = new Color32(227, 160, 130, 255);

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            pen.Clear();
            Rect bounds = rectTransform.rect;
            float scale = Mathf.Min(bounds.width, bounds.height) / 80;
            if (scale <= 0) return;
            if (Victory) DrawMedal();
            else DrawBrokenSeal();
            pen.CopyTo(target, bounds.center, scale);
        }

        void DrawMedal()
        {
            Color brass = FieldMenuSkin.Accent, ivory = FieldMenuSkin.Ink;
            Color ribbon = new Color32(88, 117, 92, 255);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 outer = new Vector2(side * 23, -34);
                Vector2 notch = new Vector2(side * 13, -29);
                Vector2 inner = new Vector2(side * 7, -37);
                Vector2 shoulder = new Vector2(side * 17, -6);
                pen.Quad(new Vector2(side * 5, -5), shoulder, outer, notch, ribbon);
                pen.Triangle(new Vector2(side * 5, -5), notch, inner, ribbon);
                pen.Line(shoulder, outer, 1.3f, brass);
                pen.Line(new Vector2(side * 10, -10), inner, 1.2f, MeshPen.Alpha(ivory, .68f));
            }
            Vector2 center = new Vector2(0, 6);
            pen.Disc(center, 29, brass, 12);
            pen.Disc(center, 26.7f, new Color32(99, 83, 52, 255), 12);
            pen.Disc(center, 23.6f, FieldMenuSkin.Raised, 36);
            pen.Ring(center, 23.6f, 1, ivory, 36);
            for (int i = 0; i < 12; i++)
            {
                float angle = (i + .5f) * Mathf.PI / 6;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                pen.Disc(center + direction * 25.4f, .8f, brass, 6);
            }
            // Fort and pennant in relief, with the gate punched back to the green enamel.
            pen.Rect(-14, -4, 28, 12, ivory);
            for (int i = 0; i < 4; i++) pen.Rect(-14 + i * 8, 7, 4, 4.8f, ivory);
            pen.Rect(-3.4f, -4, 6.8f, 5.2f, FieldMenuSkin.Raised);
            pen.Disc(new Vector2(0, 1.4f), 3.4f, FieldMenuSkin.Raised, 12);
            pen.Line(new Vector2(-.8f, 8), new Vector2(-.8f, 23), 1.8f, brass);
            pen.Quad(new Vector2(0, 22.4f), new Vector2(12, 21), new Vector2(7.6f, 18.3f),
                new Vector2(0, 17), brass);
            pen.Line(new Vector2(-10, -8), new Vector2(10, -8), 1.25f, brass);
        }

        void DrawBrokenSeal()
        {
            // Each half has its own silhouette and gap. The break remains visible in grayscale.
            var center = new Vector2(0, 3);
            pen.Disc(center, 28, Wax, 10);
            pen.Ring(center, 24, 1.4f, WaxRim, 28);
            pen.Rect(-14, -7, 27, 14, WaxRim);
            pen.Rect(-14, 6, 5, 8, WaxRim);
            pen.Rect(-5, 6, 4.5f, 5, WaxRim);
            pen.Quad(new Vector2(6, 5), new Vector2(13, 4), new Vector2(15, 11),
                new Vector2(9, 13), WaxRim);
            pen.Rect(-9, -7, 4, 6, Wax);
            Color gap = FieldMenuSkin.Panel;
            var upper = new Vector2(5.6f, 31);
            var joint = new Vector2(-2.7f, 5.2f);
            var ledge = new Vector2(4.2f, 6.7f);
            var lower = new Vector2(-7.4f, -25.4f);
            pen.Line(upper, joint, 4.8f, gap);
            pen.Line(joint - Vector2.right, ledge + Vector2.right, 4.8f, gap);
            pen.Line(ledge, lower, 4.8f, gap);
            pen.Triangle(new Vector2(-11, -26), new Vector2(-17, -32), new Vector2(-20, -23), WaxRim);
            pen.Triangle(new Vector2(4, -30), new Vector2(11, -28), new Vector2(8, -35), Wax);
        }
    }
}
