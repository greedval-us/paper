using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Coarse reusable buckets make tower queries local rather than scanning the full wave.</summary>
    internal sealed class EnemySpatialIndex
    {
        private const int BucketSize = 4;
        private readonly int columns, rows;
        private readonly List<EnemyState>[] buckets;

        internal EnemySpatialIndex(int width, int height)
        {
            columns = (width + BucketSize - 1) / BucketSize;
            rows = (height + BucketSize - 1) / BucketSize;
            buckets = new List<EnemyState>[columns * rows];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<EnemyState>(16);
        }

        internal void Rebuild(List<EnemyState> enemies)
        {
            foreach (var bucket in buckets) bucket.Clear();
            foreach (var enemy in enemies)
            {
                if (enemy.Health <= 0) continue;
                int x = Clamp((int)(enemy.Position.X / BucketSize), columns - 1);
                int y = Clamp((int)(enemy.Position.Y / BucketSize), rows - 1);
                buckets[y * columns + x].Add(enemy);
            }
        }

        internal void Query(Point2 point, float radius, List<EnemyState> result)
        {
            result.Clear();
            int minX = Clamp((int)Math.Floor((point.X - radius) / BucketSize), columns - 1);
            int maxX = Clamp((int)Math.Floor((point.X + radius) / BucketSize), columns - 1);
            int minY = Clamp((int)Math.Floor((point.Y - radius) / BucketSize), rows - 1);
            int maxY = Clamp((int)Math.Floor((point.Y + radius) / BucketSize), rows - 1);
            float radiusSquared = radius * radius;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    foreach (var enemy in buckets[y * columns + x])
                    {
                        if (enemy.Health <= 0) continue;
                        float dx = enemy.Position.X - point.X, dy = enemy.Position.Y - point.Y;
                        if (dx * dx + dy * dy <= radiusSquared) result.Add(enemy);
                    }
        }

        private static int Clamp(int value, int max) => Math.Max(0, Math.Min(max, value));
    }
}
