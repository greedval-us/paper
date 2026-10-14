#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class PlayerAssetStandTests
    {
        PlayerAssetStand stand;
        string[] profileBefore;

        static string[] ProfileMetadata()
        {
            string file = WorkshopPersistence.ProfilePath, directory = Path.GetDirectoryName(file);
            return Directory.Exists(directory) ? Directory.GetFiles(directory, Path.GetFileName(file) + "*")
                .Select(path => new FileInfo(path)).OrderBy(info => info.Name)
                .Select(info => info.Name + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks).ToArray() : Array.Empty<string>();
        }

        [UnitySetUp] public IEnumerator Open()
        {
            profileBefore = ProfileMetadata();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/PaperDefense/Scenes/Tests/27_PlayerAssets.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            stand = Object.FindAnyObjectByType<PlayerAssetStand>(); Assert.That(stand, Is.Not.Null);
        }

        [UnityTearDown] public IEnumerator Close()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Player asset verification complete"));
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(ProfileMetadata(), Is.EqualTo(profileBefore), "Art inspection must not modify the player's saved album.");
        }

        Button Button(string name) => stand.GetComponentsInChildren<Button>().Single(value => value.name == name);

        [UnityTest] public IEnumerator InventoryContainsEveryTextureIconAndStaticArtShape()
        {
            Assert.That(Object.FindObjectsByType<RunController>(), Is.Empty);
            Assert.That(stand.TextureCount, Is.EqualTo(Resources.LoadAll<Texture2D>("Art/Player").Length));
            Assert.That(stand.TextureCount, Is.GreaterThanOrEqualTo(2));
            for (int tab = 1; tab <= 3; tab++)
            {
                Button("Asset tab " + tab).onClick.Invoke(); yield return null;
                int expected = tab == 1 ? Enum.GetValues(typeof(PlayerIcon)).Length : NotebookVisualThemeAsset.Default.Data.Shapes.Count(shape =>
                    tab == 2 ? shape.Id.StartsWith("ui.", StringComparison.Ordinal) : shape.Id.StartsWith("terrain.", StringComparison.Ordinal) || shape.Id.StartsWith("object.", StringComparison.Ordinal));
                Assert.That(stand.ItemCount, Is.EqualTo(expected));
                var visited = new System.Collections.Generic.HashSet<string>();
                while (true)
                {
                    var cards = stand.GetComponentsInChildren<Button>().Where(value => value.name.StartsWith("Asset ", StringComparison.Ordinal) && !value.name.StartsWith("Asset tab ", StringComparison.Ordinal)).ToArray();
                    Assert.That(cards.Length, Is.EqualTo(stand.VisibleItemCount));
                    foreach (var card in cards)
                    {
                        Assert.That(visited.Add(card.name), Is.True, "Duplicate asset in pagination: " + card.name);
                        card.onClick.Invoke(); Canvas.ForceUpdateCanvases();
                        Graphic graphic = tab == 1 ? (Graphic)card.GetComponentInChildren<PlayerIconGraphic>() : card.GetComponentInChildren<NotebookArtGraphic>();
                        Assert.That(graphic.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0), card.name);
                    }
                    foreach (var label in stand.GetComponentsInChildren<Text>())
                        Assert.That(label.text, Does.Not.Contain("[missing:").And.Not.Contain("[invalid:"));
                    var next = Button("Next asset page"); if (!next.IsInteractable()) break;
                    next.onClick.Invoke(); yield return null;
                }
                Assert.That(visited.Count, Is.EqualTo(expected));
            }
        }

        [UnityTest] public IEnumerator InteractivePreviewUsesCandidateStyleAndDisposesAllGameStateOnReturn()
        {
            Button("Open player preview").onClick.Invoke(); yield return null;
            var game = stand.Preview;
            Assert.That(game, Is.Not.Null); Assert.That(game.NotebookStyle, Is.True);
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Assert.That(game.GetComponentsInChildren<PlayerCardGraphic>().All(card => card.NotebookStyle), Is.True);
            Assert.That(game.StartBattle(), Is.True); game.Pause(); yield return null;
            Assert.That(game.Drawing.enabled, Is.False);
            Button("Close player preview").onClick.Invoke(); yield return null;
            Assert.That(stand.Preview, Is.Null);
            Assert.That(Object.FindObjectsByType<RunController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length, Is.EqualTo(1));
            Assert.That(Camera.main.enabled, Is.True);
            Button("Open player preview").onClick.Invoke(); yield return null;
            Assert.That(stand.Preview.Page, Is.EqualTo(PlayerPage.Home), "Reopening must start a fresh memory-only session.");
        }
    }
}
#endif
