using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class ProfileStandardConversionTests
    {
        // Armor keeps the historical material costs away from integer boundaries: 58 / 105 / 186.
        // The explicit historical fixture retains the material multipliers and their float rounding rules.
        static TowerBlueprint StandardPage() => new TowerBlueprint("legacy.armored.gun", "Historical armored gun",
            ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Armor);

        static GameBalance HistoricalBalance()
        {
            var balance = GameBalance.CreateDefault();
            // This known historical catalog describes a single-target gun explicitly.
            balance.Structure(StructureKind.Gun).MaxTargets = 1;
            return balance;
        }

        static WorkshopProfileData Historical(TowerBlueprint page = null)
        {
            var profile = new WorkshopProfile(); profile.GrantGold(321);
            var data = profile.ToData(); data.Version = 7;
            page = page ?? StandardPage();
            data.Album = new[] { new BlueprintData(page) };
            data.Loadout = new[] { page.Id, null, null, null };
            data.Entries = null; data.Library = null; data.Rewards = null; data.Operations = null;
            return data;
        }

        static AssemblyCatalogInput MatchingCatalogInput(GameBalance balance)
        {
            var input = AssemblyResolverFixture.Input();
            var gun = balance.Structure(StructureKind.Gun); var rules = balance.Construction;
            void Set(string id, AssemblyStat stat, ModifierOperation operation, float value)
                => AssemblyResolverFixture.Set(input, id, stat, operation, value);
            Set("frame.standard", AssemblyStat.Cost, ModifierOperation.Add, rules.StandardBaseCost);
            Set("frame.standard", AssemblyStat.Health, ModifierOperation.Set, rules.FoundationHealth);
            Set("frame.standard", AssemblyStat.Capacity, ModifierOperation.Set, rules.StandardCapacity);
            Set("armor.plate", AssemblyStat.Cost, ModifierOperation.Add, rules.ArmorCost);
            Set("armor.plate", AssemblyStat.Load, ModifierOperation.Add, rules.ArmorLoad);
            Set("armor.plate", AssemblyStat.BuildSeconds, ModifierOperation.Add, rules.ArmorBuildSeconds);
            Set("armor.plate", AssemblyStat.Health, ModifierOperation.Multiply, rules.ArmorHealthMultiplier);
            Set("armor.plate", AssemblyStat.Defense, ModifierOperation.Add, rules.ArmorDefense);
            Set("core.gun", AssemblyStat.Cost, ModifierOperation.Add, gun.Cost - rules.WeaponAssemblyCredit);
            Set("core.gun", AssemblyStat.Load, ModifierOperation.Add, rules.GunLoad);
            Set("core.gun", AssemblyStat.BuildSeconds, ModifierOperation.Add, gun.BuildSeconds);
            Set("core.gun", AssemblyStat.Damage, ModifierOperation.Set, gun.Damage);
            Set("core.gun", AssemblyStat.Interval, ModifierOperation.Set, gun.ShotInterval);
            Set("core.gun", AssemblyStat.ReloadSeconds, ModifierOperation.Set, gun.ShotInterval);
            Set("core.gun", AssemblyStat.MagazineSize, ModifierOperation.Set, 1);
            Set("core.gun", AssemblyStat.Range, ModifierOperation.Set, gun.Range);
            Set("core.gun", AssemblyStat.MinimumRange, ModifierOperation.Set, 0);
            Set("core.gun", AssemblyStat.TargetLimit, ModifierOperation.Set, 1);
            Set("core.gun", AssemblyStat.Radius, ModifierOperation.Set, gun.Radius);
            Set("core.gun", AssemblyStat.FlightSeconds, ModifierOperation.Set, gun.FlightSeconds);
            Set("core.gun", AssemblyStat.JumpRange, ModifierOperation.Set, gun.JumpRange);
            Set("core.gun", AssemblyStat.ConeDegrees, ModifierOperation.Set, gun.ConeAngleDegrees);
            Set("core.gun", AssemblyStat.Slow, ModifierOperation.Set, gun.SlowSpeedMultiplier);
            Set("core.gun", AssemblyStat.SlowSeconds, ModifierOperation.Set, gun.SlowDuration);
            return input;
        }

        static string Diagnostics(ProfileMigrationResult result) => string.Join(",", result.Diagnostics.Select(issue => issue.Code + ":" + issue.Path));

        [Test] public void MatchingStandardRequiresExplicitHistoricalBalanceBeforeConversion()
        {
            var balance = HistoricalBalance();
            var catalog = AssemblyResolverFixture.Modified(MatchingCatalogInput(balance));
            var source = Historical();
            var withoutBalance = ProfileMigrationV8.Migrate(source, catalog);
            Assert.That(withoutBalance.Success, Is.True, Diagnostics(withoutBalance));
            Assert.That(withoutBalance.Candidate.Entries.Single().Kind, Is.EqualTo(AlbumEntryKind.Legacy));

            var proven = ProfileMigrationV8.Migrate(source, catalog, balance);
            Assert.That(proven.Success, Is.True, Diagnostics(proven));
            Assert.That(proven.Candidate.Entries.Single().Kind, Is.EqualTo(AlbumEntryKind.Tower));
        }

        [Test] public void MatchingStandardConvertsWithIdentityProgressAndAllMaterialStatisticsIntact()
        {
            var balance = HistoricalBalance();
            var catalog = AssemblyResolverFixture.Modified(MatchingCatalogInput(balance));
            var source = Historical(); string original = JsonUtility.ToJson(source);
            var migration = ProfileMigrationV8.Migrate(source, catalog, balance);
            Assert.That(migration.Success, Is.True, Diagnostics(migration));
            var candidate = migration.Candidate; var entry = candidate.Entries.Single();
            Assert.That(entry.Kind, Is.EqualTo(AlbumEntryKind.Tower));
            Assert.That(entry.Id, Is.EqualTo(source.Album[0].Id));
            Assert.That(entry.Name, Is.EqualTo(source.Album[0].Name));
            Assert.That(candidate.Gold, Is.EqualTo(321));
            Assert.That(candidate.TotalXp, Is.Zero);
            Assert.That(candidate.Loadout.Take(4), Is.EqualTo(source.Loadout));
            Assert.That(candidate.Loadout.Skip(4), Is.All.Null);
            foreach (string right in source.Unlocked) Assert.That(candidate.IsUnlocked(right), Is.True, right);
            var legacy = new AlbumEntry(source.Album[0].Id, source.Album[0].Name, 1, source.Album[0].ToBlueprint(7));
            int materialIndex = 0;
            foreach (MaterialTier material in Enum.GetValues(typeof(MaterialTier)))
            {
                var previous = TowerCatalog.ResolveEntry(legacy, balance, material);
                var converted = TowerCatalog.ResolveEntry(entry, balance, material);
                Assert.That(previous.Valid, Is.True, material.ToString());
                Assert.That(converted.Valid, Is.True, material.ToString());
                Assert.That(previous.Value.Cost, Is.EqualTo(new[] { 58, 105, 186 }[materialIndex++]));
                AssertEquivalent(previous.Value, converted.Value, material);
            }
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original), "Migration must leave its historical DTO untouched.");
        }

        [Test] public void OneDifferentWeaponStatisticRetainsTheCompleteLegacyPage()
        {
            var balance = HistoricalBalance();
            var input = MatchingCatalogInput(balance);
            AssemblyResolverFixture.Set(input, "core.gun", AssemblyStat.Range, ModifierOperation.Set, balance.Structure(StructureKind.Gun).Range + 1);
            var source = Historical();
            var result = ProfileMigrationV8.Migrate(source, AssemblyResolverFixture.Modified(input), balance);
            Assert.That(result.Success, Is.True, Diagnostics(result));
            var entry = result.Candidate.Entries.Single();
            Assert.That(entry.Kind, Is.EqualTo(AlbumEntryKind.Legacy));
            Assert.That(JsonUtility.ToJson(new BlueprintData(entry.LegacyBlueprint)), Is.EqualTo(JsonUtility.ToJson(source.Album[0])));
        }

        [Test] public void OppositeReloadChangesCannotCancelInAggregateDpsAndPermitConversion()
        {
            var balance = HistoricalBalance(); var input = MatchingCatalogInput(balance);
            // The old feed policy matches both one-active and two-active mount cases here.
            balance.Construction.DynamicFeed = true;
            var piercer = balance.Structure(StructureKind.Piercer);
            void Set(AssemblyStat stat, ModifierOperation operation, float value)
                => AssemblyResolverFixture.Set(input, "core.piercer", stat, operation, value);
            Set(AssemblyStat.Cost, ModifierOperation.Add, piercer.Cost - balance.Construction.WeaponAssemblyCredit);
            Set(AssemblyStat.Load, ModifierOperation.Add, balance.Construction.PiercerLoad);
            Set(AssemblyStat.BuildSeconds, ModifierOperation.Add, piercer.BuildSeconds);
            Set(AssemblyStat.Damage, ModifierOperation.Set, piercer.Damage);
            Set(AssemblyStat.Interval, ModifierOperation.Set, piercer.ShotInterval);
            Set(AssemblyStat.ReloadSeconds, ModifierOperation.Set, .675f);
            Set(AssemblyStat.MagazineSize, ModifierOperation.Set, 1);
            Set(AssemblyStat.Range, ModifierOperation.Set, piercer.Range);
            Set(AssemblyStat.MinimumRange, ModifierOperation.Set, 0);
            Set(AssemblyStat.TargetLimit, ModifierOperation.Set, piercer.MaxPierce);
            Set(AssemblyStat.Radius, ModifierOperation.Set, piercer.Radius);
            Set(AssemblyStat.FlightSeconds, ModifierOperation.Set, piercer.FlightSeconds);
            Set(AssemblyStat.JumpRange, ModifierOperation.Set, piercer.JumpRange);
            Set(AssemblyStat.ConeDegrees, ModifierOperation.Set, piercer.ConeAngleDegrees);
            Set(AssemblyStat.Slow, ModifierOperation.Set, piercer.SlowSpeedMultiplier);
            Set(AssemblyStat.SlowSeconds, ModifierOperation.Set, piercer.SlowDuration);
            AssemblyResolverFixture.Set(input, "core.gun", AssemblyStat.MagazineSize, ModifierOperation.Set, 2);
            AssemblyResolverFixture.Set(input, "core.gun", AssemblyStat.ReloadSeconds, ModifierOperation.Set, .84f);
            var catalog = AssemblyResolverFixture.Modified(input);
            var page = new TowerBlueprint("legacy.mixed.cadence", "Two historical cadences", ChassisKind.Standard,
                StructureKind.Gun, StructureKind.Piercer);
            var old = new AlbumEntry(page.Id, page.Name, 1, page);
            var changedRecipe = AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(), AssemblyResolverFixture.Weapon("piercer"));
            foreach (MaterialTier material in Enum.GetValues(typeof(MaterialTier)))
            {
                var previous = TowerCatalog.ResolveEntry(old, balance, material);
                var changed = AssemblyResolver.Resolve(changedRecipe, catalog, balance, material);
                Assert.That(previous.Valid, Is.True, material.ToString());
                Assert.That(changed.Valid, Is.True, material.ToString());
                Assert.That(changed.Value.Dps, Is.EqualTo(previous.Value.Dps).Within(.0001f), "Aggregate DPS deliberately hides opposite cadence changes.");
                Assert.That(changed.Value.Weapons[0].DpsAtFeed(changed.Value.FeedMultiplier),
                    Is.Not.EqualTo(previous.Value.Weapons[0].DpsAtFeed(previous.Value.FeedMultiplier)).Within(.0001f));
            }

            var result = ProfileMigrationV8.Migrate(Historical(page), catalog, balance);
            Assert.That(result.Success, Is.True, Diagnostics(result));
            Assert.That(result.Candidate.Entries.Single().Kind, Is.EqualTo(AlbumEntryKind.Legacy),
                "Every weapon cadence must be preserved independently even when the total DPS matches.");
        }

        [Test] public void AChangedCadenceWhenOneMountIsInactivePreventsConversion()
        {
            var balance = HistoricalBalance();
            Assert.That(balance.Construction.DynamicFeed, Is.False);
            var catalog = AssemblyResolverFixture.Modified(MatchingCatalogInput(balance));
            var page = new TowerBlueprint("legacy.double.feed", "Historical fixed shared feed", ChassisKind.Standard,
                StructureKind.Gun, StructureKind.Gun);
            var previous = TowerCatalog.ResolveEntry(new AlbumEntry(page.Id, page.Name, 1, page), balance);
            var changed = AssemblyResolver.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(), AssemblyResolverFixture.Weapon()),
                catalog, balance, MaterialTier.Pencil);
            Assert.That(previous.Valid, Is.True);
            Assert.That(changed.Valid, Is.True);
            Assert.That(changed.Value.Dps, Is.EqualTo(previous.Value.Dps).Within(.0001f), "Both-active statistics match.");
            Assert.That(changed.Value.FeedForActiveMounts(2), Is.EqualTo(TowerCatalog.FeedMultiplier(page, balance, 2)).Within(.0001f));
            Assert.That(changed.Value.FeedForActiveMounts(1), Is.Not.EqualTo(TowerCatalog.FeedMultiplier(page, balance, 1)).Within(.0001f));

            var result = ProfileMigrationV8.Migrate(Historical(page), catalog, balance);
            Assert.That(result.Success, Is.True, Diagnostics(result));
            Assert.That(result.Candidate.Entries.Single().Kind, Is.EqualTo(AlbumEntryKind.Legacy),
                "Matching both-active DPS cannot prove the historical cadence when a mount loses its target.");
        }

        [Test] public void FreeformDrawingRemainsLegacyEvenWithAnOtherwiseMatchingCatalog()
        {
            var drawing = new DrawingSnapshot(new[] {
                new DrawingPart(DrawingBindings.Base, new[] { new DrawingStroke(new[] {
                    new DrawingPoint(-.3f,-.3f), new DrawingPoint(.3f,-.3f), new DrawingPoint(.3f,.3f),
                    new DrawingPoint(-.3f,.3f), new DrawingPoint(-.3f,-.3f) }) }),
                new DrawingPart(DrawingBindings.Mount0, new[] { new DrawingStroke(new[] {
                    new DrawingPoint(-.08f,0), new DrawingPoint(.08f,0), new DrawingPoint(.08f,.7f),
                    new DrawingPoint(-.08f,.7f), new DrawingPoint(-.08f,0) }) })
            });
            var page = new TowerBlueprint("legacy.freeform.gun", "Historical freeform gun", ChassisKind.Standard,
                StructureKind.Gun, modules: TowerModules.Armor, drawing: drawing, origin: DrawingOrigin.Freeform, freeformRuleVersion: 1);
            var balance = HistoricalBalance();
            var result = ProfileMigrationV8.Migrate(Historical(page), AssemblyResolverFixture.Modified(MatchingCatalogInput(balance)), balance);
            Assert.That(result.Success, Is.True, Diagnostics(result));
            Assert.That(result.Candidate.Entries.Single().Kind, Is.EqualTo(AlbumEntryKind.Legacy));
            Assert.That(result.Candidate.Entries.Single().LegacyBlueprint.Origin, Is.EqualTo(DrawingOrigin.Freeform));
        }

        static void AssertEquivalent(ResolvedEntry previous, ResolvedEntry converted, MaterialTier material)
        {
            string context = material.ToString();
            Assert.That(converted.Cost, Is.EqualTo(previous.Cost), context + ".cost");
            Assert.That(converted.Load, Is.EqualTo(previous.Load), context + ".load");
            Assert.That(converted.Capacity, Is.EqualTo(previous.Capacity), context + ".capacity");
            Assert.That(converted.Health, Is.EqualTo(previous.Health).Within(.0001f), context + ".health");
            Assert.That(converted.BuildSeconds, Is.EqualTo(previous.BuildSeconds).Within(.0001f), context + ".build");
            Assert.That(converted.Defense, Is.EqualTo(previous.Defense).Within(.0001f), context + ".defense");
            Assert.That(converted.FrontDefense, Is.EqualTo(previous.FrontDefense).Within(.0001f), context + ".frontDefense");
            Assert.That(converted.RefundFraction, Is.EqualTo(previous.RefundFraction).Within(.0001f), context + ".refund");
            Assert.That(converted.Dps, Is.EqualTo(previous.Dps).Within(.0001f), context + ".dps");
            Assert.That(converted.Weapons.Count, Is.EqualTo(previous.Weapons.Count), context + ".weapons");
            var oldWeapon = previous.Weapons.Single(); var newWeapon = converted.Weapons.Single();
            Assert.That(newWeapon.SlotIndex, Is.EqualTo(oldWeapon.SlotIndex), context + ".mount");
            Assert.That(newWeapon.Attack, Is.EqualTo(oldWeapon.Attack), context + ".attack");
            Assert.That(newWeapon.Damage, Is.EqualTo(oldWeapon.Damage).Within(.0001f), context + ".damage");
            Assert.That(newWeapon.Range, Is.EqualTo(oldWeapon.Range).Within(.0001f), context + ".range");
            Assert.That(newWeapon.MinimumRange, Is.EqualTo(oldWeapon.MinimumRange).Within(.0001f), context + ".minimumRange");
            Assert.That(newWeapon.TargetLimit, Is.EqualTo(oldWeapon.TargetLimit), context + ".targetLimit");
            Assert.That(newWeapon.AcquisitionSeconds, Is.EqualTo(oldWeapon.AcquisitionSeconds).Within(.0001f), context + ".acquisition");
            Assert.That(newWeapon.IntervalAtFeed(converted.FeedMultiplier), Is.EqualTo(oldWeapon.IntervalAtFeed(previous.FeedMultiplier)).Within(.0001f), context + ".interval");
            Assert.That(newWeapon.ReloadAtFeed(converted.FeedMultiplier), Is.EqualTo(oldWeapon.ReloadAtFeed(previous.FeedMultiplier)).Within(.0001f), context + ".reload");
            Assert.That(newWeapon.Radius, Is.EqualTo(oldWeapon.Radius).Within(.0001f), context + ".radius");
            Assert.That(newWeapon.FlightSeconds, Is.EqualTo(oldWeapon.FlightSeconds).Within(.0001f), context + ".flight");
            Assert.That(newWeapon.JumpRange, Is.EqualTo(oldWeapon.JumpRange).Within(.0001f), context + ".jump");
            Assert.That(newWeapon.ConeDegrees, Is.EqualTo(oldWeapon.ConeDegrees).Within(.0001f), context + ".cone");
            Assert.That(newWeapon.SlowSpeedMultiplier, Is.EqualTo(oldWeapon.SlowSpeedMultiplier).Within(.0001f), context + ".slow");
            Assert.That(newWeapon.SlowSeconds, Is.EqualTo(oldWeapon.SlowSeconds).Within(.0001f), context + ".slowSeconds");
        }
    }
}
