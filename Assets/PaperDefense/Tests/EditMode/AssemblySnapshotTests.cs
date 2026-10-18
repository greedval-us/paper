using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class AssemblySnapshotTests
    {
        [Test] public void SavedRecipe_SurvivesLibraryEditsDeletionAndNewCatalogRevision()
        {
            var data = AssemblyResolverFixture.Input();
            var library = new List<WeaponRecipeInput> { data.Starters[0].Mounts[0].Weapon };
            var catalog = data.CreateSnapshot(); var saved = catalog.Starters[0].Snapshot;
            library[0].Mechanism.Id = "core.piercer"; library.Clear();
            AssemblyResolverFixture.Set(data, "core.gun", AssemblyStat.Cost, ModifierOperation.Add, 999);
            data.Definitions.Single(d => d.Id == "core.gun").Revision = 2;
            var current = new ComponentCatalog("album-v2", data.Definitions.Select(d => d.CreateSnapshot()));
            Assert.That(current.TryGet("core.gun", 1, out _), Is.False);
            var resolved = AssemblyResolver.Resolve(saved, GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(resolved.Valid, Is.True); Assert.That(resolved.Value.Cost, Is.EqualTo(40));
            Assert.That(resolved.Value.Snapshot.BalanceRevision, Is.EqualTo("album-v1.0"));
            Assert.That(resolved.Value.Weapons[0].Damage, Is.EqualTo(12));
        }

        [Test] public void ResolvedValues_DoNotRetainMutableBalanceOrAuthoringArrays()
        {
            var data = AssemblyResolverFixture.Input(); var balance = GameBalance.CreateDefault();
            var result = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(), data.CreateSnapshot(), balance, MaterialTier.Ink);
            balance.Material(MaterialTier.Ink).CostMultiplier = 99; balance.Material(MaterialTier.Ink).HealthMultiplier = 99;
            data.Definitions = new ComponentDefinitionInput[0]; data.Starters = new AssemblyStarterInput[0];
            Assert.That(result.Cost, Is.EqualTo(72)); Assert.That(result.Health, Is.EqualTo(115.5f).Within(.0001f));
            Assert.That(result.Weapons[0].Damage, Is.EqualTo(15));
            Assert.That(result.Weapons, Is.Not.InstanceOf<ResolvedWeapon[]>());
        }

        [Test] public void LegacyAdapter_PreservesExistingCostCooldownAndSequentialModifiers()
        {
            var data = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/PaperDefense/Configuration/Game/PlayerContent.asset").catalog.CreateCopy();
            data.Modules.Single(x => x.Id == "module.armor").Modifiers = new List<StatModifierInput> {
                new StatModifierInput { Stat = ModifierStat.FoundationHealth, Operation = ModifierOperation.Set, Scope = ModifierScope.Foundation, Value = 100 } };
            data.Modules.Single(x => x.Id == "module.feed").Modifiers = new List<StatModifierInput> {
                new StatModifierInput { Stat = ModifierStat.FoundationHealth, Operation = ModifierOperation.Multiply, Scope = ModifierScope.Foundation, Value = 2 } };
            var balance = GameBalance.CreateDefault(); balance.Content = data.CreateSnapshot();
            var modules = new[] { new ModuleSelection("a", "module.armor"), new ModuleSelection("b", "module.feed") };
            var blueprint = new TowerBlueprint("legacy", "Legacy", "chassis.standard", "weapon.gun", modules: modules, content: balance.Content);
            var result = LegacyAlbumAdapter.Resolve(new AlbumEntry("legacy", "Legacy", 1, blueprint), balance, MaterialTier.Marker);
            Assert.That(result.Valid, Is.True); Assert.That(result.Value.Kind, Is.EqualTo(AlbumEntryKind.Legacy));
            Assert.That(result.Value.Health, Is.EqualTo(840).Within(.0001f));
            Assert.That(result.Value.Cost, Is.EqualTo(TowerCatalog.Cost(blueprint, balance, MaterialTier.Marker)));
            Assert.That(result.Value.Weapons[0].Interval, Is.EqualTo(TowerCatalog.ActionInterval(blueprint, balance, "weapon.gun", TowerCatalog.FeedMultiplier(blueprint, balance))));
            var reversed = new TowerBlueprint("legacy2", "Legacy reversed", "chassis.standard", "weapon.gun", modules: modules.Reverse(), content: balance.Content);
            Assert.That(LegacyAlbumAdapter.Resolve(new AlbumEntry("legacy2", "Legacy reversed", 1, reversed), balance).Value.Health, Is.EqualTo(100));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyAdapter_DerivedOverflowIsDiagnosticBeforePublication(bool health)
        {
            var balance = GameBalance.CreateDefault();
            if (health) { balance.Construction.FoundationHealth = float.MaxValue / 8; balance.Construction.ArmorDefense = .8f; }
            else balance.Structure(StructureKind.Gun).Damage = float.MaxValue / 2;
            var blueprint = new TowerBlueprint("extreme", "Extreme legacy", ChassisKind.Standard, StructureKind.Gun, modules: health ? TowerModules.Armor : TowerModules.None);
            var result = LegacyAlbumAdapter.Resolve(new AlbumEntry("extreme", "Extreme legacy", 1, blueprint), balance, MaterialTier.Marker);
            Assert.That(result.Valid, Is.False); Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(x => x.Code), Does.Contain("assembly.number.nonfinite"));
        }

        [TestCase(true, "assembly.balance.missing")]
        [TestCase(false, "assembly.material.unknown")]
        public void LegacyAdapter_IncompleteBalanceReturnsDiagnostics(bool effects, string code)
        {
            var balance = GameBalance.CreateDefault();
            if (effects) balance.Effects = null; else balance.MaterialDefinitions[1] = null;
            var blueprint = new TowerBlueprint("legacy", "Legacy", ChassisKind.Standard, StructureKind.Gun);
            AssemblyResolution result = null;
            Assert.DoesNotThrow(() => result = LegacyAlbumAdapter.Resolve(new AlbumEntry("legacy", "Legacy", 1, blueprint), balance));
            Assert.That(result.Valid, Is.False); Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(x => x.Code), Does.Contain(code));
        }
    }
}
