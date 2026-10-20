using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    internal static class AssemblyResolverFixture
    {
        internal static AssemblyCatalogInput Input() => JsonUtility.FromJson<AssemblyCatalogInput>(JsonUtility.ToJson(
            AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").catalog));
        internal static ComponentCatalog Catalog() => Input().CreateSnapshot();
        internal static PartRef Part(string id, int revision = 1) => new PartRef(id, revision);
        internal static WeaponRecipe Weapon(string core = "gun", string head = null, string magazine = "standard", string trigger = "standard", string sight = "iron")
            => new WeaponRecipe(Part("core." + core), Part("head." + (head ?? core)), Part("sight." + sight), Part("magazine." + magazine), Part("trigger." + trigger));
        internal static FoundationRecipe Foundation(string frame = "standard", string hull = "standard", string armor = "none", string feed = "standard", string turn = "standard")
            => new FoundationRecipe(Part("frame." + frame), Part("hull." + hull), Part("armor." + armor), Part("feed." + feed), Part("turn." + turn));
        internal static AssemblyRecipe Tower(WeaponRecipe first = null, WeaponRecipe second = null, FoundationRecipe foundation = null)
        {
            var mounts = new[] { new WeaponMountRecipe(0, first ?? Weapon()) }.ToList();
            if (second != null) mounts.Add(new WeaponMountRecipe(1, second));
            return new AssemblyRecipe(new TowerRecipe(foundation ?? Foundation(), mounts));
        }
        internal static void Set(AssemblyCatalogInput input, string id, AssemblyStat stat, ModifierOperation operation, float value)
        {
            var part = input.Definitions.Single(d => d.Id == id);
            var existing = part.Modifiers.FirstOrDefault(m => m.Stat == stat && m.Operation == operation);
            if (existing != null) existing.Value = value;
            else part.Modifiers = part.Modifiers.Concat(new[] { new ComponentModifierInput { Stat = stat, Operation = operation, Value = value } }).ToArray();
        }
        internal static ComponentCatalog Modified(AssemblyCatalogInput input)
            => new ComponentCatalog(input.BalanceRevision, input.Definitions.Select(d => d.CreateSnapshot()));
        internal static ResolvedEntry Resolve(AssemblyRecipe recipe, ComponentCatalog catalog = null, GameBalance balance = null, MaterialTier material = MaterialTier.Pencil)
        {
            var result = AssemblyResolver.Resolve(recipe, catalog ?? Catalog(), balance ?? GameBalance.CreateDefault(), material);
            Assert.That(result.Valid, Is.True, string.Join(",", result.Diagnostics.Select(d => d.Code + ":" + d.Path)));
            Assert.That(result.Diagnostics, Is.Empty);
            return result.Value;
        }
    }

    public sealed class AssemblyResolverTests
    {
        static int FoundationQuote(FoundationRecipe foundation, ComponentCatalog catalog, GameBalance balance, MaterialTier material)
        {
            var method = typeof(AssemblyResolver).GetMethod("FoundationCost", new[] { typeof(FoundationRecipe), typeof(ComponentCatalog), typeof(GameBalance), typeof(MaterialTier) });
            Assert.That(method, Is.Not.Null, "The foundation quote must use the existing resolver cost fold instead of duplicating it in presentation.");
            return (int)method.Invoke(null, new object[] { foundation, catalog, balance, material });
        }

        [Test] public void FoundationQuoteExcludesEveryWeaponContribution()
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "core.gun", AssemblyStat.Cost, ModifierOperation.Add, 10000);
            var catalog = AssemblyResolverFixture.Modified(data);
            Assert.That(FoundationQuote(AssemblyResolverFixture.Foundation(), catalog, GameBalance.CreateDefault(), MaterialTier.Pencil), Is.EqualTo(16));
        }

        [Test] public void WeaponQuoteExcludesEveryFoundationContribution()
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "frame.standard", AssemblyStat.Cost, ModifierOperation.Add, 10000);
            AssemblyResolverFixture.Set(data, "frame.standard", AssemblyStat.Cost, ModifierOperation.Multiply, 9);
            Assert.That(AssemblyResolver.WeaponCost(AssemblyResolverFixture.Weapon(), AssemblyResolverFixture.Modified(data),
                GameBalance.CreateDefault(), MaterialTier.Pencil), Is.EqualTo(24));
        }

        [TestCase(MaterialTier.Pencil, 30)] [TestCase(MaterialTier.Ink, 54)]
        public void WeaponQuoteUsesTheSameMultipliersAndSingleRoundingAsTheSharedFold(MaterialTier material, int expected)
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "head.gun", AssemblyStat.Cost, ModifierOperation.Multiply, 1.25f);
            Assert.That(AssemblyResolver.WeaponCost(AssemblyResolverFixture.Weapon(), AssemblyResolverFixture.Modified(data),
                GameBalance.CreateDefault(), material), Is.EqualTo(expected));
        }

        [TestCase(MaterialTier.Pencil, 38)] [TestCase(MaterialTier.Ink, 68)]
        public void FoundationQuoteUsesSelectedPartsMultipliersAndTheSharedMaterialRounding(MaterialTier material, int expected)
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "frame.standard", AssemblyStat.Cost, ModifierOperation.Multiply, 1.25f);
            var catalog = AssemblyResolverFixture.Modified(data);
            Assert.That(FoundationQuote(AssemblyResolverFixture.Foundation(armor: "plate"), catalog, GameBalance.CreateDefault(), material), Is.EqualTo(expected));
        }

        [Test] public void StarterRecipes_MatchBalanceV1()
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var entries = catalog.Starters.Select(e => AssemblyResolverFixture.Resolve(e.Snapshot.Recipe, catalog)).ToArray();
            Assert.That(entries.Take(4).Select(e => e.Cost), Is.EqualTo(new[] { 40, 64, 60, 99 }));
            Assert.That(entries.Take(4).Select(e => e.Health), Is.All.EqualTo(55));
            Assert.That(entries.Take(4).Select(e => e.Load), Is.EqualTo(new[] { 4, 8, 6, 9 }));
            Assert.That(entries.Skip(4).Select(e => e.Cost), Is.EqualTo(new[] { 16, 22, 65, 60 }));
            Assert.That(entries.Skip(4).Select(e => e.Health), Is.EqualTo(new[] { 200, 55, 70, 80 }));
            Assert.That(entries.Take(4).Select(e => e.BuildSeconds), Is.EqualTo(new[] { 1.4f, 1.8f, 1.9f, 2.6f }).Within(.0001f));
            Assert.That(entries.Skip(4).Select(e => e.BuildSeconds), Is.EqualTo(new[] { .35f, .8f, 2f, 2.2f }).Within(.0001f));
        }

        [TestCase("gun", 40, 12, 8, .42f)]
        [TestCase("short", 36, 10.8f, 6.4f, .357f)]
        [TestCase("long", 52, 13.2f, 10, .504f)]
        public void GunHeads_ResolveWithoutSubtractingCreditTwice(string head, int cost, float damage, float range, float interval)
        {
            var result = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(head: head)));
            var weapon = result.Weapons.Single();
            Assert.That(result.Cost, Is.EqualTo(cost)); Assert.That(weapon.Damage, Is.EqualTo(damage).Within(.0001f));
            Assert.That(weapon.Range, Is.EqualTo(range).Within(.0001f)); Assert.That(weapon.Interval, Is.EqualTo(interval).Within(.0001f));
            Assert.That(weapon.ReloadSeconds, Is.EqualTo(interval).Within(.0001f)); Assert.That(weapon.MagazineSize, Is.EqualTo(6));
        }

        [TestCase("standard", "standard", 6, .42f, .42f, 2.52f)]
        [TestCase("standard", "burst", 3, .189f, .882f, 1.26f)]
        [TestCase("drum", "standard", 12, .357f, 1.26f, 5.187f)]
        public void MagazineAndTrigger_KeepBothTimesAndExactCycle(string magazine, string trigger, int size, float interval, float reload, float cycle)
        {
            var w = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(magazine: magazine, trigger: trigger))).Weapons.Single();
            Assert.That(w.MagazineSize, Is.EqualTo(size)); Assert.That(w.Interval, Is.EqualTo(interval).Within(.0001f));
            Assert.That(w.ReloadSeconds, Is.EqualTo(reload).Within(.0001f)); Assert.That(w.CycleSeconds, Is.EqualTo(cycle).Within(.0001f));
            Assert.That(w.AcquisitionSeconds, Is.EqualTo(.25f).Within(.0001f));
        }

        [Test] public void MixedWeapons_ShareFeedOnlyOnceAndReleaseSupplyWhenAMountIsIdle()
        {
            var result = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(), AssemblyResolverFixture.Weapon("piercer")));
            Assert.That(result.Load, Is.EqualTo(10)); Assert.That(result.FeedMultiplier, Is.EqualTo(.8f).Within(.0001f));
            Assert.That(result.FeedForActiveMounts(0), Is.Zero); Assert.That(result.FeedForActiveMounts(1), Is.EqualTo(1));
            Assert.That(result.FeedForActiveMounts(2), Is.EqualTo(.8f).Within(.0001f));
            Assert.That(result.Weapons[0].Interval, Is.EqualTo(.42f).Within(.0001f));
            Assert.That(result.Weapons[0].IntervalAtFeed(.8f), Is.EqualTo(.525f).Within(.0001f));
            Assert.That(result.Weapons[0].ReloadAtFeed(.8f), Is.EqualTo(.525f).Within(.0001f));
            Assert.That(result.Dps, Is.EqualTo(38.8571428f).Within(.0001f));
        }

        [Test] public void SingleRoundMagazine_UsesReloadWithoutAnExtraInterval()
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "core.piercer", AssemblyStat.Interval, ModifierOperation.Set, .25f);
            AssemblyResolverFixture.Set(data, "core.piercer", AssemblyStat.ReloadSeconds, ModifierOperation.Set, 2);
            var weapon = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon("piercer")), AssemblyResolverFixture.Modified(data)).Weapons[0];
            Assert.That(weapon.CycleSeconds, Is.EqualTo(2)); Assert.That(weapon.NominalDps, Is.EqualTo(10));
        }

        [Test] public void AdditionsPrecedeProducts_RegardlessOfDefinitionOrModifierOrder()
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "head.gun", AssemblyStat.Damage, ModifierOperation.Add, 4);
            AssemblyResolverFixture.Set(data, "head.gun", AssemblyStat.Damage, ModifierOperation.Multiply, 2);
            AssemblyResolverFixture.Set(data, "sight.iron", AssemblyStat.Damage, ModifierOperation.Add, 2);
            AssemblyResolverFixture.Set(data, "sight.iron", AssemblyStat.Damage, ModifierOperation.Multiply, 1.5f);
            var first = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(), AssemblyResolverFixture.Modified(data));
            Array.Reverse(data.Definitions); foreach (var d in data.Definitions) Array.Reverse(d.Modifiers);
            var second = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(), AssemblyResolverFixture.Modified(data));
            Assert.That(first.Weapons[0].Damage, Is.EqualTo(54)); Assert.That(second.Weapons[0].Damage, Is.EqualTo(54));
        }

        [TestCase(MaterialTier.Pencil, 40, 55, 12, 1.4f)]
        [TestCase(MaterialTier.Ink, 72, 115.5f, 15, 1.82f)]
        [TestCase(MaterialTier.Marker, 128, 231, 19.2f, 2.45f)]
        public void Material_IsAppliedOnceUsingSuppliedBalance(MaterialTier material, int cost, float health, float damage, float build)
        {
            var recipe = AssemblyResolverFixture.Tower(); var catalog = AssemblyResolverFixture.Catalog(); var balance = GameBalance.CreateDefault();
            var first = AssemblyResolverFixture.Resolve(recipe, catalog, balance, material);
            var again = AssemblyResolverFixture.Resolve(first.Snapshot.Recipe, catalog, balance, material);
            Assert.That(first.Cost, Is.EqualTo(cost)); Assert.That(first.Health, Is.EqualTo(health).Within(.0001f));
            Assert.That(first.Weapons[0].Damage, Is.EqualTo(damage).Within(.0001f)); Assert.That(first.BuildSeconds, Is.EqualTo(build).Within(.0001f));
            Assert.That(again.Cost, Is.EqualTo(cost)); Assert.That(again.Health, Is.EqualTo(health).Within(.0001f));
            Assert.That(AssemblyResolverFixture.Resolve(recipe, catalog, balance).Cost, Is.EqualTo(40));
            balance.Material(material).DamageMultiplier = 1;
            Assert.That(AssemblyResolverFixture.Resolve(recipe, catalog, balance, material).Weapons[0].Damage, Is.EqualTo(12));
        }

        [Test] public void AlbumLimits_KeepDefenseFrontSectorAndMinimumIntervalSeparate()
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "armor.plate", AssemblyStat.Defense, ModifierOperation.Add, .8f);
            AssemblyResolverFixture.Set(data, "head.gun", AssemblyStat.Interval, ModifierOperation.Multiply, .001f);
            var result = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(foundation: AssemblyResolverFixture.Foundation(armor: "plate", feed: "boost")), AssemblyResolverFixture.Modified(data));
            Assert.That(result.Defense, Is.EqualTo(.6f)); Assert.That(result.FeedMultiplier, Is.EqualTo(1.25f));
            Assert.That(result.Weapons[0].Interval, Is.EqualTo(.00042f).Within(.000001f)); Assert.That(result.Weapons[0].IntervalAtFeed(1.25f), Is.EqualTo(.05f));
            var front = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(foundation: AssemblyResolverFixture.Foundation(armor: "front")));
            Assert.That(front.Defense, Is.Zero); Assert.That(front.FrontDefense, Is.EqualTo(.4f));
            Assert.That(front.FrontDefenseDegrees, Is.EqualTo(90)); Assert.That(front.EffectiveHealth, Is.EqualTo(55));
        }

        [Test] public void IntervalFloor_IsAppliedAfterCurrentFeedRatherThanToIntrinsicTiming()
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "head.gun", AssemblyStat.Interval, ModifierOperation.Multiply, .1f);
            var result = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(second: AssemblyResolverFixture.Weapon()), AssemblyResolverFixture.Modified(data));
            Assert.That(result.Weapons[0].IntervalAtFeed(.8f), Is.EqualTo(.0525f).Within(.00001f));
            Assert.That(result.Weapons[0].Interval, Is.EqualTo(.042f).Within(.00001f));
        }

        [TestCase(.8f, .7f, .7f)]
        [TestCase(.3f, .9f, .4f)]
        public void Slow_UsesStrongestValueWithSpeedFloor(float first, float second, float expected)
        {
            var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "function.slow", AssemblyStat.Slow, ModifierOperation.Set, first);
            AssemblyResolverFixture.Set(data, "shell.standard", AssemblyStat.Slow, ModifierOperation.Set, second);
            var catalog = AssemblyResolverFixture.Modified(data);
            var recipe = new AssemblyRecipe(new StructureRecipe(AssemblyStructureFamily.Slow,
                AssemblyResolverFixture.Part("site.slow"), AssemblyResolverFixture.Part("function.slow"), AssemblyResolverFixture.Part("shell.standard"), AssemblyResolverFixture.Part("utility.none")));
            Assert.That(AssemblyResolverFixture.Resolve(recipe, catalog).Support.SlowSpeedMultiplier, Is.EqualTo(expected));
        }

        [TestCase(false, 80, 70, 6.5f, 4.8f, 0)]
        [TestCase(true, 72, 68, 0, 0, 15)]
        public void SupportVariants_PreserveFunctionalBaselinesAndMultipliers(bool generator, int cost, float health, float radius, float repair, int income)
        {
            string family = generator ? "generator" : "repair";
            var recipe = new AssemblyRecipe(new StructureRecipe(generator ? AssemblyStructureFamily.Generator : AssemblyStructureFamily.Repair,
                AssemblyResolverFixture.Part("site." + family), AssemblyResolverFixture.Part("function." + family),
                AssemblyResolverFixture.Part("shell.standard"), AssemblyResolverFixture.Part(generator ? "utility.economy" : "utility.range")));
            var value = AssemblyResolverFixture.Resolve(recipe);
            Assert.That(value.Cost, Is.EqualTo(cost)); Assert.That(value.Health, Is.EqualTo(health).Within(.0001f));
            Assert.That(value.Support.Radius, Is.EqualTo(radius).Within(.0001f));
            Assert.That(value.Support.RepairPerSecond, Is.EqualTo(repair).Within(.0001f)); Assert.That(value.Support.IncomePerWave, Is.EqualTo(income));
        }

        [Test] public void Support_UsesSiteAndFunctionBaselinesWithoutMaterialBoostingRate()
        {
            var catalog = AssemblyResolverFixture.Catalog(); var balance = GameBalance.CreateDefault();
            var repair = AssemblyResolverFixture.Resolve(catalog.Starters[6].Snapshot.Recipe, catalog, balance, MaterialTier.Ink);
            Assert.That(repair.Support.Kind, Is.EqualTo(AssemblyStructureFamily.Repair));
            Assert.That(repair.Support.Radius, Is.EqualTo(5)); Assert.That(repair.Support.RepairPerSecond, Is.EqualTo(6));
            Assert.That(repair.Health, Is.EqualTo(147).Within(.0001f)); Assert.That(repair.Capacity, Is.EqualTo(6));
            var generator = AssemblyResolverFixture.Resolve(catalog.Starters[7].Snapshot.Recipe, catalog);
            Assert.That(generator.Support.IncomePerWave, Is.EqualTo(12)); Assert.That(generator.Support.RefundFraction, Is.Zero);
            Assert.That(generator.Support.MaximumInstances, Is.EqualTo(2)); Assert.That(generator.Support.AllowedMaterials, Is.EqualTo(new[] { MaterialTier.Pencil }));
            Assert.That(generator.Support.PaybackWaves(generator.Cost), Is.EqualTo(5));
            var slow = AssemblyResolverFixture.Resolve(catalog.Starters[5].Snapshot.Recipe, catalog).Support;
            Assert.That(slow.SlowSpeedMultiplier, Is.EqualTo(.6f)); Assert.That(slow.SlowSeconds, Is.EqualTo(.6f));
        }

        [TestCase("piercer", 7, 0, 0)]
        [TestCase("fan", 8, 0, 90)]
        [TestCase("ricochet", 4, 3, 0)]
        [TestCase("mortar", 0, 0, 0)]
        [TestCase("blotter", 0, 0, 0)]
        public void FamilyLimits_AreTypedAndPreserved(string family, int targets, float jump, float cone)
        {
            var w = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(family))).Weapons[0];
            Assert.That(w.TargetLimit, Is.EqualTo(targets)); Assert.That(w.JumpRange, Is.EqualTo(jump)); Assert.That(w.ConeDegrees, Is.EqualTo(cone));
            if (family == "mortar") { Assert.That(w.MinimumRange, Is.EqualTo(6)); Assert.That(w.FlightSeconds, Is.EqualTo(.8f)); Assert.That(w.Radius, Is.EqualTo(2.4f)); }
            if (family == "blotter") Assert.That(w.Damage, Is.Zero);
        }

        [TestCase("burstDrum", "assembly.part.required")]
        [TestCase("overload", "assembly.load.exceeded")]
        [TestCase("headFamily", "assembly.part.incompatible")]
        [TestCase("frameSlots", "assembly.mount.unavailable")]
        [TestCase("revision", "assembly.part.missing")]
        [TestCase("category", "assembly.part.category")]
        [TestCase("time", "assembly.number.range")]
        [TestCase("magazine", "assembly.number.integer")]
        [TestCase("overflow", "assembly.number.nonfinite")]
        public void InvalidRecipes_ReturnDiagnosticsWithoutPublishingAValue(string defect, string code)
        {
            var data = AssemblyResolverFixture.Input(); var recipe = AssemblyResolverFixture.Tower();
            switch (defect)
            {
                case "burstDrum": recipe = AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(magazine: "drum", trigger: "burst")); break;
                case "overload": recipe = AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon("piercer"), AssemblyResolverFixture.Weapon("piercer")); break;
                case "headFamily": recipe = AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(head: "mortar")); break;
                case "frameSlots": recipe = AssemblyResolverFixture.Tower(second: AssemblyResolverFixture.Weapon(), foundation: AssemblyResolverFixture.Foundation(frame: "precision")); break;
                case "revision": recipe = AssemblyResolverFixture.Tower(foundation: new FoundationRecipe(AssemblyResolverFixture.Part("frame.standard", 2), AssemblyResolverFixture.Part("hull.standard"), AssemblyResolverFixture.Part("armor.none"), AssemblyResolverFixture.Part("feed.standard"), AssemblyResolverFixture.Part("turn.standard"))); break;
                case "category": recipe = AssemblyResolverFixture.Tower(foundation: new FoundationRecipe(AssemblyResolverFixture.Part("hull.standard"), AssemblyResolverFixture.Part("hull.standard"), AssemblyResolverFixture.Part("armor.none"), AssemblyResolverFixture.Part("feed.standard"), AssemblyResolverFixture.Part("turn.standard"))); break;
                case "time": AssemblyResolverFixture.Set(data, "core.gun", AssemblyStat.Interval, ModifierOperation.Set, 0); break;
                case "magazine": AssemblyResolverFixture.Set(data, "core.gun", AssemblyStat.MagazineSize, ModifierOperation.Set, 6.5f); break;
                case "overflow": AssemblyResolverFixture.Set(data, "core.gun", AssemblyStat.Damage, ModifierOperation.Set, float.MaxValue); AssemblyResolverFixture.Set(data, "head.gun", AssemblyStat.Damage, ModifierOperation.Multiply, 2); break;
            }
            var result = AssemblyResolver.Resolve(recipe, AssemblyResolverFixture.Modified(data), GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(result.Valid, Is.False); Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
            Assert.That(result.Diagnostics.All(d => !string.IsNullOrWhiteSpace(d.Path)), Is.True);
        }

        [Test] public void InvalidMountShape_IsRejectedBeforeResolution()
        {
            Assert.Throws<AssemblyCatalogException>(() => new TowerRecipe(AssemblyResolverFixture.Foundation(), new[] { new WeaponMountRecipe(1, AssemblyResolverFixture.Weapon()) }));
            Assert.Throws<AssemblyCatalogException>(() => new WeaponMountRecipe(3, AssemblyResolverFixture.Weapon()));
        }

        [Test] public void GeneratorAndBadMaterial_ReturnFailureInsteadOfPartialNumbers()
        {
            var catalog = AssemblyResolverFixture.Catalog(); var balance = GameBalance.CreateDefault();
            var generator = AssemblyResolver.Resolve(catalog.Starters[7].Snapshot.Recipe, catalog, balance, MaterialTier.Ink);
            Assert.That(generator.Valid, Is.False); Assert.That(generator.Value, Is.Null);
            Assert.That(generator.Diagnostics.Select(x => x.Code), Does.Contain("assembly.material.unsupported"));
            balance.Material(MaterialTier.Pencil).CostMultiplier = float.NaN;
            var bad = AssemblyResolver.Resolve(AssemblyResolverFixture.Tower(), catalog, balance, MaterialTier.Pencil);
            Assert.That(bad.Valid, Is.False); Assert.That(bad.Value, Is.Null);
            Assert.That(bad.Diagnostics.Select(x => x.Code), Does.Contain("assembly.number.nonfinite"));
        }
    }
}
