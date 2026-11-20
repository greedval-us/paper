using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Editor;

namespace PaperDefense.Tests
{
    public sealed class StructureVariantCatalogTests
    {
        static ComponentCatalog Catalog() => AlbumBalanceImport.ReadValidated(File.ReadAllText(AlbumBalanceImport.SourcePath)).CreateSnapshot();
        static AssemblyRecipe Replace(ComponentCatalog catalog, AssemblyStructureFamily family, string id)
        {
            Assert.That(catalog.TryGet(id, 1, out var selected), Is.True, "Missing replacement " + id);
            var source = catalog.Templates.Single(entry => entry.Kind == AlbumEntryKind.Structure && entry.Snapshot.Recipe.Structure.Family == family).Snapshot.Recipe.Structure;
            var parts = source.Parts.Select(reference => {
                catalog.TryGet(reference.Id, reference.Revision, out var part);
                return part.Category == selected.Category ? new PartRef(id, 1) : reference;
            }).ToArray();
            return new AssemblyRecipe(new StructureRecipe(family, parts[0], parts[1], parts[2], parts[3]));
        }
        static ResolvedEntry Resolve(ComponentCatalog catalog, AssemblyStructureFamily family, string id)
        {
            var resolution = AssemblyResolver.Resolve(Replace(catalog, family, id), catalog, GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(resolution.Valid, Is.True, string.Join("; ", resolution.Diagnostics.Select(issue => issue.Code)));
            return resolution.Value;
        }

        [TestCase(AssemblyStructureFamily.Wall)] [TestCase(AssemblyStructureFamily.Slow)]
        [TestCase(AssemblyStructureFamily.Repair)] [TestCase(AssemblyStructureFamily.Generator)]
        [TestCase(AssemblyStructureFamily.Spikes)] [TestCase(AssemblyStructureFamily.Beacon)] [TestCase(AssemblyStructureFamily.Prism)]
        public void EveryFamilyEachCategoryHasAnInstallableAlternative(AssemblyStructureFamily family)
        {
            var catalog = Catalog();
            foreach (var category in new[] { ComponentCategory.Site, ComponentCategory.Function, ComponentCategory.Shell, ComponentCategory.Utility })
            {
                var choices = catalog.Definitions.Where(part => part.Category == category && (part.StructureFamilies & family) != 0).ToArray();
                Assert.That(choices.Length, Is.GreaterThanOrEqualTo(2), family + "/" + category + " must be replaceable.");
                foreach (var choice in choices) Resolve(catalog, family, choice.Id);
            }
        }

        [TestCase(AssemblyStructureFamily.Wall, "site.wall.compact", 14, 150f, .3f)]
        [TestCase(AssemblyStructureFamily.Slow, "site.slow.compact", 20, 41.25f, .7f)]
        [TestCase(AssemblyStructureFamily.Repair, "site.repair.compact", 60, 52.5f, 1.75f)]
        [TestCase(AssemblyStructureFamily.Generator, "site.generator.compact", 55, 60f, 1.95f)]
        [TestCase(AssemblyStructureFamily.Spikes, "site.spikes.compact", 29, 32f, .75f)]
        [TestCase(AssemblyStructureFamily.Beacon, "site.beacon.compact", 44, 48f, 1.3f)]
        public void CompactSitesTradeHealthAndCapacityForLowerCostAndFasterConstruction(AssemblyStructureFamily family, string id, int cost, float health, float build)
        {
            var resolved = Resolve(Catalog(), family, id);
            Assert.That(resolved.Cost, Is.EqualTo(cost)); Assert.That(resolved.Health, Is.EqualTo(health).Within(.0001f));
            Assert.That(resolved.Capacity, Is.EqualTo(5)); Assert.That(resolved.BuildSeconds, Is.EqualTo(build).Within(.0001f));
        }

        [Test] public void BracedWallFunctionBuysMoreHealthWithExtraCostWeightAndBuildTime()
        {
            var resolved = Resolve(Catalog(), AssemblyStructureFamily.Wall, "function.wall.braced");
            Assert.That(resolved.Cost, Is.EqualTo(22)); Assert.That(resolved.Health, Is.EqualTo(250));
            Assert.That(resolved.Load, Is.EqualTo(3)); Assert.That(resolved.BuildSeconds, Is.EqualTo(.55f).Within(.0001f));
        }
        [Test] public void DilutedSlowFunctionCoversMoreGroundWithWeakerControl()
        {
            var resolved = Resolve(Catalog(), AssemblyStructureFamily.Slow, "function.slow.diluted");
            Assert.That(resolved.Cost, Is.EqualTo(22)); Assert.That(resolved.Support.Radius, Is.EqualTo(1.3f).Within(.0001f));
            Assert.That(resolved.Support.SlowSpeedMultiplier, Is.EqualTo(.75f)); Assert.That(resolved.Load, Is.EqualTo(2));
        }
        [Test] public void FocusedRepairFunctionRepairsFasterWithinASmallerRadius()
        {
            var resolved = Resolve(Catalog(), AssemblyStructureFamily.Repair, "function.repair.focused");
            Assert.That(resolved.Cost, Is.EqualTo(75)); Assert.That(resolved.Support.RepairPerSecond, Is.EqualTo(9));
            Assert.That(resolved.Support.Radius, Is.EqualTo(3.5f)); Assert.That(resolved.Load, Is.EqualTo(4));
            Assert.That(resolved.BuildSeconds, Is.EqualTo(2.2f).Within(.0001f));
        }
        [Test] public void QuickGeneratorTradesIncomeForLowerPriceAndBuildTime()
        {
            var resolved = Resolve(Catalog(), AssemblyStructureFamily.Generator, "function.generator.quick");
            Assert.That(resolved.Cost, Is.EqualTo(50)); Assert.That(resolved.Support.IncomePerWave, Is.EqualTo(14));
            Assert.That(resolved.BuildSeconds, Is.EqualTo(1.6f).Within(.0001f));
            Assert.That(resolved.Support.MaximumInstances, Is.EqualTo(2)); Assert.That(resolved.Support.RefundFraction, Is.Zero);
            var catalog = Catalog(); var quick = Replace(catalog, AssemblyStructureFamily.Generator, "function.generator.quick").Structure;
            var economic = new AssemblyRecipe(new StructureRecipe(quick.Family, quick.Site, quick.Function, quick.Shell, new PartRef("utility.economy", 1)));
            var combined = AssemblyResolver.Resolve(economic, catalog, GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(combined.Valid, Is.True, "The economy utility must retain integer income on the quick pump.");
            Assert.That(combined.Value.Support.IncomePerWave, Is.EqualTo(21));
        }
        [Test] public void ConcentratedSlowUtilityProvidesStrongerControlInsideASmallerArea()
        {
            var catalog = Catalog(); var resolved = Resolve(catalog, AssemblyStructureFamily.Slow, "utility.concentrate");
            Assert.That(resolved.Cost, Is.EqualTo(30)); Assert.That(resolved.Load, Is.EqualTo(3));
            Assert.That(resolved.Support.SlowSpeedMultiplier, Is.EqualTo(.5f)); Assert.That(resolved.Support.Radius, Is.EqualTo(.8f).Within(.0001f));
            catalog.TryGet("utility.concentrate", 1, out var utility);
            Assert.That(utility.StructureFamilies, Is.EqualTo(AssemblyStructureFamily.Slow));
        }

        [TestCase("site.wall.compact", 2, 35)] [TestCase("site.slow.compact", 2, 35)]
        [TestCase("site.repair.compact", 2, 35)] [TestCase("site.generator.compact", 2, 35)]
        [TestCase("function.wall.braced", 3, 55)] [TestCase("function.slow.diluted", 3, 55)]
        [TestCase("function.repair.focused", 3, 60)] [TestCase("function.generator.quick", 3, 60)]
        [TestCase("utility.concentrate", 3, 50)]
        public void SidegradesRequireTheirLevelAndGoldAndAreUnlockedOnlyOnce(string id, int level, int gold)
        {
            var catalog = Catalog(); Assert.That(catalog.TryGet(id, 1, out var part), Is.True, id);
            Assert.That(part.RequiredLevel, Is.EqualTo(level)); Assert.That(part.UnlockGold, Is.EqualTo(gold));
            WorkshopProfile Profile(int available, int xp)
            {
                var data = WorkshopProfile.CreateNew(catalog).ToData(); data.Gold = available; data.TotalXp = xp;
                return WorkshopProfile.FromData(data, catalog);
            }
            var lowLevel = Profile(gold, 0);
            Assert.That(lowLevel.TryPurchaseAssemblyPart(part, out var code), Is.False); Assert.That(code, Is.EqualTo("profile.part.level"));
            Assert.That(lowLevel.Gold, Is.EqualTo(gold)); Assert.That(lowLevel.IsUnlocked(id), Is.False);
            var poor = Profile(gold - 1, 1330);
            Assert.That(poor.TryPurchaseAssemblyPart(part, out code), Is.False); Assert.That(code, Is.EqualTo("profile.part.gold"));
            Assert.That(poor.Gold, Is.EqualTo(gold - 1)); Assert.That(poor.IsUnlocked(id), Is.False);
            var eligible = Profile(gold, level == 2 ? 100 : 230);
            Assert.That(eligible.TryPurchaseAssemblyPart(part, out code), Is.True, code);
            Assert.That(eligible.Gold, Is.Zero); Assert.That(eligible.IsUnlocked(id), Is.True);
            Assert.That(eligible.TryPurchaseAssemblyPart(part, out code), Is.True, code); Assert.That(eligible.Gold, Is.Zero);
        }
    }
}
