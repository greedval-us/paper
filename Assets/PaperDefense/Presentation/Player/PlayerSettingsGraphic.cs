using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Ballpoint switch used by the settings sketch, without a baked caption.</summary>
    public sealed class PlayerSettingsToggleGraphic : MaskableGraphic
    {
        public bool Value;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            var pen = new MeshPen(null, 1024);
            float radius = r.height * .43f, travel = Mathf.Max(0, r.width * .5f - radius - 2);
            Color fill = Value ? new Color32(35, 139, 77, 255) : new Color32(183, 193, 207, 255);
            Vector2 left = new Vector2(-travel, 0), right = new Vector2(travel, 0);
            pen.Line(left, right, radius * 2, fill);
            pen.Disc(left, radius, fill, 32); pen.Disc(right, radius, fill, 32);
            for (float x = -travel + 3; x < travel; x += 5)
                pen.Line(new Vector2(x - 5, -radius * .75f), new Vector2(x + 5, radius * .75f), .6f,
                    Value ? new Color32(10, 88, 51, 110) : new Color32(68, 91, 127, 100));
            Vector2 previous = new Vector2(travel + radius, 0);
            for (int i = 1; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                Vector2 next = new Vector2((Mathf.Cos(a) >= 0 ? travel : -travel) + Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                pen.Line(previous, next, 1.6f, PlayerUi.Ink); previous = next;
            }
            Vector2 knob = Value ? right : left;
            pen.Disc(knob, radius * .85f, PlayerUi.Card, 32);
            pen.Ring(knob, radius * .85f, 1.1f, PlayerUi.Ink, 32);
            pen.CopyTo(vh, r.center, 1);
        }
    }

    public sealed class PlayerSettingsDisplayGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var pen = new MeshPen(null, 128);
            pen.Box(new Vector2(0, .175f), new Vector2(1.52f, 1.05f), Color.clear, color, .07f, 22);
            pen.Box(new Vector2(0, .17f), new Vector2(1.28f, .82f), Color.clear, color, .03f, 33);
            pen.Line(new Vector2(-.22f, -.4f), new Vector2(-.22f, -.65f), .07f, color);
            pen.Line(new Vector2(.22f, -.4f), new Vector2(.22f, -.65f), .07f, color);
            pen.Line(new Vector2(-.48f, -.67f), new Vector2(.48f, -.67f), .08f, color);
            pen.CopyTo(vh, rectTransform.rect.center, Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .48f);
        }
    }

}
