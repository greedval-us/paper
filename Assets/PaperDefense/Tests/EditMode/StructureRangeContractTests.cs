using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class StructureRangeContractTests
    {
        [TestCase(StructureKind.Wall)]
        [TestCase(StructureKind.Spikes)]
        public void ConstructionToolsHaveNoWeaponRange(StructureKind kind)
        {
            var balance = GameBalance.CreateDefault();
            // Tool geometry must never become a firing range or require a weapon catalog entry.
            balance.Structure(kind).Range = 17;
            Assert.That(TowerCatalog.WeaponRange(null, balance, kind), Is.Zero);
        }

        [TestCase(StructureKind.Gun)]
        [TestCase(StructureKind.Piercer)]
        [TestCase(StructureKind.Mortar)]
        [TestCase(StructureKind.Fan)]
        [TestCase(StructureKind.Ricochet)]
        [TestCase(StructureKind.Stapler)]
        [TestCase(StructureKind.Blotter)]
        public void StandaloneWeaponsKeepTheirCatalogRange(StructureKind kind)
        {
            var balance = GameBalance.CreateDefault();
            balance.Structure(kind).Range = 17;
            Assert.That(TowerCatalog.WeaponRange(null, balance, kind), Is.EqualTo(17));
        }

        [Test]
        public void UnknownWeaponIdsRemainValidationErrors()
        {
            Assert.Throws<ArgumentException>(() => TowerCatalog.WeaponRange(null, GameBalance.CreateDefault(), "weapon.unknown"));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void KindRangeUsesTheActualWeaponMountForScopedUpgrades(int mount)
        {
            var balance = GameBalance.CreateDefault();
            balance.Structure(StructureKind.Mortar).Range = 17;
            var input = LegacyContentCatalog.CreateInput(balance);
            input.Templates.Add(new DrawingTemplateDefinitionInput { Id = "template.range", DisplayName = "Range", Weight = 0 });
            foreach (var item in input.Chassis) { item.TemplateId = "template.range"; item.MountCount = 3; }
            foreach (var item in input.Weapons) item.TemplateId = "template.range";
            foreach (var item in input.Modules) item.TemplateId = "template.range";
            input.Modules.Add(new ModuleDefinitionInput {
                Id = "module.range", Name = "Range", TemplateId = "template.range",
                Modifiers = new List<StatModifierInput> { new StatModifierInput {
                    Stat = ModifierStat.WeaponRange, Operation = ModifierOperation.Add,
                    Scope = ModifierScope.Mount, MountIndex = mount, Value = 9
                } }
            });
            balance.Content = input.CreateSnapshot();
            var blueprint = new TowerBlueprint("range", "Range", "chassis.standard", new[] {
                new WeaponMountSelection(0, "weapon.gun"), new WeaponMountSelection(mount, "weapon.mortar")
            }, new[] { new ModuleSelection("range", "module.range") }, content: balance.Content);

            Assert.That(TowerCatalog.WeaponRange(blueprint, balance, StructureKind.Mortar), Is.EqualTo(26));
            Assert.That(TowerCatalog.WeaponRange(blueprint, balance, StructureKind.Gun), Is.EqualTo(balance.Structure(StructureKind.Gun).Range));
        }
    }
}
