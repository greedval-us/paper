using System;
using PaperDefense.Core;

namespace PaperDefense.Validation
{
    /// <summary>Bearings for the single placement order issued by a simulated player.</summary>
    public static class BattleFacingOrders
    {
        public static float Toward(Cell cell, Cell objective) => Toward(cell, new Point2(objective.X + .5f, objective.Y + .5f));
        public static float Toward(Cell cell, Point2 target) =>
            (float)(Math.Atan2(target.Y - cell.Y - .5f, target.X - cell.X - .5f) * 180 / Math.PI);
    }
}
