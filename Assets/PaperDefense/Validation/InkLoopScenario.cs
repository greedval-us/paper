using System;
using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Validation
{
    /// <summary>Budgeted workshop campaign driven only by public simulation commands.</summary>
    public sealed class InkLoopScenario
    {
        const float Delta = 1f / 30;
        const int MaximumTicks = 60000;
        static readonly Cell[] Directions = { new Cell(-1,0),new Cell(1,0),new Cell(0,-1),new Cell(0,1),
            new Cell(-1,-1),new Cell(1,-1),new Cell(-1,1),new Cell(1,1) };
        readonly IReadOnlyList<TowerBlueprint> loadout;
        readonly List<Cell> baseCells = new List<Cell>(), wellCells = new List<Cell>();
        bool lastWellActive, capturedAgain, baseRingBuilt, wellRingBuilt;
        int observedEnemyId, basePurchases, wellPurchases;
        long nextOrders;
        public GameSession Session {get;}
        public InkLoopReport Report {get;} = new InkLoopReport();
        public bool Finished => Report.Completed;

        public InkLoopScenario(GameBalance balance, int seed = 1337)
        {
            Session = new GameSession(balance, seed, false);
            loadout = new WorkshopProfile().GetLoadoutBlueprints();
            Report.Seed=seed;Report.Width=balance.Width;Report.Height=balance.Height;
            Report.StartingInk=balance.StartingInk;Report.StartingBaseHealth=balance.BaseHealth;
            Report.PassiveInkPerSecond=balance.PassiveInkPerSecond;Report.WaveInkGrant=balance.WaveInkGrant;
            Report.EarlyLaunchInkPerSecond=balance.EarlyLaunchInkPerSecond;Report.MaxEarlyLaunchInk=balance.MaxEarlyLaunchInk;
            Report.WaveGold=balance.WaveGold;Report.VictoryGold=balance.VictoryGold;
            Report.WellActivationCost=balance.WellActivationCost;Report.WellInkPerWave=balance.WellInkPerWave;
            Report.WellHealth=balance.WellHealth;Report.WaveTuning=new InkLoopWaveTuning[balance.Waves.Count];
            Report.EnemyMechanics=balance.EnableEnemyMechanics;Report.InkWells=balance.EnableInkWells;
            Report.WaveCounts=new int[balance.Waves.Count];
            for(int i=0;i<balance.Waves.Count;i++)
            {
                var wave=balance.Waves[i];Report.WaveCounts[i]=wave.Count;Report.ExpectedKills+=wave.Count;
                Report.WaveTuning[i]=new InkLoopWaveTuning{Count=wave.Count,Sides=wave.Sides,SpawnInterval=wave.SpawnInterval,
                    RunnerFraction=wave.RunnerFraction,GnawerFraction=wave.GnawerFraction,SpitterFraction=wave.SpitterFraction,
                    OutlinedFraction=wave.OutlinedFraction,HatchedFraction=wave.HatchedFraction};
            }
            foreach(int r in new[]{3,4,6,7,8,9})foreach(var direction in Directions)
                baseCells.Add(new Cell(Session.BaseCell.X+r*direction.X,Session.BaseCell.Y+r*direction.Y));
            if(Session.Wells.Count>1)
            {
                var center=Session.Wells[1].Cell;
                foreach(int r in new[]{3,5,7})foreach(var direction in Directions)
                    wellCells.Add(new Cell(center.X+r*direction.X,center.Y+r*direction.Y));
                Activate("Activate initially exposed blue point");
            }
            Observe();
        }

        public void Step()
        {
            if(Finished)return;
            if(Session.Phase==RunPhase.Planning||Session.Phase==RunPhase.Intermission)
            {
                if(Session.PendingUpgrade)
                {
                    var upgrade=Session.WaveIndex==4?RunUpgrade.RapidFire:RunUpgrade.DeepInk;
                    Session.ChooseUpgrade(upgrade);Record("Upgrade",true,0,upgrade.ToString());
                }
                OrderBatteries();
                Session.StartNextWave();Record("StartWave",Session.Phase==RunPhase.Assault,0,"Launch immediately; no waiting-income farming");
            }
            if(Session.TickIndex>=nextOrders)
            {
                nextOrders=Session.TickIndex+60;
                if(Report.WellLosses>0&&Session.Wells.Count>1&&!Session.Wells[1].Active&&Session.Ink>=Session.Balance.WellActivationCost+3*108+120)
                {Activate("Paid recapture after actual enemy destruction");capturedAgain=Session.Wells[1].Active;}
                OrderBatteries();
                PaintAndBlotter();
            }
            Session.Tick(Delta);Observe();
            if(Session.Phase==RunPhase.Victory||Session.Phase==RunPhase.Defeat||Session.TickIndex>=MaximumTicks)Finish();
        }

        void OrderBatteries()
        {
            // Base survival comes first. Before recapture the exposed point receives no defenders.
            if(!baseRingBuilt)baseRingBuilt=BuildRing(Session.BaseCell,"Base pencil perimeter before launch");
            int baseLimit=Session.WaveIndex<2?4:Session.WaveIndex<4?12:48;
            int reserve=!capturedAgain?324+120+Session.Balance.WellActivationCost:wellPurchases<4?108:0;
            if(basePurchases<4)reserve=0;
            foreach(var cell in baseCells)
            {
                if(basePurchases>=baseLimit||Session.Ink<108+reserve)break;
                if(Session.StructureAt(cell)!=null)continue;
                int slot=basePurchases%5==4?2:3;
                if(Build(cell,slot,"Base battery from starting/earned ink"))basePurchases++;
            }
            if(!capturedAgain)return;
            if(!wellRingBuilt)wellRingBuilt=BuildRing(Session.Wells[1].Cell,"Pencil perimeter around recaptured point");
            int wellLimit=Session.WaveIndex<3?4:Session.WaveIndex<5?8:16;
            foreach(var cell in wellCells)
            {
                if(wellPurchases>=wellLimit||Session.Ink<108)break;
                if(Session.StructureAt(cell)!=null)continue;
                int slot=wellPurchases%4==3?1:3;
                if(Build(cell,slot,"Reinforce recaptured blue point with earned ink"))wellPurchases++;
            }
        }

        bool BuildRing(Cell center,string condition)
        {
            var cells=new List<Cell>();const int r=5;
            for(int x=-r;x<=r;x++){cells.Add(new Cell(center.X+x,center.Y-r));cells.Add(new Cell(center.X+x,center.Y+r));}
            for(int y=-r+1;y<r;y++){cells.Add(new Cell(center.X-r,center.Y+y));cells.Add(new Cell(center.X+r,center.Y+y));}
            if(Session.Ink<120)return false;
            int before=Session.Ink;bool success=Session.TryBuild(cells,StructureKind.Wall,MaterialTier.Pencil,out string reason);
            Record("BuildWalls",success,before-Session.Ink,condition,center,null,reason);return success;
        }

        bool Build(Cell cell,int slot,string condition)
        {
            int before=Session.Ink;
            bool success=Session.TryBuildBlueprint(new[]{cell},loadout[slot],MaterialTier.Pencil,InkColor.None,out string reason);
            Record("BuildBlueprint",success,before-Session.Ink,condition,cell,loadout[slot].Id,reason);
            return success;
        }

        void Activate(string condition)
        {
            int before=Session.Ink;bool success=Session.TryActivateWell(1,out string reason);
            Record("ActivateWell",success,before-Session.Ink,condition,Session.Wells[1].Cell,"Blue",reason);
            if(success&&Report.WellLosses>0)Report.Recaptures++;
        }

        void PaintAndBlotter()
        {
            if(Session.ColoredInk(InkColor.Blue)>=5)
                foreach(var tower in Session.Structures)
                {
                    if(tower.Blueprint==null||tower.Color!=InkColor.None||tower.Kind==StructureKind.Mortar)continue;
                    int before=Session.ColoredInk(InkColor.Blue);
                    bool success=Session.TryPaint(tower.Cell,InkColor.Blue,out string reason);
                    Record("Paint",success,0,"One-time cartridge bought with wave payout",tower.Cell,"Blue",reason);
                    if(success)
                    {int spent=before-Session.ColoredInk(InkColor.Blue);Report.ColoredInkSpent+=spent;Report.Commands[Report.Commands.Count-1].ColoredInkSpent=spent;}
                    break;
                }
            if(Session.BlotterCooldownRemaining<=0&&Session.Blots.Count>=4&&Session.Ink>=Session.Balance.BlotterCost+108)
            {
                Cell cell=Session.Blots[0].Cell;int before=Session.Ink;
                bool success=Session.TryBlotter(cell,out string reason);
                Record("Blotter",success,before-Session.Ink,"Clear a naturally produced corpse mark",cell,null,reason);
                if(success)Report.BlotterUses++;
            }
        }

        void Observe()
        {
            Report.PeakAlive=Math.Max(Report.PeakAlive,Session.Enemies.Count);
            Report.PeakBlots=Math.Max(Report.PeakBlots,Session.Blots.Count);
            Report.PeakEnemyProjectiles=Math.Max(Report.PeakEnemyProjectiles,Session.EnemyProjectiles.Count);
            int buffs=0,spitters=0,slows=0;
            foreach(var enemy in Session.Enemies)
            {
                if(enemy.BlotRemaining>0)buffs++;
                if(enemy.BlueRemaining>0)slows++;
                if(enemy.Kind==EnemyKind.Spitter)spitters++;
                if(enemy.Id>observedEnemyId)
                {observedEnemyId=enemy.Id;if(enemy.Kind==EnemyKind.Spitter)Report.SpitterSpawnsObserved++;}
            }
            Report.PeakBuffedEnemies=Math.Max(Report.PeakBuffedEnemies,buffs);
            Report.PeakSlowedEnemies=Math.Max(Report.PeakSlowedEnemies,slows);
            Report.PeakSpitters=Math.Max(Report.PeakSpitters,spitters);
            if(Session.Wells.Count>1)
            {
                var well=Session.Wells[1];
                if(lastWellActive&&!well.Active)
                {Report.WellLosses++;Record("ObservedWellLoss",true,0,"Health reached zero through enemy attacks",well.Cell,"Blue");}
                lastWellActive=well.Active;
                Report.BlueInkEarned=Session.ColoredInk(InkColor.Blue)+Report.ColoredInkSpent;
            }
            if(Session.Phase==RunPhase.Intermission||Session.Phase==RunPhase.Victory)
                if(Report.Waves.Count==0||Report.Waves[Report.Waves.Count-1].Wave!=Session.WaveIndex)
                    Report.Waves.Add(new InkLoopWave{Wave=Session.WaveIndex,Tick=Session.TickIndex,Kills=Session.Kills,
                        BaseHealth=Session.BaseHealth,Ink=Session.Ink,Gold=Session.GoldEarned,BlueInk=Session.ColoredInk(InkColor.Blue),
                        WellActive=Session.Wells.Count>1&&Session.Wells[1].Active,Structures=Session.Structures.Count});
        }

        void Record(string action,bool success,int cost,string condition,Cell cell=default,string blueprint=null,string reason=null)
        {
            Report.Commands.Add(new InkLoopCommand{Tick=Session.TickIndex,Wave=Session.WaveIndex,Action=action,Success=success,
                OrdinaryInkSpent=cost,InkAfter=Session.Ink,Condition=condition,X=cell.X,Y=cell.Y,Blueprint=blueprint,Reason=reason});
            if(!success)Report.FailedCommands++;
        }
        void Finish()
        {
            Report.Completed=true;Report.Phase=Session.Phase.ToString();Report.Tick=Session.TickIndex;
            Report.ElapsedSeconds=Session.Elapsed;Report.Kills=Session.Kills;Report.BaseHealth=Session.BaseHealth;
            Report.Ink=Session.Ink;Report.GoldEarned=Session.GoldEarned;Report.Structures=Session.Structures.Count;
            Report.FlowRebuilds=Session.FlowRebuildCount;Report.TimedOut=Session.Phase!=RunPhase.Victory&&Session.Phase!=RunPhase.Defeat;
            Report.CampaignPassed=Session.Phase==RunPhase.Victory&&Session.Kills==Report.ExpectedKills;
            Report.MechanicsObserved=Report.WellLosses>0&&Report.Recaptures>0&&Report.BlueInkEarned>0&&
                Report.PeakSlowedEnemies>0&&Report.PeakBuffedEnemies>0&&Report.PeakSpitters>0&&Report.PeakEnemyProjectiles>0&&Report.BlotterUses>0;
            Report.Passed=Report.CampaignPassed&&Report.MechanicsObserved;
            ulong hash=14695981039346656037UL;
            Action<long> add=value=>{unchecked{for(int i=0;i<8;i++){hash^=(byte)value;hash*=1099511628211UL;value>>=8;}}};
            add(Session.TickIndex);add((int)Session.Phase);add(Session.Kills);add(Session.Ink);add(Session.GoldEarned);
            add(BitConverter.ToInt32(BitConverter.GetBytes(Session.BaseHealth),0));
            foreach(var command in Report.Commands)
            {add(command.Tick);add(command.X);add(command.Y);add(command.Success?1:0);add(command.OrdinaryInkSpent);add(command.ColoredInkSpent);add(command.InkAfter);}
            Report.Fingerprint=hash.ToString("x16");
        }
    }

    [Serializable] public sealed class InkLoopReport
    {
        public string Scope="Workshop balance supplied by caller, unchanged; zero starter structures. Public commands only; default four blueprints. Blue point deliberately exposed until natural loss, then paid recapture with a saved defense budget. Buy pencil wall rings and protected mortars/piercers/dual guns; retry occupied candidate cells every 60 ticks. Every wave launches immediately. Fixed-step headless evidence; not a frame-time benchmark.",Phase,Fingerprint,SourceBalanceAsset;
        public int Seed,Width,Height,StartingInk,ExpectedKills,Kills,Ink,GoldEarned,Structures,FailedCommands,FlowRebuilds;
        public int PeakAlive,PeakBlots,PeakBuffedEnemies,PeakSlowedEnemies,PeakSpitters,PeakEnemyProjectiles,SpitterSpawnsObserved;
        public int WellLosses,Recaptures,BlueInkEarned,ColoredInkSpent,BlotterUses;
        public int WaveInkGrant,MaxEarlyLaunchInk,WaveGold,VictoryGold,WellActivationCost,WellInkPerWave;
        public int[] WaveCounts;
        public InkLoopWaveTuning[] WaveTuning;
        public long Tick;
        public float StartingBaseHealth,BaseHealth,ElapsedSeconds;
        public float PassiveInkPerSecond,EarlyLaunchInkPerSecond,WellHealth;
        public bool EnemyMechanics,InkWells,Completed,TimedOut,CampaignPassed,MechanicsObserved,Passed;
        public List<InkLoopCommand> Commands=new List<InkLoopCommand>();
        public List<InkLoopWave> Waves=new List<InkLoopWave>();
    }
    [Serializable] public sealed class InkLoopCommand
    {
        public long Tick;public int Wave,X,Y,OrdinaryInkSpent,ColoredInkSpent,InkAfter;
        public string Action,Condition,Blueprint,Reason;public bool Success;
    }
    [Serializable] public sealed class InkLoopWave
    {
        public int Wave,Kills,Ink,Gold,BlueInk,Structures;public long Tick;
        public float BaseHealth;public bool WellActive;
    }
    [Serializable] public sealed class InkLoopWaveTuning
    {
        public int Count,Sides;
        public float SpawnInterval,RunnerFraction,GnawerFraction,SpitterFraction,OutlinedFraction,HatchedFraction;
    }
}
