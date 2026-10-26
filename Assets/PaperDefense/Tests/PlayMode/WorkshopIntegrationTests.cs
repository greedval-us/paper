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
