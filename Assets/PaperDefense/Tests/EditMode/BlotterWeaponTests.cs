using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class BlotterWeaponTests
    {
        static GameBalance Balance()
        {
            var balance=GameBalance.CreateDefault(); var weapon=balance.Structure(StructureKind.Blotter);
            weapon.Cost=30; weapon.MaxHealth=55; weapon.BuildSeconds=1.2f; weapon.Range=9;
            weapon.Damage=0; weapon.ShotInterval=2; weapon.Radius=1.5f; balance.Construction.AcquisitionSeconds=0;
            return balance;
        }
        static StructureState Tower(TowerBlueprint blueprint=null)
        {
            blueprint=blueprint??new TowerBlueprint("blotter","Blotter",ChassisKind.Standard,StructureKind.Blotter);
            return new StructureState {Id=1,Cell=new Cell(10,10),Kind=blueprint.FirstWeapon,Blueprint=blueprint,
                Health=55,MaxHealth=55,IsComplete=true,Tier=MaterialTier.Pencil};
        }
        static EnemyState Enemy()=>new EnemyState {Id=1,Position=new Point2(11.5f,10.5f),Health=100000,MaxHealth=100000};
        static RunStatistics Fire(GameBalance balance,StructureState tower,IBlotContext context,List<EnemyState> enemies,out List<ShotEvent> shots)
        {
            var stats=new RunStatistics(); shots=new List<ShotEvent>();
            new WeaponSystem(balance,context).Fire(new List<StructureState>{tower},enemies,shots,1,1,1f/30,1,stats,new SimulationEventBuffer());
            return stats;
        }
        static RunStatistics Run(GameBalance balance,StructureState tower,IBlotContext context,List<EnemyState> enemies,int ticks)
        {
            var weapons=new WeaponSystem(balance,context); var stats=new RunStatistics();
            var towers=new List<StructureState>{tower}; var shots=new List<ShotEvent>(); var events=new SimulationEventBuffer(); const float delta=1f/30;
            for(int tick=0;tick<ticks;tick++)
            {
                if(!balance.Construction.DynamicFeed)
                {tower.Cooldown=Math.Max(0,tower.Cooldown-delta);tower.SecondaryCooldown=Math.Max(0,tower.SecondaryCooldown-delta);}
                shots.Clear(); weapons.Fire(towers,enemies,shots,1,1,delta,tick+1,stats,events,(tick+1)*(double)delta);
            }
            return stats;
        }

        [Test] public void BlotterCleansItsConfiguredRadiusWithoutAnyEnemyAndRecordsOnlyActualClears()
        {
            var balance=Balance(); var tower=Tower();
            var context=new BlotFixture(new Cell(12,10),new Cell(13,10),new Cell(14,10),new Cell(20,10));
            var stats=Fire(balance,tower,context,new List<EnemyState>(),out var shots);
            Assert.That(stats.BlotsCleared,Is.EqualTo(2)); Assert.That(context.Contains(new Cell(12,10)),Is.False);
            Assert.That(context.Contains(new Cell(13,10)),Is.False); Assert.That(context.Contains(new Cell(14,10)),Is.True);
            Assert.That(context.Contains(new Cell(20,10)),Is.True);
            Assert.That(stats.Damage(StructureKind.Blotter),Is.Zero); Assert.That(stats.Launches(StructureKind.Blotter),Is.EqualTo(1));
            Assert.That(shots.Count,Is.EqualTo(1)); Assert.That(shots[0].To.X,Is.EqualTo(12.5f));
            Assert.That(shots[0].Radius,Is.EqualTo(1.5f)); Assert.That(tower.PrimaryAim.X,Is.EqualTo(12.5f));
        }

        [Test] public void ACombatMountStillAcquiringAnEnemyCannotBlockBlotCleanup()
        {
            var balance=Balance(); balance.Construction.AcquisitionSeconds=5; balance.Construction.DynamicFeed=true;
            var blueprint=new TowerBlueprint("gun-blotter","Gun and blotter",ChassisKind.Standard,StructureKind.Gun,StructureKind.Blotter);
            var tower=Tower(blueprint);
            var stats=Fire(balance,tower,new BlotFixture(new Cell(12,10)),new List<EnemyState>{Enemy()},out var shots);
            Assert.That(tower.PrimaryAcquisitionRemaining,Is.GreaterThan(4)); Assert.That(stats.Launches(StructureKind.Gun),Is.Zero);
            Assert.That(stats.Launches(StructureKind.Blotter),Is.EqualTo(1)); Assert.That(stats.BlotsCleared,Is.EqualTo(1));
            Assert.That(tower.SecondaryAim.X,Is.EqualTo(12.5f)); Assert.That(tower.SecondaryCooldown,Is.EqualTo(2).Within(.00001f));
            Assert.That(shots.Count,Is.EqualTo(1));
        }

        [Test] public void AnIdleBlotterWithNoBlotsDoesNotStealAnActiveGunsDynamicFeed()
        {
            var balance=Balance(); balance.Construction.DynamicFeed=true;
            var blueprint=new TowerBlueprint("gun-blotter","Gun and blotter",ChassisKind.Standard,StructureKind.Gun,StructureKind.Blotter);
            var stats=Run(balance,Tower(blueprint),new BlotFixture(),new List<EnemyState>{Enemy()},900);
            Assert.That(stats.Launches(StructureKind.Gun),Is.EqualTo(70)); Assert.That(stats.Launches(StructureKind.Blotter),Is.Zero);
            Assert.That(stats.BlotsCleared,Is.Zero); Assert.That(stats.IdleSeconds(StructureKind.Blotter),Is.EqualTo(30).Within(.001f));
        }

        [Test] public void TwoWorkingBlottersShareProductionAndChooseTheirOwnRemainingBlotTargets()
        {
            var balance=Balance(); balance.Construction.DynamicFeed=true; balance.Structure(StructureKind.Blotter).Radius=0;
            var blueprint=new TowerBlueprint("dual-blotter","Dual blotter",ChassisKind.Standard,StructureKind.Blotter,StructureKind.Blotter);
            var tower=Tower(blueprint); var context=new BlotFixture(new Cell(12,10),new Cell(13,10),new Cell(14,10));
            var stats=Fire(balance,tower,context,new List<EnemyState>(),out var shots);
            Assert.That(stats.BlotsCleared,Is.EqualTo(2)); Assert.That(stats.Launches(StructureKind.Blotter),Is.EqualTo(2));
            Assert.That(tower.PrimaryAim.X,Is.EqualTo(12.5f)); Assert.That(tower.SecondaryAim.X,Is.EqualTo(13.5f));
            Assert.That(tower.Cooldown,Is.EqualTo(2/.8f).Within(.00001f));
            Assert.That(tower.SecondaryCooldown,Is.EqualTo(2/.8f).Within(.00001f));
            Assert.That(context.Contains(new Cell(14,10)),Is.True); Assert.That(shots.Count,Is.EqualTo(2));
        }

        [Test] public void CleanupCadenceUsesTheConfiguredIntervalWithoutDealingDamage()
        {
            var balance=Balance(); balance.Structure(StructureKind.Blotter).Range=100; balance.Structure(StructureKind.Blotter).Radius=0;
            var cells=new Cell[20]; for(int i=0;i<cells.Length;i++) cells[i]=new Cell(i,5);
            var result=Run(balance,Tower(),new BlotFixture(cells),new List<EnemyState>(),900);
            Assert.That(result.BlotsCleared,Is.EqualTo(15));
            Assert.That(result.Launches(StructureKind.Blotter),Is.EqualTo(15));
            Assert.That(result.Damage(StructureKind.Blotter),Is.Zero);
            Assert.That(balance.Structure(StructureKind.Blotter).Radius,Is.Zero);
        }

        [Test] public void ConfiguredRadiusAndIntervalChangeActualCleanupInsteadOfEnemyAttacks()
        {
            var balance=Balance(); var weapon=balance.Structure(StructureKind.Blotter); weapon.Radius=0;
            var narrow=Fire(balance,Tower(),new BlotFixture(new Cell(12,10),new Cell(13,10)),new List<EnemyState>(),out _);
            weapon.Radius=1;
            var wide=Fire(balance,Tower(),new BlotFixture(new Cell(12,10),new Cell(13,10)),new List<EnemyState>(),out _);
            Assert.That(narrow.BlotsCleared,Is.EqualTo(1)); Assert.That(wide.BlotsCleared,Is.EqualTo(2));
            weapon.Range=100; weapon.Radius=0;
            var cells=new Cell[40]; for(int i=0;i<cells.Length;i++) cells[i]=new Cell(i,5);
            var slow=Run(balance,Tower(),new BlotFixture(cells),new List<EnemyState>(),900);
            weapon.ShotInterval=1;
            var fast=Run(balance,Tower(),new BlotFixture(cells),new List<EnemyState>(),900);
            Assert.That(slow.BlotsCleared,Is.EqualTo(15)); Assert.That(fast.BlotsCleared,Is.EqualTo(30));
        }

        [Test] public void SupportCleanupCannotDealDamageOrConsumeTheCombatMountsInkEntitlement()
        {
            var balance=Balance(); balance.Structure(StructureKind.Blotter).Damage=999;
            balance.Effects.LimitTowerColor=true; balance.Effects.ColorTargetsPerWindow=1;
            var blueprint=new TowerBlueprint("blotter-gun","Blotter and gun",ChassisKind.Standard,StructureKind.Blotter,StructureKind.Gun);
            var tower=Tower(blueprint); tower.Color=InkColor.Blue; var enemy=Enemy(); enemy.Health=100;
            var stats=Fire(balance,tower,new BlotFixture(new Cell(12,10)),new List<EnemyState>{enemy},out var shots);
            Assert.That(enemy.Health,Is.EqualTo(88)); Assert.That(enemy.BlueRemaining,Is.GreaterThan(0));
            Assert.That(stats.Damage(StructureKind.Blotter),Is.Zero); Assert.That(stats.Damage(StructureKind.Gun),Is.EqualTo(12));
            Assert.That(stats.BlotsCleared,Is.EqualTo(1)); Assert.That(shots.Count,Is.EqualTo(2));
        }

        [Test] public void MissingBlotContextLeavesTheSupportMountIdle()
        {
            var stats=Fire(Balance(),Tower(),null,new List<EnemyState>{Enemy()},out var shots);
            Assert.That(stats.BlotsCleared,Is.Zero); Assert.That(stats.Launches(StructureKind.Blotter),Is.Zero);
            Assert.That(stats.Damage(StructureKind.Blotter),Is.Zero); Assert.That(shots.Count,Is.Zero);
        }

        [Test] public void BlotterReloadFinishesWhileItsSheetIsClean()
        {
            var balance=Balance(); balance.Construction.DynamicFeed=true;
            var tower=Tower(); var context=new BlotFixture(new Cell(12,10));
            var weapons=new WeaponSystem(balance,context); var stats=new RunStatistics();
            var towers=new List<StructureState>{tower}; var enemies=new List<EnemyState>();
            var shots=new List<ShotEvent>(); var events=new SimulationEventBuffer();
            weapons.Fire(towers,enemies,shots,1,1,1f/30,1,stats,events);
            Assert.That(stats.Launches(StructureKind.Blotter),Is.EqualTo(1));
            for(int i=0;i<90;i++) weapons.Fire(towers,enemies,shots,1,1,1f/30,i+2,stats,events);
            Assert.That(tower.PrimaryActionRemaining,Is.Zero);
            Assert.That(tower.Cooldown,Is.Zero);
            Assert.That(stats.Launches(StructureKind.Blotter),Is.EqualTo(1));
        }

        sealed class BlotFixture:IBlotContext
        {
            readonly List<Cell> cells;
            internal BlotFixture(params Cell[] source){cells=new List<Cell>(source);}
            internal bool Contains(Cell cell)=>cells.Contains(cell);
            public bool TryFindNearest(Point2 origin,float range,out Cell cell)
            {
                cell=default; bool found=false; float nearest=range*range;
                foreach(var candidate in cells)
                {
                    float dx=candidate.X+.5f-origin.X,dy=candidate.Y+.5f-origin.Y,distance=dx*dx+dy*dy;
                    if(distance>nearest) continue;
                    if(!found || distance<nearest || candidate.Y<cell.Y || (candidate.Y==cell.Y && candidate.X<cell.X))
                    {cell=candidate;nearest=distance;found=true;}
                }
                return found;
            }
            public int Clear(Cell center,float radius)
            {
                int count=0;
                for(int i=cells.Count-1;i>=0;i--)
                {
                    float dx=cells[i].X-center.X,dy=cells[i].Y-center.Y;
                    if(dx*dx+dy*dy>radius*radius) continue;
                    cells.RemoveAt(i);count++;
                }
                return count;
            }
        }
    }
}
