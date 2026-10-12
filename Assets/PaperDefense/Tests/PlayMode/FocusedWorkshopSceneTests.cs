#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using PaperDefense.Validation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class FocusedWorkshopSceneTests
    {
        const string SceneRoot = "Assets/PaperDefense/Scenes/Tests/";
        MechanicsTestScene harness;
        WorkshopHud hud;

        IEnumerator Open(string name, bool expectHud = true)
        {
            EditorSceneManager.LoadSceneInPlayMode(SceneRoot + name, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            harness = Object.FindFirstObjectByType<MechanicsTestScene>();
            Assert.That(harness, Is.Not.Null);
            Assert.That(harness.Prepared, Is.True, harness.PreparationError);
            harness.enabled = false;
            hud = Object.FindFirstObjectByType<WorkshopHud>();
            if (expectHud) Assert.That(hud, Is.Not.Null);
        }

        [UnityTearDown] public IEnumerator CloseSavedScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(SceneRoot, System.StringComparison.Ordinal)) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Focused workshop verification complete"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        bool Active(string name) => hud.GetComponentsInChildren<Transform>(true)
            .Any(value => value.name == name && value.gameObject.activeInHierarchy);
        void Click(string name)
        {
            var button = hud.GetComponentsInChildren<Button>(true).Single(value => value.name == name);
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }
        void NoObjects(params string[] names)
        {
            var objects = hud.GetComponentsInChildren<Transform>(true);
            foreach (string name in names)
                Assert.That(objects.Any(value => value.name == name), Is.False,
                    harness.scenario.focus + " must not create " + name);
        }
        static void Inside(RectTransform child, RectTransform viewport)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = viewport.InverseTransformPoint(corner);
                Assert.That(point.x, Is.InRange(viewport.rect.xMin - 1, viewport.rect.xMax + 1), child.name + " clipped horizontally by " + viewport.name);
                Assert.That(point.y, Is.InRange(viewport.rect.yMin - 1, viewport.rect.yMax + 1), child.name + " clipped vertically by " + viewport.name);
            }
        }
        void AllChassisVisible()
        {
            Canvas.ForceUpdateCanvases();
            var objects = hud.GetComponentsInChildren<RectTransform>(true);
            var catalogue = objects.Single(value => value.name == "Chassis catalogue");
            var board = objects.Single(value => value.name == "Workshop paper");
            foreach (string name in new[] { "Chassis Standard", "Chassis Heavy", "Chassis chassis.precision", "Chassis chassis.battery" })
            {
                var button = objects.Single(value => value.name == name);
                Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
                Inside(button, catalogue); Inside(button, board);
                foreach (var mask in button.GetComponentsInParent<RectMask2D>(true))
                    if (mask.enabled) Inside(button, mask.rectTransform);
                foreach (var mask in button.GetComponentsInParent<Mask>(true))
                    if (mask.enabled) Inside(button, mask.GetComponent<RectTransform>());
            }
        }
        static bool ConstructionOnly(DrawingInput input)
        {
            var property = typeof(DrawingInput).GetProperty("ConstructionOnly");
            Assert.That(property, Is.Not.Null, "Construction input needs an explicit restricted mode.");
            return (bool)property.GetValue(input);
        }

        [UnityTest] public IEnumerator AssemblySceneEditsThreeMountsAndModulesWithoutOtherWorkshopFlows()
        {
            yield return Open("01_Assembly.unity");
            AllChassisVisible();
            Click("Chassis chassis.battery");
            Click("First Gun"); Click("Second Piercer"); Click("Third Mortar");
            Click("Module Armor"); Click("Module Tracking");
            Assert.That(hud.Draft.ChassisId, Is.EqualTo("chassis.battery"));
            Assert.That(hud.Draft.WeaponMounts.Count, Is.EqualTo(3));
            Assert.That(hud.Draft.GetWeaponId(2), Is.EqualTo("weapon.mortar"));
            var preview = harness.Controller.PreviewBlueprint(hud.Draft);
            Assert.That(preview.Valid, Is.True, preview.Reason);
            Assert.That(preview.Load, Is.EqualTo(22));
            Assert.That(Active("Live blueprint sketch"), Is.True);
            Assert.That(harness.Bootstrap.Drawing.enabled, Is.False);
            Assert.That(hud.DrawingEditor, Is.Null);
            Assert.That(hud.MortarFootprintEditor, Is.Null);
            NoObjects("Blueprint album", "Assign slot 0", "Assign slot 1", "Assign slot 2", "Assign slot 3",
                "Pre-run map", "Start run", "Open drawing editor", "Drawing quality details", "Open trial",
                "Save blueprint", "Save blueprint as new", "Delete blueprint", "Profile save status", "Retry profile save",
                "Mortar footprint 0", "Mortar footprint 1", "Mortar footprint 2", "Drawing editor shade",
                "Authored drawing editor", "Mortar footprint sheet", "Test sheet", "Trial background",
                "Drawing quality shade", "Drawing quality details panel");
        }

        [UnityTest] public IEnumerator DrawingSceneEditsThirdLayerAndFootprintWithoutAssemblyAlbumOrBattle()
        {
            yield return Open("02_Drawing.unity");
            float baselineQuality = harness.Controller.PreviewBlueprint(harness.Controller.Workshop.Loadout[0]).DrawingQuality;
            Assert.That(hud.Draft.WeaponMounts.Count, Is.EqualTo(3));
            Click("Open drawing editor");
            Assert.That(hud.DrawingEditor.IsOpen, Is.True);
            Click("Draw part mount.2");
            Assert.That(hud.DrawingEditor.Session.AddStroke(DrawingBindings.Mount2,
                new[] { new DrawingPoint(-.1f, 0), new DrawingPoint(.1f, .3f) }, out string reason), Is.True, reason);
            Click("Apply drawing");
            Assert.That(hud.Draft.Drawing.GetPart(DrawingBindings.Mount2).Strokes.Count, Is.GreaterThan(0));
            Click("Mortar footprint 2");
            Assert.That(hud.MortarFootprintEditor.IsOpen, Is.True);
            Click("Close footprint");
            harness.CompareDrawing();
            Assert.That(harness.QualityComparison, Is.Not.Null);
            Assert.That(harness.QualityComparison.First.DrawingQuality, Is.EqualTo(hud.Draft.Assessment.Quality).Within(.001f));
            Assert.That(harness.QualityComparison.First.DrawingQuality, Is.Not.EqualTo(baselineQuality));
            Assert.That(Active("Test sheet"), Is.True);
            var lanes = hud.GetComponentsInChildren<TrialLaneSketch>(true);
            Assert.That(lanes.Length, Is.EqualTo(2));
            Assert.That(lanes.All(value => value.gameObject.activeInHierarchy && value.HasRecording), Is.True,
                "Both results must display recordings from the quality comparison.");
            Assert.That(harness.Bootstrap.Drawing.enabled, Is.False);
            NoObjects("Chassis catalogue", "First weapon catalogue", "Second weapon catalogue", "Third weapon catalogue",
                "Armor catalogue", "Module catalogue", "Blueprint album", "Assign slot 0", "Assign slot 1", "Assign slot 2", "Assign slot 3",
                "Pre-run map", "Start run", "Save blueprint", "Save blueprint as new", "Delete blueprint", "Profile save status", "Retry profile save", "Open trial");
        }

        [UnityTest] public IEnumerator ProfileSceneSavesAlbumCopyWithoutAssemblyDrawingTrialsOrBattle()
        {
            yield return Open("12_ProfilePersistence.unity");
            Assert.That(Active("Blueprint album"), Is.True);
            Assert.That(Active("Profile save status"), Is.True);
            int count = harness.Controller.Workshop.Album.Count;
            int writes = harness.ProfileSaveCalls;
            Click("Save blueprint as new");
            Assert.That(harness.Controller.Workshop.Album.Count, Is.EqualTo(count + 1));
            Assert.That(harness.ProfileSaveCalls, Is.GreaterThan(writes));
            Assert.That(harness.Bootstrap.Drawing.enabled, Is.False);
            Assert.That(hud.DrawingEditor, Is.Null);
            Assert.That(hud.MortarFootprintEditor, Is.Null);
            NoObjects("Chassis catalogue", "First weapon catalogue", "Second weapon catalogue", "Third weapon catalogue",
                "Armor catalogue", "Module catalogue", "Open drawing editor", "Drawing quality details", "Open trial",
                "Mortar footprint 0", "Mortar footprint 1", "Mortar footprint 2",
                "Assign slot 0", "Assign slot 1", "Assign slot 2", "Assign slot 3", "Pre-run map", "Start run",
                "Drawing editor shade", "Authored drawing editor", "Mortar footprint sheet", "Test sheet", "Trial background",
                "Drawing quality shade", "Drawing quality details panel");
        }

        [UnityTest] public IEnumerator SceneInputIsDisabledForWeaponsRestrictedToConstructionAndFullForIntegration()
        {
            yield return Open("03_Weapons/Gun.unity", false);
            Assert.That(harness.Bootstrap.Drawing.enabled, Is.False,
                "A weapon stand must not accept construction or restart keyboard commands.");

            yield return Open("10_ConstructionNavigation.unity", false);
            var input = harness.Bootstrap.Drawing;
            Assert.That(input.isActiveAndEnabled, Is.True);
            Assert.That(ConstructionOnly(input), Is.True);
            float standScale = Mathf.Max(.65f, Mathf.Min(Screen.width / 1280f, Screen.height / 800f));
            Assert.That(harness.Bootstrap.worldCamera.pixelRect.yMin, Is.GreaterThanOrEqualTo(60 * standScale + 12 - 1),
                "Construction clicks must not reach the world behind the stand control strip.");
            input.SelectTool(StructureKind.Wall);
            input.SelectSlot(0);
            Assert.That(input.BlueprintSlot, Is.EqualTo(-1));
            Assert.That(input.Tool, Is.EqualTo(StructureKind.Wall));
            input.SelectTool(StructureKind.Gun);
            Assert.That(input.Tool, Is.EqualTo(StructureKind.Wall));
            input.SelectMode(DrawingMode.Paint);
            Assert.That(input.BlueprintSlot, Is.EqualTo(-1));
            Assert.That(input.Tool, Is.EqualTo(StructureKind.Wall));
            Assert.That(input.Mode, Is.EqualTo(DrawingMode.Build));
            input.SelectTool(StructureKind.Spikes); input.SelectMode(DrawingMode.Erase);
            Assert.That(input.Tool, Is.EqualTo(StructureKind.Spikes));
            Assert.That(input.Mode, Is.EqualTo(DrawingMode.Erase));
            input.SelectMode(DrawingMode.Upgrade);
            Assert.That(input.Mode, Is.EqualTo(DrawingMode.Upgrade));

            yield return Open("13_Integration.unity");
            input = harness.Bootstrap.Drawing;
            Assert.That(input.isActiveAndEnabled, Is.True);
            Assert.That(ConstructionOnly(input), Is.False);
            foreach (string name in new[] { "Chassis catalogue", "Blueprint album", "Assign slot 0", "Pre-run map", "Open drawing editor", "Open trial", "Start run" })
                Assert.That(Active(name), Is.True, "Integration must preserve " + name);
            Click("Start run");
            Assert.That(harness.Controller.InWorkshop, Is.False);
            input.SelectSlot(0);
            Assert.That(input.BlueprintSlot, Is.Zero);
        }
    }
}
#endif
