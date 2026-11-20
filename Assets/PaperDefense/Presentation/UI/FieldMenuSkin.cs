using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Field headquarters materials, drawn inside the existing menu control bounds.</summary>
    public static class FieldMenuSkin
    {
        public static readonly Color Background = new Color32(13, 23, 24, 255);
        public static readonly Color Panel = new Color32(24, 36, 37, 255);
        public static readonly Color Raised = new Color32(37, 51, 52, 255);
        public static readonly Color Ink = new Color32(243, 241, 226, 255);
        public static readonly Color Muted = new Color32(172, 190, 184, 255);
        public static readonly Color Accent = new Color32(241, 184, 90, 255);
        public static readonly Color AccentInk = new Color32(38, 32, 21, 255);
        public static readonly Color Border = new Color32(112, 138, 131, 255);
        public static readonly Color Disabled = new Color32(147, 162, 156, 255);
        public static readonly Color PreviewPaper = new Color32(230, 223, 196, 255);

        static readonly Color Red = new Color32(247, 162, 140, 255);
        static readonly Color Green = new Color32(159, 204, 162, 255);
        static readonly Color Chosen = new Color32(29, 63, 56, 255);
        // uGUI rebuilds meshes on the main thread and copies this geometry before the next draw.
        static readonly MeshPen strokes = new MeshPen(null, 1024);

        public static void Tab(Button button)
        {
            if (button.targetGraphic is PlayerCardGraphic card) card.FieldTab = true;
            else if (button.targetGraphic is WorkshopSurfaceGraphic surface) surface.FieldTab = true;
            button.targetGraphic.SetVerticesDirty();
        }

        /// <summary>Retains semantic highlights while making legacy notebook ink legible on forest panels.</summary>
        public static Color TextColor(Color source, bool primary = false, bool disabled = false)
        {
            if (source.a <= 0) return source;
            Color result;
            if (disabled) result = Disabled;
            else if (primary) result = AccentInk;
            else if (Same(source, Accent) || Same(source, Red) || Same(source, Green) || Same(source, Ink) ||
                Same(source, Muted) || Same(source, AccentInk) || Same(source, Disabled)) result = source;
            else if (Same(source, PlayerUi.Ink) || Same(source, NotebookPalette.Navy)) result = Ink;
            else if (Same(source, PlayerUi.Muted) || Same(source, NotebookPalette.Pencil)) result = Muted;
            else if (source.r > .48f && source.r > source.g * 1.3f && source.r > source.b * 1.35f)
                result = source.g > source.b * 1.5f ? Accent : Red;
            else if (source.g > source.r * 1.12f && source.g > source.b * 1.06f) result = Green;
            else if (source.b > source.r * 1.15f && source.b > source.g * 1.12f) result = Accent;
            else result = Ink;
            return Alpha(result, source.a);
        }

        public static void Draw(VertexHelper target, Rect rect, Color source, bool control, bool primary,
            bool selected, bool focused, bool disabled, bool hovered, bool pressed)
            => Draw(target, rect, source, control, primary, selected, focused, disabled, hovered, pressed, null);

        public static void Draw(VertexHelper target, Rect rect, Color source, bool control, bool primary,
            bool selected, bool focused, bool disabled, bool hovered, bool pressed, UiInteractionFeedback feedback, bool tab = false)
        {
            if (source.a <= 0 || rect.width <= 0 || rect.height <= 0) return;
            float hover = feedback != null ? feedback.HoverAmount : hovered ? 1 : 0;
            float press = feedback != null ? feedback.PressAmount : pressed ? 1 : 0;
            float focus = feedback != null ? feedback.FocusAmount : focused ? 1 : 0;
            float choice = feedback != null ? feedback.SelectionAmount : selected ? 1 : 0;
            if (feedback != null) disabled = !feedback.Available;
            Color fill = SurfaceColor(source, control, primary);
            if (tab) fill = Panel;
            if (disabled) fill = Color.Lerp(Panel, Raised, .22f);
            else
            {
                if (!primary) fill = Color.Lerp(fill, Chosen, choice * .88f);
                // Keep the body response visible without washing out warning or secondary ink.
                fill = Color.Lerp(fill, Ink, hover * (primary ? .10f : .045f));
                fill = Color.Lerp(fill, Background, press * (primary ? .14f : .30f));
            }

            float alpha = source.a;
            float smallSide = Mathf.Min(rect.width, rect.height);
            if (smallSide < 8)
            {
                Fill(target, rect, 0, Alpha(fill, alpha), Alpha(fill, alpha));
                return;
            }

            Rect edge = Inset(rect, 1 + (disabled ? 0 : press));
            float cut = tab ? 0 : Mathf.Min(primary ? 4 : 2, smallSide * .1f);
            Color top = Color.Lerp(fill, Ink, primary ? .065f : .012f);
            Color bottom = Color.Lerp(fill, Background, primary ? .035f : .18f);
            Fill(target, edge, cut, Alpha(bottom, alpha), Alpha(top, alpha));

            var pen = strokes;
            pen.Clear();
            Color rim = Color.Lerp(Border, Accent, choice);
            if (tab)
                pen.Rect(edge.xMin, edge.yMin, edge.width, 1, Alpha(Border, alpha * .32f));
            else
                Outline(pen, edge, cut, 1, Alpha(primary && !disabled ? Ink : rim,
                    alpha * (control ? .25f + .37f * hover + .30f * choice : .26f)));
            if (choice > 0 && !disabled)
            {
                Color signal = Alpha(primary ? AccentInk : Accent, alpha * choice);
                if (tab)
                    pen.Rect(edge.xMin + 1, edge.yMin, edge.width - 2, 3, signal);
                else
                    pen.Rect(edge.xMin + 1, edge.yMin + cut, 3, edge.height - 2 * cut, signal);
            }
            if (press > 0 && !disabled && smallSide >= 22)
            {
                float inset = Mathf.Min(7, smallSide * .2f);
                pen.Line(new Vector2(edge.xMin + inset, edge.yMax - 3),
                    new Vector2(edge.xMax - inset, edge.yMax - 3), 1.2f, Alpha(Background, alpha * press * .85f));
                pen.Line(new Vector2(edge.xMin + inset, edge.yMin + 3),
                    new Vector2(edge.xMax - inset, edge.yMin + 3), .8f, Alpha(Ink, alpha * press * .28f));
            }
            if (focus > 0 && !disabled && smallSide >= 22)
                Focus(pen, Inset(edge, 6), Alpha(primary ? AccentInk : Ink, alpha * focus));
            pen.CopyTo(target, Vector2.zero, 1);
        }

        static Color SurfaceColor(Color source, bool control, bool primary)
        {
            if (primary) return Accent;
            if (control) return Raised;
            if (Same(source, Background) || Same(source, Panel) || Same(source, Raised) ||
                Same(source, PreviewPaper) || Same(source, Accent)) return source;
            if (Same(source, PlayerUi.Blue) || Same(source, NotebookPalette.Marker)) return Accent;
            if (source.r > .48f && source.r > source.g * 1.3f && source.r > source.b * 1.35f)
                return source.g > source.b * 1.5f ? Accent : Red;
            if (source.g > source.r * 1.12f && source.g > source.b * 1.06f) return Green;
            if (source.grayscale < .4f) return Background;
            if (Same(source, PlayerUi.Selected)) return Raised;
            return Panel;
        }

        static void Focus(MeshPen pen, Rect rect, Color tint)
        {
            float length = Mathf.Min(12, rect.width * .16f, rect.height * .28f);
            for (int i = 0; i < 4; i++)
            {
                bool left = i < 2, bottom = i == 0 || i == 3;
                var corner = new Vector2(left ? rect.xMin : rect.xMax, bottom ? rect.yMin : rect.yMax);
                pen.Line(corner, corner + new Vector2(left ? length : -length, 0), 1.6f, tint);
                pen.Line(corner, corner + new Vector2(0, bottom ? length : -length), 1.6f, tint);
            }
        }

        static void Outline(MeshPen pen, Rect rect, float cut, float width, Color tint)
        {
            for (int i = 0; i < 8; i++) pen.Line(Point(rect, cut, i), Point(rect, cut, (i + 1) % 8), width, tint);
        }

        static void Fill(VertexHelper target, Rect rect, float cut, Color bottom, Color top)
        {
            int start = target.currentVertCount;
            target.AddVert(rect.center, Color.Lerp(bottom, top, .5f), Vector2.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector2 point = Point(rect, cut, i);
                target.AddVert(point, Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)), Vector2.zero);
            }
            for (int i = 0; i < 8; i++) target.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
        }

        static Vector2 Point(Rect r, float cut, int index)
        {
            cut = Mathf.Clamp(cut, 0, Mathf.Min(r.width, r.height) * .5f);
            switch (index)
            {
                case 0: return new Vector2(r.xMin + cut, r.yMin);
                case 1: return new Vector2(r.xMax - cut, r.yMin);
                case 2: return new Vector2(r.xMax, r.yMin + cut);
                case 3: return new Vector2(r.xMax, r.yMax - cut);
                case 4: return new Vector2(r.xMax - cut, r.yMax);
                case 5: return new Vector2(r.xMin + cut, r.yMax);
                case 6: return new Vector2(r.xMin, r.yMax - cut);
                default: return new Vector2(r.xMin, r.yMin + cut);
            }
        }

        static Rect Inset(Rect r, float amount) => new Rect(r.xMin + amount, r.yMin + amount,
            Mathf.Max(0, r.width - amount * 2), Mathf.Max(0, r.height - amount * 2));
        static Color Alpha(Color source, float alpha) { source.a = alpha; return source; }
        static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < .015f &&
            Mathf.Abs(a.g - b.g) < .015f && Mathf.Abs(a.b - b.b) < .015f;
    }
}
