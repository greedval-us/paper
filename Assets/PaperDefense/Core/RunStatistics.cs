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
        public readonly string ContentId;
        public readonly MaterialTier Tier;
        public readonly int Investment;
        public BreachRecord(Cell cell,long tick,float elapsed,StructureKind kind,MaterialTier tier,int investment,string contentId=null)
        {ContentId=contentId??LegacyContentIds.Weapon(kind);Cell=cell;Tick=tick;Elapsed=elapsed;Kind=kind;Tier=tier;Investment=investment;}
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
        sealed class WeaponMetrics { internal float Damage,Idle;internal int Launches;internal readonly Dictionary<MaterialTier,int> Built=new Dictionary<MaterialTier,int>(); }
        readonly Dictionary<string,WeaponMetrics> metrics=new Dictionary<string,WeaponMetrics>(StringComparer.Ordinal);
        readonly List<string> weaponIds=new List<string>();
        public IReadOnlyList<string> WeaponIds=>weaponIds.AsReadOnly();
        public float TotalDamage {get;private set;} public float TotalIdleSeconds {get;private set;}
        public float TotalRepairedHealth { get; private set; }
        public int TotalGeneratorIncome { get; private set; }
        public int TotalSpawnedEnemies { get; private set; }
        public int BaseHits { get; private set; }
        public float BaseDamageTaken { get; private set; }
        readonly int[] spawnedByKind = new int[6], spawnedByDrawingTier = new int[3];
        public IReadOnlyList<int> SpawnedByKind => Array.AsReadOnly(spawnedByKind);
        public IReadOnlyList<int> SpawnedByDrawingTier => Array.AsReadOnly(spawnedByDrawingTier);
        static int AddCount(int value, int amount) => (int)Math.Min(int.MaxValue, (long)value + Math.Max(0, amount));
        static float AddMeasurement(float value, float amount) =>
            amount > 0 && !float.IsNaN(amount) && !float.IsInfinity(amount)
                ? (float)Math.Min(float.MaxValue, (double)value + amount) : value;
        public void RecordRepair(float actual) => TotalRepairedHealth = AddMeasurement(TotalRepairedHealth, actual);
        public void RecordGeneratorIncome(int actual) => TotalGeneratorIncome = AddCount(TotalGeneratorIncome, actual);
        public void RecordSpawn(EnemyKind kind, DrawingTier tier)
        {
            if (!Enum.IsDefined(typeof(EnemyKind), kind) || !Enum.IsDefined(typeof(DrawingTier), tier)) return;
            TotalSpawnedEnemies = AddCount(TotalSpawnedEnemies, 1);
            spawnedByKind[(int)kind] = AddCount(spawnedByKind[(int)kind], 1);
            spawnedByDrawingTier[(int)tier] = AddCount(spawnedByDrawingTier[(int)tier], 1);
        }
        public void RecordBaseDamage(float actual)
        {
            if (!(actual > 0) || float.IsNaN(actual) || float.IsInfinity(actual)) return;
            BaseHits = AddCount(BaseHits, 1);
            BaseDamageTaken = AddMeasurement(BaseDamageTaken, actual);
        }
        WeaponMetrics Metrics(string id,bool create)
        {
            if(id==null)return null;
            if(metrics.TryGetValue(id,out var value))return value;
            if(!create)return null;
            value=new WeaponMetrics();metrics.Add(id,value);weaponIds.Add(id);return value;
        }
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
        public int BlotsCleared {get;private set;}
        public void RecordBlotsCleared(int count)
        {if(count>0) BlotsCleared=(int)Math.Min(int.MaxValue,(long)BlotsCleared+count);}
        public IReadOnlyList<BreachRecord> Breaches=>breachView;
        public IReadOnlyList<RunUpgrade> Upgrades=>upgradeView;
        public RunStatistics(){breachView=breaches.AsReadOnly();upgradeView=upgrades.AsReadOnly();}
        public float Damage(StructureKind kind)=>Damage(LegacyContentIds.Weapon(kind));
        public float Damage(string id)=>Metrics(id,false)?.Damage??0;
        public float IdleSeconds(StructureKind kind)=>IdleSeconds(LegacyContentIds.Weapon(kind));
        public float IdleSeconds(string id)=>Metrics(id,false)?.Idle??0;
        public int Launches(StructureKind kind)=>Launches(LegacyContentIds.Weapon(kind));
        public int Launches(string id)=>Metrics(id,false)?.Launches??0;
        public int BuiltCount(StructureKind kind,MaterialTier tier)=>BuiltCount(LegacyContentIds.Weapon(kind),tier);
        public int BuiltCount(string id,MaterialTier tier){var value=Metrics(id,false);return value!=null&&value.Built.TryGetValue(tier,out int count)?count:0;}
        public void RecordDamage(StructureKind kind,float amount)=>RecordDamage(LegacyContentIds.Weapon(kind),amount);
        public void RecordDamage(string id,float amount){if(amount>0&&!float.IsNaN(amount)&&!float.IsInfinity(amount)){var value=Metrics(id,true);if(value!=null){value.Damage+=amount;TotalDamage+=amount;}}}
        public void RecordIdle(StructureKind kind,float seconds)=>RecordIdle(LegacyContentIds.Weapon(kind),seconds);
        public void RecordIdle(string id,float seconds){if(seconds>0&&!float.IsNaN(seconds)&&!float.IsInfinity(seconds)){var value=Metrics(id,true);if(value!=null){value.Idle+=seconds;TotalIdleSeconds+=seconds;}}}
        public void RecordLaunch(StructureKind kind)=>RecordLaunch(LegacyContentIds.Weapon(kind));
        public void RecordLaunch(string id){var value=Metrics(id,true);if(value!=null)value.Launches++;}
        public void RecordBuild(int cost,int segments,StructureKind kind=StructureKind.Wall,MaterialTier tier=MaterialTier.Pencil)=>RecordBuild(cost,segments,LegacyContentIds.Weapon(kind),tier);
        public void RecordBuild(int cost,int segments,string id,MaterialTier tier)
        {BuildInvestment+=Math.Max(0,cost);segments=Math.Max(0,segments);TotalStructuresBuilt+=segments;var value=Metrics(id,true);if(value!=null){value.Built.TryGetValue(tier,out int count);value.Built[tier]=count+segments;}}
        public void RecordUpgrade(int cost)=>UpgradeInvestment+=Math.Max(0,cost);
        public void RecordRefund(int refund)=>RefundedInk+=Math.Max(0,refund);
        public void RecordLoss(Cell cell,long tick,float elapsed,StructureKind kind,MaterialTier tier,int investment,string contentId=null)
        {
            LostInvestment+=Math.Max(0,investment);DestroyedStructures++;
            if(breaches.Count==32)breaches.RemoveAt(0);
            breaches.Add(new BreachRecord(cell,tick,elapsed,kind,tier,Math.Max(0,investment),contentId));
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
        public int BlotsCleared=>source.BlotsCleared;
        public int NavigationRebuilds=>source.NavigationRebuilds;
        public double NavigationTotalMilliseconds=>source.NavigationTotalMilliseconds;
        public double NavigationMaxMilliseconds=>source.NavigationMaxMilliseconds;
        public IReadOnlyList<BreachRecord> Breaches=>source.Breaches;
        public IReadOnlyList<RunUpgrade> Upgrades=>source.Upgrades;
        public IReadOnlyList<string> WeaponIds=>source.WeaponIds;
        public float TotalDamage=>source.TotalDamage;public float TotalIdleSeconds=>source.TotalIdleSeconds;
        public float TotalRepairedHealth => source.TotalRepairedHealth;
        public int TotalGeneratorIncome => source.TotalGeneratorIncome;
        public int TotalSpawnedEnemies => source.TotalSpawnedEnemies;
        public int BaseHits => source.BaseHits;
        public float BaseDamageTaken => source.BaseDamageTaken;
        public IReadOnlyList<int> SpawnedByKind => source.SpawnedByKind;
        public IReadOnlyList<int> SpawnedByDrawingTier => source.SpawnedByDrawingTier;
        public float Damage(string id)=>source.Damage(id);public float IdleSeconds(string id)=>source.IdleSeconds(id);public int Launches(string id)=>source.Launches(id);
        public int BuiltCount(string id,MaterialTier tier)=>source.BuiltCount(id,tier);
        public float Damage(StructureKind kind)=>source.Damage(kind);
        public float IdleSeconds(StructureKind kind)=>source.IdleSeconds(kind);
        public int Launches(StructureKind kind)=>source.Launches(kind);
        public int BuiltCount(StructureKind kind,MaterialTier tier)=>source.BuiltCount(kind,tier);
    }
}
