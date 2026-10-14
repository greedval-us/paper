using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;
namespace PaperDefense.Tests
{
    public sealed class ArmorCombatIntegrationTests
    {
        static TowerBlueprint Page(int facing)=>new TowerBlueprint("armor","Armor",ChassisKind.Standard,StructureKind.Gun,modules:TowerModules.Armor)
            .WithReinforcement(new ArmorDistribution(new[]{12,0,0,0},facing));
        static GameBalance Rules()
        {
            var b=GameBalance.CreateDefault();b.EnableEnemyMechanics=true;b.Construction.ArmorDefense=.25f;b.Construction.ArmorHealthMultiplier=1;
            b.Effects.MaximumDefense=.6f;b.Construction.FoundationHealth=1000;
            b.Structure(StructureKind.Gun).BuildSeconds=b.Construction.ArmorBuildSeconds=0;return b;
        }
        [TestCase(3,40f)] [TestCase(1,100f)]
        public void InterceptedGlobUsesItsLaunchSideEvenWhenItsDestinationIsBeyondTower(int facing,float damage)
        {
            var session=new GameSession(Rules(),starterDefenses:false);
            Assert.That(session.TryBuildBlueprint(new[]{new Cell(6,5)},Page(facing),MaterialTier.Pencil,InkColor.None,out string reason),Is.True,reason);
            var tower=session.Structures[0];float health=tower.Health;
            ((List<EnemyGlob>)session.EnemyProjectiles).Add(new EnemyGlob{Id=1,From=new Point2(4.5f,5.5f),To=new Point2(8.5f,5.5f),Damage=100,Duration=0,Remaining=0,ObjectiveIndex=int.MinValue});
            session.Tick(.01f);Assert.That(health-tower.Health,Is.EqualTo(damage).Within(.001));Assert.That(session.EnemyProjectiles,Is.Empty);
        }
        [TestCase(3,10.8f)] [TestCase(1,27f)]
        public void RealMeleeHitUsesAttackerSideRatherThanTurretAim(int facing,float damage)
        {
            var b=Rules();b.Width=20;b.Height=16;b.Structure(StructureKind.Wall).MaxHealth=1000;b.Structure(StructureKind.Wall).BuildSeconds=0;
            b.Waves.Clear();b.Waves.Add(new WaveDefinition{Count=1,GnawerFraction=1});var session=new GameSession(b,starterDefenses:false);
            var ring=new List<Cell>();for(int x=8;x<=12;x++){ring.Add(new Cell(x,6));ring.Add(new Cell(x,10));}
            for(int y=7;y<=9;y++){if(y!=8)ring.Add(new Cell(8,y));ring.Add(new Cell(12,y));}
            Assert.That(session.TryBuild(ring,StructureKind.Wall,MaterialTier.Pencil,out _),Is.True);
            Assert.That(session.TryBuildBlueprint(new[]{new Cell(8,8)},Page(facing),MaterialTier.Pencil,InkColor.None,out string reason),Is.True,reason);
            var tower=session.StructureAt(new Cell(8,8));float health=tower.Health;
            session.StartNextWave();session.Tick(.01f);session.Enemies[0].Position=new Point2(7.5f,8.5f);session.Tick(.01f);
            Assert.That(health-tower.Health,Is.EqualTo(damage).Within(.001));
        }
        [Test] public void CounterfireTrialHonorsFixedFacing()
        {
            var b=Rules();b.Structure(StructureKind.Gun).Damage=0;
            var front=TrialRunner.Run(Page(3),b,TrialScenario.Crowd,100,counterfire:true);
            var rear=TrialRunner.Run(Page(1),b,TrialScenario.Crowd,100,counterfire:true);
            int index=-1;for(int i=0;i<rear.Frames.Count;i++)if(rear.Frames[i].Towers[0].Health<1000){index=i;break;}
            Assert.That(index,Is.GreaterThan(0));
            Assert.That(1000-front.Frames[index].Towers[0].Health,Is.LessThan(1000-rear.Frames[index].Towers[0].Health));
        }
    }
}
