#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class CompositeConstructionIntegrationTests
    {
        static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static void Start(AssemblyCommandFixture fixture)
            => Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);

        static Cell ClearCell(RunController run, int slot, MaterialTier material = MaterialTier.Pencil)
        {
            for (int y = 2; y < run.Session.Balance.Height - 2; y++)
            for (int x = 2; x < run.Session.Balance.Width - 2; x++)
            {
                var cell = new Cell(x, y);
                if (run.QuoteEntry(new[] { cell }, slot, material, InkColor.None).Valid) return cell;
            }
            Assert.Fail("The isolated board must have an affordable legal cell for slot " + slot);
            return default;
        }

        static Cell[] ClearLine(RunController run, int slot)
        {
            for (int y = 2; y < run.Session.Balance.Height - 2; y++)
            for (int x = 2; x < run.Session.Balance.Width - 5; x++)
            {
                var cells = Enumerable.Range(0, 4).Select(offset => new Cell(x + offset, y)).ToArray();
                if (cells.All(cell => run.QuoteEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None).Valid)) return cells;
            }
            Assert.Fail("The isolated board must contain a clear four-cell construction gesture.");
            return Array.Empty<Cell>();
        }

        static Cell ClearProbeCell(GameSession probe, ResolvedEntry entry)
        {
            var rules = probe.Balance;
            for (int y = 2; y < rules.Height - 2; y++)
            for (int x = 2; x < rules.Width - 2; x++)
            {
                var cell = new Cell(x, y);
                if (probe.QuoteEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None).Valid) return cell;
            }
            Assert.Fail("The funded probe must find a legal cell independently of the actual session's empty wallet.");
            return default;
        }

        static GameSession Session(RunController run)
            => (GameSession)typeof(RunController).GetField("session", PrivateInstance).GetValue(run);

        static void FundColor(RunController run, InkColor color, int amount)
            => ((int[])typeof(GameSession).GetField("coloredInk", PrivateInstance).GetValue(Session(run)))[(int)color] = amount;

        static void UseLegacyPages(WorkshopProfileData data)
        {
            var historical = new WorkshopProfile().ToData();
            data.Entries = historical.Entries; data.Album = historical.Album; data.Loadout = historical.Loadout;
            data.Unlocked = data.Unlocked.Concat(historical.Unlocked).Distinct().ToArray();
        }

        static void AssertDirectSupportToolsBlocked(RunController run, Cell cell)
        {
            int ink = run.Session.Ink, entities = run.Session.Structures.Count;
            foreach (var kind in new[] { StructureKind.Wall, StructureKind.Spikes, StructureKind.TarPit, StructureKind.RepairPost })
            {
                var quote = run.QuoteBuild(new[] { cell }, kind, MaterialTier.Pencil);
                Assert.That(quote.Valid, Is.False, "Configured album runs must build " + kind + " through a selected page.");
                Assert.That(run.Build(new[] { cell }, kind, MaterialTier.Pencil, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo(quote.Reason));
            }
            Assert.That(run.Session.Ink, Is.EqualTo(ink));
            Assert.That(run.Session.Structures.Count, Is.EqualTo(entities));
        }

        [TestCase(false)] [TestCase(true)]
        public void ExistingWorldViewCanBindAllEightCompositeTypesWithAndWithoutRanges(bool ranges)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture);
                for (int slot = 0; slot < 8; slot++)
                    Assert.That(fixture.Run.BuildEntry(new[] { ClearCell(fixture.Run, slot) }, slot,
                        MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                var root = new GameObject("Composite construction render boundary"); root.SetActive(false);
                try
                {
                    var view = root.AddComponent<NotebookWorldView>(); view.ShowRanges = ranges;
                    Assert.DoesNotThrow(() => view.Initialize(fixture.Run.Session));
                    Assert.That(view.StaticGeometryRefreshCount, Is.GreaterThan(0));
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test] public void EightFrozenAlbumSlotsBuildTowersAndSupportForExactly426Ink()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture);
                Assert.That(fixture.Run.Session.Ink, Is.EqualTo(640));
                Assert.That(fixture.Run.RunLoadout.Entries.Count, Is.EqualTo(8));
                for (int slot = 0; slot < 8; slot++)
                {
                    var expected = fixture.Run.RunLoadout.ResolveSlot(slot, MaterialTier.Pencil);
                    var cell = ClearCell(fixture.Run, slot); int ink = fixture.Run.Session.Ink;
                    var quote = fixture.Run.QuoteEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None);
                    Assert.That(quote.Valid, Is.True, quote.Reason);
                    Assert.That(quote.Cost, Is.EqualTo(expected.Cost));
                    Assert.That(fixture.Run.BuildEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                    var built = fixture.Run.Session.StructureAt(cell).Value;
                    Assert.That(built.ResolvedEntry, Is.Not.Null);
                    Assert.That(built.ResolvedEntry.Kind, Is.EqualTo(expected.Kind));
                    Assert.That(built.MaxHealth, Is.EqualTo(expected.Health).Within(.0001f));
                    Assert.That(built.BuildRemaining, Is.EqualTo(expected.BuildSeconds).Within(.0001f));
                    Assert.That(ink - fixture.Run.Session.Ink, Is.EqualTo(expected.Cost));
                }
                Assert.That(fixture.Run.Session.Structures.Count, Is.EqualTo(8));
                Assert.That(fixture.Run.Session.Ink, Is.EqualTo(214));
            }
        }

        [TestCase(MaterialTier.Pencil)] [TestCase(MaterialTier.Ink)] [TestCase(MaterialTier.Marker)]
        public void MaterialPriceHealthAndConstructionTimeComeFromTheFrozenResolvedEntry(MaterialTier material)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); var expected = fixture.Run.RunLoadout.ResolveSlot(0, material);
                var cell = ClearCell(fixture.Run, 0, material); int ink = fixture.Run.Session.Ink;
                var quote = fixture.Run.QuoteEntry(new[] { cell }, 0, material, InkColor.None);
                Assert.That(quote.Cost, Is.EqualTo(expected.Cost));
                Assert.That(fixture.Run.BuildEntry(new[] { cell }, 0, material, InkColor.None, out var reason), Is.True, reason);
                var built = fixture.Run.Session.StructureAt(cell).Value;
                Assert.That(built.Tier, Is.EqualTo(material));
                Assert.That(built.ResolvedEntry.Material, Is.EqualTo(material));
                Assert.That(built.Health, Is.EqualTo(expected.Health).Within(.0001f));
                Assert.That(built.MaxHealth, Is.EqualTo(expected.Health).Within(.0001f));
                Assert.That(built.BuildRemaining, Is.EqualTo(expected.BuildSeconds).Within(.0001f));
                Assert.That(ink - fixture.Run.Session.Ink, Is.EqualTo(expected.Cost));
            }
        }

        [Test] public void ColoredTowerUsesPencilCartridgePriceEvenWhenBuiltWithMarker()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); FundColor(fixture.Run, InkColor.Red, 100);
                var rules = fixture.Run.RunLoadout.Balance.Construction;
                int pencilCost = fixture.Run.RunLoadout.ResolveSlot(0, MaterialTier.Pencil).Cost;
                int expectedColor = Math.Max(rules.ColorMinimum, (pencilCost + rules.ColorCostDivisor - 1) / rules.ColorCostDivisor);
                var cell = ClearCell(fixture.Run, 0, MaterialTier.Marker);
                int ink = fixture.Run.Session.Ink, color = fixture.Run.Session.ColoredInk(InkColor.Red);
                var quote = fixture.Run.QuoteEntry(new[] { cell }, 0, MaterialTier.Marker, InkColor.Red);
                Assert.That(quote.Valid, Is.True, quote.Reason);
                Assert.That(fixture.Run.BuildEntry(new[] { cell }, 0, MaterialTier.Marker, InkColor.Red, out var reason), Is.True, reason);
                Assert.That(ink - fixture.Run.Session.Ink, Is.EqualTo(quote.Cost));
                Assert.That(color - fixture.Run.Session.ColoredInk(InkColor.Red), Is.EqualTo(expectedColor));
                Assert.That(fixture.Run.Session.StructureAt(cell).Value.Color, Is.EqualTo(InkColor.Red));
            }
        }

        [TestCase("outside")] [TestCase("occupied")] [TestCase("empty-slot")]
        [TestCase("no-ink")] [TestCase("no-color")] [TestCase("support-color")]
        public void RejectedConstructionLeavesEntitiesInkAndCartridgesUntouched(string failure)
        {
            using (var fixture = new AssemblyCommandFixture(configure: data => {
                if (failure == "empty-slot") data.Loadout[7] = null;
            }))
            {
                if (failure == "no-ink") fixture.Preset.rules.StartingInk = 0;
                Start(fixture);
                int slot = failure == "empty-slot" ? 7 : failure == "support-color" ? 4 : 0;
                Cell cell;
                if (failure == "no-ink")
                {
                    var rules = fixture.Run.RunLoadout.Balance; rules.StartingInk = 100000;
                    var probe = new GameSession(rules, starterDefenses: false);
                    var entry = fixture.Run.RunLoadout.ResolveSlot(0, MaterialTier.Pencil);
                    cell = ClearProbeCell(probe, entry);
                }
                else cell = ClearCell(fixture.Run, failure == "empty-slot" ? 0 : slot);
                var cells = new[] { cell };
                InkColor color = failure == "no-color" || failure == "support-color" ? InkColor.Red : InkColor.None;
                if (failure == "support-color") FundColor(fixture.Run, InkColor.Red, 100);
                if (failure == "outside") cells = new[] { cell, new Cell(-1, 0) };
                if (failure == "occupied")
                {
                    Assert.That(fixture.Run.BuildEntry(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None, out var existingReason), Is.True, existingReason);
                    cells = new[] { ClearCell(fixture.Run, 0), cell };
                }
                int entities = fixture.Run.Session.Structures.Count, ink = fixture.Run.Session.Ink;
                int cartridges = fixture.Run.Session.ColoredInk(InkColor.Red);
                var quote = fixture.Run.QuoteEntry(cells, slot, MaterialTier.Pencil, color);
                Assert.That(quote.Valid, Is.False);
                Assert.That(fixture.Run.BuildEntry(cells, slot, MaterialTier.Pencil, color, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo(quote.Reason));
                Assert.That(fixture.Run.Session.Structures.Count, Is.EqualTo(entities));
                Assert.That(fixture.Run.Session.Ink, Is.EqualTo(ink));
                Assert.That(fixture.Run.Session.ColoredInk(InkColor.Red), Is.EqualTo(cartridges));
            }
        }

        [Test] public void RepeatedTowerInstancesSpendBattleInkWithoutChangingPermanentRightsOrProfile()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 94, xp: 390))
            {
                Start(fixture); string before = fixture.State(); byte[] disk = fixture.SavedBytes;
                int unitCost = fixture.Run.RunLoadout.ResolveSlot(0, MaterialTier.Pencil).Cost, ink = fixture.Run.Session.Ink;
                for (int copy = 0; copy < 3; copy++)
                    Assert.That(fixture.Run.BuildEntry(new[] { ClearCell(fixture.Run, 0) }, 0, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                Assert.That(fixture.Run.Session.Structures.Count, Is.EqualTo(3));
                Assert.That(ink - fixture.Run.Session.Ink, Is.EqualTo(3 * unitCost));
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void ConstructionIgnoresLaterProfileSlotAndAuthoringChanges()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); var snapshot = fixture.Run.RunLoadout;
                var expected = snapshot.ResolveSlot(0, MaterialTier.Marker);
                var cell = ClearCell(fixture.Run, 0, MaterialTier.Marker);
                var profileField = typeof(RunController).GetField("profile", PrivateInstance);
                var profile = (WorkshopProfile)profileField.GetValue(fixture.Run);
                var data = profile.ToData();
                string previous = data.Loadout[0]; data.Loadout[0] = data.Loadout[6]; data.Loadout[6] = previous;
                var changedProfile = WorkshopProfile.FromData(data, fixture.Catalog, fixture.Balance);
                Assert.That(changedProfile.GetLoadoutEntries()[0].Kind, Is.EqualTo(AlbumEntryKind.Structure));
                profileField.SetValue(fixture.Run, changedProfile);
                fixture.AssemblyAsset.catalog.Definitions.Single(part => part.Id == "core.gun").Modifiers.Single(modifier => modifier.Stat == AssemblyStat.Cost).Value = 999;
                fixture.Preset.rules.MaterialDefinitions[(int)MaterialTier.Marker].CostMultiplier = 100;
                fixture.Preset.rules.MaterialDefinitions[(int)MaterialTier.Marker].HealthMultiplier = 100;
                fixture.Preset.rules.MaterialDefinitions[(int)MaterialTier.Marker].BuildTimeMultiplier = 100;

                var quote = fixture.Run.QuoteEntry(new[] { cell }, 0, MaterialTier.Marker, InkColor.None);
                Assert.That(quote.Valid, Is.True, quote.Reason); Assert.That(quote.Cost, Is.EqualTo(expected.Cost));
                Assert.That(fixture.Run.BuildEntry(new[] { cell }, 0, MaterialTier.Marker, InkColor.None, out var reason), Is.True, reason);
                var built = fixture.Run.Session.StructureAt(cell).Value;
                Assert.That(fixture.Run.RunLoadout, Is.SameAs(snapshot));
                Assert.That(built.ResolvedEntry.Kind, Is.EqualTo(AlbumEntryKind.Tower));
                Assert.That(built.MaxHealth, Is.EqualTo(expected.Health).Within(.0001f));
                Assert.That(built.BuildRemaining, Is.EqualTo(expected.BuildSeconds).Within(.0001f));
            }
        }

        [TestCase(0, false)] [TestCase(4, true)] [TestCase(5, true)] [TestCase(6, false)] [TestCase(7, false)]
        public void GestureUsesTheFrozenSlotEntryForContinuousOrSinglePlacement(int slot, bool continuous)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); var entry = fixture.Run.RunLoadout.ResolveSlot(slot, MaterialTier.Pencil);
                var line = ClearLine(fixture.Run, slot); var gesture = new ConstructionGesture();
                gesture.Begin(line[0], entry); gesture.Move(line[3]);
                Assert.That(gesture.Cells.Count, Is.EqualTo(continuous ? 4 : 1));
                Assert.That(gesture.Cells.Last(), Is.EqualTo(line[3]));
                var quote = fixture.Run.QuoteEntry(gesture.Cells, slot, MaterialTier.Pencil, InkColor.None);
                Assert.That(quote.Valid, Is.True, quote.Reason);
                Assert.That(quote.Cost, Is.EqualTo(entry.Cost * gesture.Cells.Count));
                Assert.That(fixture.Run.BuildEntry(gesture.Cells, slot, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                Assert.That(fixture.Run.Session.Structures.Count, Is.EqualTo(gesture.Cells.Count));
                foreach (var cell in gesture.Cells)
                    Assert.That(fixture.Run.Session.StructureAt(cell).Value.ResolvedEntry.Kind, Is.EqualTo(entry.Kind));
            }
        }

        [Test] public void ModernAlbumBlocksDirectSupportToolsWhileLegacyFixtureCommandsRemainCompatible()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); var cell = ClearCell(fixture.Run, 4); int ink = fixture.Run.Session.Ink;
                foreach (var kind in new[] { StructureKind.Wall, StructureKind.Spikes, StructureKind.TarPit, StructureKind.RepairPost })
                {
                    var quote = fixture.Run.QuoteBuild(new[] { cell }, kind, MaterialTier.Pencil);
                    Assert.That(quote.Valid, Is.False);
                    Assert.That(fixture.Run.Build(new[] { cell }, kind, MaterialTier.Pencil, out var reason), Is.False);
                    Assert.That(reason, Is.EqualTo(quote.Reason));
                }
                Assert.That(fixture.Run.Session.Ink, Is.EqualTo(ink)); Assert.That(fixture.Run.Session.Structures, Is.Empty);
            }
            var root = new GameObject("Isolated historical construction adapter"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>(); asset.ResetToDefaults();
            try
            {
                var run = root.AddComponent<RunController>(); run.AutomaticSimulation = false; run.balanceAsset = asset;
                run.InitializeWorkshop(false); Assert.That(run.StartRun(out var reason), Is.True, reason);
                var cell = new Cell(12, 12);
                var quote = run.QuoteBlueprint(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None);
                Assert.That(quote.Valid, Is.True, quote.Reason);
                Assert.That(run.BuildBlueprint(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
                Assert.That(run.Session.StructureAt(cell).Value.Blueprint, Is.Not.Null);
                Assert.That(run.Build(new[] { new Cell(14, 12) }, StructureKind.Wall, MaterialTier.Pencil, out reason), Is.True, reason);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }

        [Test] public void ConfiguredAlbumWithOnlyLegacyPagesStillRequiresSelectedSlotsForSupportTools()
        {
            using (var fixture = new AssemblyCommandFixture(configure: UseLegacyPages))
            {
                Assert.That(fixture.Database.assemblyCatalog, Is.Not.Null);
                Start(fixture);
                Assert.That(fixture.Run.RunLoadout.Entries.Where(entry => entry != null).Select(entry => entry.Kind),
                    Is.Not.Empty.And.All.EqualTo(AlbumEntryKind.Legacy));
                var cell = ClearCell(fixture.Run, 0);
                AssertDirectSupportToolsBlocked(fixture.Run, cell);
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void ConfiguredAlbumSlotRequirementSurvivesRemovingOrReplacingItsAuthoringCatalog(bool removeCatalog)
        {
            using (var fixture = new AssemblyCommandFixture(configure: UseLegacyPages))
            {
                Start(fixture); var snapshot = fixture.Run.RunLoadout;
                var cell = ClearCell(fixture.Run, 0);
                var replacement = ScriptableObject.CreateInstance<AssemblyCatalogAsset>();
                try
                {
                    replacement.catalog = new AssemblyCatalogInput { BalanceRevision = "later.empty.catalog" };
                    fixture.Database.assemblyCatalog = removeCatalog ? null : replacement;
                    Assert.That(fixture.Run.RunLoadout, Is.SameAs(snapshot));
                    AssertDirectSupportToolsBlocked(fixture.Run, cell);
                    Assert.That(fixture.Run.BuildEntry(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                    Assert.That(fixture.Run.Session.StructureAt(cell).Value.Blueprint, Is.Not.Null,
                        "A frozen historical page remains usable after authoring configuration changes.");
                }
                finally { Object.DestroyImmediate(replacement); }
            }
        }
    }
}
#endif
