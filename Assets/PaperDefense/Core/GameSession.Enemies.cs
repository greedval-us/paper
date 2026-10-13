using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        private CorpseBlotSystem corpseBlots;
        private readonly List<EnemyGlob> enemyGlobs = new List<EnemyGlob>(64);
        private int nextEnemyGlobId = 1;
        public IReadOnlyList<BlotState> Blots => corpseBlots == null ? (IReadOnlyList<BlotState>)Array.Empty<BlotState>() : corpseBlots.Blots;
        public IReadOnlyList<EnemyGlob> EnemyProjectiles => enemyGlobs;
        public float BlotterCooldownRemaining { get; private set; }
        internal IBlotContext CreateBlotContext() => Mechanics.CorpseBlots ? GetCorpseBlots() : null;

        private CorpseBlotSystem GetCorpseBlots()
        {
            if (corpseBlots == null) corpseBlots = new CorpseBlotSystem(rules.Width, rules.Height,PublishBlotEvent);
            return corpseBlots;
        }

        private void PublishBlotEvent(SimulationEventKind kind,Cell cell)
            => EventBuffer.Publish(TickIndex,kind,Center(cell),Center(cell),default,simulationSeconds:Elapsed);

        private void PublishEnemyAttack(EnemyState enemy,Point2 to)
            => EventBuffer.Publish(TickIndex,SimulationEventKind.EnemyAttack,enemy.Position,to,default,entityId:enemy.Id,
                simulationSeconds:Elapsed,enemyKind:enemy.Kind,enemyDrawingTier:enemy.DrawingTier);

        public bool TryBlotter(Cell cell, out string reason)
        {
            reason = "";
            if (!Mechanics.CorpseBlots) { reason = UserText.Message("copy.babf0ea42d60454a"); return false; }
            if (!Inside(cell)) { reason = UserText.Message("copy.9517113b967397be"); return false; }
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat) { reason = UserText.Message("copy.850e91bbbd5c94d8"); return false; }
            if (BlotterCooldownRemaining > 0) { reason = UserText.Message("copy.c52b7a817b7ba26e"); return false; }
            int cost = Math.Max(0, rules.BlotterCost);
            if (Ink < cost) { reason = UserText.Message("copy.d952db5ec0c442f9", cost); return false; }
            float radius = Math.Max(0, rules.BlotterRadius);
            var system = GetCorpseBlots();
            if (!system.HasManualTarget(cell, radius)) { reason = UserText.Message("copy.a7dd42a018cce2b9"); return false; }
            Ink -= cost;
            BlotterCooldownRemaining = Math.Max(0, rules.BlotterCooldown);
            system.ClearManual(cell, radius);
            return true;
        }

        private void TickEnemyStatuses(float delta)
        {
            if (delta <= 0) return;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Health <= 0) continue;
                if (enemy.BlotRemaining > 0 || enemy.RedRemaining > 0 || enemy.BlueRemaining > 0 || enemy.GreenRemaining > 0)
                    EnemyEffects.Advance(enemy, delta, Statistics, enemyEffects, Mechanics.CorpseBlots ? rules.BlotReduction : 0,EventBuffer,TickIndex,Elapsed);
            }
        }

        private void TickCorpseBlots(float delta)
        {
            if (!Mechanics.CorpseBlots || delta <= 0) return;
            BlotterCooldownRemaining = Math.Max(0, BlotterCooldownRemaining - delta);
            var system = GetCorpseBlots();
            system.Advance(delta);
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Health <= 0) continue;
                if (system.TryConsume(CellOf(enemy.Position)))
                {
                    enemy.BlotRemaining = Math.Max(enemy.BlotRemaining, Math.Max(0, rules.BlotBuffSeconds));
                    EventBuffer.Publish(TickIndex,SimulationEventKind.BlotAbsorbed,Center(CellOf(enemy.Position)),enemy.Position,default,
                        entityId:enemy.Id,simulationSeconds:Elapsed,enemyKind:enemy.Kind,enemyDrawingTier:enemy.DrawingTier);
                }
            }
            system.FinishStep();
        }

        private void OnEnemyDeath(EnemyState enemy)
        {
            enemy.WindingUp = false; enemy.WindupRemaining = 0;
            EnemyEffects.PublishDeath(enemy,EventBuffer,TickIndex,Elapsed);
            if (!Mechanics.CorpseBlots || rules.BlotLifetime <= 0) return;
            GetCorpseBlots().Add(CellOf(enemy.Position), rules.BlotLifetime);
        }

        private bool TryRangedAttack(EnemyState enemy, float delta)
        {
            if (!Mechanics.RangedEnemies || enemy.Kind != EnemyKind.Spitter) return false;
            var definition = rules.Enemy(EnemyKind.Spitter);
            enemy.RangedCooldown = Math.Max(0, enemy.RangedCooldown - delta);
            if (enemy.WindingUp)
            {
                enemy.WindupRemaining = Math.Max(0, enemy.WindupRemaining - delta);
                if (enemy.WindupRemaining > .000001f) return true;
                // Admission bounds live glob state even under extreme custom firing rates.
                if (enemyGlobs.Count >= grid.Length * Math.Max(1, rules.MaxEnemiesPerCell)) return true;
                float duration = Math.Max(0, definition.ProjectileSeconds);
                var glob=new EnemyGlob
                {
                    Id = nextEnemyGlobId++, From = enemy.Position, To = enemy.RangedTarget,
                    Remaining = duration, Duration = duration, Damage = Math.Max(0, definition.Damage),
                    ObjectiveIndex = enemy.RangedObjectiveIndex,
                    SourceEnemyId=enemy.Id,SourceEnemyKind=enemy.Kind,SourceEnemyDrawingTier=enemy.DrawingTier
                };
                enemyGlobs.Add(glob);
                PublishEnemyAttack(enemy,glob.To);
                EventBuffer.Publish(TickIndex,SimulationEventKind.EnemyGlobLaunched,glob.From,glob.To,default,
                    entityId:glob.Id,travelSeconds:duration,simulationSeconds:Elapsed,enemyKind:enemy.Kind,enemyDrawingTier:enemy.DrawingTier);
                enemy.WindingUp = false;
                enemy.RangedCooldown = Math.Max(rules.Combat.MinimumActionInterval, definition.AttackInterval);
                return true;
            }
            Point2 target = TargetPoint(enemy);
            int objective = enemy.ObjectiveIndex;
            if (TraceGlobBlocker(enemy.Position, target, out var targetBlocker))
            {
                target = Center(targetBlocker.Cell);
                objective = int.MinValue;
            }
            float range = Math.Max(rules.Combat.MinimumRangedDistance, definition.Range);
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
                if (TraceGlobBlocker(from, to, out var blocker,out var impact))
                {
                    PublishGlobImpact(glob,impact);
                    blocker.Health = Math.Max(0, blocker.Health - DamageRules.StructureDamage(glob.Damage, blocker, rules));
                    if (blocker.Health <= 0) RemoveStructure(blocker, true);
                    else RefreshHealthBand(blocker);
                    continue;
                }
                if (glob.Remaining <= .000001f)
                {
                    PublishGlobImpact(glob,glob.To);
                    if (!glob.TargetsStructure) DamageObjective(glob.ObjectiveIndex, glob.Damage);
                    continue;
                }
                enemyGlobs[write++] = glob;
            }
            if (write < enemyGlobs.Count) enemyGlobs.RemoveRange(write, enemyGlobs.Count - write);
        }

        private void PublishGlobImpact(EnemyGlob glob,Point2 point)
            =>EventBuffer.Publish(TickIndex,SimulationEventKind.EnemyGlobImpact,glob.From,point,default,entityId:glob.Id,
                simulationSeconds:Elapsed,enemyKind:glob.SourceEnemyKind,enemyDrawingTier:glob.SourceEnemyDrawingTier);

        private static Point2 LerpPoint(Point2 from, Point2 to, float fraction) =>
            new Point2(from.X + (to.X - from.X) * fraction, from.Y + (to.Y - from.Y) * fraction);

        /// <summary>Grid DDA checks only cells crossed this step, including new interception walls.</summary>
        private bool TraceGlobBlocker(Point2 from, Point2 to, out StructureState blocker)
            =>TraceGlobBlocker(from,to,out blocker,out _);

        private bool TraceGlobBlocker(Point2 from, Point2 to, out StructureState blocker,out Point2 impact)
        {
            blocker = null;
            impact=to;float entry=0;
            Cell cell = CellOf(from), end = CellOf(to);
            float dx = to.X - from.X, dy = to.Y - from.Y;
            int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0, sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;
            float deltaX = sx == 0 ? float.PositiveInfinity : Math.Abs(1 / dx);
            float deltaY = sy == 0 ? float.PositiveInfinity : Math.Abs(1 / dy);
            float nextX = sx == 0 ? float.PositiveInfinity : ((sx > 0 ? cell.X + 1 : cell.X) - from.X) / dx;
            float nextY = sy == 0 ? float.PositiveInfinity : ((sy > 0 ? cell.Y + 1 : cell.Y) - from.Y) / dy;
            for (int visited = 0; visited <= rules.Width + rules.Height + 2; visited++)
            {
                var structure = StructureAt(cell);
                if (IsBlocker(structure)) { blocker = structure;impact=LerpPoint(from,to,Math.Max(0,Math.Min(1,entry))); return true; }
                if (cell == end) return false;
                if (nextX < nextY)
                {
                    if (nextX > 1.000001f) return false;
                    entry=nextX;
                    cell = new Cell(cell.X + sx, cell.Y); nextX += deltaX;
                }
                else
                {
                    if (nextY > 1.000001f) return false;
                    entry=nextY;
                    cell = new Cell(cell.X, cell.Y + sy); nextY += deltaY;
                }
            }
            return false;
        }
    }
}
