using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Small, deliberately imperfect ink silhouettes used by the batched world view.</summary>
    internal static class DoodleGlyphs
    {
        public static Color TierColor(MaterialTier tier)
        {
            switch (tier)
            {
                case MaterialTier.Ink: return NotebookPalette.Blue;
                case MaterialTier.Marker: return NotebookPalette.Marker;
                default: return NotebookPalette.Pencil;
            }
        }
        public static Color InkTone(InkColor color)
        { return color == InkColor.Red ? NotebookPalette.Red : color == InkColor.Blue ? NotebookPalette.Blue : color == InkColor.Green ? NotebookPalette.Green : NotebookPalette.Navy; }

        public static void StructureBase(MeshPen pen, StructureView state)
        {
            Vector2 p = new Vector2(state.Cell.X + .5f, state.Cell.Y + .5f);
            Color color = TierColor(state.Tier);
            bool building = !state.IsComplete;
            float alpha = building ? .48f : 1;
            color.a = alpha;
            Color edge = MeshPen.Alpha(NotebookPalette.Navy, alpha);
            Color fill = Color.Lerp(NotebookPalette.Paper, color, .22f);
            if (building) fill.a = .7f;
            pen.Disc(p + new Vector2(.055f, -.065f), .46f, MeshPen.Alpha(NotebookPalette.Navy, .07f), 12, .8f);

            switch (state.Kind)
            {
                case StructureKind.Wall: Wall(pen, p, color, edge, fill, state.Id); break;
                case StructureKind.Spikes: Spikes(pen, p, color, edge, fill, state.Id); break;
                case StructureKind.Piercer: Piercer(pen, p, color, edge, fill, state.Id); break;
                case StructureKind.Mortar: Mortar(pen, p, color, edge, fill, state.Id); break;
                default: Gun(pen, p, color, edge, fill, state.Id); break;
            }
            if (state.Blueprint != null)
            {
                if (state.Blueprint.Chassis == ChassisKind.Heavy) pen.Ring(p, .46f, .075f, edge, 12, 1, state.Id);
                if ((state.Blueprint.Modules & TowerModules.Armor) != 0)
                    for (int side = -1; side <= 1; side += 2)
                        pen.Box(p + new Vector2(side * .39f, 0), new Vector2(.14f, .52f), fill, edge, .055f, state.Id + side);
            }
            if (state.Color != InkColor.None)
            {
                pen.Box(p + new Vector2(0, -.33f), new Vector2(.29f, .16f), InkTone(state.Color), edge, .025f, state.Id);
                pen.Disc(p + new Vector2(.12f, -.33f), .065f, InkTone(state.Color), 8);
            }

            if (building)
            {
                pen.Ring(p, .48f, .035f, MeshPen.Alpha(color, .8f), 24, 1, state.Id, true);
                if (state.WaitingForClearance)
                {
                    // A red blocked-cell sign distinguishes waiting from active construction.
                    pen.Box(p, new Vector2(.54f, .54f), MeshPen.Alpha(NotebookPalette.Paper, .7f), NotebookPalette.Red, .045f, state.Id);
                    pen.Line(p + new Vector2(-.2f, -.2f), p + new Vector2(.2f, .2f), .065f, NotebookPalette.Red);
                }
            }

            float health = state.MaxHealth > 0 ? Mathf.Clamp01(state.Health / (float)state.MaxHealth) : 1;
            if (health < .99f)
            {
                pen.Line(p + new Vector2(-.34f, -.52f), p + new Vector2(.34f, -.52f), .055f,
                         MeshPen.Alpha(NotebookPalette.Navy, .17f));
                pen.Line(p + new Vector2(-.34f, -.52f), p + new Vector2(-.34f + .68f * health, -.52f),
                         .055f, health > .35f ? NotebookPalette.Blue : NotebookPalette.Red);
                if (health < .6f)
                {
                    Vector2 a = p + new Vector2(-.12f, .28f);
                    Vector2 b = p + new Vector2(.015f, .04f);
                    pen.Line(a, b, .045f, NotebookPalette.Navy);
                    pen.Line(b, p + new Vector2(-.08f, -.09f), .045f, NotebookPalette.Navy);
                }
            }
        }

        public static void StructureDynamic(MeshPen pen, StructureView state, float buildDuration)
        {
            Vector2 p = new Vector2(state.Cell.X + .5f, state.Cell.Y + .5f);
            Color color = state.Color == InkColor.None ? TierColor(state.Tier) : InkTone(state.Color);
            if (!state.IsComplete) color.a = .48f;
            Color edge = MeshPen.Alpha(NotebookPalette.Navy, color.a);
            if (state.Blueprint != null)
            {
                bool dual = state.Blueprint.SecondWeapon.HasValue;
                DrawWeaponBarrel(pen, p + (dual ? Vector2.left * .17f : Vector2.zero), state.Blueprint.FirstWeapon, color, edge, state.Aim);
                if (dual) DrawWeaponBarrel(pen, p + Vector2.right * .17f, state.Blueprint.SecondWeapon.Value, color, edge, state.Aim);
                if ((state.Blueprint.Modules & TowerModules.Feed) != 0) pen.Disc(p - Vector2.up * .18f, .08f, NotebookPalette.Green, 8);
                if ((state.Blueprint.Modules & TowerModules.Tracking) != 0) pen.Ring(p + new Vector2(.38f, .29f), .1f, .025f, NotebookPalette.Red, 12);
            }
            else if (state.Kind == StructureKind.Gun) GunBarrel(pen, p, color, edge, state.Aim);
            else if (state.Kind == StructureKind.Piercer) PiercerBarrel(pen, p, color, edge, state.Aim);
            if (state.IsComplete) return;
            float progress = Mathf.Repeat(Time.unscaledTime * .45f + state.Id * .13f, 1);
            float angle = progress * Mathf.PI * 2;
            pen.Disc(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .48f, .065f,
                     state.WaitingForClearance ? NotebookPalette.Red : color, 8);
            float fraction = 1 - Mathf.Clamp01(state.BuildRemaining / Mathf.Max(.01f, buildDuration));
            pen.Line(p + new Vector2(-.35f, -.6f), p + new Vector2(.35f, -.6f), .06f,
                     MeshPen.Alpha(NotebookPalette.Navy, .16f));
            pen.Line(p + new Vector2(-.35f, -.6f), p + new Vector2(-.35f + .7f * fraction, -.6f), .06f,
                     state.WaitingForClearance ? NotebookPalette.Red : color);
        }
        static void DrawWeaponBarrel(MeshPen pen, Vector2 p, StructureKind kind, Color color, Color edge, Point2 aim)
        {
            if (kind == StructureKind.Gun) GunBarrel(pen, p, color, edge, aim);
            else if (kind == StructureKind.Piercer) PiercerBarrel(pen, p, color, edge, aim);
            else
            {
                pen.Disc(p + Vector2.up * .11f, .17f, edge, 12, .8f);
                pen.Ring(p + Vector2.up * .11f, .15f, .05f, color, 12, .8f);
                pen.Line(p - Vector2.up * .12f, p + Vector2.up * .08f, .17f, color);
            }
        }

        static void Wall(MeshPen pen, Vector2 p, Color color, Color edge, Color fill, int seed)
        {
            pen.Box(p, new Vector2(.88f, .83f), fill, edge, .064f, seed);
            pen.Stroke(p + new Vector2(-.36f, .31f), p + new Vector2(.34f, .31f), .044f, color, seed + 8);
            pen.Stroke(p + new Vector2(-.36f, -.31f), p + new Vector2(.34f, -.31f), .044f, color, seed + 9);
            pen.Line(p + new Vector2(-.42f, 0), p + new Vector2(.42f, 0), .037f, edge);
            pen.Line(p + new Vector2(-.08f, 0), p + new Vector2(-.08f, .4f), .035f, edge);
            pen.Line(p + new Vector2(.22f, 0), p + new Vector2(.22f, -.4f), .035f, edge);
            pen.Line(p + new Vector2(-.4f, -.35f), p + new Vector2(-.2f, -.15f), .027f, color);
            pen.Line(p + new Vector2(-.25f, -.36f), p + new Vector2(-.08f, -.19f), .027f, color);
        }

        static Vector2 Direction(Point2 aim, Vector2 p)
        {
            if (aim.X == 0 && aim.Y == 0) return Vector2.up;
            Vector2 direction = new Vector2(aim.X, aim.Y) - p;
            return direction.sqrMagnitude > .01f ? direction.normalized : Vector2.up;
        }

        static void Gun(MeshPen pen, Vector2 p, Color color, Color edge, Color fill, int seed)
        {
            pen.Disc(p, .37f, fill, 12);
            pen.Ring(p, .37f, .062f, edge, 12, 1, seed);
            pen.Ring(p, .26f, .03f, color, 12, 1, seed + 9);
            pen.Line(p + new Vector2(-.27f, -.34f), p + new Vector2(-.37f, -.43f), .055f, edge);
            pen.Line(p + new Vector2(.27f, -.34f), p + new Vector2(.37f, -.43f), .055f, edge);
        }

        static void GunBarrel(MeshPen pen, Vector2 p, Color color, Color edge, Point2 aim)
        {
            Vector2 direction = Direction(aim, p);
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 end = p + direction * .53f;
            pen.Line(p, end, .18f, edge);
            pen.Line(p, end, .095f, color);
            pen.Line(end - side * .13f, end + side * .13f, .065f, edge);
            pen.Disc(p, .11f, color, 10);
        }

        static void Piercer(MeshPen pen, Vector2 p, Color color, Color edge, Color fill, int seed)
        {
            pen.Quad(p + new Vector2(0, .41f), p + new Vector2(.38f, 0),
                     p + new Vector2(0, -.41f), p + new Vector2(-.38f, 0), fill);
            pen.Stroke(p + new Vector2(0, .41f), p + new Vector2(.38f, 0), .055f, edge, seed);
            pen.Stroke(p + new Vector2(.38f, 0), p + new Vector2(0, -.41f), .055f, edge, seed + 1);
            pen.Stroke(p + new Vector2(0, -.41f), p + new Vector2(-.38f, 0), .055f, edge, seed + 2);
            pen.Stroke(p + new Vector2(-.38f, 0), p + new Vector2(0, .41f), .055f, edge, seed + 3);
            pen.Line(p + new Vector2(-.3f, 0), p + new Vector2(.3f, 0), .075f, edge);
        }

        static void PiercerBarrel(MeshPen pen, Vector2 p, Color color, Color edge, Point2 aim)
        {
            Vector2 direction = Direction(aim, p);
            Vector2 side = new Vector2(-direction.y, direction.x);
            pen.Line(p - direction * .22f, p + direction * .55f, .08f, color);
            pen.Line(p + direction * .47f, p + direction * .3f + side * .13f, .06f, edge);
            pen.Line(p + direction * .47f, p + direction * .3f - side * .13f, .06f, edge);
            pen.Line(p - side * .3f, p + side * .3f, .075f, edge);
            pen.Line(p - side * .29f, p + direction * .23f, .036f, color);
            pen.Line(p + side * .29f, p + direction * .23f, .036f, color);
        }

        static void Mortar(MeshPen pen, Vector2 p, Color color, Color edge, Color fill, int seed)
        {
            pen.Box(p + new Vector2(0, -.17f), new Vector2(.75f, .34f), fill, edge, .055f, seed);
            pen.Disc(p + new Vector2(-.02f, .1f), .29f, color, 12, .9f);
            pen.Ring(p + new Vector2(-.02f, .1f), .29f, .057f, edge, 12, .9f, seed);
            pen.Line(p + new Vector2(.03f, .13f), p + new Vector2(.23f, .42f), .25f, edge);
            pen.Line(p + new Vector2(.03f, .13f), p + new Vector2(.23f, .42f), .135f, color);
            pen.Disc(p + new Vector2(.23f, .42f), .125f, NotebookPalette.Paper, 10, .63f);
            pen.Ring(p + new Vector2(.23f, .42f), .125f, .04f, edge, 10, .63f, seed);
            pen.Disc(p + new Vector2(-.24f, -.22f), .095f, edge, 8);
            pen.Disc(p + new Vector2(.24f, -.22f), .095f, edge, 8);
        }

        static void Spikes(MeshPen pen, Vector2 p, Color color, Color edge, Color fill, int seed)
        {
            pen.Box(p + new Vector2(0, -.2f), new Vector2(.86f, .3f), fill, edge, .045f, seed);
            for (int i = 0; i < 3; i++)
            {
                float x = -.28f + i * .28f;
                Vector2 a = p + new Vector2(x - .13f, -.17f);
                Vector2 b = p + new Vector2(x + .025f, .35f + (i % 2) * .07f);
                Vector2 c = p + new Vector2(x + .14f, -.17f);
                pen.Triangle(a, b, c, fill);
                pen.Line(a, b, .05f, edge); pen.Line(b, c, .05f, edge);
                pen.Line(b, p + new Vector2(x, -.16f), .026f, color);
            }
        }

        public static void Enemy(MeshPen pen, EnemyView enemy, float time, bool compact = false)
        {
            if (compact) { CompactEnemy(pen, enemy); EnemyMarks(pen, enemy, time); return; }
            Vector2 p = new Vector2(enemy.Position.X, enemy.Position.Y);
            float motion = Mathf.Sin(time * (enemy.Kind == EnemyKind.Runner ? 15 : 9) + enemy.Id * 1.37f);
            float scale = enemy.Kind == EnemyKind.Gnawer ? .43f : enemy.Kind == EnemyKind.Runner ? .29f : .32f;
            Color edge = MeshPen.Alpha(NotebookPalette.Navy, enemy.DrawingTier == DrawingTier.Sketch ? .73f : 1);
            float lineWidth = enemy.DrawingTier == DrawingTier.Hatched ? .078f : enemy.DrawingTier == DrawingTier.Outlined ? .059f : .04f;
            Color fill = enemy.Kind == EnemyKind.Spitter ? new Color(.78f, .83f, .82f) : new Color(.89f, .89f, .83f);
            pen.Disc(p + new Vector2(.04f, -.21f), scale * .9f, MeshPen.Alpha(edge, .08f), 8, .3f);
            p.y += motion * .025f;
            if (enemy.Kind == EnemyKind.Runner)
            {
                Vector2 a = p + new Vector2(-scale, -scale * .6f);
                Vector2 b = p + new Vector2(scale * .15f, scale * 1.25f);
                Vector2 c = p + new Vector2(scale, -scale * .6f);
                pen.Triangle(a, b, c, fill);
                pen.Line(a, b, lineWidth, edge); pen.Line(b, c, lineWidth, edge); pen.Line(c, a, lineWidth, edge);
                pen.Line(p + new Vector2(-.26f, .04f), p + new Vector2(-.45f, .11f), .03f, MeshPen.Alpha(edge, .5f));
                pen.Line(p + new Vector2(-.28f, -.08f), p + new Vector2(-.48f, -.06f), .03f, MeshPen.Alpha(edge, .5f));
            }
            else
            {
                pen.Disc(p, scale, fill, enemy.Kind == EnemyKind.Gnawer ? 9 : 11, .88f);
                pen.Ring(p, scale, lineWidth, edge, enemy.Kind == EnemyKind.Gnawer ? 9 : 11, .88f, enemy.Id);
                float horn = enemy.Kind == EnemyKind.Gnawer ? .22f : .12f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 a = p + new Vector2(side * scale * .65f, scale * .5f);
                    Vector2 b = p + new Vector2(side * (scale + .05f), scale + horn);
                    Vector2 c = p + new Vector2(side * .04f, scale * .75f);
                    pen.Triangle(a, b, c, fill);
                    pen.Line(a, b, .05f, edge); pen.Line(b, c, .05f, edge);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = p + new Vector2(side * scale * .34f, scale * .13f);
                pen.Line(eye - new Vector2(.055f, side * .015f), eye + new Vector2(.055f, side * .015f), .075f, NotebookPalette.Red);
                Vector2 foot = p + new Vector2(side * scale * .5f, -scale * .75f);
                pen.Line(foot, foot + new Vector2(side * .09f, -.11f - side * motion * .035f), .05f, edge);
            }
            pen.Line(p + new Vector2(-.1f, -scale * .37f), p + new Vector2(.1f, -scale * .37f), .035f, edge);
            if (enemy.Kind == EnemyKind.Gnawer)
            {
                pen.Triangle(p + new Vector2(-.1f, -.12f), p + new Vector2(-.04f, -.22f), p + new Vector2(.015f, -.12f), NotebookPalette.Paper);
                pen.Triangle(p + new Vector2(.015f, -.12f), p + new Vector2(.07f, -.22f), p + new Vector2(.12f, -.12f), NotebookPalette.Paper);
            }
            if (enemy.Kind == EnemyKind.Spitter)
            {
                pen.Disc(p + new Vector2(0, -.12f), .13f, edge, 10, .8f);
                pen.Disc(p + new Vector2(0, -.12f), .065f, NotebookPalette.Marker, 8);
                pen.Line(p + new Vector2(-.26f, -.14f), p + new Vector2(-.4f, -.26f), .07f, edge);
                pen.Line(p + new Vector2(.26f, -.14f), p + new Vector2(.4f, -.26f), .07f, edge);
            }
            float health = enemy.MaxHealth > 0 ? enemy.Health / (float)enemy.MaxHealth : 1;
            if (health < .95f)
            {
                pen.Line(p + new Vector2(-scale, scale + .2f), p + new Vector2(scale, scale + .2f), .042f, MeshPen.Alpha(edge, .15f));
                pen.Line(p + new Vector2(-scale, scale + .2f), p + new Vector2(-scale + scale * 2 * health, scale + .2f), .042f, NotebookPalette.Red);
            }
            EnemyMarks(pen, enemy, time);
        }
        static void EnemyMarks(MeshPen pen, EnemyView enemy, float time)
        {
            Vector2 p = new Vector2(enemy.Position.X, enemy.Position.Y);
            if (enemy.DrawingTier == DrawingTier.Hatched)
                for (int i = -1; i <= 1; i++) pen.Line(p + new Vector2(-.17f, i * .1f - .11f), p + new Vector2(.15f, i * .1f + .04f), .028f, NotebookPalette.Navy);
            else if (enemy.DrawingTier == DrawingTier.Outlined) pen.Ring(p, .37f, .028f, MeshPen.Alpha(NotebookPalette.Navy, .65f), 8, .88f, enemy.Id);
            if (enemy.BlotRemaining > 0) pen.Ring(p, .48f, .07f, MeshPen.Alpha(NotebookPalette.Marker, .7f), 12, .9f, enemy.Id);
            if (enemy.BlueRemaining > 0) pen.Line(p + new Vector2(-.31f, -.43f), p + new Vector2(.31f, -.43f), .075f, NotebookPalette.Blue);
            if (enemy.GreenRemaining > 0) pen.Ring(p, .42f, .045f, NotebookPalette.Green, 8, 1, enemy.Id, true);
            if (enemy.RedRemaining > 0) pen.Disc(p + new Vector2(.32f, .24f), .09f, NotebookPalette.Red, 7);
            if (enemy.Kind == EnemyKind.Spitter && enemy.WindupRemaining > 0)
            {
                float pulse = .6f + .4f * Mathf.Sin(time * 18);
                pen.Ring(p, .54f, .055f + pulse * .035f, MeshPen.Alpha(NotebookPalette.Red, .6f + .4f * pulse), 16);
                pen.Line(p + new Vector2(0, .56f), p + new Vector2(0, .85f), .095f, NotebookPalette.Red);
                pen.Disc(p + new Vector2(0, .48f), .05f, NotebookPalette.Red, 6);
            }
        }

        // The mass-swarm glyph keeps the silhouette and red eyes at ~18–68 vertices,
        // compared with ~67–151 for the full doodle. It also avoids animated limbs.
        static void CompactEnemy(MeshPen pen, EnemyView enemy)
        {
            Vector2 p = new Vector2(enemy.Position.X, enemy.Position.Y);
            Color ink = NotebookPalette.Navy;
            Color fill = new Color(.86f, .86f, .81f);
            float scale = enemy.Kind == EnemyKind.Gnawer ? .43f : enemy.Kind == EnemyKind.Runner ? .29f : .32f;
            if (enemy.Kind == EnemyKind.Runner)
            {
                Vector2 a = new Vector2(-scale, -scale * .6f);
                Vector2 b = new Vector2(scale * .15f, scale * 1.25f);
                Vector2 c = new Vector2(scale, -scale * .6f);
                pen.Triangle(p + a, p + b, p + c, ink);
                pen.Triangle(p + a * .72f, p + b * .72f, p + c * .72f, fill);
                pen.Line(p + new Vector2(-.31f, -.02f), p + new Vector2(-.47f, .02f), .04f, ink);
            }
            else
            {
                int segments = enemy.Kind == EnemyKind.Gnawer ? 8 : 7;
                pen.Disc(p, scale, ink, segments, .88f);
                pen.Disc(p, scale - .055f, fill, segments, .88f);
                float horn = enemy.Kind == EnemyKind.Gnawer ? .22f : .12f;
                for (int side = -1; side <= 1; side += 2)
                {
                    pen.Triangle(p + new Vector2(side * scale * .7f, scale * .45f),
                                 p + new Vector2(side * (scale + .05f), scale + horn),
                                 p + new Vector2(side * .04f, scale * .76f), ink);
                }
                if (enemy.Kind == EnemyKind.Gnawer)
                {
                    pen.Triangle(p + new Vector2(-.12f, -.12f), p + new Vector2(-.05f, -.24f), p + new Vector2(.02f, -.12f), ink);
                    pen.Triangle(p + new Vector2(.02f, -.12f), p + new Vector2(.08f, -.24f), p + new Vector2(.14f, -.12f), ink);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = p + new Vector2(side * scale * .34f, scale * .13f);
                pen.Rect(eye.x - .055f, eye.y - .045f, .11f, .09f, NotebookPalette.Red);
            }
        }

        public static void Castle(MeshPen pen, Vector2 p, float healthFraction)
        {
            Color ink = NotebookPalette.Navy;
            Color blue = NotebookPalette.Blue;
            Color stone = new Color(.89f, .91f, .88f);
            pen.Disc(p + new Vector2(.1f, -.81f), 1.7f, MeshPen.Alpha(ink, .09f), 20, .35f);
            pen.Box(p + new Vector2(0, -.06f), new Vector2(2.5f, 1.83f), stone, ink, .09f, 91);
            for (int i = 0; i < 5; i++)
                pen.Box(p + new Vector2(-.98f + i * .49f, .95f), new Vector2(.32f, .38f), stone, ink, .073f, i * 73);
            pen.Line(p + new Vector2(-1.2f, .67f), p + new Vector2(1.2f, .67f), .06f, blue);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 tower = p + new Vector2(side * 1.2f, .13f);
                pen.Box(tower, new Vector2(.72f, 2.17f), stone, ink, .09f, 101 + side);
                pen.Box(tower + new Vector2(-.25f, 1.14f), new Vector2(.22f, .38f), stone, ink, .067f, 31);
                pen.Box(tower + new Vector2(.25f, 1.14f), new Vector2(.22f, .38f), stone, ink, .067f, 32);
                pen.Box(tower + new Vector2(0, .53f), new Vector2(.18f, .43f), ink, ink, .04f, 52);
                for (int i = 0; i < 3; i++)
                    pen.Stroke(tower + new Vector2(-.26f, -.36f + i * .28f), tower + new Vector2(.21f, -.36f + i * .28f), .034f, MeshPen.Alpha(blue, .6f), i + side);
                pen.Line(tower + new Vector2(.2f, -.92f), tower + new Vector2(.32f, -.78f), .04f, blue);
            }
            pen.Box(p + new Vector2(0, -.58f), new Vector2(.72f, .79f), ink, ink, .04f, 19);
            pen.Disc(p + new Vector2(0, -.2f), .36f, ink, 14, .72f);
            for (int i = 0; i < 4; i++)
                pen.Line(p + new Vector2(-.25f + i * .165f, -.9f), p + new Vector2(-.25f + i * .165f, -.11f), .036f, stone);
            pen.Line(p + new Vector2(-.34f, -.58f), p + new Vector2(.34f, -.58f), .04f, blue);
            pen.Stroke(p + new Vector2(.18f, 1.1f), p + new Vector2(.18f, 2.25f), .065f, ink, 58);
            float wave = Mathf.Sin(Time.unscaledTime * 2.2f) * .07f;
            pen.Quad(p + new Vector2(.21f, 2.22f), p + new Vector2(1.02f, 2.1f + wave),
                     p + new Vector2(.82f, 1.82f + wave), p + new Vector2(.21f, 1.91f), blue);
            pen.Line(p + new Vector2(.21f, 2.22f), p + new Vector2(1.02f, 2.1f + wave), .044f, ink);
            pen.Line(p + new Vector2(.21f, 1.91f), p + new Vector2(.82f, 1.82f + wave), .038f, ink);
            // Blue brick hatching is intentionally uneven, like a doodle in a school notebook.
            pen.Line(p + new Vector2(-.77f, .17f), p + new Vector2(-.42f, .17f), .036f, blue);
            pen.Line(p + new Vector2(.34f, -.06f), p + new Vector2(.76f, -.06f), .036f, blue);
            pen.Line(p + new Vector2(.63f, -.06f), p + new Vector2(.63f, .2f), .033f, blue);
            if (healthFraction < .6f)
            {
                pen.Line(p + new Vector2(.7f, .61f), p + new Vector2(.51f, .32f), .07f, ink);
                pen.Line(p + new Vector2(.51f, .32f), p + new Vector2(.67f, .16f), .07f, ink);
                pen.Line(p + new Vector2(.67f, .16f), p + new Vector2(.55f, -.12f), .07f, ink);
            }
        }

        public static void Tree(MeshPen pen, Vector2 p, float size, Color color, int seed)
        {
            pen.Stroke(p, p + Vector2.up * size * .65f, .04f, color, seed);
            for (int i = 0; i < 3; i++)
            {
                float y = size * (.28f + i * .18f);
                float span = size * (.31f - i * .065f);
                pen.Stroke(p + new Vector2(-span, y), p + new Vector2(0, y + size * .3f), .035f, color, seed + i);
                pen.Stroke(p + new Vector2(0, y + size * .3f), p + new Vector2(span, y), .035f, color, seed + i + 3);
            }
            pen.Line(p + new Vector2(-size * .15f, -.03f), p + new Vector2(size * .14f, -.03f), .025f, color);
        }

        public static void Rock(MeshPen pen, Vector2 p, float size, Color color, int seed)
        {
            Vector2 a = p + new Vector2(-size * .5f, 0);
            Vector2 b = p + new Vector2(-size * .27f, size * .4f);
            Vector2 c = p + new Vector2(size * .25f, size * .5f);
            Vector2 d = p + new Vector2(size * .52f, .02f);
            pen.Stroke(a, b, .035f, color, seed); pen.Stroke(b, c, .035f, color, seed + 1);
            pen.Stroke(c, d, .035f, color, seed + 2); pen.Stroke(d, a, .035f, color, seed + 3);
            pen.Line(b, p + new Vector2(.08f, size * .03f), .025f, color);
            pen.Line(c, p + new Vector2(.08f, size * .03f), .025f, color);
        }
    }
}
