using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    // Expected routes on the authored terrain before any towers or barricades divert enemies.
    // The field and traces are built once and use the same steering as moving enemies.
    internal sealed class BattlefieldApproaches
    {
        const int HeadingStep = 10;
        readonly List<(int side, Point2[] points)> routes = new List<(int, Point2[])>();
        readonly int[] scores = new int[360 / HeadingStep];
        Cell origin;
        int activeSides, entranceScore;
        float entranceHeading;

        internal BattlefieldApproaches(BattlefieldView map, Cell goal, int width, int height)
        {
            var field = new WeightedFlowField(width, height, map);
            field.Rebuild(goal, new StructureState[width * height]);
            foreach (var entrance in map.Entrances)
                routes.Add((map.Side(entrance), TraceApproach(entrance)));

            Point2[] TraceApproach(Cell entrance)
            {
                var points = new List<Point2>();
                var position = new Point2(entrance.X + .5f, entrance.Y + .5f);
                int steps = map.DistanceToCastle(entrance) * 4 + 4;
                for (int i = 0; i < steps; i++)
                {
                    var cell = new Cell((int)position.X, (int)position.Y);
                    if (i % 2 == 0) points.Add(position);
                    if (cell == goal) return points.ToArray();
                    var direction = field.Direction(position);
                    var next = new Point2(position.X + direction.X * .5f, position.Y + direction.Y * .5f);
                    if (!map.IsWalkable(new Cell((int)next.X, (int)next.Y)))
                    {
                        var turn = field.Next(cell);
                        next = new Point2(turn.X + .5f, turn.Y + .5f);
                    }
                    position = next;
                }
                // Narrow road bends can block continuous steering. Cell routing is monotone to the castle.
                points.Clear();
                for (var cell = entrance; cell != goal; cell = field.Next(cell))
                    points.Add(new Point2(cell.X + .5f, cell.Y + .5f));
                points.Add(new Point2(goal.X + .5f, goal.Y + .5f));
                return points.ToArray();
            }
        }

        internal void Begin(Cell tower, int sides, float towardEntrance)
        {
            origin = tower;
            activeSides = sides;
            entranceHeading = towardEntrance;
            entranceScore = 0;
            Array.Clear(scores, 0, scores.Length);
        }

        internal void AddWeapon(AttackKind attack, float minimumRange, float range, float width)
        {
            if (attack == AttackKind.Radial || range <= 0) return;
            double minSquared = minimumRange * (double)minimumRange, maxSquared = range * (double)range;
            double half = width * .5;
            foreach (var route in routes)
            {
                if ((activeSides & route.side) == 0) continue;
                foreach (var point in route.points)
                {
                    double dx = point.X - origin.X - .5, dy = point.Y - origin.Y - .5;
                    double squared = dx * dx + dy * dy;
                    if (squared < minSquared || squared > maxSquared || squared == 0) continue;
                    float angle = FiringSector.Heading(dx, dy);
                    if (AngleDistance(angle, entranceHeading) <= half) entranceScore++;
                    // One route sample votes for each heading from which this mount can reach it.
                    int first = (int)Math.Ceiling((angle - half) / HeadingStep);
                    int last = (int)Math.Floor((angle + half) / HeadingStep);
                    for (int bin = first; bin <= last; bin++)
                        scores[(bin + scores.Length) % scores.Length]++;
                }
            }
        }

        internal float BestHeading()
        {
            int bestScore = entranceScore;
            float best = entranceHeading, bestDistance = 0;
            for (int i = 0; i < scores.Length; i++)
            {
                float heading = i * HeadingStep, distance = AngleDistance(heading, entranceHeading);
                if (scores[i] > bestScore || (scores[i] == bestScore && distance < bestDistance))
                {
                    best = heading; bestScore = scores[i]; bestDistance = distance;
                }
            }
            return best;
        }

        static float AngleDistance(float a, float b) => Math.Abs((a - b + 540) % 360 - 180);
    }
}
