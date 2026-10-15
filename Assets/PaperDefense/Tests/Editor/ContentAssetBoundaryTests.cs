using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class ContentAssetBoundaryTests
    {
        ContentDatabaseAsset sourceDatabase, database;
        BalancePresetAsset sourcePreset, preset;
        string sourceCatalogJson, sourceRulesJson;

        [SetUp]
        public void CopyActualAuthoringAssetsIntoUnpublishedCandidates()
        {
            sourceDatabase = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(ContentDatabaseMigration.DatabasePath);
            sourcePreset = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(ContentDatabaseMigration.DirectoryPath + "/Historical.asset");
            Assert.That(sourceDatabase, Is.Not.Null, "Migrate the real catalog before running its boundary tests.");
            Assert.That(sourcePreset, Is.Not.Null);
            sourceCatalogJson = JsonUtility.ToJson(sourceDatabase.catalog);
            sourceRulesJson = JsonUtility.ToJson(sourcePreset.rules);
            database = ScriptableObject.CreateInstance<ContentDatabaseAsset>();
            database.catalog = sourceDatabase.catalog.CreateCopy();
            preset = ScriptableObject.CreateInstance<BalancePresetAsset>();
            preset.name = "UnpublishedBoundaryCandidate";
            preset.database = database;
            preset.rules = GlobalBalanceSettings.From(sourcePreset.CreateBalance());
        }

        [TearDown]
        public void DestroyOnlyTheCandidatesAndVerifySourceAssetsWereNotEdited()
        {
            if (preset != null) Object.DestroyImmediate(preset);
            if (database != null) Object.DestroyImmediate(database);
            if (sourceDatabase != null) Assert.That(JsonUtility.ToJson(sourceDatabase.catalog), Is.EqualTo(sourceCatalogJson));
            if (sourcePreset != null) Assert.That(JsonUtility.ToJson(sourcePreset.rules), Is.EqualTo(sourceRulesJson));
        }

        [Test]
        public void ActualCatalogCopyOwnsNestedDataAndPublishesTheSameDefinitions()
        {
            Assert.That(JsonUtility.ToJson(database.catalog), Is.EqualTo(sourceCatalogJson));
            var source = sourceDatabase.CreateSnapshot();
            var copy = database.CreateSnapshot();
            Assert.That(copy, Is.Not.SameAs(source));
            Assert.That(copy.AllParts.Select(part => part.Id), Is.EqualTo(source.AllParts.Select(part => part.Id)));
            Assert.That(copy.Weapons.Select(weapon => weapon.Stats.Cost), Is.EqualTo(source.Weapons.Select(weapon => weapon.Stats.Cost)));
            Assert.That(copy.Templates.Select(template => template.Signature), Is.EqualTo(source.Templates.Select(template => template.Signature)));
            Assert.That(database.catalog.Weapons[0].Stats, Is.Not.SameAs(sourceDatabase.catalog.Weapons[0].Stats));
            Assert.That(database.catalog.Modules[0].Modifiers[0], Is.Not.SameAs(sourceDatabase.catalog.Modules[0].Modifiers[0]));
            Assert.That(database.catalog.Templates[0].Contours[0].Points[0], Is.Not.SameAs(sourceDatabase.catalog.Templates[0].Contours[0].Points[0]));

            database.catalog.Weapons[0].Stats.Cost += 17;
            database.catalog.Modules[0].Modifiers[0].Value += .25f;
            database.catalog.Templates[0].Contours[0].Points[0].X *= .5f;
            database.catalog.Effects[0].Duration += 1;
            Assert.That(JsonUtility.ToJson(sourceDatabase.catalog), Is.EqualTo(sourceCatalogJson));
            Assert.That(copy.Weapons[0].Stats.Cost, Is.EqualTo(source.Weapons[0].Stats.Cost));
        }

        [Test]
        public void CompleteReplacementKeepsExplicitZeroAndFalseAndLeavesTheBaseUntouched()
        {
            var weapon = database.catalog.CreateCopy().Weapons.Single(item => item.Id == "weapon.fan");
            weapon.Enabled = false; weapon.InitiallyUnlocked = false;
            weapon.UnlockGold = 0; weapon.Load = 0; weapon.AssemblyCredit = 0;
            weapon.Stats.Cost = 0; weapon.Stats.Damage = 0; weapon.Stats.Range = 0; weapon.Stats.BuildSeconds = 0;
            preset.overrides.Weapons.Add(weapon);
            var effect = database.catalog.CreateCopy().Effects.Single(item => item.Id == database.catalog.RedEffectId);
            effect.Duration = 0; effect.Strength = 0; effect.IgnoresDefense = false;
            preset.overrides.Effects.Add(effect);

            var result = preset.CreateBalance();
            var actual = result.Content.GetWeapon(weapon.Id);
            Assert.That(actual.Enabled, Is.False); Assert.That(actual.InitiallyUnlocked, Is.False);
            Assert.That(actual.UnlockGold, Is.Zero); Assert.That(actual.Load, Is.Zero); Assert.That(actual.AssemblyCredit, Is.Zero);
            Assert.That(actual.Stats.Cost, Is.Zero); Assert.That(actual.Stats.Damage, Is.Zero);
            Assert.That(actual.Stats.Range, Is.Zero); Assert.That(actual.Stats.BuildSeconds, Is.Zero);
            Assert.That(result.Content.GetEffect(effect.Id).Duration, Is.Zero);
            Assert.That(result.Content.GetEffect(effect.Id).Strength, Is.Zero);
            Assert.That(result.Content.GetEffect(effect.Id).IgnoresDefense, Is.False);
            Assert.That(result.Effects.RedDuration, Is.Zero); Assert.That(result.Effects.RedDamagePerTick, Is.Zero);
            Assert.That(result.Effects.RedIgnoresDefense, Is.False);
            Assert.That(JsonUtility.ToJson(database.catalog), Is.EqualTo(sourceCatalogJson));
        }

        [Test]
        public void MissingReplacementInheritsTheWholeBaseEntry()
        {
            var expected = database.CreateSnapshot();
            var replacement = database.catalog.CreateCopy().Weapons.Single(item => item.Id == "weapon.gun");
            replacement.Stats.Cost += 23;
            preset.overrides.Weapons.Add(replacement);
            var result = preset.CreateBalance().Content;

            Assert.That(result.GetWeapon(replacement.Id).Stats.Cost, Is.EqualTo(replacement.Stats.Cost));
            foreach (var weapon in expected.Weapons.Where(item => item.Id != replacement.Id))
            {
                var inherited = result.GetWeapon(weapon.Id);
                Assert.That(inherited.Stats.Cost, Is.EqualTo(weapon.Stats.Cost), weapon.Id);
                Assert.That(inherited.Stats.Damage, Is.EqualTo(weapon.Stats.Damage), weapon.Id);
                Assert.That(inherited.Enabled, Is.EqualTo(weapon.Enabled), weapon.Id);
                Assert.That(inherited.EffectIds, Is.EqualTo(weapon.EffectIds), weapon.Id);
            }
            Assert.That(result.GetChassis("chassis.heavy").BaseCost, Is.EqualTo(expected.GetChassis("chassis.heavy").BaseCost));
            Assert.That(result.GetModule("module.rail").Modifiers[0].Value, Is.EqualTo(expected.GetModule("module.rail").Modifiers[0].Value));
            Assert.That(result.Templates.Select(item => item.Signature), Is.EqualTo(expected.Templates.Select(item => item.Signature)));
        }

        [TestCase("chassis", false)] [TestCase("chassis", true)]
        [TestCase("weapon", false)] [TestCase("weapon", true)]
        [TestCase("module", false)] [TestCase("module", true)]
        [TestCase("effect", false)] [TestCase("effect", true)]
        [TestCase("template", false)] [TestCase("template", true)]
        public void DuplicateOrUnknownReplacementFailsBeforePublishingOrMutatingTheBase(string category, bool unknown)
        {
            var published = preset.CreateBalance();
            string before = JsonUtility.ToJson(database.catalog);
            string id = AddInvalidOverride(category, unknown);
            var error = Assert.Throws<ArgumentException>(() => preset.CreateBalance());
            Assert.That(TextResourcesAsset.Default.Resolve(error.Message), Does.Contain(id));
            Assert.That(TextResourcesAsset.Default.Resolve(error.Message), Does.Contain(category));
            Assert.That(JsonUtility.ToJson(database.catalog), Is.EqualTo(before));
            Assert.That(published.Content.GetWeapon("weapon.gun").Stats.Cost, Is.EqualTo(sourceDatabase.CreateSnapshot().GetWeapon("weapon.gun").Stats.Cost));
            preset.overrides = new ContentOverrides();
            Assert.DoesNotThrow(() => preset.CreateBalance());
        }

        [Test]
        public void PublishedSnapshotIsolatedFromLaterNestedAssetAndOverrideEdits()
        {
            var replacement = database.catalog.CreateCopy().Weapons.Single(item => item.Id == "weapon.gun");
            preset.overrides.Weapons.Add(replacement);
            var published = preset.CreateBalance();
            var frozen = published.Content;
            var gun = frozen.GetWeapon(replacement.Id);
            var module = frozen.GetModule("module.rail");
            var template = frozen.GetTemplate(gun.TemplateId);
            int oldCost = gun.Stats.Cost, oldEffects = gun.EffectIds.Count;
            float oldModifier = module.Modifiers[0].Value, oldDuration = frozen.GetEffect(frozen.RedEffectId).Duration;
            float oldPointX = template.Contours[0].Points[0].X;
            int oldRevision = frozen.Assessment.Revision;

            replacement.Stats.Cost += 31; replacement.EffectIds.Add(database.catalog.RedEffectId);
            database.catalog.Modules.Single(item => item.Id == module.Id).Modifiers[0].Value += .2f;
            database.catalog.Effects.Single(item => item.Id == frozen.RedEffectId).Duration += 2;
            database.catalog.Templates.Single(item => item.Id == template.Id).Contours[0].Points[0].X *= .5f;
            database.catalog.Assessment.Revision++;
            preset.rules.StartingInk += 109;

            Assert.That(gun.Stats.Cost, Is.EqualTo(oldCost)); Assert.That(gun.EffectIds.Count, Is.EqualTo(oldEffects));
            Assert.That(module.Modifiers[0].Value, Is.EqualTo(oldModifier));
            Assert.That(frozen.GetEffect(frozen.RedEffectId).Duration, Is.EqualTo(oldDuration));
            Assert.That(template.Contours[0].Points[0].X, Is.EqualTo(oldPointX));
            Assert.That(frozen.Assessment.Revision, Is.EqualTo(oldRevision));
            Assert.That(published.StartingInk, Is.EqualTo(sourcePreset.rules.StartingInk));
            var later = preset.CreateBalance();
            Assert.That(later.Content.GetWeapon(gun.Id).Stats.Cost, Is.EqualTo(oldCost + 31));
            Assert.That(later.Content.Assessment.Revision, Is.EqualTo(oldRevision + 1));
            Assert.That(later.StartingInk, Is.EqualTo(published.StartingInk + 109));
            Assert.Throws<NotSupportedException>(() => ((IList<WeaponDefinition>)frozen.Weapons).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)gun.EffectIds).Clear());
        }

        [Test]
        public void SharedRulesFromAndCreateBalanceOwnEveryNestedGroup()
        {
            var source = sourcePreset.CreateBalance();
            var input = GlobalBalanceSettings.From(source);
            string expected = JsonUtility.ToJson(input);
            source.Mechanics.ColorCartridges = !source.Mechanics.ColorCartridges;
            source.Construction.MaxFeedMultiplier += .5f;
            source.Effects.ColorTargetsPerWindow++;
            source.Progression.UpgradeEveryWaves++;
            source.Combat.MinimumActionInterval += .01f;
            source.Trials.Scenarios[0].Health++;
            source.Trials.BudgetChoices.Add(17);
            source.WellPlacements[0].ReservedRadius++;
            source.StructureDefinitions[0].Cost++;
            source.MaterialDefinitions[0].CostMultiplier += .25f;
            source.EnemyDefinitions[0].MaxHealth++;
            source.Waves[0].Count++;
            Assert.That(JsonUtility.ToJson(input), Is.EqualTo(expected));
            Assert.That(input.StructureDefinitions.Select(item => item.Kind), Is.EquivalentTo(new[] { StructureKind.Wall, StructureKind.Spikes }));

            var published = input.CreateBalance();
            string frozenJson = JsonUtility.ToJson(published);
            input.Mechanics.ColorCartridges = !input.Mechanics.ColorCartridges;
            input.Construction.MaxFeedMultiplier += .5f;
            input.Effects.ColorTargetsPerWindow++;
            input.Progression.UpgradeEveryWaves++;
            input.Combat.MinimumActionInterval += .01f;
            input.Trials.Scenarios[0].Health++;
            input.Trials.BudgetChoices.Add(17);
            input.WellPlacements[0].ReservedRadius++;
            input.StructureDefinitions[0].Cost++;
            input.MaterialDefinitions[0].CostMultiplier += .25f;
            input.EnemyDefinitions[0].MaxHealth++;
            input.Waves[0].Count++;
            Assert.That(JsonUtility.ToJson(published), Is.EqualTo(frozenJson));
        }

        [TestCase("Mechanics")] [TestCase("Construction")] [TestCase("Effects")]
        [TestCase("Progression")] [TestCase("Combat")] [TestCase("Trials")]
        [TestCase("WellPlacements")] [TestCase("StructureDefinitions")]
        [TestCase("MaterialDefinitions")] [TestCase("EnemyDefinitions")] [TestCase("Waves")]
        public void MissingSharedRuleGroupReportsItsExactAuthoringPath(string group)
        {
            typeof(GlobalBalanceSettings).GetField(group).SetValue(preset.rules, null);
            var error = Assert.Throws<ArgumentException>(() => preset.CreateBalance());
            Assert.That(TextResourcesAsset.Default.Resolve(error.Message), Does.Contain("preset.rules." + group));
        }

        [Test]
        public void ProductionPresetAllowsRemovingACanonicalWeaponWithoutRecreatingIt()
        {
            Assert.That(database.catalog.Weapons.RemoveAll(item => item.Id == "weapon.gun"), Is.EqualTo(1));
            database.catalog.StartingPages.RemoveAll(item => item.FirstWeaponId == "weapon.gun" || item.SecondWeaponId == "weapon.gun");
            preset.rules.Trials.BaselineWeaponId = "weapon.piercer";
            var balance = preset.CreateBalance();
            Assert.That(balance.Content.TryGetWeapon("weapon.gun", out _), Is.False);
            Assert.That(balance.StructureDefinitions.Any(item => item.Kind == StructureKind.Gun), Is.False);
            var available = new TowerBlueprint("remaining-weapon", "Remaining weapon", "chassis.standard", "weapon.piercer", content: balance.Content);
            Assert.That(TowerCatalog.Validate(available, balance, out string reason), Is.True, reason);
            Assert.That(TowerCatalog.Cost(available, balance, MaterialTier.Pencil), Is.GreaterThan(0));
            Assert.DoesNotThrow(() => new GameSession(balance, 7, false));
        }

        [TestCase("capacity")] [TestCase("disabled weapon")] [TestCase("disabled module")]
        [TestCase("locked weapon")] [TestCase("locked module")] [TestCase("locked chassis")]
        [TestCase("incompatible module")]
        public void InvalidConfiguredStartupPageFailsBeforePublication(string problem)
        {
            var published = preset.CreateBalance();
            var standard = database.catalog.Chassis.Single(item => item.Id == "chassis.standard");
            var gun = database.catalog.Weapons.Single(item => item.Id == "weapon.gun");
            var armor = database.catalog.Modules.Single(item => item.Id == "module.armor");
            switch (problem)
            {
                case "capacity": standard.Capacity = 0; break;
                case "disabled weapon": gun.Enabled = false; break;
                case "disabled module": armor.Enabled = false; break;
                case "locked weapon": gun.InitiallyUnlocked = false; break;
                case "locked module": armor.InitiallyUnlocked = false; break;
                case "locked chassis": standard.InitiallyUnlocked = false; break;
                case "incompatible module": armor.AllowedChassisIds.Clear(); armor.AllowedChassisIds.Add("chassis.heavy"); break;
            }
            var error = Assert.Throws<ArgumentException>(() => preset.CreateBalance());
            Assert.That(TextResourcesAsset.Default.Resolve(error.Message), Does.Contain("UnpublishedBoundaryCandidate.database.catalog.StartingPages"));
            Assert.That(published.Content.GetWeapon("weapon.gun").Enabled, Is.True);
            Assert.That(published.Content.GetChassis("chassis.standard").Capacity, Is.GreaterThan(0));
        }

        [Test]
        public void StartupValidationUsesPreparedSharedRulesRatherThanHistoricalDefaults()
        {
            database.catalog.Modules.Add(new ModuleDefinitionInput
            {
                Id = "module.startup-delay", Name = "Startup delay adjustment", InitiallyUnlocked = true,
                TemplateId = database.catalog.Modules[0].TemplateId,
                Modifiers = new List<StatModifierInput> { new StatModifierInput {
                    Stat = ModifierStat.AcquisitionSeconds, Operation = ModifierOperation.Add,
                    Scope = ModifierScope.AllWeapons, Value = -1 } }
            });
            database.catalog.StartingPages[0].Modules.Add(new StartingModuleDefinitionInput {
                InstanceId = "startup-delay", DefinitionId = "module.startup-delay" });
            preset.rules.Construction.AcquisitionSeconds = 2;
            var balance = preset.CreateBalance();
            Assert.DoesNotThrow(() => new WorkshopProfile(balance.Content));
            var profile = new WorkshopProfile(balance.Content, balance);
            Assert.That(TowerCatalog.AcquisitionSeconds(profile.GetLoadoutBlueprints()[0], balance), Is.EqualTo(1));
            preset.rules.Construction.AcquisitionSeconds = .5f;
            var error = Assert.Throws<ArgumentException>(() => preset.CreateBalance());
            Assert.That(TextResourcesAsset.Default.Resolve(error.Message), Does.Contain("StartingPages"));
            Assert.That(TowerCatalog.AcquisitionSeconds(profile.GetLoadoutBlueprints()[0], balance), Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void EmptyAndPartiallyAssignedStartupAlbumsAreSupported(bool assignOne)
        {
            var page = database.catalog.StartingPages[0];
            database.catalog.StartingPages.Clear();
            if (assignOne) { page.Slot = 2; database.catalog.StartingPages.Add(page); }
            var balance = preset.CreateBalance();
            var profile = new WorkshopProfile(balance.Content);
            Assert.That(profile.Album.Count, Is.EqualTo(assignOne ? 1 : 0));
            var slots = profile.GetLoadoutBlueprints();
            Assert.That(slots.Count, Is.EqualTo(ProfileLimits.LoadoutSlotCount));
            Assert.That(slots[0], Is.Null); Assert.That(slots[1], Is.Null); Assert.That(slots[3], Is.Null);
            Assert.That(slots[2]?.Id, Is.EqualTo(assignOne ? page.Id : null));
        }

        [TestCase("Chassis")] [TestCase("Weapons")]
        public void ProductionWorkshopRequiresAnEnabledChoiceForNewDrawing(string category)
        {
            database.catalog.StartingPages.Clear();
            if (category == "Chassis") foreach (var item in database.catalog.Chassis) item.Enabled = false;
            else foreach (var item in database.catalog.Weapons) item.Enabled = false;
            var error = Assert.Throws<ArgumentException>(() => preset.CreateBalance());
            Assert.That(TextResourcesAsset.Default.Resolve(error.Message), Does.Contain("UnpublishedBoundaryCandidate.database.catalog." + category));
        }

        string AddInvalidOverride(string category, bool unknown)
        {
            var copy = database.catalog.CreateCopy();
            string id;
            switch (category)
            {
                case "chassis":
                    var chassis = copy.Chassis[0]; id = chassis.Id;
                    if (unknown) chassis.Id = id = "chassis.unregistered";
                    preset.overrides.Chassis.Add(chassis); if (!unknown) preset.overrides.Chassis.Add(chassis);
                    break;
                case "weapon":
                    var weapon = copy.Weapons[0]; id = weapon.Id;
                    if (unknown) weapon.Id = id = "weapon.unregistered";
                    preset.overrides.Weapons.Add(weapon); if (!unknown) preset.overrides.Weapons.Add(weapon);
                    break;
                case "module":
                    var module = copy.Modules[0]; id = module.Id;
                    if (unknown) module.Id = id = "module.unregistered";
                    preset.overrides.Modules.Add(module); if (!unknown) preset.overrides.Modules.Add(module);
                    break;
                case "effect":
                    var effect = copy.Effects[0]; id = effect.Id;
                    if (unknown) effect.Id = id = "effect.unregistered";
                    preset.overrides.Effects.Add(effect); if (!unknown) preset.overrides.Effects.Add(effect);
                    break;
                case "template":
                    var template = copy.Templates[0]; id = template.Id;
                    if (unknown) template.Id = id = "template.unregistered";
                    preset.overrides.Templates.Add(template); if (!unknown) preset.overrides.Templates.Add(template);
                    break;
                default: throw new ArgumentException(category);
            }
            return id;
        }
    }
}
