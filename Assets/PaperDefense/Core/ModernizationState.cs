namespace PaperDefense.Core
{
    public readonly struct ModernizationView
    {
        public ModernizationRanks Ranks {get;} public int Revision {get;}
        public int Rank(ModernizationDirection direction)=>Ranks.Rank(direction);
        public int StepsUsed=>Ranks.StepsUsed;public int ColorRank=>Ranks.ColorRank;
        public bool IsWorking {get;} public float WorkRemaining {get;} public float WorkDuration {get;}
        public ModernizationDirection? PendingDirection {get;} public InkColor PendingColor {get;}
        internal ModernizationView(ModernizationState state)
        {Ranks=state.Ranks;Revision=state.Revision;IsWorking=state.Pending!=null;WorkRemaining=state.Remaining;WorkDuration=state.Pending?.WorkSeconds??0;PendingDirection=state.Pending==null||state.Pending.IsColor?(ModernizationDirection?)null:state.Pending.Direction;PendingColor=state.Pending!=null&&state.Pending.IsColor?state.Pending.Color:InkColor.None;}
    }
    internal sealed class ModernizationState
    {
        internal readonly ResolvedEntry Baseline;
        internal ModernizationRanks Ranks;internal int Revision;internal ModernizationQuote Pending;internal float Remaining;
        internal ModernizationState(ResolvedEntry baseline){Baseline=baseline;}
    }
    public sealed class ModernizationQuote
    {
        public bool Valid {get;} public string Reason {get;} public int StructureId {get;} public int Revision {get;}
        public ModernizationDirection Direction {get;} public InkColor Color {get;} public bool IsColor {get;}
        public int CurrentRank {get;} public int NextRank {get;} public int Cost {get;} public float WorkSeconds {get;}
        public ResolvedEntry Before {get;} public ResolvedEntry After {get;}
        public ColorEffectPayload BeforeColor {get;} public ColorEffectPayload AfterColor {get;}
        internal System.Guid RunId {get;}
        internal ModernizationRanks Result {get;}
        internal ModernizationQuote(System.Guid runId,int id,int revision,ModernizationDirection direction,InkColor color,bool isColor,int current,int next,int cost,float seconds,
            ResolvedEntry before,ResolvedEntry after,ColorEffectPayload beforeColor,ColorEffectPayload afterColor,ModernizationRanks result,string reason)
        {RunId=runId;StructureId=id;Revision=revision;Direction=direction;Color=color;IsColor=isColor;CurrentRank=current;NextRank=next;Cost=cost;WorkSeconds=seconds;Before=before;After=after;BeforeColor=beforeColor;AfterColor=afterColor;Result=result;Reason=reason??"";Valid=Reason.Length==0;}
    }
}
