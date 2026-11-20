using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum ModernizationDirection { Power, Mechanics, Range, Resilience }

    public readonly struct ModernizationRanks
    {
        public int Power { get; } public int Mechanics { get; } public int Range { get; } public int Resilience { get; }
        public InkColor Color { get; } public int ColorRank { get; }
        public int StepsUsed => Power + Mechanics + Range + Resilience;
        public ModernizationRanks(int power=0, int mechanics=0, int range=0, int resilience=0, InkColor color=InkColor.None, int colorRank=0)
        {
            if (power<0||power>3||mechanics<0||mechanics>3||range<0||range>3||resilience<0||resilience>3||colorRank<0||colorRank>3)
                throw new ArgumentOutOfRangeException(nameof(power));
            if (power+mechanics+range+resilience>6 || (power==3?1:0)+(mechanics==3?1:0)+(range==3?1:0)+(resilience==3?1:0)>1)
                throw new ArgumentException("At most six ordinary steps and one direction at rank III are allowed.");
            if (!Enum.IsDefined(typeof(InkColor),color) || (color==InkColor.None && colorRank!=0)) throw new ArgumentException("Color rank requires a color.");
            Power=power;Mechanics=mechanics;Range=range;Resilience=resilience;Color=color;ColorRank=colorRank;
        }
        public int Rank(ModernizationDirection direction)
        {
            switch(direction) { case ModernizationDirection.Power:return Power;case ModernizationDirection.Mechanics:return Mechanics;case ModernizationDirection.Range:return Range;case ModernizationDirection.Resilience:return Resilience;default:throw new ArgumentOutOfRangeException(nameof(direction)); }
        }
        internal ModernizationRanks Next(ModernizationDirection direction) => new ModernizationRanks(Power+(direction==ModernizationDirection.Power?1:0),Mechanics+(direction==ModernizationDirection.Mechanics?1:0),Range+(direction==ModernizationDirection.Range?1:0),Resilience+(direction==ModernizationDirection.Resilience?1:0),Color,ColorRank);
        internal ModernizationRanks WithColor(InkColor color,int rank) => new ModernizationRanks(Power,Mechanics,Range,Resilience,color,rank);
    }

    public static class ModernizationRules
    {
        static readonly float[] power={1,1.30f,1.65f,2.10f}, mechanics={1,1.20f,1.45f,1.75f}, range={1,1.10f,1.22f,1.36f}, health={1,1.35f,1.85f,2.60f};
        // Percentage of the original pencil cost for each additional rank (I, II, III).
        static readonly int[,] prices = {
            { 20, 30, 40 }, // Power
            { 15, 22, 30 }, // Mechanics
            { 20, 32, 45 }, // Range
            { 25, 40, 60 }  // Resilience
        };
        public static int OrdinaryCost(int baseCost,ModernizationDirection direction,int nextRank)
        {
            ValidateRank(nextRank);if(baseCost<0)throw new ArgumentOutOfRangeException(nameof(baseCost));
            if(!Enum.IsDefined(typeof(ModernizationDirection),direction))throw new ArgumentOutOfRangeException(nameof(direction));
            return checked((int)Math.Max(4,((long)baseCost*prices[(int)direction,nextRank-1]+99)/100));
        }
        public static int ColorCost(int baseCost,int nextRank)
        {
            ValidateRank(nextRank);
            if(baseCost<0)throw new ArgumentOutOfRangeException(nameof(baseCost));
            long firstRank = Math.Max(2, ((long)baseCost + 9) / 10);
            return checked((int)(firstRank * nextRank));
        }
        public static float WorkSeconds(int nextRank) {ValidateRank(nextRank);return nextRank+1;}
        static void ValidateRank(int rank) {if(rank<1||rank>3)throw new ArgumentOutOfRangeException(nameof(rank));}
        public static ResolvedEntry Resolve(ResolvedEntry baseline,ModernizationRanks ranks)
        {
            if(baseline==null)throw new ArgumentNullException(nameof(baseline));
            if(baseline.Kind==AlbumEntryKind.Structure)throw new ArgumentException("Field modernization requires a tower.",nameof(baseline));
            var weapons=new List<ResolvedWeapon>();
            foreach(var w in baseline.Weapons)
                weapons.Add(new ResolvedWeapon(w.SlotIndex,w.SourceId,w.Family,w.Attack,
                    w.Damage*(w.Attack==AttackKind.Cleanup?1:DamageMultiplier(ranks)),w.Range*RangeMultiplier(ranks),w.MinimumRange,
                    WorkInterval(w.Interval,ranks,w.LegacyContinuous?w.MinimumActionInterval:0),w.MagazineSize,WorkInterval(w.ReloadSeconds,ranks,w.LegacyContinuous?w.MinimumActionInterval:0),
                    w.AcquisitionSeconds,w.TargetLimit,w.Radius,w.FlightSeconds,w.JumpRange,w.ConeDegrees,w.SlowSpeedMultiplier,w.SlowSeconds,w.MinimumActionInterval,w.FiringArcDegrees,w.LegacyContinuous));
            return new ResolvedEntry(baseline.Kind,baseline.Material,baseline.Snapshot,baseline.Cost,baseline.Health*health[ranks.Resilience],baseline.Load,baseline.Capacity,
                baseline.BuildSeconds,baseline.Defense,baseline.FrontDefense,baseline.FeedCapacity,baseline.FeedModifier,baseline.RefundFraction,weapons,baseline.Support,
                baseline.HasFieldModernization||ranks.StepsUsed>0);
        }
        internal static float WorkInterval(float original,ModernizationRanks ranks,float floor) => Math.Max(floor,original/mechanics[ranks.Mechanics]);
        internal static float DamageMultiplier(ModernizationRanks ranks) => power[ranks.Power];
        internal static float RangeMultiplier(ModernizationRanks ranks) => range[ranks.Range];
        public static ColorEffectPayload ResolveColor(ColorEffectPayload baseline,int rank)
        {
            if(rank<0||rank>3)throw new ArgumentOutOfRangeException(nameof(rank));
            if(rank==0||baseline.Color==InkColor.None)return default;
            float strength=baseline.Strength;
            if(baseline.Color==InkColor.Red)strength*=rank==1?1:rank==2?1.4f:2;
            else if(baseline.Color==InkColor.Blue)strength=Math.Max(.4f,1-(1-strength)*(rank==1?1:rank==2?1.2571429f:1.4857143f));
            else if(baseline.Color==InkColor.Green)strength=Math.Min(1,strength*(rank==1?1:rank==2?1.4f:1.8f));
            return new ColorEffectPayload(baseline.Color,baseline.Duration,strength,baseline.TickSeconds,baseline.IgnoresDefense);
        }
        public static bool HasAttack(ResolvedEntry entry)
        {if(entry==null)return false;foreach(var w in entry.Weapons)if(w.Attack!=AttackKind.Cleanup&&w.Damage>0)return true;return false;}
        internal static bool HasColorEffect(ColorEffectPayload payload)
        {
            if(payload.Duration<=0)return false;
            switch(payload.Color)
            {
                case InkColor.Red:return payload.Strength>0&&payload.TickSeconds>0;
                case InkColor.Blue:return payload.Strength<1;
                case InkColor.Green:return payload.Strength>0;
                default:return false;
            }
        }
        internal static void InitializeModernizationForTrial(StructureState structure,ModernizationRanks ranks,int maximumRank,GameBalance balance)
        {
            if(structure==null||balance==null)throw new ArgumentNullException();
            if(maximumRank<1||maximumRank>3)throw new ArgumentOutOfRangeException(nameof(maximumRank));
            if(structure.ResolvedEntry?.Kind!=AlbumEntryKind.Tower)throw new ArgumentException("Trial modernization requires a tower.");
            foreach(ModernizationDirection direction in Enum.GetValues(typeof(ModernizationDirection)))if(ranks.Rank(direction)>maximumRank)throw new ArgumentException("Trial rank exceeds technology.");
            if(ranks.ColorRank>maximumRank||(!HasAttack(structure.ResolvedEntry)&&(ranks.Power>0||ranks.Color!=InkColor.None)))throw new ArgumentException("Trial rank is incompatible with the tower.");
            if(ranks.Color!=InkColor.None&&(!MechanicsAvailability.Resolve(balance).ColorCartridges||
                !HasColorEffect(ColorEffectPayload.Capture(ranks.Color,balance.ResolveContent()))))
                throw new ArgumentException("Trial color has no available effect.");
            structure.ModernizationState=new ModernizationState(structure.ResolvedEntry){Ranks=ranks};
            structure.ResolvedEntry=Resolve(structure.ModernizationState.Baseline,ranks);structure.Health=structure.MaxHealth=structure.ResolvedEntry.Health;
            structure.Color=ranks.Color;structure.ColorBudget=new TowerColorBudget(1,3+Math.Max(0,ranks.ColorRank-1));
        }
    }
}
