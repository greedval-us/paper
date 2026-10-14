using System;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;

namespace PaperDefense.Tests
{
    /// <summary>Exercises the shipped configuration with a finite, reproducible construction plan.</summary>
    public sealed class TowerDefenseBalanceTests
    {
        static GameBalance MainBalance()
        {
            var preset=AssetDatabase.LoadAssetAtPath<BalancePresetAsset>("Assets/PaperDefense/Configuration/Content/Target.asset");
            Assert.That(preset,Is.Not.Null);
            var balance=preset.CreateBalance();
            // Tests launch deliberately; combat, costs, rewards and enemy rules are unchanged.
            balance.PlanningSeconds=10000;balance.IntermissionSeconds=10000;
            return balance;
        }

        [Test] public void ActualStarterWeaponsNeedSeveralHitsAndAffordableOpeningProtectsCastle()
        {
            var b=MainBalance();var profile=new WorkshopProfile(b.Content,b);var loadout=profile.GetLoadoutBlueprints();
            foreach(var bp in loadout)foreach(var mount in bp.WeaponMounts)
            {
                float damage=TowerCatalog.WeaponDamage(bp,b,mount.WeaponId,MaterialTier.Pencil,mount.SlotIndex);
                Assert.That(Math.Ceiling(b.Enemy(EnemyKind.Doodle).MaxHealth/damage),Is.GreaterThanOrEqualTo(3),bp.Id);
            }
            var game=new GameSession(b,17,false);
            foreach(var cell in new[]{new Cell(44,28),new Cell(52,28),new Cell(44,36),new Cell(52,36),new Cell(48,26),new Cell(48,38),new Cell(42,32),new Cell(54,32)})
                Assert.That(game.TryBuildBlueprint(new[]{cell},loadout[0],MaterialTier.Pencil,InkColor.None,out string reason),Is.True,reason);
            Assert.That(b.StartingInk-game.Ink,Is.EqualTo(320));
            game.Tick(3);CompleteWave(game,b);
            Assert.That(game.Phase,Is.EqualTo(RunPhase.Intermission));
            Assert.That(game.Kills,Is.EqualTo(b.Waves[0].Count));Assert.That(game.BaseHealth,Is.EqualTo(b.BaseHealth));
        }

        [TestCase(17)] [TestCase(41)]
        public void ReinforcedMixedDefenseCanFinishAllEightWavesWithinEarnedInk(int seed)
        {
            var b=MainBalance();var loadout=new WorkshopProfile(b.Content,b).GetLoadoutBlueprints();var game=new GameSession(b,seed,false);
            // A central perimeter covers all four sides; earned ink adds piercing weapons and artillery.
            int[] points={44,28,52,28,44,36,52,36,48,26,48,38,42,32,54,32,42,26,54,26,42,38,54,38,
                40,28,56,28,40,36,56,36,44,24,52,24,44,40,52,40,40,24,56,24,40,40,56,40,
                38,32,58,32,48,22,48,42,38,26,58,26,38,38,58,38};
            int expectedKills=0;
            for(int wave=0;wave<b.Waves.Count;wave++)
            {
                if(game.PendingUpgrade)game.ChooseUpgrade(RunUpgrade.DeepInk);
                // Restore destroyed guns first, then add completed support and further outer defenses.
                for(int cursor=0;cursor<points.Length/2;cursor++)
                {
                    if(cursor==8)
                    {
                        // A small wall perimeter buys firing time; two posts can reach its full length.
                        for(int y=30;y<=34;y++)for(int x=46;x<=50;x++)
                            if(x==46||x==50||y==30||y==34)BuildSupport(new Cell(x,y),StructureKind.Wall);
                        BuildSupport(new Cell(44,32),StructureKind.RepairPost);
                        BuildSupport(new Cell(52,32),StructureKind.RepairPost);
                        // From wave 3, cover ranged enemies approaching from either half of the map.
                        // Repair alone can hold a distant Spitter forever without killing it.
                        if (wave >= 2) foreach (var artilleryCell in new[] { new Cell(48,28), new Cell(48,36) })
                        {
                            if (game.StructureAt(artilleryCell) != null) continue;
                            var artilleryQuote = game.QuoteBlueprint(new[] { artilleryCell }, loadout[3], MaterialTier.Pencil, InkColor.None);
                            if (!artilleryQuote.Valid && artilleryQuote.Cost > game.Ink) continue;
                            Assert.That(artilleryQuote.Valid, Is.True, artilleryQuote.Reason);
                            Assert.That(game.TryBuildBlueprint(new[] { artilleryCell }, loadout[3], MaterialTier.Pencil, InkColor.None, out string artilleryReason), Is.True, artilleryReason);
                            Assert.That(game.Ink, Is.GreaterThanOrEqualTo(0));
                        }
                    }
                    var cell=new Cell(points[cursor*2],points[cursor*2+1]);var bp=loadout[cursor<12?0:cursor%3==0?2:3];
                    if(game.StructureAt(cell)!=null)continue;
                    var quote=game.QuoteBlueprint(new[]{cell},bp,MaterialTier.Pencil,InkColor.None);
                    if(!quote.Valid&&quote.Cost>game.Ink)break;
                    Assert.That(quote.Valid,Is.True,$"Plan cell {cell}: {quote.Reason}");
                    Assert.That(game.TryBuildBlueprint(new[]{cell},bp,MaterialTier.Pencil,InkColor.None,out string reason),Is.True,reason);
                    Assert.That(game.Ink,Is.GreaterThanOrEqualTo(0));
                }
                game.Tick(3);CompleteWave(game,b,loadout);expectedKills+=b.Waves[wave].Count;
                TestContext.WriteLine($"seed {seed}, wave {wave+1}: {game.Kills} kills, {game.BaseHealth:0} castle HP, {game.Ink} ink, {game.Structures.Count} structures");
                Assert.That(game.Phase,Is.Not.EqualTo(RunPhase.Defeat),$"seed {seed}, wave {wave+1}");
                Assert.That(game.Kills,Is.EqualTo(expectedKills));
            }
            Assert.That(game.Phase,Is.EqualTo(RunPhase.Victory));
            Assert.That(game.BaseHealth,Is.GreaterThan(0));
            TestContext.WriteLine($"seed {seed}: {game.Kills} kills, {game.BaseHealth:0} castle HP, {game.Ink} ink, {game.Elapsed:0.0}s, {game.Statistics.BuildInvestment} construction ink");
            void BuildSupport(Cell cell,StructureKind kind)
            {
                if(game.StructureAt(cell)!=null)return;
                var quote=game.QuoteBuild(new[]{cell},kind,MaterialTier.Pencil);
                if(!quote.Valid&&quote.Cost>game.Ink)return;
                Assert.That(quote.Valid,Is.True,quote.Reason);
                Assert.That(game.TryBuild(new[]{cell},kind,MaterialTier.Pencil,out string reason),Is.True,reason);
            }
        }

        static void CompleteWave(GameSession game,GameBalance balance,System.Collections.Generic.IReadOnlyList<TowerBlueprint> reinforcementLoadout=null)
        {
            game.StartNextWave();int steps=0, reinforcements=0;
            while(game.Phase==RunPhase.Assault&&steps++<18000)
            {
                game.Tick(balance.SimulationStep);
                // Broad fronts require player responses during the wave as well as a fixed
                // opening. Spend only earned ink, at most four ordinary towers per wave.
                bool needsResponse = (game.RemainingToSpawn == 0 && game.Enemies.Count <= 3) || game.BaseHealth < balance.BaseHealth;
                if (reinforcementLoadout != null && game.WaveIndex >= 3 && needsResponse && steps % 900 == 0 && reinforcements < 4 &&
                    ReinforceThreat(game, reinforcementLoadout)) reinforcements++;
            }
            if (game.Phase == RunPhase.Assault)
            {
                TestContext.WriteLine($"Unresolved wave {game.WaveIndex}, backlog {game.RemainingToSpawn}, alive {game.Enemies.Count}");
                foreach (var enemy in game.Enemies)
                {
                    var destination = (Point2)typeof(EnemyState).GetField("Destination", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(enemy);
                    TestContext.WriteLine($"Enemy {enemy.Id} {enemy.Kind} HP {enemy.Health} at {enemy.Position.X:R},{enemy.Position.Y:R}; destination {destination.X:R},{destination.Y:R}");
                }
            }
            Assert.That(game.Phase,Is.Not.EqualTo(RunPhase.Assault),"A wave must resolve; no trapped enemies or lost spawn backlog.");
        }

        static bool ReinforceThreat(GameSession game,System.Collections.Generic.IReadOnlyList<TowerBlueprint> loadout)
        {
            EnemyState threat=null;float nearest=float.MaxValue;
            foreach(var enemy in game.Enemies)
            {
                float dx=enemy.Position.X-game.BaseCell.X-.5f,dy=enemy.Position.Y-game.BaseCell.Y-.5f;
                float score=dx*dx+dy*dy;
                if(enemy.Kind==EnemyKind.Spitter)score*=.25f;
                if(score<nearest){nearest=score;threat=enemy;}
            }
            if(threat==null)return false;
            var blueprint=loadout[threat.Kind==EnemyKind.Spitter?3:2];
            var balance=game.Balance;
            if(TowerCatalog.Cost(blueprint,balance,MaterialTier.Pencil)>game.Ink)blueprint=loadout[2];
            if(TowerCatalog.Cost(blueprint,balance,MaterialTier.Pencil)>game.Ink)blueprint=loadout[0];
            // Redeploy an out-of-range tower for the last ranged holdout, using the normal
            // damaged-health refund. Never grant ink or remove an enemy to end the wave.
            for(int salvage=0;TowerCatalog.Cost(blueprint,balance,MaterialTier.Pencil)>game.Ink&&salvage<3;salvage++)
            {
                if(game.Enemies.Count!=1||threat.Kind!=EnemyKind.Spitter)return false;
                StructureState candidate=null;float farthest=0;
                foreach(var structure in game.Structures)
                {
                    if(ConstructionToolRules.IsTool(structure.Kind))continue;
                    float dx=structure.Cell.X+.5f-threat.Position.X,dy=structure.Cell.Y+.5f-threat.Position.Y;
                    float distance=dx*dx+dy*dy;
                    if(distance>farthest){candidate=structure;farthest=distance;}
                }
                if(candidate==null)return false;
                int before=game.Ink;
                Assert.That(game.TryErase(candidate.Cell,out int refund),Is.True);
                Assert.That(game.Ink,Is.EqualTo(before+refund));
                TestContext.WriteLine($"Redeploy {candidate.Cell}: {refund} refunded ink.");
            }
            if(TowerCatalog.Cost(blueprint,balance,MaterialTier.Pencil)>game.Ink)return false;
            var mount=blueprint.WeaponMounts[0];
            float range=TowerCatalog.WeaponRange(blueprint,balance,mount.WeaponId,mount.SlotIndex);
            float towardsX=game.BaseCell.X+.5f-threat.Position.X,towardsY=game.BaseCell.Y+.5f-threat.Position.Y;
            float length=(float)Math.Sqrt(towardsX*towardsX+towardsY*towardsY);
            float fraction=length>0?Math.Min(.5f,range*.65f/length):0;
            int x=(int)(threat.Position.X+towardsX*fraction);
            int y=(int)(threat.Position.Y+towardsY*fraction);
            for(int radius=0;radius<=2;radius++)for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
            {
                if(Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
                var cells=new[]{new Cell(x+dx,y+dy)};
                var quote=game.QuoteBlueprint(cells,blueprint,MaterialTier.Pencil,InkColor.None);
                if(!quote.Valid)continue;
                int before=game.Ink;
                Assert.That(game.TryBuildBlueprint(cells,blueprint,MaterialTier.Pencil,InkColor.None,out string reason),Is.True,reason);
                Assert.That(game.Ink,Is.EqualTo(before-quote.Cost).And.GreaterThanOrEqualTo(0));
                TestContext.WriteLine($"Wave {game.WaveIndex}: reinforce {cells[0]} for {quote.Cost} earned ink.");
                return true;
            }
            return false;
        }
    }
}
