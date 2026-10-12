using NUnit.Framework;
using PaperDefense.Core;
using System.Collections.Generic;

namespace PaperDefense.Tests
{
    public class InkLoopTests
    {
        static GameBalance Balance()
        {
            var b=GameBalance.CreateDefault(); b.Width=96;b.Height=64;b.EnableInkWells=true;
            b.StartingInk=2000;b.PassiveInkPerSecond=0;b.PlanningSeconds=1000;b.EarlyLaunchInkPerSecond=0;
            b.Waves=new List<WaveDefinition>{new WaveDefinition{Count=0},new WaveDefinition{Count=0},new WaveDefinition{Count=0}};
            return b;
        }
        static TowerBlueprint Double()=>new TowerBlueprint("double","Два",ChassisKind.Standard,StructureKind.Gun,StructureKind.Gun);
        [Test] public void ColorCartridgeCostDependsOnAssemblyAndNotDrawingMaterial()
        {
            var game=new GameSession(Balance(),starterDefenses:false);var bp=Double();
            Assert.That(game.ColorCost(bp,MaterialTier.Marker),Is.EqualTo(game.ColorCost(bp,MaterialTier.Pencil)));
            Assert.That(game.ColorCost(bp,MaterialTier.Pencil),Is.GreaterThan(game.ColorCost(
                new TowerBlueprint("single","Один",ChassisKind.Standard,StructureKind.Gun),MaterialTier.Pencil)));
        }
        [Test] public void BlueprintStrokeIsAtomicAndChargesActualAssemblyOncePerCell()
        {
            var b=Balance();var g=new GameSession(b,starterDefenses:false);var bp=Double();
            Assert.That(g.TryBuildBlueprint(new[]{new Cell(10,10),g.BaseCell},bp,MaterialTier.Pencil,InkColor.None,out _),Is.False);
            Assert.That(g.Ink,Is.EqualTo(2000));
            Assert.That(g.TryBuildBlueprint(new[]{new Cell(10,10),new Cell(10,10),new Cell(11,10)},bp,MaterialTier.Pencil,InkColor.None,out _),Is.True);
            Assert.That(g.Ink,Is.EqualTo(2000-2*TowerCatalog.Cost(bp,b,MaterialTier.Pencil)));
            Assert.That(g.Structures.Count,Is.EqualTo(2));
            Assert.That(g.Structures[0].Blueprint,Is.SameAs(bp));
            Assert.That(g.Structures[0],Is.Not.SameAs(g.Structures[1]));
        }
        [Test] public void FailedColorBuildDoesNotSpendOrdinaryInk()
        {
            var g=new GameSession(Balance(),starterDefenses:false);
            Assert.That(g.TryBuildBlueprint(new[]{new Cell(10,10)},Double(),MaterialTier.Pencil,InkColor.Blue,out _),Is.False);
            Assert.That(g.Ink,Is.EqualTo(2000));Assert.That(g.Structures,Is.Empty);
        }
        [Test] public void WellPaysOnlyForWholeProtectedWaveAndNeverForWaiting()
        {
            var b=Balance();var g=new GameSession(b,starterDefenses:false);
            Assert.That(g.TryActivateWell(0,out _),Is.True);
            g.Tick(5);Assert.That(g.ColoredInk(InkColor.Red),Is.Zero);
            g.StartNextWave();g.Tick(.04f);
            Assert.That(g.ColoredInk(InkColor.Red),Is.EqualTo(b.WellInkPerWave));
            Assert.That(g.GoldEarned,Is.EqualTo(b.WaveGold));
        }
        [Test] public void ActivationDuringAssaultDoesNotEarnPartialWave()
        {
            var g=new GameSession(Balance(),starterDefenses:false);g.StartNextWave();
            Assert.That(g.TryActivateWell(1,out _),Is.True);g.Tick(.04f);
            Assert.That(g.ColoredInk(InkColor.Blue),Is.Zero);
            g.StartNextWave();g.Tick(.04f);
            Assert.That(g.ColoredInk(InkColor.Blue),Is.EqualTo(g.Balance.WellInkPerWave));
        }
        [Test] public void WellLossIsOptionalRecapturableAndDoesNotDisablePaint()
        {
            var b=Balance();var g=new GameSession(b,starterDefenses:false);g.TryActivateWell(0,out _);
            g.StartNextWave();g.Tick(.04f);
            var bp=Double();var cell=new Cell(10,10);g.TryBuildBlueprint(new[]{cell},bp,MaterialTier.Pencil,InkColor.Red,out _);
            Assert.That(g.StructureAt(cell).Color,Is.EqualTo(InkColor.Red));
            g.DamageObjective(0,10000);
            Assert.That(g.Phase,Is.Not.EqualTo(RunPhase.Defeat));Assert.That(g.Wells[0].Active,Is.False);
            Assert.That(g.StructureAt(cell).Color,Is.EqualTo(InkColor.Red));
            Assert.That(g.TryActivateWell(0,out _),Is.True);Assert.That(g.Wells[0].Health,Is.EqualTo(b.WellHealth));
        }
        [Test] public void PaintChargesOnceAndMaterialUpgradePreservesAssemblyAndColor()
        {
            var b=Balance();foreach(var d in b.StructureDefinitions)d.BuildSeconds=0;
            var g=new GameSession(b,starterDefenses:false);g.TryActivateWell(1,out _);g.StartNextWave();g.Tick(.04f);
            var bp=new TowerBlueprint("one","Один",ChassisKind.Standard,StructureKind.Gun);var cell=new Cell(10,10);
            g.TryBuildBlueprint(new[]{cell},bp,MaterialTier.Pencil,InkColor.None,out _);
            Assert.That(g.TryPaint(cell,InkColor.Blue,out _),Is.True);int color=g.ColoredInk(InkColor.Blue);
            Assert.That(g.TryPaint(cell,InkColor.Blue,out _),Is.True);Assert.That(g.ColoredInk(InkColor.Blue),Is.EqualTo(color));
            Assert.That(g.TryUpgrade(cell,out _),Is.True);Assert.That(g.StructureAt(cell).Blueprint,Is.SameAs(bp));
            Assert.That(g.StructureAt(cell).Color,Is.EqualTo(InkColor.Blue));
        }
        [Test] public void ObjectiveCellsAreReservedAndGoldDoesNotAccrueWithTime()
        {
            var g=new GameSession(Balance(),starterDefenses:false);g.Tick(10);
            Assert.That(g.GoldEarned,Is.Zero);
            Assert.That(g.TryBuild(new[]{g.Wells[0].Cell},StructureKind.Wall,MaterialTier.Pencil,out _),Is.False);
        }
        [Test] public void ColorsExpireAndRedDealsDamageWhenEnemyBlotsAreDisabled()
        {
            var b=Balance();b.EnableEnemyMechanics=false;
            var g=new GameSession(b,starterDefenses:false);
            var enemy=new EnemyState{Id=1,Kind=EnemyKind.Doodle,Position=new Point2(10,10),Health=100,MaxHealth=100};
            ((List<EnemyState>)g.Enemies).Add(enemy);
            EnemyEffects.ApplyHit(enemy,0,InkColor.Red,g.Statistics,StructureKind.Gun);
            EnemyEffects.ApplyHit(enemy,0,InkColor.Blue,g.Statistics,StructureKind.Gun);
            EnemyEffects.ApplyHit(enemy,0,InkColor.Green,g.Statistics,StructureKind.Gun);
            g.Tick(1);
            Assert.That(enemy.Health,Is.EqualTo(96).Within(.01f));Assert.That(enemy.BlueRemaining,Is.EqualTo(2).Within(.01f));
            g.Tick(2.1f);
            Assert.That(enemy.Health,Is.EqualTo(88).Within(.01f));
            Assert.That(enemy.RedRemaining,Is.Zero);Assert.That(enemy.BlueRemaining,Is.Zero);Assert.That(enemy.GreenRemaining,Is.Zero);
        }
        [TestCase(0f, -1)]
        [TestCase(1f, 0)]
        public void ConfiguredWellThreatShareControlsObjectives(float share, int objective)
        {
            var balance = Balance(); balance.Progression.WellThreatFraction = share;
            balance.Waves = new List<WaveDefinition> { new WaveDefinition { Count = 32, SpawnInterval = .005f } };
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryActivateWell(0, out _), Is.True);
            game.StartNextWave(); game.Tick(.2f);
            Assert.That(game.Enemies.Count, Is.EqualTo(32));
            foreach (var enemy in game.Enemies) Assert.That(enemy.ObjectiveIndex, Is.EqualTo(objective));
        }
    }
}
