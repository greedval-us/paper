using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Owns bounded corpse marks and their lifecycle; callers own buffs, costs and cooldowns.</summary>
    internal sealed class CorpseBlotSystem : IBlotRangeContext
    {
        readonly int width, height, cellCount;
        readonly List<BlotState> blots;
        BlotState[] cells;
        bool[] listed;
        readonly Action<SimulationEventKind,Cell> publish;
        readonly Action<Cell> publishCleared;

        internal IReadOnlyList<BlotState> Blots { get; }

        internal CorpseBlotSystem(int width, int height,Action<SimulationEventKind,Cell> publish=null)
        {
            if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width));
            this.width = width; this.height = height; cellCount = width * height;
            this.publish=publish;
            if(publish!=null)publishCleared=cell=>publish(SimulationEventKind.BlotCleared,cell);
            blots = new List<BlotState>(Math.Min(256, cellCount));
            Blots = blots.AsReadOnly();
        }

        internal void Add(Cell cell, float lifetime)
        {
            if (!Inside(cell) || lifetime <= 0) return;
            if (cells == null)
            {
                cells = new BlotState[cellCount];
                listed = new bool[cellCount];
            }
            int index = cell.Y * width + cell.X;
            var blot = cells[index];
            bool appeared=blot==null||blot.Remaining<=0;
            if (blot == null) cells[index] = blot = new BlotState { Cell = cell };
            if (!listed[index])
            {
                blots.Add(blot); listed[index] = true;
            }
            // A cell owns one ordinary mark, including when its previous mark was consumed this step.
            blot.Remaining = Math.Max(0, lifetime);
            if(appeared)publish?.Invoke(SimulationEventKind.BlotSpawned,cell);
        }

        internal void Advance(float delta)
        {
            if (delta <= 0) return;
            for (int i = 0; i < blots.Count; i++)
            {
                var blot=blots[i];float previous=blot.Remaining;
                blot.Remaining=Math.Max(0,blot.Remaining-delta);
                if(previous>0&&blot.Remaining<=0)publish?.Invoke(SimulationEventKind.BlotExpired,blot.Cell);
            }
        }

        internal bool TryConsume(Cell cell)
        {
            if (cells == null || !Inside(cell)) return false;
            var blot = cells[cell.Y * width + cell.X];
            if (blot == null || blot.Remaining <= 0) return false;
            blot.Remaining = 0;
            return true;
        }

        // Aging and ordered consumption share one compaction pass at the end of a simulation step.
        internal void FinishStep() => Compact();

        internal bool HasManualTarget(Cell center, float radius)
        {
            float radiusSquared = radius * radius;
            for (int i = 0; i < blots.Count; i++)
            {
                var blot = blots[i];
                float dx = blot.Cell.X - center.X, dy = blot.Cell.Y - center.Y;
                if (blot.Remaining > 0 && dx * dx + dy * dy <= radiusSquared) return true;
            }
            return false;
        }

        internal void ClearManual(Cell center, float radius)
        {
            // Retain the manual tool's historical float arithmetic separately from automatic cleanup.
            float radiusSquared = radius * radius;
            for (int i = 0; i < blots.Count; i++)
            {
                var blot = blots[i];
                float dx = blot.Cell.X - center.X, dy = blot.Cell.Y - center.Y;
                if (dx * dx + dy * dy <= radiusSquared)
                {
                    if(blot.Remaining>0)publish?.Invoke(SimulationEventKind.BlotCleared,blot.Cell);
                    blot.Remaining = 0;
                }
            }
            Compact();
        }

        public bool TryFindNearest(Point2 origin, float range, out Cell cell)
            => BlotQueries.TryFindNearest(blots, origin, range, out cell);
        public bool TryFindNearest(Point2 origin,float minimumRange,float maximumRange,out Cell cell)
            =>BlotQueries.TryFindNearest(blots,origin,minimumRange,maximumRange,out cell);

        public int Clear(Cell center, float radius)
        {
            int cleared = BlotQueries.Clear(blots, center, radius,publishCleared);
            Compact();
            return cleared;
        }

        void Compact()
        {
            int write = 0;
            for (int read = 0; read < blots.Count; read++)
            {
                var blot = blots[read];
                if (blot.Remaining > 0) blots[write++] = blot;
                else listed[blot.Cell.Y * width + blot.Cell.X] = false;
            }
            if (write < blots.Count) blots.RemoveRange(write, blots.Count - write);
        }

        bool Inside(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height;
    }
}
