#if UNITY_EDITOR
using System.Collections;
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
        [UnityTest] public IEnumerator SpaceResumesTheSamePausedBattleWithItsModalOpen()
        {
            var session = game.Run.Session; game.Pause();
            yield return Keys(Key.Space);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.Paused, Is.False); Assert.That(game.Run.Session, Is.SameAs(session));
        }
    }
}
#endif
