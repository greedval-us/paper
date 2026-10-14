using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
namespace PaperDefense.Tests
{
    public sealed class FanVolleyCombatTests
    {
        static EnemyState Enemy(int id,float x,float y,float health=100)=>new EnemyState { Id=id,Position=new Point2(x,y),Health=health,MaxHealth=health };
        static List<ShotEvent> Fire(FanVolley pattern,List<EnemyState> enemies,int mount=0)
        {
            var balance=GameBalance.CreateDefault();balance.Construction.AcquisitionSeconds=0;
            var blueprint=(mount==0?new TowerBlueprint("fan","Fan",ChassisKind.Standard,StructureKind.Fan):new TowerBlueprint("fan","Fan",ChassisKind.Heavy,StructureKind.Gun,StructureKind.Fan)).WithFanVolley(mount,pattern);
            var tower=new StructureState { Id=1,Cell=new Cell(10,10),Kind=blueprint.FirstWeapon,Blueprint=blueprint,IsComplete=true,Tier=MaterialTier.Pencil,Health=100,MaxHealth=100 };
            var shots=new List<ShotEvent>();new WeaponSystem(balance).FireIsolatedMount(tower,mount,enemies,shots,1f/30,1,new RunStatistics(),new SimulationEventBuffer(),0);
            return shots;
        }
        [Test] public void OneGuideConcentratesAllSixPelletsWithOneSharedAction()
        {
            var enemies=new List<EnemyState>{Enemy(1,13.5f,10.5f),Enemy(2,14.5f,10.5f)};
            var shots=Fire(new FanVolley(new[]{FanGuide.FromAngle(0)}),enemies);
            Assert.That(shots.Count,Is.EqualTo(6));Assert.That(enemies[0].Health,Is.EqualTo(40));Assert.That(enemies[1].Health,Is.EqualTo(100));
            Assert.That(shots.Select(s=>s.ActionId).Distinct().Count(),Is.EqualTo(1));
        }
        [Test] public void SideGuidesMissAimAndKeepFullRangeInsteadOfSeekingTargets()
        {
            var enemies=new List<EnemyState>{Enemy(1,13.5f,10.5f)};
            var shots=Fire(new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(30)}),enemies,1);
            Assert.That(shots.Count,Is.EqualTo(6));Assert.That(enemies[0].Health,Is.EqualTo(100));
            foreach(var shot in shots){Assert.That(shot.MountSlotIndex,Is.EqualTo(1));float x=shot.To.X-shot.From.X,y=shot.To.Y-shot.From.Y;Assert.That(x*x+y*y,Is.EqualTo(36).Within(.0001));}
        }
        [Test] public void KillingFrontTargetDoesNotMakeSimultaneousPelletsPierceIt()
        {
            var enemies=new List<EnemyState>{Enemy(1,12.5f,10.5f,10),Enemy(2,14.5f,10.5f)};
            var shots=Fire(new FanVolley(new[]{FanGuide.FromAngle(0)}),enemies);
            Assert.That(enemies[0].Health,Is.Zero);Assert.That(enemies[1].Health,Is.EqualTo(100));Assert.That(shots.Count,Is.EqualTo(6));
            Assert.That(shots.Select(s=>s.To).Distinct().Count(),Is.EqualTo(1));
        }
        [Test] public void EveryPelletUsesNormalDefenseAndSharesTheTowerColorBudget()
        {
            var balance=GameBalance.CreateDefault();balance.EnableEnemyMechanics=true;balance.Effects.LimitTowerColor=true;balance.Effects.HatchedDefense=.2f;balance.Construction.AcquisitionSeconds=0;
            var angles=new[]{-40f,-24,-8,8,24,40};var pattern=new FanVolley(angles.Select(FanGuide.FromAngle));
            var blueprint=new TowerBlueprint("fan","Fan",ChassisKind.Standard,StructureKind.Fan).WithFanVolley(0,pattern);
            var tower=new StructureState{Id=1,Cell=new Cell(10,10),Kind=StructureKind.Fan,Blueprint=blueprint,Tier=MaterialTier.Pencil,IsComplete=true,Color=InkColor.Blue,Health=100};
            var enemies=new List<EnemyState>{Enemy(1,12.5f,10.5f)};
            for(int i=0;i<angles.Length;i++){double a=angles[i]*System.Math.PI/180;var enemy=Enemy(i+2,10.5f+4*(float)System.Math.Cos(a),10.5f-4*(float)System.Math.Sin(a));enemy.DrawingTier=DrawingTier.Hatched;enemies.Add(enemy);}
            var shots=new List<ShotEvent>();new WeaponSystem(balance).Fire(new List<StructureState>{tower},enemies,shots,1,1,1f/30,1,new RunStatistics(),new SimulationEventBuffer());
            Assert.That(shots.Count,Is.EqualTo(6));Assert.That(enemies[0].Health,Is.EqualTo(100));
            Assert.That(enemies.Skip(1).All(e=>e.Health==92),Is.True);Assert.That(enemies.Count(e=>e.BlueRemaining>0),Is.EqualTo(3));
        }
        [Test] public void ProbeUsesTheSameOneActionAndKeepsBudgetUnchanged()
        {
            var balance=GameBalance.CreateDefault();var original=new TowerBlueprint("fan","Fan",ChassisKind.Standard,StructureKind.Fan);
            var a=FanVolleyProbe.Run(original.WithFanVolley(0,new FanVolley(new[]{FanGuide.FromAngle(0)})),0,FanFormation.Single,balance);
            var b=FanVolleyProbe.Run(original.WithFanVolley(0,new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(0),FanGuide.FromAngle(30)})),0,FanFormation.Front,balance);
            Assert.That(a.Completed&&b.Completed,Is.True);Assert.That(a.Pellets,Is.EqualTo(6));Assert.That(b.Pellets,Is.EqualTo(6));
            Assert.That(a.Damage,Is.EqualTo(60));Assert.That(a.Hits,Is.EqualTo(1));Assert.That(b.Hits,Is.EqualTo(3));Assert.That(a.Cost,Is.EqualTo(b.Cost));
            Assert.That(b.Frames.Last().Shots.Count,Is.EqualTo(b.Pellets));Assert.That(original.GetFanVolley(0),Is.Null);
        }
    }
}
