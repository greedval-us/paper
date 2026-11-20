using System;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        readonly EnemyCrowdMotion crowdMotion;
        // Symmetric alternatives; stable per-enemy handedness resolves identical scores.
        static readonly float[] CrowdAngles = { 0, .5235988f, -.5235988f, 1.0471976f, -1.0471976f, 1.5707963f, -1.5707963f };


        Point2 CrowdPreferredDirection(EnemyState enemy) => IsBlocker(StructureAt(CellOf(enemy.Destination))) ?
                EnemyCrowdMotion.Unit(new Point2(enemy.Destination.X - enemy.Position.X, enemy.Destination.Y - enemy.Position.Y)) :
                EnemyFlow(enemy).Direction(enemy.Position);

        StructureState ContactWallAhead(EnemyState enemy)
        {
            var position = enemy.Position;
            var goal = TargetPoint(enemy);
            float reach = rules.Combat.StructureContactDistance;
            float nearest = reach * reach;
            int minX = Math.Max(0, (int)Math.Ceiling(position.X - .5f - reach));
            int maxX = Math.Min(rules.Width - 1, (int)Math.Floor(position.X - .5f + reach));
            int minY = Math.Max(0, (int)Math.Ceiling(position.Y - .5f - reach));
            int maxY = Math.Min(rules.Height - 1, (int)Math.Floor(position.Y - .5f + reach));
            StructureState best = null;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var wall = grid[y * rules.Width + x];
                if (wall?.Kind != StructureKind.Wall || !IsBlocker(wall)) continue;
                float dx = x + .5f - position.X, dy = y + .5f - position.Y;
                if (dx * (goal.X - position.X) + dy * (goal.Y - position.Y) <= 0) continue;
                float distance = dx * dx + dy * dy;
                if (distance > nearest || (distance == nearest && best != null && best.Id < wall.Id)) continue;
                nearest = distance;
                best = wall;
            }
            return best;
        }

        void MoveInCrowd(EnemyState enemy, float movement)
        {
            if (movement <= 0) { enemy.CrowdHoldingPosition = true; return; }
            Point2 preferred = enemy.NavigationDirection;
            Point2 steering = crowdMotion.Steer(enemy, preferred, movement);
            Point2 slide = crowdMotion.Slide(enemy, preferred, movement);
            Point2 best = enemy.Position;
            float bestScore = float.MinValue;
            int handedness = (enemy.Id & 1) == 0 ? 1 : -1;
            // Half-steps let queues approach smoothly without violating the swept body test.
            for (int speed = 0; speed < 2 && bestScore == float.MinValue; speed++)
            {
                float travel = movement * (speed == 0 ? 1 : .5f);
                for (int candidate = -7; candidate < CrowdAngles.Length; candidate++)
                {
                    Point2 direction;
                    // Exact wall tangents are needed beside narrow gaps; a fixed angular fan
                    // alone can miss the only free direction between a neighbor and terrain.
                    if (candidate == -7) direction = slide;
                    else if (candidate == -6) direction = new Point2(1, 0);
                    else if (candidate == -5) direction = new Point2(-1, 0);
                    else if (candidate == -4) direction = new Point2(0, 1);
                    else if (candidate == -3) direction = new Point2(0, -1);
                    else if (candidate == -2) direction = steering;
                    else if (candidate == -1) direction = EnemyCrowdMotion.Unit(new Point2(enemy.Destination.X - enemy.Position.X, enemy.Destination.Y - enemy.Position.Y));
                    else
                    {
                        float angle = CrowdAngles[candidate] * handedness;
                        float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
                        direction = new Point2(preferred.X * c - preferred.Y * s, preferred.X * s + preferred.Y * c);
                    }
                    float progress = direction.X * preferred.X + direction.Y * preferred.Y;
                    // A packed queue must be able to yield a small step away from its goal.
                    // Forbidding all backward motion locks the leading body between followers.
                    if (direction.X * direction.X + direction.Y * direction.Y < .5f) continue;
                    float score = progress + (enemy.YieldingToNeighbor ? 2.5f : .8f) * (direction.X * steering.X + direction.Y * steering.Y) +
                        rules.Crowd.DirectionMemory * (direction.X * enemy.MovementDirection.X + direction.Y * enemy.MovementDirection.Y);
                    if (score <= bestScore) continue;
                    var next = new Point2(enemy.Position.X + direction.X * travel, enemy.Position.Y + direction.Y * travel);
                    if (!CanTraverseCrowd(enemy.Position, next) || !crowdMotion.CanMove(enemy, next)) continue;
                    if (CellOf(next) != CellOf(enemy.Position) && !crowd.CanEnter(CellOf(next))) continue;
                    best = next; bestScore = score;
                }
            }
            if (bestScore == float.MinValue) return;
            Point2 previous = enemy.Position;
            if (!crowd.TryMove(previous, best)) return;
            enemy.Position = best;
            enemy.MovementDirection = EnemyCrowdMotion.Unit(new Point2(best.X - previous.X, best.Y - previous.Y));
            crowdMotion.Moved(enemy, previous);
        }

        bool CanTraverseCrowd(Point2 from, Point2 to)
        {
            float padding = rules.Crowd.ObstacleClearance;
            if (to.X < padding || to.Y < padding || to.X >= rules.Width - padding || to.Y >= rules.Height - padding) return false;
            int minX = Math.Max(0, (int)Math.Floor(Math.Min(from.X, to.X) - padding));
            int maxX = Math.Min(rules.Width - 1, (int)Math.Floor(Math.Max(from.X, to.X) + padding));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(from.Y, to.Y) - padding));
            int maxY = Math.Min(rules.Height - 1, (int)Math.Floor(Math.Max(from.Y, to.Y) + padding));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Cell(x, y);
                    if ((Battlefield == null || Battlefield.IsWalkable(cell)) && !IsBlocker(StructureAt(cell))) continue;
                    float enter = 0, exit = 1;
                    if (ClipCrowdAxis(from.X, to.X - from.X, x - padding, x + 1 + padding, ref enter, ref exit) &&
                        ClipCrowdAxis(from.Y, to.Y - from.Y, y - padding, y + 1 + padding, ref enter, ref exit)) return false;
                }
            return true;
        }

        bool EnemyTouchesConstruction(EnemyState enemy, System.Collections.Generic.HashSet<Cell> cells)
        {
            float padding = rules.Crowd.ObstacleClearance;
            for (int y = (int)Math.Floor(enemy.Position.Y - padding); y <= (int)Math.Floor(enemy.Position.Y + padding); y++)
                for (int x = (int)Math.Floor(enemy.Position.X - padding); x <= (int)Math.Floor(enemy.Position.X + padding); x++)
                {
                    var cell = new Cell(x, y);
                    if (cells.Contains(cell) && StructureAt(cell) == null) return true;
                }
            return false;
        }

        static bool ClipCrowdAxis(float origin, float delta, float low, float high, ref float enter, ref float exit)
        {
            if (Math.Abs(delta) < .000001f) return origin >= low && origin <= high;
            float a = (low - origin) / delta, b = (high - origin) / delta;
            enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
            return enter <= exit;
        }
    }
}
