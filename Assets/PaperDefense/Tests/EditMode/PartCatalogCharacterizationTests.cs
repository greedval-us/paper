using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class PartCatalogCharacterizationTests
    {
        [Test]
        public void HistoricalPartsKeepTheirStableOrderPricesAndInitialRights()
        {
            var profile = new WorkshopProfile();
            string[] ids = { "chassis.standard", "weapon.gun", "weapon.piercer", "weapon.mortar", "module.armor",
                "chassis.heavy", "module.feed", "module.tracking", "weapon.fan", "weapon.ricochet",
                "weapon.stapler", "weapon.blotter", "module.rail" };
            int[] prices = { 0, 0, 0, 0, 0, 90, 60, 60, 60, 60, 40, 40, 60 };
            string[] names = { "Обычное основание", "Пулемёт", "Пробивающая пушка", "Мортира", "Бронепластины",
                "Тяжёлое основание", "Улучшенная подача", "Быстрое наведение", "Веерный ствол", "Рикошет",
                "Степлер", "Промокашка", "Направляющая" };
            Assert.That(profile.Parts.Count, Is.EqualTo(ids.Length));
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.That(profile.Parts[i].Id, Is.EqualTo(ids[i]));
                Assert.That(TestText.Resolve(profile.Parts[i].Name), Is.EqualTo(names[i]));
                Assert.That(profile.Parts[i].GoldCost, Is.EqualTo(prices[i]));
                Assert.That(profile.IsUnlocked(ids[i]), Is.EqualTo(i < 5));
                Assert.That(TestText.Resolve(profile.Parts[i].Description), Is.Not.Empty);
            }
        }

        [Test]
        public void ConfiguredUnlockPricesOverrideOnlyTheirDefinitions()
        {
            var balance = GameBalance.CreateDefault(); var rules = balance.Construction;
            rules.HeavyUnlockGold = 7; rules.FeedUnlockGold = 9; rules.TrackingUnlockGold = 11;
            rules.FanUnlockGold = 13; rules.RicochetUnlockGold = 15; rules.StaplerUnlockGold = 17;
            rules.BlotterUnlockGold = 19; rules.RailUnlockGold = 21;
            var profile = new WorkshopProfile(); var configured = profile.GetParts(balance);
            int[] prices = { 0, 0, 0, 0, 0, 7, 9, 11, 13, 15, 17, 19, 21 };
            for (int i = 0; i < prices.Length; i++)
            {
                Assert.That(configured[i].Id, Is.EqualTo(profile.Parts[i].Id));
                Assert.That(configured[i].Name, Is.EqualTo(profile.Parts[i].Name));
                Assert.That(configured[i].Description, Is.EqualTo(profile.Parts[i].Description));
                Assert.That(configured[i].GoldCost, Is.EqualTo(prices[i]));
                Assert.That(profile.IsUnlocked(configured[i].Id), Is.EqualTo(i < 5));
            }
            Assert.That(profile.Parts[5].GoldCost, Is.EqualTo(90));
            Assert.That(rules.UnlockGold("unknown.future"), Is.Zero);
        }

        [Test]
        public void LegacyToolAndAssemblyKeepObservedMarkerPricesAndPayment()
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000;
            var session = new GameSession(balance, starterDefenses: false);
            var blueprint = new TowerBlueprint("marker-gun", "Marker gun", ChassisKind.Standard, StructureKind.Gun);
            var legacyCell = new[] { new Cell(4, 4) }; var assemblyCell = new[] { new Cell(5, 4) };
            // Unity 6000.5.7f1 Mono baseline: both paths charge129; keep each path's operation types unchanged.
            Assert.That(session.QuoteBuild(legacyCell, StructureKind.Gun, MaterialTier.Marker).Cost, Is.EqualTo(129));
            Assert.That(session.QuoteBlueprint(assemblyCell, blueprint, MaterialTier.Marker, InkColor.None).Cost, Is.EqualTo(129));
            Assert.That(TowerCatalog.Cost(blueprint, balance, MaterialTier.Marker), Is.EqualTo(129));
            Assert.That(session.TryBuild(legacyCell, StructureKind.Gun, MaterialTier.Marker, out string reason), Is.True, reason);
            Assert.That(session.Ink, Is.EqualTo(871));
            Assert.That(session.TryBuildBlueprint(assemblyCell, blueprint, MaterialTier.Marker, InkColor.None, out reason), Is.True, reason);
            Assert.That(session.Ink, Is.EqualTo(742));
        }

        [Test]
        public void SerializedEnumsKeepTheirOriginalNumericContracts()
        {
            AssertDense<MaterialTier>("Pencil", "Ink", "Marker");
            AssertDense<StructureKind>("Wall", "Gun", "Piercer", "Mortar", "Spikes", "Fan", "Ricochet", "Stapler", "Blotter", "TarPit", "RepairPost", "Generator");
            AssertDense<EnemyKind>("Doodle", "Runner", "Gnawer", "Spitter", "Brute", "Bulwark");
            AssertDense<DrawingTier>("Sketch", "Outlined", "Hatched");
            AssertDense<InkColor>("None", "Red", "Blue", "Green");
            AssertDense<RunPhase>("Planning", "Assault", "Intermission", "Victory", "Defeat");
            AssertDense<TargetPriority>("Nearest", "Strongest", "Siege");
            AssertDense<RunUpgrade>("RapidFire", "DeepInk", "Salvage");
            AssertDense<GameBalancePreset>("Historical", "Target");
            AssertDense<ChassisKind>("Standard", "Heavy");
            AssertDense<TrialScenario>("Crowd", "Fast", "Armored", "Blots");
            AssertDense<DrawingPartKey>("Base", "Mount0", "Mount1", "Armor", "Feed", "Tracking", "Decoration", "Rail");
            Assert.That((int)DrawingOrigin.LegacyStandard, Is.EqualTo(1));
            Assert.That((int)DrawingOrigin.PlayerDrawing, Is.EqualTo(2));
            Assert.That((int)TowerModules.None, Is.Zero);
            Assert.That((int)TowerModules.Armor, Is.EqualTo(1));
            Assert.That((int)TowerModules.Feed, Is.EqualTo(2));
            Assert.That((int)TowerModules.Tracking, Is.EqualTo(4));
            Assert.That((int)TowerModules.Rail, Is.EqualTo(8));
        }

        [Test]
        public void PartDefinitionsProvideCanonicalIdsLabelsAndExplicitRolesWithoutFallbacks()
        {
            StructureKind[] mounts = { StructureKind.Gun, StructureKind.Piercer, StructureKind.Mortar, StructureKind.Fan,
                StructureKind.Ricochet, StructureKind.Stapler, StructureKind.Blotter };
            string[] ids = { "weapon.gun", "weapon.piercer", "weapon.mortar", "weapon.fan", "weapon.ricochet", "weapon.stapler", "weapon.blotter" };
            string[] labels = { "ПУЛЕМЁТ", "ПРОБОЙ", "МОРТИРА", "ВЕЕР", "РИКОШЕТ", "СКРЕПКИ", "ПРОМОКАШКА" };
            for (int i = 0; i < mounts.Length; i++)
            {
                Assert.That(PartCatalog.TryGetMount(mounts[i], out var part), Is.True);
                Assert.That(part.Id, Is.EqualTo(ids[i])); Assert.That(TestText.Resolve(part.ButtonLabel), Is.EqualTo(labels[i]));
                Assert.That(part.IsCombatMount, Is.EqualTo(i < 6));
                Assert.That(PartCatalog.TryGet(ids[i], out var byId), Is.True);
                Assert.That(byId, Is.SameAs(part));
            }
            Assert.That(PartCatalog.TryGetModule(TowerModules.Rail, out var rail), Is.True);
            Assert.That(TestText.Resolve(rail.ButtonLabel), Is.EqualTo("НАПРАВЛ.")); Assert.That(TestText.Resolve(rail.UnlockLabel), Is.EqualTo("НАПРАВЛЯЮЩАЯ"));
            Assert.That(PartCatalog.TryGet("unknown.future", out _), Is.False);
            Assert.That(PartCatalog.TryGetMount(StructureKind.Wall, out _), Is.False);
            Assert.That(PartCatalog.TryGetMount((StructureKind)999, out _), Is.False);
            Assert.That(PartCatalog.GetMountId((StructureKind)999), Is.Null);
            Assert.That(PartCatalog.TryGetModule(TowerModules.None, out _), Is.False);
            Assert.That(PartCatalog.TryGetModule(TowerModules.Armor | TowerModules.Feed, out _), Is.False);
            Assert.That(PartCatalog.TryGetChassis((ChassisKind)999, out _), Is.False);
            Assert.That(PartCatalog.IsHistorical("module.tracking"), Is.True);
            Assert.That(PartCatalog.IsHistorical("weapon.fan"), Is.False);
        }

        static void AssertDense<T>(params string[] names) where T : struct
        {
            Assert.That(Enum.GetNames(typeof(T)), Is.EqualTo(names));
            for (int i = 0; i < names.Length; i++)
                Assert.That(Convert.ToInt32(Enum.Parse(typeof(T), names[i])), Is.EqualTo(i));
        }
    }
}
