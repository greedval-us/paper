using System;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        readonly EnemyCrowdMotion crowdMotion;
        // Symmetric alternatives; stable per-enemy handedness resolves identical scores.
        static readonly float[] CrowdAngles = { 0, .5235988f, -.5235988f, 1.0471976f, -1.0471976f, 1.5707963f, -1.5707963f };

        bool TryCrowdSpawn(int side, float visualScale, out Point2 position)
        {
            int entrances = Battlefield == null ? 1 : Battlefield.Entrances.Count;
            int start = random.Next(entrances);
            int extent = side <= 2 ? rules.Height : rules.Width;
            for (int i = 0; i < entrances; i++)
            {
                Cell entrance = Battlefield == null ? default : Battlefield.Entrances[(start + i) % entrances];
                if (Battlefield != null && Battlefield.Side(entrance) != side) continue;
                int center = side <= 2 ? entrance.Y : entrance.X;
                int half = Battlefield != null && !Battlefield.IsOpenTerrain ? 0 : rules.Crowd.EntranceHalfWidth;
                int low = Battlefield == null ? 1 : Math.Max(1, center - half);
                int high = Battlefield == null ? extent - 2 : Math.Min(extent - 2, center + half);
                int count = high - low + 1;
                if (count <= 0) continue;
                int offset = random.Next(count);
                for (int attempt = 0; attempt < count; attempt++)
                {
                    int tangent = low + (offset + attempt) % count;
                    var cell = side == 1 ? new Cell(0, tangent) : side == 2 ? new Cell(rules.Width - 1, tangent) :
                        side == 4 ? new Cell(tangent, rules.Height - 1) : new Cell(tangent, 0);
                    var candidate = Center(cell);
                    if (!crowd.CanEnter(cell) || (Battlefield != null && !Battlefield.IsWalkable(cell)) || IsBlocker(StructureAt(cell))) continue;
                    if (!crowdMotion.CanSpawn(candidate, visualScale)) continue;
                    position = candidate; return true;
                }
            }
            position = default; return false;
        }

        Point2 CrowdPreferredDirection(EnemyState enemy) => IsBlocker(StructureAt(CellOf(enemy.Destination))) ?
                EnemyCrowdMotion.Unit(new Point2(enemy.Destination.X - enemy.Position.X, enemy.Destination.Y - enemy.Position.Y)) :
                EnemyFlow(enemy).Direction(enemy.Position);

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
