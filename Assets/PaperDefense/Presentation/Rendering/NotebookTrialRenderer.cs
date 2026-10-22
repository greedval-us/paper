using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    internal static class NotebookTrialRenderer
    {
        internal static Rect CombatBounds(TrialReport report, Rect overview)
        {
            if (report == null || report.Frames.Count == 0) return overview;
            // The close-up is a camera over one real engagement. It never changes the
            // recording, the actors' positions or the shared whole-assembly view transform.
            if (TryFirstWeaponAction(report, false, out var source, out var action, out var actionFrame) ||
                TryFirstWeaponAction(report, true, out source, out action, out actionFrame))
            {
                var bounds = SourceBounds(source);
                var from = DoodleGlyphs.SegmentOrigin(source, action.MountSlotIndex, action.To, Point(action.From),
                    action.SimulationSeconds, null, NotebookVisualThemeAsset.Default);
                Include(ref bounds, from); Include(ref bounds, Point(action.From)); Include(ref bounds, Point(action.To));
                IncludeTargetVisual(ref bounds, report, actionFrame, source, action);
                if (action.Kind == SimulationEventKind.ProjectileLaunched) IncludeProjectile(ref bounds, from, action);
                return CloseUp(bounds);
            }
            if (TrySupportFocus(report, out var supportBounds)) return CloseUp(supportBounds);
            foreach (var frame in report.Frames)
                foreach (var tower in frame.Towers)
                {
                    if (tower.ResolvedEntry?.Snapshot == null || tower.Health <= 0) continue;
                    return CloseUp(SourceBounds(tower));
                }
            return overview;
        }

        static Vector2 Center(StructureView tower) => new Vector2(tower.Cell.X + .5f, tower.Cell.Y + .5f);
        static Vector2 Point(Point2 point) => new Vector2(point.X, point.Y);
        static Rect SourceBounds(StructureView tower) => tower.ResolvedEntry?.Snapshot != null
            ? NotebookAssemblyView.Bounds(Center(tower), tower.ResolvedEntry.Snapshot)
            : new Rect(Center(tower) - Vector2.one * .6f, Vector2.one * 1.2f);
        static void Include(ref Rect bounds, Vector2 point) => bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, point.x),
            Mathf.Min(bounds.yMin, point.y), Mathf.Max(bounds.xMax, point.x), Mathf.Max(bounds.yMax, point.y));
        static void Include(ref Rect bounds, Rect other) { Include(ref bounds, other.min); Include(ref bounds, other.max); }
        static Rect CloseUp(Rect bounds) => Rect.MinMaxRect(bounds.xMin - .8f, bounds.yMin - .6f, bounds.xMax + .8f, bounds.yMax + .9f);

        static void IncludeTargetVisual(ref Rect bounds, TrialReport report, TrialFrame actionFrame, StructureView source, SimulationEvent action)
        {
            int targetId = source.GetMountTargetId(action.MountSlotIndex);
            EnemyView target = default; TrialFrame targetFrame = null;
            // Hits can remove a victim before the event frame is recorded. Preserve its
            // latest actual live view by ID; the endpoint is the fallback for area shots.
            foreach (var frame in report.Frames)
            {
                if (frame.Seconds > actionFrame.Seconds) continue;
                foreach (var enemy in frame.Enemies)
                    if (enemy.Health > 0 && targetId > 0 && enemy.Id == targetId && (targetFrame == null || frame.Seconds > targetFrame.Seconds))
                    { target = enemy; targetFrame = frame; }
            }
            if (targetFrame == null)
            {
                float nearest = float.PositiveInfinity;
                foreach (var frame in report.Frames)
                {
                    if (frame.Seconds > actionFrame.Seconds) continue;
                    foreach (var enemy in frame.Enemies)
                    {
                        if (enemy.Health <= 0) continue;
                        float distance = (Point(enemy.Position) - Point(action.To)).sqrMagnitude;
                        if (distance > nearest || distance == nearest && targetFrame != null &&
                            (frame.Seconds < targetFrame.Seconds || frame.Seconds == targetFrame.Seconds && enemy.Id >= target.Id)) continue;
                        target = enemy; targetFrame = frame; nearest = distance;
                    }
                }
            }
            if (targetFrame == null) return;
            var pen = EnemyVisual(target, targetFrame.Seconds);
            var theme = NotebookVisualThemeAsset.Default;
            float scale = NotebookEnemyView.ScaleFor(true);
            foreach (var death in actionFrame.Events)
                if (death.Kind == SimulationEventKind.EnemyDied && death.EntityId == target.Id)
                    DoodleGlyphs.DrawResourceEnemy(pen, death.EnemyKind, death.EnemyDrawingTier, Point(death.To),
                        NotebookEnemyMotion.Death, 0, theme, death.EnemyVisualScale, scale);
            if (pen.TryGetBounds(out var visual)) Include(ref bounds, visual);
        }

        static MeshPen EnemyVisual(EnemyView enemy, double seconds)
        {
            var pen = new MeshPen(null);
            DoodleGlyphs.Enemy(pen, enemy, (float)seconds, theme: NotebookVisualThemeAsset.Default,
                sampleSeconds: seconds, uniformScale: NotebookEnemyView.ScaleFor(true));
            return pen;
        }

        static void IncludeEnemyVisual(ref Rect bounds, EnemyView enemy, double seconds)
        {
            if (EnemyVisual(enemy, seconds).TryGetBounds(out var visual)) Include(ref bounds, visual);
        }

        static bool TryFirstWeaponAction(TrialReport report, bool includeWindup, out StructureView source, out SimulationEvent action, out TrialFrame actionFrame)
        {
            source = default; action = default; actionFrame = null; bool found = false;
            foreach (var frame in report.Frames)
                foreach (var candidate in frame.Events)
                {
                    bool eligible = includeWindup ? candidate.Kind == SimulationEventKind.WeaponActionStarted :
                        candidate.Kind == SimulationEventKind.Shot || candidate.Kind == SimulationEventKind.ProjectileLaunched;
                    if (!eligible || found && (candidate.SimulationSeconds > action.SimulationSeconds ||
                        candidate.SimulationSeconds == action.SimulationSeconds && candidate.Sequence >= action.Sequence)) continue;
                    foreach (var tower in frame.Towers)
                    {
                        if (tower.Id != candidate.SourceStructureId || tower.ResolvedEntry?.Snapshot?.Recipe.Tower == null) continue;
                        source = tower; action = candidate; actionFrame = frame; found = true; break;
                    }
                }
            return found;
        }

        static bool ActiveSupport(StructureView tower, AssemblyStructureFamily family) => tower.Health > 0 && tower.IsComplete &&
            tower.ResolvedEntry?.Support?.Kind == family;
        static bool TrySupportFocus(TrialReport report, out Rect bounds)
        {
            StructureView first = default; bool hasSupport = false;
            foreach (var frame in report.Frames)
            {
                foreach (var tower in frame.Towers)
                    if (tower.ResolvedEntry?.Support != null && tower.Health > 0 && tower.IsComplete)
                    { first = tower; hasSupport = true; break; }
                if (hasSupport) break;
            }
            bounds = default;
            if (!hasSupport) return false;
            var family = first.ResolvedEntry.Support.Kind;
            if (family == AssemblyStructureFamily.Repair)
            {
                for (int index = 1; index < report.Frames.Count; index++)
                    foreach (var target in report.Frames[index].Towers)
                    {
                        bool repaired = false;
                        foreach (var previous in report.Frames[index - 1].Towers)
                            if (previous.Id == target.Id && target.Health > previous.Health) { repaired = true; break; }
                        if (!repaired) continue;
                        foreach (var support in report.Frames[index].Towers)
                            if (ActiveSupport(support, family) && support.Id != target.Id &&
                                Vector2.Distance(Center(support), Center(target)) <= support.ResolvedEntry.Support.Radius)
                            { bounds = SourceBounds(support); Include(ref bounds, SourceBounds(target)); return true; }
                    }
            }
            else if (family == AssemblyStructureFamily.Slow)
            {
                foreach (var frame in report.Frames)
                    foreach (var support in frame.Towers)
                    {
                        if (!ActiveSupport(support, family)) continue;
                        foreach (var enemy in frame.Enemies)
                            if (enemy.Health > 0 && Vector2.Distance(Center(support), Point(enemy.Position)) <= support.ResolvedEntry.Support.Radius)
                            { bounds = SourceBounds(support); Include(ref bounds, Point(enemy.Position)); IncludeEnemyVisual(ref bounds, enemy, frame.Seconds); return true; }
                    }
            }
            else if (family == AssemblyStructureFamily.Generator)
            {
                foreach (var frame in report.Frames)
                    foreach (var support in frame.Towers)
                        if (support.Id == first.Id && support.GeneratorPaidWaves > first.GeneratorPaidWaves)
                        { bounds = SourceBounds(support); return true; }
            }
            bounds = SourceBounds(first);
            // A wall can be bypassed without an EnemyAttack. Its closest real recorded
            // approach is still useful, and avoids inventing a contact at the spawn point.
            float distance = float.PositiveInfinity, seconds = float.PositiveInfinity; int enemyId = int.MaxValue;
            foreach (var frame in report.Frames)
                foreach (var enemy in frame.Enemies)
                {
                    if (enemy.Health <= 0) continue;
                    float next = (Point(enemy.Position) - Center(first)).sqrMagnitude;
                    if (next > distance || next == distance && (frame.Seconds > seconds || frame.Seconds == seconds && enemy.Id >= enemyId)) continue;
                    distance = next; seconds = frame.Seconds; enemyId = enemy.Id;
                }
            if (!float.IsInfinity(distance))
                foreach (var frame in report.Frames)
                    if (frame.Seconds == seconds)
                        foreach (var enemy in frame.Enemies)
                            if (enemy.Id == enemyId) { Include(ref bounds, Point(enemy.Position)); IncludeEnemyVisual(ref bounds, enemy, frame.Seconds); return true; }
            return true;
        }

        static bool SpatialEvent(SimulationEventKind kind)
        {
            switch (kind)
            {
                case SimulationEventKind.Shot: case SimulationEventKind.ProjectileLaunched: case SimulationEventKind.ProjectileImpact:
                case SimulationEventKind.WeaponActionStarted: case SimulationEventKind.EnemyAttack: case SimulationEventKind.EnemyHit: case SimulationEventKind.EnemyDied:
                case SimulationEventKind.EnemyGlobLaunched: case SimulationEventKind.EnemyGlobImpact:
                case SimulationEventKind.StructureCompleted: case SimulationEventKind.StructureDestroyed: case SimulationEventKind.StructureErased:
                case SimulationEventKind.BlotSpawned: case SimulationEventKind.BlotExpired: case SimulationEventKind.BlotCleared: case SimulationEventKind.BlotAbsorbed:
                case SimulationEventKind.BaseHit: case SimulationEventKind.InkWellHit: case SimulationEventKind.InkWellActivated:
                    return true;
                default: return false;
            }
        }
        static void IncludeProjectile(ref Rect bounds, Vector2 from, SimulationEvent action)
        {
            var to = Point(action.To);
            float arc = Mathf.Min(3, Vector2.Distance(from, to) * .22f);
            Include(ref bounds, new Vector2((from.x + to.x) * .5f, Mathf.Max(from.y, to.y) + arc));
            if (action.Radius > 0) { Include(ref bounds, to - Vector2.one * action.Radius); Include(ref bounds, to + Vector2.one * action.Radius); }
        }

        internal static Rect RecordingBounds(TrialReport report)
        {
            if (report == null || report.Frames.Count == 0) return new Rect(8, 8, 42, 28);
            float left = float.PositiveInfinity, bottom = left, right = float.NegativeInfinity, top = right;
            void Include(float x, float y)
            { left = Mathf.Min(left, x); bottom = Mathf.Min(bottom, y); right = Mathf.Max(right, x); top = Mathf.Max(top, y); }
            foreach (var frame in report.Frames)
            {
                foreach (var tower in frame.Towers)
                {
                    var at = new Vector2(tower.Cell.X + .5f, tower.Cell.Y + .5f);
                    Include(at.x, at.y);
                    var snapshot = tower.ResolvedEntry?.Snapshot;
                    if (snapshot == null) continue;
                    var silhouette = NotebookAssemblyView.Bounds(at, snapshot);
                    Include(silhouette.xMin, silhouette.yMin); Include(silhouette.xMax, silhouette.yMax);
                }
                foreach (var enemy in frame.Enemies)
                    if (enemy.Health > 0) Include(enemy.Position.X, enemy.Position.Y);
                foreach (var blot in frame.Blots) Include(blot.Cell.X + .5f, blot.Cell.Y + .5f);
                foreach (var action in frame.Events)
                {
                    if (!SpatialEvent(action.Kind)) continue;
                    Include(action.From.X, action.From.Y); Include(action.To.X, action.To.Y);
                    if (action.Kind == SimulationEventKind.ProjectileLaunched)
                    {
                        float arc = Mathf.Min(3, Vector2.Distance(Point(action.From), Point(action.To)) * .22f);
                        Include((action.From.X + action.To.X) * .5f, Mathf.Max(action.From.Y, action.To.Y) + arc);
                    }
                    if (action.Radius > 0 && (action.Kind == SimulationEventKind.ProjectileImpact || action.Kind == SimulationEventKind.ProjectileLaunched || action.Kind == SimulationEventKind.EnemyGlobImpact))
                    { Include(action.To.X - action.Radius, action.To.Y - action.Radius); Include(action.To.X + action.Radius, action.To.Y + action.Radius); }
                }
                foreach (var shot in frame.Shots)
                { Include(shot.From.X, shot.From.Y); Include(shot.To.X, shot.To.Y); }
            }
            if (float.IsInfinity(left)) return new Rect(8, 8, 42, 28);
            // This stable envelope includes the whole recording, the tallest projectile arc,
            // moving weapon silhouettes and health bars. Playback never pans or changes scale.
            return Rect.MinMaxRect(left - 2, bottom - 2, right + 2, top + 3);
        }

        internal static void Draw(MeshPen pen, NotebookTrialPlayback playback, Rect? framing = null)
        {
            pen.Clear();
            var theme = NotebookVisualThemeAsset.Default;
            var frame = playback.Report.Frames[playback.FrameIndex];
            float seconds = (float)playback.Seconds;
            var bounds = framing ?? new Rect(8, 8, 42, 28);
            int grid = framing.HasValue ? 2 : 4;
            for (int y = Mathf.CeilToInt(bounds.yMin / grid) * grid; y <= bounds.yMax; y += grid)
                pen.Line(new Vector2(bounds.xMin, y), new Vector2(bounds.xMax, y), .04f, MeshPen.Alpha(NotebookPalette.Blue, .12f));
            for (int x = Mathf.CeilToInt(bounds.xMin / grid) * grid; x <= bounds.xMax; x += grid)
                pen.Line(new Vector2(x, bounds.yMin), new Vector2(x, bounds.yMax), .04f, MeshPen.Alpha(NotebookPalette.Blue, .12f));
            if (!framing.HasValue)
                pen.Line(new Vector2(48, 8), new Vector2(48, 36), .09f, MeshPen.Alpha(NotebookPalette.Red, .65f));
            foreach (var blot in frame.Blots)
            {
                if (playback.IsBlotEnded(blot.Cell)) continue;
                Vector2 at = new Vector2(blot.Cell.X + .5f, blot.Cell.Y + .5f);
                DoodleGlyphs.DrawResource(pen, "object.blot", at, theme: theme);
            }
            foreach (var tower in frame.Towers)
            {
                if (tower.Health <= 0 || playback.IsStructureEnded(tower.Id)) continue;
                // A combat close-up may omit remote copies. Keep their whole silhouettes
                // out of the camera instead of leaving a severed base along its edge.
                // RecordingBounds includes every copy, so the overview still draws them all.
                var silhouette = SourceBounds(tower);
                if (framing.HasValue && (silhouette.xMin < bounds.xMin || silhouette.xMax > bounds.xMax ||
                    silhouette.yMin < bounds.yMin || silhouette.yMax > bounds.yMax)) continue;
                DoodleGlyphs.StructureBase(pen, tower, theme);
                DoodleGlyphs.StructureDynamic(pen, tower, 1, seconds, playback.Animation, theme);
            }
            DrawEnemies(pen, playback, theme, framing.HasValue);
            DrawProjectiles(pen, playback, theme);
            DrawEffects(pen, playback, theme);
        }
        internal static void DrawEnemies(MeshPen pen, NotebookTrialPlayback playback, NotebookVisualThemeAsset theme = null, bool modernPresentation = false)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            var frame = playback.Report.Frames[playback.FrameIndex];
            float seconds = (float)playback.Seconds;
            float enemyScale = NotebookEnemyView.ScaleFor(modernPresentation);
            foreach (var enemy in frame.Enemies)
                if (enemy.Health > 0 && !playback.IsEnemyEnded(enemy.Id))
                    DoodleGlyphs.Enemy(pen, enemy, seconds, false, playback.Animation, theme, frame.Seconds, uniformScale: enemyScale);
            foreach (var death in playback.Animation.Deaths)
                DoodleGlyphs.DrawResourceEnemy(pen, death.Kind, death.Tier, death.Position,
                    NotebookEnemyMotion.Death, (float)(playback.Seconds - death.Born), theme, death.VisualScale, enemyScale);
        }
        internal static void DrawProjectiles(MeshPen pen, NotebookTrialPlayback playback, NotebookVisualThemeAsset theme = null)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            var frame = playback.Report.Frames[playback.FrameIndex];
            float seconds = (float)playback.Seconds;
            foreach (var launch in playback.Projectiles)
            {
                if (playback.IsProjectileEnded(launch.EntityId, launch.Kind == SimulationEventKind.EnemyGlobLaunched)) continue;
                float progress = Mathf.Clamp01((seconds - (float)launch.SimulationSeconds) / Mathf.Max(.001f, launch.TravelSeconds));
                Vector2 from = Muzzle(frame, launch, playback, theme), to = new Vector2(launch.To.X, launch.To.Y);
                bool mortar = launch.Kind == SimulationEventKind.ProjectileLaunched;
                Vector2 point = Vector2.Lerp(from, to, progress) + (mortar ? Vector2.up * Mathf.Sin(progress * Mathf.PI) * Mathf.Min(3, Vector2.Distance(from, to) * .22f) : Vector2.zero);
                DoodleGlyphs.DrawResource(pen, mortar ? "fx.mortar.projectile" : "fx.spitter.projectile", point, theme: theme);
                if (mortar) NotebookEffectRenderer.DrawFootprint(pen, to, launch.ImpactFootprint, launch.Radius, 1, .025f,
                    MeshPen.Alpha(NotebookPalette.Marker, .2f), 24, launch.EntityId, true);
            }
            // Historical recordings retain the snapshot path when they predate semantic launch events.
            foreach (var projectile in frame.Projectiles)
            {
                if (playback.IsProjectileEnded(projectile.Id) || projectile.Remaining <= seconds - frame.Seconds) continue;
                bool recorded = false;
                foreach (var launch in playback.Projectiles)
                    if (launch.Kind == SimulationEventKind.ProjectileLaunched && launch.EntityId == projectile.Id) { recorded = true; break; }
                if (recorded) continue;
                float progress = Mathf.Clamp01(1 - (projectile.Remaining - (seconds - frame.Seconds)) / Mathf.Max(.001f, projectile.Duration));
                Vector2 from = new Vector2(projectile.From.X, projectile.From.Y), to = new Vector2(projectile.To.X, projectile.To.Y);
                Vector2 point = Vector2.Lerp(from, to, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * Mathf.Min(3, Vector2.Distance(from, to) * .22f);
                DoodleGlyphs.DrawResource(pen, "fx.mortar.projectile", point, theme: theme);
                NotebookEffectRenderer.DrawFootprint(pen, to, projectile.ImpactFootprint, projectile.Radius, 1, .025f,
                    MeshPen.Alpha(NotebookPalette.Marker, .2f), 24, projectile.Id, true);
            }
        }
        internal static void DrawEffects(MeshPen pen, NotebookTrialPlayback playback, NotebookVisualThemeAsset theme = null)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            var frame = playback.Report.Frames[playback.FrameIndex];
            foreach (var item in playback.Effects)
            {
                float age = (float)(playback.Seconds - item.SimulationSeconds);
                NotebookEffectRenderer.Draw(pen, item, Muzzle(frame, item, playback, theme), age, theme);
            }
            if (frame.Events.Count == 0)
                foreach (var shot in frame.Shots)
                    pen.Line(new Vector2(shot.From.X, shot.From.Y), new Vector2(shot.To.X, shot.To.Y), .035f, MeshPen.Alpha(NotebookPalette.Blue, .5f));
        }
        static Vector2 Muzzle(TrialFrame frame, SimulationEvent item, NotebookTrialPlayback playback, NotebookVisualThemeAsset theme)
        {
            if (item.MountSlotIndex >= 0)
                foreach (var tower in frame.Towers)
                    if (tower.Id == item.SourceStructureId)
                    {
                        return DoodleGlyphs.SegmentOrigin(tower, item.MountSlotIndex, item.To,
                            new Vector2(item.From.X, item.From.Y), playback.Seconds, playback.Animation, theme);
                    }
            return new Vector2(item.From.X, item.From.Y);
        }
    }
}
