using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public enum HordeView { None, Close, Overview }

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
        public static Rect Bounds(Vector2 origin, RecipeSnapshot snapshot, float radians = 0)
        {
            var fit = Fit(snapshot);
            var rect = fit.Envelope;
            Vector2 center = origin + NotebookMountLayout.Rotate(rect.center * fit.Scale, radians);
            float c = Mathf.Abs(Mathf.Cos(radians)), s = Mathf.Abs(Mathf.Sin(radians));
            Vector2 size = new Vector2(rect.width * c + rect.height * s, rect.width * s + rect.height * c) * fit.Scale;
            return new Rect(center - size * .5f, size);
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
            NotebookAnimationState animation = null, NotebookVisualThemeAsset theme = null, int firingSlot = -1, Point2 shotAim = default)
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
            float body = NotebookMountLayout.BodyRadians(state);
            foreach (var anchor in rest.Anchors)
            {
                var mount = state.GetMountState(anchor.SlotIndex);
                var aim = anchor.SlotIndex == firingSlot ? shotAim : mount.HasAimPoint ? mount.Aim : default;
                var pivot = NotebookAssemblyView.Point(at, NotebookMountLayout.Rotate(anchor.RotationCenter, body), snapshot);
                Vector2 forward = NotebookTowerComposition.AimDirection(aim, pivot, state.FacingDegrees);
                float angle = Mathf.Atan2(forward.y, forward.x) - Mathf.PI * .5f - body;
                angle = Mathf.Atan2(Mathf.Sin(angle), Mathf.Cos(angle));
                var weapon = snapshot.Recipe.Tower.Mounts;
                for (int i = 0; i < weapon.Count; i++)
                {
                    if (weapon[i].SlotIndex != anchor.SlotIndex) continue;
                    var part = weapon[i].Weapon.Mechanism;
                    AssemblyVisualComposer.Fits.TryGetWeapon(part.Id, part.Revision, out var fit);
                    bool reduced = (theme ?? NotebookVisualThemeAsset.Default).Data.ReducedMotion;
                    float recoil = reduced ? 0 : animation?.Recoil(state.Id, anchor.SlotIndex, seconds, theme) ?? 0;
                    var mechanics = MountMechanics(mount, fit.RecoilLimit > 0 ? -recoil / fit.RecoilLimit : 0,
                        reduced);
                    poses.Add(new AssemblyMountPose(anchor.SlotIndex, Mathf.Clamp(angle, fit.MinRadians, fit.MaxRadians),
                        Mathf.Clamp(recoil, -fit.RecoilLimit, 0), mechanics));
                    break;
                }
            }
            var ranks = state.Modernization;
            var development = new AssemblyDevelopmentPose(ranks.Rank(ModernizationDirection.Power),
                ranks.Rank(ModernizationDirection.Mechanics), ranks.Rank(ModernizationDirection.Range),
                ranks.Rank(ModernizationDirection.Resilience), state.Color, ranks.ColorRank,
                NightNotebookEffectPalette.ColorInk(state.Color, theme));
            return AssemblyVisualComposer.ComposePlaced(snapshot, new AssemblyVisualPose(poses, development: development), body);
        }

        internal static AssemblyMechanicsPose MountMechanics(WeaponMountView mount, float fire, bool reducedMotion)
            => reducedMotion ? default : new AssemblyMechanicsPose(mount.Cycle != null && mount.IsReloading,
                mount.Cycle != null ? (float)(1 - mount.CycleRemainingWork) : 0, fire);

        internal static Vector2 SegmentOrigin(StructureView structure, int slot, Point2 aim, Vector2 recordedFrom,
            double seconds, NotebookAnimationState animation, NotebookVisualThemeAsset theme)
        {
            Vector2 source = new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f);
            // Only a shot emitted at the structure centre uses its visual barrel.
            // Ricochet continuations keep the preceding victim as their origin.
            if (slot < 0 || (recordedFrom - source).sqrMagnitude > .00000001f) return recordedFrom;
            if (structure.ResolvedEntry != null && structure.ResolvedEntry.Kind != AlbumEntryKind.Legacy)
            {
                // Use the aim at launch, even if the mount is tracking another target now.
                var composition = ComposeAssembly(structure, seconds, animation, theme, slot, aim);
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
            float body = NotebookMountLayout.BodyRadians(state);
            Rect artwork = modern ? NotebookAssemblyView.Bounds(p, state.ResolvedEntry.Snapshot, body)
                : new Rect(p - Vector2.one * .5f, Vector2.one);
            float alpha = building ? .48f : 1;
            Color color = MeshPen.Alpha(TierColor(state.Tier), alpha);
            Color edge = MeshPen.Alpha(NotebookPalette.Navy, alpha);
            if (state.Kind == StructureKind.Wall && !building && wallMask != 0)
                WallRails(pen, p, wallMask);
            if (modern)
            {
                // The parts themselves stay in the shared dynamic composition. Contact
                // shadows and joining rails sit on the page below all the moving parts.
                if (state.ResolvedEntry.Support != null && !building)
                    pen.Disc(new Vector2(p.x + .06f, artwork.yMin + .11f), artwork.width * .44f,
                        new Color32(4, 15, 12, 125), 12, .19f);
            }
            else if (NotebookTowerComposition.UsesComposition(state.Blueprint))
                NotebookTowerComposition.DrawStatic(pen, state.Blueprint, p, state.Tier, state.Color, alpha, theme, body);
            else
            {
                string id = state.Kind == StructureKind.Wall ? "object.wall." + wallMask
                    : state.Kind == StructureKind.Spikes ? "object.spikes"
                    : state.Kind == StructureKind.TarPit ? "object.tar"
                    : state.Kind == StructureKind.RepairPost ? "object.repair"
                    : state.Kind == StructureKind.Generator ? "object.generator"
                    : state.Kind == StructureKind.Beacon ? "object.beacon"
                    : state.Kind == StructureKind.Prism ? "object.prism" : "chassis.standard";
                DrawResource(pen, id, p, radians: body, opacity: alpha, theme: theme);
            }
            if (state.Blueprint?.Reinforcement != null) Reinforcement(pen, p, state.Blueprint.Reinforcement, edge, body);
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

        static void WallRails(MeshPen pen, Vector2 center, int mask)
        {
            // Adjacent occupied cells meet exactly at their shared border.
            var foot = center + new Vector2(0, -.13f);
            for (int edge = 0; edge < 4; edge++)
            {
                if ((mask & (1 << edge)) == 0) continue;
                Vector2 axis = edge == 0 ? Vector2.up : edge == 1 ? Vector2.right : edge == 2 ? Vector2.down : Vector2.left;
                var join = foot + axis*.5f;
                pen.Line(foot, join, .16f, NightNotebookArt.Paper);
                pen.Line(foot, join, .085f, NightNotebookArt.Body);
            }
        }

        static void Reinforcement(MeshPen pen,Vector2 center,ArmorDistribution armor,Color color,float body)
        {
            for(int sector=0;sector<4;sector++)
            {
                int count=armor.Points[sector];
                for(int line=0;line<count;line++)
                {
                    float radius=.55f+line*.014f;Vector2 previous=default;
                    for(int step=0;step<=8;step++)
                    {
                        float angle=((sector+armor.FacingQuarterTurns)*90-37+74*step/8)*Mathf.Deg2Rad-body;
                        var next=center+new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*radius;
                        if(step>0)pen.Line(previous,next,.012f,color);previous=next;
                    }
                }
            }
            var front=ArmorProbe.Direction(armor.FacingQuarterTurns);var d=NotebookMountLayout.Rotate(new Vector2(front.X,front.Y),body);var tip=center+d*.49f;
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
            {
                var mechanics = animation?.StructureMechanics(state.Id, seconds,
                    (theme ?? NotebookVisualThemeAsset.Default).Data.ReducedMotion) ?? default;
                ComposeAssembly(state, seconds, animation, theme).Draw(pen, p,
                    uniformScale: NotebookAssemblyView.ScaleFor(state.ResolvedEntry.Snapshot),
                    mechanics: mechanics, construction: state.IsComplete ? 1 : fraction);
                if (mechanics.Activity > 0 && state.ResolvedEntry.Support?.Kind == AssemblyStructureFamily.Generator)
                {
                    // Work pulses are launched only by an actual payment event.
                    var receipt = p + new Vector2(.18f, .42f + (1-mechanics.Activity)*.25f);
                    var paidInk = MeshPen.Alpha(NightNotebookArt.Accent, mechanics.Activity);
                    pen.Line(receipt + new Vector2(-.055f, -.035f), receipt + new Vector2(.055f, .035f), .025f, paidInk);
                    pen.Line(receipt + new Vector2(-.055f, .035f), receipt + new Vector2(.055f, .105f), .025f, paidInk);
                }
            }
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
                    Vector2 pivot = p + NotebookMountLayout.Rotate(new Vector2(0, .04f), NotebookMountLayout.BodyRadians(state));
                    var primary = state.GetMountState(0);
                    Vector2 forward = NotebookTowerComposition.AimDirection(primary.HasAimPoint ? primary.Aim : default, pivot, state.FacingDegrees);
                    pivot += forward * (animation?.Recoil(state.Id, 0, seconds, theme) ?? 0);
                    DrawResource(pen, weapon, pivot, radians: Mathf.Atan2(forward.y, forward.x) - Mathf.PI * .5f,
                        opacity: state.IsComplete ? 1 : .48f, theme: theme);
                }
            }
            DrawModernization(pen, state, theme);
            if (state.IsComplete) return;
            bool modern = state.ResolvedEntry != null && state.ResolvedEntry.Kind != AlbumEntryKind.Legacy;
            Rect artwork = modern ? NotebookAssemblyView.Bounds(p, state.ResolvedEntry.Snapshot, NotebookMountLayout.BodyRadians(state))
                : new Rect(p - Vector2.one * .5f, Vector2.one);
            // The nib follows actual completed work; waiting for clearance cannot
            // keep pretending to assemble a structure through an unrelated orbit.
            Vector2 nib = new Vector2(Mathf.Lerp(artwork.xMin, artwork.xMax, fraction), artwork.yMin - .3f);
            pen.Line(nib + new Vector2(-.07f,-.11f), nib + new Vector2(.04f,.06f), .035f,
                state.WaitingForClearance ? NotebookPalette.Red : NightNotebookArt.Accent);
            Vector2 left = modern ? new Vector2(artwork.xMin, artwork.yMin - .3f) : p + new Vector2(-.35f, -.6f);
            float width = modern ? Mathf.Max(.7f, artwork.width) : .7f;
            pen.Line(left, left + Vector2.right * width, .06f, MeshPen.Alpha(NotebookPalette.Navy, .16f));
            pen.Line(left, left + Vector2.right * (width * fraction), .06f,
                state.WaitingForClearance ? NotebookPalette.Red : color);
        }

        /// <summary>State-only marks, wholly inside the occupied cell. Never alter certified art or weapon poses.</summary>
        internal static void DrawModernization(MeshPen pen, StructureView state, NotebookVisualThemeAsset theme = null)
        {
            var work = state.Modernization;
            if (!state.IsComplete || (work.StepsUsed == 0 && work.ColorRank == 0 && !work.IsWorking)) return;
            var p = new Vector2(state.Cell.X + .5f, state.Cell.Y + .5f);
            // Four fixed columns read left to right like the direction controls: power, mechanics, range, resilience.
            // Position, shape and tick count carry information even with no movement or color perception.
            pen.Box(p + new Vector2(0, -.29f), new Vector2(.78f, .24f),
                MeshPen.Alpha(NotebookPalette.Paper, .94f), MeshPen.Alpha(NotebookPalette.Navy, .6f), .015f, state.Id);
            for (int direction = 0; direction < 4; direction++)
            {
                var at = p + new Vector2(-.29f + direction * .19f, -.29f);
                var rank = work.Rank((ModernizationDirection)direction);
                var ink = rank > 0 ? NotebookPalette.Navy : MeshPen.Alpha(NotebookPalette.Navy, .3f);
                if (direction == 0)
                {
                    pen.Line(at + new Vector2(-.035f, .015f), at + new Vector2(0, .065f), .018f, ink);
                    pen.Line(at + new Vector2(0, .065f), at + new Vector2(.035f, .015f), .018f, ink);
                }
                else if (direction == 1)
                {
                    pen.Line(at + new Vector2(-.035f, .02f), at + new Vector2(.035f, .02f), .018f, ink);
                    pen.Line(at + new Vector2(-.035f, .06f), at + new Vector2(.035f, .06f), .018f, ink);
                }
                else if (direction == 2)
                {
                    pen.Line(at + new Vector2(-.04f, .02f), at + new Vector2(.04f, .06f), .018f, ink);
                    pen.Line(at + new Vector2(.005f, .07f), at + new Vector2(.04f, .06f), .018f, ink);
                    pen.Line(at + new Vector2(.04f, .06f), at + new Vector2(.03f, .025f), .018f, ink);
                }
                else
                {
                    pen.Line(at + new Vector2(-.035f, .065f), at + new Vector2(0, .015f), .018f, ink);
                    pen.Line(at + new Vector2(0, .015f), at + new Vector2(.035f, .065f), .018f, ink);
                }
                for (int tick = 0; tick < rank; tick++)
                {
                    var from = at + new Vector2(-.04f + tick * .04f, -.065f);
                    pen.Line(from, from + Vector2.up * .045f, .02f, ink);
                }
                if (work.IsWorking && work.PendingDirection == (ModernizationDirection)direction)
                    pen.Line(at + new Vector2(-.06f, .09f), at + new Vector2(.06f, .09f), .02f, NightNotebookArt.Accent);
            }
            if (state.Color != InkColor.None)
            {
                var at = p + new Vector2(.32f, -.08f);
                pen.Disc(at, .065f, InkTone(state.Color), 10);
                for (int tick = 0; tick < work.ColorRank; tick++)
                {
                    var from = at + new Vector2(-.04f + tick * .04f, -.025f);
                    pen.Line(from, from + Vector2.up * .05f, .014f, NotebookPalette.Paper);
                }
            }
            if (!work.IsWorking) return;
            float progress = 1 - Mathf.Clamp01(work.WorkRemaining / Mathf.Max(.001f, work.WorkDuration));
            var left = p + new Vector2(-.35f, -.445f);
            pen.Line(left, left + Vector2.right * .7f, .04f, MeshPen.Alpha(NotebookPalette.Navy, .22f));
            pen.Line(left, left + Vector2.right * (.7f * progress), .04f,
                work.PendingColor != InkColor.None ? InkTone(work.PendingColor) : NightNotebookArt.Accent);
            // A stationary diagonal nib identifies unfinished work even at exactly zero progress.
            var nib = left + Vector2.right * (.7f * progress);
            pen.Line(nib + new Vector2(-.02f, -.025f), nib + new Vector2(.02f, .025f), .018f, NotebookPalette.Navy);
        }

        public static void Enemy(MeshPen pen, EnemyView enemy, float time, bool compact = false,
            NotebookAnimationState animation = null, NotebookVisualThemeAsset theme = null, double sampleSeconds = double.NaN, float windupSeconds = 0,
            bool showHealth = true, float uniformScale = 1, HordeView horde = HordeView.None, EnemyFamily family = EnemyFamily.Ink)
        {
            int first = pen.VertexCount;
            float phase = time + enemy.Id * .137f;
            NotebookEnemyMotion motion = animation != null ? animation.Observe(enemy, time, out phase, theme, sampleSeconds, windupSeconds, family)
                : enemy.WindupRemaining > 0 ? NotebookEnemyMotion.Attack : NotebookEnemyMotion.Idle;
            // Large crowds keep the same role and clip phase through a cached native detail level.
            var position = new Vector2(enemy.Position.X, enemy.Position.Y);
            bool distantDoodle = enemy.Boss == BossRank.None && horde == HordeView.Overview && enemy.Kind == EnemyKind.Doodle && enemy.DrawingTier == DrawingTier.Sketch &&
                enemy.VisualScale * uniformScale <= 24 * pen.WorldUnitsPerPixel && theme != null && theme.UsesNativeCrowdGeometry;
            if (distantDoodle)
                InkSwarmEnemyArt.DrawDistantDoodle(pen, position, enemy.VisualScale, motion, phase, theme, family);
            else
                DrawResourceEnemy(pen, enemy.Kind, enemy.DrawingTier, position, motion, phase, theme, enemy.VisualScale, compact: compact, family: family, boss: enemy.Boss);
            EnemyMarks(pen, enemy, time, theme);
            if (showHealth)
            {
                bool grunt = enemy.Boss == BossRank.None && horde != HordeView.None &&
                    (enemy.Kind == EnemyKind.Doodle || enemy.Kind == EnemyKind.Runner || enemy.Kind == EnemyKind.Gnawer);
                bool wounded = enemy.Health > 0 && enemy.Health < enemy.MaxHealth;
                if ((!grunt || (horde == HordeView.Close && wounded)) && pen.TryGetBounds(first, out var silhouette))
                {
                    if (grunt) HordeWoundBar(pen, enemy, silhouette, theme ?? NotebookVisualThemeAsset.Default);
                    else EnemyHealth(pen, enemy, silhouette);
                }
            }
            float presentationScale = VisualScale(uniformScale);
            if (presentationScale != 1) pen.ScaleFrom(first, new Vector2(enemy.Position.X, enemy.Position.Y), presentationScale);
        }
        internal static float VisualScale(float value) => float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? 1 : value;
        internal static float EnemyHealthRatio(float health, float maximum)
            => maximum > 0 && !float.IsNaN(health) && !float.IsNaN(maximum) ? Mathf.Clamp01(health / maximum) : 0;
        static void HordeWoundBar(MeshPen pen, EnemyView enemy, Rect silhouette, NotebookVisualThemeAsset theme)
        {
            float scale = VisualScale(enemy.VisualScale);
            float width = Mathf.Min(silhouette.width, .46f * scale);
            float height = Mathf.Max(.04f * scale, 2 * pen.WorldUnitsPerPixel);
            float x = silhouette.center.x - width * .5f, y = silhouette.yMax + .07f * scale;
            float ratio = EnemyHealthRatio(enemy.Health, enemy.MaxHealth);
            pen.Rect(x, y, width, height, theme.Color(NotebookInkChannel.Surface));
            pen.Rect(x, y, width * ratio, height, theme.Color(ratio > .35f ? NotebookInkChannel.Sketch : NotebookInkChannel.Danger));
        }
        static void EnemyHealth(MeshPen pen, EnemyView enemy, Rect silhouette)
        {
            float scale = VisualScale(enemy.VisualScale), width = Mathf.Max(.72f, .72f * scale);
            float height = Mathf.Max(.24f, pen.WorldUnitsPerPixel * 2);
            // Solid paper backing and a dark border remain readable above both terrain and ink art.
            // Follow this animated body's real extent, including status/attack marks, rather
            // than the retired glyph's fixed height. Earlier actors share the same batch.
            Vector2 at = new Vector2(silhouette.center.x, silhouette.yMax + .13f * scale);
            pen.Rect(at.x - width * .5f - .06f, at.y - .06f, width + .12f, height + .12f, NotebookPalette.Paper);
            pen.Rect(at.x - width * .5f - .025f, at.y - .025f, width + .05f, height + .05f, NotebookPalette.Navy);
            pen.Rect(at.x - width * .5f, at.y, width, height, MeshPen.Alpha(NotebookPalette.Navy, .22f));
            float ratio = EnemyHealthRatio(enemy.Health, enemy.MaxHealth);
            if (ratio > 0) pen.Rect(at.x - width * .5f, at.y, width * ratio, height,
                ratio > .35f ? NotebookPalette.Green : NotebookPalette.Red);
            if (enemy.Boss != BossRank.None)
                for (int i = 1; i < 4; i++)
                    pen.Rect(at.x + width * (i * .25f - .5f), at.y, Mathf.Max(.018f, pen.WorldUnitsPerPixel), height, NotebookPalette.Navy);
        }
        internal static bool DrawResourceEnemy(MeshPen pen, EnemyKind kind, DrawingTier tier, Vector2 position,
            NotebookEnemyMotion motion, float phase, NotebookVisualThemeAsset theme = null, float visualScale = 1, float uniformScale = 1,
            bool compact = false, EnemyFamily family = EnemyFamily.Ink, BossRank boss = BossRank.None)
        {
            int first = pen.VertexCount;
            theme = theme ?? NotebookVisualThemeAsset.Default;
            visualScale = VisualScale(visualScale);
            Vector2 silhouette = boss != BossRank.None ? Vector2.one : kind == EnemyKind.Runner ? new Vector2(1.16f, .86f)
                : kind == EnemyKind.Brute ? new Vector2(1.18f, 1.12f)
                : kind == EnemyKind.Bulwark ? new Vector2(1.08f, 1.05f) : Vector2.one;
            string id = NotebookAnimationState.EnemyId(kind, tier, motion, family, boss);
            var frame = theme?.FindClip(id)?.Sample(Mathf.Max(0, phase));
            var compactShape = compact && boss == BossRank.None ? theme.FindCompactEnemy(frame?.ShapeId ?? id) : null;
            Vector2 at = position + (frame?.Offset ?? Vector2.zero) * visualScale;
            float scale = (frame?.Scale ?? 1) * visualScale;
            bool drawn = compactShape != null
                ? NotebookArtRenderer.Draw(pen, compactShape, at, scale, frame?.Rotation ?? 0,
                    opacity: frame?.Alpha ?? 1, localScale: silhouette, theme: theme)
                : DrawResource(pen, frame?.ShapeId ?? id, at, scale, frame?.Rotation ?? 0, frame?.Alpha ?? 1, silhouette, theme);
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
            if (enemy.WindupRemaining > 0)
            {
                float pulse = (theme ?? NotebookVisualThemeAsset.Default).Data.ReducedMotion ? 1 : .6f + .4f * Mathf.Sin(time * 18);
                pen.Ring(p, .54f, .055f + pulse * .035f, MeshPen.Alpha(NotebookPalette.Red, .6f + .4f * pulse), 16);
                pen.Line(p + new Vector2(0, .56f), p + new Vector2(0, .85f), .095f, NotebookPalette.Red);
                pen.Disc(p + new Vector2(0, .48f), .05f, NotebookPalette.Red, 6);
            }
            pen.ScaleFrom(first, p, VisualScale(enemy.VisualScale));
        }

        public static void Castle(MeshPen pen, Vector2 p, float healthFraction, float seconds = 0, NotebookVisualThemeAsset theme = null, float uniformScale = 1)
            => DrawResource(pen, healthFraction <= 0 ? "object.castle.ruined" : healthFraction < .6f ? "object.castle.damaged" : "object.castle", p, 3 * uniformScale, theme: theme);
        public static void Tree(MeshPen pen, Vector2 p, float size, Color color, int seed)
            => DrawResource(pen, "terrain.pine", p + Vector2.up * size * .5f, size, opacity: color.a);
        public static void Rock(MeshPen pen, Vector2 p, float size, Color color, int seed)
            => DrawResource(pen, "terrain.rock", p + Vector2.up * size * .25f, size, opacity: color.a);
    }
}
