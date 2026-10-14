using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class TowerDefenseRulesTests
    {
        static GameBalance Map()
        {
            var b=GameBalance.CreateDefault(); b.Width=20;b.Height=16;b.StartingInk=1000;b.PassiveInkPerSecond=0;
            b.PlanningSeconds=1000;b.EnableEnemyMechanics=true;b.EnableInkWells=false;
            b.Battlefield=new BattlefieldDefinition{Enabled=true};
            for(int x=0;x<=10;x++)b.Battlefield.Roads.Add(new BattlefieldCell(x,8));
            b.Battlefield.Entrances.Add(new BattlefieldCell(0,8));b.Battlefield.BarricadeSites.Add(new BattlefieldCell(4,8));
            foreach(var d in b.StructureDefinitions)d.BuildSeconds=0;
            b.Waves=new List<WaveDefinition>{new WaveDefinition{Count=3,SpawnInterval=.01f,Sides=1}};
            return b;
        }
        static GameBalance OpenMap()
        {
            var b=Map();b.Battlefield.Mode=BattlefieldMode.OpenTerrain;
            b.Battlefield.Roads.Clear();b.Battlefield.BarricadeSites.Clear();
            b.Battlefield.Entrances.Add(new BattlefieldCell(19,8));
            b.Battlefield.Entrances.Add(new BattlefieldCell(10,15));
            b.Battlefield.Entrances.Add(new BattlefieldCell(10,0));
            b.Waves[0].Sides=15;return b;
        }
        [Test] public void OpenTerrainAllowsFreeConstructionAndRejectsFullTerrainFootprintsAtomically()
        {
            var b=OpenMap();b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=5,Y=3,Width=2,Height=3});
            var game=new GameSession(b,starterDefenses:false);
            foreach(var kind in new[]{StructureKind.Wall,StructureKind.Spikes,StructureKind.TarPit,StructureKind.Gun,StructureKind.RepairPost})
            {
                Assert.That(game.QuoteBuild(new[]{new Cell(3,2)},kind,MaterialTier.Pencil).Valid,Is.True,kind.ToString());
                Assert.That(game.TryBuild(new[]{new Cell(3,2),new Cell(6,5)},kind,MaterialTier.Pencil,out _),Is.False);
                Assert.That(game.Ink,Is.EqualTo(1000));Assert.That(game.Structures,Is.Empty);
            }
            var bp=new TowerBlueprint("test","test",ChassisKind.Standard,StructureKind.Gun);
            Assert.That(game.TryBuildBlueprint(new[]{new Cell(3,2),new Cell(5,3)},bp,MaterialTier.Pencil,InkColor.None,out _),Is.False);
            Assert.That(game.Ink,Is.EqualTo(1000));Assert.That(game.Structures,Is.Empty);
            Assert.That(game.TryBuild(new[]{new Cell(3,2),new Cell(4,2)},StructureKind.Wall,MaterialTier.Pencil,out _),Is.True);
            Assert.That(game.Structures.Count,Is.EqualTo(2));
            Assert.That(game.QuoteBuild(new[]{new Cell(0,3)},StructureKind.Wall,MaterialTier.Pencil).Valid,Is.False);
            Assert.That(game.QuoteBuild(new[]{game.BaseCell},StructureKind.Wall,MaterialTier.Pencil).Valid,Is.False);
        }
        [Test] public void OpenTerrainNavigationDetoursAroundMountainsAndAvoidsExpensiveWalls()
        {
            var b=OpenMap();b.Waves[0].Sides=1;b.Waves[0].Count=1;
            b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=4,Y=6,Width=3,Height=5});
            var game=new GameSession(b,17,false);
            Assert.That(game.TryBuild(new[]{new Cell(2,8)},StructureKind.Wall,MaterialTier.Pencil,out _),Is.True);
            game.StartNextWave();bool detoured=false;float originalHealth=game.StructureAt(new Cell(2,8)).Health;
            for(int i=0;i<100;i++)
            {
                game.Tick(.1f);
                foreach(var e in game.Enemies)
                {
                    var c=new Cell((int)e.Position.X,(int)e.Position.Y);
                    Assert.That(game.Battlefield.IsWalkable(c),Is.True);
                    if(c.Y<6||c.Y>=11)detoured=true;
                }
            }
            Assert.That(detoured,Is.True);Assert.That(game.StructureAt(new Cell(2,8)).Health,Is.EqualTo(originalHealth));
        }
        [Test] public void OpenTerrainRejectsDisconnectedEntranceObjectiveAndInvalidMode()
        {
            var b=OpenMap();b.Battlefield.Mode=(BattlefieldMode)17;
            Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
            b=OpenMap();b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=3,Y=0,Width=1,Height=16});
            Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
            b=NotebookTowerDefensePreset.Create();
            b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=15,Y=12,Width=9,Height=1});
            b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=15,Y=20,Width=9,Height=1});
            b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=15,Y=13,Width=1,Height=7});
            b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=23,Y=13,Width=1,Height=7});
            Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
        }
        [Test] public void OpenTerrainModeAndTerrainSnapshotsRemainIndependent()
        {
            var b=OpenMap();b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Rock,X=3,Y=3,Width=2});
            var game=new GameSession(b,starterDefenses:false);var snapshot=game.Balance;
            b.Battlefield.Mode=BattlefieldMode.FixedRoads;b.Battlefield.Props.Clear();
            snapshot.Battlefield.Mode=BattlefieldMode.FixedRoads;snapshot.Battlefield.Props[0].X=8;
            Assert.That(game.Battlefield.IsOpenTerrain,Is.True);Assert.That(game.Battlefield.IsWalkable(new Cell(4,3)),Is.False);
            Assert.That(game.Battlefield.IsWalkable(new Cell(8,3)),Is.True);
        }
        [Test] public void FourSideCongestionRetainsEverySpawnAndUsesEveryEntrance()
        {
            var b=OpenMap();b.Waves[0].Count=20;b.MaxEnemiesPerCell=1;b.Enemy(EnemyKind.Doodle).Speed=0;
            var game=new GameSession(b,17,false);game.StartNextWave();
            for(int i=0;i<200;i++)game.Tick(.05f);
            Assert.That(game.Enemies.Count,Is.EqualTo(4));Assert.That(game.RemainingToSpawn,Is.EqualTo(16));
            var sides=new HashSet<int>();foreach(var e in game.Enemies)sides.Add(game.Battlefield.Side(new Cell((int)e.Position.X,(int)e.Position.Y)));
            Assert.That(sides,Is.EquivalentTo(new[]{1,2,4,8}));
        }
        [Test] public void OpenTerrainBreachesCheaperWallRatherThanTakingLongBorderDetour()
        {
            var b=OpenMap();b.Waves[0].Sides=1;b.Waves[0].Count=1;b.Structure(StructureKind.Wall).MaxHealth=12;
            var cells=new List<Cell>();for(int y=1;y<15;y++)cells.Add(new Cell(4,y));
            var game=new GameSession(b,17,false);
            Assert.That(game.TryBuild(cells,StructureKind.Wall,MaterialTier.Pencil,out _),Is.True);
            game.StartNextWave();for(int i=0;i<70;i++)game.Tick(.1f);
            Assert.That(game.StructureAt(new Cell(4,8)),Is.Null);
            Assert.That(game.Enemies[0].Position.X,Is.GreaterThan(4.5f));
        }
        [Test] public void MainMapSpawnsFromFourSidesAndTargetsAnyActiveWell()
        {
            var b=NotebookTowerDefensePreset.Create();b.StartingInk=10000;b.Progression.WellThreatFraction=1;
            b.MaxEnemiesPerCell=100;b.Enemy(EnemyKind.Doodle).Speed=0;
            b.Waves=new List<WaveDefinition>{new WaveDefinition{Count=96,Sides=15,SpawnInterval=.005f}};
            var game=new GameSession(b,17,false);
            for(int i=0;i<game.Wells.Count;i++)Assert.That(game.TryActivateWell(i,out _),Is.True);
            game.StartNextWave();game.Tick(.5f);
            var sides=new HashSet<int>();var objectives=new HashSet<int>();
            foreach(var e in game.Enemies)
            {
                var c=new Cell((int)e.Position.X,(int)e.Position.Y);
                Assert.That(game.Battlefield.IsWalkable(c),Is.True);
                sides.Add(game.Battlefield.Side(c));objectives.Add(e.ObjectiveIndex);
            }
            Assert.That(game.Enemies.Count,Is.EqualTo(96));Assert.That(game.RemainingToSpawn,Is.Zero);
            Assert.That(sides,Is.EquivalentTo(new[]{1,2,4,8}));Assert.That(objectives,Is.EquivalentTo(new[]{0,1,2}));
            foreach(var w in game.Wells)Assert.That(w.Threatened,Is.True);
            for(int i=0;i<game.Wells.Count;i++)game.DamageObjective(i,game.Wells[i].MaxHealth);
            foreach(var e in game.Enemies)Assert.That(e.ObjectiveIndex,Is.EqualTo(-1));
        }
        [Test] public void MapRejectsDisconnectedEntranceAndObjective()
        {
            var b=Map();b.Battlefield.Roads.RemoveAt(3);Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
            b=Map();b.EnableInkWells=true;Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
        }
        [Test] public void MapRejectsDuplicateOutsideAndOverlappingTerrain()
        {
            var b=Map();b.Battlefield.Roads.Add(new BattlefieldCell(0,8));Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
            b=Map();b.Battlefield.BarricadeSites.Add(new BattlefieldCell(3,3));Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
            b=Map();b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=2,Y=8});Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
        }
        [Test] public void NextTowardCastleKeepsOffRoadBoundaryCellUnchanged()
        {
            var map=Map().ResolveBattlefield();var outside=new Cell(-1,8);
            Assert.That(map.NextTowardCastle(outside),Is.EqualTo(outside));
        }
        [Test] public void PlacementAndBlueprintQuotesAreAtomicAndUseSameMap()
        {
            var game=new GameSession(Map(),starterDefenses:false);
            Assert.That(game.TryBuild(new[]{new Cell(4,8),new Cell(5,8)},StructureKind.Wall,MaterialTier.Pencil,out _),Is.False);
            Assert.That(game.Ink,Is.EqualTo(1000));Assert.That(game.Structures,Is.Empty);
            Assert.That(game.QuoteBuild(new[]{new Cell(4,8)},StructureKind.Wall,MaterialTier.Pencil).Valid,Is.True);
            Assert.That(game.QuoteBuild(new[]{new Cell(3,7)},StructureKind.Spikes,MaterialTier.Pencil).Valid,Is.False);
            var bp=new TowerBlueprint("test","test",ChassisKind.Standard,StructureKind.Gun);
            Assert.That(game.TryBuildBlueprint(new[]{new Cell(3,7),new Cell(3,8)},bp,MaterialTier.Pencil,InkColor.None,out _),Is.False);
            Assert.That(game.Ink,Is.EqualTo(1000));Assert.That(game.Structures,Is.Empty);
            Assert.That(game.QuoteBlueprint(new[]{new Cell(3,7)},bp,MaterialTier.Pencil,InkColor.None).Valid,Is.True);
        }
        [Test] public void BattlefieldAndBalanceSnapshotsAreIndependent()
        {
            var b=Map();var game=new GameSession(b,starterDefenses:false);var view=new RunReadModel(game);
            b.Battlefield.Roads.Clear();game.Balance.Battlefield.Roads.Clear();
            Assert.That(view.Battlefield.IsRoad(new Cell(3,8)),Is.True);
            Assert.That(view.Battlefield.Roads,Is.Not.InstanceOf<List<Cell>>());
        }
        [Test] public void SpawnCongestionKeepsBacklogAndEnemiesStayOnRoad()
        {
            var b=Map();b.MaxEnemiesPerCell=1;b.Enemy(EnemyKind.Doodle).Speed=0;
            var game=new GameSession(b,starterDefenses:false);game.StartNextWave();game.Tick(.5f);
            Assert.That(game.Enemies.Count,Is.EqualTo(1));Assert.That(game.RemainingToSpawn,Is.EqualTo(2));
            Assert.That(game.Battlefield.IsRoad(new Cell((int)game.Enemies[0].Position.X,(int)game.Enemies[0].Position.Y)),Is.True);
        }
        [Test] public void RepairUsesStrongestSinglePostAndBoundsHealth()
        {
            var b=Map();var game=new GameSession(b,starterDefenses:false);
            game.TryBuild(new[]{new Cell(4,8)},StructureKind.Wall,MaterialTier.Pencil,out _);
            game.TryBuild(new[]{new Cell(3,7),new Cell(5,7)},StructureKind.RepairPost,MaterialTier.Pencil,out _);
            var wall=game.StructureAt(new Cell(4,8));wall.Health=10;
            game.Tick(.5f);Assert.That(wall.Health,Is.EqualTo(14).Within(.001));
            wall.Health=wall.MaxHealth-1;game.Tick(.5f);Assert.That(wall.Health,Is.EqualTo(wall.MaxHealth));
            var post=game.StructureAt(new Cell(3,7));post.Health=10;game.Tick(.5f);Assert.That(post.Health,Is.EqualTo(10));
            wall.Health=0;game.Tick(.5f);Assert.That(wall.Health,Is.Zero);
        }
        [Test] public void StrongSlowsDoNotMultiplyAndWeakerEffectResumes()
        {
            var e=new EnemyState{Health=100,MaxHealth=100};EnemyEffects.ApplySlow(e,.65f,2);EnemyEffects.ApplySlow(e,.8f,3);
            Assert.That(EnemyEffects.MovementMultiplier(e),Is.EqualTo(.65f));
            EnemyEffects.Advance(e,2.1f,null);Assert.That(EnemyEffects.MovementMultiplier(e),Is.EqualTo(.8f));
            EnemyEffects.Advance(e,1,null);Assert.That(EnemyEffects.MovementMultiplier(e),Is.EqualTo(1));
        }
        [Test] public void ModernHealthGrowthChangesHealthAndMaximumTogether()
        {
            var b=Map();b.WaveHealthGrowth=.18f;b.Waves.Add(new WaveDefinition{Count=1,Sides=1});
            var game=new GameSession(b,starterDefenses:false);game.StartNextWave();game.Tick(.1f);
            foreach(var enemy in game.Enemies)enemy.Health=0;
            game.Tick(.1f);game.StartNextWave();game.Tick(.1f);
            Assert.That(game.Enemies[0].Health,Is.EqualTo(b.Enemy(EnemyKind.Doodle).MaxHealth*1.18f).Within(.001));
            Assert.That(game.Enemies[0].MaxHealth,Is.EqualTo(game.Enemies[0].Health));
        }
        [Test] public void MainRecipeConnectsAllWellsAndReservesFullObjectiveFootprints()
        {
            var b=NotebookTowerDefensePreset.Create();var map=b.ResolveBattlefield();
            Assert.That(map.IsOpenTerrain,Is.True);Assert.That(map.Roads,Is.Empty);Assert.That(map.BarricadeSites,Is.Empty);
            Assert.That(map.Entrances.Count,Is.EqualTo(16));
            foreach(int side in new[]{1,2,4,8})Assert.That(map.HasEntrance(side),Is.True);
            foreach(var entrance in map.Entrances)Assert.That(map.DistanceToCastle(entrance),Is.LessThan(int.MaxValue));
            foreach(var wave in b.Waves)Assert.That(wave.Sides,Is.EqualTo(15));
            foreach(var w in b.WellPlacements)Assert.That(map.DistanceToCastle(w.Resolve(b.Width,b.Height)),Is.LessThan(int.MaxValue));
            for(int y=20;y<=44;y++)for(int x=36;x<=60;x++)Assert.That(map.IsWalkable(new Cell(x,y)),Is.True,"Clear central defensive area");
            b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Rock,X=16,Y=14,Width=3,Height=2});
            Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
        }
        [Test] public void GroundDistanceAndTerrainFootprintsBoundConstruction()
        {
            var b=Map();b.Battlefield.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Mountain,X=5,Y=3,Width=2,Height=2});
            var game=new GameSession(b,starterDefenses:false);
            Assert.That(game.QuoteBuild(new[]{new Cell(4,2)},StructureKind.Gun,MaterialTier.Pencil).Valid,Is.True);
            Assert.That(game.QuoteBuild(new[]{new Cell(4,1)},StructureKind.Gun,MaterialTier.Pencil).Valid,Is.False);
            Assert.That(game.QuoteBuild(new[]{new Cell(6,4)},StructureKind.RepairPost,MaterialTier.Pencil).Valid,Is.False);
        }
        [Test] public void EnemiesBreachMarkedWallWithoutLeavingRoad()
        {
            var b=Map();b.Waves[0].Count=1;b.Structure(StructureKind.Wall).MaxHealth=12;
            var game=new GameSession(b,starterDefenses:false);
            Assert.That(game.TryBuild(new[]{new Cell(4,8)},StructureKind.Wall,MaterialTier.Pencil,out _),Is.True);
            game.StartNextWave();
            for(int i=0;i<70;i++)
            {
                game.Tick(.1f);
                foreach(var e in game.Enemies)Assert.That(game.Battlefield.IsRoad(new Cell((int)e.Position.X,(int)e.Position.Y)),Is.True);
            }
            Assert.That(game.StructureAt(new Cell(4,8)),Is.Null);
            Assert.That(game.Enemies[0].Position.X,Is.GreaterThan(4.5f));
        }
        [Test] public void CompletedTarUsesStrongestSlowAndIncompletePostsDoNotRepair()
        {
            var b=Map();b.Enemy(EnemyKind.Doodle).Speed=0;b.Waves[0].Count=1;
            var game=new GameSession(b,starterDefenses:false);
            game.TryBuild(new[]{new Cell(6,8)},StructureKind.TarPit,MaterialTier.Pencil,out _);
            game.TryBuild(new[]{new Cell(4,8)},StructureKind.Wall,MaterialTier.Pencil,out _);
            game.TryBuild(new[]{new Cell(3,7)},StructureKind.RepairPost,MaterialTier.Pencil,out _);
            var post=game.StructureAt(new Cell(3,7));post.IsComplete=false;post.BuildRemaining=10;
            var wall=game.StructureAt(new Cell(4,8));wall.Health=10;
            game.StartNextWave();game.Tick(.1f);var enemy=game.Enemies[0];enemy.Position=new Point2(6.5f,8.5f);
            game.Tick(.05f);Assert.That(EnemyEffects.MovementMultiplier(enemy),Is.EqualTo(.65f));
            Assert.That(wall.Health,Is.EqualTo(10));
            EnemyEffects.ApplySlow(enemy,.4f,2);game.Tick(.05f);
            Assert.That(EnemyEffects.MovementMultiplier(enemy),Is.EqualTo(.4f));
        }
        [Test] public void ZeroWaveSidesRequiresTheLegacyWestFallbackEntrance()
        {
            var b=Map();b.Waves[0].Sides=0;
            Assert.DoesNotThrow(()=>BalanceValidation.Validate(b));
            b.Battlefield.Entrances[0]=new BattlefieldCell(10,15);
            for(int y=9;y<=15;y++)b.Battlefield.Roads.Add(new BattlefieldCell(10,y));
            Assert.Throws<ArgumentException>(()=>BalanceValidation.Validate(b));
        }
        [Test] public void AllActiveWellsChooseOnlyForwardObjectivesAndFallBackToCastle()
        {
            var b=NotebookTowerDefensePreset.Create();b.Battlefield=NotebookTowerDefensePreset.CreateFixedRoadBattlefield();b.Waves=new List<WaveDefinition>{new WaveDefinition{Count=60,Sides=7,SpawnInterval=.005f}};
            // This stationary legacy-road fixture checks objective assignment, not admission spacing.
            b.Crowd.Enabled=false;
            b.MaxEnemiesPerCell=100;b.Progression.WellThreatFraction=1;b.StartingInk=10000;b.Enemy(EnemyKind.Doodle).Speed=0;
            var game=new GameSession(b,17,false);for(int i=0;i<game.Wells.Count;i++)Assert.That(game.TryActivateWell(i,out _),Is.True);
            game.StartNextWave();game.Tick(.5f);
            Assert.That(game.Enemies.Count,Is.EqualTo(60));
            foreach(var e in game.Enemies)
            {
                int expected=e.Position.X<1?0:e.Position.X>94?1:2;
                Assert.That(e.ObjectiveIndex,Is.EqualTo(expected),"Opposing routes must never target another branch's well.");
            }
            for(int i=0;i<game.Wells.Count;i++)game.DamageObjective(i,game.Wells[i].MaxHealth);
            foreach(var e in game.Enemies)Assert.That(e.ObjectiveIndex,Is.EqualTo(-1));
        }
        [Test] public void AllWellsActiveThreeEntranceWaveResolvesWithoutCounterflowDeadlock()
        {
            var b=NotebookTowerDefensePreset.Create();b.Battlefield=NotebookTowerDefensePreset.CreateFixedRoadBattlefield();b.StartingInk=10000;b.BaseHealth=100000;b.Progression.WellThreatFraction=1;
            b.Waves=new List<WaveDefinition>{new WaveDefinition{Count=90,Sides=7,SpawnInterval=.05f}};
            b.Enemy(EnemyKind.Doodle).Speed=4;var gun=b.Structure(StructureKind.Gun);gun.Damage=100;gun.ShotInterval=.05f;gun.Range=10;gun.BuildSeconds=0;
            var game=new GameSession(b,17,false);for(int i=0;i<game.Wells.Count;i++)Assert.That(game.TryActivateWell(i,out _),Is.True);
            Assert.That(game.TryBuild(new[]{new Cell(44,32),new Cell(52,32)},StructureKind.Gun,MaterialTier.Pencil,out _),Is.True);
            game.StartNextWave();for(int i=0;i<600&&game.Phase==RunPhase.Assault;i++)game.Tick(.25f);
            Assert.That(game.Phase,Is.EqualTo(RunPhase.Victory));Assert.That(game.Kills,Is.EqualTo(90));
        }
    }
}

