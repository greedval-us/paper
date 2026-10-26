using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class AssemblyPreviewMetricsTests
    {
        [TestCase(MaterialTier.Pencil)] [TestCase(MaterialTier.Ink)] [TestCase(MaterialTier.Marker)]
        public void ValidRecipesUseTheSameNumbersAndMaterialAsProductionResolution(MaterialTier material)
        {
            var catalog = AssemblyResolverFixture.Catalog(); var balance = GameBalance.CreateDefault();
            foreach (var entry in catalog.Starters)
            {
                var resolved = AssemblyResolver.Resolve(entry.Snapshot.Recipe, catalog, balance, material);
                var preview = AssemblyResolver.PreviewMetrics(entry.Snapshot.Recipe, catalog, balance, material);
                if (!resolved.Valid) { Assert.That(preview, Is.Null); continue; }
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.Cost, Is.EqualTo(resolved.Value.Cost));
                Assert.That(preview.Load, Is.EqualTo(resolved.Value.Load));
                Assert.That(preview.Capacity, Is.EqualTo(resolved.Value.Capacity));
                Assert.That(preview.Health, Is.EqualTo(resolved.Value.Health));
                Assert.That(preview.BuildSeconds, Is.EqualTo(resolved.Value.BuildSeconds));
                Assert.That(preview.Overloaded, Is.False);
            }
        }

        [TestCase(MaterialTier.Pencil)] [TestCase(MaterialTier.Ink)] [TestCase(MaterialTier.Marker)]
        public void OverloadedTowerKeepsActualLoadCapacityAndCostWithoutPublishingAResolvedEntry(MaterialTier material)
        {
            var input = AssemblyResolverFixture.Input(); var balance = GameBalance.CreateDefault(); var recipe = AssemblyResolverFixture.Tower();
            var expected = AssemblyResolverFixture.Resolve(recipe, balance: balance, material: material);
            AssemblyResolverFixture.Set(input, "frame.standard", AssemblyStat.Capacity, ModifierOperation.Set, 2);
            var catalog = AssemblyResolverFixture.Modified(input);
            var resolved = AssemblyResolver.Resolve(recipe, catalog, balance, material);
            Assert.That(resolved.Value, Is.Null); Assert.That(resolved.Diagnostics.Any(d => d.Code == "assembly.load.exceeded"), Is.True);
            var preview = AssemblyResolver.PreviewMetrics(recipe, catalog, balance, material);
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.Load, Is.EqualTo(4)); Assert.That(preview.Capacity, Is.EqualTo(2)); Assert.That(preview.Overloaded, Is.True);
            Assert.That(preview.Cost, Is.EqualTo(expected.Cost)); Assert.That(preview.Health, Is.EqualTo(expected.Health));
            Assert.That(preview.BuildSeconds, Is.EqualTo(expected.BuildSeconds));
            Assert.That((object)preview, Is.Not.InstanceOf<ResolvedEntry>());
            Assert.That(AssemblyResolver.Resolve(recipe, catalog, balance, material).Value, Is.Null, "Preview cannot turn an overloaded recipe into a buildable object.");
        }

        [Test] public void OverloadedSupportUsesTheSameFourPartCalculation()
        {
            var input = AssemblyResolverFixture.Input(); var recipe = AssemblyResolverFixture.Catalog().Starters.First(e =>
                e.Kind == AlbumEntryKind.Structure && e.Snapshot.Recipe.Structure.Family == AssemblyStructureFamily.Wall).Snapshot.Recipe;
            AssemblyResolverFixture.Set(input, "site.wall", AssemblyStat.Capacity, ModifierOperation.Set, 1);
            AssemblyResolverFixture.Set(input, "function.wall", AssemblyStat.Load, ModifierOperation.Add, 3);
            var catalog = AssemblyResolverFixture.Modified(input); var balance = GameBalance.CreateDefault();
            Assert.That(AssemblyResolver.Resolve(recipe, catalog, balance, MaterialTier.Pencil).Value, Is.Null);
            var preview = AssemblyResolver.PreviewMetrics(recipe, catalog, balance, MaterialTier.Pencil);
            Assert.That(preview, Is.Not.Null); Assert.That(preview.Load, Is.EqualTo(3)); Assert.That(preview.Capacity, Is.EqualTo(1));
            Assert.That(preview.Cost, Is.EqualTo(16)); Assert.That(preview.Health, Is.EqualTo(200));
            Assert.That(preview.BuildSeconds, Is.EqualTo(.35f).Within(.0001));
        }

        [TestCase("incompatible")] [TestCase("required")] [TestCase("revision")] [TestCase("zero-interval")] [TestCase("overflow")]
        public void OtherInvalidRecipesDoNotInventPartialMetrics(string invalid)
        {
            var input = AssemblyResolverFixture.Input();
            var weapon = invalid == "incompatible" ? AssemblyResolverFixture.Weapon(head: "mortar")
                : invalid == "required" ? AssemblyResolverFixture.Weapon(magazine: "drum", trigger: "burst")
                : invalid == "revision" ? new WeaponRecipe(new PartRef("core.gun", 99), new PartRef("head.gun", 1), new PartRef("sight.iron", 1), new PartRef("magazine.standard", 1), new PartRef("trigger.standard", 1))
                : AssemblyResolverFixture.Weapon();
            if (invalid == "zero-interval") AssemblyResolverFixture.Set(input, "core.gun", AssemblyStat.Interval, ModifierOperation.Set, 0);
            if (invalid == "overflow")
            {
                AssemblyResolverFixture.Set(input, "core.gun", AssemblyStat.Damage, ModifierOperation.Set, float.MaxValue);
                AssemblyResolverFixture.Set(input, "head.gun", AssemblyStat.Damage, ModifierOperation.Multiply, float.MaxValue);
            }
            Assert.That(AssemblyResolver.PreviewMetrics(AssemblyResolverFixture.Tower(weapon), AssemblyResolverFixture.Modified(input), GameBalance.CreateDefault(), MaterialTier.Pencil), Is.Null);
        }

        [Test] public void MissingInputAndUnsupportedGeneratorMaterialReturnNoMetrics()
        {
            var catalog = AssemblyResolverFixture.Catalog(); var balance = GameBalance.CreateDefault(); var recipe = AssemblyResolverFixture.Tower();
            Assert.That(AssemblyResolver.PreviewMetrics(null, catalog, balance, MaterialTier.Pencil), Is.Null);
            Assert.That(AssemblyResolver.PreviewMetrics(recipe, null, balance, MaterialTier.Pencil), Is.Null);
            Assert.That(AssemblyResolver.PreviewMetrics(recipe, catalog, null, MaterialTier.Pencil), Is.Null);
            var generator = catalog.Starters.First(e => e.Kind == AlbumEntryKind.Structure && e.Snapshot.Recipe.Structure.Family == AssemblyStructureFamily.Generator);
            Assert.That(AssemblyResolver.PreviewMetrics(generator.Snapshot.Recipe, catalog, balance, MaterialTier.Ink), Is.Null);
        }
    }
}
