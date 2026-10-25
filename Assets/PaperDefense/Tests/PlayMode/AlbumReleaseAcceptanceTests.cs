#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AlbumReleaseAcceptanceTests
    {
        static readonly HashSet<string> RetiredGraphics = new HashSet<string>(StringComparer.Ordinal)
        {
            "DrawingCanvasGraphic", "FootprintCanvasGraphic", "FootprintAxisGraphic",
            "FanVolleyGraphic", "ArmorReinforcementGraphic", "DrawingPaperGridGraphic",
            "DrawingStencilGraphic", "DrawingStencilBoxGraphic"
        };

        [Test] public void NewJourney_HasNoDrawingEditor()
        {
            // A legacy bootstrap or non-guided HUD opening a canvas must fail this gate,
            // even when the ordinary PlayerGameRoot happens to use the composite route.
            var reachableDrawingEditors = new HashSet<string>(StringComparer.Ordinal);
            using (var fixture = new AssemblyCommandFixture())
            {
                var player = new GameObject("Release acceptance player"); player.SetActive(false);
                try
                {
                    var game = player.AddComponent<PlayerGameRoot>(); game.Configure(fixture.BalanceAsset, true);
                    player.SetActive(true); game.Run.AutomaticSimulation = false;
                    game.Run.InitializeWorkshop(fixture.Store);
                    foreach (var route in new[]
                    {
                        (PlayerPage.Home, "workshop"), (PlayerPage.Home, "Open album"),
                        (PlayerPage.Map, "Map album"), (PlayerPage.Preparation, "Edit album"),
                        (PlayerPage.Preparation, "Open preparation album")
                    })
                    {
                        game.Navigate(route.Item1);
                        var button = player.GetComponentsInChildren<Button>().Single(b => b.name == route.Item2);
                        Assert.That(button.IsInteractable(), Is.True, route.Item2);
                        button.onClick.Invoke();
                        Inspect(player, route.Item1 + "/" + route.Item2, reachableDrawingEditors);
                    }
                    game.Navigate(PlayerPage.Workshop);
                    Inspect(player, "Navigate(Workshop)", reachableDrawingEditors);
                    game.OpenAlbum(); game.ShowAlbum(); game.CloseAlbum();
                    game.EditAlbumEntry(null);
                    Inspect(player, "EditAlbumEntry(new)", reachableDrawingEditors);
                    game.EditAlbumEntry(fixture.Gun());
                    Inspect(player, "EditAlbumEntry(existing)", reachableDrawingEditors);
                    game.ReturnFromWorkshop(); game.ReturnToAlbumFromResult();
                    Inspect(player, "ReturnToAlbumFromResult", reachableDrawingEditors);
                    Assert.That(game.Drawing, Is.Not.Null, "Construction input is still required in battle.");
                    Assert.That(player.GetComponentInChildren<WorkshopHud>(true).Composite, Is.Not.Null);
                }
                finally { Object.DestroyImmediate(player); }

                // Public HUD and bootstrap configuration were also shipped editor/menu entry routes.
                // Exercise every value, so a forgotten mode cannot conceal the retired editor.
                foreach (WorkshopHudMode mode in Enum.GetValues(typeof(WorkshopHudMode)))
                {
                    foreach (bool guided in new[] { false, true })
                    {
                        var owner = new GameObject("Release acceptance HUD");
                        try
                        {
                            var hud = owner.AddComponent<WorkshopHud>(); hud.Mode = mode; hud.UseGuidedFlow = guided;
                            hud.Initialize(fixture.Run);
                            Assert.That(hud.Composite, Is.Not.Null, "A historical mode must reach the released album.");
                            Inspect(owner, "WorkshopHud/" + mode + "/guided=" + guided, reachableDrawingEditors);
                        }
                        finally { Object.DestroyImmediate(owner); }
                    }

                    var legacy = new GameObject("Release acceptance bootstrap"); legacy.SetActive(false);
                    try
                    {
                        var camera = new GameObject("Isolated release camera", typeof(Camera));
                        camera.transform.SetParent(legacy.transform, false);
                        var bootstrap = legacy.AddComponent<PaperDefenseBootstrap>();
                        bootstrap.balance = fixture.BalanceAsset; bootstrap.memoryOnlyProfile = true;
                        bootstrap.worldCamera = camera.GetComponent<Camera>(); bootstrap.showBattleHud = false;
                        bootstrap.showWorkshopHud = true; bootstrap.workshopHudMode = mode;
                        legacy.SetActive(true); bootstrap.Controller.AutomaticSimulation = false;
                        Assert.That(legacy.GetComponentInChildren<WorkshopHud>(true).Composite, Is.Not.Null);
                        Inspect(legacy, "PaperDefenseBootstrap/" + mode, reachableDrawingEditors);
                        Assert.That(bootstrap.Drawing, Is.Not.Null);
                    }
                    finally { Object.DestroyImmediate(legacy); }
                }
            }

            AssertPlayerSceneScriptsResolve();
            Assert.That(reachableDrawingEditors, Is.Empty,
                "Every player, legacy HUD, and bootstrap entrance must use the released album UI.");
        }

        [Test] public void StandaloneBootstrapPreparationStartsAnEightSlotAlbumRun()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var owner = new GameObject("Standalone album entrance"); owner.SetActive(false);
                try
                {
                    var camera = new GameObject("Standalone isolated camera", typeof(Camera));
                    camera.transform.SetParent(owner.transform, false);
                    var bootstrap = owner.AddComponent<PaperDefenseBootstrap>();
                    bootstrap.balance = fixture.BalanceAsset; bootstrap.memoryOnlyProfile = true;
                    bootstrap.worldCamera = camera.GetComponent<Camera>(); bootstrap.showBattleHud = false;
                    bootstrap.showWorkshopHud = true;
                    owner.SetActive(true); bootstrap.Controller.AutomaticSimulation = false;
                    var before = bootstrap.Controller.Session;
                    Assert.That(bootstrap.Controller.InWorkshop, Is.True);
                    var button = owner.GetComponentsInChildren<Button>().Single(control => control.name == "Composite preparation");
                    Assert.That(button.IsInteractable(), Is.True); button.onClick.Invoke();
                    Assert.That(bootstrap.Controller.InWorkshop, Is.False,
                        "The standalone entrance must start the run when no player navigation callback is installed.");
                    Assert.That(bootstrap.Controller.Session, Is.Not.SameAs(before));
                    Assert.That(bootstrap.Controller.UsesAlbumSlots, Is.True);
                    Assert.That(bootstrap.Controller.RunLoadout.Resolved.Count, Is.EqualTo(8));
                }
                finally { Object.DestroyImmediate(owner); }
            }
        }

        static void Inspect(GameObject owner, string route, ISet<string> editors)
        {
            foreach (var transform in owner.GetComponentsInChildren<Transform>(true))
            {
                foreach (var component in transform.GetComponents<Component>())
                {
                    Assert.That(component, Is.Not.Null, route + ": missing script on " + transform.name);
                    if (RetiredGraphics.Contains(component.GetType().Name))
                        editors.Add(route + ": " + component.GetType().Name);
                }
            }
        }

        static void AssertPlayerSceneScriptsResolve()
        {
            const string scene = "Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene), Is.Not.Null);
            var references = Regex.Matches(File.ReadAllText(scene), @"m_Script:\s*\{([^}]+)\}");
            Assert.That(references.Count, Is.GreaterThan(0), "The shipped scene must contain its bootstrap scripts.");
            foreach (Match reference in references)
            {
                var guid = Regex.Match(reference.Groups[1].Value, @"guid:\s*([a-fA-F0-9]{32})");
                Assert.That(guid.Success, Is.True, scene + ": missing serialized script reference " + reference.Value);
                string path = AssetDatabase.GUIDToAssetPath(guid.Groups[1].Value);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Assert.That(script, Is.Not.Null, scene + ": missing MonoScript " + guid.Groups[1].Value);
                Assert.That(script.GetClass(), Is.Not.Null, scene + ": unresolved component " + path);
            }
        }
    }
}
#endif
