using System.Collections;
using NUnit.Framework;
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

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var existing in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(existing.gameObject);
            yield return null;
            root = new GameObject("Workshop integration");
            root.SetActive(false);
            game = root.AddComponent<PaperDefenseBootstrap>();
            var camera = new GameObject("Workshop camera", typeof(Camera));
            camera.transform.SetParent(root.transform);
            game.worldCamera = camera.GetComponent<Camera>();
            root.SetActive(true);
            game.Controller.InitializeWorkshop(false);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); yield return null; }

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
            Assert.That(game.Controller.Workshop.Loadout.Count, Is.EqualTo(4));
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
            Button("Open trial").onClick.Invoke();
            Assert.DoesNotThrow(() => Button("Run trial").onClick.Invoke());
            Assert.That(workshop.Status, Does.Contain("откройте"));
            yield return null;
        }

        [UnityTest] public IEnumerator WorkshopDrawingsAndRecordedLanesUploadRealGeometry()
        {
            Canvas.ForceUpdateCanvases();
            AssertRendered(root.GetComponentInChildren<BlueprintSketch>(true));
            AssertRendered(root.GetComponentInChildren<WorkshopMapSketch>(true));

            Button("Open trial").onClick.Invoke();
            Button("Run trial").onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var lanes = root.GetComponentsInChildren<TrialLaneSketch>(true);
            Assert.That(lanes.Length, Is.EqualTo(2));
            foreach (var lane in lanes) AssertRendered(lane);
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
