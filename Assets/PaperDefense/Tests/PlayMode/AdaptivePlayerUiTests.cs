#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AdaptivePlayerUiTests
    {
        GameObject root;
        PlayerGameRoot game;
        PlayerShell shell;
        GameBalanceAsset balance;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;

            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(source, Is.Not.Null, "Use the production assembly catalog with a memory-only profile.");
            balance = Object.Instantiate(source);
            balance.planningSeconds = 1000;
            root = new GameObject("Adaptive player UI fixture"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true);
            game.Run.AutomaticSimulation = false;
            shell = root.GetComponentInChildren<PlayerShell>();
            // Keep explicit test sizes stable instead of following the editor's Game view on Update.
            shell.enabled = false;
            yield return null;
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            Object.Destroy(root); Object.Destroy(balance);
            yield return null;
        }

        Button Button(string name)
        {
            var button = root.GetComponentsInChildren<Button>().SingleOrDefault(control => control.name == name);
            Assert.That(button, Is.Not.Null, "Missing active control: " + name);
            return button;
        }

        RectTransform Rect(string name)
        {
            var rect = root.GetComponentsInChildren<RectTransform>().SingleOrDefault(control => control.name == name);
            Assert.That(rect, Is.Not.Null, "Missing active region: " + name);
            return rect;
        }

        void Click(string name)
        {
            var button = Button(name);
            Assert.That(button.IsInteractable(), Is.True, name + " must accept a click.");
            button.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
        }

        [Test] public void BackReturnsFromCampaignToHomeWithoutAdvancingTheIdleSession()
        {
            var session = game.Run.Session; long tick = session.TickIndex;
            Click("Open campaign");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Campaign));

            game.Back();

            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Assert.That(Button("Open campaign").IsInteractable(), Is.True);
            AssertIdleSession(session, tick);
        }

        [TestCase(800, 600)] [TestCase(1280, 720)] [TestCase(1920, 1080)]
        public void HomeArrowNavigationFollowsTheVisibleRows(int width, int height)
        {
            shell.ApplyLayout(width, height); Canvas.ForceUpdateCanvases();
            Assert.That(Button("help").GetComponentInChildren<PlayerIconGraphic>().Icon, Is.EqualTo(PlayerIcon.Help));
            Assert.That(Button("Open album").GetComponentInChildren<PlayerIconGraphic>().Icon, Is.EqualTo(PlayerIcon.Book));
            AssertHomeNeighbors("Open campaign", null, "Open album", null, null);
            AssertHomeNeighbors("Open album", "Open campaign", "settings", null, "workshop");
            AssertHomeNeighbors("workshop", "Open campaign", "help", "Open album", null);
            AssertHomeNeighbors("settings", "Open album", "exit", null, "help");
            AssertHomeNeighbors("help", "workshop", "exit", "settings", null);
            AssertHomeNeighbors("exit", "settings", null, null, null);
        }

        void AssertHomeNeighbors(string name, string up, string down, string left, string right)
        {
            var button = Button(name);
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit), name);
            string[] expected = { up, down, left, right };
            Selectable[] neighbors = { button.FindSelectableOnUp(), button.FindSelectableOnDown(),
                button.FindSelectableOnLeft(), button.FindSelectableOnRight() };
            MoveDirection[] directions = { MoveDirection.Up, MoveDirection.Down, MoveDirection.Left, MoveDirection.Right };
            for (int i = 0; i < directions.Length; i++)
            {
                Assert.That(neighbors[i]?.name, Is.EqualTo(expected[i]), name + " / " + directions[i]);
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                button.OnMove(new AxisEventData(EventSystem.current) { moveDir = directions[i] });
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(expected[i] ?? name),
                    "Keyboard movement must use the same visible neighbor: " + name + " / " + directions[i]);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void PreparationSummaryPrioritizesStartingInkAndTheBlockedStartReason(bool empty)
        {
            game.Navigate(PlayerPage.Preparation);
            if (empty)
                for (int slot = 0; slot < ProfileLimits.LoadoutSlotCount; slot++) game.AssignAlbumEntry(slot, null);
            shell.ApplyLayout(800, 600); Canvas.ForceUpdateCanvases();
            var summary = Rect("Preparation summary");
            Assert.That(summary.GetComponentsInChildren<Text>().Any(text => text.name == "Preparation loadout count"), Is.False,
                "The loadout count already appears above the cards.");
            var ink = summary.GetComponentsInChildren<Text>().Single(text => text.name == "Preparation starting ink");
            Assert.That(ink.text, Does.Contain(game.Run.Session.Balance.StartingInk.ToString()));
            bool ready = game.Run.CanStartAlbumRun(out var reason);
            Assert.That(ready, Is.EqualTo(!empty));
            Assert.That(Button("Start battle").interactable, Is.EqualTo(ready));
            var ribbon = Rect("Assembly preparation ribbon");
            var explanation = ribbon.GetComponentsInChildren<Text>().Single(text => text.name == "Preparation readiness");
            Assert.That(explanation.text, Is.EqualTo(ready ? "Готово к защите" : GameText.Resolve(reason)));
            AssertWithin(ribbon, explanation.rectTransform);
            Assert.That(explanation.GetComponentInParent<ScrollRect>(), Is.Null,
                "Readiness stays visible outside the loadout viewport.");
        }

        [Test] public void EnteringWorkshopSelectsAnAvailableControlForKeyboardNavigation()
        {
            Click("workshop");
            var workshop = root.GetComponentInChildren<WorkshopHud>(true).Composite.Root;
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.transform.IsChildOf(workshop), Is.True);
            Assert.That(selected.GetComponent<Selectable>().IsInteractable(), Is.True);
        }

        [Test] public void BackReturnsFromPreparationToTheMissionMap()
        {
            Click("Open campaign"); Click("Open mission"); Click("Prepare mission");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            var session = game.Run.Session; long tick = session.TickIndex;
            var loadout = game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray();

            game.Back();

            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Assert.That(Button("Prepare mission").IsInteractable(), Is.True);
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id), Is.EqualTo(loadout));
            AssertIdleSession(session, tick);
        }

        [Test] public void BackFromAnAlbumOpenedInPreparationRestoresPreparation()
        {
            game.Navigate(PlayerPage.Preparation);
            var session = game.Run.Session; long tick = session.TickIndex;
            var loadout = game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray();
            Click("Open preparation album");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));

            game.Back();

            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(Button("Start battle").IsInteractable(), Is.True);
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id), Is.EqualTo(loadout));
            AssertIdleSession(session, tick);
        }

        [Test] public void ClickHelpThenBackClosesTheModalBeforeReturningFromPreparation()
        {
            game.Navigate(PlayerPage.Preparation);
            var session = game.Run.Session; long tick = session.TickIndex;
            var start = Button("Start battle");
            Click("Screen help");

            var help = Rect("Context help");
            Assert.That(help.GetComponent<WorkshopModal>(), Is.Not.Null);
            Assert.That(Button("Close context help").transform.IsChildOf(help), Is.True);
            Assert.That(start.IsInteractable(), Is.False, "The help sheet must cover the preparation actions.");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));

            game.Back();

            Assert.That(root.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Context help"), Is.False);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation), "The first Back closes help without navigating.");
            Assert.That(Button("Start battle").IsInteractable(), Is.True);
            AssertIdleSession(session, tick);
            game.Back();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map), "A second Back uses the page's return destination.");
        }

        [Test] public void CompactPreparationKeepsAllSlotsVisibleAndNavigationOutsideTheViewport()
        {
            game.Navigate(PlayerPage.Preparation);
            shell.ApplyLayout(800, 600); Canvas.ForceUpdateCanvases();
            var page = Rect("Page Preparation");
            Assert.That(Button("Screen help").GetComponentInChildren<PlayerIconGraphic>().Icon, Is.EqualTo(PlayerIcon.Help));
            Assert.That(Button("Screen help").GetComponent<PlayerTooltip>().Message, Is.Not.Empty);
            var scroll = root.GetComponentsInChildren<ScrollRect>()
                .SingleOrDefault(control => control.name == "Preparation loadout scroll");
            Assert.That(scroll, Is.Not.Null, "The loadout retains its bounded viewport at 800x600.");
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            AssertWithin(page, scroll.viewport);
            Assert.That(scroll.viewport.rect.height, Is.GreaterThan(0));
            Assert.That(scroll.content.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height + 1));

            var grid = Rect("Assembly preparation loadout");
            var flow = grid.GetComponent<GridLayoutGroup>();
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(flow.constraintCount, Is.EqualTo(4));
            Assert.That(grid.IsChildOf(scroll.content) || grid == scroll.content, Is.True);
            var cards = Enumerable.Range(0, ProfileLimits.LoadoutSlotCount).Select(slot => Rect("Album loadout " + slot)).ToArray();
            for (int slot = 0; slot < cards.Length; slot++)
            {
                AssertWithin(grid, cards[slot]);
                AssertWithin(scroll.viewport, cards[slot]);
                var change = Button("Replace album entry " + slot);
                Assert.That(change.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(32), "Slot " + slot);
                AssertWithin(cards[slot], change.GetComponent<RectTransform>());
            }

            var viewportBounds = Bounds(page, scroll.viewport);
            foreach (string name in new[] { "Preparation navigation", "Preparation material", "Preparation summary" })
            {
                var region = Rect(name); AssertWithin(page, region);
                Assert.That(viewportBounds.Overlaps(Bounds(page, region)), Is.False, name + " overlaps the loadout viewport.");
            }
            foreach (string name in new[] { "Edit album", "Open preparation album", "Start battle" })
            {
                var action = Button(name).GetComponent<RectTransform>(); AssertWithin(page, action);
                Assert.That(viewportBounds.Overlaps(Bounds(page, action)), Is.False, name + " overlaps the loadout viewport.");
            }

            scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            AssertWithin(scroll.viewport, Button("Replace album entry 7").GetComponent<RectTransform>());
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(game.Run.InWorkshop, Is.True);
        }

        [TestCase(800, 600)] [TestCase(1280, 720)] [TestCase(1920, 1080)]
        public void HomePlayRetainsAVisibleMinimumFortyFourPixelTarget(int width, int height)
        {
            shell.ApplyLayout(width, height); Canvas.ForceUpdateCanvases();
            var play = Button("Open campaign");
            Assert.That(play.IsInteractable(), Is.True);
            Assert.That(play.GetComponent<RectTransform>().rect.height * shell.Layout.Scale, Is.GreaterThanOrEqualTo(44));
            AssertWithin(Rect("Page Home"), play.GetComponent<RectTransform>());
        }

        [Test] public void CompactSettingsKeepsReadableActionsInABoundedScrollAndDoneOutsideIt()
        {
            game.OpenSettings(); shell.ApplyLayout(800, 600); Canvas.ForceUpdateCanvases();
            var page = Rect("Page Settings");
            var scroll = page.GetComponentsInChildren<ScrollRect>().Single(control => control.name == "Settings responsive scroll");
            AssertWithin(page, scroll.viewport);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            var done = Button("Close settings").GetComponent<RectTransform>();
            AssertWithin(page, done);
            Assert.That(done.IsChildOf(scroll.content), Is.False);
            Assert.That(Bounds(page, done).Overlaps(Bounds(page, scroll.viewport)), Is.False);
            foreach (string name in new[] { "Toggle fullscreen", "Resolution previous", "Resolution next", "Apply resolution", "Toggle ranges", "Reduce motion", "Open help", "Close settings" })
                Assert.That(Button(name).GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(44), name);

            scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            AssertWithin(scroll.viewport, Button("Open help").GetComponent<RectTransform>());
            bool ranges = game.World.ShowRanges;
            Click("Toggle ranges help");
            Assert.That(Rect("Context help"), Is.Not.Null);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
            Assert.That(game.World.ShowRanges, Is.EqualTo(ranges), "Opening the explanation must not change the setting.");
            game.Back();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
            Assert.That(Button("Close settings").IsInteractable(), Is.True);
        }

        [TestCase(800, 600)] [TestCase(1280, 720)]
        public void HelpUsesReadableChunksWithAnAlwaysVisibleReturnAction(int width, int height)
        {
            game.OpenSettings(); Click("Open help");
            shell.ApplyLayout(width, height); Canvas.ForceUpdateCanvases();
            var page = Rect("Page Help");
            var scroll = page.GetComponentsInChildren<ScrollRect>().Single(control => control.name == "Help responsive scroll");
            AssertWithin(page, scroll.viewport);
            var close = Button("Close help").GetComponent<RectTransform>();
            AssertWithin(page, close);
            Assert.That(close.IsChildOf(scroll.content), Is.False);
            Assert.That(close.rect.height, Is.GreaterThanOrEqualTo(44));
            Assert.That(Bounds(page, close).Overlaps(Bounds(page, scroll.viewport)), Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
            foreach (var body in scroll.content.GetComponentsInChildren<Text>().Where(label => label.name.EndsWith(" text")))
            {
                Assert.That(body.resizeTextForBestFit, Is.False, body.name);
                Assert.That(body.fontSize * shell.Layout.Scale, Is.GreaterThanOrEqualTo(18), body.name);
                Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + 1), body.name);
            }
            scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            var controls = Rect("Help controls");
            var controlsBounds = Bounds(scroll.viewport, controls);
            Assert.That(controlsBounds.yMin, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1));
            Assert.That(controlsBounds.yMin, Is.LessThan(scroll.viewport.rect.yMax));
            Click("Close help");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
        }

        void AssertIdleSession(IRunReadModel session, long tick)
        {
            Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Drawing.enabled, Is.False);
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
        }

        [TestCase(RunPhase.Victory)] [TestCase(RunPhase.Defeat)]
        public void CompactResultKeepsItsNavigationReadableAndDetailsDismissible(RunPhase outcome)
        {
            Assert.That(game.StartBattle(), Is.True);
            var session = typeof(RunController).GetField("session", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game.Run);
            typeof(GameSession).GetProperty("CompletedWaves").SetValue(session, outcome == RunPhase.Victory ? game.Run.Session.Balance.Waves.Count : 3);
            typeof(GameSession).GetProperty("WaveIndex").SetValue(session, outcome == RunPhase.Victory ? game.Run.Session.Balance.Waves.Count : 4);
            typeof(GameSession).GetProperty("Phase").SetValue(session, outcome);
            game.Navigate(PlayerPage.Result); shell.ApplyLayout(800, 600); Canvas.ForceUpdateCanvases();
            var page = Rect("Page Result");
            foreach (string name in new[] { "Result home", "Result map", "Result album", "Result retry", "Result statistics" })
            {
                var action = Button(name).GetComponent<RectTransform>();
                AssertWithin(page, action);
                Assert.That(action.rect.height, Is.GreaterThanOrEqualTo(44), name);
            }
            Click("Result statistics");
            Assert.That(Rect("Context help").gameObject.activeInHierarchy, Is.True);
            game.Back();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Result));
            Assert.That(Button("Result home").IsInteractable(), Is.True);
        }


        static UnityEngine.Rect Bounds(RectTransform boundary, RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 minimum = boundary.InverseTransformPoint(corners[0]), maximum = minimum;
            foreach (var corner in corners)
            {
                Vector2 point = boundary.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
            }
            return UnityEngine.Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        static void AssertWithin(RectTransform boundary, RectTransform target)
        {
            var bounds = Bounds(boundary, target);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(boundary.rect.xMin - 1), target.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(boundary.rect.xMax + 1), target.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(boundary.rect.yMin - 1), target.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(boundary.rect.yMax + 1), target.name);
        }
    }
}
#endif
