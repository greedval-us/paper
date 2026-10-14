using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class RicochetWeaponTests
    {
        static GameBalance Balance()
        {
            var balance=GameBalance.CreateDefault(); var weapon=balance.Structure(StructureKind.Ricochet);
            weapon.Cost=65; weapon.MaxHealth=55; weapon.BuildSeconds=1.6f; weapon.Range=10;
            weapon.Damage=12; weapon.ShotInterval=.85f; weapon.JumpRange=3; weapon.MaxTargets=4;
            return balance;
        }
        static EnemyState Enemy(int id,float x,float y,EnemyKind kind=EnemyKind.Doodle)
            =>new EnemyState {Id=id,Kind=kind,Position=new Point2(x,y),Health=100,MaxHealth=100};
        static StructureState Tower(TowerBlueprint blueprint=null)
            =>new StructureState {Id=1,Cell=new Cell(10,10),Kind=StructureKind.Ricochet,
                Health=55,IsComplete=true,Tier=MaterialTier.Pencil,
                Blueprint=blueprint??new TowerBlueprint("ricochet","Ricochet",ChassisKind.Standard,StructureKind.Ricochet)};
        static RunStatistics Fire(GameBalance balance,StructureState tower,List<EnemyState> enemies,out List<ShotEvent> shots)
        {
            // Geometry and effect assertions begin with both mounts ready to aim and fire.
            balance.Construction.AcquisitionSeconds=0;
            var stats=new RunStatistics(); shots=new List<ShotEvent>();
            new WeaponSystem(balance).Fire(new List<StructureState>{tower},enemies,shots,
                1,1,1f/30,1,stats,new SimulationEventBuffer());
            return stats;
        }

        [Test] public void ChainIncludesFirstVictimAndReachesBeyondTowerRangeThroughExactJumpBoundaries()
        {
            var balance=Balance(); balance.Structure(StructureKind.Ricochet).Range=2;
            var enemies=new List<EnemyState>();
            for(int i=0;i<5;i++) enemies.Add(Enemy(i+1,12.5f+i*3,10.5f));
            enemies.Add(Enemy(6,12.5f,14.01f));
            var stats=Fire(balance,Tower(),enemies,out var shots);
            for(int i=0;i<4;i++) Assert.That(enemies[i].Health,Is.EqualTo(88),$"Chain victim {i+1}");
            Assert.That(enemies[4].Health,Is.EqualTo(100),"The cap counts the first victim.");
            Assert.That(enemies[5].Health,Is.EqualTo(100),"Every jump must stay within its own range.");
            Assert.That(stats.Damage(StructureKind.Ricochet),Is.EqualTo(48));
            Assert.That(stats.Launches(StructureKind.Ricochet),Is.EqualTo(1));
            Assert.That(shots.Count,Is.EqualTo(4));
            for(int i=0;i<shots.Count;i++)
            {
                Assert.That(shots[i].From.X,Is.EqualTo(i==0?10.5f:enemies[i-1].Position.X));
                Assert.That(shots[i].To.X,Is.EqualTo(enemies[i].Position.X));
            }
        }

        [Test] public void NextVictimUsesNearestAliveUnhitNeighborThenIdRegardlessOfInputOrder()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var first=Enemy(1,11.5f,10.5f); var upper=Enemy(2,13.5f,11.5f);
                var lower=Enemy(3,13.5f,9.5f); var last=Enemy(4,15.5f,11.5f);
                var dead=Enemy(0,12.5f,10.5f); dead.Health=0;
                var enemies=new List<EnemyState>{first,upper,lower,last,dead}; if(reverse) enemies.Reverse();
                Fire(Balance(),Tower(),enemies,out var shots);
                Assert.That(shots.Count,Is.EqualTo(4));
                Assert.That(shots[0].To.X,Is.EqualTo(first.Position.X));
                Assert.That(shots[1].To.Y,Is.EqualTo(upper.Position.Y),"Equal distances break ties by ID.");
                Assert.That(shots[2].To.Y,Is.EqualTo(lower.Position.Y),"Already hit neighbors cannot be chosen again.");
                Assert.That(shots[3].To.X,Is.EqualTo(last.Position.X));
                foreach(var enemy in new[]{first,upper,lower,last}) Assert.That(enemy.Health,Is.EqualTo(88));
                Assert.That(dead.Health,Is.Zero);
            }
        }

        [Test] public void ConfiguredJumpRangeStopsTheChainImmediatelyOutsideItsBoundary()
        {
            var balance=Balance(); balance.Structure(StructureKind.Ricochet).Range=1.5f;
            var first=Enemy(1,11.5f,10.5f); var second=Enemy(2,13.5f,10.5f);
            balance.Structure(StructureKind.Ricochet).JumpRange=1.99f;
            var shortStats=Fire(balance,Tower(),new List<EnemyState>{first,second},out _);
            Assert.That(first.Health,Is.EqualTo(88)); Assert.That(second.Health,Is.EqualTo(100));
            first.Health=second.Health=100; balance.Structure(StructureKind.Ricochet).JumpRange=2;
            var exactStats=Fire(balance,Tower(),new List<EnemyState>{first,second},out _);
            Assert.That(second.Health,Is.EqualTo(88));
            Assert.That(shortStats.Damage(StructureKind.Ricochet),Is.EqualTo(12));
            Assert.That(exactStats.Damage(StructureKind.Ricochet),Is.EqualTo(24));
        }

        [Test] public void HighTargetCapCannotRepeatVictimsOrCreateExtraVisualSegments()
        {
            var balance=Balance(); balance.Structure(StructureKind.Ricochet).MaxTargets=8;
            var first=Enemy(1,11.5f,10.5f); var second=Enemy(2,12.5f,10.5f);
            var stats=Fire(balance,Tower(),new List<EnemyState>{first,second},out var shots);
            Assert.That(first.Health,Is.EqualTo(88)); Assert.That(second.Health,Is.EqualTo(88));
            Assert.That(stats.Damage(StructureKind.Ricochet),Is.EqualTo(24)); Assert.That(shots.Count,Is.EqualTo(2));
        }

        [Test] public void EveryChainHitUsesOrdinaryDefenseAndDamageWhileColorBudgetRemainsShared()
        {
            var balance=Balance(); balance.EnableEnemyMechanics=true; balance.Effects.LimitTowerColor=true;
            balance.Effects.HatchedDefense=.25f; balance.Structure(StructureKind.Ricochet).MaxTargets=5;
            balance.Structure(StructureKind.Ricochet).Range=2;
            var tower=Tower(); tower.Color=InkColor.Blue; var enemies=new List<EnemyState>();
            for(int i=0;i<5;i++) {var enemy=Enemy(i+1,12.5f+i*3,10.5f); enemy.DrawingTier=DrawingTier.Hatched; enemies.Add(enemy);}
            var stats=Fire(balance,tower,enemies,out var shots); int colored=0;
            foreach(var enemy in enemies) {Assert.That(enemy.Health,Is.EqualTo(91)); if(enemy.BlueRemaining>0) colored++;}
            Assert.That(colored,Is.EqualTo(3)); Assert.That(stats.Damage(StructureKind.Ricochet),Is.EqualTo(45));
            Assert.That(shots.Count,Is.EqualTo(5)); Assert.That(stats.Launches(StructureKind.Ricochet),Is.EqualTo(1));
        }

        [Test] public void GunAndRicochetUseIndependentFirstVictimsAndOneSharedProductionFeed()
        {
            var balance=Balance(); balance.Construction.DynamicFeed=true;
            balance.Structure(StructureKind.Gun).Range=2; balance.Structure(StructureKind.Gun).Damage=4;
            var blueprint=new TowerBlueprint("gun-ricochet","Gun and ricochet",ChassisKind.Standard,StructureKind.Gun,StructureKind.Ricochet);
            var tower=Tower(blueprint); tower.Kind=StructureKind.Gun; tower.Priority=TargetPriority.Siege;
            var far=Enemy(1,14.5f,10.5f,EnemyKind.Gnawer); var near=Enemy(2,11.5f,10.5f); near.Health=4;
            var branch=Enemy(3,17.5f,10.5f);
            var stats=Fire(balance,tower,new List<EnemyState>{far,near,branch},out var shots);
            Assert.That(near.Health,Is.Zero); Assert.That(far.Health,Is.EqualTo(88)); Assert.That(branch.Health,Is.EqualTo(88));
            Assert.That(tower.PrimaryTargetId,Is.EqualTo(2)); Assert.That(tower.SecondaryTargetId,Is.EqualTo(1));
            Assert.That(tower.PrimaryAim.X,Is.EqualTo(near.Position.X)); Assert.That(tower.SecondaryAim.X,Is.EqualTo(far.Position.X));
            Assert.That(tower.Cooldown,Is.EqualTo(.42f/.8f).Within(.00001f));
            Assert.That(tower.SecondaryCooldown,Is.EqualTo(.85f/.8f).Within(.00001f));
            Assert.That(stats.Damage(StructureKind.Gun),Is.EqualTo(4)); Assert.That(stats.Damage(StructureKind.Ricochet),Is.EqualTo(24));
            Assert.That(shots.Count,Is.EqualTo(3));
        }

        [Test] public void RealTrialHonorsBothConfiguredJumpRangeAndTotalVictimLimitAtEqualBudget()
        {
            var balance=Balance(); var weapon=balance.Structure(StructureKind.Ricochet);
            var blueprint=new TowerBlueprint("trial-ricochet","Ricochet",ChassisKind.Standard,StructureKind.Ricochet);
            weapon.JumpRange=.01f;
            var shortChain=TrialRunner.Run(blueprint,balance,TrialScenario.Crowd,130);
            weapon.JumpRange=3;
            var fullChain=TrialRunner.Run(blueprint,balance,TrialScenario.Crowd,130);
            weapon.MaxTargets=1;
            var oneVictim=TrialRunner.Run(blueprint,balance,TrialScenario.Crowd,130);
            Assert.That(fullChain.Copies,Is.EqualTo(2)); Assert.That(fullChain.Cost,Is.EqualTo(130));
            Assert.That(shortChain.Damage,Is.GreaterThan(0)); Assert.That(fullChain.Damage,Is.GreaterThan(shortChain.Damage));
            Assert.That(fullChain.Damage,Is.GreaterThan(oneVictim.Damage));
            Assert.That(weapon.JumpRange,Is.EqualTo(3)); Assert.That(weapon.MaxTargets,Is.EqualTo(1));
        }
    }
}
