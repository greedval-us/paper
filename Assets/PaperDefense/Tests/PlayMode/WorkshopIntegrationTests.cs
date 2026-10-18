using System.Collections;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class WorkshopIntegrationTests
    {
        GameObject root;
        PaperDefenseBootstrap game;
        GameBalanceAsset fixtureBalance;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var existing in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(existing.gameObject);
            yield return null;
            root = new GameObject("Workshop integration");
            root.SetActive(false);
            game = root.AddComponent<PaperDefenseBootstrap>();
            fixtureBalance = ScriptableObject.CreateInstance<GameBalanceAsset>();
            fixtureBalance.ResetToDefaults(); fixtureBalance.useTargetWorkshopPreset = true;
            fixtureBalance.baseHealth = 250; fixtureBalance.planningSeconds = 35; fixtureBalance.intermissionSeconds = 20;
            game.balance = fixtureBalance;
            game.memoryOnlyProfile = true;
            var camera = new GameObject("Workshop camera", typeof(Camera));
            camera.transform.SetParent(root.transform);
            game.worldCamera = camera.GetComponent<Camera>();
            root.SetActive(true);
            game.Controller.InitializeWorkshop(false);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); Object.Destroy(fixtureBalance); yield return null; }

        Button Button(string name)
        {
            foreach (var candidate in root.GetComponentsInChildren<Button>(true))
                if (candidate.name == name) return candidate;
            Assert.Fail("Missing workshop button: " + name);
            return null;
        }

        [UnityTest] public IEnumerator NormalBootOpensWorkshopAndStartUsesFourBlueprints()
        {
            Assert.That(game.Controller.InWorkshop, Is.True);
            Assert.That(game.Controller.Workshop.Loadout.Count, Is.EqualTo(ProfileLimits.LoadoutSlotCount));
            Assert.That(root.GetComponentsInChildren<WorkshopHud>(true).Length, Is.EqualTo(1));
            long tick = game.Controller.Session.TickIndex;
            yield return null;
            Assert.That(game.Controller.Session.TickIndex, Is.EqualTo(tick));
            Button("Start run").onClick.Invoke();
            yield return null;
            Assert.That(game.Controller.InWorkshop, Is.False);
            Assert.That(game.Controller.Session.Balance.Width, Is.EqualTo(96));
            Assert.That(game.Controller.Session.Wells.Count, Is.EqualTo(3));
        }

        [UnityTest] public IEnumerator ComposerShowsRealLoadAndRejectsOverloadedArmor()
        {
            var workshop = root.GetComponentInChildren<WorkshopHud>(true);
            Button("New blueprint").onClick.Invoke();
            Button("First Gun").onClick.Invoke();
            Button("Second Gun").onClick.Invoke();
            Assert.That(workshop.Draft.SecondWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(game.Controller.PreviewBlueprint(workshop.Draft, MaterialTier.Pencil).Load, Is.EqualTo(8));
            Button("Module Armor").onClick.Invoke();
            Assert.That(game.Controller.PreviewBlueprint(workshop.Draft, MaterialTier.Pencil).Valid, Is.False);
            int before = game.Controller.Workshop.Album.Count;
            Button("Save blueprint").onClick.Invoke();
            Assert.That(game.Controller.Workshop.Album.Count, Is.EqualTo(before));
            Assert.That(workshop.Status, Is.Not.Empty);
            yield return null;
        }

        [UnityTest] public IEnumerator SavedDraftAndFieldPlacementAreIndependentOfLaterEditing()
        {
            var workshop = root.GetComponentInChildren<WorkshopHud>(true);
            Button("New blueprint").onClick.Invoke();
            Button("First Gun").onClick.Invoke();
            Button("Second Gun").onClick.Invoke();
            Button("Open drawing editor").onClick.Invoke();
            Assert.That(workshop.DrawingEditor.Session.AddStroke(DrawingPartKey.Mount0,
                new[] { new DrawingPoint(0, 0), new DrawingPoint(0, .4f) }, out _), Is.True);
            FreeformUiTestDrawing.Complete(workshop.DrawingEditor.Session); Button("Apply drawing").onClick.Invoke();
            var saved = workshop.Draft;
            Button("Save blueprint").onClick.Invoke();
            Button("Assign slot 0").onClick.Invoke();
            Assert.That(game.Controller.Workshop.Loadout[0].Id, Is.EqualTo(saved.Id));
            Button("First Mortar").onClick.Invoke();
            Assert.That(game.Controller.Workshop.Loadout[0].FirstWeapon, Is.EqualTo(StructureKind.Gun));
            Button("Start run").onClick.Invoke();
            yield return null;
            Assert.That(game.Controller.BuildBlueprint(new[] { new Cell(10, 10) }, 0,
                MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            Assert.That(game.Controller.Session.StructureAt(new Cell(10, 10)).Value.Blueprint.SecondWeapon,
                Is.EqualTo(StructureKind.Gun));
        }

        [UnityTest] public IEnumerator LockedTrialShowsAnExplanationWithoutThrowing()
        {
            var workshop = root.GetComponentInChildren<WorkshopHud>(true);
            Button("New blueprint").onClick.Invoke();
            Button("Module Feed").onClick.Invoke();
            Button("Open drawing editor").onClick.Invoke();
            FreeformUiTestDrawing.Complete(workshop.DrawingEditor.Session);
            workshop.DrawingEditor.Close(true);
            Assert.That(workshop.Draft.FreeformDesign.IsComplete, Is.True);
            Button("Open trial").onClick.Invoke();
            Assert.DoesNotThrow(() => Button("Run trial").onClick.Invoke());
            Assert.That(workshop.Status, Does.Contain("откройте"));
            yield return null;
        }

        [UnityTest] public IEnumerator WorkshopDrawingsAndRecordedLanesUploadRealGeometry()
        {
            Canvas.ForceUpdateCanvases();
            var workshop = root.GetComponentInChildren<WorkshopHud>(true);
            AssertRendered(workshop.GetComponentInChildren<BlueprintSketch>());
            AssertRendered(workshop.GetComponentInChildren<WorkshopMapSketch>());

            Button("Open trial").onClick.Invoke();
            Button("Run trial").onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var lanes = root.GetComponentsInChildren<TrialLaneSketch>(true);
            Assert.That(lanes.Length, Is.EqualTo(2));
            foreach (var lane in lanes) AssertRendered(lane);
        }

        [UnityTest] public IEnumerator ActiveBattlePaletteUploadsBlueprintAndToolIconGeometry()
        {
            Assert.That(game.Controller.StartRun(out string reason), Is.True, reason);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var hud = root.GetComponentInChildren<NotebookHud>(true);
            var blueprints = hud.GetComponentsInChildren<BlueprintSketch>();
            Assert.That(blueprints.Length, Is.EqualTo(ProfileLimits.LoadoutSlotCount));
            for (int i = 0; i < blueprints.Length; i++)
            {
                if (game.Controller.BuildLoadout[i] != null) AssertRendered(blueprints[i]);
                else
                {
                    var mesh = blueprints[i].GetComponent<CanvasRenderer>().GetMesh();
                    Assert.That(mesh == null || mesh.vertexCount == 0, Is.True, "An empty slot must not show another page's artwork.");
                    Assert.That(blueprints[i].GetComponentInParent<Button>().interactable, Is.False);
                }
            }
            var icons = hud.GetComponentsInChildren<NotebookArtGraphic>();
            Assert.That(icons.Length, Is.GreaterThanOrEqualTo(4), "Every available construction/action card has visible art.");
            foreach (var icon in icons) AssertRendered(icon);
        }

        static void AssertRendered(Graphic graphic)
        {
            Assert.That(graphic, Is.Not.Null);
            var renderer = graphic.GetComponent<CanvasRenderer>();
            Assert.That(renderer, Is.Not.Null, graphic.name + " needs a CanvasRenderer");
            var mesh = renderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, graphic.name + " did not upload a UI mesh");
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), graphic.name + " has no visible geometry");
        }
    }
}
