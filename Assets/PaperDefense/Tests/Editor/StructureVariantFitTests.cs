using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class StructureVariantFitTests
    {
        [TestCase("site.wall.compact", "site.wall")]
        [TestCase("site.slow.compact", "site.slow")]
        [TestCase("site.repair.compact", "site.repair")]
        [TestCase("site.generator.compact", "site.generator")]
        [TestCase("function.wall.braced", "function.wall")]
        [TestCase("function.slow.diluted", "function.slow")]
        [TestCase("function.repair.focused", "function.repair")]
        [TestCase("function.generator.quick", "function.generator")]
        [TestCase("utility.concentrate", "utility.none")]
        public void VariantsKeepTheirBaseMechanicalSocketMetadata(string variantId, string baseId)
        {
            var fits = AssemblyFitResources.Load();
            Assert.That(fits.TryGetPart(variantId, 1, out var variant), Is.True, variantId);
            Assert.That(fits.TryGetPart(baseId, 1, out var original), Is.True, baseId);
            Assert.That(variant.Category, Is.EqualTo(original.Category));
            Assert.That(variant.AttachType, Is.EqualTo(original.AttachType));
            Assert.That(variant.ExpectedSocketType, Is.EqualTo(original.ExpectedSocketType));
            Assert.That(variant.SocketSchemaVersion, Is.EqualTo(original.SocketSchemaVersion));
        }

        [TestCase(AssemblyStructureFamily.Wall, "wall", "braced")]
        [TestCase(AssemblyStructureFamily.Slow, "slow", "diluted")]
        [TestCase(AssemblyStructureFamily.Repair, "repair", "focused")]
        [TestCase(AssemblyStructureFamily.Generator, "generator", "quick")]
        [TestCase(AssemblyStructureFamily.Spikes, "spikes", "barbed")]
        [TestCase(AssemblyStructureFamily.Beacon, "beacon", "focused")]
        [TestCase(AssemblyStructureFamily.Prism, "prism", "focused")]
        public void CompactSitesAndAlternativeFunctionsAreCertifiedOnlyForTheirOwnFamily(
            AssemblyStructureFamily family, string name, string functionSuffix)
        {
            var fits = AssemblyFitResources.Load();
            var site = P("site." + name + ".compact");
            var function = P("function." + name + "." + functionSuffix);
            Assert.That(AssemblyFitRules.Validate(Recipe(family, site, function, P("utility.none")), fits), Is.Empty);
            foreach (var other in new[] { AssemblyStructureFamily.Wall, AssemblyStructureFamily.Slow,
                AssemblyStructureFamily.Repair, AssemblyStructureFamily.Generator,
                AssemblyStructureFamily.Spikes, AssemblyStructureFamily.Beacon, AssemblyStructureFamily.Prism }.Where(value => value != family))
            {
                Assert.That(AssemblyFitRules.Validate(Recipe(other, site, function, P("utility.none")), fits)
                    .Any(issue => issue.Code == "assembly.fit.coverage"), Is.True, other.ToString());
            }
        }

        [TestCase(AssemblyStructureFamily.Wall, "wall", false)]
        [TestCase(AssemblyStructureFamily.Slow, "slow", true)]
        [TestCase(AssemblyStructureFamily.Repair, "repair", false)]
        [TestCase(AssemblyStructureFamily.Generator, "generator", false)]
        public void ConcentrateUtilityFitsOnlyTheSlowStructure(AssemblyStructureFamily family, string name, bool expected)
        {
            var issues = AssemblyFitRules.Validate(Recipe(family, P("site." + name),
                P("function." + name), P("utility.concentrate")), AssemblyFitResources.Load());
            Assert.That(issues.Count == 0, Is.EqualTo(expected), family.ToString());
        }

        static AssemblyRecipe Recipe(AssemblyStructureFamily family, PartRef site, PartRef function, PartRef utility)
            => new AssemblyRecipe(new StructureRecipe(family, site, function, P("shell.standard"), utility));
        static PartRef P(string id) => new PartRef(id, 1);
    }
}
