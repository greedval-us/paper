using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class StandardWorkshopFlowTests
    {
        GameObject root, events;
        RunController run;
        WorkshopWizard wizard;
        Store store;
        int changes;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Isolated standard workshop flow", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 1000);
            events = new GameObject("Isolated standard workshop events", typeof(EventSystem));
            var host = new GameObject("Isolated standard workshop controller");
            host.transform.SetParent(root.transform, false); host.SetActive(false);
            run = host.AddComponent<RunController>(); store = new Store();
            run.InitializeWorkshop(store, allowLegacyFixtureDefaults: true);
            wizard = new WorkshopWizard(root.transform, run, null, _ => changes++, () => { });
            Canvas.ForceUpdateCanvases();
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(events);
        }

        static string Snapshot(TowerBlueprint page) => JsonUtility.ToJson(new BlueprintData(page));
        string ProfileState() => run.Workshop.Gold + ";" +
            string.Join("|", run.Workshop.Album.Select(Snapshot)) + ";" +
            string.Join("|", run.Workshop.Loadout.Select(page => page?.Id)) + ";" +
            string.Join("|", run.Workshop.Parts.Where(part => run.Workshop.IsUnlocked(part.Id)).Select(part => part.Id));
        void AssertStandard(string action)
        {
            Assert.That(wizard.Draft.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard), action);
            Assert.That(wizard.Draft.Drawing.IsEmpty, Is.True, action);
            Assert.That(wizard.Draft.Drawing.StrokeCount, Is.Zero, action);
            Assert.That(wizard.Draft.FreeformDesign, Is.Null, action);
            Assert.That(wizard.Drawing == null || !wizard.Drawing.Root.activeInHierarchy, Is.True, action);
        }
        object Command(string name, params object[] args)
        {
            var method = typeof(WorkshopWizard).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Exercise the wizard command behind its UI: " + name);
            return method.Invoke(wizard, args);
        }
        void Review()
        {
            Assert.That(wizard.Next(), Is.True, wizard.Status);
            Assert.That(wizard.Next(), Is.True, wizard.Status);
            Assert.That(wizard.Step, Is.EqualTo(2));
        }

        [Test] public void StandardOriginSurvivesEveryFoundationWeaponAndModuleMutation()
        {
            int writes = store.Writes, gold = run.Workshop.Gold;
            AssertStandard("NewDraft");
            Assert.That(wizard.SelectChassis("chassis.heavy"), Is.True); AssertStandard("foundation");
            Assert.That(wizard.SelectWeapon(0, "weapon.piercer"), Is.True); AssertStandard("primary weapon");
            Assert.That(wizard.SelectWeapon(1, "weapon.gun"), Is.True); AssertStandard("second weapon");
            var twoMountAssembly = wizard.Draft;
            Assert.That(wizard.SelectWeapon(2, "weapon.mortar"), Is.False, "The legacy heavy foundation has only two mounts.");
            Assert.That(wizard.Draft, Is.SameAs(twoMountAssembly)); AssertStandard("reject third weapon");
            Assert.That(wizard.SelectWeapon(2, null), Is.False, "An absent third mount cannot be removed.");
            Assert.That(wizard.Draft, Is.SameAs(twoMountAssembly)); AssertStandard("reject absent mount removal");
            Assert.That(wizard.SelectWeapon(1, null), Is.True); AssertStandard("remove second weapon");
            Assert.That(wizard.ChangeModule("module.armor"), Is.True); AssertStandard("add module");
            Assert.That(wizard.ChangeModule("module.armor", remove: true), Is.True); AssertStandard("remove module");
            var required = wizard.Draft;
            Assert.That(wizard.SelectWeapon(0, null), Is.False);
            Assert.That(wizard.Draft, Is.SameAs(required), "The primary mount remains mandatory.");
            Assert.That(store.Writes, Is.EqualTo(writes)); Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
        }

        [Test] public void SaveVariantAndLoadRetainStandardAssemblyWithoutOpeningDrawing()
        {
            Review(); string firstId = wizard.Draft.Id;
            Assert.That(wizard.Save(), Is.True, wizard.Status); int writes = store.Writes;
            Assert.That(wizard.SaveVariant(), Is.True, wizard.Status); AssertStandard("saved variant");
            Assert.That(wizard.Draft.Id, Is.Not.EqualTo(firstId));
            Assert.That(store.Writes, Is.EqualTo(writes + 1));
            string variant = Snapshot(wizard.Draft);
            var saved = run.Workshop.Album.Single(page => page.Id == wizard.Draft.Id);
            Assert.That(Snapshot(saved), Is.EqualTo(variant));
            wizard.NewDraft(); AssertStandard("separate new draft");
            wizard.Load(saved);
            Assert.That(wizard.Step, Is.EqualTo(2)); Assert.That(Snapshot(wizard.Draft), Is.EqualTo(variant));
            AssertStandard("load standard variant");
            Assert.That(wizard.GoToStep(1), Is.True, "A standard saved page remains editable.");
            Assert.That(wizard.SelectWeapon(0, "weapon.piercer"), Is.True); AssertStandard("edit loaded variant");
        }

        TowerBlueprint Historical(DrawingOrigin origin)
        {
            var source = new TowerBlueprint("historical-" + origin, "Historical page", ChassisKind.Heavy,
                StructureKind.Mortar, StructureKind.Fan, TowerModules.Armor, origin: origin).WithContent(run.Content);
            var drawing = new DrawingEditSession(source);
            Assert.That(drawing.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f, -.35f),
                new DrawingPoint(.35f, .35f), out var reason), Is.True, reason);
            foreach (var mount in source.WeaponMounts)
                Assert.That(drawing.AddRectangle(DrawingBindings.Mount(mount.SlotIndex), new DrawingPoint(-.0625f, 0),
                    new DrawingPoint(.0625f, .6f), out reason), Is.True, reason);
            return source.WithDrawing(drawing.Snapshot);
        }

        [TestCase(DrawingOrigin.PlayerDrawing)]
        [TestCase(DrawingOrigin.Freeform)]
        public void HistoricalLoadOpensReviewAndPreservesOriginalGeometryAndRules(DrawingOrigin origin)
        {
            var source = Historical(origin); string before = Snapshot(source);
            int writes = store.Writes;
            wizard.Load(source);
            Assert.That(wizard.Step, Is.EqualTo(2));
            Assert.That(Snapshot(wizard.Draft), Is.EqualTo(before));
            Assert.That(wizard.Draft.Origin, Is.EqualTo(origin));
            Assert.That(wizard.Draft.Drawing.IsEmpty, Is.False);
            Assert.That(wizard.Drawing == null || !wizard.Drawing.Root.activeInHierarchy, Is.True);
            Assert.That(root.transform.Find("Wizard review"), Is.Not.Null);
            Assert.That(store.Writes, Is.EqualTo(writes), "Reading historical data must never rewrite the profile.");
            Assert.That(root.GetComponentsInChildren<UnityEngine.UI.InputField>(true)
                .Where(field => field.gameObject.activeInHierarchy).All(field => !field.IsInteractable()), Is.True,
                "A historical name must be read-only along with its composition.");
        }

        [TestCase(DrawingOrigin.PlayerDrawing, "Back")]
        [TestCase(DrawingOrigin.PlayerDrawing, "GoToFoundation")]
        [TestCase(DrawingOrigin.PlayerDrawing, "GoToWeapons")]
        [TestCase(DrawingOrigin.PlayerDrawing, "SelectChassis")]
        [TestCase(DrawingOrigin.PlayerDrawing, "SelectWeapon")]
        [TestCase(DrawingOrigin.PlayerDrawing, "ChangeModule")]
        [TestCase(DrawingOrigin.PlayerDrawing, "Next")]
        [TestCase(DrawingOrigin.PlayerDrawing, "Save")]
        [TestCase(DrawingOrigin.PlayerDrawing, "SaveVariant")]
        [TestCase(DrawingOrigin.PlayerDrawing, "Mortar")]
        [TestCase(DrawingOrigin.PlayerDrawing, "Volley")]
        [TestCase(DrawingOrigin.PlayerDrawing, "Armor")]
        [TestCase(DrawingOrigin.PlayerDrawing, "Rename")]
        [TestCase(DrawingOrigin.PlayerDrawing, "EditorCallback")]
        [TestCase(DrawingOrigin.Freeform, "Back")]
        [TestCase(DrawingOrigin.Freeform, "GoToFoundation")]
        [TestCase(DrawingOrigin.Freeform, "GoToWeapons")]
        [TestCase(DrawingOrigin.Freeform, "SelectChassis")]
        [TestCase(DrawingOrigin.Freeform, "SelectWeapon")]
        [TestCase(DrawingOrigin.Freeform, "ChangeModule")]
        [TestCase(DrawingOrigin.Freeform, "Next")]
        [TestCase(DrawingOrigin.Freeform, "Save")]
        [TestCase(DrawingOrigin.Freeform, "SaveVariant")]
        [TestCase(DrawingOrigin.Freeform, "Mortar")]
        [TestCase(DrawingOrigin.Freeform, "Volley")]
        [TestCase(DrawingOrigin.Freeform, "Armor")]
        [TestCase(DrawingOrigin.Freeform, "Rename")]
        [TestCase(DrawingOrigin.Freeform, "EditorCallback")]
        public void HistoricalCommandsCannotEditSaveOrDuplicateThePage(DrawingOrigin origin, string command)
        {
            wizard.Load(Historical(origin));
            var loaded = wizard.Draft; string draft = Snapshot(loaded);
            string profile = ProfileState(); int writes = store.Writes, callbacks = changes;
            switch (command)
            {
                case "Back": wizard.Back(); break;
                case "GoToFoundation": Assert.That(wizard.GoToStep(0), Is.False); break;
                case "GoToWeapons": Assert.That(wizard.GoToStep(1), Is.False); break;
                case "SelectChassis": Assert.That(wizard.SelectChassis("chassis.standard"), Is.False); break;
                case "SelectWeapon": Assert.That(wizard.SelectWeapon(0, "weapon.gun"), Is.False); break;
                case "ChangeModule": Assert.That(wizard.ChangeModule("module.feed"), Is.False); break;
                case "Next": Assert.That(wizard.Next(), Is.False); break;
                case "Save": Assert.That(wizard.Save(), Is.False); break;
                case "SaveVariant": Assert.That(wizard.SaveVariant(), Is.False); break;
                case "Mortar": Command("OpenGeometry", 0); break;
                case "Volley": Command("OpenGeometry", 1); break;
                case "Armor": Command("OpenArmor"); break;
                case "Rename": Command("Rename", "Changed historical name"); break;
                case "EditorCallback": Command("SetDraft", loaded.WithName("Changed by an old editor"), false); break;
                default: throw new ArgumentOutOfRangeException(nameof(command));
            }
            Assert.That(wizard.Step, Is.EqualTo(2), command);
            Assert.That(wizard.Draft, Is.SameAs(loaded), command);
            Assert.That(Snapshot(wizard.Draft), Is.EqualTo(draft), command);
            Assert.That(ProfileState(), Is.EqualTo(profile), command);
            Assert.That(store.Writes, Is.EqualTo(writes), command);
            Assert.That(changes, Is.EqualTo(callbacks), command);
            Assert.That(wizard.Mortar == null || !wizard.Mortar.IsOpen, Is.True, command);
            Assert.That(wizard.Volley == null || !wizard.Volley.IsOpen, Is.True, command);
            Assert.That(wizard.Armor == null || !wizard.Armor.IsOpen, Is.True, command);
            Assert.That(wizard.Drawing == null || !wizard.Drawing.Root.activeInHierarchy, Is.True, command);
        }

        [TestCase(DrawingOrigin.PlayerDrawing)]
        [TestCase(DrawingOrigin.Freeform)]
        public void SeparateNewDraftLeavesHistoricalDataIntactAndRestoresStandardEditing(DrawingOrigin origin)
        {
            var historical = Historical(origin); string before = Snapshot(historical);
            wizard.Load(historical); wizard.NewDraft();
            Assert.That(wizard.Step, Is.Zero); Assert.That(wizard.Draft.Id, Is.Not.EqualTo(historical.Id));
            AssertStandard("new draft after historical review");
            Assert.That(wizard.SelectChassis("chassis.standard"), Is.True);
            Assert.That(wizard.SelectWeapon(0, "weapon.gun"), Is.True);
            Assert.That(Snapshot(historical), Is.EqualTo(before));
            Assert.That(store.Writes, Is.Zero);
        }

        sealed class Store : IWorkshopProfileStore
        {
            public int Writes;
            public ProfileLoadResult Load()
            {
                var profile = new WorkshopProfile(); profile.GrantGold(10000);
                foreach (var part in profile.Parts)
                    Assert.That(profile.Unlock(part.Id, out var reason), Is.True, reason);
                return new ProfileLoadResult(profile, ProfileLoadSource.Primary, "Isolated standard flow", true);
            }
            public ProfileSaveResult Save(WorkshopProfile value)
            {
                Writes++; return new ProfileSaveResult(true, "Saved isolated standard flow");
            }
        }
    }
}
