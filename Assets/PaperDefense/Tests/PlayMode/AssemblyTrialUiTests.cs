#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AssemblyTrialUiTests
    {
        AssemblyCommandFixture fixture;
        GameObject canvas, events;
        CompositeWorkshopPanel panel;
        [Serializable] sealed class FitEnvelope { public AssemblyFitCatalogInput fits; }

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(gold: 94, xp: 390);
            canvas = new GameObject("Isolated assembly trial canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
            events = new GameObject("Isolated assembly trial events", typeof(EventSystem));
            panel = new CompositeWorkshopPanel(canvas.transform, fixture.Run, null, null);
            panel.Reflow(1280, 720);
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(canvas); Object.DestroyImmediate(events); fixture.Dispose();
        }

        Button Button(string name) => panel.Root.GetComponentsInChildren<Button>(true).SingleOrDefault(button => button.name == name);
        void Click(string name)
        {
            var button = Button(name);
            Assert.That(button, Is.Not.Null, name + " must be reachable.");
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Canvas.ForceUpdateCanvases();
        }
        Text Text(string name) => panel.Root.GetComponentsInChildren<Text>(true).SingleOrDefault(text => text.name == name);
        TrialLaneSketch Lane => panel.Root.GetComponentInChildren<TrialLaneSketch>();
        object Report
        {
            get
            {
                var property = typeof(CompositeWorkshopPanel).GetProperty("LastAssemblyTrial", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(property, Is.Not.Null, "Shared reports must be available for towers and support.");
                return property.GetValue(panel);
            }
        }
        static object Value(object report, string name)
        {
            Assert.That(report, Is.Not.Null);
            var property = report.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(report);
        }
        AlbumEntry Support(AssemblyStructureFamily family) => fixture.Catalog.Starters.Single(entry =>
            entry.Kind == AlbumEntryKind.Structure && entry.Snapshot.Recipe.Structure.Family == family);
        void Open(AlbumEntry entry) { panel.OpenEntry(entry); Click("Composite trial"); }
        static string Json(AlbumEntry entry) => JsonUtility.ToJson(new AlbumEntryData(entry));

        [Test] public void OpeningTrialDoesNotInventMeasuredResultsOrStartSimulation()
        {
            Open(fixture.Gun());
            Assert.That(panel.LastTrial, Is.Null, "Opening the field is distinct from running it.");
            Assert.That(Report, Is.Null);
            Assert.That(Lane, Is.Not.Null);
            Assert.That(Lane.HasRecording, Is.False);
            Assert.That(Button("Composite trial run").IsInteractable(), Is.True);
            Assert.That(Button("Composite trial pause").IsInteractable(), Is.False);
            Assert.That(Text("Composite trial measured result").text, Does.Not.Match(@"[0-9]"));
        }

        [Test] public void GeneratorTrial_DoesNotAwardProfile()
        {
            var before = fixture.SavedData;
            string state = fixture.State(); byte[] bytes = fixture.SavedBytes;
            int writes = fixture.Store.SaveCalls, loads = fixture.Store.LoadCalls;
            Open(Support(AssemblyStructureFamily.Generator)); Click("Composite trial run");
            Assert.That(Report, Is.Not.Null);
            Assert.That(Value(Report, "Income"), Is.GreaterThan(0));
            Assert.That(Value(Report, "PaidWaves"), Is.GreaterThan(0));
            Assert.That(Value(Report, "Damage"), Is.Null, "Generator income is not DPS.");
            Click("Composite trial pause"); Click("Composite trial restart"); Click("Composite close trial");
            var after = fixture.Run.Workshop;
            Assert.That(after.Gold, Is.EqualTo(before.Gold));
            Assert.That(after.TotalXp, Is.EqualTo(before.TotalXp));
            Assert.That(JsonUtility.ToJson(fixture.SavedData), Is.EqualTo(JsonUtility.ToJson(before)), "Reward and operation receipts must remain identical.");
            Assert.That(fixture.State(), Is.EqualTo(state));
            Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
            Assert.That(fixture.Store.LoadCalls, Is.EqualTo(loads));
            Assert.That(fixture.Run.InWorkshop, Is.True);
        }

        [UnityTest] public IEnumerator PauseAndRestartUseTheSameSeedAndKeepTheUnsavedDraft()
        {
            panel.OpenEntry(fixture.Gun()); panel.DraftSession.Rename("Имя без сохранения"); panel.RefreshStatus();
            var session = panel.DraftSession; var draft = session.Draft;
            string before = Json(draft), state = fixture.State(); int writes = fixture.Store.SaveCalls;
            Click("Composite trial"); Click("Composite trial run");
            var report = Report; var recording = panel.LastTrial;
            Assert.That(Value(report, "Seed"), Is.EqualTo(1337));
            Click("Composite trial pause");
            Assert.That(Lane.PlaybackPaused, Is.True);
            float frozen = Lane.PlaybackSeconds;
            yield return new WaitForSecondsRealtime(.035f);
            Assert.That(Lane.PlaybackSeconds, Is.EqualTo(frozen).Within(.0001f));
            Click("Composite trial pause");
            Assert.That(Lane.PlaybackPaused, Is.False);
            Click("Composite trial restart");
            Assert.That(Report, Is.Not.SameAs(report));
            Assert.That(Value(Report, "Seed"), Is.EqualTo(1337));
            Assert.That(Value(Report, "Budget"), Is.EqualTo(400));
            Assert.That(panel.LastTrial.Damage, Is.EqualTo(recording.Damage));
            Assert.That(panel.LastTrial.Frames.SelectMany(frame => frame.Events).Select(item => item.Kind + ":" + item.SimulationSeconds + ":" + item.SourceStructureId),
                Is.EqualTo(recording.Frames.SelectMany(frame => frame.Events).Select(item => item.Kind + ":" + item.SimulationSeconds + ":" + item.SourceStructureId)));
            Click("Composite close trial");
            Assert.That(panel.DraftSession, Is.SameAs(session));
            Assert.That(panel.DraftSession.Draft, Is.SameAs(draft));
            Assert.That(Json(draft), Is.EqualTo(before));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Имя без сохранения"));
            Assert.That(panel.Step, Is.EqualTo(2));
            Assert.That(fixture.State(), Is.EqualTo(state));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void ChangingScenarioOrBudgetClearsThePreviousMeasurement()
        {
            Open(fixture.Gun()); Click("Composite trial run");
            Assert.That(Report, Is.Not.Null);
            Click("Composite trial scenario Armored");
            Assert.That(Report, Is.Null); Assert.That(Lane.HasRecording, Is.False);
            Click("Composite trial run");
            Assert.That(Value(Report, "Scenario").ToString(), Is.EqualTo("Armored"));
            Click("Composite trial budget");
            Assert.That(Report, Is.Null); Assert.That(panel.LastTrial, Is.Null);
            Click("Composite trial run");
            Assert.That(Value(Report, "Budget"), Is.EqualTo(800));
            Assert.That((int)Value(Report, "Cost"), Is.LessThanOrEqualTo(800));
        }

        [TestCase(AssemblyStructureFamily.Repair, "RepairedHealth")]
        [TestCase(AssemblyStructureFamily.Generator, "Income")]
        public void SupportResultsShowTheirApplicableProductionMetric(AssemblyStructureFamily family, string metric)
        {
            Open(Support(family)); Click("Composite trial run");
            Assert.That(Value(Report, metric), Is.GreaterThan(0));
            Assert.That(Value(Report, "Damage"), Is.Null);
            Assert.That(Text("Composite trial measured result").text, Does.Not.Contain("DPS"));
            Assert.That(Text("Composite trial conditions").text, Does.Contain("400").And.Contain("1337"));
        }

        [Test] public void TrialModalBlocksBackgroundSaveAndRestoresReviewFocusOnReturn()
        {
            panel.OpenEntry(fixture.Gun());
            var save = Button("Composite save");
            Click("Composite trial");
            Assert.That(save.IsInteractable(), Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(Lane.transform.parent.parent), Is.True);
            Click("Composite close trial");
            Assert.That(Button("Composite save").IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("Composite trial").gameObject));
        }

        [Test] public void UncoveredBlotterDraftCannotBypassTheVisualFitGate()
        {
            Assert.That(fixture.Commands.PurchasePart("core.blotter", Guid.NewGuid()).Success, Is.True);
            Assert.That(fixture.Commands.PurchasePart("head.blotter", Guid.NewGuid()).Success, Is.True);
            var gun = fixture.Gun(); var tower = gun.Snapshot.Recipe.Tower; var old = tower.Mounts[0].Weapon;
            var weapon = new WeaponRecipe(new PartRef("core.blotter", 1), new PartRef("head.blotter", 1), old.Sight, old.Magazine, old.Trigger);
            var blotter = new AlbumEntry("trial.blotter", "Промокашка", 1, new RecipeSnapshot(
                new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) })),
                fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
            Assert.That(blotter.Snapshot.Recipe.Parts.All(part => fixture.Run.Workshop.IsUnlocked(part.Id)), Is.True);
            panel.OpenEntry(blotter);
            Assert.That(panel.DraftSession.CanTrial, Is.False, "Uncovered part art cannot bypass the accepted fitting boundary.");
            Assert.That(Button("Composite trial").IsInteractable(), Is.False);
            Assert.That(panel.RunTrial(), Is.Null);
            Assert.That(panel.Root.Find("Composite trial overlay"), Is.Null);
        }

        [Test] public void BlotScenarioShowsMeasuredCleaningRatherThanTheoreticalDps()
        {
            Open(fixture.Gun()); Click("Composite trial scenario Blots"); Click("Composite trial run");
            Assert.That(Value(Report, "Scenario").ToString(), Is.EqualTo("Blots"));
            Assert.That(Value(Report, "BlotsCleared"), Is.EqualTo(0), "A gun does not clean blots.");
            Assert.That(Value(Report, "UnresolvedBlots"), Is.GreaterThan(0));
            Assert.That(Text("Composite trial measured result").text, Does.Not.Contain("DPS"));
        }

        [TestCase("invalid")] [TestCase("locked")] [TestCase("overload")] [TestCase("clearance")]
        public void InvalidLockedOverloadAndClearanceDraftsCannotLaunch(string kind)
        {
            var entry = fixture.Gun();
            if (kind == "locked")
            {
                var tower = entry.Snapshot.Recipe.Tower; var old = tower.Mounts[0].Weapon;
                var weapon = new WeaponRecipe(old.Mechanism, new PartRef("head.long", 1), old.Sight, old.Magazine, old.Trigger);
                entry = new AlbumEntry("trial.locked", "Закрытая деталь", 1, new RecipeSnapshot(new AssemblyRecipe(new TowerRecipe(tower.Foundation,
                    new[] { new WeaponMountRecipe(0, weapon) })), fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
            }
            if (kind == "overload")
            {
                var source = fixture.Catalog.Starters.First(page => page.Kind == AlbumEntryKind.Tower && page.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar");
                var tower = source.Snapshot.Recipe.Tower;
                entry = new AlbumEntry("trial.overload", "Перегруз", 1, new RecipeSnapshot(new AssemblyRecipe(new TowerRecipe(tower.Foundation,
                    new[] { new WeaponMountRecipe(0, tower.Mounts[0].Weapon), new WeaponMountRecipe(1, tower.Mounts[0].Weapon) })), fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
            }
            panel.OpenEntry(entry);
            if (kind == "invalid") panel.DraftSession.Rename("");
            if (kind == "clearance")
            {
                var input = JsonUtility.FromJson<FitEnvelope>(Resources.Load<TextAsset>("Art/NotebookMounts").text).fits;
                var frame = input.Frames.Single(item => item.FrameId == entry.Snapshot.Recipe.Tower.Foundation.Frame.Id);
                frame.Keepouts = new[] { new AssemblyFitKeepoutInput { X = -.5f, Y = -.5f, Width = 1, Height = 1 } };
                var session = new WorkshopDraftSession(fixture.Run, fixture.Catalog, new AssemblyFitCatalog(input)); session.Begin(entry);
                Assert.That(session.Diagnostics.Select(issue => issue.Code), Does.Contain("assembly.clearance"));
                typeof(CompositeWorkshopPanel).GetProperty("DraftSession").SetValue(panel, session);
            }
            panel.Refresh();
            Assert.That(panel.DraftSession.CanTrial, Is.False);
            Assert.That(Button("Composite trial").IsInteractable(), Is.False);
            Assert.That(panel.RunTrial(), Is.Null);
            Assert.That(panel.Root.Find("Composite trial overlay"), Is.Null);
        }

        [TestCase(false)] [TestCase(true)]
        public void SupportTrialStillRejectsLockedOrInvalidStats(bool locked)
        {
            var repair = Support(AssemblyStructureFamily.Repair).Snapshot.Recipe.Structure;
            var recipe = new AssemblyRecipe(new StructureRecipe(AssemblyStructureFamily.Repair, repair.Site,
                repair.Function,
                locked ? new PartRef("shell.reinforced", 1) : repair.Shell, repair.Utility));
            var definitions = fixture.Catalog.Definitions.Select(part => !locked && part.Id == "site.repair"
                ? new ComponentDefinition(part.Id, part.Revision, part.Name, part.Category, part.RequiredLevel,
                    part.UnlockGold, part.WeaponFamilies, part.StructureFamilies,
                    part.Modifiers.Select(modifier => modifier.Stat == AssemblyStat.Health && modifier.Operation == ModifierOperation.Set
                        ? new ComponentModifier(AssemblyStat.Health, ModifierOperation.Set, -1) : modifier), part.RequiredParts)
                : part);
            var entry = new AlbumEntry("trial.invalid.support", "Недопустимый ремонт", 1,
                new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, definitions));
            if (locked) Assert.That(fixture.Run.Workshop.IsUnlocked("shell.reinforced"), Is.False);
            else Assert.That(TowerCatalog.ResolveEntry(entry, fixture.Balance).Valid, Is.False);
            int writes = fixture.Store.SaveCalls;
            panel.OpenEntry(entry);
            Assert.That(panel.DraftSession.CanTrial, Is.False);
            Assert.That(Button("Composite trial").IsInteractable(), Is.False);
            Assert.That(fixture.Run.RunAssemblyTrial(entry, AssemblyTrialScenario.Repair), Is.Null);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [TestCase(1024, 768)] [TestCase(1280, 720)] [TestCase(1920, 1080)]
        public void TrialControlsStayWithinTheSheetAtSupportedSizes(int width, int height)
        {
            panel.Reflow(width, height); Open(fixture.Gun());
            var sheet = panel.Root.Find("Composite trial overlay/Composite trial sheet") as RectTransform;
            Assert.That(sheet, Is.Not.Null);
            foreach (var control in sheet.GetComponentsInChildren<Selectable>())
            {
                var corners = new Vector3[4]; ((RectTransform)control.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 local = sheet.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(sheet.rect.xMin - .1f, sheet.rect.xMax + .1f), control.name);
                    Assert.That(local.y, Is.InRange(sheet.rect.yMin - .1f, sheet.rect.yMax + .1f), control.name);
                }
            }
            Assert.That(Button("Composite trial run"), Is.Not.Null);
            Assert.That(Button("Composite close trial"), Is.Not.Null);
            Assert.That(Lane.rectTransform.rect.width, Is.GreaterThan(400));
            Assert.That(Lane.rectTransform.rect.height, Is.GreaterThan(250));
        }
    }
}
#endif
