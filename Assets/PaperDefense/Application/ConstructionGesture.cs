using System;
using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    /// <summary>One map-owned drag. Device coordinates, UI hit testing and payment belong to its callers.</summary>
    public sealed class ConstructionGesture
    {
        readonly List<Cell> cells = new List<Cell>();
        readonly HashSet<Cell> visited = new HashSet<Cell>();
        readonly IReadOnlyList<Cell> readOnlyCells;
        Cell last;
        bool continuous;

        public bool Active { get; private set; }
        public IReadOnlyList<Cell> Cells => readOnlyCells;

        public ConstructionGesture() => readOnlyCells = cells.AsReadOnly();

        public void Begin(Cell cell, bool continuous)
        {
            Cancel();
            Active = true;
            this.continuous = continuous;
            last = cell;
            Add(cell);
        }

        public void Move(Cell cell)
        {
            if (!Active || cell.Equals(last)) return;
            if (continuous) AddLine(last, cell);
            else { cells.Clear(); visited.Clear(); Add(cell); }
            last = cell;
        }

        public void Cancel()
        {
            Active = false;
            cells.Clear();
            visited.Clear();
        }

        void Add(Cell cell) { if (visited.Add(cell)) cells.Add(cell); }

        void AddLine(Cell from, Cell to)
        {
            int x = from.X, y = from.Y, dx = Math.Abs(to.X - x), sx = x < to.X ? 1 : -1;
            int dy = -Math.Abs(to.Y - y), sy = y < to.Y ? 1 : -1, error = dx + dy;
            while (true)
            {
                Add(new Cell(x, y));
                if (x == to.X && y == to.Y) break;
                int twiceError = error * 2;
                if (twiceError >= dy) { error += dy; x += sx; }
                if (twiceError <= dx) { error += dx; y += sy; }
            }
        }
    }
}
