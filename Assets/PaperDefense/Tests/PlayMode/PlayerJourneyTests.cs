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
    public sealed class PlayerJourneyTests
    {
        GameObject root;
        PlayerGameRoot game;
        GameBalanceAsset balance;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            root = new GameObject("Player journey fixture"); root.SetActive(false);
            var camera = new GameObject("Fixture camera", typeof(Camera)); camera.tag = "MainCamera"; camera.transform.SetParent(root.transform);
            balance = ScriptableObject.CreateInstance<GameBalanceAsset>(); balance.ResetToDefaults(); balance.planningSeconds = 1000;
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true); yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); Object.Destroy(balance); yield return null; }

        Button Find(string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
                if (button.name == name) return button;
            Assert.Fail("Missing active control: " + name); return null;
        }

        [UnityTest] public IEnumerator PreparationPickerChangesOnlyTheChosenSlotAfterAnExplicitChoice()
        {
            var choice = game.Run.Workshop.Loadout[0].AsVariant("picker-choice", "Новая страница");
            Assert.That(game.Run.SaveBlueprint(choice, out var reason), Is.True, reason);
            game.Navigate(PlayerPage.Preparation); yield return null;
            string first = game.Run.Workshop.Loadout[0].Id;
            string second = game.Run.Workshop.Loadout[1].Id;
            Find("Replace blueprint 0").onClick.Invoke(); yield return null;
            Assert.That(game.Run.Workshop.Loadout[0].Id, Is.EqualTo(first));
            Assert.That(Find("Start battle").IsInteractable(), Is.False);
            Find("Close blueprint picker").onClick.Invoke(); yield return null;
            Assert.That(Find("Start battle").IsInteractable(), Is.True);
            Assert.That(game.Run.Workshop.Loadout[0].Id, Is.EqualTo(first));
            Find("Replace blueprint 0").onClick.Invoke(); yield return null;
            Assert.That(Find("Pick blueprint " + second).interactable, Is.False);
            Find("Pick blueprint " + choice.Id).onClick.Invoke(); yield return null;
            Assert.That(game.Run.Workshop.Loadout[0].Id, Is.EqualTo(choice.Id));
            Assert.That(game.Run.Workshop.Loadout[1].Id, Is.EqualTo(second));
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
        }

        [UnityTest] public IEnumerator FreshLaunchShowsMenuWithoutAdvancingOrWritingProfile()
        {
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            Assert.That(game.Drawing.enabled, Is.False);
            long tick = game.Run.Session.TickIndex; yield return null;
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(root.GetComponentsInChildren<NotebookHud>(true), Is.Empty);
            Assert.That(Find("Open campaign").GetComponentInChildren<PlayerIconGraphic>(), Is.Not.Null);
            Assert.That(Find("Open campaign").GetComponentInChildren<Text>().text, Is.EqualTo("Начать игру"));
        }

        [UnityTest] public IEnumerator IconNavigationReachesRealBattleWithFrozenLoadout()
        {
            Find("Open campaign").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Find("Prepare mission").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Find("Starting material 1").onClick.Invoke();
            Find("Start battle").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.InWorkshop, Is.False);
            Assert.That(game.Run.BuildLoadout.Count, Is.EqualTo(4));
            Assert.That(game.Drawing.Tier, Is.EqualTo(MaterialTier.Ink));
            Assert.That(game.Drawing.enabled, Is.True);
            Assert.That(game.Drawing.HandleSessionHotkeys, Is.False);
        }

        [UnityTest] public IEnumerator PauseBlocksConstructionAndSimulationUntilResume()
        {
            Assert.That(game.StartBattle(), Is.True); yield return null;
            Find("Battle pause").onClick.Invoke();
            long tick = game.Run.Session.TickIndex;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause));
            Assert.That(game.Drawing.enabled, Is.False);
            Assert.That(game.Run.Paused, Is.True);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Find("Resume").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.Paused, Is.False);
            Assert.That(game.Drawing.enabled, Is.True);
        }

        [UnityTest] public IEnumerator SettingsKeepBattlePausedAndReturnToPause()
        {
            game.StartBattle(); game.Pause();
            Find("Pause settings").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
            Assert.That(game.Run.Paused, Is.True); Assert.That(game.Drawing.enabled, Is.False);
            bool ranges = game.World.ShowRanges;
            Find("Toggle ranges").onClick.Invoke();
            Assert.That(game.World.ShowRanges, Is.EqualTo(!ranges));
            Find("Close settings").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause));
        }

        [UnityTest] public IEnumerator PauseBlocksKeyboardSubmitOnPreviouslyFocusedBattleControl()
        {
            game.StartBattle(); yield return null;
            var speed = Find("Battle speed");
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(speed.gameObject);
            game.Pause();
            int previousSpeed = game.Run.Speed;
            Assert.That(speed.IsInteractable(), Is.False);
            UnityEngine.EventSystems.ExecuteEvents.Execute(speed.gameObject,
                new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.submitHandler);
            Assert.That(game.Run.Speed, Is.EqualTo(previousSpeed));
            game.Resume(); Assert.That(speed.IsInteractable(), Is.True);
        }

        [UnityTest] public IEnumerator LeavingBattleKeepsAlbumAndReturnsToSafePreparation()
        {
            string blueprint = game.Run.Workshop.Loadout[0].Id;
            int gold = game.Run.Workshop.Gold;
            game.StartBattle(); game.Pause(); Find("Leave battle").onClick.Invoke(); yield return null;
            Assert.That(game.Run.InWorkshop, Is.False);
            Assert.That(game.Run.Paused, Is.True);
            Find("Cancel confirmation").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause));
            Find("Leave battle").onClick.Invoke();
            Find("Confirm action").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Assert.That(game.Run.InWorkshop, Is.True); Assert.That(game.Drawing.enabled, Is.False);
            Assert.That(game.Run.Workshop.Loadout[0].Id, Is.EqualTo(blueprint));
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold));
        }

        [UnityTest] public IEnumerator ExistingWorkshopReturnsToPreparationBeforeStartingRun()
        {
            Find("workshop").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Workshop));
            Assert.That(Find("Start run"), Is.Not.Null);
            Find("Start run").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(root.GetComponentInChildren<WorkshopHud>(true).PresentationVisible, Is.False);
        }

        [UnityTest] public IEnumerator DirectSettingsPauseBattleAndHelpReturnsToSettings()
        {
            game.StartBattle(); game.OpenSettings(); yield return null;
            long tick = game.Run.Session.TickIndex;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(game.Run.Paused, Is.True);
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Find("Open help").onClick.Invoke();
            Find("Close help").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
            Find("Close settings").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause));
        }

        [UnityTest] public IEnumerator UpgradeChoicesDisplayAmountsFromTheActiveRules()
        {
            Assert.That(game.StartBattle(), Is.True);
            game.Navigate(PlayerPage.Upgrades); yield return null;
            var rules = game.Run.Session.Balance.Progression;
            var labels = new System.Collections.Generic.List<string>();
            foreach (var label in root.GetComponentsInChildren<Text>()) labels.Add(label.text);
            Assert.That(labels, Does.Contain("Темп стрельбы +" + Mathf.RoundToInt((rules.RapidFireMultiplier - 1) * 100) + "%"));
            Assert.That(labels, Does.Contain("Сразу +" + rules.SalvageImmediateInk + " штрихов\nНаграда волн +" +
                Mathf.RoundToInt((rules.SalvageWaveRewardMultiplier - 1) * 100) + "%\nВозврат +" + Mathf.RoundToInt(rules.SalvageRefundBonus * 100) + " п.п."));
            Assert.That(Find("Choose upgrade 2"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator ScalingWorkshopPreservesAcceptedGeometryAndUndo()
        {
            game.Navigate(PlayerPage.Workshop); yield return null;
            var wizard = root.GetComponentInChildren<WorkshopHud>().Guided;
            wizard.Next(); wizard.Next();
            var session = wizard.Drawing.Session;
            Assert.That(session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f,-.35f), new DrawingPoint(.35f,.35f), out _), Is.True);
            wizard.Drawing.Canvas.FinishStroke();
            string hash = session.Snapshot.GeometryHash;
            game.SetInterfaceScale(1.5f); yield return null; yield return null;
            Assert.That(wizard.Drawing.Session, Is.SameAs(session));
            Assert.That(session.Snapshot.GeometryHash, Is.EqualTo(hash));
            Assert.That(session.CanUndo, Is.True);
            wizard.Drawing.Canvas.Undo();
            Assert.That(session.Snapshot.StrokeCount, Is.Zero);
        }

        [UnityTest] public IEnumerator ReducedMotionDoesNotChangeSimulationOrSharedArt()
        {
            game.Run.AutomaticSimulation = false; game.StartBattle(); game.Pause();
            string original = JsonUtility.ToJson(NotebookVisualThemeAsset.Default.Data);
            var session = game.Run.Session; long tick = session.TickIndex; int ink = session.Ink;
            game.SetReducedMotion(true); yield return null;
            Assert.That(game.Run.Session, Is.SameAs(session)); Assert.That(session.TickIndex, Is.EqualTo(tick));
            Assert.That(session.Ink, Is.EqualTo(ink)); Assert.That(game.Run.Paused, Is.True);
            Assert.That(JsonUtility.ToJson(NotebookVisualThemeAsset.Default.Data), Is.EqualTo(original));
            Assert.That(game.World.VisualTheme, Is.Not.SameAs(NotebookVisualThemeAsset.Default));
            game.SetReducedMotion(false); Assert.That(game.ReducedMotion, Is.False);
        }

        [UnityTest] public IEnumerator AlbumSearchRetainsLoadoutAndShowsAnEmptyResult()
        {
            game.Navigate(PlayerPage.Workshop); yield return null;
            var wizard = root.GetComponentInChildren<WorkshopHud>().Guided; wizard.ShowAlbum(true); yield return null;
            string first = game.Run.Workshop.Loadout[0].Id;
            var field = root.GetComponentInChildren<InputField>(); field.onEndEdit.Invoke("Нет такого чертежа 7429"); yield return null;
            Assert.That(game.Run.Workshop.Loadout[0].Id, Is.EqualTo(first));
            bool found = false;
            foreach (var label in root.GetComponentsInChildren<Text>()) if (label.name == "Album empty") found = label.text.Contains("ничего не найдено");
            Assert.That(found, Is.True); Assert.That(Find("Wizard close album"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator ScrolledBlueprintCannotDrawOverFixedNavigation()
        {
            game.Navigate(PlayerPage.Preparation); yield return null;
            ScrollRect scroll = null; BlueprintSketch sketch = null;
            foreach (var item in root.GetComponentsInChildren<ScrollRect>()) if (item.name == "Preparation scroll") scroll = item;
            foreach (var item in scroll.GetComponentsInChildren<BlueprintSketch>()) { sketch = item; break; }
            var card = (RectTransform)sketch.transform.parent;
            card.anchoredPosition = new Vector2(card.anchoredPosition.x, 500);
            Canvas.ForceUpdateCanvases(); yield return null;
            Assert.That(sketch.canvasRenderer.cull, Is.True, "Blueprint must be clipped outside the preparation viewport.");
            Assert.That(typeof(IClippable).IsAssignableFrom(typeof(WorkshopMapSketch)), Is.True);
            Assert.That(typeof(IClippable).IsAssignableFrom(typeof(FootprintAxisGraphic)), Is.True);
        }
    }
}
