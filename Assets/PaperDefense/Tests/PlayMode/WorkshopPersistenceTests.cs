using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class WorkshopPersistenceTests
    {
        string directory, path;
        readonly List<GameObject> roots = new List<GameObject>();

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PaperWorkshopTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "profile.json");
        }

        [TearDown] public void TearDown()
        {
            foreach (GameObject root in roots) Object.DestroyImmediate(root);
            roots.Clear();
            string resolved = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved).StartsWith("PaperWorkshopTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        RunController Controller()
        {
            var root = new GameObject("Manual workshop persistence controller");
            root.SetActive(false); roots.Add(root);
            var controller = root.AddComponent<RunController>();
            controller.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
            return controller;
        }

        [Test] public void DiskRoundTripPreservesPaidUnlocksSavedAssemblyAndFourSlots()
        {
            var original = new WorkshopProfile(); original.GrantGold(500);
            Assert.That(original.Unlock("chassis.heavy", out _), Is.True);
            Assert.That(original.Unlock("module.tracking", out _), Is.True);
            var blueprint = new TowerBlueprint("saved", "Мой тяжёлый пулемёт", ChassisKind.Heavy,
                StructureKind.Gun, modules: TowerModules.Tracking);
            Assert.That(original.SaveBlueprint(blueprint, out _), Is.True);
            Assert.That(original.AssignSlot(0, blueprint.Id, out _), Is.True);
            Assert.That(WorkshopPersistence.Save(original, path), Is.True, WorkshopPersistence.LastError);
            original.GrantGold(7);
            Assert.That(WorkshopPersistence.Save(original, path), Is.True, WorkshopPersistence.LastError);
            var restored = WorkshopPersistence.Load(path);
            Assert.That(WorkshopPersistence.LastError, Is.Empty);
            Assert.That(restored.Gold, Is.EqualTo(original.Gold));
            Assert.That(restored.IsUnlocked("chassis.heavy"), Is.True);
            Assert.That(restored.IsUnlocked("module.tracking"), Is.True);
            Assert.That(restored.Loadout, Is.EqualTo(original.Loadout));
            Assert.That(restored.GetLoadoutBlueprints()[0].Chassis, Is.EqualTo(ChassisKind.Heavy));
            Assert.That(restored.GetLoadoutBlueprints()[0].Modules, Is.EqualTo(TowerModules.Tracking));
            Assert.That(File.Exists(path + ".bak"), Is.True);
        }

        [TestCase("{\"Version\":999,\"Gold\":10000}")]
        [TestCase("this is not json")]
        public void CorruptVersionOrSyntaxReturnsUsableStarterProfile(string json)
        {
            File.WriteAllText(path, json);
            LogAssert.Expect(LogType.Warning, new Regex("^Не удалось прочитать альбом:"));
            WorkshopProfile restored = WorkshopPersistence.Load(path);
            Assert.That(WorkshopPersistence.LastError, Is.Not.Empty);
            Assert.That(restored.Gold, Is.Zero);
            Assert.That(restored.Loadout.Count, Is.EqualTo(4));
            foreach (TowerBlueprint blueprint in restored.GetLoadoutBlueprints())
                Assert.That(restored.ValidateOwned(blueprint, out _), Is.True);
        }

        [Test] public void NegativeGoldAndBrokenSelectionAreRepairedWithoutGrantingLockedParts()
        {
            var data = new WorkshopProfile().ToData();
            data.Version = 1; // Legacy sanitisation; v2 drawing pages are loaded strictly.
            data.Gold = -100;
            data.Album = new[] { new BlueprintData { Id = "locked", Name = "Locked", Chassis = ChassisKind.Heavy,
                FirstWeapon = StructureKind.Gun, Modules = TowerModules.Tracking } };
            data.Loadout = new[] { "locked", "locked", "missing", "" };
            File.WriteAllText(path, JsonUtility.ToJson(data));
            WorkshopProfile restored = WorkshopPersistence.Load(path);
            Assert.That(WorkshopPersistence.LastError, Is.Empty);
            Assert.That(restored.Gold, Is.Zero);
            Assert.That(restored.IsUnlocked("chassis.heavy"), Is.False);
            Assert.That(restored.IsUnlocked("module.tracking"), Is.False);
            Assert.That(new HashSet<string>(restored.Loadout).Count, Is.EqualTo(4));
            foreach (TowerBlueprint blueprint in restored.GetLoadoutBlueprints())
                Assert.That(restored.ValidateOwned(blueprint, out _), Is.True);
        }

        [Test] public void TrialNeverSpendsRunInkOrGrantsGoldOrAdvancesTheBattle()
        {
            RunController controller = Controller();
            int gold = controller.Workshop.Gold, ink = controller.Session.Ink;
            long tick = controller.Session.TickIndex;
            int album = controller.Workshop.Album.Count;
            TrialComparison trial = controller.RunTrial(controller.Workshop.Loadout[0], TrialScenario.Crowd, true, 400);
            Assert.That(trial, Is.Not.Null);
            Assert.That(trial.First.Damage, Is.GreaterThan(0));
            Assert.That(controller.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(controller.Session.Ink, Is.EqualTo(ink));
            Assert.That(controller.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(controller.Workshop.Album.Count, Is.EqualTo(album));
            Assert.That(controller.AdvanceFrame(1, false, 2), Is.Zero);
        }

        [Test] public void StartingRunFreezesAssemblySelectionAndRejectsAllWorkshopEdits()
        {
            RunController controller = Controller();
            var selected = new TowerBlueprint("custom", "Два ствола", ChassisKind.Standard,
                StructureKind.Gun, StructureKind.Gun);
            Assert.That(controller.SaveBlueprint(selected, out _), Is.True);
            Assert.That(controller.AssignBlueprint(0, selected.Id, out _), Is.True);
            Assert.That(controller.StartRun(out _), Is.True);
            var replacement = new TowerBlueprint(selected.Id, "Замена", ChassisKind.Standard, StructureKind.Piercer);
            Assert.That(controller.SaveBlueprint(replacement, out _), Is.False);
            Assert.That(controller.AssignBlueprint(0, controller.Workshop.Loadout[2].Id, out _), Is.False);
            Assert.That(controller.DeleteBlueprint(selected.Id, out _), Is.False);
            Assert.That(controller.UnlockPart("module.feed", out _), Is.False);
            Assert.That(controller.RunTrial(selected, TrialScenario.Fast), Is.Null);
            int inkBeforeRejectedBuild = controller.Session.Ink;
            int structuresBeforeRejectedBuild = controller.Session.Structures.Count;
            Assert.That(controller.Build(new[] { new Cell(8, 10) }, StructureKind.Gun,
                MaterialTier.Pencil, out _), Is.False);
            Assert.That(controller.Session.Ink, Is.EqualTo(inkBeforeRejectedBuild));
            Assert.That(controller.Session.Structures.Count, Is.EqualTo(structuresBeforeRejectedBuild));
            Assert.That(controller.Build(new[] { new Cell(8, 10) }, StructureKind.Wall,
                MaterialTier.Pencil, out string wallReason), Is.True, wallReason);
            Assert.That(controller.Session.Ink, Is.LessThan(inkBeforeRejectedBuild));
            Assert.That(controller.Session.Structures.Count, Is.EqualTo(structuresBeforeRejectedBuild + 1));
            Assert.That(controller.BuildBlueprint(new[] { new Cell(10, 10) }, 0,
                MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            TowerBlueprint deployed = controller.Session.StructureAt(new Cell(10, 10)).Value.Blueprint;
            Assert.That(deployed.FirstWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(deployed.SecondWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(controller.Workshop.Loadout[0].Name, Is.EqualTo(selected.Name));
        }

        [Test] public void WorkshopPresentationOnlyExposesImmutableAssembliesAndReadOnlyCollections()
        {
            RunController controller = Controller();
            Assert.That(typeof(WorkshopReadModel).GetFields(), Is.Empty);
            foreach (var property in typeof(WorkshopReadModel).GetProperties())
                Assert.That(property.GetSetMethod(), Is.Null, property.Name);
            foreach (var property in typeof(TowerBlueprint).GetProperties())
                Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            foreach (var property in typeof(PartUnlock).GetProperties())
                Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            var album = (IList<TowerBlueprint>)controller.Workshop.Album;
            Assert.Throws<NotSupportedException>(() => album[0] = new TowerBlueprint("mutated", "Mutated",
                ChassisKind.Standard, StructureKind.Mortar));
            var loadout = (IList<TowerBlueprint>)controller.Workshop.Loadout;
            Assert.Throws<NotSupportedException>(() => loadout[0] = controller.Workshop.Loadout[1]);
            Assert.That(controller.Workshop.Loadout[0].Id, Is.EqualTo("starter.gun"));
        }

        [Test] public void InvalidWeaponPreviewReturnsTheCoreRejectionReason()
        {
            RunController controller = Controller();
            TowerPreview preview = controller.PreviewBlueprint(new TowerBlueprint("invalid", "Некорректный",
                ChassisKind.Standard, (StructureKind)999));
            Assert.That(preview.Valid, Is.False);
            Assert.That(preview.Reason, Is.Not.Empty);
        }
    }
}
