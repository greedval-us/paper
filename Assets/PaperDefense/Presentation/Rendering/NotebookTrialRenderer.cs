using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    internal static class NotebookTrialRenderer
    {
        internal static void Draw(MeshPen pen, NotebookTrialPlayback playback)
        {
            pen.Clear();
            var theme = NotebookVisualThemeAsset.Default;
            var frame = playback.Report.Frames[playback.FrameIndex];
            float seconds = (float)playback.Seconds;
            for (int y = 8; y <= 36; y += 4)
                pen.Line(new Vector2(8, y), new Vector2(50, y), .04f, MeshPen.Alpha(NotebookPalette.Blue, .12f));
            for (int x = 8; x <= 50; x += 4)
                pen.Line(new Vector2(x, 8), new Vector2(x, 36), .04f, MeshPen.Alpha(NotebookPalette.Blue, .12f));
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
                DoodleGlyphs.StructureBase(pen, tower, theme);
                DoodleGlyphs.StructureDynamic(pen, tower, 1, seconds, playback.Animation, theme);
            }
            DrawEnemies(pen, playback, theme);
            DrawProjectiles(pen, playback, theme);
            DrawEffects(pen, playback, theme);
        }
        internal static void DrawEnemies(MeshPen pen, NotebookTrialPlayback playback, NotebookVisualThemeAsset theme = null)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            var frame = playback.Report.Frames[playback.FrameIndex];
            float seconds = (float)playback.Seconds;
            foreach (var enemy in frame.Enemies)
                if (enemy.Health > 0 && !playback.IsEnemyEnded(enemy.Id))
                    DoodleGlyphs.Enemy(pen, enemy, seconds, false, playback.Animation, theme, frame.Seconds);
            foreach (var death in playback.Animation.Deaths)
                DoodleGlyphs.DrawResourceEnemy(pen, death.Kind, death.Tier, death.Position,
                    NotebookEnemyMotion.Death, (float)(playback.Seconds - death.Born), theme, death.VisualScale);
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
                        return NotebookTowerComposition.SegmentOrigin(tower, item.MountSlotIndex, item.To,
                            new Vector2(item.From.X, item.From.Y), playback.Seconds, playback.Animation, theme);
            return new Vector2(item.From.X, item.From.Y);
        }
    }
}
