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

        [UnityTest] public IEnumerator KeyboardFocusRevealsPlayerControlsInBothScrollDirections()
        { yield return CheckScrollFocus(false); }

        [UnityTest] public IEnumerator KeyboardFocusRevealsWorkshopControlsInBothScrollDirections()
        { yield return CheckScrollFocus(true); }

        IEnumerator CheckScrollFocus(bool workshop)
        {
            var ui = new PlayerUi();
            var parent = root.GetComponentInChildren<Canvas>().transform;
            foreach (bool horizontal in new[] { false, true })
            {
                var content = ui.Scroll(parent, "Focus fixture", new Rect(20, 20, 260, 160), 600);
                var scroll = content.parent.GetComponent<ScrollRect>();
                scroll.horizontal = horizontal; scroll.vertical = !horizontal;
                content.sizeDelta = horizontal ? new Vector2(600, 160) : new Vector2(260, 600);
                Rect bounds = horizontal ? new Rect(440, 20, 140, 44) : new Rect(20, 500, 140, 44);
                var button = workshop
                    ? new WorkshopUi().Button(content, "Focus target", bounds, "Инструмент", () => { })
                    : ui.Button(content, "Focus target", bounds, PlayerIcon.Pencil, "Инструмент", () => { });
                Canvas.ForceUpdateCanvases(); yield return null;
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
                yield return null; Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 p = scroll.viewport.InverseTransformPoint(corner);
                    Assert.That(p.x, Is.InRange(scroll.viewport.rect.xMin - 1, scroll.viewport.rect.xMax + 1), "Focused control must be visible horizontally.");
                    Assert.That(p.y, Is.InRange(scroll.viewport.rect.yMin - 1, scroll.viewport.rect.yMax + 1), "Focused control must be visible vertically.");
                }
                Object.Destroy(scroll.gameObject); yield return null;
            }
        }

        [UnityTest] public IEnumerator KeyboardFocusRevealsNestedScrollAndClearsItsMarkOnDeselect()
        {
            var ui = new PlayerUi();
            var outer = ui.Scroll(root.GetComponentInChildren<Canvas>().transform, "Outer focus fixture", new Rect(20, 20, 280, 200), 800);
            var inner = ui.Scroll(outer, "Inner focus fixture", new Rect(10, 450, 260, 160), 600);
            var button = ui.Button(inner, "Nested focus target", new Rect(20, 500, 140, 44), PlayerIcon.Pencil, "Инструмент", () => { });
            var events = UnityEngine.EventSystems.EventSystem.current;
            yield return null; events.SetSelectedGameObject(button.gameObject); yield return null;
            foreach (var content in new[] { inner, outer })
            {
                var viewport = content.parent.GetComponent<ScrollRect>().viewport;
                var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                    Assert.That(viewport.InverseTransformPoint(corner).y, Is.InRange(viewport.rect.yMin - 1, viewport.rect.yMax + 1));
            }
            Assert.That(button.GetComponent<PlayerCardGraphic>().Focused, Is.True);
            events.SetSelectedGameObject(null);
            Assert.That(button.GetComponent<PlayerCardGraphic>().Focused, Is.False);
        }

        [UnityTest] public IEnumerator DrawingSummaryFollowsTheVisibleSheetAndStageChangesResetScroll()
        {
            game.Navigate(PlayerPage.Workshop); yield return null;
            game.SetInterfaceScale(1.5f);
            var wizard = root.GetComponentInChildren<WorkshopHud>().Guided;
            wizard.Next(); wizard.Next(); yield return null; yield return null;
            var nodes = root.GetComponentsInChildren<RectTransform>(true);
            var area = System.Array.Find(nodes, x => x.name == "Guided drawing work area");
            var summary = System.Array.Find(nodes, x => x.name == "Guided drawing summary");
            var scroll = System.Array.Find(root.GetComponentsInChildren<ScrollRect>(), x => x.name == "Workshop responsive scroll");
            if (summary.anchoredPosition.x < 1)
                Assert.That(-summary.anchoredPosition.y - area.rect.height * area.localScale.y, Is.EqualTo(20).Within(1));
            else Assert.That(summary.anchoredPosition.y, Is.EqualTo(0).Within(1));
            scroll.verticalNormalizedPosition = 0;
            wizard.GoToStep(1); yield return null; yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));
            scroll.verticalNormalizedPosition = 0;
            wizard.GoToStep(0); yield return null; yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));
        }

        [UnityTest] public IEnumerator WorkshopFocusAlsoRevealsTheEmbeddedProbeAndEditableName()
        {
            game.Navigate(PlayerPage.Workshop); game.SetInterfaceScale(1.5f); yield return null;
            var wizard = root.GetComponentInChildren<WorkshopHud>().Guided;
            wizard.Next(); wizard.Next();
            var session = wizard.Drawing.Session;
            session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f, -.35f), new DrawingPoint(.35f, .35f), out _);
            foreach (var mount in session.Blueprint.WeaponMounts)
                session.AddRectangle(DrawingBindings.Mount(mount.SlotIndex), new DrawingPoint(-.0625f, 0), new DrawingPoint(.0625f, .5f), out _);
            wizard.Drawing.Canvas.FinishStroke(); yield return null; yield return null;
            var probe = Find("Drawing probe fire");
            var events = UnityEngine.EventSystems.EventSystem.current;
            events.SetSelectedGameObject(probe.gameObject); yield return null;
            AssertVisibleInWorkshop((RectTransform)probe.transform);
            Assert.That(wizard.Next(), Is.True); yield return null; yield return null;
            var field = System.Array.Find(root.GetComponentsInChildren<InputField>(), x => x.name == "Wizard name");
            var scroll = System.Array.Find(root.GetComponentsInChildren<ScrollRect>(), x => x.name == "Workshop responsive scroll");
            scroll.verticalNormalizedPosition = 0;
            events.SetSelectedGameObject(field.gameObject); yield return null;
            AssertVisibleInWorkshop((RectTransform)field.transform);
        }

        void AssertVisibleInWorkshop(RectTransform target)
        {
            var viewport = System.Array.Find(root.GetComponentsInChildren<ScrollRect>(), x => x.name == "Workshop responsive scroll").viewport;
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            foreach (var corner in corners)
                Assert.That(viewport.InverseTransformPoint(corner).y, Is.InRange(viewport.rect.yMin - 1, viewport.rect.yMax + 1), target.name);
        }

        [UnityTest] public IEnumerator BattleHintUsesTheDockCaptionAndRestoresTheBlueprintName()
        {
            game.StartBattle(); yield return null;
            var label = System.Array.Find(root.GetComponentsInChildren<Text>(), x => x.name == "Battle tool hint");
            Assert.That(label.text, Does.Contain(GameText.Resolve(game.Run.BuildLoadout[0].Name)));
            var button = Find("Build tool 11");
            var events = UnityEngine.EventSystems.EventSystem.current;
            events.SetSelectedGameObject(button.gameObject); yield return null;
            Assert.That(label.text, Does.Contain("Окраска"));
            events.SetSelectedGameObject(null); yield return null;
            Assert.That(label.text, Does.Contain(GameText.Resolve(game.Run.BuildLoadout[0].Name)));
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
