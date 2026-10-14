using System;
using System.Collections.Generic;
namespace PaperDefense.Core
{
    public sealed class ArmorDistribution
    {
        public const int SectorCount=4, Budget=12;
        public IReadOnlyList<int> Points { get; }
        public int FacingQuarterTurns { get; }
        public int Remaining { get; }
        public ArmorDistribution(IEnumerable<int> points,int facingQuarterTurns=0)
        {
            if(points==null)throw new ArgumentNullException(nameof(points));
            if(facingQuarterTurns<0||facingQuarterTurns>3)throw new ArgumentOutOfRangeException(nameof(facingQuarterTurns));
            var owned=new List<int>();int total=0;
            foreach(int value in points)
            {
                if(owned.Count==SectorCount||value<0||value>Budget)throw new ArgumentException("Invalid armor allocation.");
                owned.Add(value);total+=value;
            }
            if(owned.Count!=SectorCount||total>Budget)throw new ArgumentException("Invalid armor budget.");
            Points=owned.AsReadOnly();FacingQuarterTurns=facingQuarterTurns;Remaining=Budget-total;
        }
        public int Sector(Point2 sourceOffset)
        {
            if(float.IsNaN(sourceOffset.X)||float.IsNaN(sourceOffset.Y)||float.IsInfinity(sourceOffset.X)||float.IsInfinity(sourceOffset.Y))
                throw new ArgumentException("Invalid attack direction.");
            double angle=Math.Atan2(sourceOffset.X,sourceOffset.Y)*180/Math.PI;
            return ((int)Math.Floor((angle+45)/90)-FacingQuarterTurns+8)%SectorCount;
        }
        public float Multiplier(Point2 sourceOffset)
        {
            if(Math.Abs(sourceOffset.X)<.000001f&&Math.Abs(sourceOffset.Y)<.000001f)
            {int least=Budget;foreach(int value in Points)least=Math.Min(least,value);return least/3f;}
            return Points[Sector(sourceOffset)]/3f;
        }
        public ArmorDistribution WithPoint(int sector,int delta)
        {
            if(sector<0||sector>=SectorCount)throw new ArgumentOutOfRangeException(nameof(sector));
            var points=new List<int>(Points);points[sector]=checked(points[sector]+delta);return new ArmorDistribution(points,FacingQuarterTurns);
        }
        public ArmorDistribution Rotate()=>new ArmorDistribution(Points,(FacingQuarterTurns+1)%SectorCount);
        public static ArmorDistribution Uniform(int facing=0)=>new ArmorDistribution(new[]{3,3,3,3},facing);
    }
}
