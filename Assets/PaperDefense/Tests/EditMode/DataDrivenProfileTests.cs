using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DataDrivenProfileTests
    {
        static ContentCatalogInput Catalog(GameBalance balance)
        {
            var input = LegacyContentCatalog.CreateInput(balance);
            input.Templates.Add(new DrawingTemplateDefinitionInput {
                Id = "template.profile-test", DisplayName = "Profile test", Weight = 25,
                Contours = new List<DrawingContourInput> { new DrawingContourInput {
                    Points = new List<DrawingCoordinateInput> { new DrawingCoordinateInput(-.4f, 0), new DrawingCoordinateInput(.4f, 0) }
                } }
            });
            foreach (var entry in input.Chassis) entry.TemplateId = "template.profile-test";
            foreach (var entry in input.Weapons) entry.TemplateId = "template.profile-test";
            foreach (var entry in input.Modules) entry.TemplateId = "template.profile-test";
            input.Chassis.Add(new ChassisDefinitionInput { Id = "chassis.profile-test", Name = "New chassis", TemplateId = "template.profile-test",
                Capacity = 40, BaseCost = 29, BaseHealth = 80, InitiallyUnlocked = true });
            input.Weapons.Add(new WeaponDefinitionInput { Id = "weapon.profile-test", Name = "New single", TemplateId = "template.profile-test",
                Load = 3, UnlockGold = 19, Attack = AttackKind.Single,
                Stats = new StructureDefinition { Cost = 43, MaxHealth = 55, BuildSeconds = .5f, Range = 9, Damage = 7, ShotInterval = .4f } });
            input.Modules.Add(new ModuleDefinitionInput { Id = "module.profile-test", Name = "New module", TemplateId = "template.profile-test",
                Load = 2, Cost = 11, UnlockGold = 11, Repeatable = true,
                Modifiers = new List<StatModifierInput> { new StatModifierInput { Stat = ModifierStat.WeaponRange,
                    Operation = ModifierOperation.Multiply, Scope = ModifierScope.AllWeapons, Value = 1.1f } } });
            return input;
        }
        static DrawingSnapshot Artwork()
            => new DrawingSnapshot(new[] {
                new DrawingPart(DrawingBindings.Mount0, new[] { new DrawingStroke(new[] { new DrawingPoint(-.4f,0), new DrawingPoint(.4f,0) }) }),
                new DrawingPart(DrawingBindings.Mount1, new[] { new DrawingStroke(new[] { new DrawingPoint(0,-.3f), new DrawingPoint(0,.4f) }) }),
                new DrawingPart(DrawingBindings.Module("first-copy"), new[] { new DrawingStroke(new[] { new DrawingPoint(-.1f,0), new DrawingPoint(.2f,.3f) }) }),
                new DrawingPart(DrawingBindings.Module("second-copy"), new[] { new DrawingStroke(new[] { new DrawingPoint(.3f,-.1f), new DrawingPoint(-.3f,.2f) }) })
            });
        static TowerBlueprint CustomPage(ContentCatalogSnapshot content)
            => new TowerBlueprint("custom.ids", "ID page", "chassis.profile-test", "weapon.profile-test", "weapon.profile-test",
                new[] { new ModuleSelection("first-copy", "module.profile-test"), new ModuleSelection("second-copy", "module.profile-test") },
                Artwork(), DrawingOrigin.PlayerDrawing, content: content);

        [Test] public void NewContentAndTwoModuleInstancesRoundTripWithoutAddingEnumValuesOrBits()
        {
            var balance = GameBalance.CreateDefault(); balance.Content = Catalog(balance).CreateSnapshot();
            var profile = new WorkshopProfile(balance.Content); profile.GrantGold(100);
            Assert.That(profile.Unlock("weapon.profile-test", out var reason, balance), Is.True, reason);
            Assert.That(profile.Unlock("module.profile-test", out reason, balance), Is.True, reason);
            var page = CustomPage(balance.Content);
            Assert.That(profile.SaveBlueprint(page, out reason, balance), Is.True, reason);
            Assert.That(profile.AssignSlot(0, page.Id, out reason, balance), Is.True, reason);
            var restored = WorkshopProfile.FromData(profile.ToData());
            TowerBlueprint actual = restored.GetLoadoutBlueprints()[0];
            Assert.That(restored.Gold, Is.EqualTo(70));
            Assert.That(actual.ChassisId, Is.EqualTo(page.ChassisId));
            Assert.That(actual.FirstWeaponId, Is.EqualTo(actual.SecondWeaponId));
            Assert.That(actual.FirstWeapon, Is.EqualTo((StructureKind)(-1)));
            Assert.That(actual.Modules, Is.EqualTo(TowerModules.None));
            Assert.That(actual.ModuleSelections.Select(m => m.InstanceId), Is.EqualTo(new[] { "first-copy", "second-copy" }));
            Assert.That(actual.ModuleSelections.Select(m => m.DefinitionId), Is.All.EqualTo("module.profile-test"));
            Assert.That(actual.Drawing.GeometryHash, Is.EqualTo(page.Drawing.GeometryHash));
            Assert.That(actual.Drawing.GetPart(DrawingBindings.Mount0).Strokes[0].Points,
                Is.Not.EqualTo(actual.Drawing.GetPart(DrawingBindings.Mount1).Strokes[0].Points));
            Assert.That(actual.Assessment.Quality, Is.EqualTo(page.Assessment.Quality));
            Assert.That(actual.Drawing.FormatVersion, Is.EqualTo(3));
            Assert.That(actual.Drawing.BindingVersion, Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void RemovedOrDisabledDefinitionPreservesUnlocksGoldLinesSlotsAndFrozenQuality(bool removed)
        {
            var balance = GameBalance.CreateDefault(); var input = Catalog(balance); balance.Content = input.CreateSnapshot();
            var profile = new WorkshopProfile(balance.Content); profile.GrantGold(100);
            profile.Unlock("weapon.profile-test", out _, balance); profile.Unlock("module.profile-test", out _, balance);
            var page = CustomPage(balance.Content); Assert.That(profile.SaveBlueprint(page, out _, balance), Is.True);
            Assert.That(profile.AssignSlot(0, page.Id, out _, balance), Is.True);
            WorkshopProfileData data = profile.ToData();
            if (removed) input.Weapons.RemoveAll(w => w.Id == "weapon.profile-test");
            else input.Weapons.Single(w => w.Id == "weapon.profile-test").Enabled = false;
            balance.Content = input.CreateSnapshot();
            var restored = WorkshopProfile.FromData(data); restored.ConfigureContent(balance.Content);
            Assert.That(restored.ValidateOwned(restored.GetLoadoutBlueprints()[0], out var reason, balance), Is.False);
            Assert.That(reason, Is.Not.Empty);
            var roundTrip = WorkshopProfile.FromData(restored.CreateWorkingCopy().ToData());
            Assert.That(roundTrip.Gold, Is.EqualTo(profile.Gold));
            Assert.That(roundTrip.IsUnlocked("weapon.profile-test"), Is.True);
            Assert.That(roundTrip.Loadout, Is.EqualTo(profile.Loadout));
            var actual = roundTrip.GetLoadoutBlueprints()[0];
            Assert.That(actual.FirstWeaponId, Is.EqualTo("weapon.profile-test"));
            Assert.That(actual.Drawing.GeometryHash, Is.EqualTo(page.Drawing.GeometryHash));
            Assert.That(actual.Assessment.Quality, Is.EqualTo(page.Assessment.Quality));
        }

        [Test] public void UnknownStableRightsArePreservedAndSerializedInOrdinalOrder()
        {
            var data = new WorkshopProfile().ToData();
            string longId = "weapon." + new string('x', 100);
            data.Unlocked = data.Unlocked.Concat(new[] { "weapon.future", longId }).Reverse().ToArray();
            var copy = WorkshopProfile.FromData(data).CreateWorkingCopy().ToData();
            Assert.That(copy.Unlocked, Does.Contain("weapon.future")); Assert.That(copy.Unlocked, Does.Contain(longId));
            Assert.That(copy.Unlocked, Is.EqualTo(copy.Unlocked.OrderBy(id => id, StringComparer.Ordinal)));
        }

        [Test] public void FreshCatalogRightsAreAuthoritativeWhileLoadedRightsRemainPermanent()
        {
            var balance = GameBalance.CreateDefault(); var input = Catalog(balance);
            input.Weapons.Single(item => item.Id == "weapon.gun").InitiallyUnlocked = false;
            foreach (var page in input.StartingPages)
                if (page.FirstWeaponId == "weapon.gun" || page.SecondWeaponId == "weapon.gun") page.Slot = -1;
            balance.Content = input.CreateSnapshot();
            var fresh = new WorkshopProfile(balance.Content);
            Assert.That(fresh.IsUnlocked("weapon.gun"), Is.False);
            Assert.That(fresh.IsUnlocked("chassis.profile-test"), Is.True);
            Assert.That(fresh.GetLoadoutBlueprints().Count, Is.EqualTo(4));
            Assert.That(fresh.GetLoadoutBlueprints()[0], Is.Null);
            foreach (var page in fresh.GetLoadoutBlueprints())
            {
                if (page == null) continue;
                Assert.That(page.FirstWeaponId, Is.Not.EqualTo("weapon.gun"));
                Assert.That(fresh.ValidateOwned(page, out string reason, balance), Is.True, reason);
            }
            var loaded = WorkshopProfile.FromData(new WorkshopProfile().ToData());
            loaded.ConfigureContent(balance.Content);
            Assert.That(loaded.IsUnlocked("weapon.gun"), Is.True);
            Assert.That(loaded.GetLoadoutBlueprints()[0].FirstWeaponId, Is.EqualTo("weapon.gun"));
        }

        [Test] public void FreshCatalogWithoutCanonicalPartsUsesItsFourExplicitStartingPages()
        {
            var input = Catalog(GameBalance.CreateDefault());
            input.Chassis.RemoveAll(item => item.Id != "chassis.profile-test");
            input.Weapons.RemoveAll(item => item.Id != "weapon.profile-test");
            input.Modules.Clear();
            input.Weapons[0].InitiallyUnlocked = true;
            input.StartingPages.Clear();
            for (int i = 0; i < 4; i++) input.StartingPages.Add(new StartingBlueprintDefinitionInput {
                Id = "configured.start." + i, Name = "Configured start " + i,
                ChassisId = "chassis.profile-test", FirstWeaponId = "weapon.profile-test", Slot = i
            });
            var content = input.CreateSnapshot();
            var fresh = new WorkshopProfile(content);
            Assert.That(fresh.Loadout.Distinct().Count(), Is.EqualTo(4));
            Assert.That(fresh.IsUnlocked("weapon.gun"), Is.False);
            foreach (var page in fresh.GetLoadoutBlueprints())
            {
                Assert.That(page.ChassisId, Is.EqualTo("chassis.profile-test"));
                Assert.That(page.FirstWeaponId, Is.EqualTo("weapon.profile-test"));
                Assert.That(page.Id, Does.StartWith("configured.start."));
                Assert.That(fresh.ValidateOwned(page, out string reason), Is.True, reason);
            }
        }

        [Test] public void EmptyConfiguredAlbumAndUnassignedSlotsRoundTripWithoutInventingStarterPages()
        {
            var input = Catalog(GameBalance.CreateDefault()); input.StartingPages.Clear();
            var fresh = new WorkshopProfile(input.CreateSnapshot());
            Assert.That(fresh.Album, Is.Empty);
            Assert.That(fresh.GetLoadoutBlueprints(), Is.All.Null);
            var restored = WorkshopProfile.FromData(fresh.ToData());
            Assert.That(restored.Album, Is.Empty);
            Assert.That(restored.Loadout, Is.All.Null);
            Assert.That(restored.CreateWorkingCopy().Album, Is.Empty);
        }

        [Test] public void WorkingCopyDoesNotRerunPreparationOrResurrectRemovedStarterPages()
        {
            var profile = new WorkshopProfile();
            Assert.That(profile.SaveBlueprint(new TowerBlueprint("replacement", "Replacement", ChassisKind.Standard, StructureKind.Gun), out _), Is.True);
            Assert.That(profile.AssignSlot(0, "replacement", out _), Is.True);
            Assert.That(profile.RemoveBlueprint("starter.gun", out _), Is.True);
            var copy = profile.CreateWorkingCopy();
            Assert.That(copy.Album.Select(p => p.Id), Is.EqualTo(profile.Album.Select(p => p.Id)));
            Assert.That(copy.Loadout, Is.EqualTo(profile.Loadout));
        }
    }
}
