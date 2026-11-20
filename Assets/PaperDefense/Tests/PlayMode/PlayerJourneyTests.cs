#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class PlayerJourneyTests
    {
        GameObject root;
        PlayerGameRoot game;
        GameBalanceAsset balance;
        BalancePresetAsset preset;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            root = new GameObject("Player journey fixture"); root.SetActive(false);
            var camera = new GameObject("Fixture camera", typeof(Camera)); camera.tag = "MainCamera"; camera.transform.SetParent(root.transform);
            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(source, Is.Not.Null);
            balance = Object.Instantiate(source); preset = Object.Instantiate(source.dataPreset);
            balance.dataPreset = preset; preset.rules.PlanningSeconds = 1000;
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true); yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); Object.Destroy(balance); Object.Destroy(preset); yield return null; }

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

        [UnityTest] public IEnumerator BattleHintUsesExactDockCaptionsAndHidesAfterPointerOrKeyboardLeaves()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var inputModule = events.currentInputModule;
            bool inputWasEnabled = inputModule != null && inputModule.enabled;
            if (inputModule != null) inputModule.enabled = false;
            try
            {
                Assert.That(game.StartBattle(), Is.True); yield return null;
                var label = System.Array.Find(root.GetComponentsInChildren<Text>(true), x => x.name == "Battle tool hint");
                Assert.That(label, Is.Not.Null, "The transient hint remains owned by the battle even while hidden.");
                Assert.That(label.gameObject.activeInHierarchy, Is.False);
                Assert.That(label.text, Is.Empty);
                var card = Find("Build tool 0");
                events.SetSelectedGameObject(card.gameObject); yield return null;
                Assert.That(label.gameObject.activeInHierarchy, Is.True);
                Assert.That(label.text, Is.EqualTo(card.GetComponent<PlayerTooltip>().Message));
                Assert.That(label.text, Does.Contain(GameText.Resolve(game.Run.RunLoadout.Entries[0].Name)));
                Assert.That(label.text, Does.Contain(game.Run.RunLoadout.ResolveSlot(0, MaterialTier.Pencil).Cost + " штр."));
                events.SetSelectedGameObject(null); yield return null;
                Assert.That(label.gameObject.activeInHierarchy, Is.False);
                Assert.That(label.text, Is.Empty, "Deselecting must clear the old caption as well as hide it.");
                var button = Find("Album battle erase");
                var pointer = new UnityEngine.EventSystems.PointerEventData(events);
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
                yield return null;
                Assert.That(label.gameObject.activeInHierarchy, Is.True);
                Assert.That(label.text, Is.EqualTo(button.GetComponent<PlayerTooltip>().Message));
                Assert.That(label.text, Is.EqualTo("Ластик · E"));
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
                yield return null;
                Assert.That(label.gameObject.activeInHierarchy, Is.False);
                Assert.That(label.text, Is.Empty, "Pointer exit must not leave a stale action or persistent blueprint caption.");
                events.SetSelectedGameObject(button.gameObject); yield return null;
                Assert.That(label.gameObject.activeInHierarchy, Is.True);
                Assert.That(label.text, Is.EqualTo("Ластик · E"));
                events.SetSelectedGameObject(card.gameObject); yield return null;
                Assert.That(label.text, Is.EqualTo(card.GetComponent<PlayerTooltip>().Message));
                Assert.That(label.text, Does.Contain(GameText.Resolve(game.Run.RunLoadout.Entries[0].Name)));
                Assert.That(game.Drawing.BlueprintSlot, Is.Zero, "Reading another action's hint must not issue its command.");
                Assert.That(game.Drawing.Mode, Is.EqualTo(DrawingMode.Build));
                events.SetSelectedGameObject(null); yield return null;
                Assert.That(label.gameObject.activeInHierarchy, Is.False);
                Assert.That(label.text, Is.Empty);
            }
            finally
            {
                if (inputModule != null) inputModule.enabled = inputWasEnabled;
            }
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
            Assert.That(Find("Open campaign").GetComponentInChildren<Text>().text, Is.EqualTo("Открыть кампанию"));
        }

        [UnityTest] public IEnumerator HomeActionsRemainVisibleAndSeparateAcrossWindowSizes()
        {
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            string[] actions = { "Open campaign", "Open album", "workshop", "settings", "help", "exit" };
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
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Campaign));
            Find("Open mission").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Find("Prepare mission").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(System.Array.Exists(root.GetComponentsInChildren<Button>(), b => b.name.StartsWith("Starting material ")), Is.False);
            Find("Start battle").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.InWorkshop, Is.False);
            Assert.That(game.Run.RunLoadout.Entries.Count, Is.EqualTo(ProfileLimits.LoadoutSlotCount));
            Assert.That(game.Drawing.Tier, Is.EqualTo(MaterialTier.Pencil));
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
                var briefingScrolls = page.GetComponentsInChildren<ScrollRect>();
                Assert.That(briefingScrolls.Length, Is.EqualTo(1), "Only the mission briefing may scroll at " + context);
                Assert.That(Find("Prepare mission").transform.IsChildOf(briefingScrolls[0].content), Is.False,
                    "The launch route must remain outside the scrolling briefing at " + context);
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

        [UnityTest] public IEnumerator NeutralPageBackgroundsAndLiveMissionMapStayVisibleAcrossWindowSizes()
        {
            var shell = root.GetComponentInChildren<PlayerShell>();
            int[,] sizes = { { 1024, 768 }, { 1280, 720 }, { 1280, 960 }, { 1600, 900 }, { 1920, 1080 } };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                shell.ApplyLayout(sizes[size, 0], sizes[size, 1]);
                string context = sizes[size, 0] + "x" + sizes[size, 1];
                game.Navigate(PlayerPage.Home); Canvas.ForceUpdateCanvases();
                AssertNeutralGraphic(ActiveRect("Page Home"), "Home illustration", context);
                game.Navigate(PlayerPage.Map); Canvas.ForceUpdateCanvases();
                var map = AssertNeutralGraphic(ActiveRect("Mission map"), "Mission illustration", context);
                Assert.That(map, Is.InstanceOf<NeutralMissionMapGraphic>(), "The mission preview must show live battlefield geometry.");
                game.Navigate(PlayerPage.Preparation); Canvas.ForceUpdateCanvases();
                AssertNeutralGraphic(ActiveRect("Page Preparation"), "Neutral page background", context);
                yield return null;
            }
        }

        Graphic AssertNeutralGraphic(RectTransform boundary, string name, string context)
        {
            var graphic = System.Array.Find(boundary.GetComponentsInChildren<Graphic>(), x => x.name == name);
            Assert.That(graphic, Is.Not.Null, "Missing neutral page graphic at " + context);
            Assert.That(graphic.isActiveAndEnabled, Is.True, name + " must render at " + context);
            Assert.That(graphic.canvasRenderer.cull, Is.False, name + " must stay visible at " + context);
            Assert.That(graphic.raycastTarget, Is.False, name + " must leave the page controls clickable.");
            Assert.That(graphic.mainTexture, Is.SameAs(Texture2D.whiteTexture), name + " must be independent of the retired atlas.");
            if (graphic is Image image) Assert.That(image.sprite, Is.Null, name);
            AssertWithin(boundary, graphic.rectTransform, context);
            var mesh = graphic.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, name);
            {
                Assert.That(mesh.vertexCount, Is.GreaterThan(3), name + " must produce visible geometry at " + context);
                Assert.That(System.Array.Exists(mesh.colors32, color => color.a > 0), Is.True, name);
                foreach (var vertex in mesh.vertices)
                {
                    Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || float.IsNaN(vertex.y)
                        || float.IsInfinity(vertex.y), Is.False, name + " must have finite geometry.");
                    Assert.That(vertex.x, Is.InRange(graphic.rectTransform.rect.xMin - 1, graphic.rectTransform.rect.xMax + 1), name);
                    Assert.That(vertex.y, Is.InRange(graphic.rectTransform.rect.yMin - 1, graphic.rectTransform.rect.yMax + 1), name);
                }
            }
            for (Transform parent = graphic.transform.parent; parent != null; parent = parent.parent)
            {
                Assert.That(parent.GetComponent<RectMask2D>(), Is.Null,
                    name + " must not lose its edges to a viewport mask at " + context);
                if (parent == boundary) break;
            }
            return graphic;
        }

        [UnityTest] public IEnumerator MissionSummaryUsesTheSelectedPageAndRetainsConfiguredCastleRules()
        {
            preset.rules.StartingInk = 913; preset.rules.BaseHealth = 337;
            preset.rules.Waves = preset.rules.Waves.GetRange(0, 3);
            game.Run.OpenWorkshop(); game.Navigate(PlayerPage.Map); yield return null;
            Assert.That(ActiveText("Mission waves").text, Does.Contain(game.Run.Session.Balance.Waves.Count.ToString()));
            Assert.That(game.Run.Session.Balance.StartingInk, Is.Not.EqualTo(913), "The selected page owns its opening budget.");
            Assert.That(game.Run.Session.Balance.Waves.Count, Is.EqualTo(game.Run.SelectedMission.WaveCount));
            Assert.That(ActiveText("Mission starting ink").text, Does.Contain(game.Run.Session.Balance.StartingInk.ToString()));
            Assert.That(ActiveText("Mission castle health").text, Does.Contain("337"));
            var session = game.Run.Session;
            Find("Prepare mission").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Drawing.enabled, Is.False);
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
            string blueprint = game.Run.Workshop.LoadoutEntries[0].Id;
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
            Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(blueprint));
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold));
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
                AssertNeutralGraphic(page, "Neutral page background", context);
                var title = System.Array.Find(page.GetComponentsInChildren<Text>(), label => label.text == "ДЕРЖИ ЛИНИЮ");
                Assert.That(title, Is.Not.Null, "Settings keep their title as live text at " + context);
                Assert.That(title.canvasRenderer.cull, Is.False, context);
                AssertWithin(page, title.rectTransform, context);
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

    }
}
#endif
