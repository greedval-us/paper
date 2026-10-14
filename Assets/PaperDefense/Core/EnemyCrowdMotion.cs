using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Live spatial buckets, soft personal space and hard swept body separation.
    /// Routing, combat and terrain remain the caller's responsibility.</summary>
    internal sealed class EnemyCrowdMotion
    {
        const float BucketSize = 4;
        readonly int columns, rows;
        readonly List<EnemyState>[] buckets;
        readonly CrowdMovementRules rules;
        readonly List<EnemyState> neighbors = new List<EnemyState>(32);
        float maxRadius;

        internal EnemyCrowdMotion(int width, int height, CrowdMovementRules rules)
        {
            this.rules = rules;
            columns = (int)Math.Ceiling(width / BucketSize); rows = (int)Math.Ceiling(height / BucketSize);
            buckets = new List<EnemyState>[columns * rows];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<EnemyState>(8);
        }

        internal float Radius(EnemyState enemy) => enemy.VisualScale * rules.RadiusPerVisualScale;
        internal void Rebuild(IReadOnlyList<EnemyState> enemies)
        {
            foreach (var bucket in buckets) bucket.Clear();
            maxRadius = 0;
            foreach (var enemy in enemies) if (enemy.Health > 0) Add(enemy);
        }
        internal void Add(EnemyState enemy)
        { buckets[Index(enemy.Position)].Add(enemy); maxRadius = Math.Max(maxRadius, Radius(enemy)); }
        internal void Moved(EnemyState enemy, Point2 previous)
        {
            int oldIndex = Index(previous), newIndex = Index(enemy.Position);
            if (oldIndex == newIndex) return;
            buckets[oldIndex].Remove(enemy); buckets[newIndex].Add(enemy);
        }
        int Index(Point2 p) => Y(p.Y) * columns + X(p.X);
        int X(float value) => Math.Max(0, Math.Min(columns - 1, (int)(value / BucketSize)));
        int Y(float value) => Math.Max(0, Math.Min(rows - 1, (int)(value / BucketSize)));

        internal bool CanSpawn(Point2 point, float visualScale)
        {
            float radius = visualScale * rules.RadiusPerVisualScale;
            Query(point, radius + maxRadius);
            foreach (var other in neighbors)
                if (Squared(point, other.Position) < Square(radius + Radius(other))) return false;
            return true;
        }

        internal bool OccupiesConstructionClearance(Cell cell)
        {
            float padding = rules.ObstacleClearance;
            Query(new Point2(cell.X + .5f, cell.Y + .5f), 1.5f);
            foreach (var enemy in neighbors)
                if (enemy.Position.X >= cell.X - padding && enemy.Position.X <= cell.X + 1 + padding &&
                    enemy.Position.Y >= cell.Y - padding && enemy.Position.Y <= cell.Y + 1 + padding) return true;
            return false;
        }

        // Query once per mover, reuse the same neighbors for all candidate directions.
        internal Point2 Steer(EnemyState enemy, Point2 preferred, float travel)
        {
            Query(enemy.Position, Radius(enemy) + maxRadius + rules.PersonalSpace + travel);
            float sx = 0, sy = 0;
            EnemyState passing = null;
            float closest = float.MaxValue;
            foreach (var other in neighbors)
            {
                if (other == enemy) continue;
                float dx = other.Position.X - enemy.Position.X, dy = other.Position.Y - enemy.Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (distance < .00001f) continue;
                float ahead = (dx * preferred.X + dy * preferred.Y) / distance;
                if (other.Id == enemy.AvoidanceNeighborId && ahead > -.2f)
                { passing = other; break; }
                if (ahead > .6f && distance < closest)
                { closest = distance; passing = other; }
            }
            if (passing != null && passing.Id != enemy.AvoidanceNeighborId)
            {
                float dx = enemy.Position.X - passing.Position.X, dy = enemy.Position.Y - passing.Position.Y;
                float side = -dy * preferred.X + dx * preferred.Y;
                enemy.AvoidanceSide = Math.Abs(side) > .05f ? Math.Sign(side) : ((enemy.Id & 1) == 0 ? 1 : -1);
                enemy.AvoidanceNeighborId = passing.Id;
            }
            else if (passing == null) enemy.AvoidanceNeighborId = 0;
            enemy.YieldingToNeighbor = passing != null && !passing.CrowdHoldingPosition && passing.SpeedScale > 0 && HasPriority(passing, enemy) &&
                (passing.YieldingToNeighbor || preferred.X * passing.NavigationDirection.X + preferred.Y * passing.NavigationDirection.Y < .5f);
            if (enemy.YieldingToNeighbor) return new Point2(-preferred.X, -preferred.Y);
            foreach (var other in neighbors)
            {
                if (other == enemy) continue;
                float dx = enemy.Position.X - other.Position.X, dy = enemy.Position.Y - other.Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float space = Radius(enemy) + Radius(other) + rules.PersonalSpace;
                if (distance >= space || distance < .00001f) continue;
                // Stable right of way breaks symmetric jams where streams merge into a gap.
                float force = (space - distance) / rules.PersonalSpace * (HasPriority(other, enemy) ? 1.5f : .5f);
                sx += dx / distance * force; sy += dy / distance * force;
                if (other == passing)
                {
                    // Commit to one passing side until the neighbor is behind us. Without
                    // this memory nearly head-on bodies flip sides every few frames.
                    sx -= dy / distance * force * enemy.AvoidanceSide;
                    sy += dx / distance * force * enemy.AvoidanceSide;
                }
            }
            return Unit(new Point2(preferred.X + sx * rules.SeparationWeight,
                preferred.Y + sy * rules.SeparationWeight));
        }

        internal bool CanMove(EnemyState enemy, Point2 next)
        {
            foreach (var other in neighbors)
            {
                if (other == enemy || other.Health <= 0) continue;
                float minimum = Square(Radius(enemy) + Radius(other));
                float before = Squared(enemy.Position, other.Position);
                // A restored/debug state may already overlap. Permit only outward recovery.
                if (before < minimum - .00001f)
                {
                    float dot = (next.X - enemy.Position.X) * (enemy.Position.X - other.Position.X) +
                        (next.Y - enemy.Position.Y) * (enemy.Position.Y - other.Position.Y);
                    if (dot < 0 || Squared(next, other.Position) <= before) return false;
                }
                else if (SegmentDistanceSquared(enemy.Position, next, other.Position) < minimum - .00001f) return false;
            }
            return true;
        }

        internal Point2 Slide(EnemyState enemy, Point2 desired, float travel)
        {
            float vx = desired.X, vy = desired.Y;
            // Project an inward step onto the exact body tangent. Quantized turn angles
            // can alternate either side of that tangent forever at almost-touching bodies.
            for (int pass = 0; pass < 3; pass++)
                foreach (var other in neighbors)
                {
                    if (other == enemy || other.Health <= 0) continue;
                    float dx = enemy.Position.X - other.Position.X, dy = enemy.Position.Y - other.Position.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distance < .00001f || distance > Radius(enemy) + Radius(other) + travel) continue;
                    float nx = dx / distance, ny = dy / distance;
                    float inward = vx * nx + vy * ny;
                    if (inward < 0) { vx -= nx * inward; vy -= ny * inward; }
                }
            return Unit(new Point2(vx, vy));
        }

        void Query(Point2 center, float radius)
        {
            neighbors.Clear();
            for (int y = Y(center.Y - radius); y <= Y(center.Y + radius); y++)
                for (int x = X(center.X - radius); x <= X(center.X + radius); x++)
                    foreach (var e in buckets[y * columns + x])
                        if (e.Health > 0 && Squared(center, e.Position) <= radius * radius) neighbors.Add(e);
        }
        internal static Point2 Unit(Point2 p)
        {
            float length = (float)Math.Sqrt(p.X * p.X + p.Y * p.Y);
            return length < .00001f ? default : new Point2(p.X / length, p.Y / length);
        }
        static float SegmentDistanceSquared(Point2 a, Point2 b, Point2 p)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, length = dx * dx + dy * dy;
            float t = length <= .0000001f ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length));
            return Square(a.X + dx * t - p.X) + Square(a.Y + dy * t - p.Y);
        }
        static float Square(float v) => v * v;
        static bool HasPriority(EnemyState a, EnemyState b) => a.NavigationCost < b.NavigationCost - .001f ||
            (Math.Abs(a.NavigationCost - b.NavigationCost) <= .001f && a.Id < b.Id);
        static float Squared(Point2 a, Point2 b) => Square(a.X - b.X) + Square(a.Y - b.Y);
    }
}
