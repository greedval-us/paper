using System.Collections.Generic;

namespace PaperDefense.Core
{
    internal interface IBlotContext
    {
        bool TryFindNearest(Point2 origin, float range, out Cell cell);
        int Clear(Cell center, float radius);
    }
    internal interface IBlotRangeContext : IBlotContext
    {bool TryFindNearest(Point2 origin,float minimumRange,float maximumRange,out Cell cell);}

    internal static class BlotQueries
    {
        internal static bool TryFindNearest(IReadOnlyList<BlotState> blots, Point2 origin, float range, out Cell cell)
            =>TryFindNearest(blots,origin,0,range,out cell);
        internal static bool TryFindNearest(IReadOnlyList<BlotState> blots,Point2 origin,float minimumRange,float range,out Cell cell)
        {
            cell = default;
            if (blots == null || !Finite(origin.X) || !Finite(origin.Y) || !Finite(range) || range < 0 || !Finite(minimumRange) || minimumRange<0 || minimumRange>range) return false;
            double minimumSquared=(double)minimumRange*minimumRange;
            double nearestSquared = (double)range * range;
            bool found = false;
            for (int i = 0; i < blots.Count; i++)
            {
                BlotState blot = blots[i];
                if (blot == null || !(blot.Remaining > 0)) continue;
                double dx = blot.Cell.X + .5 - origin.X, dy = blot.Cell.Y + .5 - origin.Y;
                double squared = dx * dx + dy * dy;
                if(squared<minimumSquared)continue;
                bool earlierCell = blot.Cell.Y < cell.Y || (blot.Cell.Y == cell.Y && blot.Cell.X < cell.X);
                if (squared > nearestSquared || (found && squared == nearestSquared && !earlierCell)) continue;
                nearestSquared = squared;
                cell = blot.Cell;
                found = true;
            }
            return found;
        }

        internal static int Clear(IReadOnlyList<BlotState> blots, Cell center, float radius)
        {
            if (blots == null || !Finite(radius) || radius < 0) return 0;
            double radiusSquared = (double)radius * radius;
            int count = 0;
            for (int i = 0; i < blots.Count; i++)
            {
                BlotState blot = blots[i];
                if (blot == null || !(blot.Remaining > 0)) continue;
                double dx = (double)blot.Cell.X - center.X, dy = (double)blot.Cell.Y - center.Y;
                if (dx * dx + dy * dy > radiusSquared) continue;
                blot.Remaining = 0;
                count++;
            }
            return count;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
