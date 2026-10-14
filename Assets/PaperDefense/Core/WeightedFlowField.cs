using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>One reverse Dijkstra field shared by every enemy. Walls have finite breach costs.</summary>
    internal sealed class WeightedFlowField
    {
        private readonly int width, height;
        private readonly BattlefieldView battlefield;
        private readonly float[] distance;
        private readonly float[] traversal;
        private readonly List<HeapEntry> heap = new List<HeapEntry>();
        private Cell goal;
        private static readonly int[] OffsetX = { 1, -1, 0, 0 };
        private static readonly int[] OffsetY = { 0, 0, 1, -1 };

        internal WeightedFlowField(int width, int height, BattlefieldView battlefield = null)
        { this.width = width; this.height = height; this.battlefield=battlefield; distance = new float[width * height]; traversal = new float[width * height]; }

        internal void Rebuild(Cell goal, StructureState[] grid, float movementSpeed = 1, float breachDamagePerSecond = 50)
        {
            this.goal = goal;
            heap.Clear();
            float walkingSeconds = 1 / Math.Max(0.01f, movementSpeed);
            float breachDps = Math.Max(0.01f, breachDamagePerSecond);
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = float.MaxValue;
                var structure = grid[i];
                bool blocker = structure != null && structure.IsComplete && structure.Health > 0 && structure.Kind != StructureKind.Spikes && structure.Kind != StructureKind.TarPit;
                // Four remaining-health bands avoid a full rebuild for every individual hit.
                float estimatedHealth = blocker ? Math.Max(1, structure.MaxHealth) * HealthBand(structure) / 4f : 0;
                traversal[i] = walkingSeconds + estimatedHealth / breachDps;
            }
            int origin = goal.Y * width + goal.X;
            distance[origin] = 0;
            Push(new HeapEntry(origin, 0));
            while (heap.Count != 0)
            {
                var current = Pop();
                if (current.Cost > distance[current.Index]) continue;
                int x = current.Index % width, y = current.Index / width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + OffsetX[direction], ny = y + OffsetY[direction];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int index = ny * width + nx;
                    if(battlefield!=null&&!battlefield.IsWalkable(new Cell(nx,ny)))continue;
                    float candidate = current.Cost + traversal[current.Index];
                    if (candidate >= distance[index]) continue;
                    distance[index] = candidate;
                    Push(new HeapEntry(index, candidate));
                }
            }
        }

        internal static int HealthBand(StructureState structure) => structure == null || structure.Health <= 0 ? 0 :
            Math.Max(1, Math.Min(4, (int)Math.Ceiling(structure.Health / Math.Max(1, structure.MaxHealth) * 4)));

        internal Cell Next(Cell cell)
        {
            int x = Math.Max(0, Math.Min(width - 1, cell.X)), y = Math.Max(0, Math.Min(height - 1, cell.Y));
            var best = new Cell(x, y);
            float bestCost = float.MaxValue;
            long bestGoalDistance = long.MaxValue;
            // Route cost (including breaches) comes first. On an exact tie, prefer progress
            // toward the goal on both axes instead of marching north/south entries along the border.
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + OffsetX[direction], ny = y + OffsetY[direction];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int index = ny * width + nx;
                if (battlefield != null && !battlefield.IsWalkable(new Cell(nx, ny))) continue;
                if (distance[index] == float.MaxValue) continue;
                float score = distance[index] + traversal[index];
                long dx = (long)goal.X - nx, dy = (long)goal.Y - ny;
                long goalDistance = dx * dx + dy * dy;
                if (score < bestCost || (score == bestCost && goalDistance < bestGoalDistance))
                { bestCost = score; bestGoalDistance = goalDistance; best = new Cell(nx, ny); }
            }
            return best;
        }

        internal Point2 Direction(Point2 position)
        {
            var cell = new Cell((int)position.X, (int)position.Y);
            if (cell == goal) return EnemyCrowdMotion.Unit(new Point2(goal.X + .5f - position.X, goal.Y + .5f - position.Y));
            Cell next = Next(cell);
            float bestCost = distance[next.Y * width + next.X] + traversal[next.Y * width + next.X];
            float vx = 0, vy = 0;
            int options = 0;
            for (int i = 0; i < 4; i++)
            {
                int x = cell.X + OffsetX[i], y = cell.Y + OffsetY[i];
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                int index = y * width + x;
                if (distance[index] == float.MaxValue || (battlefield != null && !battlefield.IsWalkable(new Cell(x, y)))) continue;
                if (distance[index] + traversal[index] > bestCost + .001f) continue;
                options++;
                vx += OffsetX[i] * Math.Max(.1f, Math.Abs(goal.X + .5f - position.X));
                vy += OffsetY[i] * Math.Max(.1f, Math.Abs(goal.Y + .5f - position.Y));
            }
            // In open areas equal-cost axes combine into a continuous heading, preserving
            // each arrival position. A forced turn still aims inside the walkable next cell.
            return EnemyCrowdMotion.Unit(options > 1 && vx * vx + vy * vy > .00001f ? new Point2(vx, vy) :
                new Point2(next.X + .5f - position.X, next.Y + .5f - position.Y));
        }

        internal float Cost(Cell cell) => distance[cell.Y * width + cell.X];

        private void Push(HeapEntry entry)
        {
            int index = heap.Count;
            heap.Add(entry);
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (heap[parent].Cost <= entry.Cost) break;
                heap[index] = heap[parent]; index = parent;
            }
            heap[index] = entry;
        }

        private HeapEntry Pop()
        {
            var result = heap[0];
            var last = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return result;
            int index = 0;
            while (index * 2 + 1 < heap.Count)
            {
                int child = index * 2 + 1;
                if (child + 1 < heap.Count && heap[child + 1].Cost < heap[child].Cost) child++;
                if (last.Cost <= heap[child].Cost) break;
                heap[index] = heap[child]; index = child;
            }
            heap[index] = last;
            return result;
        }

        private readonly struct HeapEntry
        {
            internal readonly int Index;
            internal readonly float Cost;
            internal HeapEntry(int index, float cost) { Index = index; Cost = cost; }
        }
    }
}
