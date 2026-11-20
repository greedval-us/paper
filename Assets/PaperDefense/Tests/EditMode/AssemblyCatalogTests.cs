using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyCatalogTests
    {
        const string Path = "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset";

        [Test]
        public void FirstClearsFundAWeaponOrFoundationAtEachLevelBeforeTheFinalPage()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path).CreateSnapshot();
            var profile = WorkshopProfile.CreateNew(catalog);
            var upgrades = new[] { "head.short", "magazine.drum", "head.wide", "hull.reinforced",
                "trigger.burst", "frame.battery", "magazine.belt", "magazine.mortar.pair" };
            for (int i = 0; i < upgrades.Length; i++)
            {
                var page = CampaignCatalog.Default.Missions[i];
                Assert.That(profile.TryApplyMissionReward(page, Guid.NewGuid(), page.WaveCount, true, 1, out var reason), Is.True, reason);
                Assert.That(MetaProgression.LevelForXp(profile.TotalXp), Is.EqualTo(i + 1));
                Assert.That(catalog.TryGet(upgrades[i], 1, out var part), Is.True);
                Assert.That(part.RequiredLevel, Is.EqualTo(i + 1));
                Assert.That(profile.IsUnlocked(part.Id), Is.False, "This purchase should remain distinct from the page reward.");
                Assert.That(profile.TryPurchaseAssemblyPart(part, out reason), Is.True, part.Id + ": " + reason);
                Assert.That(profile.IsUnlocked(part.Id), Is.True);
                if (i == 0)
                {
                    Assert.That(profile.Gold, Is.EqualTo(45));
                    Assert.That(profile.TryPurchaseStructureFamily(AssemblyStructureFamily.Spikes, out reason), Is.True, reason);
                }
            }
        }

        [Test]
        public void PairedMortarCassetteFitsTheStarterTowerAndTradesASecondShotForReloadTime()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path).CreateSnapshot();
            var rules = GameBalance.CreateDefault();
            var mortar = catalog.Starters.Single(entry => entry.Id == "album.mortar");
            var tower = mortar.Snapshot.Recipe.Tower;
            var stock = tower.Mounts[0].Weapon;
            var pair = new WeaponRecipe(stock.Mechanism, stock.Head, stock.Sight,
                new PartRef("magazine.mortar.pair", 1), stock.Trigger, stock.Mount);
            var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, pair) }));
            var result = AssemblyResolver.Resolve(recipe, catalog, rules, MaterialTier.Pencil, AssemblyFitResources.Load());
            Assert.That(result.Valid, Is.True, string.Join(", ", result.Diagnostics.Select(d => d.Code)));
            var resolved = result.Value.Weapons.Single();
            var original = TowerCatalog.ResolveEntry(mortar, rules).Value.Weapons.Single();
            Assert.That(resolved.MagazineSize, Is.EqualTo(2));
            Assert.That(resolved.Interval, Is.LessThan(original.Interval * .5f));
            Assert.That(resolved.ReloadSeconds, Is.GreaterThan(original.ReloadSeconds));
            Assert.That(result.Value.Load, Is.EqualTo(result.Value.Capacity));
            Assert.That(result.Value.Cost, Is.EqualTo(117));
            Assert.That(resolved.Damage, Is.EqualTo(original.Damage));
            Assert.That(resolved.Radius, Is.EqualTo(original.Radius));
        }

        [Test]
        public void AStockArmoredMortarCanMountEitherSpecialistPartAndAHeavyFrameSupportsBoth()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path).CreateSnapshot();
            var stock = catalog.Starters.Single(entry => entry.Id == "album.mortar").Snapshot.Recipe.Tower;
            var rules = GameBalance.CreateDefault();
            var fits = AssemblyFitResources.Load();
            var weapon = stock.Mounts[0].Weapon;
            var frame = stock.Foundation;
            AssemblyResolution Resolve(FoundationRecipe foundation, PartRef magazine)
            {
                var wide = new WeaponRecipe(weapon.Mechanism, new PartRef("head.wide", 1),
                    weapon.Sight, magazine, weapon.Trigger, weapon.Mount);
                var recipe = new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, wide) }));
                return AssemblyResolver.Resolve(recipe, catalog, rules, MaterialTier.Pencil, fits);
            }
            Assert.That(Resolve(frame, weapon.Magazine).Valid, Is.True);
            var pair = new PartRef("magazine.mortar.pair", 1);
            Assert.That(Resolve(frame, pair).Diagnostics.Select(d => d.Code), Does.Contain("assembly.load.exceeded"));
            var heavy = new FoundationRecipe(new PartRef("frame.heavy", 1), frame.Hull, frame.Armor, frame.Feed, frame.Turn);
            var combined = Resolve(heavy, pair);
            Assert.That(combined.Valid, Is.True, string.Join(", ", combined.Diagnostics.Select(d => d.Code)));
            Assert.That(combined.Value.Load, Is.EqualTo(11));
        }

        [Test]
        public void ProductionCatalogHasCompleteFreeStarterSet()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path).CreateSnapshot();
            Assert.That(catalog.BalanceRevision, Is.EqualTo("album-v1.4"));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(91));
            Assert.That(catalog.Starters.Count, Is.EqualTo(8));
            Assert.That(catalog.Definitions.Count(x => x.Category <= ComponentCategory.Turn), Is.EqualTo(18));
            Assert.That(catalog.Definitions.Count(x => x.Category == ComponentCategory.Mechanism), Is.EqualTo(7));
            Assert.That(catalog.Definitions.Count(x => x.Category >= ComponentCategory.Head && x.Category <= ComponentCategory.Trigger), Is.EqualTo(27));
            Assert.That(catalog.Definitions.Count(x => x.Category >= ComponentCategory.Site && x.Category <= ComponentCategory.Utility), Is.EqualTo(36));
            Assert.That(catalog.Definitions.Count(x => x.Category == ComponentCategory.Mount), Is.EqualTo(3));
            Assert.That(catalog.Starters.Select(x => x.Id), Is.EqualTo(new[] {
                "album.guard", "album.double", "album.piercer", "album.mortar",
                "album.wall", "album.slow", "album.repair", "album.generator" }));
            foreach (var entry in catalog.Starters)
                foreach (var part in entry.Snapshot.Recipe.Parts)
                {
                    Assert.That(catalog.TryGet(part.Id, part.Revision, out var definition), Is.True, part.Id);
                    Assert.That(definition.UnlockGold, Is.Zero, part.Id);
                }
            Assert.That(catalog.TryGet("core.gun", 2, out _), Is.False);
            Assert.That(catalog.TryGet("missing", 1, out _), Is.False);
        }

        [Test]
        public void PublishedSnapshot_OwnsNestedDefinitionsAndRecipeArrays()
        {
            var source = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path);
            var candidate = UnityEngine.Object.Instantiate(source);
            try
            {
                var snapshot = candidate.CreateSnapshot();
                var firstId = candidate.catalog.Definitions[0].Id;
                candidate.catalog.Definitions[0].Id = "changed";
                candidate.catalog.Definitions[0].Modifiers[0].Value = 999;
                candidate.catalog.Starters[1].Mounts[0].Weapon.Mechanism.Id = "changed";
                candidate.catalog.Starters = Array.Empty<AssemblyStarterInput>();
                Assert.That(snapshot.TryGet(firstId, 1, out var frame), Is.True);
                Assert.That(frame.Get(AssemblyStat.Cost, ModifierOperation.Add), Is.EqualTo(16));
                Assert.That(snapshot.Starters.Count, Is.EqualTo(8));
                Assert.That(snapshot.Starters[1].Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id, Is.EqualTo("core.gun"));
                Assert.That(snapshot.Definitions, Is.Not.InstanceOf<ComponentDefinition[]>());
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }

        [Test]
        public void RecipeSnapshot_CopiesDefinitionArrayAndKeepsExactRevisions()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path).CreateSnapshot();
            var starter = catalog.Starters[0];
            var definitions = starter.Snapshot.Definitions.ToArray();
            var snapshot = new RecipeSnapshot(starter.Snapshot.Recipe, catalog.BalanceRevision, definitions);
            definitions[0] = null;
            Assert.That(snapshot.Definitions[0], Is.Not.Null);
            Assert.That(snapshot.Definitions.All(x => x.Revision == 1), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void StarterInput_RejectsPopulatedAlternatePayload(bool tower)
        {
            var source = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path);
            var candidate = UnityEngine.Object.Instantiate(source);
            try
            {
                var entry = candidate.catalog.Starters[tower ? 0 : 4];
                if (tower)
                    entry.Structure = new StructureRecipeInput { Function = new PartRefInput { Id = "function.wall" } };
                else
                    entry.Foundation = new FoundationRecipeInput { Hull = new PartRefInput { Id = "hull.standard" } };
                var error = Assert.Throws<AssemblyCatalogException>(() => entry.CreateRecipe());
                Assert.That(error.Diagnostics.Select(x => x.Code), Does.Contain("assembly.recipe.payload"));
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }
    }
}
