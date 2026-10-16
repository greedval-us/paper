using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>New atlas illustrations and live semantic marks of the batched battlefield.</summary>
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

        internal static bool DrawResource(MeshPen pen, string id, Vector2 at, float scale = 1,
            float radians = 0, float opacity = 1, Vector2? localScale = null, NotebookVisualThemeAsset theme = null)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            var shape = theme?.FindShape(id);
            return shape != null
                ? NotebookArtRenderer.Draw(pen, shape, at, scale, radians, opacity: opacity, localScale: localScale, theme: theme)
                : NotebookSpriteCatalog.TryDraw(pen, id, at, scale, radians, opacity, localScale);
        }

        public static void StructureBase(MeshPen pen, StructureView state, NotebookVisualThemeAsset theme = null, int wallMask = 0)
        {
            Vector2 p = new Vector2(state.Cell.X + .5f, state.Cell.Y + .5f);
            bool building = !state.IsComplete;
            if (building && NotebookTowerComposition.UsesComposition(state.Blueprint)) return;
            float alpha = building ? .48f : 1;
            Color color = MeshPen.Alpha(TierColor(state.Tier), alpha);
            Color edge = MeshPen.Alpha(NotebookPalette.Navy, alpha);
            if (NotebookTowerComposition.UsesComposition(state.Blueprint))
                NotebookTowerComposition.DrawStatic(pen, state.Blueprint, p, state.Tier, state.Color, alpha, theme);
            else
            {
                string id = state.Kind == StructureKind.Wall ? "object.wall." + wallMask
                    : state.Kind == StructureKind.Spikes ? "object.spikes"
                    : state.Kind == StructureKind.TarPit ? "object.tar"
                    : state.Kind == StructureKind.RepairPost ? "object.repair" : "chassis.standard";
                DrawResource(pen, id, p, opacity: alpha, theme: theme);
            }
            if (state.Blueprint?.Reinforcement != null) Reinforcement(pen, p, state.Blueprint.Reinforcement, edge);
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
                    pen.Box(p, new Vector2(.54f, .54f), MeshPen.Alpha(NotebookPalette.Paper, .7f), NotebookPalette.Red, .045f, state.Id);
                    pen.Line(p + new Vector2(-.2f, -.2f), p + new Vector2(.2f, .2f), .065f, NotebookPalette.Red);
                }
            }
            float health = state.MaxHealth > 0 ? Mathf.Clamp01(state.Health / (float)state.MaxHealth) : 1;
            if (health < .99f)
            {
                pen.Line(p + new Vector2(-.34f, -.52f), p + new Vector2(.34f, -.52f), .055f, MeshPen.Alpha(NotebookPalette.Navy, .17f));
                pen.Line(p + new Vector2(-.34f, -.52f), p + new Vector2(-.34f + .68f * health, -.52f),
                    .055f, health > .35f ? NotebookPalette.Blue : NotebookPalette.Red);
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
            float fraction = 1 - Mathf.Clamp01(state.BuildRemaining / Mathf.Max(.01f, buildDuration));
            if (!state.IsComplete && NotebookTowerComposition.UsesComposition(state.Blueprint))
                NotebookTowerComposition.DrawConstruction(pen, state, p, fraction, seconds, animation, theme);
            else if (NotebookTowerComposition.UsesComposition(state.Blueprint))
                NotebookTowerComposition.DrawMounts(pen, state.Blueprint, p, state.Tier, state.Color,
                    state, true, animation, state.Id, seconds, state.IsComplete ? 1 : .48f, theme);
            else
            {
                string weapon = LegacyContentIds.Weapon(state.Kind);
                if (weapon != null)
                {
                    Vector2 pivot = p + new Vector2(0, .04f);
                    Vector2 forward = Direction(state.PrimaryAim, pivot);
                    pivot += forward * (animation?.Recoil(state.Id, 0, seconds, theme) ?? 0);
                    DrawResource(pen, weapon, pivot, radians: Mathf.Atan2(forward.y, forward.x) - Mathf.PI * .5f,
                        opacity: state.IsComplete ? 1 : .48f, theme: theme);
                }
            }
            if (state.IsComplete) return;
            float progress = Mathf.Repeat((float)seconds * .45f + state.Id * .13f, 1);
            float angle = progress * Mathf.PI * 2;
            pen.Disc(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .48f, .065f,
                state.WaitingForClearance ? NotebookPalette.Red : color, 8);
            pen.Line(p + new Vector2(-.35f, -.6f), p + new Vector2(.35f, -.6f), .06f, MeshPen.Alpha(NotebookPalette.Navy, .16f));
            pen.Line(p + new Vector2(-.35f, -.6f), p + new Vector2(-.35f + .7f * fraction, -.6f), .06f,
                state.WaitingForClearance ? NotebookPalette.Red : color);
        }

        static Vector2 Direction(Point2 aim, Vector2 pivot)
        {
            if (aim.X == 0 && aim.Y == 0) return Vector2.up;
            Vector2 direction = new Vector2(aim.X, aim.Y) - pivot;
            return direction.sqrMagnitude > .01f ? direction.normalized : Vector2.up;
        }

        public static void Enemy(MeshPen pen, EnemyView enemy, float time, bool compact = false,
            NotebookAnimationState animation = null, NotebookVisualThemeAsset theme = null, double sampleSeconds = double.NaN, float windupSeconds = 0,
            bool showHealth = true)
        {
            float phase = time + enemy.Id * .137f;
            NotebookEnemyMotion motion = animation != null ? animation.Observe(enemy, time, out phase, theme, sampleSeconds, windupSeconds)
                : enemy.WindupRemaining > 0 ? NotebookEnemyMotion.Attack : NotebookEnemyMotion.Idle;
            // Four atlas vertices are cheaper than the retired compact glyph. Preserve the same
            // silhouette and animation for individual enemies and the largest crowds.
            DrawResourceEnemy(pen, enemy.Kind, enemy.DrawingTier, new Vector2(enemy.Position.X, enemy.Position.Y), motion, phase, theme, enemy.VisualScale);
            EnemyMarks(pen, enemy, time, theme);
            if (showHealth) EnemyHealth(pen, enemy);
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
            pen.Rect(at.x - width * .5f, at.y, width, height, MeshPen.Alpha(NotebookPalette.Navy, .22f));
            float ratio = EnemyHealthRatio(enemy.Health, enemy.MaxHealth);
            if (ratio > 0) pen.Rect(at.x - width * .5f, at.y, width * ratio, height,
                ratio > .35f ? NotebookPalette.Green : NotebookPalette.Red);
        }
        internal static bool DrawResourceEnemy(MeshPen pen, EnemyKind kind, DrawingTier tier, Vector2 position,
            NotebookEnemyMotion motion, float phase, NotebookVisualThemeAsset theme = null, float visualScale = 1)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            visualScale = VisualScale(visualScale);
            Vector2 silhouette = kind == EnemyKind.Runner ? new Vector2(1.16f, .86f)
                : kind == EnemyKind.Brute ? new Vector2(1.18f, 1.12f)
                : kind == EnemyKind.Bulwark ? new Vector2(1.08f, 1.05f) : Vector2.one;
            string id = NotebookAnimationState.EnemyId(kind, tier, motion);
            var frame = theme?.FindClip(id)?.Sample(Mathf.Max(0, phase));
            return DrawResource(pen, frame?.ShapeId ?? id, position + (frame?.Offset ?? Vector2.zero) * visualScale,
                (frame?.Scale ?? 1) * visualScale, frame?.Rotation ?? 0, frame?.Alpha ?? 1, silhouette, theme);
        }

        static void EnemyMarks(MeshPen pen, EnemyView enemy, float time, NotebookVisualThemeAsset theme)
        {
            int first = pen.VertexCount;
            Vector2 p = new Vector2(enemy.Position.X, enemy.Position.Y);
            if (enemy.BlotRemaining > 0) pen.Ring(p, .48f, .05f, MeshPen.Alpha(NotebookPalette.Marker, .6f), 12, .9f, enemy.Id);
            if (enemy.BlueRemaining > 0) DrawResource(pen, "fx.status.blue", p, theme: theme);
            if (enemy.GreenRemaining > 0) DrawResource(pen, "fx.status.green", p, theme: theme);
            if (enemy.RedRemaining > 0) DrawResource(pen, "fx.status.red", p, theme: theme);
            if (enemy.Kind == EnemyKind.Spitter && enemy.WindupRemaining > 0)
            {
                float pulse = .6f + .4f * Mathf.Sin(time * 18);
                pen.Ring(p, .54f, .055f + pulse * .035f, MeshPen.Alpha(NotebookPalette.Red, .6f + .4f * pulse), 16);
                pen.Line(p + new Vector2(0, .56f), p + new Vector2(0, .85f), .095f, NotebookPalette.Red);
                pen.Disc(p + new Vector2(0, .48f), .05f, NotebookPalette.Red, 6);
            }
            pen.ScaleFrom(first, p, VisualScale(enemy.VisualScale));
        }

        public static void Castle(MeshPen pen, Vector2 p, float healthFraction, float seconds = 0, NotebookVisualThemeAsset theme = null)
            => DrawResource(pen, healthFraction < .6f ? "object.castle.damaged" : "object.castle", p, 3, theme: theme);
        public static void Tree(MeshPen pen, Vector2 p, float size, Color color, int seed)
            => DrawResource(pen, "terrain.pine", p + Vector2.up * size * .5f, size, opacity: color.a);
        public static void Rock(MeshPen pen, Vector2 p, float size, Color color, int seed)
            => DrawResource(pen, "terrain.rock", p + Vector2.up * size * .25f, size, opacity: color.a);
    }
}
