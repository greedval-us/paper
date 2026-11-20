using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Validation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class CampaignPlayabilityTests
    {
        [Test]
        public void EachPageCanBuyACompleteOpeningDefenseBeforeTheFirstWave()
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out var catalog);
            var basket = new[] { "album.guard", "album.guard", "album.piercer", "album.mortar",
                "album.wall", "album.wall", "album.wall", "album.wall", "album.slow" };
            foreach (var mission in CampaignCatalog.Default.Missions)
            {
                var balance = MissionBalanceFactory.Create(baseline, mission);
                var session = new GameSession(balance, mission.Seed, false);
                var starters = catalog.Starters.ToDictionary(entry => entry.Id,
                    entry => TowerCatalog.ResolveEntry(entry, balance).Value);
                int priorOpening = MissionBalanceFactory.CreatePreviousOpeningSnapshot(balance, mission).StartingInk;
                int extraGuards = mission.NotebookIndex + 2;
                Assert.That(session.Ink - priorOpening, Is.GreaterThanOrEqualTo(extraGuards * starters["album.guard"].Cost), mission.Id);
                int expected = balance.StartingInk;
                foreach (var id in basket)
                {
                    var entry = starters[id];
                    Assert.That(session.Ink, Is.GreaterThanOrEqualTo(entry.Cost), mission.Id + " opening purchase " + id);
                    var cell = Enumerable.Range(0, balance.Width * balance.Height)
                        .Select(i => new Cell(i % balance.Width, i / balance.Width))
                        .First(c => session.QuoteEntry(new[] { c }, entry, MaterialTier.Pencil, InkColor.None).Valid);
                    Assert.That(session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                    expected -= entry.Cost;
                }
                Assert.That(session.WaveIndex, Is.Zero, mission.Id);
                Assert.That(session.Structures.Count, Is.EqualTo(basket.Length), mission.Id);
                Assert.That(session.Ink, Is.EqualTo(expected), mission.Id);
            }
        }

        [Test]
        public void EveryCampaignIntermissionGrantPaysForEitherAStarterBatteryOrAFortifiedLine()
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out var catalog);
            foreach (var mission in CampaignCatalog.Default.Missions)
            {
                var balance = MissionBalanceFactory.Create(baseline, mission);
                var prices = catalog.Starters.ToDictionary(entry => entry.Id,
                    entry => TowerCatalog.ResolveEntry(entry, balance).Value.Cost);
                int battery = prices["album.guard"] + prices["album.piercer"] + prices["album.mortar"];
                int fortifiedLine = 2 * prices["album.guard"] + prices["album.piercer"] +
                    3 * prices["album.wall"] + prices["album.slow"];
                for (int wave = 0; wave < balance.Waves.Count - 1; wave++)
                {
                    int grant = balance.Waves[wave].InkGrant;
                    Assert.That(grant, Is.GreaterThanOrEqualTo(battery), $"{mission.Id}, after wave {wave + 1}: three different guns.");
                    Assert.That(grant, Is.GreaterThanOrEqualTo(fortifiedLine), $"{mission.Id}, after wave {wave + 1}: guns, barricades and slowing support.");
                }
            }
        }

        [Test]
        public void PreparingTwoFrontsPaysMoreThanHoldingOneFrontAtTheSameStage()
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out _);
            var missions = CampaignCatalog.Default.Missions;
            var encirclement = MissionBalanceFactory.Create(baseline, missions[0]).Waves;
            var northPass = MissionBalanceFactory.Create(baseline, missions[1]).Waves;
            // The second clear funds the third assault. The castle faces north and south on
            // the first map, while both mountain passes on the second map lead south from one edge.
            Assert.That(encirclement[2].Sides, Is.EqualTo(4 | 8));
            Assert.That(northPass[2].Sides, Is.EqualTo(4));
            Assert.That(encirclement[1].InkGrant, Is.GreaterThan(northPass[1].InkGrant));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void EveryAuthoredMissionIsWinnableWithPaidInitiallyOwnedAssembliesAndLosesWithoutDefense(int index)
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out var catalog);
            string original = JsonUtility.ToJson(baseline);
            var mission = CampaignCatalog.Default.Missions[index];
            var defended = CampaignPlayabilityScenario.Run(baseline, catalog, mission);
            Assert.That(JsonUtility.ToJson(baseline), Is.EqualTo(original), "Validation must not rewrite production rules.");
            Assert.That(defended.Passed, Is.True, JsonUtility.ToJson(defended, true));
            Assert.That(defended.CompletedWaves, Is.EqualTo(mission.WaveCount));
            Assert.That(defended.Waves.Count, Is.EqualTo(mission.WaveCount));
            foreach (var wave in defended.Waves)
            {
                Assert.That(wave.SpawnedByKind.Sum(), Is.EqualTo(wave.Kills));
                Assert.That(wave.SpawnedByDrawingTier.Sum(), Is.EqualTo(wave.Kills));
            }
            Assert.That(defended.Waves.All(w => w.PeakAlive >= 200), Is.True,
                "Every assault must admit a real horde even while the paid defense is firing.");
            Assert.That(defended.Builds.All(build => build.FacingDegrees >= 0 && build.FacingDegrees < 360), Is.True,
                "Every paid placement records its chosen heading.");
            Assert.That(defended.Builds.All(b => catalog.Starters.Any(e => e.Id == b.EntryId)), Is.True);
            Assert.That(defended.Builds.All(b => b.Cost > 0 && b.InkAfter >= 0), Is.True);
            Assert.That(defended.FieldModernization, Is.True);
            Assert.That(defended.GlobalUpgradeChoices, Is.Zero, "Paid progression must not conceal free DeepInk bonuses.");
            Assert.That(defended.PaidInk, Is.EqualTo(defended.Builds.Sum(b => b.Cost) +
                defended.Modernizations.Where(u => !u.IsColor).Sum(u => u.Cost)));
            CollectionAssert.AreEqual(Enumerable.Range(1, mission.WaveCount - 1), defended.Flasks.Select(f => f.Wave));
            CollectionAssert.AreEquivalent(new[] { InkColor.Red, InkColor.Blue, InkColor.Green }, defended.Flasks.Select(f => f.Color).Distinct());
            Assert.That(defended.Flasks.All(f => f.Amount == 12 && f.BalanceAfter >= f.Amount), Is.True);
            foreach (var color in defended.Flasks.GroupBy(f => f.Color))
            {
                int spent = defended.Modernizations.Where(u => u.IsColor && u.Color == color.Key.ToString()).Sum(u => u.Cost);
                Assert.That(spent, Is.LessThanOrEqualTo(color.Sum(f => f.Amount)));
            }
            Assert.That(defended.Modernizations.Where(u => !u.IsColor).Any(), Is.True);
            Assert.That(defended.Modernizations.All(u => u.Cost > 0 && u.WorkSeconds > 0 && u.InkAfter >= 0), Is.True);
            Assert.That(defended.CompletedModernizations, Is.GreaterThan(0));
            Assert.That(defended.Modernizations.Where(u => u.CompletedAt >= 0).All(u => u.CompletedAt - u.Seconds >= u.WorkSeconds - .04f), Is.True,
                "Paid work must actually consume its configured simulation time.");
            Assert.That(defended.MaximumRank, Is.EqualTo(index == 0 ? 1 : index < 3 ? 2 : 3));
            Assert.That(defended.Builds.Select(b => b.EntryId), Does.Contain("album.guard"));
            Assert.That(defended.Builds.Select(b => b.EntryId), Does.Contain("album.piercer"));
            Assert.That(defended.Builds.Select(b => b.EntryId), Does.Contain("album.mortar"));
            Assert.That(defended.Builds.Select(b => b.EntryId), Does.Contain("album.slow"));
            Assert.That(defended.Builds.Select(b => b.EntryId), Does.Contain("album.repair"));
            var unguarded = CampaignPlayabilityScenario.Run(baseline, catalog, mission, false);
            Assert.That(unguarded.Passed, Is.True, JsonUtility.ToJson(unguarded, true));
            Assert.That(unguarded.Outcome, Is.EqualTo("Defeat"));
            Assert.That(unguarded.Builds, Is.Empty);
        }

        [Test]
        public void FacingTheAnnouncedFrontsWinsWhereIdenticallyPricedWestFacingBatteriesFall()
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out var catalog);
            var mission = CampaignCatalog.Default.Missions[0];
            var balance = MissionBalanceFactory.Create(baseline, mission);
            GameSession Play(bool followForecast, int[] spend)
            {
                var session = new GameSession(balance, mission.Seed, false);
                var home = session.BaseCell;
                for (int wave = 1; wave <= mission.WaveCount; wave++)
                {
                    int before = session.Ink;
                    PaperDefense.Presentation.AlbumReleaseProbe.VisitSiegePlacements(wave, (id, x, y, heading) => {
                        var entry = TowerCatalog.ResolveEntry(catalog.Starters.Single(e => e.Id == id), balance).Value;
                        var cell = new Cell(home.X + x, home.Y + y);
                        Assert.That(session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out var reason,
                            id, followForecast ? heading : 180), Is.True, reason);
                    });
                    spend[wave - 1] = before - session.Ink;
                    session.StartNextWave();
                    for (int i = 0; i < CampaignPlayabilityScenario.MaximumTicksPerWave && session.Phase == RunPhase.Assault; i++)
                        session.Tick(CampaignPlayabilityScenario.StepSeconds);
                    if (session.Phase != RunPhase.Intermission) break;
                }
                return session;
            }
            var adaptiveSpend = new int[mission.WaveCount];
            var westernSpend = new int[mission.WaveCount];
            var adaptive = Play(true, adaptiveSpend);
            var western = Play(false, westernSpend);
            Assert.That(adaptive.Phase, Is.EqualTo(RunPhase.Victory));
            Assert.That(western.Phase, Is.EqualTo(RunPhase.Defeat));
            Assert.That(western.CompletedWaves, Is.GreaterThanOrEqualTo(1));
            CollectionAssert.AreEqual(adaptiveSpend.Take(western.WaveIndex), westernSpend.Take(western.WaveIndex));
        }

        [TestCase(0)] [TestCase(4)] [TestCase(8)]
        public void PaidStrategyComparisonsShareRulesAndKeepDistinctPurchases(int index)
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out var catalog);
            var mission = CampaignCatalog.Default.Missions[index];
            var mixed = CampaignPlayabilityScenario.Run(baseline, catalog, mission);
            var expansion = CampaignPlayabilityScenario.Run(baseline, catalog, mission, policy: CampaignDefensePolicy.Expansion);
            var concentration = CampaignPlayabilityScenario.Run(baseline, catalog, mission, policy: CampaignDefensePolicy.Concentrated);
            Assert.That(mixed.Passed, Is.True, JsonUtility.ToJson(mixed, true));
            foreach (var row in new[] { expansion, concentration })
            {
                Assert.That(row.StartingInk, Is.EqualTo(mixed.StartingInk));
                Assert.That(row.StartingBaseHealth, Is.EqualTo(mixed.StartingBaseHealth));
                Assert.That(row.Seed, Is.EqualTo(mixed.Seed));
                Assert.That(row.MaximumRank, Is.EqualTo(mixed.MaximumRank));
                Assert.That(row.GlobalUpgradeChoices, Is.Zero);
            }
            Assert.That(expansion.Modernizations, Is.Empty);
            Assert.That(concentration.Modernizations.Any(), Is.True);
        }

        [Test]
        public void DevelopmentCanSaveInkWhileAnotherMapRequiresMorePositions()
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out var catalog);
            // The two northern passes can share a battery near the southern castle.
            var crossfire = CampaignCatalog.Default.Missions[1];
            var wide = CampaignPlayabilityScenario.Run(baseline, catalog, crossfire, policy: CampaignDefensePolicy.Expansion);
            var tall = CampaignPlayabilityScenario.Run(baseline, catalog, crossfire, policy: CampaignDefensePolicy.Concentrated);
            Assert.That(wide.Passed && tall.Passed, Is.True);
            Assert.That(tall.PaidInk, Is.LessThan(wide.PaidInk));
            Assert.That(tall.Builds.Count, Is.LessThan(wide.Builds.Count));
            var finale = CampaignCatalog.Default.Missions[2];
            wide = CampaignPlayabilityScenario.Run(baseline, catalog, finale, policy: CampaignDefensePolicy.Expansion);
            tall = CampaignPlayabilityScenario.Run(baseline, catalog, finale, policy: CampaignDefensePolicy.Concentrated);
            Assert.That(wide.CompletedWaves, Is.GreaterThan(tall.CompletedWaves));
        }

        [Test]
        public void AuthoredMissionReplaysIdenticalPaidCommandsWaveSnapshotsAndOutcome()
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out var catalog);
            var mission = CampaignCatalog.Default.Missions[4];
            var first = CampaignPlayabilityScenario.Run(baseline, catalog, mission);
            var repeated = CampaignPlayabilityScenario.Run(baseline, catalog, mission);
            Assert.That(first.Passed, Is.True, JsonUtility.ToJson(first, true));
            Assert.That(repeated.Passed, Is.True, JsonUtility.ToJson(repeated, true));
            // Wall duration measures the machine, while all simulation state and paid commands must repeat.
            first.WallMilliseconds = repeated.WallMilliseconds = 0;
            Assert.That(JsonUtility.ToJson(repeated), Is.EqualTo(JsonUtility.ToJson(first)));
        }
    }
}
