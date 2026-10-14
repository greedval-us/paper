using PaperDefense.Core;
using System.Collections.Generic;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Small, deliberately imperfect ink silhouettes used by the batched world view.</summary>
    internal static class DoodleGlyphs
    {
        internal static bool UsesTemplateMount(StructureKind kind) => kind == StructureKind.Fan ||
            kind == StructureKind.Ricochet || kind == StructureKind.Stapler || kind == StructureKind.Blotter;

        static readonly Dictionary<StructureKind, AuthoredPartGeometry> standardMounts = new Dictionary<StructureKind, AuthoredPartGeometry>();
        internal static AuthoredPartGeometry StandardMount(StructureKind kind)
        {
            if (standardMounts.TryGetValue(kind, out var cached)) return cached;
            var blueprint = new TowerBlueprint("standard-mount", UserText.Message("copy.cb4d54cfa87bc552"), ChassisKind.Standard, kind);
            foreach (var part in DrawingTemplateCatalog.Create(blueprint))
                if (part.Key == DrawingPartKey.Mount0)
                {
                    var source = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount0, part.Contours) });
                    cached = AuthoredGeometryCache.For(source).GetPart(DrawingPartKey.Mount0);
                    standardMounts.Add(kind, cached); return cached;
                }
            return null;
        }
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

        public static void StructureBase(MeshPen pen, StructureView state, NotebookVisualThemeAsset theme = null, int wallMask = 0)
        {
            Vector2 p = new Vector2(state.Cell.X + .5f, state.Cell.Y + .5f);
            Color color = TierColor(state.Tier);
            bool building = !state.IsComplete;
            float alpha = building ? .48f : 1;
            color.a = alpha;
            Color edge = MeshPen.Alpha(NotebookPalette.Navy, alpha);
            Color fill = Color.Lerp(NotebookPalette.Paper, color, .22f);
            if (building) fill.a = .7f;
            bool authored = state.Blueprint != null && state.Blueprint.HasPlayerDrawing;
            if (NotebookTowerComposition.UsesComposition(state.Blueprint))
                NotebookTowerComposition.DrawStatic(pen, state.Blueprint, p, state.Tier, state.Color, alpha, theme);
            else if (state.Kind == StructureKind.Wall && NotebookArtRenderer.Draw(pen,
                (theme ?? NotebookVisualThemeAsset.Default)?.FindShape("object.wall." + wallMask), p, strokeTint: MeshPen.Alpha(color, 1), opacity: alpha, theme: theme)) { }
            else if (state.Kind == StructureKind.Spikes && NotebookArtRenderer.Draw(pen,
                (theme ?? NotebookVisualThemeAsset.Default)?.FindShape("object.spikes"), p, strokeTint: MeshPen.Alpha(color, 1), opacity: alpha, theme: theme)) { }
            else if ((state.Kind == StructureKind.TarPit || state.Kind == StructureKind.RepairPost) && NotebookArtRenderer.Draw(pen,
                (theme ?? NotebookVisualThemeAsset.Default)?.FindShape(state.Kind == StructureKind.TarPit ? "object.tar" : "object.repair"),
                p, strokeTint: MeshPen.Alpha(color, 1), opacity: alpha, theme: theme)) { }
            else if (authored || ReferenceDrawingCache.UsesTemplateVisual(state.Blueprint))
            {
                var geometry = AuthoredGeometryCache.For(ReferenceDrawingCache.VisibleDrawing(state.Blueprint));
                foreach (var template in DrawingTemplateCatalog.Create(state.Blueprint))
                {
                    if (template.Role == DrawingPartRole.Mount) continue;
                    var pivot = template.Pivot;
                    pen.Append(geometry.GetPart(template.BindingId), p + new Vector2(pivot.X, pivot.Y), 0, color);
                }
            }
            else
            {
                pen.Disc(p + new Vector2(.055f, -.065f), .46f, MeshPen.Alpha(NotebookPalette.Navy, .07f), 12, .8f);
                switch (state.Kind)
                {
                    case StructureKind.Wall: Wall(pen, p, color, edge, fill, state.Id); break;
                    case StructureKind.Spikes: Spikes(pen, p, color, edge, fill, state.Id); break;
                    case StructureKind.TarPit:
                        pen.Disc(p, .4f, MeshPen.Alpha(NotebookPalette.Navy, .55f), 11, .65f);
                        pen.Ring(p, .4f, .05f, edge, 11, .65f, state.Id); break;
                    case StructureKind.RepairPost:
                        pen.Box(p, new Vector2(.65f, .65f), fill, edge, .05f, state.Id);
                        pen.Line(p - Vector2.right * .2f, p + Vector2.right * .2f, .12f, NotebookPalette.Green);
                        pen.Line(p - Vector2.up * .2f, p + Vector2.up * .2f, .12f, NotebookPalette.Green); break;
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
                    if ((state.Blueprint.Modules & TowerModules.Rail) != 0)
                    {
                        pen.Line(p + new Vector2(-.28f, -.24f), p + new Vector2(.28f, -.24f), .035f, edge);
                        pen.Line(p + new Vector2(-.28f, -.34f), p + new Vector2(.28f, -.34f), .035f, edge);
                        for (int i = -2; i <= 2; i++) pen.Line(p + new Vector2(i * .12f, -.21f), p + new Vector2(i * .12f, -.37f), .02f, color);
                    }
                }
            }
            if(state.Blueprint?.Reinforcement!=null)Reinforcement(pen,p,state.Blueprint.Reinforcement,edge);
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

        static void Reinforcement(MeshPen pen,Vector2 center,ArmorDistribution armor,Color color)
        {
            for(int sector=0;sector<4;sector++)
            {
                int count=armor.Points[sector];
                for(int line=0;line<count;line++)
                {
                    float radius=.55f+line*.014f;Vector2 previous=default;
                    for(int step=0;step<=8;step++)
                    {
                        float angle=((sector+armor.FacingQuarterTurns)*90-37+74*step/8)*Mathf.Deg2Rad;
                        var next=center+new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*radius;
                        if(step>0)pen.Line(previous,next,.012f,color);previous=next;
                    }
                }
            }
            var front=ArmorProbe.Direction(armor.FacingQuarterTurns);var d=new Vector2(front.X,front.Y);var tip=center+d*.49f;
            pen.Line(center+d*.34f,tip,.025f,color);var side=new Vector2(-d.y,d.x);
            pen.Line(tip,tip-d*.07f+side*.04f,.02f,color);pen.Line(tip,tip-d*.07f-side*.04f,.02f,color);
        }

        public static void StructureDynamic(MeshPen pen, StructureView state, float buildDuration, double seconds = 0,
            NotebookAnimationState animation = null, NotebookVisualThemeAsset theme = null)
        {
            Vector2 p = new Vector2(state.Cell.X + .5f, state.Cell.Y + .5f);
            Color color = state.Color == InkColor.None ? TierColor(state.Tier) : InkTone(state.Color);
            if (!state.IsComplete) color.a = .48f;
            Color edge = MeshPen.Alpha(NotebookPalette.Navy, color.a);
            if (state.Blueprint != null)
            {
                if (NotebookTowerComposition.UsesComposition(state.Blueprint))
                    NotebookTowerComposition.DrawMounts(pen, state.Blueprint, p, state.Tier, state.Color,
                        state, true, animation, state.Id, seconds, state.IsComplete ? 1 : .48f, theme);
                else if (state.Blueprint.HasPlayerDrawing || ReferenceDrawingCache.UsesTemplateVisual(state.Blueprint))
                {
                    var geometry = AuthoredGeometryCache.For(ReferenceDrawingCache.VisibleDrawing(state.Blueprint));
                    foreach (var mount in state.Blueprint.WeaponMounts)
                        DrawAuthoredMount(pen, geometry, state.Blueprint, "mount." + mount.SlotIndex,
                            p, state.GetMountAim(mount.SlotIndex), color);
                }
                else
                {
                    foreach (var mount in state.Blueprint.WeaponMounts)
                    {
                        Point2 pivot;
                        if (state.Blueprint.Content != null && state.Blueprint.Content.TryGetChassis(state.Blueprint.ChassisId, out var chassis))
                            pivot = chassis.GetMountPivot(mount.SlotIndex, state.Blueprint.WeaponMounts.Count);
                        else pivot = new Point2(state.Blueprint.WeaponMounts.Count == 1 ? 0 : mount.SlotIndex == 0 ? -.17f : .17f, .04f);
                        DrawWeaponBarrel(pen, p + new Vector2(pivot.X, pivot.Y - .04f),
                            LegacyContentIds.Weapon(mount.WeaponId), color, edge, state.GetMountAim(mount.SlotIndex));
                    }
                    if ((state.Blueprint.Modules & TowerModules.Feed) != 0) pen.Disc(p - Vector2.up * .18f, .08f, NotebookPalette.Green, 8);
                    if ((state.Blueprint.Modules & TowerModules.Tracking) != 0) pen.Ring(p + new Vector2(.38f, .29f), .1f, .025f, NotebookPalette.Red, 12);
                }
            }
            else if (state.Kind == StructureKind.Gun) GunBarrel(pen, p, color, edge, state.PrimaryAim);
            else if (state.Kind == StructureKind.Piercer) PiercerBarrel(pen, p, color, edge, state.PrimaryAim);
            if (state.IsComplete) return;
            float progress = Mathf.Repeat((float)seconds * .45f + state.Id * .13f, 1);
            float angle = progress * Mathf.PI * 2;
            pen.Disc(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .48f, .065f,
                     state.WaitingForClearance ? NotebookPalette.Red : color, 8);
            float fraction = 1 - Mathf.Clamp01(state.BuildRemaining / Mathf.Max(.01f, buildDuration));
            pen.Line(p + new Vector2(-.35f, -.6f), p + new Vector2(.35f, -.6f), .06f,
                     MeshPen.Alpha(NotebookPalette.Navy, .16f));
            pen.Line(p + new Vector2(-.35f, -.6f), p + new Vector2(-.35f + .7f * fraction, -.6f), .06f,
                     state.WaitingForClearance ? NotebookPalette.Red : color);
        }
        static void DrawAuthoredMount(MeshPen pen, AuthoredDrawingGeometry geometry, TowerBlueprint blueprint,
            string bindingId, Vector2 position, Point2 aim, Color color)
        {
            var pivot = DrawingTemplateCatalog.Pivot(blueprint, bindingId);
            Vector2 at = position + new Vector2(pivot.X, pivot.Y);
            Vector2 direction = Direction(aim, at);
            float angle = Mathf.Atan2(direction.y, direction.x) - Mathf.PI * .5f;
            pen.Append(geometry.GetPart(bindingId), at, angle, color);
        }
        static void DrawWeaponBarrel(MeshPen pen, Vector2 p, StructureKind kind, Color color, Color edge, Point2 aim)
        {
            if (UsesTemplateMount(kind))
            {
                Vector2 pivot = p + Vector2.up * .04f;
                Vector2 forward = Direction(aim, pivot);
                pen.Append(StandardMount(kind), pivot, Mathf.Atan2(forward.y, forward.x) - Mathf.PI * .5f, color);
                return;
            }
            if (kind == StructureKind.Gun) GunBarrel(pen, p, color, edge, aim);
            else if (kind == StructureKind.Piercer) PiercerBarrel(pen, p, color, edge, aim);
            else
            {
                Vector2 direction = Direction(aim, p);
                pen.Disc(p + direction * .11f, .17f, edge, 12, .8f);
                pen.Ring(p + direction * .11f, .15f, .05f, color, 12, .8f);
                pen.Line(p - direction * .12f, p + direction * .08f, .17f, color);
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

        public static void Enemy(MeshPen pen, EnemyView enemy, float time, bool compact = false,
            NotebookAnimationState animation = null, NotebookVisualThemeAsset theme = null, double sampleSeconds = double.NaN, float windupSeconds = 0)
        {
            if (compact)
            {
                int first = pen.VertexCount;
                CompactEnemy(pen, enemy);
                pen.ScaleFrom(first, new Vector2(enemy.Position.X, enemy.Position.Y), VisualScale(enemy.VisualScale));
                EnemyMarks(pen, enemy, time, theme); EnemyHealth(pen, enemy); return;
            }
            float phase = time + enemy.Id * .137f;
            NotebookEnemyMotion motionKind = animation != null ? animation.Observe(enemy, time, out phase, theme, sampleSeconds, windupSeconds)
                : enemy.WindupRemaining > 0 ? NotebookEnemyMotion.Attack : NotebookEnemyMotion.Idle;
            if (DrawResourceEnemy(pen, enemy.Kind, enemy.DrawingTier, new Vector2(enemy.Position.X, enemy.Position.Y), motionKind, phase, theme, enemy.VisualScale))
            {
                EnemyMarks(pen, enemy, time, theme);
                EnemyHealth(pen, enemy);
                return;
            }
            int firstFallback = pen.VertexCount;
            Vector2 p = new Vector2(enemy.Position.X, enemy.Position.Y);
            float motion = motionKind == NotebookEnemyMotion.Walk ? Mathf.Sin(time * (enemy.Kind == EnemyKind.Runner ? 15 : 9) + enemy.Id * 1.37f) : 0;
            float scale = enemy.Kind == EnemyKind.Gnawer ? .43f : enemy.Kind == EnemyKind.Brute ? .46f : enemy.Kind == EnemyKind.Bulwark ? .4f : enemy.Kind == EnemyKind.Runner ? .29f : .32f;
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
            pen.ScaleFrom(firstFallback, new Vector2(enemy.Position.X, enemy.Position.Y), VisualScale(enemy.VisualScale));
            EnemyMarks(pen, enemy, time, theme); EnemyHealth(pen, enemy);
        }
        internal static float VisualScale(float value) => float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? 1 : value;
        internal static float EnemyHealthRatio(float health, float maximum)
            => maximum > 0 && !float.IsNaN(health) && !float.IsNaN(maximum) ? Mathf.Clamp01(health / maximum) : 0;
        static void EnemyHealth(MeshPen pen, EnemyView enemy)
        {
            float scale = VisualScale(enemy.VisualScale), width = Mathf.Max(.72f, .72f * scale);
            float height = Mathf.Max(.24f, pen.WorldUnitsPerPixel * 2);
            // Solid paper backing and a dark border remain readable above both terrain and ink art.
            Vector2 at = new Vector2(enemy.Position.X, enemy.Position.Y) + Vector2.up * (.75f * scale + .13f);
            pen.Rect(at.x - width * .5f - .06f, at.y - .06f, width + .12f, height + .12f, NotebookPalette.Paper);
            pen.Rect(at.x - width * .5f - .025f, at.y - .025f, width + .05f, height + .05f, NotebookPalette.Navy);
            pen.Rect(at.x - width * .5f, at.y, width, height, new Color(.77f, .75f, .69f));
            float ratio = EnemyHealthRatio(enemy.Health, enemy.MaxHealth);
            if (ratio > 0) pen.Rect(at.x - width * .5f, at.y, width * ratio, height,
                ratio > .35f ? NotebookPalette.Green : NotebookPalette.Red);
        }
        internal static bool DrawResourceEnemy(MeshPen pen, EnemyKind kind, DrawingTier tier, Vector2 position,
            NotebookEnemyMotion motion, float phase, NotebookVisualThemeAsset theme = null, float visualScale = 1)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            if (theme == null) return false;
            visualScale = VisualScale(visualScale);
            string id = NotebookAnimationState.EnemyId(kind, tier, motion);
            var clip = theme.FindClip(id);
            if (clip != null)
            {
                var frame = clip.Sample(Mathf.Max(0, phase));
                if (frame == null) return false;
                return NotebookArtRenderer.Draw(pen, theme.FindShape(frame.ShapeId), position + frame.Offset * visualScale,
                    frame.Scale * visualScale, frame.Rotation, opacity: frame.Alpha, theme: theme);
            }
            return NotebookArtRenderer.Draw(pen, theme.FindShape(id), position, scale: visualScale, theme: theme);
        }

        static void EnemyMarks(MeshPen pen, EnemyView enemy, float time, NotebookVisualThemeAsset theme = null)
        {
            int firstMark = pen.VertexCount;
            Vector2 p = new Vector2(enemy.Position.X, enemy.Position.Y);
            if (enemy.DrawingTier == DrawingTier.Hatched)
                for (int i = -1; i <= 1; i++) pen.Line(p + new Vector2(-.17f, i * .1f - .11f), p + new Vector2(.15f, i * .1f + .04f), .028f, NotebookPalette.Navy);
            else if (enemy.DrawingTier == DrawingTier.Outlined) pen.Ring(p, .37f, .028f, MeshPen.Alpha(NotebookPalette.Navy, .65f), 8, .88f, enemy.Id);
            if (enemy.BlotRemaining > 0) pen.Ring(p, .48f, .07f, MeshPen.Alpha(NotebookPalette.Marker, .7f), 12, .9f, enemy.Id);
            theme = theme ?? NotebookVisualThemeAsset.Default;
            if (enemy.BlueRemaining > 0 && !NotebookArtRenderer.Draw(pen, theme?.FindShape("fx.status.blue"), p, theme: theme))
                pen.Line(p + new Vector2(-.31f, -.43f), p + new Vector2(.31f, -.43f), .075f, NotebookPalette.Blue);
            if (enemy.GreenRemaining > 0 && !NotebookArtRenderer.Draw(pen, theme?.FindShape("fx.status.green"), p, theme: theme))
                pen.Ring(p, .42f, .045f, NotebookPalette.Green, 8, 1, enemy.Id, true);
            if (enemy.RedRemaining > 0 && !NotebookArtRenderer.Draw(pen, theme?.FindShape("fx.status.red"), p, theme: theme))
                pen.Disc(p + new Vector2(.32f, .24f), .09f, NotebookPalette.Red, 7);
            if (enemy.Kind == EnemyKind.Spitter && enemy.WindupRemaining > 0)
            {
                float pulse = .6f + .4f * Mathf.Sin(time * 18);
                pen.Ring(p, .54f, .055f + pulse * .035f, MeshPen.Alpha(NotebookPalette.Red, .6f + .4f * pulse), 16);
                pen.Line(p + new Vector2(0, .56f), p + new Vector2(0, .85f), .095f, NotebookPalette.Red);
                pen.Disc(p + new Vector2(0, .48f), .05f, NotebookPalette.Red, 6);
            }
            pen.ScaleFrom(firstMark, p, VisualScale(enemy.VisualScale));
        }

        // The mass-swarm glyph keeps the silhouette and red eyes at ~18–68 vertices,
        // compared with ~67–151 for the full doodle. It also avoids animated limbs.
        static void CompactEnemy(MeshPen pen, EnemyView enemy)
        {
            Vector2 p = new Vector2(enemy.Position.X, enemy.Position.Y);
            Color ink = NotebookPalette.Navy;
            Color fill = new Color(.86f, .86f, .81f);
            float scale = enemy.Kind == EnemyKind.Gnawer ? .43f : enemy.Kind == EnemyKind.Brute ? .46f : enemy.Kind == EnemyKind.Bulwark ? .4f : enemy.Kind == EnemyKind.Runner ? .29f : .32f;
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

        public static void Castle(MeshPen pen, Vector2 p, float healthFraction, float seconds = 0, NotebookVisualThemeAsset theme = null)
        {
            // Resource art uses normalized icon coordinates; match the fallback landmark size.
            if (NotebookArtRenderer.Draw(pen, (theme ?? NotebookVisualThemeAsset.Default)?.FindShape(
                healthFraction < .6f ? "object.castle.damaged" : "object.castle"), p, scale: 3, theme: theme)) return;
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
            float wave = Mathf.Sin(seconds * 2.2f) * .07f;
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
