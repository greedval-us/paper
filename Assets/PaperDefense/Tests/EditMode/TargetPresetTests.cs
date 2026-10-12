using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class TargetPresetTests
    {
        [Test] public void TargetTransformOwnsItsRulesAndPreservesHistoricalInput()
        {
            var historical = GameBalance.CreateDefault();
            var target = GameBalancePresets.ApplyTargetWorkshop(historical);
            Assert.That(target.Preset, Is.EqualTo(GameBalancePreset.Target));
            Assert.That(target.PassiveInkPerSecond, Is.Zero); Assert.That(target.EarlyLaunchInkPerSecond, Is.Zero);
            Assert.That(target.MaxEarlyLaunchInk, Is.Zero);
            for (int i = 0; i < target.Waves.Count; i++) Assert.That(target.Waves[i].InkGrant, Is.EqualTo(140 + 20 * i));
            foreach (var material in target.MaterialDefinitions) Assert.That(material.DamageMultiplier, Is.EqualTo(1));
            Assert.That(target.Construction.ArmorHealthMultiplier, Is.EqualTo(1)); Assert.That(target.Construction.ArmorDefense, Is.EqualTo(.25f));
            Assert.That(target.Construction.DynamicFeed, Is.True); Assert.That(target.Construction.DynamicFeedCapacity, Is.EqualTo(1.6f));
            Assert.That(target.Effects.MaximumDefense, Is.EqualTo(.6f)); Assert.That(target.Effects.LimitTowerColor, Is.True);
            Assert.That(target.Effects.ColorWindowSeconds, Is.EqualTo(1)); Assert.That(target.Effects.ColorTargetsPerWindow, Is.EqualTo(3));
            Assert.That(target.Progression.RequireAssignedEnemyRewards, Is.True);
            Assert.That(target.Progression.SalvageSupplyMultiplier, Is.EqualTo(1));
            Assert.That(target.Progression.SalvageWaveRewardMultiplier, Is.EqualTo(1.25f));
            Assert.That(historical.Preset, Is.EqualTo(GameBalancePreset.Historical)); Assert.That(historical.PassiveInkPerSecond, Is.EqualTo(1.8f));
            Assert.That(historical.Material(MaterialTier.Marker).DamageMultiplier, Is.EqualTo(1.6f));
            Assert.That(historical.Construction.ArmorHealthMultiplier, Is.EqualTo(1.5f)); Assert.That(historical.Construction.DynamicFeed, Is.False);
            Assert.That(historical.Effects.MaximumDefense, Is.EqualTo(.95f)); Assert.That(historical.Effects.LimitTowerColor, Is.False);
            Assert.That(historical.Progression.RequireAssignedEnemyRewards, Is.False);
            Assert.That(historical.Waves[0].InkGrant, Is.EqualTo(-1));
            target.Construction.ArmorDefense = .4f; Assert.That(historical.Construction.ArmorDefense, Is.Zero);
        }
        [Test] public void AdjustableTargetValuesAndWaveRewardsSurviveOwnedSnapshots()
        {
            var settings = new TargetPresetSettings { WaveInkBase = 90, WaveInkIncrement = 5, ArmorDefense = .3f,
                ColorTargetsPerWindow = 2, DynamicFeedCapacity = 1.4f, SalvageWaveRewardMultiplier = 1.1f };
            var target = GameBalancePresets.CreateTargetWorkshop(settings); var copy = target.CreateSnapshot();
            settings.WaveInkBase = 999; target.Waves[0].InkGrant = 999; target.Construction.ArmorDefense = .5f;
            Assert.That(copy.Waves[0].InkGrant, Is.EqualTo(90)); Assert.That(copy.Waves[7].InkGrant, Is.EqualTo(125));
            Assert.That(copy.Construction.ArmorDefense, Is.EqualTo(.3f)); Assert.That(copy.Construction.DynamicFeedCapacity, Is.EqualTo(1.4f));
            Assert.That(copy.Effects.ColorTargetsPerWindow, Is.EqualTo(2)); Assert.That(copy.Progression.SalvageWaveRewardMultiplier, Is.EqualTo(1.1f));
            Assert.That(copy.StartingInk, Is.EqualTo(GameBalancePresets.DefaultWorkshopStartingInk));
        }
        [Test] public void ExplicitWaveRewardAndStartingInkOverrideTheTargetPattern()
        {
            var source = GameBalance.CreateDefault(); source.Waves[2].InkGrant = 777; source.StartingInk = 456;
            var target = GameBalancePresets.ApplyTargetWorkshop(source);
            Assert.That(target.Waves[2].InkGrant, Is.EqualTo(777));
            Assert.That(target.Waves[0].InkGrant, Is.EqualTo(140)); Assert.That(target.Waves[3].InkGrant, Is.EqualTo(200));
            Assert.That(target.StartingInk, Is.EqualTo(456)); Assert.That(source.Waves[0].InkGrant, Is.EqualTo(-1));
        }
        [Test] public void ArmorDefenseAndTargetHealthComeFromTheSharedCatalog()
        {
            var balance = GameBalancePresets.CreateTargetWorkshop();
            var armor = new TowerBlueprint("armor", "Броня", ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Armor);
            var plain = new TowerBlueprint("plain", "Основание", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(TowerCatalog.Defense(armor, balance), Is.EqualTo(.25f)); Assert.That(TowerCatalog.Defense(plain, balance), Is.Zero);
            Assert.That(TowerCatalog.Health(armor, balance, MaterialTier.Pencil), Is.EqualTo(55));
        }
        [Test] public void TargetSettingsCloneIsIndependentAndRejectsInvalidFractionsAndWindows()
        {
            var settings = new TargetPresetSettings(); var copy = settings.CreateSnapshot(); copy.ArmorDefense = .1f;
            Assert.That(settings.ArmorDefense, Is.EqualTo(.25f));
            settings.ColorWindowSeconds = float.NaN;
            Assert.That(() => settings.CreateSnapshot(), Throws.InstanceOf<System.ArgumentException>());
        }
    }
}
