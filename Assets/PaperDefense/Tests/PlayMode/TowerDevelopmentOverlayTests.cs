#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class TowerDevelopmentOverlayTests
    {
        GameObject root;
        PlayerGameRoot game;
        PlayerShell shell;
        GameBalanceAsset balance;
        BalancePresetAsset preset;

        TowerDevelopmentOverlay Overlay => root.GetComponentsInChildren<TowerDevelopmentOverlay>().Single();
        PlayerBattleHud Hud => (PlayerBattleHud)typeof(PlayerShell).GetField("battle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);
        Button Badge(int id) => Overlay.GetComponentsInChildren<Button>(true).Single(button => button.name == "Tower development " + id);
        Text Level(int id) => Badge(id).GetComponentsInChildren<Text>(true).Single(text => text.name == "Tower level " + id);
        PlayerIconGraphic Status(int id) => Badge(id).GetComponentsInChildren<PlayerIconGraphic>(true).Single(icon => icon.name != "Tower ink " + id);
        StructureView Current(StructureView tower) => game.Run.Session.StructureAt(tower.Cell).Value;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            root = new GameObject("Tower development overlay fixture"); root.SetActive(false);
            new GameObject("Fixture camera", typeof(Camera)).transform.SetParent(root.transform);
            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            balance = Object.Instantiate(source); preset = Object.Instantiate(source.dataPreset);
            balance.dataPreset = preset; preset.rules.PlanningSeconds = 1000;
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true); yield return null;
            Assert.That(game.StartBattle(), Is.True);
            game.Run.AutomaticSimulation = false;
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            shell.ApplyLayout(800, 600);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            Object.Destroy(root); Object.Destroy(balance); Object.Destroy(preset); yield return null;
        }

        StructureView BuildReadyTower(Cell? near = null)
        {
            var rules = game.Run.Session.Balance;
            var target = near ?? new Cell(rules.Width / 2, rules.Height / 2);
            var cells = from y in Enumerable.Range(2, rules.Height - 4)
                        from x in Enumerable.Range(2, rules.Width - 4)
                        let cell = new Cell(x, y)
                        orderby Mathf.Abs(x - target.X) + Mathf.Abs(y - target.Y)
                        select cell;
            foreach (var cell in cells)
            {
                if (near.HasValue && Mathf.Abs(cell.X - near.Value.X) + Mathf.Abs(cell.Y - near.Value.Y) > 3) continue;
                if (!game.Run.QuoteEntry(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None).Valid) continue;
                Assert.That(game.Run.BuildEntry(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                for (int step = 0; step < 600 && !game.Run.Session.StructureAt(cell).Value.IsComplete; step++) game.Run.StepSimulation();
                var tower = game.Run.Session.StructureAt(cell).Value;
                Assert.That(tower.IsComplete, Is.True); return tower;
            }
            Assert.Fail("No affordable legal production tower position near " + target); return default;
        }

        void Refresh()
        {
            Hud.Refresh(); Overlay.RefreshNow(); Canvas.ForceUpdateCanvases();
        }

        IEnumerator Focus(StructureView tower)
        {
            game.Drawing.SetBattleView(false);
            game.Drawing.Focus(tower.Cell);
            yield return null; yield return null;
            Refresh();
        }

        static Rect Bounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var a = parent.InverseTransformPoint(corners[0]); var b = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        void AssertInsideField(Button button)
        {
            var field = (RectTransform)Overlay.transform;
            var rect = Bounds(field, (RectTransform)button.transform);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(field.rect.xMin));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(field.rect.xMax));
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(field.rect.yMin));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(field.rect.yMax));
        }

        [UnityTest] public IEnumerator AvailablePlusMatchesRealQuotesAndBadgeSelectsItsOwnTowerWithoutSpending()
        {
            var first = BuildReadyTower(); var second = BuildReadyTower(first.Cell);
            yield return Focus(second);
            var run = game.Run.Session;
            bool available = new[] { ModernizationDirection.Power, ModernizationDirection.Mechanics, ModernizationDirection.Range, ModernizationDirection.Resilience }
                .Any(direction => run.QuoteModernization(second.Id, direction).Valid) ||
                new[] { InkColor.Red, InkColor.Blue, InkColor.Green }.Any(color => run.QuoteColorModernization(second.Id, color).Valid);
            Assert.That(available, Is.True, "The real starting ink must afford at least one improvement.");
            Assert.That(Status(second.Id).Icon, Is.EqualTo(PlayerIcon.Plus));
            Assert.That(Level(second.Id).text, Is.EqualTo("0/6"));
            game.Drawing.Inspect(first.Cell); game.Drawing.SelectMode(DrawingMode.Erase); Refresh();
            int ink = run.Ink, count = run.Structures.Count; long tick = run.TickIndex;
            int revision = Current(second).Modernization.Revision;
            var action = Badge(second.Id);
            Assert.That(action.gameObject.activeInHierarchy, Is.True); Assert.That(action.IsInteractable(), Is.True);
            action.onClick.Invoke();
            Assert.That(game.Drawing.Mode, Is.EqualTo(DrawingMode.Build));
            Assert.That(game.Drawing.InspectedCell, Is.EqualTo(second.Cell));
            Assert.That(run.Ink, Is.EqualTo(ink)); Assert.That(run.Structures.Count, Is.EqualTo(count)); Assert.That(run.TickIndex, Is.EqualTo(tick));
            Assert.That(Current(second).Modernization.Revision, Is.EqualTo(revision));
            Assert.That(Current(first).Modernization.StepsUsed, Is.Zero);
            Assert.That(root.GetComponentsInChildren<Button>().Any(button => button.name == "Modernize Power"), Is.True);
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            typeof(GameSession).GetProperty("Ink").SetValue(session, 0);
            Refresh();
            Assert.That(new[] { ModernizationDirection.Power, ModernizationDirection.Mechanics, ModernizationDirection.Range, ModernizationDirection.Resilience }
                .Any(direction => game.Run.QuoteModernization(second.Id, direction).Valid), Is.False);
            Assert.That(new[] { InkColor.Red, InkColor.Blue, InkColor.Green }
                .Any(color => game.Run.QuoteColorModernization(second.Id, color).Valid), Is.False);
            Assert.That(Status(second.Id).Icon, Is.EqualTo(PlayerIcon.Lock));
            Assert.That(Badge(second.Id).GetComponent<PlayerTooltip>().Message, Does.Contain("усиление недоступно"));
        }

        [UnityTest] public IEnumerator PaidUpgradeShowsWorkProgressThenTheCompletedOrdinaryLevel()
        {
            var tower = BuildReadyTower(); yield return Focus(tower);
            Badge(tower.Id).onClick.Invoke();
            var quote = game.Run.QuoteModernization(tower.Id, ModernizationDirection.Power);
            Assert.That(quote.Valid, Is.True, quote.Reason); int ink = game.Run.Session.Ink;
            var buy = root.GetComponentsInChildren<Button>().Single(button => button.name == "Modernize Power");
            buy.onClick.Invoke(); Refresh();
            Assert.That(game.Run.Session.Ink, Is.EqualTo(ink - quote.Cost));
            Assert.That(Status(tower.Id).Icon, Is.EqualTo(PlayerIcon.Pencil));
            var progress = Badge(tower.Id).GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Tower modernization progress");
            Assert.That(progress.gameObject.activeInHierarchy, Is.True);
            float started = progress.localScale.x;
            for (int step = 0; step < 30; step++) game.Run.StepSimulation();
            Refresh();
            Assert.That(progress.localScale.x, Is.GreaterThan(started).And.LessThan(1));
            for (int step = 0; step < 300 && Current(tower).Modernization.IsWorking; step++) game.Run.StepSimulation();
            Refresh();
            Assert.That(Current(tower).Modernization.Rank(ModernizationDirection.Power), Is.EqualTo(1));
            Assert.That(Level(tower.Id).text, Is.EqualTo("1/6"));
            Assert.That(progress.gameObject.activeSelf, Is.False);
            Assert.That(Status(tower.Id).Icon, Is.EqualTo(PlayerIcon.Plus));
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            var coloredInk = (int[])typeof(GameSession).GetField("coloredInk", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            coloredInk[(int)InkColor.Blue] = 1000; // Isolated presentation fixture, no campaign balance assertion.
            var colorQuote = game.Run.QuoteColorModernization(tower.Id, InkColor.Blue);
            Assert.That(game.Run.Modernize(colorQuote, out var reason), Is.True, reason);
            for (int step = 0; step < 300 && Current(tower).Modernization.IsWorking; step++) game.Run.StepSimulation();
            Refresh();
            var symbol = Badge(tower.Id).GetComponentsInChildren<PlayerIconGraphic>().Single(icon => icon.name == "Tower ink " + tower.Id);
            Assert.That(symbol.Icon, Is.EqualTo(PlayerIcon.Drop)); Assert.That(symbol.color, Is.EqualTo(NotebookPalette.Blue));
            Assert.That(Badge(tower.Id).GetComponentsInChildren<Text>().Any(text => text.text == "I"), Is.True);
            Assert.That(Level(tower.Id).text, Is.EqualTo("1/6"), "Color must not consume ordinary steps.");
            Assert.That(Badge(tower.Id).GetComponent<PlayerTooltip>().Message, Does.Contain("Синий I").And.Contain("закреплён"));
            game.Drawing.Inspect(null); game.Drawing.SetBattleView(true);
            yield return null; yield return null; Refresh();
            Assert.That(Level(tower.Id).gameObject.activeSelf, Is.False);
            Assert.That(Status(tower.Id).color, Is.EqualTo(NotebookPalette.Blue), "An overview marker keeps the tower's ink color.");
        }

        [UnityTest] public IEnumerator RemovedTowerRetiresItsClickableBadgeAndConnector()
        {
            var tower = BuildReadyTower(); yield return Focus(tower);
            var old = Badge(tower.Id); Assert.That(old.gameObject.activeInHierarchy, Is.True);
            Assert.That(game.Run.Erase(tower.Cell, out _), Is.True); Refresh();
            Assert.That(Overlay.ActiveBadgeCount, Is.Zero); Assert.That(Overlay.VisibleBadgeCount, Is.Zero);
            Assert.That(old.gameObject.activeInHierarchy, Is.False);
            Assert.That(Overlay.GetComponentsInChildren<Button>().Any(button => button.name == "Tower development " + tower.Id), Is.False);
            Assert.That(Overlay.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Tower badge connector"), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator UnchangedResourcesAndRevisionDoNotRecalculateQuotesEveryRefresh()
        {
            var tower = BuildReadyTower(); yield return Focus(tower); Refresh();
            var overlay = Overlay; int refreshes = overlay.QuoteRefreshCount;
            int ink = game.Run.Session.Ink, revision = Current(tower).Modernization.Revision; long tick = game.Run.Session.TickIndex;
            Assert.That(refreshes, Is.GreaterThan(0));
            for (int frame = 0; frame < 5; frame++) { overlay.RefreshNow(); Canvas.ForceUpdateCanvases(); yield return null; }
            Assert.That(overlay.QuoteRefreshCount, Is.EqualTo(refreshes));
            Assert.That(game.Run.Session.Ink, Is.EqualTo(ink)); Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(Current(tower).Modernization.Revision, Is.EqualTo(revision));
        }

        [UnityTest] public IEnumerator NearbyBadgesStayDistinctAndInsideTheFieldAt800And1600()
        {
            var first = BuildReadyTower(); var second = BuildReadyTower(first.Cell);
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1600, 900) })
            {
                shell.ApplyLayout(size.x, size.y); yield return Focus(first);
                Assert.That(Overlay.ActiveBadgeCount, Is.EqualTo(2)); Assert.That(Overlay.VisibleBadgeCount, Is.EqualTo(2), size.ToString());
                var a = Badge(first.Id); var b = Badge(second.Id);
                AssertInsideField(a); AssertInsideField(b);
                Assert.That(Bounds((RectTransform)Overlay.transform, (RectTransform)a.transform)
                    .Overlaps(Bounds((RectTransform)Overlay.transform, (RectTransform)b.transform)), Is.False, size.ToString());
                foreach (var text in new[] { Level(first.Id), Level(second.Id) })
                {
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(14));
                    Assert.That(text.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(text.text.Length));
                }
            }
        }

        [UnityTest] public IEnumerator HordeOverviewKeepsEightClickableTowerMarkersAndExpandsTheInspectedTower()
        {
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            typeof(GameSession).GetProperty(nameof(GameSession.Ink)).SetValue(session, 1000);
            var towers = new StructureView[8];
            for (int i = 0; i < towers.Length; i++) towers[i] = BuildReadyTower();
            Assert.That(game.Run.Session.Balance.Waves[0].IsHorde, Is.True);
            game.Drawing.SetBattleView(true);
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1600, 900) })
            {
                shell.ApplyLayout(size.x, size.y); yield return null; yield return null; Refresh();
                Assert.That(Overlay.VisibleBadgeCount, Is.EqualTo(towers.Length));
                var field = (RectTransform)Overlay.transform;
                for (int i = 0; i < towers.Length; i++)
                {
                    var button = Badge(towers[i].Id);
                    var bounds = Bounds(field, (RectTransform)button.transform);
                    Assert.That(bounds.width, Is.InRange(15.9f, 16.1f));
                    Assert.That(bounds.height, Is.InRange(15.9f, 16.1f));
                    Assert.That(button.IsInteractable(), Is.True);
                    Assert.That(button.GetComponentsInChildren<Text>(), Is.Empty);
                    Assert.That(button.GetComponent<PlayerTooltip>().Message, Does.Contain("0/6"));
                    for (int j = 0; j < i; j++)
                        Assert.That(bounds.Overlaps(Bounds(field, (RectTransform)Badge(towers[j].Id).transform)), Is.False);
                }
            }
            var chosen = towers[3];
            var chosenButton = Badge(chosen.Id);
            chosenButton.onClick.Invoke(); Refresh();
            Assert.That(game.Drawing.InspectedCell, Is.EqualTo(chosen.Cell));
            Assert.That(Level(chosen.Id).gameObject.activeInHierarchy, Is.True);
            Assert.That(Badge(towers[0].Id).GetComponentsInChildren<Text>(), Is.Empty);
            game.Drawing.Inspect(null);
            yield return Focus(chosen);
            Assert.That(Badge(chosen.Id), Is.SameAs(chosenButton));
            Assert.That(Level(chosen.Id).text, Is.EqualTo("0/6"));
            Assert.That(Level(chosen.Id).gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest] public IEnumerator CameraPanAndZoomRepositionBadgesAndOffscreenTowersLoseTheirTargets()
        {
            var tower = BuildReadyTower(); yield return Focus(tower);
            var initial = ((RectTransform)Badge(tower.Id).transform).anchoredPosition;
            game.Drawing.Focus(new Cell(tower.Cell.X + 4, tower.Cell.Y));
            yield return null; yield return null; Refresh();
            var panned = ((RectTransform)Badge(tower.Id).transform).anchoredPosition;
            Assert.That(Vector2.Distance(initial, panned), Is.GreaterThan(1), "The badge must follow the same tower when the camera moves.");
            AssertInsideField(Badge(tower.Id));
            game.Drawing.SetBattleView(true);
            yield return null; yield return null; Refresh();
            Assert.That(Badge(tower.Id).gameObject.activeInHierarchy, Is.True);
            Assert.That(Vector2.Distance(panned, ((RectTransform)Badge(tower.Id).transform).anchoredPosition), Is.GreaterThan(1));
            AssertInsideField(Badge(tower.Id));
            game.Drawing.SetBattleView(false);
            int farX = tower.Cell.X < game.Run.Session.Balance.Width / 2 ? game.Run.Session.Balance.Width - 2 : 2;
            game.Drawing.Focus(new Cell(farX, tower.Cell.Y));
            yield return null; yield return null; Refresh();
            var point = game.World.WorldCamera.WorldToViewportPoint(new Vector3(tower.Cell.X + .5f, tower.Cell.Y + .5f, 0));
            Assert.That(point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1, Is.True, "Fixture camera must actually move the tower offscreen.");
            Assert.That(Overlay.ActiveBadgeCount, Is.EqualTo(1)); Assert.That(Overlay.VisibleBadgeCount, Is.Zero);
            Assert.That(Badge(tower.Id).gameObject.activeInHierarchy, Is.False, "An offscreen tower must not leave a clickable stale target.");
        }
    }
}
#endif
