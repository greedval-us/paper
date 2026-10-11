using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>One reverse Dijkstra field shared by every enemy. Walls have finite breach costs.</summary>
    internal sealed class WeightedFlowField
    {
        private readonly int width, height;
        private readonly float[] distance;
        private readonly float[] traversal;
        private readonly List<HeapEntry> heap = new List<HeapEntry>();
        private static readonly int[] OffsetX = { 1, -1, 0, 0 };
        private static readonly int[] OffsetY = { 0, 0, 1, -1 };

        internal WeightedFlowField(int width, int height)
        { this.width = width; this.height = height; distance = new float[width * height]; traversal = new float[width * height]; }

        internal void Rebuild(Cell goal, StructureState[] grid, float movementSpeed = 1, float breachDamagePerSecond = 50)
        {
            heap.Clear();
            float walkingSeconds = 1 / Math.Max(0.01f, movementSpeed);
            float breachDps = Math.Max(0.01f, breachDamagePerSecond);
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = float.MaxValue;
                var structure = grid[i];
                bool blocker = structure != null && structure.IsComplete && structure.Health > 0 && structure.Kind != StructureKind.Spikes;
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
            // Fixed neighbor order and strict comparison keep equal-cost routes deterministic.
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + OffsetX[direction], ny = y + OffsetY[direction];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int index = ny * width + nx;
                float score = distance[index] + traversal[index];
                if (score < bestCost) { bestCost = score; best = new Cell(nx, ny); }
            }
            return best;
        }

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
