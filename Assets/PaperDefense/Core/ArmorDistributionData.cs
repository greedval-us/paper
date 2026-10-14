using System;
namespace PaperDefense.Core
{
    [Serializable] public sealed class ArmorDistributionData
    {
        public int Version, FacingQuarterTurns;
        public int[] Points;
        public ArmorDistributionData() { }
        public ArmorDistributionData(ArmorDistribution source)
        {
            Version=1;FacingQuarterTurns=source.FacingQuarterTurns;Points=new int[ArmorDistribution.SectorCount];
            for(int i=0;i<Points.Length;i++)Points[i]=source.Points[i];
        }
        public ArmorDistribution ToDistribution()
        {
            if(Version>1)throw new NotSupportedException("Newer reinforcement rules.");
            // Unity's serializer materializes null inline classes as an empty version-zero object.
            if(Version==0&&FacingQuarterTurns==0&&(Points==null||Points.Length==0))return null;
            if(Version!=1)throw new ArgumentException("Invalid reinforcement version.");
            return new ArmorDistribution(Points,FacingQuarterTurns);
        }
        public ArmorDistributionData CreateCopy()=>new ArmorDistributionData{Version=Version,FacingQuarterTurns=FacingQuarterTurns,Points=Points==null?null:(int[])Points.Clone()};
    }
}
