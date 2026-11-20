#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AlbumPlayerControlsTests
    {
        AssemblyCommandFixture fixture;
        GameObject root;
        PlayerGameRoot game;
        PlayerShell shell;
        Keyboard keyboard;
        NativeInputIsolation isolation;
        InputSettings original, settings;
        [UnitySetUp] public IEnumerator SetUp()
        {
            isolation = new NativeInputIsolation(); original = InputSystem.settings;
            settings = Object.Instantiate(original);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var flags = original.hideFlags;
            try { original.hideFlags = HideFlags.None; InputSystem.settings = settings; }
            finally { original.hideFlags = flags; }
            fixture = new AssemblyCommandFixture();
            root = new GameObject("Isolated album player controls"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(fixture.BalanceAsset, true);
            root.SetActive(true); game.Run.AutomaticSimulation = false; game.Run.InitializeWorkshop(fixture.Store);
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>("AlbumPlayerControlKeyboard"); keyboard.MakeCurrent();
            Assert.That(game.StartBattle(), Is.True); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            Object.Destroy(root); yield return null; fixture.Dispose();
            InputSystem.settings = original; Object.Destroy(settings); isolation.Dispose();
        }
        IEnumerator Keys(params Key[] keys)
        {
            keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        [UnityTest] public IEnumerator CtrlFFocusesAlbumSearchAndEscapeClearsBeforeClosingThePageOrPicker()
        {
            game.ReturnToMap();
            game.Navigate(PlayerPage.Preparation);
            game.OpenAlbum();
            var search = shell.Album.Root.GetComponentInChildren<AlbumSearchInput>();
            yield return Keys(Key.LeftCtrl, Key.F);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(search.gameObject));
            Assert.That(search.isFocused, Is.True);
            search.text = "ремонт";
            Assert.That(shell.Album.SelectedId, Is.EqualTo("album.repair"));
            yield return Keys(Key.Escape);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));
            Assert.That(search.text, Is.Empty);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Album filter All"));
            yield return Keys(Key.Escape);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));

            var replace = shell.GetComponentsInChildren<Button>().Single(button => button.name == "Replace album entry 2");
            EventSystem.current.SetSelectedGameObject(replace.gameObject);
            replace.onClick.Invoke();
            var picker = shell.GetComponentsInChildren<WorkshopModal>().Single(modal => modal.name == "Assembly album picker");
            search = picker.GetComponentInChildren<AlbumSearchInput>();
            var tab = picker.GetComponentsInChildren<Button>().Single(button => button.name == "Album filter All");
            EventSystem.current.SetSelectedGameObject(tab.gameObject);
            yield return Keys(Key.LeftCtrl, Key.F);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(search.gameObject));
            Assert.That(search.isFocused, Is.True);
            search.text = "дозор";
            yield return Keys(Key.Escape);
            Assert.That(search.text, Is.Empty);
            Assert.That(picker.isActiveAndEnabled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Album filter All"));
            yield return Keys(Key.Escape);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(shell.GetComponentInChildren<AlbumSearchInput>(), Is.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(replace.gameObject));
        }

        [UnityTest] public IEnumerator FocusedNameInputBlocksThePlayerRootsBattleHotkeysAndBuildGate()
        {
            var field = new GameObject("Focused player name", typeof(RectTransform), typeof(InputField));
            field.transform.SetParent(root.transform, false); EventSystem.current.SetSelectedGameObject(field);
            Assert.That(game.CanBuild, Is.False);
            yield return Keys(Key.Digit8, Key.N, Key.F, Key.Space);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle)); Assert.That(game.Run.Paused, Is.False);
            Assert.That(game.Run.Session.WaveIndex, Is.Zero); Assert.That(game.Run.Speed, Is.EqualTo(1));
            Assert.That(game.Drawing.BlueprintSlot, Is.Zero);
        }
        [UnityTest] public IEnumerator ConfirmationBlocksTheRootsWaveAndSpeedHotkeysUntilCancelled()
        {
            shell.Confirm("Выйти?", "Проверка изолированного окна", () => Assert.Fail("Unconfirmed action executed"));
            Assert.That(game.CanBuild, Is.False);
            yield return Keys(Key.N, Key.F, Key.Digit8);
            Assert.That(game.Run.Session.WaveIndex, Is.Zero); Assert.That(game.Run.Speed, Is.EqualTo(1));
            shell.CancelConfirmation(); Assert.That(game.CanBuild, Is.True);
        }
        [UnityTest] public IEnumerator SpacePausesFieldButCannotCloseThePauseMenu()
        {
            var session = game.Run.Session;
            yield return Keys(Key.Space);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.TacticalPaused, Is.True); Assert.That(game.CanBuild, Is.True);
            game.Pause();
            yield return Keys(Key.Space);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause));
            Assert.That(game.Run.Paused, Is.True); Assert.That(game.Run.Session, Is.SameAs(session));
            game.Resume();
            Assert.That(game.Run.TacticalPaused, Is.True); Assert.That(game.CanBuild, Is.True);
            yield return Keys(Key.Space);
            Assert.That(game.Run.Paused, Is.False);
        }
    }
}
#endif
