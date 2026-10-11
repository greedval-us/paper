using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PaperDefense.Core
{
    public readonly struct BreachRecord
    {
        public readonly Cell Cell;
        public readonly long Tick;
        public readonly float Elapsed;
        public readonly StructureKind Kind;
        public readonly MaterialTier Tier;
        public readonly int Investment;
        public BreachRecord(Cell cell,long tick,float elapsed,StructureKind kind,MaterialTier tier,int investment)
        {Cell=cell;Tick=tick;Elapsed=elapsed;Kind=kind;Tier=tier;Investment=investment;}
    }

    /// <summary>Fixed-size aggregates plus the last 32 breaches, never a per-hit journal.</summary>
    public sealed class RunStatistics
    {
        public int NavigationRebuilds { get; private set; }
        public double NavigationTotalMilliseconds { get; private set; }
        public double NavigationMaxMilliseconds { get; private set; }
        public void RecordNavigation(double milliseconds)
        {
            NavigationRebuilds++;
            NavigationTotalMilliseconds += milliseconds;
            NavigationMaxMilliseconds = Math.Max(NavigationMaxMilliseconds, milliseconds);
        }
        readonly float[] damage=new float[5],idle=new float[5];
        readonly int[] launches=new int[5];
        readonly int[] built=new int[15];
        readonly List<BreachRecord> breaches=new List<BreachRecord>(32);
        readonly List<RunUpgrade> upgrades=new List<RunUpgrade>(3);
        readonly ReadOnlyCollection<BreachRecord> breachView;
        readonly ReadOnlyCollection<RunUpgrade> upgradeView;
        public int BuildInvestment {get;private set;}
        public int UpgradeInvestment {get;private set;}
        public int RefundedInk {get;private set;}
        public int LostInvestment {get;private set;}
        public int PeakAliveEnemies {get;private set;}
        public int TotalStructuresBuilt {get;private set;}
        public int DestroyedStructures {get;private set;}
        public IReadOnlyList<BreachRecord> Breaches=>breachView;
        public IReadOnlyList<RunUpgrade> Upgrades=>upgradeView;
        public RunStatistics(){breachView=breaches.AsReadOnly();upgradeView=upgrades.AsReadOnly();}
        public float Damage(StructureKind kind)=>damage[(int)kind];
        public float IdleSeconds(StructureKind kind)=>idle[(int)kind];
        public int Launches(StructureKind kind)=>launches[(int)kind];
        public int BuiltCount(StructureKind kind,MaterialTier tier)=>built[(int)kind*3+(int)tier];
        public void RecordDamage(StructureKind kind,float amount){if(amount>0&&!float.IsNaN(amount)&&!float.IsInfinity(amount))damage[(int)kind]+=amount;}
        public void RecordIdle(StructureKind kind,float seconds){if(seconds>0&&!float.IsNaN(seconds)&&!float.IsInfinity(seconds))idle[(int)kind]+=seconds;}
        public void RecordLaunch(StructureKind kind)=>launches[(int)kind]++;
        public void RecordBuild(int cost,int segments,StructureKind kind=StructureKind.Wall,MaterialTier tier=MaterialTier.Pencil)
        {BuildInvestment+=Math.Max(0,cost);segments=Math.Max(0,segments);TotalStructuresBuilt+=segments;built[(int)kind*3+(int)tier]+=segments;}
        public void RecordUpgrade(int cost)=>UpgradeInvestment+=Math.Max(0,cost);
        public void RecordRefund(int refund)=>RefundedInk+=Math.Max(0,refund);
        public void RecordLoss(Cell cell,long tick,float elapsed,StructureKind kind,MaterialTier tier,int investment)
        {
            LostInvestment+=Math.Max(0,investment);DestroyedStructures++;
            if(breaches.Count==32)breaches.RemoveAt(0);
            breaches.Add(new BreachRecord(cell,tick,elapsed,kind,tier,Math.Max(0,investment)));
        }
        public void RecordUpgradeChoice(RunUpgrade upgrade){if(upgrades.Count<16)upgrades.Add(upgrade);}
        public void ObserveEnemies(int alive)=>PeakAliveEnemies=Math.Max(PeakAliveEnemies,alive);
    }

    public sealed class RunStatisticsView
    {
        readonly RunStatistics source;
        internal RunStatisticsView(RunStatistics source){this.source=source;}
        public int BuildInvestment=>source.BuildInvestment;
        public int UpgradeInvestment=>source.UpgradeInvestment;
        public int RefundedInk=>source.RefundedInk;
        public int LostInvestment=>source.LostInvestment;
        public int PeakAliveEnemies=>source.PeakAliveEnemies;
        public int TotalStructuresBuilt=>source.TotalStructuresBuilt;
        public int DestroyedStructures=>source.DestroyedStructures;
        public int NavigationRebuilds=>source.NavigationRebuilds;
        public double NavigationTotalMilliseconds=>source.NavigationTotalMilliseconds;
        public double NavigationMaxMilliseconds=>source.NavigationMaxMilliseconds;
        public IReadOnlyList<BreachRecord> Breaches=>source.Breaches;
        public IReadOnlyList<RunUpgrade> Upgrades=>source.Upgrades;
        public float Damage(StructureKind kind)=>source.Damage(kind);
        public float IdleSeconds(StructureKind kind)=>source.IdleSeconds(kind);
        public int Launches(StructureKind kind)=>source.Launches(kind);
        public int BuiltCount(StructureKind kind,MaterialTier tier)=>source.BuiltCount(kind,tier);
    }
}
