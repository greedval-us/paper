using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class TrialCounterfireDefenseTests
    {
        [Test] public void CounterfireUsesSharedStructureDefenseWithoutIncreasingTowerHealth()
        {
            var balance = GameBalance.CreateDefault(); balance.EnableEnemyMechanics = true;
            balance.Construction.FoundationHealth = 500;
            balance.Construction.ArmorHealthMultiplier = 1;
            balance.Construction.ArmorDefense = 0;
            balance.Structure(StructureKind.Gun).Damage = 0;
            var blueprint = new TowerBlueprint("counterfire", "Armored trial", ChassisKind.Standard,
                StructureKind.Gun, modules: TowerModules.Armor);
            TrialReport plain = TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, 100, counterfire: true);
            balance.Construction.ArmorDefense = .6f;
            TrialReport armored = TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, 100, counterfire: true);
            int firstHit = -1;
            for (int i = 0; i < plain.Frames.Count; i++)
                if (plain.Frames[i].Towers[0].Health < 500) { firstHit = i; break; }
            Assert.That(firstHit, Is.GreaterThan(0), "The real training enemies must reach counterfire range.");
            var before = plain.Frames[firstHit].Towers[0]; var defended = armored.Frames[firstHit].Towers[0];
            Assert.That(before.Health, Is.GreaterThan(0), "The fixture records an ordinary nonfatal first counterfire volley.");
            Assert.That(before.MaxHealth, Is.EqualTo(500)); Assert.That(defended.MaxHealth, Is.EqualTo(500));
            Assert.That(defended.Health, Is.EqualTo(500 - (500 - before.Health) * .4f).Within(.001f));
        }
    }
}
