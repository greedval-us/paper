using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Reusable admission counts bound crowd overlap without pairwise separation.</summary>
    internal sealed class EnemyCrowdGrid
    {
        private readonly int width, height, capacity;
        private readonly int[] counts;

        internal EnemyCrowdGrid(int width, int height, int capacity)
        {
            this.width = width; this.height = height; this.capacity = Math.Max(1, capacity);
            counts = new int[width * height];
        }

        internal void Rebuild(IReadOnlyList<EnemyState> enemies)
        {
            Array.Clear(counts, 0, counts.Length);
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i].Health > 0) counts[Index(enemies[i].Position)]++;
        }

        internal int Count(Cell cell) => Inside(cell) ? counts[cell.Y * width + cell.X] : capacity;
        internal bool CanEnter(Cell cell) => Inside(cell) && Count(cell) < capacity;
        internal Point2 Add(Cell cell)
        {
            int index = cell.Y * width + cell.X;
            int slot = counts[index]++;
            // All spawn points stay inside their border cell. Six slots permit modest overlap.
            float angle = (slot % capacity) * (float)(Math.PI * 2 / capacity);
            return new Point2(cell.X + 0.5f + (float)Math.Cos(angle) * 0.22f,
                cell.Y + 0.5f + (float)Math.Sin(angle) * 0.22f);
        }

        internal bool TryMove(Point2 from, Point2 to)
        {
            int oldIndex = Index(from), nextIndex = Index(to);
            if (oldIndex == nextIndex) return true;
            if (counts[nextIndex] >= capacity) return false;
            counts[oldIndex] = Math.Max(0, counts[oldIndex] - 1);
            counts[nextIndex]++;
            return true;
        }

        private bool Inside(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height;
        private int Index(Point2 point) => Math.Max(0, Math.Min(height - 1, (int)Math.Floor(point.Y))) * width +
            Math.Max(0, Math.Min(width - 1, (int)Math.Floor(point.X)));
    }
}
