using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class StaplerWeaponTests
    {
        static GameBalance Balance()
        {
            var balance=GameBalance.CreateDefault(); var weapon=balance.Structure(StructureKind.Stapler);
            weapon.Cost=35; weapon.MaxHealth=55; weapon.BuildSeconds=1.2f; weapon.Range=9;
            weapon.Damage=4; weapon.ShotInterval=.65f; weapon.SlowSpeedMultiplier=.8f; weapon.SlowDuration=2;
            balance.Construction.AcquisitionSeconds=0;
            return balance;
        }
        static EnemyState Enemy(int id=1,float x=12.5f,EnemyKind kind=EnemyKind.Doodle)
            =>new EnemyState {Id=id,Kind=kind,Position=new Point2(x,10.5f),Health=100,MaxHealth=100};
        static StructureState Tower(TowerBlueprint blueprint=null)
            =>new StructureState {Id=1,Cell=new Cell(10,10),Kind=StructureKind.Stapler,
                Health=55,IsComplete=true,Tier=MaterialTier.Pencil,
                Blueprint=blueprint??new TowerBlueprint("stapler","Stapler",ChassisKind.Standard,StructureKind.Stapler)};
        static RunStatistics Fire(GameBalance balance,StructureState tower,EnemyState enemy,float? quality=null)
        {
            var stats=new RunStatistics();
            new WeaponSystem(balance,quality).Fire(new List<StructureState>{tower},new List<EnemyState>{enemy},new List<ShotEvent>(),
                1,1,1f/30,1,stats,new SimulationEventBuffer());
            return stats;
        }

        [Test] public void UncoloredStaplerDealsItsDamageAndAppliesOnlyItsConfiguredIntrinsicSlow()
        {
            var balance=Balance(); var enemy=Enemy(); var stats=Fire(balance,Tower(),enemy);
            Assert.That(enemy.Health,Is.EqualTo(96)); Assert.That(stats.Damage(StructureKind.Stapler),Is.EqualTo(4));
            Assert.That(stats.Launches(StructureKind.Stapler),Is.EqualTo(1));
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(.8f));
            Assert.That(enemy.BlueRemaining,Is.EqualTo(2));
            EnemyEffects.Advance(enemy,1.9f,null,balance.Effects);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(.8f));
            EnemyEffects.Advance(enemy,.11f,null,balance.Effects);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(1));
        }

        [Test] public void WeakStaplesDoNotMultiplyOrExtendBlueAndTheirOwnRemainderResumes()
        {
            var balance=Balance(); balance.Effects.BlueDuration=1; var enemy=Enemy();
            EnemyEffects.ApplyHit(enemy,0,InkColor.Blue,null,StructureKind.Gun,balance.Effects);
            Fire(balance,Tower(),enemy);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(.65f));
            EnemyEffects.Advance(enemy,.75f,null,balance.Effects);
            Fire(balance,Tower(),enemy);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(.65f));
            EnemyEffects.Advance(enemy,.26f,null,balance.Effects);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(.8f),"A weak refresh must not prolong the stronger blue effect.");
            EnemyEffects.Advance(enemy,1.75f,null,balance.Effects);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(1));
        }

        [Test] public void AConfiguredStrongerShortStapleExpiresBackToTheLongerBlueEffect()
        {
            var balance=Balance(); balance.Structure(StructureKind.Stapler).SlowSpeedMultiplier=.5f;
            balance.Structure(StructureKind.Stapler).SlowDuration=.4f; balance.Effects.BlueDuration=2;
            var enemy=Enemy(); EnemyEffects.ApplyHit(enemy,0,InkColor.Blue,null,StructureKind.Gun,balance.Effects);
            Fire(balance,Tower(),enemy);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(.5f));
            EnemyEffects.Advance(enemy,.41f,null,balance.Effects);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(.65f));
            EnemyEffects.Advance(enemy,1.6f,null,balance.Effects);
            Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(1));
        }

        [Test] public void DeniedInkBudgetDoesNotSuppressOrdinaryDamageOrTheIntrinsicStapleSlow()
        {
            var balance=Balance(); balance.Effects.LimitTowerColor=true; balance.Effects.ColorTargetsPerWindow=1;
            var tower=Tower(); tower.Color=InkColor.Green; var first=Enemy(); var second=Enemy(2,50);
            var enemies=new List<EnemyState>{first,second}; var towers=new List<StructureState>{tower};
            var weapons=new WeaponSystem(balance); var stats=new RunStatistics(); var shots=new List<ShotEvent>(); var events=new SimulationEventBuffer();
            weapons.Fire(towers,enemies,shots,1,1,1f/30,1,stats,events,.01);
            Assert.That(first.GreenRemaining,Is.GreaterThan(0));
            first.Position=new Point2(50,10.5f); second.Position=new Point2(12.5f,10.5f); tower.Cooldown=0;
            weapons.Fire(towers,enemies,shots,1,1,.69f,2,stats,events,.7);
            Assert.That(second.GreenRemaining,Is.Zero,"The second target has no remaining color entitlement in this window.");
            Assert.That(second.Health,Is.EqualTo(96)); Assert.That(stats.Damage(StructureKind.Stapler),Is.EqualTo(8));
            Assert.That(EnemyEffects.MovementMultiplier(second,balance.Effects),Is.EqualTo(.8f));
        }

        [Test] public void HistoricalDrawingQualityDoesNotChangeStaplerCadenceDamageOrSlow()
        {
            var balance=Balance(); var slowTower=Tower(); var perfectTower=Tower(); var slowEnemy=Enemy(); var perfectEnemy=Enemy();
            Fire(balance,slowTower,slowEnemy,0); Fire(balance,perfectTower,perfectEnemy,100);
            Assert.That(slowTower.Cooldown,Is.EqualTo(.65f).Within(.00001f));
            Assert.That(perfectTower.Cooldown,Is.EqualTo(.65f).Within(.00001f));
            Assert.That(slowEnemy.Health,Is.EqualTo(perfectEnemy.Health));
            Assert.That(EnemyEffects.MovementMultiplier(slowEnemy,balance.Effects),Is.EqualTo(.8f));
            Assert.That(EnemyEffects.MovementMultiplier(perfectEnemy,balance.Effects),Is.EqualTo(.8f));
            Assert.That(slowEnemy.BlueRemaining,Is.EqualTo(2)); Assert.That(perfectEnemy.BlueRemaining,Is.EqualTo(2));
        }

        [Test] public void GunAndStaplerKeepIndependentTargetsAndShareTheSameProductionRule()
        {
            var balance=Balance(); balance.Construction.DynamicFeed=true;
            balance.Structure(StructureKind.Gun).Range=2; balance.Structure(StructureKind.Gun).Damage=4;
            var blueprint=new TowerBlueprint("gun-stapler","Gun and stapler",ChassisKind.Standard,StructureKind.Gun,StructureKind.Stapler);
            var tower=Tower(blueprint); tower.Kind=StructureKind.Gun; tower.Priority=TargetPriority.Siege;
            var far=Enemy(1,14.5f,EnemyKind.Gnawer); var near=Enemy(2,11.5f); near.Health=4;
            var stats=new RunStatistics(); var shots=new List<ShotEvent>();
            new WeaponSystem(balance).Fire(new List<StructureState>{tower},new List<EnemyState>{far,near},shots,
                1,1,1f/30,1,stats,new SimulationEventBuffer());
            Assert.That(near.Health,Is.Zero); Assert.That(far.Health,Is.EqualTo(96));
            Assert.That(tower.PrimaryTargetId,Is.EqualTo(2)); Assert.That(tower.SecondaryTargetId,Is.EqualTo(1));
            Assert.That(tower.PrimaryAim.X,Is.EqualTo(near.Position.X)); Assert.That(tower.SecondaryAim.X,Is.EqualTo(far.Position.X));
            Assert.That(EnemyEffects.MovementMultiplier(far,balance.Effects),Is.EqualTo(.8f));
            Assert.That(tower.Cooldown,Is.EqualTo(.42f/.8f).Within(.00001f));
            Assert.That(tower.SecondaryCooldown,Is.EqualTo(.65f/.8f).Within(.00001f));
            Assert.That(stats.Damage(StructureKind.Gun),Is.EqualTo(4)); Assert.That(stats.Damage(StructureKind.Stapler),Is.EqualTo(4));
            Assert.That(shots.Count,Is.EqualTo(2));
        }

        [Test] public void RealTrialUsesConfiguredSlowStrengthAndDurationWithoutMutatingItsRules()
        {
            var balance=Balance(); var weapon=balance.Structure(StructureKind.Stapler);
            weapon.Range=100; weapon.Damage=.01f; weapon.SlowSpeedMultiplier=.2f; weapon.SlowDuration=0;
            var blueprint=new TowerBlueprint("trial-stapler","Stapler",ChassisKind.Standard,StructureKind.Stapler);
            var noDuration=TrialRunner.Run(blueprint,balance,TrialScenario.Crowd,35);
            weapon.SlowDuration=6; weapon.SlowSpeedMultiplier=1;
            var noReduction=TrialRunner.Run(blueprint,balance,TrialScenario.Crowd,35);
            weapon.SlowSpeedMultiplier=.2f;
            var slowed=TrialRunner.Run(blueprint,balance,TrialScenario.Crowd,35);
            Assert.That(slowed.Copies,Is.EqualTo(1)); Assert.That(slowed.Cost,Is.EqualTo(35));
            float baselineX=FirstEnemyAtOneSecond(noDuration).Position.X;
            Assert.That(FirstEnemyAtOneSecond(noReduction).Position.X,Is.EqualTo(baselineX).Within(.0001f));
            Assert.That(FirstEnemyAtOneSecond(slowed).Position.X,Is.LessThan(baselineX-.5f));
            Assert.That(FirstEnemyAtOneSecond(noDuration).BlueRemaining,Is.Zero);
            Assert.That(FirstEnemyAtOneSecond(slowed).BlueRemaining,Is.GreaterThan(0));
            Assert.That(weapon.SlowSpeedMultiplier,Is.EqualTo(.2f)); Assert.That(weapon.SlowDuration,Is.EqualTo(6));
        }

        static EnemyView FirstEnemyAtOneSecond(TrialReport report)
        {
            foreach(var frame in report.Frames)
                if(frame.Seconds>=.999f)
                {
                    foreach(var enemy in frame.Enemies) if(enemy.Id==1) return enemy;
                    break;
                }
            Assert.Fail("The bounded lane must retain its first low-damage target at one second.");
            return default;
        }
    }
}
