using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>One whole-enemy view transform; recorded simulation size and position remain untouched.</summary>
    public static class NotebookEnemyView
    {
        public const float ModernScale = .7f;
        public static float ScaleFor(bool modernPresentation) => modernPresentation ? ModernScale : 1;
    }

    /// <summary>One uniform presentation transform for modern assemblies in battle and recorded trials.</summary>
    public static class NotebookAssemblyView
    {
        public const float Scale = 2;
        static ConditionalWeakTable<RecipeSnapshot, ViewFit> viewFits = new ConditionalWeakTable<RecipeSnapshot, ViewFit>();
        static string revision;
        sealed class ViewFit
        {
            public Rect Envelope;
            public float Scale;
        }
        public static Vector2 Point(Vector2 origin, Vector2 local) => origin + local * Scale;
        public static Rect Bounds(Vector2 origin, Rect local) => new Rect(Point(origin, local.min), local.size * Scale);
        public static float ScaleFor(RecipeSnapshot snapshot) => snapshot == null ? Scale : Fit(snapshot).Scale;
        public static Vector2 Point(Vector2 origin, Vector2 local, RecipeSnapshot snapshot) => origin + local * ScaleFor(snapshot);
        public static Rect Bounds(Vector2 origin, RecipeSnapshot snapshot)
        {
            var fit = Fit(snapshot);
            return new Rect(origin + fit.Envelope.min * fit.Scale, fit.Envelope.size * fit.Scale);
        }

        static ViewFit Fit(RecipeSnapshot snapshot)
        {
            if (revision != AssemblyVisualComposer.Revision)
            {
                viewFits = new ConditionalWeakTable<RecipeSnapshot, ViewFit>();
                revision = AssemblyVisualComposer.Revision;
            }
            return viewFits.GetValue(snapshot, CalculateFit);
        }

        static ViewFit CalculateFit(RecipeSnapshot snapshot)
        {
            Rect envelope = new Rect(Vector2.zero, Vector2.zero);
            var tower = snapshot.Recipe.Tower;
            if (tower == null) Include(ref envelope, AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose()).Bounds);
            else
            {
                // Fixed foundation art and certified swept circles form one immutable envelope.
                // Current aimed or recoiling quads never participate in this scale calculation.
                Include(ref envelope, AssemblyVisualComposer.ComposeFoundation(tower.Foundation).Bounds);
                AssemblyVisualComposer.Fits.TryGetFrame(tower.Foundation.Frame.Id, tower.Foundation.Frame.Revision, out var frame);
                foreach (var mount in tower.Mounts)
                {
                    AssemblyVisualComposer.Fits.TryGetWeapon(mount.Weapon.Mechanism.Id, mount.Weapon.Mechanism.Revision, out var weapon);
                    foreach (var socket in frame.Mounts)
                    {
                        if (socket.SlotIndex != mount.SlotIndex) continue;
                        Vector2 pivot = new Vector2(socket.Position.X + weapon.Pivot.X, socket.Position.Y + weapon.Pivot.Y);
                        float radius = AssemblyFitRules.SweptRadius(weapon);
                        Include(ref envelope, new Rect(pivot - Vector2.one * radius, Vector2.one * radius * 2));
                        foreach (var region in weapon.Regions)
                            if (region.Motion == "fixed")
                                foreach (var point in region.Points)
                                    Include(ref envelope, new Rect(pivot + new Vector2(point.X, point.Y), Vector2.zero));
                    }
                }
            }
            float extent = Mathf.Max(Mathf.Abs(envelope.xMin), Mathf.Abs(envelope.xMax), Mathf.Abs(envelope.yMin), Mathf.Abs(envelope.yMax));
            return new ViewFit { Envelope = envelope, Scale = Mathf.Min(Scale, 1 / Mathf.Max(.0001f, extent)) };
        }

        static void Include(ref Rect envelope, Rect next) => envelope = Rect.MinMaxRect(Mathf.Min(envelope.xMin, next.xMin),
            Mathf.Min(envelope.yMin, next.yMin), Mathf.Max(envelope.xMax, next.xMax), Mathf.Max(envelope.yMax, next.yMax));
    }

    /// <summary>New atlas illustrations and live semantic marks of the batched battlefield.</summary>
    internal static class DoodleGlyphs
    {
        static ConditionalWeakTable<RecipeSnapshot, AssemblyVisualComposition> restAssemblies = new ConditionalWeakTable<RecipeSnapshot, AssemblyVisualComposition>();
        static string assemblyRevision;

        internal static AssemblyVisualComposition ComposeAssembly(StructureView state, double seconds = 0,
            NotebookAnimationState animation = null, NotebookVisualThemeAsset theme = null)
        {
            var snapshot = state.ResolvedEntry?.Snapshot;
            if (snapshot == null || state.ResolvedEntry.Kind == AlbumEntryKind.Legacy) return null;
            if (assemblyRevision != AssemblyVisualComposer.Revision)
            {
                restAssemblies = new ConditionalWeakTable<RecipeSnapshot, AssemblyVisualComposition>();
                assemblyRevision = AssemblyVisualComposer.Revision;
            }
            // ResolvedEntry owns the frozen run snapshot. An edited album page can never
            // change an already placed object's artwork or its projectile origin.
            var rest = restAssemblies.GetValue(snapshot, recipe => AssemblyVisualComposer.Compose(recipe, new AssemblyVisualPose()));
            if (snapshot.Recipe.Tower == null) return rest;
            var poses = new List<AssemblyMountPose>(rest.Anchors.Count);
            Vector2 at = new Vector2(state.Cell.X + .5f, state.Cell.Y + .5f);
            foreach (var anchor in rest.Anchors)
            {
                var aim = state.GetMountAim(anchor.SlotIndex);
                Vector2 delta = new Vector2(aim.X, aim.Y) - NotebookAssemblyView.Point(at, anchor.RotationCenter, snapshot);
                float angle = aim.X == 0 && aim.Y == 0 || delta.sqrMagnitude < .000001f ? 0 :
                    Mathf.Atan2(delta.y, delta.x) - Mathf.PI * .5f;
                angle = Mathf.Atan2(Mathf.Sin(angle), Mathf.Cos(angle));
                var weapon = snapshot.Recipe.Tower.Mounts;
                for (int i = 0; i < weapon.Count; i++)
                {
                    if (weapon[i].SlotIndex != anchor.SlotIndex) continue;
                    var part = weapon[i].Weapon.Mechanism;
                    AssemblyVisualComposer.Fits.TryGetWeapon(part.Id, part.Revision, out var fit);
                    float recoil = animation?.Recoil(state.Id, anchor.SlotIndex, seconds, theme) ?? 0;
                    poses.Add(new AssemblyMountPose(anchor.SlotIndex, Mathf.Clamp(angle, fit.MinRadians, fit.MaxRadians),
                        Mathf.Clamp(recoil, -fit.RecoilLimit, 0)));
                    break;
                }
            }
            return AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose(poses));
        }

        internal static Vector2 SegmentOrigin(StructureView structure, int slot, Point2 aim, Vector2 recordedFrom,
            double seconds, NotebookAnimationState animation, NotebookVisualThemeAsset theme)
        {
            Vector2 source = new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f);
            // Only a shot emitted at the structure centre uses its visual barrel.
            // Ricochet continuations keep the preceding victim as their origin.
            if (slot < 0 || (recordedFrom - source).sqrMagnitude > .00000001f) return recordedFrom;
            if (structure.ResolvedEntry != null && structure.ResolvedEntry.Kind != AlbumEntryKind.Legacy)
            {
                var composition = ComposeAssembly(structure, seconds, animation, theme);
                foreach (var anchor in composition.Anchors)
                    if (anchor.SlotIndex == slot) return NotebookAssemblyView.Point(source, anchor.Muzzle, structure.ResolvedEntry.Snapshot);
                return recordedFrom;
            }
            return NotebookTowerComposition.SegmentOrigin(structure, slot, aim, recordedFrom, seconds, animation, theme);
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
            bool modern = state.ResolvedEntry != null && state.ResolvedEntry.Kind != AlbumEntryKind.Legacy;
            Rect artwork = modern ? NotebookAssemblyView.Bounds(p, state.ResolvedEntry.Snapshot)
                : new Rect(p - Vector2.one * .5f, Vector2.one);
            float alpha = building ? .48f : 1;
            Color color = MeshPen.Alpha(TierColor(state.Tier), alpha);
            Color edge = MeshPen.Alpha(NotebookPalette.Navy, alpha);
            if (modern)
            {
                // The complete modern tree is ordered in one dynamic batch, including
                // front hull panels that must cover rotating weapon fragments.
            }
            else if (NotebookTowerComposition.UsesComposition(state.Blueprint))
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
                float radius = modern ? Mathf.Max(.48f, artwork.size.magnitude * .5f) : .48f;
                pen.Ring(p, radius, .035f, MeshPen.Alpha(color, .8f), 24, 1, state.Id, true);
                if (state.WaitingForClearance)
                {
                    pen.Box(p, new Vector2(.54f, .54f), MeshPen.Alpha(NotebookPalette.Paper, .7f), NotebookPalette.Red, .045f, state.Id);
                    pen.Line(p + new Vector2(-.2f, -.2f), p + new Vector2(.2f, .2f), .065f, NotebookPalette.Red);
                }
            }
            float health = state.MaxHealth > 0 ? Mathf.Clamp01(state.Health / (float)state.MaxHealth) : 1;
            if (health < .99f)
            {
                Vector2 left = modern ? new Vector2(artwork.xMin, artwork.yMin - .16f) : p + new Vector2(-.34f, -.52f);
                float width = modern ? Mathf.Max(.68f, artwork.width) : .68f;
                pen.Line(left, left + Vector2.right * width, .055f, MeshPen.Alpha(NotebookPalette.Navy, .17f));
                pen.Line(left, left + Vector2.right * (width * health),
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
            if (state.ResolvedEntry != null && state.ResolvedEntry.Kind != AlbumEntryKind.Legacy)
                ComposeAssembly(state, seconds, animation, theme).Draw(pen, p, state.IsComplete ? 1 : .48f,
                    uniformScale: NotebookAssemblyView.ScaleFor(state.ResolvedEntry.Snapshot));
            else if (!state.IsComplete && NotebookTowerComposition.UsesComposition(state.Blueprint))
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
            bool modern = state.ResolvedEntry != null && state.ResolvedEntry.Kind != AlbumEntryKind.Legacy;
            Rect artwork = modern ? NotebookAssemblyView.Bounds(p, state.ResolvedEntry.Snapshot)
                : new Rect(p - Vector2.one * .5f, Vector2.one);
            float progress = Mathf.Repeat((float)seconds * .45f + state.Id * .13f, 1);
            float angle = progress * Mathf.PI * 2;
            float radius = modern ? Mathf.Max(.48f, artwork.size.magnitude * .5f) : .48f;
            pen.Disc(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, .065f,
                state.WaitingForClearance ? NotebookPalette.Red : color, 8);
            Vector2 left = modern ? new Vector2(artwork.xMin, artwork.yMin - .3f) : p + new Vector2(-.35f, -.6f);
            float width = modern ? Mathf.Max(.7f, artwork.width) : .7f;
            pen.Line(left, left + Vector2.right * width, .06f, MeshPen.Alpha(NotebookPalette.Navy, .16f));
            pen.Line(left, left + Vector2.right * (width * fraction), .06f,
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
            bool showHealth = true, float uniformScale = 1)
        {
            int first = pen.VertexCount;
            float phase = time + enemy.Id * .137f;
            NotebookEnemyMotion motion = animation != null ? animation.Observe(enemy, time, out phase, theme, sampleSeconds, windupSeconds)
                : enemy.WindupRemaining > 0 ? NotebookEnemyMotion.Attack : NotebookEnemyMotion.Idle;
            // Four atlas vertices are cheaper than the retired compact glyph. Preserve the same
            // silhouette and animation for individual enemies and the largest crowds.
            DrawResourceEnemy(pen, enemy.Kind, enemy.DrawingTier, new Vector2(enemy.Position.X, enemy.Position.Y), motion, phase, theme, enemy.VisualScale);
            EnemyMarks(pen, enemy, time, theme);
            if (showHealth) EnemyHealth(pen, enemy);
            float presentationScale = VisualScale(uniformScale);
            if (presentationScale != 1) pen.ScaleFrom(first, new Vector2(enemy.Position.X, enemy.Position.Y), presentationScale);
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
            NotebookEnemyMotion motion, float phase, NotebookVisualThemeAsset theme = null, float visualScale = 1, float uniformScale = 1)
        {
            int first = pen.VertexCount;
            theme = theme ?? NotebookVisualThemeAsset.Default;
            visualScale = VisualScale(visualScale);
            Vector2 silhouette = kind == EnemyKind.Runner ? new Vector2(1.16f, .86f)
                : kind == EnemyKind.Brute ? new Vector2(1.18f, 1.12f)
                : kind == EnemyKind.Bulwark ? new Vector2(1.08f, 1.05f) : Vector2.one;
            string id = NotebookAnimationState.EnemyId(kind, tier, motion);
            var frame = theme?.FindClip(id)?.Sample(Mathf.Max(0, phase));
            bool drawn = DrawResource(pen, frame?.ShapeId ?? id, position + (frame?.Offset ?? Vector2.zero) * visualScale,
                (frame?.Scale ?? 1) * visualScale, frame?.Rotation ?? 0, frame?.Alpha ?? 1, silhouette, theme);
            float presentationScale = VisualScale(uniformScale);
            if (presentationScale != 1) pen.ScaleFrom(first, position, presentationScale);
            return drawn;
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

        public static void Castle(MeshPen pen, Vector2 p, float healthFraction, float seconds = 0, NotebookVisualThemeAsset theme = null, float uniformScale = 1)
            => DrawResource(pen, healthFraction < .6f ? "object.castle.damaged" : "object.castle", p, 3 * uniformScale, theme: theme);
        public static void Tree(MeshPen pen, Vector2 p, float size, Color color, int seed)
            => DrawResource(pen, "terrain.pine", p + Vector2.up * size * .5f, size, opacity: color.a);
        public static void Rock(MeshPen pen, Vector2 p, float size, Color color, int seed)
            => DrawResource(pen, "terrain.rock", p + Vector2.up * size * .25f, size, opacity: color.a);
    }
}
