using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        private readonly List<BlotState> blots = new List<BlotState>(256);
        private readonly List<EnemyGlob> enemyGlobs = new List<EnemyGlob>(64);
        private BlotState[] blotCells;
        private int nextEnemyGlobId = 1;
        public IReadOnlyList<BlotState> Blots => blots;
        public IReadOnlyList<EnemyGlob> EnemyProjectiles => enemyGlobs;
        public float BlotterCooldownRemaining { get; private set; }
        internal IBlotContext CreateBlotContext() => new SessionBlotContext(this);

        sealed class SessionBlotContext : IBlotContext
        {
            readonly GameSession session;
            internal SessionBlotContext(GameSession session) { this.session = session; }
            public bool TryFindNearest(Point2 origin, float range, out Cell cell)
                => BlotQueries.TryFindNearest(session.blots, origin, range, out cell);
            public int Clear(Cell center, float radius)
            {
                int cleared = BlotQueries.Clear(session.blots, center, radius);
                session.CompactBlots();
                return cleared;
            }
        }

        public bool TryBlotter(Cell cell, out string reason)
        {
            reason = "";
            if (!Balance.EnableEnemyMechanics) { reason = "Промокашка недоступна на этом листе."; return false; }
            if (!Inside(cell)) { reason = "Выберите клетку на листе."; return false; }
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat) { reason = "Эта страница уже закончена."; return false; }
            if (BlotterCooldownRemaining > 0) { reason = "Промокашке нужно высохнуть."; return false; }
            int cost = Math.Max(0, Balance.BlotterCost);
            if (Ink < cost) { reason = "Не хватает штрихов: нужно " + cost + "."; return false; }
            float radius = Math.Max(0, Balance.BlotterRadius), radiusSquared = radius * radius;
            bool found = false;
            for (int i = 0; i < blots.Count; i++)
            {
                var blot = blots[i];
                float dx = blot.Cell.X - cell.X, dy = blot.Cell.Y - cell.Y;
                if (blot.Remaining > 0 && dx * dx + dy * dy <= radiusSquared) { found = true; break; }
            }
            if (!found) { reason = "В этом радиусе нет клякс."; return false; }
            Ink -= cost;
            BlotterCooldownRemaining = Math.Max(0, Balance.BlotterCooldown);
            for (int i = 0; i < blots.Count; i++)
            {
                var blot = blots[i];
                float dx = blot.Cell.X - cell.X, dy = blot.Cell.Y - cell.Y;
                if (dx * dx + dy * dy <= radiusSquared) blot.Remaining = 0;
            }
            CompactBlots();
            return true;
        }

        private void TickEnemyMechanics(float delta)
        {
            if (delta <= 0) return;
            if (Balance.EnableEnemyMechanics && blotCells == null) blotCells = new BlotState[grid.Length];
            BlotterCooldownRemaining = Math.Max(0, BlotterCooldownRemaining - delta);
            for (int i = 0; i < blots.Count; i++) blots[i].Remaining = Math.Max(0, blots[i].Remaining - delta);
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Health <= 0) continue;
                if (enemy.BlotRemaining > 0 || enemy.RedRemaining > 0 || enemy.BlueRemaining > 0 || enemy.GreenRemaining > 0)
                    EnemyEffects.Advance(enemy, delta, Statistics, Balance.Effects, Balance.BlotReduction);
                if (enemy.Health <= 0) continue;
                if (!Balance.EnableEnemyMechanics) continue;
                var cell = CellOf(enemy.Position);
                if (!Inside(cell)) continue;
                var blot = blotCells[cell.Y * Balance.Width + cell.X];
                if (blot == null || blot.Remaining <= 0) continue;
                blot.Remaining = 0;
                enemy.BlotRemaining = Math.Max(enemy.BlotRemaining, Math.Max(0, Balance.BlotBuffSeconds));
            }
            CompactBlots();
            AdvanceEnemyGlobs(delta);
        }

        private void CompactBlots()
        {
            int write = 0;
            for (int read = 0; read < blots.Count; read++)
                if (blots[read].Remaining > 0) blots[write++] = blots[read];
            if (write < blots.Count) blots.RemoveRange(write, blots.Count - write);
        }

        private void OnEnemyDeath(EnemyState enemy)
        {
            enemy.WindingUp = false; enemy.WindupRemaining = 0;
            if (!Balance.EnableEnemyMechanics || Balance.BlotLifetime <= 0) return;
            var cell = CellOf(enemy.Position);
            if (!Inside(cell)) return;
            if (blotCells == null) blotCells = new BlotState[grid.Length];
            int index = cell.Y * Balance.Width + cell.X;
            var blot = blotCells[index];
            if (blot == null) blotCells[index] = blot = new BlotState { Cell = cell };
            if (blot.Remaining <= 0) blots.Add(blot);
            // One ordinary corpse mark per cell: buffed corpses never strengthen the next buff.
            blot.Remaining = Math.Max(0, Balance.BlotLifetime);
        }

        private bool TryRangedAttack(EnemyState enemy, float delta)
        {
            if (!Balance.EnableEnemyMechanics || enemy.Kind != EnemyKind.Spitter) return false;
            var definition = Balance.Enemy(EnemyKind.Spitter);
            enemy.RangedCooldown = Math.Max(0, enemy.RangedCooldown - delta);
            if (enemy.WindingUp)
            {
                enemy.WindupRemaining = Math.Max(0, enemy.WindupRemaining - delta);
                if (enemy.WindupRemaining > .000001f) return true;
                // Admission bounds live glob state even under extreme custom firing rates.
                if (enemyGlobs.Count >= grid.Length * Math.Max(1, Balance.MaxEnemiesPerCell)) return true;
                float duration = Math.Max(0, definition.ProjectileSeconds);
                enemyGlobs.Add(new EnemyGlob
                {
                    Id = nextEnemyGlobId++, From = enemy.Position, To = enemy.RangedTarget,
                    Remaining = duration, Duration = duration, Damage = Math.Max(0, definition.Damage),
                    ObjectiveIndex = enemy.RangedObjectiveIndex
                });
                enemy.WindingUp = false;
                enemy.RangedCooldown = Math.Max(.05f, definition.AttackInterval);
                return true;
            }
            Point2 target = TargetPoint(enemy);
            int objective = enemy.ObjectiveIndex;
            if (TraceGlobBlocker(enemy.Position, target, out var targetBlocker))
            {
                target = Center(targetBlocker.Cell);
                objective = int.MinValue;
            }
            float range = Math.Max(1.2f, definition.Range);
            if (DistanceSquared(enemy.Position, target) > range * range) return false;
            if (enemy.RangedCooldown <= 0)
            {
                enemy.RangedTarget = target;
                enemy.RangedObjectiveIndex = objective;
                enemy.WindingUp = true;
                enemy.WindupRemaining = Math.Max(0, definition.WindupSeconds);
            }
            return true;
        }

        private float MovementEffect(EnemyState enemy) => Balance.EnableEnemyMechanics ?
            EnemyEffects.MovementMultiplier(enemy, Balance.Effects, Balance.BlotSpeedBonus) : 1;

        private void AdvanceEnemyGlobs(float delta)
        {
            int write = 0;
            for (int read = 0; read < enemyGlobs.Count; read++)
            {
                var glob = enemyGlobs[read];
                float previous = glob.Duration <= .000001f ? 0 : 1 - glob.Remaining / glob.Duration;
                glob.Remaining = Math.Max(0, glob.Remaining - delta);
                float current = glob.Duration <= .000001f ? 1 : 1 - glob.Remaining / glob.Duration;
                var from = LerpPoint(glob.From, glob.To, previous);
                var to = LerpPoint(glob.From, glob.To, current);
                if (TraceGlobBlocker(from, to, out var blocker))
                {
                    blocker.Health = Math.Max(0, blocker.Health - DamageRules.StructureDamage(glob.Damage, blocker, Balance));
                    if (blocker.Health <= 0) RemoveStructure(blocker, true);
                    else RefreshHealthBand(blocker);
                    continue;
                }
                if (glob.Remaining <= .000001f)
                {
                    if (!glob.TargetsStructure) DamageObjective(glob.ObjectiveIndex, glob.Damage);
                    continue;
                }
                enemyGlobs[write++] = glob;
            }
            if (write < enemyGlobs.Count) enemyGlobs.RemoveRange(write, enemyGlobs.Count - write);
        }

        private static Point2 LerpPoint(Point2 from, Point2 to, float fraction) =>
            new Point2(from.X + (to.X - from.X) * fraction, from.Y + (to.Y - from.Y) * fraction);

        /// <summary>Grid DDA checks only cells crossed this step, including new interception walls.</summary>
        private bool TraceGlobBlocker(Point2 from, Point2 to, out StructureState blocker)
        {
            blocker = null;
            Cell cell = CellOf(from), end = CellOf(to);
            float dx = to.X - from.X, dy = to.Y - from.Y;
            int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0, sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;
            float deltaX = sx == 0 ? float.PositiveInfinity : Math.Abs(1 / dx);
            float deltaY = sy == 0 ? float.PositiveInfinity : Math.Abs(1 / dy);
            float nextX = sx == 0 ? float.PositiveInfinity : ((sx > 0 ? cell.X + 1 : cell.X) - from.X) / dx;
            float nextY = sy == 0 ? float.PositiveInfinity : ((sy > 0 ? cell.Y + 1 : cell.Y) - from.Y) / dy;
            for (int visited = 0; visited <= Balance.Width + Balance.Height + 2; visited++)
            {
                var structure = StructureAt(cell);
                if (IsBlocker(structure)) { blocker = structure; return true; }
                if (cell == end) return false;
                if (nextX < nextY)
                {
                    if (nextX > 1.000001f) return false;
                    cell = new Cell(cell.X + sx, cell.Y); nextX += deltaX;
                }
                else
                {
                    if (nextY > 1.000001f) return false;
                    cell = new Cell(cell.X, cell.Y + sy); nextY += deltaY;
                }
            }
            return false;
        }
    }
}
