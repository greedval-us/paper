#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class NotebookArtStandSceneTests
    {
        const string SceneRoot = "Assets/PaperDefense/Scenes/Tests/";
        NotebookArtStand stand;
        string[] profileFiles;
        string persistenceError;

        [SetUp] public void CapturePersistenceMetadata()
        {
            profileFiles = ProfileMetadata();
            persistenceError = WorkshopPersistence.LastError;
        }

        // Metadata only: verification never opens, creates, repairs or saves a user's profile.
        static string[] ProfileMetadata()
        {
            string path = WorkshopPersistence.ProfilePath;
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            return Directory.GetFiles(directory, Path.GetFileName(path) + "*")
                .Select(value => new FileInfo(value))
                .OrderBy(value => value.FullName, StringComparer.Ordinal)
                .Select(value => value.Name + ":" + value.Length + ":" + value.LastWriteTimeUtc.Ticks).ToArray();
        }

        IEnumerator Open(string sceneName, NotebookArtStandMode mode)
        {
            EditorSceneManager.LoadSceneInPlayMode(SceneRoot + sceneName, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            stand = Object.FindFirstObjectByType<NotebookArtStand>();
            Assert.That(stand, Is.Not.Null, sceneName);
            Assert.That(stand.Mode, Is.EqualTo(mode));
            Assert.That(stand.preset, Is.Not.Null);
            Assert.That(stand.textResources, Is.Not.Null);
            stand.SetPaused(true);
            Click("Restart");
            Assert.That(NotebookVisualThemeAsset.Default, Is.Not.Null);

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Any(camera => camera.enabled), Is.True,
                "The saved art scene must contain its own camera.");
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Light>(true)).Any(light => light.enabled), Is.True,
                "The saved art scene must contain its own light.");
            Assert.That(Object.FindObjectsByType<RunController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty,
                "An isolated art scene must not initialize a battle or a persistent workshop.");
            Assert.That(Object.FindObjectsByType<WorkshopHud>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            AssertResolvedLabels();
        }

        [UnityTearDown] public IEnumerator CloseArtSceneWithoutPersistenceChanges()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path.StartsWith(SceneRoot, StringComparison.Ordinal))
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Notebook art verification complete"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Assert.That(ProfileMetadata(), Is.EqualTo(profileFiles), "Art browsing must leave persistent profile files unchanged.");
            Assert.That(WorkshopPersistence.LastError, Is.EqualTo(persistenceError), "The art stand must not invoke profile loading or saving.");
        }

        Transform[] Cards() => stand.GetComponentsInChildren<Transform>(true)
            .Where(value => value.name.StartsWith("Art card ", StringComparison.Ordinal)).ToArray();

        NotebookArtGraphic[] CardGraphics() => Cards().Select(card => card.GetComponentInChildren<NotebookArtGraphic>()).ToArray();

        void Click(string name)
        {
            var button = stand.GetComponentsInChildren<Button>(true).Single(value => value.name == name);
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            Assert.That(button.targetGraphic.raycastTarget, Is.True, name + " must accept pointer input.");
            button.onClick.Invoke();
        }

        void AssertResolvedLabels()
        {
            var labels = stand.GetComponentsInChildren<Text>(true).Where(label => label.gameObject.activeInHierarchy).ToArray();
            Assert.That(labels.Length, Is.GreaterThan(5));
            foreach (var label in labels)
            {
                Assert.That(string.IsNullOrWhiteSpace(label.text), Is.False, label.transform.parent.name);
                Assert.That(label.text, Does.Not.Contain("\u001ePD1:"), label.transform.parent.name);
                Assert.That(label.text, Does.Not.Contain("[missing:"), label.transform.parent.name);
                Assert.That(label.text, Does.Not.Contain("[invalid:"), label.transform.parent.name);
                Assert.That(label.text.StartsWith("art.", StringComparison.Ordinal), Is.False, label.text);
            }
        }

        static Mesh RenderedMesh(Graphic graphic)
        {
            Canvas.ForceUpdateCanvases();
            var mesh = graphic.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, graphic.transform.parent.name + " did not upload a UI mesh.");
            return mesh;
        }

        static Vector3[] RenderedVertices(Graphic graphic)
        {
            var mesh = RenderedMesh(graphic);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), graphic.transform.parent.name + " has empty art geometry.");
            return mesh.vertices;
        }

        static T Field<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + name);
            return (T)field.GetValue(owner);
        }

        void AssertCards(int expected)
        {
            Assert.That(stand.CardCount, Is.EqualTo(expected));
            Assert.That(Cards().Length, Is.EqualTo(expected), "Only the current gallery page may keep card objects.");
            foreach (var graphic in CardGraphics())
            {
                Assert.That(graphic, Is.Not.Null);
                RenderedVertices(graphic);
            }
            AssertResolvedLabels();
        }

        [UnityTest] public IEnumerator TowerSceneShowsFifteenSelectablePartsAndFiveValidCompositions()
        {
            yield return Open("24_TowerArt.unity", NotebookArtStandMode.Towers);
            AssertCards(15);
            string[] ids = { "chassis.standard", "chassis.heavy", "chassis.precision", "chassis.battery",
                "weapon.gun", "weapon.piercer", "weapon.mortar", "weapon.fan", "weapon.ricochet", "weapon.stapler", "weapon.blotter",
                "module.armor", "module.feed", "module.tracking", "module.rail" };
            Assert.That(CardGraphics().Select(graphic => graphic.ShapeId), Is.EquivalentTo(ids));
            var selection = stand.GetComponentsInChildren<NotebookArtGraphic>().Single(graphic => graphic.name == "Selected part");
            foreach (string id in ids)
            {
                Click("Art card " + id);
                Assert.That(selection.ShapeId, Is.EqualTo(id));
                RenderedVertices(selection);
            }
            var balance = stand.preset.CreateBalance();
            var sketch = stand.GetComponentInChildren<BlueprintSketch>();
            for (int variant = 0; variant < 5; variant++)
            {
                Click("Combination " + variant);
                var blueprint = Field<TowerBlueprint>(sketch, "blueprint");
                Assert.That(blueprint, Is.Not.Null);
                Assert.That(TowerCatalog.Validate(blueprint, balance, out string reason), Is.True, reason);
                Assert.That(blueprint.WeaponMounts.Count, Is.EqualTo(new[] { 2, 2, 1, 3, 3 }[variant]));
                Assert.That(blueprint.ModuleSelections.Count, Is.EqualTo(new[] { 0, 1, 4, 2, 0 }[variant]));
                RenderedVertices(sketch);
            }
            yield return VerifyThreeIdenticalWeaponPreview(sketch);
            VerifyOwnDrawingPreview(sketch);
            yield return VerifyClockControls();
            AssertResolvedLabels();
        }

        IEnumerator VerifyThreeIdenticalWeaponPreview(BlueprintSketch sketch)
        {
            Click("Combination 4");
            Assert.That(Field<TowerBlueprint>(sketch, "blueprint").WeaponMounts.Select(mount => mount.WeaponId),
                Is.EqualTo(new[] { "weapon.gun", "weapon.gun", "weapon.gun" }));
            Click("Restart");
            var first = RenderedVertices(sketch);
            stand.Advance(.03f);
            var moving = RenderedVertices(sketch);
            Assert.That(moving.SequenceEqual(first), Is.False, "The assembled preview must animate its actual UI geometry.");
            float frozen = stand.Seconds;
            yield return null;
            Assert.That(stand.Seconds, Is.EqualTo(frozen));
            Assert.That(RenderedVertices(sketch), Is.EqualTo(moving), "Pause must freeze the visible assembled pose.");

            Click("Aim");
            Click("Recoil");
            stand.Advance(0);
            var still = RenderedVertices(sketch);
            stand.Advance(.1f);
            Assert.That(RenderedVertices(sketch), Is.EqualTo(still), "Disabling both preview controls must stop visible motion.");

            Click("Recoil");
            Click("Restart");
            var state = Field<NotebookAnimationState>(stand, "previewAnimation");
            var neutral = RenderedVertices(sketch);
            stand.Advance(.03f);
            Assert.That(RenderedVertices(sketch).SequenceEqual(neutral), Is.False, "Recoil must move geometry with aim disabled.");
            Assert.That(state.Recoil(1, 0, stand.Seconds), Is.LessThan(0));
            Assert.That(state.Recoil(1, 1, stand.Seconds), Is.Zero);
            Assert.That(state.Recoil(1, 2, stand.Seconds), Is.Zero);
            stand.Advance(.18f); stand.Advance(.03f);
            Assert.That(state.Recoil(1, 0, stand.Seconds), Is.Zero);
            Assert.That(state.Recoil(1, 1, stand.Seconds), Is.LessThan(0));
            Assert.That(state.Recoil(1, 2, stand.Seconds), Is.Zero);
            stand.Advance(.18f); stand.Advance(.03f);
            Assert.That(state.Recoil(1, 0, stand.Seconds), Is.Zero);
            Assert.That(state.Recoil(1, 1, stand.Seconds), Is.Zero);
            Assert.That(state.Recoil(1, 2, stand.Seconds), Is.LessThan(0));
            Assert.That(state.RecoilStartCount, Is.EqualTo(3), "Identical weapon IDs must retain three independent mount actions.");

            Click("Recoil");
            Click("Aim");
            stand.Advance(0);
            var aimed = RenderedVertices(sketch);
            stand.Advance(.05f);
            Assert.That(RenderedVertices(sketch).SequenceEqual(aimed), Is.False, "Aim must move geometry with recoil disabled.");
            Click("Recoil");
        }

        void VerifyOwnDrawingPreview(BlueprintSketch sketch)
        {
            Click("Draw tower");
            var editor = Field<DrawingEditorPanel>(stand, "drawingEditor");
            Assert.That(editor.IsOpen, Is.True);
            Assert.That(editor.Session.Blueprint.WeaponMounts.Count, Is.EqualTo(3));
            Click("Draw part mount.2");
            Assert.That(editor.Canvas.SelectedBinding, Is.EqualTo(DrawingBindings.Mount2));
            var points = new[] { new DrawingPoint(-.11f, -.07f), new DrawingPoint(.02f, .09f), new DrawingPoint(.13f, .3f) };
            Assert.That(editor.Session.AddStroke(DrawingBindings.Mount2, points, out string reason), Is.True, reason);
            FreeformUiTestDrawing.Complete(editor.Session);
            editor.Canvas.FinishStroke();
            var drawing = editor.Session.Snapshot;
            Click("Apply drawing");
            Assert.That(editor.IsOpen, Is.False);
            var blueprint = Field<TowerBlueprint>(sketch, "blueprint");
            Assert.That(blueprint.Origin, Is.EqualTo(DrawingOrigin.Freeform));
            Assert.That(blueprint.Drawing, Is.SameAs(drawing));
            Assert.That(blueprint.FreeformDesign.IsComplete, Is.True);
            foreach (string binding in new[] { DrawingBindings.Base, DrawingBindings.Mount0, DrawingBindings.Mount1 })
                Assert.That(drawing.GetPart(binding).Strokes, Is.Not.Empty, binding);
            Assert.That(drawing.GetPart(DrawingBindings.Mount2).Strokes.Count, Is.EqualTo(1));
            var ownVertices = RenderedVertices(sketch);
            Assert.That(ownVertices.Length, Is.GreaterThan(0), "A complete freeform tower renders its authored functional parts.");
            string hash = drawing.GeometryHash;
            var stamp = blueprint.EvaluationStamp;
            int builds = AuthoredGeometryCache.BuildCount;
            for (int frame = 0; frame < 20; frame++)
            {
                stand.Advance(.01f);
                var vertices = RenderedVertices(sketch);
                Assert.That(vertices.Length, Is.EqualTo(ownVertices.Length));
                Assert.That(Vector3.Distance(vertices[0], vertices[1]),
                    Is.EqualTo(Vector3.Distance(ownVertices[0], ownVertices[1])).Within(.0001), "Preview motion must preserve stroke geometry.");
                Assert.That(Field<TowerBlueprint>(sketch, "blueprint"), Is.SameAs(blueprint));
                Assert.That(blueprint.Drawing, Is.SameAs(drawing));
                Assert.That(drawing.GeometryHash, Is.EqualTo(hash));
                Assert.That(blueprint.EvaluationStamp, Is.SameAs(stamp));
            }
            Assert.That(AuthoredGeometryCache.BuildCount, Is.EqualTo(builds), "Advancing a preview must reuse prepared author geometry.");
            Assert.That(drawing.GetPart(DrawingBindings.Mount2).Strokes[0].Points, Is.EqualTo(points));

            Click("Combination 0"); Click("Combination 4");
            Assert.That(Field<TowerBlueprint>(sketch, "blueprint").Drawing, Is.SameAs(drawing), "Each combination must keep its own memory-only drawing.");
            var restored = Field<TowerBlueprint>(sketch, "blueprint");
            Click("Draw tower"); Click("Draw part mount.2"); Click("Clear drawing part");
            Assert.That(!stand.GetComponentsInChildren<Button>(true).Single(b => b.name == "Apply drawing").interactable, Is.True);
            editor.Close(true);
            Assert.That(editor.IsOpen, Is.True, "Removing a required barrel must reject apply and keep the editor open.");
            Assert.That(Field<TowerBlueprint>(sketch, "blueprint"), Is.SameAs(restored));
            Assert.That(drawing.GeometryHash, Is.EqualTo(hash), "Rejected incomplete edits must not alter the applied snapshot.");
            editor.Close(false);
            Click("Reference tower");
            Assert.That(Field<TowerBlueprint>(sketch, "blueprint").Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            RenderedVertices(sketch);
        }

        IEnumerator VerifyClockControls()
        {
            Assert.That(stand.Paused, Is.True);
            float frozen = stand.Seconds;
            yield return null;
            Assert.That(stand.Seconds, Is.EqualTo(frozen));
            stand.Advance(.125f);
            Assert.That(stand.Seconds, Is.EqualTo(frozen + .125f).Within(.00001));
            stand.Advance(-1);
            Assert.That(stand.Seconds, Is.EqualTo(frozen + .125f).Within(.00001));
            Click("Step");
            Assert.That(stand.Paused, Is.True);
            Assert.That(stand.Seconds, Is.EqualTo(frozen + .125f + 1f / 30).Within(.00001));
            Click("Restart");
            Assert.That(stand.Seconds, Is.Zero);
            foreach (float speed in new[] { .25f, 2f, 1f })
            {
                Click("Speed");
                float before = stand.Seconds;
                Click("Pause");
                Assert.That(stand.Paused, Is.False);
                yield return null;
                Click("Pause");
                Assert.That(stand.Paused, Is.True);
                Assert.That(stand.Seconds - before, Is.EqualTo(Time.unscaledDeltaTime * speed).Within(.001f), "Playback speed must affect the actual stand update.");
            }
            Click("Restart");
            Assert.That(stand.Seconds, Is.Zero);
        }

        [UnityTest] public IEnumerator EnemySceneBindsAllTwelveVariantsToFiveAnimatedResourcePoses()
        {
            yield return Open("25_EnemyAnimation.unity", NotebookArtStandMode.Enemies);
            AssertCards(12);
            var theme = NotebookVisualThemeAsset.Default;
            string[] families = { "doodle", "runner", "gnawer", "spitter" };
            string[] tiers = { "sketch", "outlined", "hatched" };
            foreach (string pose in new[] { "idle", "walk", "attack", "hit", "death" })
            {
                Click("Pose " + pose);
                Assert.That(stand.Seconds, Is.Zero);
                var graphics = CardGraphics();
                Assert.That(graphics.Select(graphic => graphic.ClipId), Is.EquivalentTo(
                    families.SelectMany(family => tiers.Select(tier => "enemy." + family + "." + tier + "." + pose))));
                foreach (var graphic in graphics)
                {
                    var clip = theme.FindClip(graphic.ClipId);
                    Assert.That(clip, Is.Not.Null, graphic.ClipId);
                    Assert.That(clip.Frames.Count, Is.GreaterThan(1), graphic.ClipId);
                    Assert.That(clip.Frames.Select(frame => frame.ShapeId).Distinct().Count(), Is.GreaterThan(1), graphic.ClipId + " must contain distinct authored frames.");
                    foreach (var frame in clip.Frames)
                    {
                        var shape = theme.FindShape(frame.ShapeId);
                        Assert.That(shape, Is.Not.Null, frame.ShapeId);
                        Assert.That(shape.Marks.Count, Is.GreaterThan(0), frame.ShapeId);
                    }
                    graphic.SetTime(0);
                    var first = RenderedVertices(graphic);
                    graphic.SetTime(clip.Frames[1].Time);
                    var second = RenderedVertices(graphic);
                    Assert.That(second.SequenceEqual(first), Is.False, graphic.ClipId + " must visibly change its UI geometry.");
                }
                stand.Advance(.025f);
                Assert.That(graphics.All(graphic => Mathf.Abs(graphic.Seconds - stand.Seconds) < .00001f), Is.True,
                    "The stand clock must update every enemy card together.");
                AssertResolvedLabels();
            }
        }

        [UnityTest] public IEnumerator EffectsSceneButtonsShowTwentyTwoEffectsTenObjectsAndAllSixteenWallMasks()
        {
            yield return Open("26_InkEffects.unity", NotebookArtStandMode.Effects);
            AssertCards(22);
            Assert.That(CardGraphics().All(graphic => graphic.ClipId != null && graphic.ClipId.StartsWith("fx.", StringComparison.Ordinal)), Is.True);
            foreach (var graphic in CardGraphics())
            {
                var clip = NotebookVisualThemeAsset.Default.FindClip(graphic.ClipId);
                Assert.That(clip, Is.Not.Null, graphic.ClipId);
                Assert.That(clip.Frames.Count, Is.GreaterThan(1));
                Assert.That(clip.Frames.All(frame => NotebookVisualThemeAsset.Default.FindShape(frame.ShapeId) != null), Is.True, graphic.ClipId);
            }
            Click("Effects page");
            yield return null; // Runtime Destroy removes the outgoing page at the frame boundary.
            AssertCards(10);
            Assert.That(CardGraphics().Select(graphic => graphic.ShapeId), Is.EquivalentTo(new[] {
                "object.castle", "object.castle.damaged", "object.spikes", "object.blot",
                "object.well.red", "object.well.red.active", "object.well.blue", "object.well.blue.active", "object.well.green", "object.well.green.active" }));
            Click("Effects page");
            yield return null;
            AssertCards(16);
            Assert.That(CardGraphics().Select(graphic => graphic.ShapeId), Is.EquivalentTo(Enumerable.Range(0, 16).Select(mask => "object.wall." + mask)));
            Click("Effects page");
            yield return null;
            AssertCards(22);
            Assert.That(stand.Seconds, Is.Zero, "Changing an art page while paused must not advance its animation clock.");
        }
    }
}
#endif
