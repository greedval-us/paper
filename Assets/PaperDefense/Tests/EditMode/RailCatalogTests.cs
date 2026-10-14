using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class RailCatalogTests
    {
        [Test] public void RailConsumesLoadGoldAndConstructionTimeWithoutChangingFeedOrDurability()
        {
            var balance = GameBalance.CreateDefault();
            var ordinary = new TowerBlueprint("plain", "Пулемёт", ChassisKind.Standard, StructureKind.Gun);
            var rail = new TowerBlueprint("rail", "Пулемёт на направляющей", ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Rail);
            Assert.That((int)TowerModules.Rail, Is.EqualTo(8));
            Assert.That(TowerCatalog.Load(rail, balance), Is.EqualTo(6));
            Assert.That(TowerCatalog.Cost(rail, balance, MaterialTier.Pencil), Is.EqualTo(60));
            Assert.That(TowerCatalog.BuildSeconds(rail, balance, MaterialTier.Pencil), Is.EqualTo(1.65f).Within(.00001f));
            Assert.That(TowerCatalog.Validate(rail, balance, out string reason), Is.True, reason);
            Assert.That(TowerCatalog.Health(rail, balance, MaterialTier.Pencil), Is.EqualTo(TowerCatalog.Health(ordinary, balance, MaterialTier.Pencil)));
            Assert.That(TowerCatalog.Defense(rail, balance), Is.EqualTo(TowerCatalog.Defense(ordinary, balance)));
            balance.Construction.DynamicFeed = true;
            Assert.That(TowerCatalog.FeedMultiplier(rail, balance), Is.EqualTo(TowerCatalog.FeedMultiplier(ordinary, balance)));
            var doubleRail = new TowerBlueprint("double", "Два пулемёта", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun, TowerModules.Rail);
            Assert.That(TowerCatalog.Load(doubleRail, balance), Is.EqualTo(10));
            Assert.That(TowerCatalog.Validate(doubleRail, balance, out reason), Is.True, reason);
            balance.Construction.RailLoad = 3;
            Assert.That(TowerCatalog.Validate(doubleRail, balance, out _), Is.False);
        }

        [TestCase(StructureKind.Gun)] [TestCase(StructureKind.Piercer)] [TestCase(StructureKind.Mortar)]
        [TestCase(StructureKind.Fan)] [TestCase(StructureKind.Ricochet)] [TestCase(StructureKind.Stapler)] [TestCase(StructureKind.Blotter)]
        public void ConfiguredRailChangesEveryMountRangeAndActionIntervalOnly(StructureKind kind)
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.RailRangeMultiplier = 1.7f; balance.Construction.RailIntervalMultiplier = 1.4f;
            var plain = new TowerBlueprint("plain", "Обычный", ChassisKind.Standard, kind);
            var rail = new TowerBlueprint("rail", "Направляющая", ChassisKind.Standard, kind, modules: TowerModules.Rail);
            var definition = balance.Structure(kind); float radius = definition.Radius, flight = definition.FlightSeconds;
            Assert.That(TowerCatalog.WeaponRange(rail, balance, kind), Is.EqualTo(definition.Range * 1.7f).Within(.00001f));
            Assert.That(TowerCatalog.WeaponRange(plain, balance, kind), Is.EqualTo(definition.Range));
            float ordinary = TowerCatalog.ActionInterval(definition, .8f, .9f);
            Assert.That(TowerCatalog.ActionInterval(rail, balance, kind, .8f, .9f), Is.EqualTo(ordinary * 1.4f).Within(.00001f));
            Assert.That(TowerCatalog.ActionInterval(plain, balance, kind, .8f, .9f), Is.EqualTo(ordinary));
            Assert.That(definition.Radius, Is.EqualTo(radius)); Assert.That(definition.FlightSeconds, Is.EqualTo(flight));
        }

        [Test] public void RailIntervalIsAppliedAfterTheHistoricalProductionFloorAndDrawingQuality()
        {
            var balance = GameBalance.CreateDefault(); balance.Structure(StructureKind.Gun).ShotInterval = .01f;
            var rail = new TowerBlueprint("rail", "Направляющая", ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Rail);
            Assert.That(TowerCatalog.ActionInterval(rail, balance, StructureKind.Gun, 100, .5f), Is.EqualTo(.12f).Within(.00001f));
        }

        [Test] public void RailConfiguredTuningIsOwnedBySnapshotsAndReadonlyRangeViews()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.RailLoad = 3; balance.Construction.RailCost = 27;
            balance.Construction.RailBuildSeconds = .7f; balance.Construction.RailRangeMultiplier = 2; balance.Construction.RailIntervalMultiplier = 1.3f;
            var copy = balance.CreateSnapshot(); var rail = new TowerBlueprint("rail", "Направляющая", ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Rail);
            var view = new RunReadModel(new GameSession(copy, starterDefenses: false)).Balance;
            balance.Construction.RailLoad = 9; balance.Construction.RailCost = 90; balance.Construction.RailRangeMultiplier = 4;
            Assert.That(TowerCatalog.Load(rail, copy), Is.EqualTo(7)); Assert.That(TowerCatalog.Cost(rail, copy, MaterialTier.Pencil), Is.EqualTo(67));
            Assert.That(TowerCatalog.BuildSeconds(rail, copy, MaterialTier.Pencil), Is.EqualTo(2.1f).Within(.00001f));
            Assert.That(view.WeaponRange(rail, StructureKind.Gun), Is.EqualTo(22));
            Assert.That(TowerCatalog.ActionInterval(rail, copy, StructureKind.Gun, 1, 1), Is.EqualTo(.546f).Within(.00001f));
        }

        [Test] public void RailNeedsASeparatePermanentRightAndChargesConfiguredGoldOnce()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.RailUnlockGold = 17;
            var profile = new WorkshopProfile(); profile.GrantGold(50);
            var page = new TowerBlueprint("rail", "Направляющая", ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Rail);
            Assert.That(profile.SaveBlueprint(page, out _, balance), Is.False);
            PartUnlock part = profile.GetParts(balance).Single(p => p.Id == "module.rail");
            Assert.That(part.GoldCost, Is.EqualTo(17)); Assert.That(part.Description, Is.Not.Empty);
            Assert.That(profile.Unlock(part.Id, out string reason, balance), Is.True, reason); Assert.That(profile.Gold, Is.EqualTo(33));
            Assert.That(profile.Unlock(part.Id, out reason, balance), Is.True, reason); Assert.That(profile.Gold, Is.EqualTo(33));
            Assert.That(profile.SaveBlueprint(page, out reason, balance), Is.True, reason);
        }

        [Test] public void AssemblyChangesKeepUnchangedRailLinesAndRemoveOnlyDisabledRailLines()
        {
            var line = new DrawingStroke(new[] { new DrawingPoint(-.2f, -.1f), new DrawingPoint(.2f, .1f) });
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, new[] { line }),
                new DrawingPart(DrawingPartKey.Mount0, new[] { line }), new DrawingPart(DrawingPartKey.Rail, new[] { line }) });
            var page = TowerBlueprint.NewDraft("rail", "Линии направляющей", ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Rail).WithDrawing(drawing);
            var changedWeapon = page.WithAssembly(page.Chassis, StructureKind.Fan, null, page.Modules);
            Assert.That(changedWeapon.Drawing.GetPart(DrawingPartKey.Rail).Strokes.Count, Is.EqualTo(1));
            Assert.That(changedWeapon.Drawing.GetPart(DrawingPartKey.Mount0).Strokes, Is.Empty);
            var removed = page.WithAssembly(page.Chassis, page.FirstWeapon, null, TowerModules.None);
            Assert.That(removed.Drawing.GetPart(DrawingPartKey.Rail).Strokes, Is.Empty);
            Assert.That(removed.Drawing.GetPart(DrawingPartKey.Base).Strokes.Count, Is.EqualTo(1));
            Assert.That(removed.Drawing.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void VersionTwoStillRejectsRailRightsAndAssemblies(bool right)
        {
            var data = HistoricalCatalogTestData.VersionTwo();
            if (right) data.Unlocked = data.Unlocked.Concat(new[] { "module.rail" }).ToArray();
            else data.Album[0].Modules |= TowerModules.Rail;
            Assert.That(() => WorkshopProfile.FromData(data), Throws.InstanceOf<ArgumentException>());
        }
    }
}
