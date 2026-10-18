#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
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
    public sealed class AlbumNavigationTests
    {
        AssemblyCommandFixture fixture;
        GameObject root;
        PlayerGameRoot game;
        PlayerShell shell;
        [UnitySetUp] public IEnumerator SetUp()
        {
            fixture = new AssemblyCommandFixture(94, 390);
            root = new GameObject("Isolated album navigation"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(fixture.BalanceAsset, true);
            root.SetActive(true); game.Run.AutomaticSimulation = false;
            game.Run.InitializeWorkshop(fixture.Store);
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            game.Navigate(PlayerPage.Home); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); yield return null; fixture.Dispose(); }
        Button Button(string name) => root.GetComponentsInChildren<Button>().Single(b => b.name == name);
        void Click(string name) => Button(name).onClick.Invoke();

        [UnityTest] public IEnumerator OpeningAlbumWithTheKeyboardChoosesAnActiveAlbumControl()
        {
            var button = Button("Open album"); EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));
            var focus = EventSystem.current.currentSelectedGameObject;
            Assert.That(focus, Is.Not.Null);
            Assert.That(focus.transform.IsChildOf(shell.Album.Root), Is.True);
            Assert.That(focus.GetComponent<Selectable>().IsInteractable(), Is.True); yield return null;
        }

        [UnityTest] public IEnumerator ClearingASlotWithTheKeyboardFocusesItsReplacementControl()
        {
            game.Navigate(PlayerPage.Preparation);
            var button = Button("Clear album entry 6"); EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            Assert.That(game.Run.Workshop.LoadoutEntries[6], Is.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("Replace album entry 6").gameObject));
            yield return null;
        }

        [UnityTest] public IEnumerator HomeAlbumReturnsToHomeAndPreparationAlbumReturnsToPreparation()
        {
            Click("Open album"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));
            Click("Album close"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            game.Navigate(PlayerPage.Preparation); game.OpenAlbum();
            Click("Album close"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(fixture.Store.SaveCalls, Is.Zero); yield return null;
        }

        [UnityTest] public IEnumerator MapAlbumUsesTheCurrentLoadoutAndReturnsToTheMap()
        {
            Assert.That(game.Run.AssemblyCommands.AssignSlot(7, null, Guid.NewGuid()).Success, Is.True);
            game.Navigate(PlayerPage.Map);
            var metric = root.GetComponentsInChildren<Text>().Single(t => t.name == "Mission album loadout");
            Assert.That(metric.text, Does.Contain("7").And.Contain("8"));
            int writes = fixture.Store.SaveCalls;
            Click("Map album"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));
            Click("Album close"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); yield return null;
        }

        [UnityTest] public IEnumerator EditingAnAlbumPageSavesItsIdentityAndReturnsToTheSameSelection()
        {
            game.OpenAlbum(); shell.Album.SetFilter(AlbumFilter.Towers);
            Click("Album choose album.guard"); Click("Album edit");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Workshop));
            var composite = root.GetComponentInChildren<WorkshopHud>(true).Composite;
            Assert.That(composite.DraftSession.Draft.Id, Is.EqualTo("album.guard"));
            var field = composite.Root.GetComponentInChildren<InputField>();
            field.text = "Новый дозор"; field.onEndEdit.Invoke(field.text); Click("Composite save");
            Click("Composite return");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));
            Assert.That(shell.Album.Filter, Is.EqualTo(AlbumFilter.Towers));
            Assert.That(shell.Album.SelectedId, Is.EqualTo("album.guard"));
            Assert.That(game.Run.Workshop.Entries.Single(e => e.Id == "album.guard").Name, Is.EqualTo("Новый дозор"));
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(94)); yield return null;
        }

        [UnityTest] public IEnumerator ReturningFromAnUnsavedEditKeepsThePublishedPageAndSelectedIdentity()
        {
            game.OpenAlbum(); Click("Album choose album.guard"); Click("Album edit");
            var composite = root.GetComponentInChildren<WorkshopHud>(true).Composite;
            composite.DraftSession.Rename("Не сохранять"); composite.RefreshStatus();
            Click("Composite return");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));
            Assert.That(shell.Album.SelectedId, Is.EqualTo("album.guard"));
            Assert.That(game.Run.Workshop.Entries.Single(e => e.Id == "album.guard").Name, Is.Not.EqualTo("Не сохранять"));
            Assert.That(fixture.Store.SaveCalls, Is.Zero); yield return null;
        }

        [UnityTest] public IEnumerator ReadinessQueryMatchesStartAndDoesNotPublishOrSaveAnything()
        {
            var commands = game.Run.AssemblyCommands;
            for (int i = 1; i < 8; i++)
            { var result = commands.AssignSlot(i, null, Guid.NewGuid()); Assert.That(result.Success, Is.True, result.Code + ":" + string.Join(";", result.Diagnostics.SelectMany(d => d.Arguments))); }
            int writes = fixture.Store.SaveCalls; var snapshot = game.Run.RunLoadout;
            Assert.That(game.Run.CanStartAlbumRun(out var reason), Is.True, reason);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
            Assert.That(game.Run.RunLoadout, Is.SameAs(snapshot)); Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Run.StartRun(out reason), Is.True, reason);
            Assert.That(game.Run.RunLoadout.Entries[0].Id, Is.EqualTo("album.guard"));
            Assert.That(game.Run.RunLoadout.Entries.Skip(1).All(e => e == null), Is.True); yield return null;
        }

        [UnityTest] public IEnumerator SupportOnlyReadinessAndActualStartBothRejectTheLoadout()
        {
            for (int i = 0; i < 4; i++)
            { var result = game.Run.AssemblyCommands.AssignSlot(i, null, Guid.NewGuid()); Assert.That(result.Success, Is.True, result.Code + ":" + string.Join(";", result.Diagnostics.SelectMany(d => d.Arguments))); }
            int writes = fixture.Store.SaveCalls;
            Assert.That(game.Run.CanStartAlbumRun(out var reason), Is.False);
            Assert.That(GameText.Resolve(reason), Does.Contain("атак"));
            Assert.That(game.Run.StartRun(out _), Is.False);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(game.Run.InWorkshop, Is.True);
            yield return null;
        }

        [TestCase(1024, 768)] [TestCase(1280, 720)] [TestCase(1920, 1080)]
        public void HomeAndMapKeepNativeActionsAndActualProgressInsideThePage(int width, int height)
        {
            foreach (var destination in new[] { PlayerPage.Home, PlayerPage.Map })
            {
                game.Navigate(destination); shell.ApplyLayout(width, height); Canvas.ForceUpdateCanvases();
                var page = root.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Page " + destination);
                Assert.That(page.GetComponentsInChildren<ScrollRect>(), Is.Empty);
                var progress = page.GetComponentsInChildren<Text>().Single(t => t.name == "Player progress");
                Assert.That(progress.text, Does.Contain("4").And.Contain("390"));
                if (destination == PlayerPage.Map)
                {
                    Assert.That(page.GetComponentsInChildren<Button>().Any(b => b.name == "Map album"), Is.True);
                    var metric = page.GetComponentsInChildren<Text>().Single(t => t.name == "Mission album loadout");
                    Assert.That(metric.text, Does.Contain("8"));
                    var colors = page.GetComponentsInChildren<Text>().Where(t => t.text == "Красная" || t.text == "Синяя" || t.text == "Зелёная").ToArray();
                    Assert.That(colors.Length, Is.EqualTo(3));
                    var prepare = page.GetComponentsInChildren<Button>().Single(b => b.name == "Prepare mission");
                    var prepareCorners = new Vector3[4]; prepare.GetComponent<RectTransform>().GetWorldCorners(prepareCorners);
                    foreach (var color in colors)
                    {
                        var colorCorners = new Vector3[4]; color.rectTransform.GetWorldCorners(colorCorners);
                        Assert.That(colorCorners[0].y, Is.GreaterThanOrEqualTo(prepareCorners[1].y), "Inkwell label overlaps preparation action");
                    }
                }
                foreach (var button in page.GetComponentsInChildren<Button>())
                {
                    var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = page.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(page.rect.xMin - 1, page.rect.xMax + 1), button.name);
                        Assert.That(point.y, Is.InRange(page.rect.yMin - 1, page.rect.yMax + 1), button.name);
                    }
                }
            }
        }
    }
}
#endif
