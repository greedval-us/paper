using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class CompositeConstructionTests
    {
        static GameBalance Rules(int ink = 640)
        {
            var balance = GameBalance.CreateDefault();
            balance.StartingInk = ink; balance.PassiveInkPerSecond = 0; balance.PlanningSeconds = 3600;
            balance.EnableInkWells = false; balance.Construction.AcquisitionSeconds = 0;
            return balance;
        }

        static ResolvedEntry Starter(int index, GameBalance balance, MaterialTier material = MaterialTier.Pencil)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            return AssemblyResolverFixture.Resolve(catalog.Starters[index].Snapshot.Recipe, catalog, balance, material);
        }

        static StructureState Build(GameSession session, ResolvedEntry entry, Cell cell)
        {
            Assert.That(session.TryBuildEntry(new[] { cell }, entry, entry.Material, InkColor.None, out string reason), Is.True, reason);
            return session.StructureAt(cell);
        }

        static StructureState[] Grid(GameSession session, GameBalance balance)
        {
            var grid = new StructureState[balance.Width * balance.Height];
            foreach (var structure in session.Structures) grid[structure.Cell.Y * balance.Width + structure.Cell.X] = structure;
            return grid;
        }

        [Test] public void ModernGeneratorDoesNotInventALegacyWeaponForANullIdentifier()
            => Assert.That(LegacyContentIds.Weapon((string)null), Is.EqualTo((StructureKind)(-1)));

        [Test] public void EightStarters_Cost426Ink()
        {
            var balance = Rules(); var session = new GameSession(balance, starterDefenses: false);
            var costs = new[] { 40, 64, 60, 99, 16, 22, 65, 60 };
            for (int index = 0; index < costs.Length; index++)
            {
                var entry = Starter(index, balance); var cell = new Cell(4 + 2 * index, 4);
                var quote = session.QuoteEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None);
                Assert.That(quote.Valid, Is.True, quote.Reason); Assert.That(quote.Cost, Is.EqualTo(costs[index]));
                var structure = Build(session, entry, cell);
                Assert.That(structure.ResolvedEntry.Cost, Is.EqualTo(costs[index]));
                Assert.That(structure.Health, Is.EqualTo(entry.Health).Within(.0001f));
            }
            Assert.That(session.Ink, Is.EqualTo(214));
            Assert.That(session.Structures.Count, Is.EqualTo(8));
            Assert.That(session.Statistics.BuildInvestment, Is.EqualTo(426));
            Assert.That(session.Statistics.TotalStructuresBuilt, Is.EqualTo(8));
        }

        [TestCase("occupied")] [TestCase("border")] [TestCase("outside")] [TestCase("castle")]
        public void AnInvalidCellRejectsTheWholeBatchWithoutChangingInkEntitiesOrStatistics(string invalid)
        {
            var balance = Rules(); var entry = Starter(0, balance); var session = new GameSession(balance, starterDefenses: false);
            var occupied = new Cell(4, 4); var existing = Build(session, entry, occupied); var free = new Cell(6, 4);
            Cell bad = invalid == "occupied" ? occupied : invalid == "border" ? new Cell(0, 4) :
                invalid == "outside" ? new Cell(-1, 4) : session.BaseCell;
            var cells = new[] { free, bad }; int ink = session.Ink, events = session.EventBuffer.Count;
            int topology = session.TopologyVersion, visual = session.StructureVisualVersion;
            var quote = session.QuoteEntry(cells, entry, MaterialTier.Pencil, InkColor.None);
            Assert.That(quote.Valid, Is.False); Assert.That(quote.Reason, Is.Not.Empty);
            Assert.That(session.TryBuildEntry(cells, entry, MaterialTier.Pencil, InkColor.None, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo(quote.Reason)); Assert.That(session.Ink, Is.EqualTo(ink));
            Assert.That(session.Structures, Is.EqualTo(new[] { existing })); Assert.That(session.StructureAt(free), Is.Null);
            Assert.That(session.Statistics.BuildInvestment, Is.EqualTo(40)); Assert.That(session.Statistics.TotalStructuresBuilt, Is.EqualTo(1));
            Assert.That(session.EventBuffer.Count, Is.EqualTo(events));
            Assert.That(session.TopologyVersion, Is.EqualTo(topology)); Assert.That(session.StructureVisualVersion, Is.EqualTo(visual));
        }

        [Test] public void InsufficientInkRejectsTheWholeBatchRatherThanBuildingTheAffordablePrefix()
        {
            var balance = Rules(79); var session = new GameSession(balance, starterDefenses: false); var entry = Starter(0, balance);
            var cells = new[] { new Cell(4, 4), new Cell(6, 4) };
            var quote = session.QuoteEntry(cells, entry, MaterialTier.Pencil, InkColor.None);
            Assert.That(quote.Valid, Is.False); Assert.That(quote.Cost, Is.EqualTo(80));
            Assert.That(session.TryBuildEntry(cells, entry, MaterialTier.Pencil, InkColor.None, out _), Is.False);
            Assert.That(session.Ink, Is.EqualTo(79)); Assert.That(session.Structures, Is.Empty);
            Assert.That(session.Statistics.TotalStructuresBuilt, Is.Zero); Assert.That(session.Statistics.BuildInvestment, Is.Zero);
        }

        [Test] public void AnEnemyInTheSecondCellRejectsTheWholeBatch()
        {
            var balance = Rules(); var entry = Starter(0, balance);
            var session = GameSession.CreatePrepared(balance, 7, new[] { new PreparedEnemy(new Point2(6.5f, 4.5f), 1000) });
            var cells = new[] { new Cell(4, 4), new Cell(6, 4) };
            Assert.That(session.QuoteEntry(cells, entry, MaterialTier.Pencil, InkColor.None).Valid, Is.False);
            Assert.That(session.TryBuildEntry(cells, entry, MaterialTier.Pencil, InkColor.None, out _), Is.False);
            Assert.That(session.Ink, Is.EqualTo(640)); Assert.That(session.Structures, Is.Empty);
            Assert.That(session.Statistics.BuildInvestment, Is.Zero);
        }

        [Test] public void DuplicateStrokeCellsAreChargedAndBuiltOnlyOnce()
        {
            var balance = Rules(); var entry = Starter(0, balance); var session = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(4, 4); var cells = new[] { cell, cell, cell };
            var quote = session.QuoteEntry(cells, entry, MaterialTier.Pencil, InkColor.None);
            Assert.That(quote.Valid, Is.True, quote.Reason); Assert.That(quote.Cells, Is.EqualTo(new[] { cell }));
            Assert.That(quote.Cost, Is.EqualTo(40)); Assert.That(session.Ink, Is.EqualTo(640));
            Assert.That(session.TryBuildEntry(cells, entry, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            Assert.That(session.Ink, Is.EqualTo(600)); Assert.That(session.Structures.Count, Is.EqualTo(1));
            Assert.That(session.Statistics.TotalStructuresBuilt, Is.EqualTo(1)); Assert.That(session.Statistics.BuildInvestment, Is.EqualTo(40));
        }

        [TestCase(MaterialTier.Pencil, 40, 55f, 1.4f)]
        [TestCase(MaterialTier.Ink, 72, 115.5f, 1.82f)]
        [TestCase(MaterialTier.Marker, 128, 231f, 2.45f)]
        public void ConstructionUsesTheResolvedMaterialCostHealthAndBuildTimeExactlyOnce(MaterialTier material, int cost, float health, float seconds)
        {
            var balance = Rules(); var entry = Starter(0, balance, material); var session = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(4, 4); var quote = session.QuoteEntry(new[] { cell }, entry, material, InkColor.None);
            Assert.That(quote.Valid, Is.True, quote.Reason); Assert.That(quote.Cost, Is.EqualTo(cost));
            var structure = Build(session, entry, cell);
            Assert.That(session.Ink, Is.EqualTo(640 - cost)); Assert.That(structure.PaidInk, Is.EqualTo(cost));
            Assert.That(structure.Tier, Is.EqualTo(material)); Assert.That(structure.Health, Is.EqualTo(health).Within(.0001f));
            Assert.That(structure.MaxHealth, Is.EqualTo(health).Within(.0001f));
            Assert.That(structure.BuildRemaining, Is.EqualTo(seconds).Within(.0001f)); Assert.That(structure.IsComplete, Is.False);
            session.Tick(seconds + .05f);
            Assert.That(structure.IsComplete, Is.True); Assert.That(structure.BuildRemaining, Is.Zero);
            Assert.That(structure.Health, Is.EqualTo(health).Within(.0001f));
            Assert.That(session.EventBuffer.Count(e => e.Kind == SimulationEventKind.StructureCompleted && e.EntityId == structure.Id), Is.EqualTo(1));
        }

        [Test] public void MismatchedMaterialRejectsAnAlreadyResolvedEntryAtomically()
        {
            var balance = Rules(); var entry = Starter(0, balance); var session = new GameSession(balance, starterDefenses: false);
            var cells = new[] { new Cell(4, 4) };
            Assert.That(session.QuoteEntry(cells, entry, MaterialTier.Ink, InkColor.None).Valid, Is.False);
            Assert.That(session.TryBuildEntry(cells, entry, MaterialTier.Ink, InkColor.None, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty); Assert.That(session.Ink, Is.EqualTo(640)); Assert.That(session.Structures, Is.Empty);
            Assert.That(session.Statistics.BuildInvestment, Is.Zero);
        }

        [Test] public void DeepInkBoostsExistingAndNewInstancesOnceWithoutChangingResolvedPriceOrConstructionTime()
        {
            var balance = Rules(); balance.Progression.UpgradeEveryWaves = 1;
            var session = GameSession.CreatePrepared(balance, 7, Array.Empty<PreparedEnemy>());
            var previous = Build(session, Starter(0, balance), new Cell(4, 4));
            session.Tick(.01f); Assert.That(session.PendingUpgrade, Is.True);
            session.ChooseUpgrade(RunUpgrade.DeepInk);
            Assert.That(previous.MaxHealth, Is.EqualTo(68.75f).Within(.0001f));
            var entry = Starter(0, balance, MaterialTier.Ink); int ink = session.Ink;
            var next = Build(session, entry, new Cell(6, 4));
            Assert.That(session.Ink, Is.EqualTo(ink - 72)); Assert.That(next.PaidInk, Is.EqualTo(72));
            Assert.That(next.MaxHealth, Is.EqualTo(144.375f).Within(.0001f));
            Assert.That(next.Health, Is.EqualTo(144.375f).Within(.0001f));
            Assert.That(next.BuildRemaining, Is.EqualTo(1.82f).Within(.0001f));
            Assert.That(entry.Health, Is.EqualTo(115.5f).Within(.0001f));
        }

        [Test] public void UpgradingAModernGunChargesOnlyItsResolvedDifferenceAndPublishesANewOwnedSnapshot()
        {
            var balance = Rules(); var entry = Starter(0, balance); var session = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(4, 4); var tower = Build(session, entry, cell); session.Tick(entry.BuildSeconds + .05f);
            tower.Health = tower.MaxHealth / 2; var previous = new StructureView(tower); int ink = session.Ink;
            Assert.That(session.TryUpgrade(cell, out string reason), Is.True, reason);
            Assert.That(session.Ink, Is.EqualTo(ink - 32)); Assert.That(tower.PaidInk, Is.EqualTo(72));
            Assert.That(session.Statistics.UpgradeInvestment, Is.EqualTo(32));
            Assert.That(tower.Tier, Is.EqualTo(MaterialTier.Ink)); Assert.That(tower.MaxHealth, Is.EqualTo(115.5f).Within(.0001f));
            Assert.That(tower.Health, Is.EqualTo(57.75f).Within(.0001f)); Assert.That(tower.BuildRemaining, Is.EqualTo(1.82f).Within(.0001f));
            Assert.That(tower.IsComplete, Is.False); Assert.That(tower.ResolvedEntry.Material, Is.EqualTo(MaterialTier.Ink));
            Assert.That(tower.ResolvedEntry.Cost, Is.EqualTo(72)); Assert.That(tower.ResolvedEntry.Weapons[0].Damage, Is.EqualTo(15));
            Assert.That(tower.ResolvedEntry, Is.Not.SameAs(previous.ResolvedEntry));
            Assert.That(previous.ResolvedEntry.Material, Is.EqualTo(MaterialTier.Pencil));
            Assert.That(previous.ResolvedEntry.Cost, Is.EqualTo(40)); Assert.That(previous.ResolvedEntry.Weapons[0].Damage, Is.EqualTo(12));
            Assert.That(previous.MaxHealth, Is.EqualTo(55)); Assert.That(previous.Health, Is.EqualTo(27.5f));
        }

        [Test] public void UpgradingARepairSupportUsesThePagePriceAndPreservesItsMaterialIndependentRate()
        {
            var balance = Rules(); var entry = Starter(6, balance); var session = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(4, 4); var repair = Build(session, entry, cell); session.Tick(entry.BuildSeconds + .05f);
            repair.Health = repair.MaxHealth / 2; var previous = new StructureView(repair); int ink = session.Ink;
            Assert.That(session.TryUpgrade(cell, out string reason), Is.True, reason);
            Assert.That(session.Ink, Is.EqualTo(ink - 52)); Assert.That(repair.PaidInk, Is.EqualTo(117));
            Assert.That(session.Statistics.UpgradeInvestment, Is.EqualTo(52));
            Assert.That(repair.MaxHealth, Is.EqualTo(147).Within(.0001f)); Assert.That(repair.Health, Is.EqualTo(73.5f).Within(.0001f));
            Assert.That(repair.BuildRemaining, Is.EqualTo(2.6f).Within(.0001f)); Assert.That(repair.IsComplete, Is.False);
            Assert.That(repair.ResolvedEntry.Material, Is.EqualTo(MaterialTier.Ink)); Assert.That(repair.ResolvedEntry.Cost, Is.EqualTo(117));
            Assert.That(repair.ResolvedEntry.Support.RepairPerSecond, Is.EqualTo(6));
            Assert.That(repair.ResolvedEntry.Support.Radius, Is.EqualTo(5));
            Assert.That(previous.ResolvedEntry.Material, Is.EqualTo(MaterialTier.Pencil));
            Assert.That(previous.ResolvedEntry.Cost, Is.EqualTo(65)); Assert.That(previous.ResolvedEntry.Support.RepairPerSecond, Is.EqualTo(6));
        }

        [TestCase(false)] [TestCase(true)]
        public void UpgradingMaterialPreservesTheMountMagazineAndNormalizedIntervalOrReloadProgress(bool reloading)
        {
            var balance = Rules(); var entry = Starter(0, balance); var session = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(4, 4); var tower = Build(session, entry, cell); session.Tick(entry.BuildSeconds + .05f);
            var mount = tower.GetMountState(0);
            mount.Cycle = WeaponCycleRules.Step(mount.Cycle, entry.Weapons[0], reloading ? 2.2f : .2f, true, 1).State;
            var previous = new StructureView(tower); var cycle = mount.Cycle;
            Assert.That(cycle.RoundsRemaining, Is.EqualTo(reloading ? 0 : 5)); Assert.That(cycle.IsReloading, Is.EqualTo(reloading));
            Assert.That(cycle.RemainingWork, Is.GreaterThan(0));
            Assert.That(session.TryUpgrade(cell, out string reason), Is.True, reason);
            Assert.That(tower.ResolvedEntry.Material, Is.EqualTo(MaterialTier.Ink));
            Assert.That(mount.Cycle.RoundsRemaining, Is.EqualTo(cycle.RoundsRemaining));
            Assert.That(mount.Cycle.IsReloading, Is.EqualTo(cycle.IsReloading));
            Assert.That(mount.Cycle.RemainingWork, Is.EqualTo(cycle.RemainingWork).Within(.000001));
            Assert.That(previous.GetMountState(0).RoundsRemaining, Is.EqualTo(cycle.RoundsRemaining));
            Assert.That(previous.GetMountState(0).IsReloading, Is.EqualTo(cycle.IsReloading));
            Assert.That(previous.GetMountState(0).CycleRemainingWork, Is.EqualTo(cycle.RemainingWork).Within(.000001));
        }

        [Test] public void GeneratorMaterialUpgradeIsRejectedBeforeSpendingInkOrChangingItsState()
        {
            var balance = Rules(); var entry = Starter(7, balance); var session = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(4, 4); var generator = Build(session, entry, cell); session.Tick(entry.BuildSeconds + .05f);
            generator.Health = generator.MaxHealth / 2;
            int ink = session.Ink, visual = session.StructureVisualVersion, topology = session.TopologyVersion, events = session.EventBuffer.Count;
            Assert.That(session.TryUpgrade(cell, out string reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.That(session.Ink, Is.EqualTo(ink)); Assert.That(session.StructureAt(cell), Is.SameAs(generator));
            Assert.That(generator.ResolvedEntry, Is.SameAs(entry)); Assert.That(generator.Tier, Is.EqualTo(MaterialTier.Pencil));
            Assert.That(generator.PaidInk, Is.EqualTo(60)); Assert.That(generator.Health, Is.EqualTo(40)); Assert.That(generator.MaxHealth, Is.EqualTo(80));
            Assert.That(generator.BuildRemaining, Is.Zero); Assert.That(generator.IsComplete, Is.True);
            Assert.That(session.Statistics.UpgradeInvestment, Is.Zero); Assert.That(session.EventBuffer.Count, Is.EqualTo(events));
            Assert.That(session.StructureVisualVersion, Is.EqualTo(visual)); Assert.That(session.TopologyVersion, Is.EqualTo(topology));
        }

        [Test] public void InsufficientColorStockRejectsTheWholeBatchWithoutSpendingOrdinaryInk()
        {
            var balance = Rules(); var entry = Starter(0, balance);
            var session = GameSession.CreatePrepared(balance, 7, Array.Empty<PreparedEnemy>(), redInk: 9);
            var cells = new[] { new Cell(4, 4), new Cell(6, 4) };
            Assert.That(session.QuoteEntry(cells, entry, MaterialTier.Pencil, InkColor.Red).Valid, Is.False);
            Assert.That(session.TryBuildEntry(cells, entry, MaterialTier.Pencil, InkColor.Red, out _), Is.False);
            Assert.That(session.Ink, Is.EqualTo(640)); Assert.That(session.ColoredInk(InkColor.Red), Is.EqualTo(9));
            Assert.That(session.Structures, Is.Empty); Assert.That(session.Statistics.BuildInvestment, Is.Zero);
        }

        [Test] public void AColoredBatchDebitsItsStockOnceAndCopiesTheColorToEveryInstance()
        {
            var balance = Rules(); var entry = Starter(0, balance);
            var session = GameSession.CreatePrepared(balance, 7, Array.Empty<PreparedEnemy>(), redInk: 10);
            Assert.That(session.TryBuildEntry(new[] { new Cell(4, 4), new Cell(6, 4) }, entry, MaterialTier.Pencil, InkColor.Red, out string reason), Is.True, reason);
            Assert.That(session.Ink, Is.EqualTo(560)); Assert.That(session.ColoredInk(InkColor.Red), Is.Zero);
            Assert.That(session.Structures.Select(s => s.Color), Is.All.EqualTo(InkColor.Red));
        }

        [TestCase(0, true)] [TestCase(4, true)] [TestCase(5, false)] [TestCase(6, true)] [TestCase(7, true)]
        public void CompletedStructuresAffectNavigationWhileTheSlowSupportStaysPassable(int index, bool blocking)
        {
            var balance = Rules(); var session = new GameSession(balance, starterDefenses: false); var entry = Starter(index, balance);
            var cell = new Cell(5, 5); var structure = Build(session, entry, cell);
            var field = new WeightedFlowField(balance.Width, balance.Height); var grid = Grid(session, balance);
            field.Rebuild(new Cell(8, 5), grid, breachDamagePerSecond: 1);
            Assert.That(field.Next(new Cell(4, 5)), Is.EqualTo(cell), "Unfinished instances do not block movement.");
            session.Tick(entry.BuildSeconds + .05f);
            field.Rebuild(new Cell(8, 5), grid, breachDamagePerSecond: 1);
            Assert.That(field.Next(new Cell(4, 5)) == cell, Is.EqualTo(!blocking));
            Assert.That(session.QuoteEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None).Valid, Is.False,
                "Passable support still owns its construction cell.");
            structure.Health = 0; field.Rebuild(new Cell(8, 5), grid, breachDamagePerSecond: 1);
            Assert.That(field.Next(new Cell(4, 5)), Is.EqualTo(cell), "Destroyed instances have no breach cost.");
        }

        [TestCase(0, 16)] [TestCase(4, 6)] [TestCase(5, 8)] [TestCase(6, 26)] [TestCase(7, 0)]
        public void ErasingAnInstanceUsesItsPaidCostHealthFractionAndResolvedRefundPolicy(int index, int refund)
        {
            var balance = Rules(); var session = new GameSession(balance, starterDefenses: false); var entry = Starter(index, balance);
            var cell = new Cell(4, 4); var structure = Build(session, entry, cell); structure.Health = structure.MaxHealth / 2;
            Assert.That(session.TryErase(cell, out int actual), Is.True); Assert.That(actual, Is.EqualTo(refund));
            Assert.That(session.Ink, Is.EqualTo(640 - entry.Cost + refund)); Assert.That(session.StructureAt(cell), Is.Null);
            Assert.That(session.Structures, Is.Empty); Assert.That(session.Statistics.RefundedInk, Is.EqualTo(refund));
            Assert.That(session.Statistics.DestroyedStructures, Is.Zero);
            Assert.That(session.QuoteEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None).Valid, Is.True);
        }

        [Test] public void AnEnemyGlobDestroysACompletedBarricadeClearsItsCellAndRecordsItsRealInvestment()
        {
            var balance = Rules(); var session = new GameSession(balance, starterDefenses: false); var entry = Starter(4, balance);
            var cell = new Cell(5, 5); var wall = Build(session, entry, cell); session.Tick(entry.BuildSeconds + .05f);
            ((List<EnemyGlob>)session.EnemyProjectiles).Add(new EnemyGlob { Id = 1, From = new Point2(3.5f, 5.5f),
                To = new Point2(7.5f, 5.5f), Damage = 1000, Duration = 0, Remaining = 0, ObjectiveIndex = int.MinValue });
            session.Tick(.01f);
            Assert.That(session.StructureAt(cell), Is.Null); Assert.That(session.Structures, Is.Empty);
            Assert.That(session.Statistics.DestroyedStructures, Is.EqualTo(1)); Assert.That(session.Statistics.LostInvestment, Is.EqualTo(16));
            Assert.That(session.Statistics.Breaches.Single().Investment, Is.EqualTo(16));
            Assert.That(session.EventBuffer.Count(e => e.Kind == SimulationEventKind.StructureDestroyed && e.EntityId == wall.Id), Is.EqualTo(1));
            Assert.That(session.QuoteEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None).Valid, Is.True);
        }

        [Test] public void TheThirdGeneratorIsRejectedIncludingWhenAllThreeAreInOneBatch()
        {
            var balance = Rules(); var entry = Starter(7, balance); var session = new GameSession(balance, starterDefenses: false);
            var cells = new[] { new Cell(4, 4), new Cell(6, 4), new Cell(8, 4) };
            Assert.That(session.TryBuildEntry(cells, entry, MaterialTier.Pencil, InkColor.None, out _), Is.False);
            Assert.That(session.Ink, Is.EqualTo(640)); Assert.That(session.Structures, Is.Empty);
            Assert.That(session.TryBuildEntry(cells.Take(2).ToArray(), entry, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            Assert.That(session.TryBuildEntry(new[] { cells[2] }, entry, MaterialTier.Pencil, InkColor.None, out _), Is.False);
            Assert.That(session.Ink, Is.EqualTo(520)); Assert.That(session.Structures.Count, Is.EqualTo(2));
            Assert.That(session.TryErase(cells[0], out int refund), Is.True); Assert.That(refund, Is.Zero);
            Assert.That(session.TryBuildEntry(new[] { cells[2] }, entry, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
        }

        [Test] public void ResolvedGeneralDefenseIsCappedAtPointSixAndAppliesToOrdinaryAndDirectionalHits()
        {
            var balance = Rules(); var input = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(input, "armor.plate", AssemblyStat.Defense, ModifierOperation.Add, .8f);
            var entry = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(foundation: AssemblyResolverFixture.Foundation(armor: "plate")),
                AssemblyResolverFixture.Modified(input), balance);
            var tower = new StructureState(entry) { Cell = new Cell(10, 10), Aim = new Point2(20.5f, 10.5f) };
            Assert.That(entry.Defense, Is.EqualTo(.6f));
            Assert.That(DamageRules.StructureDamage(100, tower, balance), Is.EqualTo(40).Within(.0001f));
            Assert.That(DamageRules.StructureDamage(100, tower, balance, new Point2(13.5f, 10.5f)), Is.EqualTo(40).Within(.0001f));
            Assert.That(DamageRules.StructureDamage(100, tower, balance, new Point2(7.5f, 10.5f)), Is.EqualTo(40).Within(.0001f));
        }

        [TestCase(13.5f, 10.5f, 60f)] [TestCase(12.5f, 12.5f, 60f)] [TestCase(12.5f, 8.5f, 60f)]
        [TestCase(12.5f, 12.51f, 100f)] [TestCase(10.5f, 13.5f, 100f)] [TestCase(7.5f, 10.5f, 100f)]
        public void FrontShieldUsesTheInclusiveNinetyDegreeFoundationSectorRatherThanMountAims(float x, float y, float damage)
        {
            var balance = Rules(); var entry = AssemblyResolverFixture.Resolve(
                AssemblyResolverFixture.Tower(foundation: AssemblyResolverFixture.Foundation(armor: "front")), balance: balance);
            var tower = new StructureState(entry) { Cell = new Cell(10, 10), Aim = new Point2(20.5f, 10.5f) };
            tower.GetMountState(0).Aim = new Point2(.5f, 10.5f); tower.GetMountState(1).Aim = new Point2(.5f, 10.5f);
            Assert.That(DamageRules.StructureDamage(100, tower, balance, new Point2(x, y)), Is.EqualTo(damage).Within(.0001f));
        }

        [Test] public void FrontAndGeneralDefenseUseTheirMaximumInsteadOfMultiplyingOrAddingReductions()
        {
            var balance = Rules(); var input = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(input, "armor.front", AssemblyStat.Defense, ModifierOperation.Add, .2f);
            var entry = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(foundation: AssemblyResolverFixture.Foundation(armor: "front")),
                AssemblyResolverFixture.Modified(input), balance);
            var tower = new StructureState(entry) { Cell = new Cell(10, 10), Aim = new Point2(20.5f, 10.5f) };
            Assert.That(DamageRules.StructureDamage(100, tower, balance), Is.EqualTo(80).Within(.0001f));
            Assert.That(DamageRules.StructureDamage(100, tower, balance, new Point2(13.5f, 10.5f)), Is.EqualTo(60).Within(.0001f));
            Assert.That(DamageRules.StructureDamage(100, tower, balance, new Point2(7.5f, 10.5f)), Is.EqualTo(80).Within(.0001f));
        }

        [Test] public void ShootingUpdatesTheMountAimWithoutTurningTheShieldFoundation()
        {
            var balance = Rules(); var entry = AssemblyResolverFixture.Resolve(
                AssemblyResolverFixture.Tower(foundation: AssemblyResolverFixture.Foundation(armor: "front")), balance: balance);
            var tower = new StructureState(entry) { Id = 1, Cell = new Cell(10, 10), IsComplete = true, BuildRemaining = 0,
                Aim = new Point2(10.5f, 20.5f) };
            var enemy = new EnemyState { Id = 1, Position = new Point2(12.5f, 10.5f), Health = 1000, MaxHealth = 1000 };
            new WeaponSystem(balance).Fire(new List<StructureState> { tower }, new List<EnemyState> { enemy }, new List<ShotEvent>(),
                1, 1, .01f, 1, new RunStatistics(), new SimulationEventBuffer(), .01);
            Assert.That(tower.Aim, Is.EqualTo(new Point2(10.5f, 20.5f)));
            Assert.That(tower.GetMountState(0).Aim, Is.EqualTo(enemy.Position));
            Assert.That(DamageRules.StructureDamage(100, tower, balance, new Point2(10.5f, 13.5f)), Is.EqualTo(60).Within(.0001f));
            Assert.That(DamageRules.StructureDamage(100, tower, balance, new Point2(13.5f, 10.5f)), Is.EqualTo(100).Within(.0001f));
        }

        [Test] public void CompositeTrialStartsWithTheSameFoundationFacingAndShieldProtectionAsBattle()
        {
            var balance = Rules(); balance.Trials.StepSeconds = .05f; balance.Trials.MaximumTicks = 100;
            balance.Trials.SpawnX = 34.5f; balance.Trials.SpawnY = 10.5f; balance.Trials.SpawnYVariation = 0;
            balance.Trials.CloseTowerX = 32; balance.Trials.CloseTowerY = 10;
            var crowd = balance.Trials.For(TrialScenario.Crowd); crowd.Count = 1; crowd.Health = 1000000; crowd.Speed = .00001f;
            var entry = AssemblyResolverFixture.Resolve(
                AssemblyResolverFixture.Tower(foundation: AssemblyResolverFixture.Foundation(armor: "front")), balance: balance);
            var page = new AlbumEntry("front.trial", "Front shield", 1, entry.Snapshot);
            var session = new GameSession(balance, starterDefenses: false);
            var tower = Build(session, entry, new Cell(32, 10));
            var report = TrialRunner.Run(page, balance, TrialScenario.Crowd, entry.Cost, counterfire: true);
            var initial = report.Frames[0].Towers.Single();
            Assert.That(initial.Aim, Is.EqualTo(tower.Aim));
            var damaged = report.Frames.First(frame => frame.Towers.Single().Health < initial.Health).Towers.Single();
            Assert.That(initial.Health - damaged.Health, Is.EqualTo(balance.Trials.CounterfireDamage * .6f).Within(.0001f));
        }

        [Test] public void BuiltTowerAndItsShotsUseOneCompatibleWeaponMetricsRow()
        {
            var balance = Rules(); var entry = Starter(0, balance);
            var session = GameSession.CreatePrepared(balance, 7,
                new[] { new PreparedEnemy(new Point2(12.5f, 10.5f), 1000000) });
            Build(session, entry, new Cell(10, 10)); session.Tick(entry.BuildSeconds + .1f);
            Assert.That(session.Statistics.BuiltCount(StructureKind.Gun, MaterialTier.Pencil), Is.EqualTo(1));
            Assert.That(session.Statistics.Launches(StructureKind.Gun), Is.GreaterThan(0));
            Assert.That(session.Statistics.WeaponIds, Is.EqualTo(new[] { "weapon.gun" }));
        }
    }
}
