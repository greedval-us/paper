using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class AssemblySidegradeTests
    {
        static ResolvedEntry Tower(string core = "gun", string head = null, string sight = "iron", string magazine = "standard", string trigger = "standard", FoundationRecipe foundation = null, bool dual = false)
        {
            var weapon = AssemblyResolverFixture.Weapon(core, head, magazine, trigger, sight);
            return AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(weapon, dual ? weapon : null, foundation));
        }

        [TestCase("outpost", 36, 72, 8)] [TestCase("siege", 56, 110, 12)]
        public void NewSingleMountFramesTradePriceDurabilityAndCapacityAndRejectASecondMount(string frame, int cost, float health, int capacity)
        {
            var foundation = AssemblyResolverFixture.Foundation(frame: frame);
            var entry = Tower(foundation: foundation);
            Assert.That(entry.Cost, Is.EqualTo(cost)); Assert.That(entry.Health, Is.EqualTo(health));
            Assert.That(entry.Capacity, Is.EqualTo(capacity));
            var invalid = AssemblyResolver.Resolve(AssemblyResolverFixture.Tower(second: AssemblyResolverFixture.Weapon(), foundation: foundation), AssemblyResolverFixture.Catalog(), GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(invalid.Valid, Is.False);
            Assert.That(invalid.Diagnostics.Select(d => d.Code), Does.Contain("assembly.mount.unavailable"));
        }

        [Test]
        public void RibbedHullAddsHealthButCostsInkLoadAndCapacity()
        {
            var standard = Tower(); var ribbed = Tower(foundation: AssemblyResolverFixture.Foundation(hull: "ribbed"));
            Assert.That(ribbed.Health, Is.EqualTo(63.25f).Within(.001f));
            Assert.That(ribbed.Health, Is.GreaterThan(standard.Health)); Assert.That(ribbed.Cost, Is.GreaterThan(standard.Cost));
            Assert.That(ribbed.Load, Is.GreaterThan(standard.Load)); Assert.That(ribbed.Capacity, Is.LessThan(standard.Capacity));
        }

        [Test]
        public void DeliberateTurnSustainsMoreDualFeedButAcquiresTargetsSlower()
        {
            var standard = Tower(dual: true);
            var deliberate = Tower(dual: true, foundation: AssemblyResolverFixture.Foundation(turn: "deliberate"));
            Assert.That(deliberate.FeedMultiplier, Is.EqualTo(.92f).Within(.0001f));
            Assert.That(deliberate.Dps, Is.GreaterThan(standard.Dps));
            Assert.That(deliberate.Weapons[0].AcquisitionSeconds, Is.GreaterThan(standard.Weapons[0].AcquisitionSeconds));
            Assert.That(deliberate.Cost, Is.GreaterThan(standard.Cost)); Assert.That(deliberate.Load, Is.GreaterThan(standard.Load));
        }

        [TestCase("fan", "fan.focus")] [TestCase("ricochet", "ricochet.long")]
        public void LongerFamilyHeadsGiveReachForSlowerCyclesAndHigherLoad(string core, string head)
        {
            var standard = Tower(core); var alternative = Tower(core, head);
            Assert.That(alternative.Weapons[0].Range, Is.GreaterThan(standard.Weapons[0].Range));
            Assert.That(alternative.Weapons[0].ReloadSeconds, Is.GreaterThan(standard.Weapons[0].ReloadSeconds));
            Assert.That(alternative.Dps, Is.LessThan(standard.Dps)); Assert.That(alternative.Load, Is.GreaterThan(standard.Load));
        }

        [Test]
        public void AbsorbingHeadCleansAWiderAreaButSlowerAndCloserWithoutBecomingAWeapon()
        {
            var standard = Tower("blotter").Weapons[0]; var absorbing = Tower("blotter", "absorb").Weapons[0];
            Assert.That(absorbing.Radius, Is.EqualTo(3)); Assert.That(absorbing.Damage, Is.Zero);
            Assert.That(absorbing.Radius, Is.GreaterThan(standard.Radius)); Assert.That(absorbing.Range, Is.LessThan(standard.Range));
            Assert.That(absorbing.ReloadSeconds, Is.GreaterThan(standard.ReloadSeconds));
        }

        [TestCase("snap", false)] [TestCase("survey", true)]
        public void SightAlternativesTradeAcquisitionAndReach(string sight, bool longerRange)
        {
            var standard = Tower().Weapons[0]; var alternative = Tower(sight: sight).Weapons[0];
            Assert.That(alternative.Range > standard.Range, Is.EqualTo(longerRange));
            Assert.That(alternative.AcquisitionSeconds > standard.AcquisitionSeconds, Is.EqualTo(longerRange));
            Assert.That(alternative.Damage, Is.EqualTo(standard.Damage));
        }

        [TestCase("quick", 3, false)] [TestCase("reserve", 12, true)]
        public void MagazineAlternativesChangeActualClipAndReloadWithSlowerShotSpacing(string magazine, int clip, bool longerReload)
        {
            var standard = Tower().Weapons[0]; var alternative = Tower(magazine: magazine).Weapons[0];
            Assert.That(alternative.MagazineSize, Is.EqualTo(clip));
            Assert.That(alternative.ReloadSeconds > standard.ReloadSeconds, Is.EqualTo(longerReload));
            Assert.That(alternative.Interval, Is.GreaterThan(standard.Interval));
        }

        [Test]
        public void EconomyTriggerActuallyDiscountsInkAndLoadButLosesDamageAndTempo()
        {
            var standard = Tower(); var economy = Tower(trigger: "economy");
            Assert.That(economy.Cost, Is.EqualTo(standard.Cost - 4)); Assert.That(economy.Load, Is.EqualTo(standard.Load - 1));
            Assert.That(economy.Dps, Is.LessThan(standard.Dps));
            Assert.That(economy.Weapons[0].Damage, Is.LessThan(standard.Weapons[0].Damage));
            Assert.That(economy.Weapons[0].ReloadSeconds, Is.GreaterThan(standard.Weapons[0].ReloadSeconds));
        }

        [TestCase("frame.outpost")] [TestCase("frame.siege")] [TestCase("hull.ribbed")] [TestCase("turn.deliberate")]
        [TestCase("head.fan.focus")] [TestCase("head.ricochet.long")] [TestCase("head.absorb")]
        [TestCase("sight.snap")] [TestCase("sight.survey")] [TestCase("magazine.quick")]
        [TestCase("magazine.reserve")] [TestCase("trigger.economy")]
        public void EveryNewPartHasAnActuallyCertifiedAssembly(string id)
        {
            ResolvedEntry entry;
            switch (id)
            {
                case "frame.outpost": entry = Tower(foundation: AssemblyResolverFixture.Foundation(frame: "outpost")); break;
                case "frame.siege": entry = Tower("mortar", foundation: AssemblyResolverFixture.Foundation(frame: "siege")); break;
                case "hull.ribbed": entry = Tower(foundation: AssemblyResolverFixture.Foundation(frame: "precision", hull: "ribbed")); break;
                case "turn.deliberate": entry = Tower(dual: true, foundation: AssemblyResolverFixture.Foundation(turn: "deliberate")); break;
                case "head.fan.focus": entry = Tower("fan", "fan.focus"); break;
                case "head.ricochet.long": entry = Tower("ricochet", "ricochet.long"); break;
                case "head.absorb": entry = Tower("blotter", "absorb"); break;
                case "sight.snap": entry = Tower(sight: "snap"); break;
                case "sight.survey": entry = Tower(sight: "survey"); break;
                case "magazine.quick": entry = Tower(magazine: "quick"); break;
                case "magazine.reserve": entry = Tower(magazine: "reserve"); break;
                default: entry = Tower(trigger: "economy"); break;
            }
            Assert.That(entry.Snapshot.Recipe.Parts.Any(p => p.Id == id), Is.True);
            Assert.That(AssemblyFitRules.Validate(entry.Snapshot.Recipe, AssemblyFitResources.Load()), Is.Empty, id);
        }

        [TestCase(false, 50, 14, 4)] [TestCase(true, 62, 21, 3)]
        public void QuickGeneratorPaysIntegerIncomeAndHasTheStatedPayback(bool economy, int cost, int income, int waves)
        {
            var recipe = new AssemblyRecipe(new StructureRecipe(AssemblyStructureFamily.Generator,
                AssemblyResolverFixture.Part("site.generator"), AssemblyResolverFixture.Part("function.generator.quick"),
                AssemblyResolverFixture.Part("shell.standard"), AssemblyResolverFixture.Part(economy ? "utility.economy" : "utility.none")));
            var entry = AssemblyResolverFixture.Resolve(recipe);
            Assert.That(entry.Cost, Is.EqualTo(cost)); Assert.That(entry.Support.IncomePerWave, Is.EqualTo(income));
            Assert.That(entry.Support.PaybackWaves(cost), Is.EqualTo(waves));
            Assert.That(entry.Support.MaximumInstances, Is.EqualTo(2)); Assert.That(entry.Support.RefundFraction, Is.Zero);
        }

        [Test]
        public void EveryCampaignFirstClearPartExistsAndAllNewSidegradesHaveOneRewardHome()
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var granted = CampaignCatalog.Default.Missions.SelectMany(m => m.UnlockPartIds).ToArray();
            foreach (var id in granted) Assert.That(catalog.TryGet(id, 1, out _), Is.True, id);
            foreach (string id in new[] { "frame.outpost", "frame.siege", "hull.ribbed", "turn.deliberate", "head.fan.focus", "head.ricochet.long", "head.absorb", "sight.snap", "sight.survey", "magazine.quick", "magazine.reserve", "trigger.economy" })
                Assert.That(granted.Count(p => p == id), Is.EqualTo(1), id);
        }
    }
}
