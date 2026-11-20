using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class GameSession : IEnemyAttackWorld
    {
        private CorpseBlotSystem corpseBlots;
        public IReadOnlyList<BlotState> Blots => corpseBlots == null ? (IReadOnlyList<BlotState>)Array.Empty<BlotState>() : corpseBlots.Blots;
        public IReadOnlyList<EnemyGlob> EnemyProjectiles => enemyAttacks.Projectiles;
        public float BlotterCooldownRemaining { get; private set; }
        internal IBlotContext CreateBlotContext() => Mechanics.CorpseBlots ? GetCorpseBlots() : null;

        private CorpseBlotSystem GetCorpseBlots()
        {
            if (corpseBlots == null) corpseBlots = new CorpseBlotSystem(rules.Width, rules.Height,PublishBlotEvent);
            return corpseBlots;
        }

        private void PublishBlotEvent(SimulationEventKind kind,Cell cell)
            => EventBuffer.Publish(TickIndex,kind,Center(cell),Center(cell),default,simulationSeconds:Elapsed);

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
                        entityId:enemy.Id,simulationSeconds:Elapsed,enemyKind:enemy.Kind,enemyDrawingTier:enemy.DrawingTier,enemyVisualScale:enemy.VisualScale);
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

        Point2 IEnemyAttackWorld.ObjectivePosition(EnemyState enemy) => TargetPoint(enemy);
        void IEnemyAttackWorld.HitObjective(int index, float damage, Point2 source) => DamageObjective(index, damage, source);

        void IEnemyAttackWorld.HitStructure(StructureState blocker, Point2 source, float rawDamage)
        {
            float previousHealth = blocker.Health;
            blocker.Health = Math.Max(0, blocker.Health - DamageRules.StructureDamage(rawDamage, blocker, rules, source));
            PublishStructureHit(blocker, source, previousHealth);
            if (blocker.Health <= 0) RemoveStructure(blocker, true);
            else RefreshHealthBand(blocker);
        }

        /// <summary>Grid DDA checks only cells crossed this step, including new interception walls.</summary>
        bool IEnemyAttackWorld.TraceBlocker(Point2 from, Point2 to, out StructureState blocker, out Point2 impact)
            => TraceBlocker(from, to, false, out blocker, out impact);

        bool TraceBlocker(Point2 from, Point2 to, bool stopAtTerrain, out StructureState blocker, out Point2 impact)
        {
            blocker = null;
            impact = to;
            float entry = 0;
            Cell cell = CellOf(from), end = CellOf(to);
            float dx = to.X - from.X, dy = to.Y - from.Y;
            int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0, sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;
            float deltaX = sx == 0 ? float.PositiveInfinity : Math.Abs(1 / dx);
            float deltaY = sy == 0 ? float.PositiveInfinity : Math.Abs(1 / dy);
            float nextX = sx == 0 ? float.PositiveInfinity : ((sx > 0 ? cell.X + 1 : cell.X) - from.X) / dx;
            float nextY = sy == 0 ? float.PositiveInfinity : ((sy > 0 ? cell.Y + 1 : cell.Y) - from.Y) / dy;
            for (int visited = 0; visited <= rules.Width + rules.Height + 2; visited++)
            {
                if (stopAtTerrain && Battlefield != null && !Battlefield.IsWalkable(cell)) return false;
                var structure = StructureAt(cell);
                if (IsBlocker(structure))
                {
                    blocker = structure;
                    impact = Point2.Lerp(from, to, Math.Max(0, Math.Min(1, entry)));
                    return true;
                }
                if (cell == end) return false;
                if (nextX < nextY)
                {
                    if (nextX > 1.000001f) return false;
                    entry = nextX;
                    cell = new Cell(cell.X + sx, cell.Y); nextX += deltaX;
                }
                else
                {
                    if (nextY > 1.000001f) return false;
                    entry = nextY;
                    cell = new Cell(cell.X, cell.Y + sy); nextY += deltaY;
                }
            }
            return false;
        }
    }
}
