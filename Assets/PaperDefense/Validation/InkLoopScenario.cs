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
        readonly List<Cell> compactBaseCells = new List<Cell>(), compactWellCells = new List<Cell>();
        static readonly int[] CompactSlots = { 1,1,2,3,1,1,2,0 };
        static readonly int[] ProtectedWellSlots = { 1,1,1,1,3,3,3,3 };
        bool lastWellActive, capturedAgain, baseRingBuilt, wellRingBuilt;
        int observedEnemyId, basePurchases, wellPurchases;
        long nextOrders;
        public GameSession Session {get;}
        public InkLoopReport Report {get;} = new InkLoopReport();
        public bool Finished => Report.Completed;

        public InkLoopScenario(GameBalance balance, int seed = 1337, IReadOnlyList<TowerBlueprint> suppliedLoadout = null)
        {
            Session = new GameSession(balance, seed, false);
            loadout = suppliedLoadout == null ? new WorkshopProfile().GetLoadoutBlueprints() : PrepareLoadout(suppliedLoadout, Session.Balance);
            Report.Seed=seed;Report.Width=balance.Width;Report.Height=balance.Height;Report.Preset=balance.Preset.ToString();
            Report.Strategy=balance.Preset==GameBalancePreset.Target?"TargetProtectedPursuitV4":"HistoricalBattery";
            if(balance.Preset==GameBalancePreset.Target)
                Report.Scope="Target workshop balance supplied by caller, unchanged; zero starter structures. Public commands only; default four blueprints. Compact mixed battery inside a repaired pencil wall ring. After spawning ends, a small enemy tail or an entirely ranged tail receives paid pursuit guns only for spitters outside every existing tower's range. Pursuit construction takes priority over recurring wall repairs. The blue point is initially exposed until actual enemy destruction, then recaptured only after fourteen base towers and a fully funded wall ring plus four cardinal dual-gun defenders and four diagonal mortars covering the ranged siege standoff. Missing well walls and paid intermission reactivation take priority over further base expansion. Additional towers use finite earned ink. One manual cleanup demonstrates the tool; blue cartridges use earned well payouts and are fitted to completed non-mortar towers with a living enemy in range. Every wave launches immediately. WaveInkGrant is only the historical fallback; each WaveTuning/Waves InkGrant resolves its ConfiguredInkGrant. This strategy chooses no Salvage reward multiplier. Fixed-step headless evidence; not a frame-time benchmark.";
            if(suppliedLoadout!=null)Report.Scope=(Report.Scope??"Public commands only; default four blueprints.")
                .Replace("default four blueprints.","caller-supplied four blueprints.")
                .Replace("four cardinal dual-gun defenders and four diagonal mortars covering the ranged siege standoff","four cardinal slot-1 defenders and four diagonal slot-3 defenders")
                .Replace("protected mortars/piercers/dual guns","caller-supplied tower pages");
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
                Report.WaveTuning[i]=new InkLoopWaveTuning{Count=wave.Count,Sides=wave.Sides,ConfiguredInkGrant=wave.InkGrant,
                    InkGrant=ResolvedInkGrant(wave,balance),SpawnInterval=wave.SpawnInterval,
                    RunnerFraction=wave.RunnerFraction,GnawerFraction=wave.GnawerFraction,SpitterFraction=wave.SpitterFraction,
                    OutlinedFraction=wave.OutlinedFraction,HatchedFraction=wave.HatchedFraction};
            }
            foreach(int r in new[]{3,4,6,7,8,9})foreach(var direction in Directions)
                baseCells.Add(new Cell(Session.BaseCell.X+r*direction.X,Session.BaseCell.Y+r*direction.Y));
            AddCompactCells(compactBaseCells,Session.BaseCell);
            if(Session.Wells.Count>1)
            {
                var center=Session.Wells[1].Cell;
                foreach(int r in new[]{3,5,7})foreach(var direction in Directions)
                    wellCells.Add(new Cell(center.X+r*direction.X,center.Y+r*direction.Y));
                AddCompactCells(compactWellCells,center);
                Activate("Activate initially exposed blue point");
            }
            Observe();
        }

        static IReadOnlyList<TowerBlueprint> PrepareLoadout(IReadOnlyList<TowerBlueprint> suppliedLoadout, GameBalance balance)
        {
            if(suppliedLoadout.Count!=ProfileLimits.HistoricalLoadoutSlotCount)
                throw new ArgumentException("suppliedLoadout must contain exactly four valid blueprint pages.",nameof(suppliedLoadout));
            var owned=new TowerBlueprint[ProfileLimits.HistoricalLoadoutSlotCount];
            for(int i=0;i<owned.Length;i++)
            {
                var page=suppliedLoadout[i];
                if(page==null)throw new ArgumentException("suppliedLoadout["+i+"] must not be null.",nameof(suppliedLoadout));
                page=page.WithContent(balance.Content);
                if(!TowerCatalog.Validate(page,balance,out string reason))
                    throw new ArgumentException("suppliedLoadout["+i+"] is invalid: "+reason,nameof(suppliedLoadout));
                owned[i]=page;
            }
            return Array.AsReadOnly(owned);
        }

        public void Step()
        {
            if(Finished)return;
            if(Session.Balance.Preset==GameBalancePreset.Target){StepTarget();return;}
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

        static void AddCompactCells(List<Cell> cells,Cell center)
        {
            var seen=new HashSet<Cell>();
            foreach(int r in new[]{3,4})foreach(var direction in Directions)
            {
                var cell=new Cell(center.X+r*direction.X,center.Y+r*direction.Y);
                if(seen.Add(cell))cells.Add(cell);
            }
            for(int y=-4;y<=4;y++)for(int x=-4;x<=4;x++)
            {
                if(Math.Abs(x)<=1&&Math.Abs(y)<=1)continue;
                var cell=new Cell(center.X+x,center.Y+y);
                if(seen.Add(cell))cells.Add(cell);
            }
        }

        void StepTarget()
        {
            if(Session.Phase==RunPhase.Planning||Session.Phase==RunPhase.Intermission)
            {
                if(Session.PendingUpgrade)
                {
                    var upgrade=Session.WaveIndex==4?RunUpgrade.RapidFire:RunUpgrade.DeepInk;
                    Session.ChooseUpgrade(upgrade);Record("Upgrade",true,0,upgrade.ToString());
                }
                OrderTargetBatteries();
                TargetPaintAndBlotter();
                Session.StartNextWave();Record("StartWave",Session.Phase==RunPhase.Assault,0,"Launch immediately; finite earned budget");
            }
            if(Session.TickIndex>=nextOrders)
            {
                nextOrders=Session.TickIndex+60;
                OrderTargetBatteries();
                TargetPaintAndBlotter();
            }
            Session.Tick(Delta);Observe();
            if(Session.Phase==RunPhase.Victory||Session.Phase==RunPhase.Defeat||Session.TickIndex>=MaximumTicks)Finish();
        }

        int ExistingTowers(IReadOnlyList<Cell> cells)
        {
            int count=0;
            foreach(var cell in cells)if(Session.StructureAt(cell)?.Blueprint!=null)count++;
            return count;
        }

        int BlueprintCost(int slot)=>TowerCatalog.Cost(loadout[slot],Session.Balance,MaterialTier.Pencil);
        int PencilWallCost=>(int)Math.Ceiling(Math.Max(0,Session.Balance.Structure(StructureKind.Wall).Cost)*
            Math.Max(0,Session.Balance.Material(MaterialTier.Pencil).CostMultiplier));

        void OrderTargetBatteries()
        {
            PursueTargetSpitters();
            RepairTargetRing(Session.BaseCell,"Initial or replacement base pencil wall");
            if(capturedAgain&&Session.Wells.Count>1)
            {
                var well=Session.Wells[1];
                RepairTargetRing(well.Cell,"Replace missing blue-point pencil wall before further base expansion");
                if(!well.Active&&(Session.Phase==RunPhase.Planning||Session.Phase==RunPhase.Intermission))
                {
                    BuildTargetWellTowers(8,Session.Balance.WellActivationCost);
                    if(ExistingTowers(compactWellCells)>=8&&Session.Ink>=Session.Balance.WellActivationCost)
                        Activate("Paid intermission reactivation before further base expansion");
                }
            }
            int baseLimit=!capturedAgain?14:Session.WaveIndex<4?20:Session.WaveIndex<6?28:40;
            int existing=ExistingTowers(compactBaseCells);
            foreach(var cell in compactBaseCells)
            {
                if(existing>=baseLimit)break;
                if(Session.StructureAt(cell)!=null)continue;
                int slot=CompactSlots[basePurchases%CompactSlots.Length];
                if(Session.Ink<BlueprintCost(slot))break;
                if(!Session.QuoteBlueprint(new[]{cell},loadout[slot],MaterialTier.Pencil,InkColor.None).Valid)continue;
                if(Build(cell,slot,"Compact base battery from starting/earned ink")){basePurchases++;existing++;}
            }
            if(Session.Wells.Count<=1||Report.WellLosses==0)return;
            Cell center=Session.Wells[1].Cell;
            if(!capturedAgain)
            {
                int missingWalls=0;
                foreach(var cell in RingCells(center))if(Session.StructureAt(cell)==null)missingWalls++;
                int reserve=Session.Balance.WellActivationCost+missingWalls*PencilWallCost;
                for(int i=0;i<ProtectedWellSlots.Length;i++)
                    if(Session.StructureAt(compactWellCells[i])==null)reserve+=BlueprintCost(ProtectedWellSlots[i]);
                if(existing<14||Session.Ink<reserve)return;
                RepairTargetRing(center,"Funded blue-point pencil perimeter before recapture");
                BuildTargetWellTowers(8,Session.Balance.WellActivationCost);
                if(ExistingTowers(compactWellCells)>=8)
                {Activate("Paid blue recapture with four dual guns and four ranged siege-covering mortars");capturedAgain=Session.Wells[1].Active;}
                return;
            }
            // The base is funded before enlarging this separate garrison.
            if(existing>=baseLimit)BuildTargetWellTowers(8);
        }

        void PursueTargetSpitters()
        {
            if(Session.Phase!=RunPhase.Assault||Session.RemainingToSpawn>0||Session.Enemies.Count==0||
                Session.Ink<BlueprintCost(0))return;
            if(Session.Enemies.Count>8)
                foreach(var enemy in Session.Enemies)if(enemy.Kind!=EnemyKind.Spitter)return;
            foreach(var enemy in Session.Enemies)
            {
                if(enemy.Kind!=EnemyKind.Spitter||CoveredByTargetTower(enemy.Position))continue;
                Point2 objective=new Point2(Session.BaseCell.X+.5f,Session.BaseCell.Y+.5f);
                foreach(var well in Session.Wells)
                {
                    if(!well.Active)continue;
                    var point=new Point2(well.Cell.X+.5f,well.Cell.Y+.5f);
                    if(DistanceSquared(enemy.Position,point)<DistanceSquared(enemy.Position,objective))objective=point;
                }
                float dx=objective.X-enemy.Position.X,dy=objective.Y-enemy.Position.Y;
                float distance=(float)Math.Sqrt(dx*dx+dy*dy);
                if(distance<=.0001f)continue;
                var balance=Session.Balance;
                float range=TowerCatalog.WeaponRange(loadout[0],balance,loadout[0].FirstWeaponId,0);
                if(range<=0)continue;
                float advance=Math.Min(distance,Math.Min(6,range*.6f));
                var center=new Cell((int)Math.Floor(enemy.Position.X+dx/distance*advance),
                    (int)Math.Floor(enemy.Position.Y+dy/distance*advance));
                for(int radius=0;radius<=3;radius++)for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++)
                {
                    if(Math.Max(Math.Abs(x),Math.Abs(y))!=radius)continue;
                    var cell=new Cell(center.X+x,center.Y+y);
                    var origin=new Point2(cell.X+.5f,cell.Y+.5f);
                    if(!MountCanReach(loadout[0],balance,origin,enemy.Position))continue;
                    if(!Session.QuoteBlueprint(new[]{cell},loadout[0],MaterialTier.Pencil,InkColor.None).Valid)continue;
                    if(Build(cell,0,"Paid pursuit gun for a surviving spitter outside every battery range"))
                        Report.PursuitTowersBuilt++;
                    return;
                }
            }
        }

        bool CoveredByTargetTower(Point2 enemy)
        {
            var balance=Session.Balance;
            foreach(var tower in Session.Structures)
            {
                if(tower.Blueprint==null||tower.Health<=0)continue;
                // Pending construction also reserves coverage so one order cannot duplicate the response.
                if(MountCanReach(tower.Blueprint,balance,new Point2(tower.Cell.X+.5f,tower.Cell.Y+.5f),enemy))return true;
            }
            return false;
        }

        static bool MountCanReach(TowerBlueprint blueprint,GameBalance balance,Point2 origin,Point2 target,bool forPaint=false)
        {
            float distance=DistanceSquared(origin,target);
            foreach(var mount in blueprint.WeaponMounts)
            {
                var weapon=balance.Content.GetWeapon(mount.WeaponId);
                if(!weapon.IsCombatMount||(forPaint&&weapon.Attack==AttackKind.AreaProjectile))continue;
                float maximum=TowerCatalog.WeaponRange(blueprint,balance,mount.WeaponId,mount.SlotIndex);
                if(maximum>0&&distance>=weapon.MinimumRange*weapon.MinimumRange&&distance<=maximum*maximum)return true;
            }
            return false;
        }

        static float DistanceSquared(Point2 a,Point2 b)
        {float dx=a.X-b.X,dy=a.Y-b.Y;return dx*dx+dy*dy;}

        void BuildTargetWellTowers(int limit,int reserve=0)
        {
            int existing=ExistingTowers(compactWellCells);
            for(int i=0;i<Math.Min(limit,compactWellCells.Count);i++)
            {
                if(existing>=limit)break;
                var cell=compactWellCells[i];
                if(Session.StructureAt(cell)!=null)continue;
                int slot=i<ProtectedWellSlots.Length?ProtectedWellSlots[i]:CompactSlots[wellPurchases%CompactSlots.Length];
                if(Session.Ink<BlueprintCost(slot)+reserve)break;
                if(!Session.QuoteBlueprint(new[]{cell},loadout[slot],MaterialTier.Pencil,InkColor.None).Valid)continue;
                if(Build(cell,slot,"Protected blue-point garrison from finite earned ink")){wellPurchases++;existing++;}
            }
        }

        static List<Cell> RingCells(Cell center)
        {
            var cells=new List<Cell>();const int r=5;
            for(int x=-r;x<=r;x++){cells.Add(new Cell(center.X+x,center.Y-r));cells.Add(new Cell(center.X+x,center.Y+r));}
            for(int y=-r+1;y<r;y++){cells.Add(new Cell(center.X-r,center.Y+y));cells.Add(new Cell(center.X+r,center.Y+y));}
            return cells;
        }

        void RepairTargetRing(Cell center,string condition)
        {
            foreach(var cell in RingCells(center))
            {
                if(Session.StructureAt(cell)!=null)continue;
                if(Session.Ink<PencilWallCost)return;
                // A live enemy may temporarily block a missing segment. Retry on the next order.
                if(!Session.QuoteBuild(new[]{cell},StructureKind.Wall,MaterialTier.Pencil).Valid)continue;
                int before=Session.Ink;bool success=Session.TryBuild(new[]{cell},StructureKind.Wall,MaterialTier.Pencil,out string reason);
                Record("BuildWalls",success,before-Session.Ink,condition,cell,null,reason);
            }
        }

        void TargetPaintAndBlotter()
        {
            var balance=Session.Balance;
            foreach(var tower in Session.Structures)
            {
                if(tower.Blueprint==null||tower.Color!=InkColor.None||
                    tower.Health<=0||!tower.IsComplete||tower.BuildRemaining>0)continue;
                if(Session.ColoredInk(InkColor.Blue)<Session.ColorCost(tower.Blueprint,tower.Tier))continue;
                bool hasTarget=false;
                Point2 origin=new Point2(tower.Cell.X+.5f,tower.Cell.Y+.5f);
                foreach(var enemy in Session.Enemies)
                    if(enemy.Health>0&&MountCanReach(tower.Blueprint,balance,origin,enemy.Position,true)){hasTarget=true;break;}
                if(!hasTarget)continue;
                int before=Session.ColoredInk(InkColor.Blue);
                bool success=Session.TryPaint(tower.Cell,InkColor.Blue,out string reason);
                Record("Paint",success,0,"Blue cartridge from a completed wave's well payout for a tower with a living target in range",tower.Cell,"Blue",reason);
                if(success)
                {int spent=before-Session.ColoredInk(InkColor.Blue);Report.ColoredInkSpent+=spent;Report.Commands[Report.Commands.Count-1].ColoredInkSpent=spent;}
                break;
            }
            if(Report.BlotterUses==0&&Session.BlotterCooldownRemaining<=0&&Session.Blots.Count>=4&&
                Session.Ink>=Session.Balance.BlotterCost+BlueprintCost(0))
            {
                Cell cell=Session.Blots[0].Cell;int before=Session.Ink;
                bool success=Session.TryBlotter(cell,out string reason);
                Record("Blotter",success,before-Session.Ink,"One paid cleanup of a naturally produced corpse mark",cell,null,reason);
                if(success)Report.BlotterUses++;
            }
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
                        ConfiguredInkGrant=Session.Balance.Waves[Session.WaveIndex-1].InkGrant,
                        InkGrant=ResolvedInkGrant(Session.Balance.Waves[Session.WaveIndex-1],Session.Balance),
                        BaseHealth=Session.BaseHealth,Ink=Session.Ink,Gold=Session.GoldEarned,BlueInk=Session.ColoredInk(InkColor.Blue),
                        WellActive=Session.Wells.Count>1&&Session.Wells[1].Active,Structures=Session.Structures.Count});
        }

        static int ResolvedInkGrant(WaveDefinition wave,GameBalance balance)
            => Math.Max(0,wave.InkGrant>=0?wave.InkGrant:balance.WaveInkGrant);

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
        public string Scope="Workshop balance supplied by caller, unchanged; zero starter structures. Public commands only; default four blueprints. Blue point deliberately exposed until natural loss, then paid recapture with a saved defense budget. Buy pencil wall rings and protected mortars/piercers/dual guns; retry occupied candidate cells every 60 ticks. Every wave launches immediately. WaveInkGrant is only the historical fallback; each WaveTuning/Waves InkGrant resolves its ConfiguredInkGrant. This strategy chooses no Salvage reward multiplier. Fixed-step headless evidence; not a frame-time benchmark.",Phase,Fingerprint,SourceBalanceAsset,Preset,Strategy;
        public int Seed,Width,Height,StartingInk,ExpectedKills,Kills,Ink,GoldEarned,Structures,FailedCommands,FlowRebuilds;
        public int PeakAlive,PeakBlots,PeakBuffedEnemies,PeakSlowedEnemies,PeakSpitters,PeakEnemyProjectiles,SpitterSpawnsObserved;
        public int WellLosses,Recaptures,BlueInkEarned,ColoredInkSpent,BlotterUses,PursuitTowersBuilt;
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
        public int Wave,Kills,Ink,Gold,BlueInk,Structures,ConfiguredInkGrant,InkGrant;public long Tick;
        public float BaseHealth;public bool WellActive;
    }
    [Serializable] public sealed class InkLoopWaveTuning
    {
        public int Count,Sides,ConfiguredInkGrant,InkGrant;
        public float SpawnInterval,RunnerFraction,GnawerFraction,SpitterFraction,OutlinedFraction,HatchedFraction;
    }
}
