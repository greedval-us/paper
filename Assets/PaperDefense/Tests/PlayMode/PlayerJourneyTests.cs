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

        [UnityTest] public IEnumerator StandardReviewRemainsVisibleAndStageChangesResetScroll()
        {
            game.Navigate(PlayerPage.Workshop); yield return null;
            var wizard = root.GetComponentInChildren<WorkshopHud>().Guided;
            Assert.That(wizard.Next(), Is.True); Assert.That(wizard.Next(), Is.True);
            yield return null; yield return null;
            var responsive = root.GetComponentInChildren<WorkshopResponsiveLayout>();
            responsive.enabled = false; responsive.Apply(1024, 768);
            var nodes = root.GetComponentsInChildren<RectTransform>(true);
            var review = System.Array.Find(nodes, x => x.name == "Wizard review");
            var heading = System.Array.Find(nodes, x => x.name == "Review heading");
            var scroll = System.Array.Find(root.GetComponentsInChildren<ScrollRect>(true), x => x.name == "Workshop responsive scroll");
            Assert.That(wizard.Step, Is.EqualTo(2)); Assert.That(wizard.Drawing, Is.Null);
            Assert.That(wizard.Draft.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(review, Is.Not.Null); Assert.That(heading, Is.Not.Null); Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.gameObject.activeInHierarchy, Is.True);
            Assert.That(review.IsChildOf(scroll.content), Is.True);
            Canvas.ForceUpdateCanvases();
            AssertVisibleInWorkshop(heading);
            AssertVisibleInCanvas((RectTransform)Find("Wizard next").transform);
            AssertVisibleInCanvas((RectTransform)Find("Wizard back").transform);
            for (int i = 0; i < 3; i++) AssertVisibleInCanvas((RectTransform)Find("Wizard step " + i).transform);

            // Seed a real scroll range, as a previously visited Review can leave one behind.
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, scroll.viewport.rect.height + 300);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.01f));
            Assert.That(wizard.GoToStep(1), Is.True); responsive.Apply(1024, 768);
            yield return null; yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));
            scroll.verticalNormalizedPosition = 0;
            Assert.That(wizard.GoToStep(0), Is.True); responsive.Apply(1024, 768);
            yield return null; yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));
        }

        [UnityTest] public IEnumerator WorkshopReviewFocusRevealsTheTrialAndEditableName()
        {
            game.Navigate(PlayerPage.Workshop); yield return null;
            var wizard = root.GetComponentInChildren<WorkshopHud>().Guided;
            Assert.That(wizard.Next(), Is.True); Assert.That(wizard.Next(), Is.True);
            yield return null; yield return null;
            var responsive = root.GetComponentInChildren<WorkshopResponsiveLayout>();
            responsive.enabled = false; responsive.Apply(1024, 768);
            var trial = Find("Wizard full trial");
            var events = UnityEngine.EventSystems.EventSystem.current;
            var scroll = System.Array.Find(root.GetComponentsInChildren<ScrollRect>(true), x => x.name == "Workshop responsive scroll");
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.gameObject.activeInHierarchy, Is.True);
            Assert.That(wizard.Step, Is.EqualTo(2)); Assert.That(wizard.Drawing, Is.Null);
            string draft = JsonUtility.ToJson(new BlueprintData(wizard.Draft));
            scroll.verticalNormalizedPosition = 0;
            events.SetSelectedGameObject(trial.gameObject); yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(events.currentSelectedGameObject, Is.SameAs(trial.gameObject));
            AssertVisibleInWorkshop((RectTransform)trial.transform);
            var field = System.Array.Find(root.GetComponentsInChildren<InputField>(), x => x.name == "Wizard name");
            Assert.That(field, Is.Not.Null);
            Assert.That(scroll.gameObject.activeInHierarchy, Is.True, "Review retains its scrollable name and save controls.");
            Assert.That(field.transform.IsChildOf(scroll.content), Is.True);
            scroll.verticalNormalizedPosition = 0;
            events.SetSelectedGameObject(field.gameObject); yield return null;
            AssertVisibleInWorkshop((RectTransform)field.transform);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(field.gameObject));
            Assert.That(JsonUtility.ToJson(new BlueprintData(wizard.Draft)), Is.EqualTo(draft));
        }

        void AssertVisibleInWorkshop(RectTransform target)
        {
            var scroll = System.Array.Find(root.GetComponentsInChildren<ScrollRect>(true), x => x.name == "Workshop responsive scroll");
            Assert.That(scroll, Is.Not.Null); Assert.That(scroll.gameObject.activeInHierarchy, Is.True);
            AssertContained(scroll.viewport.rect, BoundsInPage(scroll.viewport, target), target.name);
        }

        static void AssertVisibleInCanvas(RectTransform target)
        {
            Assert.That(target.gameObject.activeInHierarchy, Is.True, target.name);
            var canvas = target.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
            AssertContained(canvas.rect, BoundsInPage(canvas, target), target.name);
        }

        static void AssertContained(Rect available, Rect bounds, string context)
        {
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(available.xMin - 1), context);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(available.xMax + 1), context);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(available.yMin - 1), context);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(available.yMax + 1), context);
        }

        [UnityTest] public IEnumerator BattleHintUsesTheDockCaptionAndRestoresTheBlueprintName()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var inputModule = events.currentInputModule;
            bool inputWasEnabled = inputModule != null && inputModule.enabled;
            if (inputModule != null) inputModule.enabled = false;
            try
            {
                game.StartBattle(); yield return null;
                var label = System.Array.Find(root.GetComponentsInChildren<Text>(), x => x.name == "Battle tool hint");
                Assert.That(label.text, Does.Contain(GameText.Resolve(game.Run.BuildLoadout[0].Name)));
                var button = Find("Build tool 11");
                events.SetSelectedGameObject(button.gameObject); yield return null;
                Assert.That(label.text, Does.Contain("Окраска"));
                events.SetSelectedGameObject(null); yield return null;
                Assert.That(label.text, Does.Contain(GameText.Resolve(game.Run.BuildLoadout[0].Name)));
            }
            finally
            {
                if (inputModule != null) inputModule.enabled = inputWasEnabled;
            }
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
            Assert.That(Find("Open campaign").GetComponentInChildren<Text>().text, Is.EqualTo("Играть"));
        }

        [UnityTest] public IEnumerator HomeActionsRemainVisibleAndSeparateAcrossWindowSizes()
        {
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            string[] actions = { "Open campaign", "workshop", "settings", "help", "exit" };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                shell.ApplyLayout(sizes[size, 0], sizes[size, 1]);
                Canvas.ForceUpdateCanvases();
                var page = System.Array.Find(root.GetComponentsInChildren<RectTransform>(), x => x.name == "Page Home");
                Assert.That(page, Is.Not.Null);
                var bounds = new Rect[actions.Length];
                string context = sizes[size, 0] + "x" + sizes[size, 1];
                for (int i = 0; i < actions.Length; i++)
                {
                    var button = Find(actions[i]);
                    Assert.That(button.IsInteractable(), Is.True, actions[i] + " at " + context);
                    bounds[i] = BoundsInPage(page, (RectTransform)button.transform);
                    Assert.That(bounds[i].width, Is.GreaterThan(0), actions[i] + " at " + context);
                    Assert.That(bounds[i].height, Is.GreaterThan(0), actions[i] + " at " + context);
                    Assert.That(bounds[i].xMin, Is.GreaterThanOrEqualTo(page.rect.xMin - 1), actions[i] + " at " + context);
                    Assert.That(bounds[i].xMax, Is.LessThanOrEqualTo(page.rect.xMax + 1), actions[i] + " at " + context);
                    Assert.That(bounds[i].yMin, Is.GreaterThanOrEqualTo(page.rect.yMin - 1), actions[i] + " at " + context);
                    Assert.That(bounds[i].yMax, Is.LessThanOrEqualTo(page.rect.yMax + 1), actions[i] + " at " + context);
                    for (int previous = 0; previous < i; previous++)
                        Assert.That(bounds[i].Overlaps(bounds[previous]), Is.False,
                            actions[i] + " overlaps " + actions[previous] + " at " + context);
                }
                // Assert before yielding: Update restores the real Screen dimensions on the next frame.
                yield return null;
            }
        }

        [UnityTest] public IEnumerator LongerHomeCaptionRemainsReadable()
        {
            var caption = Find("workshop").GetComponentInChildren<Text>();
            caption.text = "Мастерская чертежей и рисунков";
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(caption.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(caption.text.Length),
                "A longer home action must fit rather than truncate after its first word.");
        }

        static Rect BoundsInPage(RectTransform page, RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 min = page.InverseTransformPoint(corners[0]), max = min;
            foreach (var corner in corners)
            {
                Vector2 local = page.InverseTransformPoint(corner);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest] public IEnumerator HomeSettingsHelpAndCancelledExitPreserveTheIdleSession()
        {
            var session = game.Run.Session;
            long tick = session.TickIndex;
            Find("settings").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
            Find("Close settings").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Find("help").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Help));
            Find("Close help").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Find("exit").onClick.Invoke(); yield return null;
            Assert.That(root.GetComponentInChildren<PlayerShell>().HasConfirmation, Is.True);
            Assert.That(Find("Open campaign").IsInteractable(), Is.False);
            Find("Cancel confirmation").onClick.Invoke(); yield return null;
            Assert.That(root.GetComponentInChildren<PlayerShell>().HasConfirmation, Is.False);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Assert.That(Find("Open campaign").IsInteractable(), Is.True);
            Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Drawing.enabled, Is.False);
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

        [UnityTest] public IEnumerator MapKeepsMissionAndNavigationVisibleAcrossWindowSizes()
        {
            game.Navigate(PlayerPage.Map);
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                shell.ApplyLayout(sizes[size, 0], sizes[size, 1]);
                Canvas.ForceUpdateCanvases();
                var page = ActiveRect("Page Map");
                string context = sizes[size, 0] + "x" + sizes[size, 1];
                Assert.That(page.GetComponentsInChildren<ScrollRect>(), Is.Empty,
                    "The mission page must fit without scrolling at " + context);
                foreach (string name in new[] { "Mission map", "Mission information" })
                    AssertWithin(page, ActiveRect(name), context);
                var back = Find("Back"); var prepare = Find("Prepare mission");
                Assert.That(back.IsInteractable(), Is.True, context);
                Assert.That(prepare.IsInteractable(), Is.True, context);
                AssertWithin(page, (RectTransform)back.transform, context);
                AssertWithin(page, (RectTransform)prepare.transform, context);
                AssertReadable(prepare.GetComponentInChildren<Text>(), shell.Layout.Scale, context);
                Assert.That(BoundsInPage(page, (RectTransform)back.transform).Overlaps(
                    BoundsInPage(page, (RectTransform)prepare.transform)), Is.False, context);
                yield return null;
            }
        }

        [UnityTest] public IEnumerator NotebookIllustrationsKeepTheirWholePageAcrossWindowSizes()
        {
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                shell.ApplyLayout(sizes[size, 0], sizes[size, 1]);
                string context = sizes[size, 0] + "x" + sizes[size, 1];
                game.Navigate(PlayerPage.Home); Canvas.ForceUpdateCanvases();
                AssertCompleteIllustration(ActiveRect("Page Home"), "Home illustration", context);
                game.Navigate(PlayerPage.Map); Canvas.ForceUpdateCanvases();
                AssertCompleteIllustration(ActiveRect("Mission map"), "Mission illustration", context);
                game.Navigate(PlayerPage.Preparation); Canvas.ForceUpdateCanvases();
                AssertCompleteIllustration(ActiveRect("Page Preparation"), "Notebook landscape", context);
                yield return null;
            }
        }

        void AssertCompleteIllustration(RectTransform boundary, string name, string context)
        {
            var image = System.Array.Find(boundary.GetComponentsInChildren<Image>(), x => x.name == name);
            Assert.That(image, Is.Not.Null, "Missing whole-page illustration at " + context);
            Assert.That(image.isActiveAndEnabled, Is.True, name + " must render at " + context);
            Assert.That(image.canvasRenderer.cull, Is.False, name + " must stay visible at " + context);
            Assert.That(image.sprite, Is.Not.Null, name + " must load its production illustration.");
            AssertWithin(boundary, image.rectTransform, context);
            float aspect = image.sprite.rect.width / image.sprite.rect.height;
            Assert.That(image.rectTransform.rect.width / image.rectTransform.rect.height, Is.EqualTo(aspect).Within(.001f),
                name + " must retain its native aspect rather than fill and crop the page at " + context);
            for (Transform parent = image.transform.parent; parent != null; parent = parent.parent)
            {
                Assert.That(parent.GetComponent<RectMask2D>(), Is.Null,
                    name + " must not lose its edges to a viewport mask at " + context);
                if (parent == boundary) break;
            }
        }

        [UnityTest] public IEnumerator PreparationShowsAllFourCardsInOneRowWithoutScrollingAcrossWindowSizes()
        {
            game.Navigate(PlayerPage.Preparation);
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                shell.ApplyLayout(sizes[size, 0], sizes[size, 1]);
                Canvas.ForceUpdateCanvases();
                var page = ActiveRect("Page Preparation");
                string context = sizes[size, 0] + "x" + sizes[size, 1];
                Assert.That(page.GetComponentsInChildren<ScrollRect>(), Is.Empty,
                    "All four blueprints and materials must fit without scrolling at " + context);
                string[] actions = { "Back", "Edit album", "Open preparation album", "Start battle" };
                var actionBounds = new Rect[actions.Length];
                for (int action = 0; action < actions.Length; action++)
                {
                    var button = Find(actions[action]);
                    Assert.That(button.IsInteractable(), Is.True, actions[action] + " at " + context);
                    AssertWithin(page, (RectTransform)button.transform, context);
                    AssertReadable(button.GetComponentInChildren<Text>(), shell.Layout.Scale, context);
                    actionBounds[action] = BoundsInPage(page, (RectTransform)button.transform);
                    for (int previous = 0; previous < action; previous++)
                        Assert.That(actionBounds[action].Overlaps(actionBounds[previous]), Is.False,
                            actions[action] + " overlaps " + actions[previous] + " at " + context);
                }
                var slotBounds = new Rect[4];
                for (int slot = 0; slot < 4; slot++)
                {
                    var card = ActiveRect("Loadout " + slot);
                    AssertWithin(page, card, context);
                    slotBounds[slot] = BoundsInPage(page, card);
                    Assert.That(slotBounds[slot].center.y, Is.EqualTo(slotBounds[0].center.y).Within(1),
                        "All four blueprints must share a single row at " + context);
                    for (int previous = 0; previous < slot; previous++)
                        Assert.That(slotBounds[slot].Overlaps(slotBounds[previous]), Is.False,
                            "Loadout cards overlap at " + context);
                    for (int action = 0; action < actionBounds.Length; action++)
                        Assert.That(slotBounds[slot].Overlaps(actionBounds[action]), Is.False,
                            "Loadout " + slot + " covers " + actions[action] + " at " + context);
                    foreach (var graphic in card.GetComponentsInChildren<Graphic>())
                    {
                        AssertWithin(card, graphic.rectTransform, context);
                        if (graphic.isActiveAndEnabled)
                            Assert.That(graphic.canvasRenderer.cull, Is.False,
                                "Card content must remain visible: " + graphic.name + " at " + context);
                    }
                    AssertReadable(ActiveText("Loadout name " + slot), shell.Layout.Scale, context);
                    AssertReadable(ActiveText("Loadout description " + slot), shell.Layout.Scale, context);
                    AssertReadable(ActiveText("Loadout cost " + slot), shell.Layout.Scale, context);
                    var replace = Find("Replace blueprint " + slot);
                    Assert.That(replace.IsInteractable(), Is.True, context);
                    AssertReadable(replace.GetComponentInChildren<Text>(), shell.Layout.Scale, context);
                }
                var materialBounds = new Rect[3];
                    AssertReadable(ActiveText("Preparation material description"), shell.Layout.Scale, context);
                for (int tier = 0; tier < 3; tier++)
                {
                    var material = Find("Starting material " + tier);
                    Assert.That(material.IsInteractable(), Is.True, context);
                    AssertWithin(page, (RectTransform)material.transform, context);
                    AssertReadable(material.GetComponentInChildren<Text>(), shell.Layout.Scale, context);
                    materialBounds[tier] = BoundsInPage(page, (RectTransform)material.transform);
                    foreach (var card in slotBounds)
                        Assert.That(materialBounds[tier].Overlaps(card), Is.False, "Material overlaps a blueprint at " + context);
                    foreach (var action in actionBounds)
                        Assert.That(materialBounds[tier].Overlaps(action), Is.False, "Material overlaps navigation at " + context);
                    for (int previous = 0; previous < tier; previous++)
                        Assert.That(materialBounds[tier].Overlaps(materialBounds[previous]), Is.False,
                            "Material choices overlap at " + context);
                }
                // Validate the requested virtual size before Update returns to the real Game view size.
                yield return null;
            }
        }

        [UnityTest] public IEnumerator MissionSummaryUsesTheActiveRulesRatherThanNumbersFromTheSketch()
        {
            balance.workshopStartingInk = 913; balance.baseHealth = 337;
            balance.waves = new[] { balance.waves[0], balance.waves[1], balance.waves[2] };
            game.Run.OpenWorkshop(); game.Navigate(PlayerPage.Map); yield return null;
            Assert.That(ActiveText("Mission waves").text, Does.Contain(game.Run.Session.Balance.Waves.Count.ToString()));
            Assert.That(ActiveText("Mission starting ink").text, Does.Contain("913"));
            Assert.That(ActiveText("Mission castle health").text, Does.Contain("337"));
            var session = game.Run.Session;
            Find("Prepare mission").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Drawing.enabled, Is.False);
        }

        [UnityTest] public IEnumerator SelectingPreparationMaterialUpdatesAllFourConstructionPrices()
        {
            foreach (var material in balance.materials) material.cost = 1.1f + (int)material.tier * 1.3f;
            game.Run.OpenWorkshop(); game.Navigate(PlayerPage.Preparation); yield return null;
            var session = game.Run.Session;
            var ids = new string[4];
            for (int slot = 0; slot < 4; slot++) ids[slot] = game.Run.Workshop.Loadout[slot].Id;
            int previousCost = -1;
            for (int material = 0; material < 3; material++)
            {
                Find("Starting material " + material).onClick.Invoke();
                Assert.That(game.Drawing.Tier, Is.EqualTo((MaterialTier)material));
                for (int slot = 0; slot < 4; slot++)
                {
                    var blueprint = game.Run.Workshop.Loadout[slot];
                    int expected = game.Run.PreviewBlueprint(blueprint, game.Drawing.Tier).Cost;
                    Assert.That(ActiveText("Loadout cost " + slot).text, Does.Contain(expected.ToString()),
                        "Slot " + slot + " must quote the selected material.");
                    Assert.That(blueprint.Id, Is.EqualTo(ids[slot]));
                }
                int firstCost = game.Run.PreviewBlueprint(game.Run.Workshop.Loadout[0], game.Drawing.Tier).Cost;
                Assert.That(firstCost, Is.GreaterThan(previousCost)); previousCost = firstCost;
                Assert.That(game.Run.Session, Is.SameAs(session));
                Assert.That(game.Run.InWorkshop, Is.True);
                yield return null;
            }
        }

        [UnityTest] public IEnumerator SelectingPreparationMaterialPreservesCompactCardPositionsAndKeyboardFocus()
        {
            yield return null; yield return null;
            game.Navigate(PlayerPage.Preparation); yield return null;
            root.GetComponentInChildren<PlayerShell>().ApplyLayout(1024, 768); Canvas.ForceUpdateCanvases();
            var page = ActiveRect("Page Preparation");
            Assert.That(page.GetComponentsInChildren<ScrollRect>(), Is.Empty);
            var cards = new Rect[4]; var prices = new Rect[4];
            for (int slot = 0; slot < 4; slot++)
            {
                cards[slot] = BoundsInPage(page, ActiveRect("Loadout " + slot));
                prices[slot] = BoundsInPage(page, ActiveText("Loadout cost " + slot).rectTransform);
            }
            var material = Find("Starting material 2");
            var events = UnityEngine.EventSystems.EventSystem.current;
            events.SetSelectedGameObject(material.gameObject);
            Canvas.ForceUpdateCanvases();
            material.onClick.Invoke();
            Canvas.ForceUpdateCanvases(); page = ActiveRect("Page Preparation");
            Assert.That(page.GetComponentsInChildren<ScrollRect>(), Is.Empty);
            Assert.That(events.currentSelectedGameObject, Is.Not.Null, "Keyboard selection must survive a material change.");
            Assert.That(events.currentSelectedGameObject.name, Is.EqualTo("Starting material 2"));
            Assert.That(game.Drawing.Tier, Is.EqualTo(MaterialTier.Marker));
            for (int slot = 0; slot < 4; slot++)
            {
                AssertSameBounds(cards[slot], BoundsInPage(page, ActiveRect("Loadout " + slot)), "Card " + slot);
                AssertSameBounds(prices[slot], BoundsInPage(page, ActiveText("Loadout cost " + slot).rectTransform), "Price " + slot);
                Assert.That(ActiveText("Loadout cost " + slot).text,
                    Does.Contain(game.Run.PreviewBlueprint(game.Run.Workshop.Loadout[slot], MaterialTier.Marker).Cost.ToString()));
            }
        }

        [UnityTest] public IEnumerator PreparationAlbumCanBeCancelledWithoutChangingTheLoadoutOrStartingBattle()
        {
            game.Navigate(PlayerPage.Preparation); yield return null;
            Find("Starting material 2").onClick.Invoke();
            var session = game.Run.Session; long tick = session.TickIndex;
            var ids = new string[4];
            for (int slot = 0; slot < 4; slot++) ids[slot] = game.Run.Workshop.Loadout[slot].Id;
            Find("Open preparation album").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(Find("Start battle").IsInteractable(), Is.False);
            Find("Close blueprint picker").onClick.Invoke(); yield return null;
            Assert.That(Find("Start battle").IsInteractable(), Is.True);
            Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Drawing.Tier, Is.EqualTo(MaterialTier.Marker));
            for (int slot = 0; slot < 4; slot++) Assert.That(game.Run.Workshop.Loadout[slot].Id, Is.EqualTo(ids[slot]));
        }

        [UnityTest] public IEnumerator PreparationBlocksStartingWithAnEmptyLoadout()
        {
            var catalog = LegacyContentCatalog.CreateInput(balance.CreateWorkshopBalance());
            catalog.StartingPages.Clear();
            // Runtime snapshots require a known template even when the fixture contains no pages.
            const string template = "template.journey.empty";
            catalog.Templates.Add(new DrawingTemplateDefinitionInput { Id = template, DisplayName = "Пустой тестовый набор" });
            foreach (var part in catalog.Chassis) part.TemplateId = template;
            foreach (var part in catalog.Weapons) part.TemplateId = template;
            foreach (var part in catalog.Modules) part.TemplateId = template;
            game.Run.InitializeWorkshop(new JourneyProfileStore(new WorkshopProfile(catalog.CreateSnapshot())));
            game.Navigate(PlayerPage.Preparation); yield return null;
            var session = game.Run.Session;
            var start = Find("Start battle");
            Assert.That(start.IsInteractable(), Is.False, "Choose all four blueprints before starting a mission.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(start.gameObject,
                new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.submitHandler);
            Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(Find("Open preparation album").IsInteractable(), Is.True);
            Assert.That(Find("Edit album").IsInteractable(), Is.True);
        }

        [UnityTest] public IEnumerator PreparationAlbumSlotTabsKeepChoicesUniqueAndCancelWithoutChangingTheLoadout()
        {
            game.Navigate(PlayerPage.Preparation); yield return null;
            var session = game.Run.Session;
            var ids = new string[4];
            for (int slot = 0; slot < 4; slot++) ids[slot] = game.Run.Workshop.Loadout[slot].Id;
            Find("Open preparation album").onClick.Invoke(); yield return null;
            for (int destination = 0; destination < 4; destination++)
            {
                Find("Album slot " + destination).onClick.Invoke(); yield return null;
                Assert.That(Find("Start battle").IsInteractable(), Is.False);
                for (int slot = 0; slot < 4; slot++)
                {
                    Assert.That(game.Run.Workshop.Loadout[slot].Id, Is.EqualTo(ids[slot]));
                    Assert.That(Find("Pick blueprint " + ids[slot]).IsInteractable(), Is.EqualTo(slot == destination),
                        "Only the current slot may keep its assigned page; the other three pages are already taken.");
                }
            }
            Find("Close blueprint picker").onClick.Invoke(); yield return null;
            Assert.That(Find("Start battle").IsInteractable(), Is.True);
            Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(game.Run.InWorkshop, Is.True);
            for (int slot = 0; slot < 4; slot++) Assert.That(game.Run.Workshop.Loadout[slot].Id, Is.EqualTo(ids[slot]));
        }

        RectTransform ActiveRect(string name)
        {
            var rect = System.Array.Find(root.GetComponentsInChildren<RectTransform>(), x => x.name == name);
            Assert.That(rect, Is.Not.Null, "Missing active layout: " + name); return rect;
        }

        Text ActiveText(string name)
        {
            var text = System.Array.Find(root.GetComponentsInChildren<Text>(), x => x.name == name);
            Assert.That(text, Is.Not.Null, "Missing live label: " + name); return text;
        }

        static void AssertWithin(RectTransform boundary, RectTransform target, string context)
        {
            var bounds = BoundsInPage(boundary, target);
            string message = target.name + " at " + context;
            Assert.That(bounds.width, Is.GreaterThan(0), message);
            Assert.That(bounds.height, Is.GreaterThan(0), message);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(boundary.rect.xMin - 1), message);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(boundary.rect.xMax + 1), message);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(boundary.rect.yMin - 1), message);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(boundary.rect.yMax + 1), message);
        }

        static void AssertReadable(Text text, float physicalScale, string context)
        {
            Assert.That(text, Is.Not.Null, "Missing visible caption at " + context);
            Assert.That(text.isActiveAndEnabled, Is.True, text.name + " must render at " + context);
            Assert.That(text.canvasRenderer.cull, Is.False, text.name + " at " + context);
            Assert.That(text.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(text.text.Length),
                "Caption must fit completely: " + text.text + " at " + context);
            // Unity reports best-fit size in generated screen pixels, including pixelsPerUnit.
            // Convert back to logical units before applying the resolution under test.
            float size = text.resizeTextForBestFit
                ? text.cachedTextGenerator.fontSizeUsedForBestFit / text.pixelsPerUnit
                : text.fontSize;
            Assert.That(size * physicalScale, Is.GreaterThanOrEqualTo(14),
                "Caption must remain readable: " + text.text + " at " + context);
        }

        static void AssertSameBounds(Rect expected, Rect actual, string context)
        {
            Assert.That(Vector2.Distance(expected.position, actual.position), Is.LessThanOrEqualTo(1), context + " moved after changing material.");
            Assert.That(Vector2.Distance(expected.size, actual.size), Is.LessThanOrEqualTo(1), context + " resized after changing material.");
        }

        sealed class JourneyProfileStore : IWorkshopProfileStore
        {
            WorkshopProfile profile;
            public JourneyProfileStore(WorkshopProfile profile) { this.profile = profile; }
            public ProfileLoadResult Load() => new ProfileLoadResult(profile, ProfileLoadSource.Primary, "Isolated player journey profile", true);
            public ProfileSaveResult Save(WorkshopProfile candidate) { profile = candidate; return new ProfileSaveResult(true, ""); }
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

        [UnityTest] public IEnumerator SettingsOfferResolutionSelectionWithoutApplyingItOnNavigation()
        {
            int width = Screen.width, height = Screen.height;
            game.OpenSettings(); yield return null;
            var next = Find("Resolution next");
            var apply = Find("Apply resolution");
            var selected = ActiveText("Selected resolution");
            string initial = selected.text;
            string current = ActiveText("Current resolution").text;
            Assert.That(apply.interactable, Is.False, "The current display size has no pending change.");
            next.onClick.Invoke(); yield return null;
            Assert.That(ActiveText("Selected resolution").text, Is.Not.EqualTo(initial));
            Assert.That(ActiveText("Current resolution").text, Is.EqualTo(current), "Browsing keeps the applied size visible.");
            Assert.That(Find("Apply resolution").interactable, Is.True);
            Assert.That(Screen.width, Is.EqualTo(width), "Browsing resolutions must leave the real display unchanged.");
            Assert.That(Screen.height, Is.EqualTo(height));
            Find("Close settings").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            game.OpenSettings(); yield return null;
            Assert.That(ActiveText("Selected resolution").text, Is.EqualTo(initial), "Closing settings discards an unapplied choice.");
            Assert.That(Find("Apply resolution").interactable, Is.False);
        }

        [UnityTest] public IEnumerator SettingsKeepDisplayChoicesAndNavigationVisibleWithoutScrolling()
        {
            game.OpenSettings();
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                shell.ApplyLayout(sizes[size, 0], sizes[size, 1]); Canvas.ForceUpdateCanvases();
                var page = ActiveRect("Page Settings");
                string context = sizes[size, 0] + "x" + sizes[size, 1];
                Assert.That(page.GetComponentsInChildren<ScrollRect>(), Is.Empty,
                    "Display choices and closing the screen must not require scrolling at " + context);
                var occupied = new System.Collections.Generic.List<Rect>();
                foreach (string name in new[] { "Toggle fullscreen", "Resolution previous", "Resolution next",
                    "Apply resolution", "Toggle ranges", "Reduce motion", "Close settings", "Open help" })
                {
                    var control = Find(name);
                    AssertWithin(page, (RectTransform)control.transform, context);
                    Rect bounds = BoundsInPage(page, (RectTransform)control.transform);
                    foreach (var previous in occupied)
                        Assert.That(bounds.Overlaps(previous), Is.False, name + " must remain separately clickable at " + context);
                    occupied.Add(bounds);
                }
                AssertReadable(ActiveText("Selected resolution"), shell.Layout.Scale, context);
                AssertReadable(ActiveText("Current resolution"), shell.Layout.Scale, context);
                AssertCompleteIllustration(page, "Notebook landscape", context);
                AssertCompleteIllustration(page, "Settings wordmark", context);
                yield return null;
            }
        }

        [UnityTest] public IEnumerator SettingsCaptionsFitAtNativePixelDensityAcrossSupportedWindows()
        {
            game.OpenSettings();
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                shell.ApplyLayout(sizes[size, 0], sizes[size, 1]); Canvas.ForceUpdateCanvases();
                string context = sizes[size, 0] + "x" + sizes[size, 1];
                foreach (var label in ActiveRect("Page Settings").GetComponentsInChildren<Text>())
                {
                    if (!label.isActiveAndEnabled || string.IsNullOrEmpty(label.text)) continue;
                    var pixels = label.rectTransform.rect.size * shell.Layout.Scale;
                    var settings = label.GetGenerationSettings(pixels);
                    // The real Game view may be larger than this requested layout. Generate at
                    // the target pixel density so a 1280 view cannot hide clipping at 1024.
                    settings.scaleFactor = 1;
                    settings.fontSize = Mathf.RoundToInt(label.fontSize * shell.Layout.Scale);
                    settings.resizeTextMinSize = Mathf.CeilToInt(label.resizeTextMinSize * shell.Layout.Scale);
                    settings.resizeTextMaxSize = Mathf.Max(settings.resizeTextMinSize,
                        Mathf.RoundToInt(label.resizeTextMaxSize * shell.Layout.Scale));
                    settings.generateOutOfBounds = false;
                    using (var generated = new TextGenerator())
                    {
                        generated.Populate(label.text, settings);
                        Assert.That(generated.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length),
                            "Whole caption must fit at native pixels: " + label.text + " at " + context);
                        if (label.name == "Current resolution" || label.name == "Selected resolution" ||
                            label.transform.parent.name == "Apply resolution" || label.transform.parent.name == "Close settings")
                            Assert.That(generated.fontSizeUsedForBestFit, Is.GreaterThanOrEqualTo(14),
                                "Essential display controls must remain readable at " + context);
                    }
                }
                yield return null;
            }
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

        [UnityTest] public IEnumerator ResizingWorkshopPreservesTheStandardAssemblyDto()
        {
            game.Navigate(PlayerPage.Workshop); yield return null;
            var wizard = root.GetComponentInChildren<WorkshopHud>().Guided;
            Assert.That(wizard.Next(), Is.True);
            Assert.That(wizard.SelectWeapon(1, "weapon.gun"), Is.True);
            Assert.That(wizard.Next(), Is.True);
            string draft = JsonUtility.ToJson(new BlueprintData(wizard.Draft));
            string ProfileState()
            {
                var state = new System.Text.StringBuilder().Append(game.Run.Workshop.Gold);
                foreach (var page in game.Run.Workshop.Album) state.Append('|').Append(JsonUtility.ToJson(new BlueprintData(page)));
                state.Append(';');
                foreach (var page in game.Run.Workshop.Loadout) state.Append('|').Append(page?.Id);
                state.Append(';');
                foreach (var part in game.Run.Workshop.Parts) state.Append('|').Append(part.Id).Append(':').Append(game.Run.Workshop.IsUnlocked(part.Id));
                return state.ToString();
            }
            string profile = ProfileState();
            var responsive = root.GetComponentInChildren<WorkshopResponsiveLayout>();
            responsive.enabled = false;
            foreach (var size in new[] { new Vector2Int(1024, 768), new Vector2Int(1920, 1080), new Vector2Int(1024, 768) })
            {
                responsive.Apply(size.x, size.y); yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(wizard.Step, Is.EqualTo(2)); Assert.That(wizard.Drawing, Is.Null);
                Assert.That(wizard.Draft.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
                Assert.That(wizard.Draft.Drawing.IsEmpty, Is.True); Assert.That(wizard.Draft.FreeformDesign, Is.Null);
                Assert.That(JsonUtility.ToJson(new BlueprintData(wizard.Draft)), Is.EqualTo(draft));
                Assert.That(ProfileState(), Is.EqualTo(profile));
                AssertVisibleInCanvas((RectTransform)Find("Wizard next").transform);
                AssertVisibleInCanvas((RectTransform)Find("Wizard back").transform);
            }
        }

        [UnityTest] public IEnumerator ResizingBattleRetiresTooltipCallbacksWithoutReactivatingTheOldHud()
        {
            game.Run.AutomaticSimulation = false;
            Assert.That(game.StartBattle(), Is.True); yield return null;
            var session = game.Run.Session; long tick = session.TickIndex;
            var shell = root.GetComponentInChildren<PlayerShell>(true);
            Assert.That(shell, Is.Not.Null);
            shell.ApplyLayout(1024, 768); yield return null;
            shell.ApplyLayout(1600, 900); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.CanBuild, Is.True);
            LogAssert.NoUnexpectedReceived();
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

        [UnityTest] public IEnumerator AuthoredPreparationDrawingFitsItsCardAndStaysClearOfCompactNavigation()
        {
            var authored = game.Run.Workshop.Loadout[0].AsVariant("compact-drawing", "Мой рисунок");
            var drawing = new DrawingEditSession(authored);
            Assert.That(drawing.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f, -.35f),
                new DrawingPoint(.35f, .35f), out var reason), Is.True, reason);
            Assert.That(drawing.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.0625f, 0),
                new DrawingPoint(.0625f, .5f), out reason), Is.True, reason);
            authored = authored.WithDrawing(drawing.Snapshot);
            Assert.That(game.Run.SaveBlueprint(authored, out reason), Is.True, reason);
            Assert.That(game.Run.AssignBlueprint(0, authored.Id, out reason), Is.True, reason); yield return null; yield return null;
            game.Navigate(PlayerPage.Preparation); yield return null;
            root.GetComponentInChildren<PlayerShell>().ApplyLayout(1024, 768); Canvas.ForceUpdateCanvases();
            var page = ActiveRect("Page Preparation"); var card = ActiveRect("Loadout 0");
            Assert.That(page.GetComponentsInChildren<ScrollRect>(), Is.Empty);
            var sketch = card.GetComponentInChildren<BlueprintSketch>();
            Assert.That(sketch, Is.Not.Null, "A player's actual drawing must stay visible in preparation.");
            var sketchRect = (RectTransform)sketch.transform;
            AssertWithin(card, sketchRect, "1024x768 window");
            AssertWithin(page, card, "1024x768 window");
            Assert.That(sketch.canvasRenderer.cull, Is.False, "The authored illustration must be visible without scrolling.");
            var pictureBounds = BoundsInPage(page, sketchRect);
            foreach (string action in new[] { "Edit album", "Open preparation album", "Start battle" })
                Assert.That(pictureBounds.Overlaps(BoundsInPage(page, (RectTransform)Find(action).transform)), Is.False,
                    "The authored drawing must stay clear of " + action);
            Assert.That(game.Run.Workshop.Loadout[0].Drawing.GeometryHash, Is.EqualTo(authored.Drawing.GeometryHash));
        }

        [UnityTest] public IEnumerator MaximumLengthStoredBlueprintNameRemainsReadableInCompactPreparation()
        {
            const string phrase = "Моя пробивающая башня 01";
            string name = phrase + phrase;
            Assert.That(name.Length, Is.EqualTo(ProfileLimits.MaximumBlueprintNameLength));
            var authored = game.Run.Workshop.Loadout[2].AsVariant("maximum-name-drawing", name);
            var drawing = new DrawingEditSession(authored);
            Assert.That(drawing.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f, -.35f),
                new DrawingPoint(.35f, .35f), out var reason), Is.True, reason);
            Assert.That(drawing.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.0625f, 0),
                new DrawingPoint(.0625f, .5f), out reason), Is.True, reason);
            authored = authored.WithDrawing(drawing.Snapshot);
            Assert.That(game.Run.SaveBlueprint(authored, out reason), Is.True, reason);
            Assert.That(game.Run.AssignBlueprint(0, authored.Id, out reason), Is.True, reason);
            game.Navigate(PlayerPage.Preparation); yield return null;
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                shell.ApplyLayout(sizes[size, 0], sizes[size, 1]);
                Canvas.ForceUpdateCanvases();
                var caption = ActiveText("Loadout name 0");
                string context = sizes[size, 0] + "x" + sizes[size, 1];
                Assert.That(caption.text, Is.EqualTo(game.Run.Workshop.Loadout[0].Name));
                Assert.That(caption.text.Length, Is.EqualTo(48));
                AssertWithin(ActiveRect("Loadout 0"), caption.rectTransform, context);
                AssertReadable(caption, shell.Layout.Scale, context);
                yield return null;
            }
        }
    }
}
