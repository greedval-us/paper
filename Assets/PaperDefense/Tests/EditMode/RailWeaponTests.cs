using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class RailWeaponTests
    {
        static TowerBlueprint Blueprint(StructureKind kind=StructureKind.Gun,bool dual=false,TowerModules modules=TowerModules.Rail)
            =>new TowerBlueprint("rail","Rail",ChassisKind.Heavy,kind,dual?(StructureKind?)kind:null,modules);
        static StructureState Tower(TowerBlueprint blueprint)
            =>new StructureState {Id=1,Cell=new Cell(10,10),Kind=blueprint?.FirstWeapon??StructureKind.Gun,
                Blueprint=blueprint,Health=55,MaxHealth=55,IsComplete=true,Tier=MaterialTier.Pencil,TargetId=1};
        static EnemyState Enemy(int id=1,float x=11.5f)
            =>new EnemyState {Id=id,Position=new Point2(x,10.5f),Health=100000,MaxHealth=100000};
        static RunStatistics Run(GameBalance balance,StructureState tower,int ticks,float quality)
        {
            var weapons=new WeaponSystem(balance,quality); var stats=new RunStatistics(); var shots=new List<ShotEvent>();
            var structures=new List<StructureState>{tower}; var enemies=new List<EnemyState>{Enemy()}; var events=new SimulationEventBuffer();
            const float delta=1f/30;
            for(int tick=0;tick<ticks;tick++)
            {
                if(!balance.Construction.DynamicFeed)
                {tower.Cooldown=Math.Max(0,tower.Cooldown-delta);tower.SecondaryCooldown=Math.Max(0,tower.SecondaryCooldown-delta);}
                shots.Clear();weapons.Fire(structures,enemies,shots,1,1,delta,tick+1,stats,events,(tick+1)*(double)delta);
            }
            return stats;
        }
        static RunStatistics Fire(GameBalance balance,StructureState tower,List<EnemyState> enemies,out List<ShotEvent> shots,IBlotContext context=null)
        {
            var stats=new RunStatistics();shots=new List<ShotEvent>();
            new WeaponSystem(balance,null,context).Fire(new List<StructureState>{tower},enemies,shots,1,1,1f/30,1,stats,new SimulationEventBuffer());
            return stats;
        }

        [TestCase(false,100,57)]
        [TestCase(false,0,50)]
        [TestCase(true,100,57)]
        [TestCase(true,0,50)]
        public void RailChangesSingleMountCadenceInBothSupplyModesWithoutChangingPerHitDamage(bool dynamic,float quality,int launches)
        {
            var balance=GameBalance.CreateDefault();balance.Construction.DynamicFeed=dynamic;
            var tower=Tower(Blueprint());var stats=Run(balance,tower,900,quality);
            Assert.That(stats.Launches(StructureKind.Gun),Is.EqualTo(launches));
            Assert.That(stats.Damage(StructureKind.Gun),Is.EqualTo(launches*12));
            Assert.That(tower.Health,Is.EqualTo(55)); Assert.That(balance.Structure(StructureKind.Gun).Range,Is.EqualTo(11));
        }

        [TestCase(false)] [TestCase(true)]
        public void DualRailUsesSharedFeedQualityAndIntervalPenaltyExactlyOnce(bool dynamic)
        {
            var balance=GameBalance.CreateDefault();balance.Construction.DynamicFeed=dynamic;
            var tower=Tower(Blueprint(dual:true));
            Run(balance,tower,1,0);
            float expected=.42f/.8f/.85f*1.2f;
            Assert.That(tower.Cooldown,Is.EqualTo(expected).Within(.000001f));
            Assert.That(tower.SecondaryCooldown,Is.EqualTo(expected).Within(.000001f));
            Assert.That(TowerCatalog.FeedMultiplier(tower.Blueprint,balance,2),Is.EqualTo(.8f));
            Assert.That(Run(balance,Tower(tower.Blueprint),900,0).Launches(StructureKind.Gun),Is.EqualTo(80));
        }

        [Test] public void RailExpandsTheActualCombatQueryUpToItsRangeBoundary()
        {
            var balance=GameBalance.CreateDefault();balance.Structure(StructureKind.Gun).Range=4;
            var target=Enemy(x:15.5f);
            var plain=Fire(balance,Tower(Blueprint(modules:TowerModules.None)),new List<EnemyState>{target},out var plainShots);
            Assert.That(plain.Launches(StructureKind.Gun),Is.Zero);Assert.That(plainShots.Count,Is.Zero);
            var rail=Fire(balance,Tower(Blueprint()),new List<EnemyState>{target},out var railShots);
            Assert.That(rail.Launches(StructureKind.Gun),Is.EqualTo(1));Assert.That(railShots.Count,Is.EqualTo(1));
            target.Position=new Point2(15.51f,10.5f);
            Assert.That(Fire(balance,Tower(Blueprint()),new List<EnemyState>{target},out _).Launches(StructureKind.Gun),Is.Zero);
        }

        [Test] public void ConfiguredRailMultipliersControlRealRangeAndCooldown()
        {
            var balance=GameBalance.CreateDefault();balance.Structure(StructureKind.Gun).Range=4;
            var enemy=Enemy(x:17.5f);
            Assert.That(Fire(balance,Tower(Blueprint()),new List<EnemyState>{enemy},out _).Launches(StructureKind.Gun),Is.Zero);
            balance.Construction.RailRangeMultiplier=2;balance.Construction.RailIntervalMultiplier=1.7f;
            var tower=Tower(Blueprint());var stats=Fire(balance,tower,new List<EnemyState>{enemy},out _);
            Assert.That(stats.Launches(StructureKind.Gun),Is.EqualTo(1));
            Assert.That(tower.Cooldown,Is.EqualTo(.42f*1.7f).Within(.000001f));
            Assert.That(balance.Structure(StructureKind.Gun).Range,Is.EqualTo(4));
            Assert.That(balance.Structure(StructureKind.Gun).ShotInterval,Is.EqualTo(.42f));
        }

        [Test] public void MortarRailChangesReachAndTempoButPreservesCapturedRadiusFlightAndImpactDamage()
        {
            var balance=GameBalance.CreateDefault();var definition=balance.Structure(StructureKind.Mortar);
            definition.Range=4;definition.Radius=3.7f;definition.FlightSeconds=2.1f;
            var tower=Tower(Blueprint(StructureKind.Mortar));var target=Enemy(x:15.5f);target.Health=100;
            var inside=Enemy(2,19.1f);inside.Health=100;var outside=Enemy(3,19.3f);outside.Health=100;
            var enemies=new List<EnemyState>{target,inside,outside};var towers=new List<StructureState>{tower};
            var weapons=new WeaponSystem(balance);var shots=new List<ShotEvent>();var stats=new RunStatistics();var events=new SimulationEventBuffer();
            weapons.Fire(towers,enemies,shots,1,1,1f/30,1,stats,events);
            Assert.That(weapons.Projectiles.Count,Is.EqualTo(1));var projectile=weapons.Projectiles[0];
            Assert.That(projectile.Radius,Is.EqualTo(3.7f));Assert.That(projectile.Duration,Is.EqualTo(2.1f));
            Assert.That(projectile.Remaining,Is.EqualTo(2.1f));Assert.That(projectile.Damage,Is.EqualTo(25));
            Assert.That(tower.Cooldown,Is.EqualTo(1.8f*1.2f).Within(.000001f));
            Assert.That(shots.Count,Is.Zero);Assert.That(target.Health,Is.EqualTo(100));
            tower.Cooldown=999;
            weapons.Fire(towers,enemies,shots,1,1,2.1f,2,stats,events);
            Assert.That(target.Health,Is.EqualTo(75));Assert.That(inside.Health,Is.EqualTo(75));Assert.That(outside.Health,Is.EqualTo(100));
            Assert.That(stats.Damage(StructureKind.Mortar),Is.EqualTo(50));
        }

        [Test] public void BlotterRailExpandsItsOwnSearchWithoutExpandingTheCleanupRadius()
        {
            var balance=GameBalance.CreateDefault();var definition=balance.Structure(StructureKind.Blotter);
            definition.Range=4;definition.Radius=1;
            var cells=new[]{new Cell(15,10),new Cell(16,10),new Cell(17,10)};
            var plain=Fire(balance,Tower(Blueprint(StructureKind.Blotter,modules:TowerModules.None)),new List<EnemyState>(),out _,new BlotFixture(cells));
            Assert.That(plain.BlotsCleared,Is.Zero);
            var context=new BlotFixture(cells);
            var rail=Fire(balance,Tower(Blueprint(StructureKind.Blotter)),new List<EnemyState>(),out var shots,context);
            Assert.That(rail.BlotsCleared,Is.EqualTo(2));Assert.That(context.Blots[2].Remaining,Is.GreaterThan(0));
            Assert.That(shots.Count,Is.EqualTo(1));Assert.That(shots[0].Radius,Is.EqualTo(1));
        }

        [Test] public void LegacyStructuresWithoutBlueprintIgnoreRailTuning()
        {
            var balance=GameBalance.CreateDefault();balance.Structure(StructureKind.Gun).Range=4;
            balance.Construction.RailRangeMultiplier=9;balance.Construction.RailIntervalMultiplier=3;
            Assert.That(Fire(balance,Tower(null),new List<EnemyState>{Enemy(x:14.6f)},out _).Launches(StructureKind.Gun),Is.Zero);
            var tower=Tower(null);var enemy=Enemy(x:13.5f);enemy.Health=100;
            var stats=Fire(balance,tower,new List<EnemyState>{enemy},out _);
            Assert.That(stats.Launches(StructureKind.Gun),Is.EqualTo(1));Assert.That(enemy.Health,Is.EqualTo(88));
            Assert.That(tower.Cooldown,Is.EqualTo(.42f).Within(.000001f));
        }

        sealed class BlotFixture:IBlotContext
        {
            internal readonly List<BlotState> Blots=new List<BlotState>();
            internal BlotFixture(IEnumerable<Cell> cells){foreach(var cell in cells) Blots.Add(new BlotState{Cell=cell,Remaining=100});}
            public bool TryFindNearest(Point2 origin,float range,out Cell cell)=>BlotQueries.TryFindNearest(Blots,origin,range,out cell);
            public int Clear(Cell cell,float radius)=>BlotQueries.Clear(Blots,cell,radius);
        }
    }
}
