using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Enamel slider on the field notebook's display and accessibility controls.</summary>
    public sealed class PlayerSettingsToggleGraphic : MaskableGraphic
    {
        public bool Value;
        static readonly MeshPen pen = new MeshPen(null, 768);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            pen.Clear();
            float radius = Mathf.Min(r.height, r.width) * .46f;
            float travel = Mathf.Max(0, r.width * .5f - radius - 1);
            Vector2 left = Vector2.left * travel, right = Vector2.right * travel;
            void Track(float capRadius, Color ink)
            {
                pen.Line(left, right, capRadius * 2, ink);
                pen.Disc(left, capRadius, ink, 16);
                pen.Disc(right, capRadius, ink, 16);
            }
            Track(radius, Value ? FieldMenuSkin.Accent : FieldMenuSkin.Border);
            Track(radius * .87f, FieldMenuSkin.Background);
            Vector2 knob = Value ? right : left, empty = -knob;
            pen.Line(empty + Vector2.left * 3, empty + Vector2.right * 3, 1.6f, FieldMenuSkin.Border);
            pen.Disc(knob, radius * .70f, Value ? FieldMenuSkin.Accent : FieldMenuSkin.Muted, 20);
            if (Value)
            {
                pen.Line(knob + new Vector2(-4, 0), knob + new Vector2(-1.2f, -2.8f), 1.8f, FieldMenuSkin.AccentInk);
                pen.Line(knob + new Vector2(-1.2f, -2.8f), knob + new Vector2(4.3f, 3), 1.8f, FieldMenuSkin.AccentInk);
            }
            else pen.Line(knob + Vector2.left * 3.5f, knob + Vector2.right * 3.5f, 1.8f, FieldMenuSkin.Background);
            pen.CopyTo(vh, r.center, 1);
        }
    }

    public sealed class PlayerSettingsDisplayGraphic : MaskableGraphic
    {
        static readonly MeshPen pen = new MeshPen(null, 128);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); pen.Clear();
            pen.Rect(-.78f, -.41f, 1.56f, 1.08f, FieldMenuSkin.Ink);
            pen.Rect(-.65f, -.20f, 1.30f, .75f, FieldMenuSkin.Background);
            pen.Line(new Vector2(-.53f, .10f), new Vector2(-.21f, .42f), .08f, FieldMenuSkin.Muted);
            pen.Line(new Vector2(-.31f, .08f), new Vector2(-.09f, .30f), .045f, FieldMenuSkin.Muted);
            pen.Disc(new Vector2(.55f, -.31f), .06f, FieldMenuSkin.AccentInk, 8);
            pen.Rect(-.14f, -.62f, .28f, .25f, FieldMenuSkin.Muted);
            pen.Rect(-.45f, -.68f, .90f, .10f, FieldMenuSkin.Ink);
            pen.CopyTo(vh, rectTransform.rect.center, Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .48f);
        }
    }

}
