#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    /// <summary>Production battle layout must remain readable between the usual resolution presets.</summary>
    public sealed class ResponsiveBattleUiTests : BattleHudThemeFixture
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Vector2Int[] HeaderSizes = {
            new Vector2Int(800, 600), new Vector2Int(1049, 720), new Vector2Int(1050, 720),
            new Vector2Int(1051, 720), new Vector2Int(1100, 720), new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3440, 1440),
            new Vector2Int(3840, 2160)
        };

        T HudField<T>(string name) => (T)typeof(PlayerBattleHud).GetField(name, Private).GetValue(Hud);
        GameSession Session => (GameSession)typeof(RunController).GetField("session", Private).GetValue(game.Run);

        [UnityTest] public IEnumerator AlbumHeaderKeepsCountersForecastAndWaveActionsSeparateAcrossBreakpoints()
        {
            StartBattle();
            foreach (var size in HeaderSizes)
            {
                Size(size); yield return null; Canvas.ForceUpdateCanvases();
                var header = Named<RectTransform>("Battle header");
                var forecast = HudField<Text>("albumForecast");
                var regions = new[] {
                    Named<RectTransform>("Battle live ink chip"), Named<RectTransform>("Battle live castle chip"),
                    Named<RectTransform>("Battle wave ribbon"), HudField<Text>("albumPhase").rectTransform,
                    Named<RectTransform>("Launch wave"), Named<RectTransform>("Battle speed"),
                    Named<RectTransform>("Battle ranges"), Named<RectTransform>("Battle overview"), Named<RectTransform>("Tactical pause"),
                    Named<RectTransform>("Battle pause")
                };
                for (int i = 0; i < regions.Length; i++)
                {
                    AssertInside(regions[i], header, size.ToString());
                    for (int previous = 0; previous < i; previous++)
                        Assert.That(BattleHudThemeEvidence.Bounds(header, regions[i]).Overlaps(
                            BattleHudThemeEvidence.Bounds(header, regions[previous])), Is.False,
                            regions[i].name + " overlaps " + regions[previous].name + " at " + size);
                }
                AssertInside(HudField<Text>("albumSpeed").rectTransform, Named<RectTransform>("Battle speed"), "Nested speed counter at " + size);
                foreach (string field in new[] { "albumInk", "albumHealth", "albumWave", "albumPhase", "albumSpeed" })
                    AssertReadable(HudField<Text>(field), "Live header counter " + field + " at " + size);
                Assert.That(HudField<Text>("albumInk").text, Is.EqualTo(game.Run.Session.Ink.ToString()));
                Assert.That(HudField<Text>("albumHealth").text, Is.EqualTo(Mathf.CeilToInt(game.Run.Session.BaseHealth) + "/" + Mathf.CeilToInt(game.Run.Session.Balance.BaseHealth)));
                Assert.That(forecast.text, Does.Contain(game.Run.Session.Balance.Waves[game.Run.Session.WaveIndex].Count.ToString()));
                var launch = Named<Button>("Launch wave");
                Assert.That(launch.GetComponent<PlayerTooltip>().Message, Does.Contain(forecast.text));
                EventSystem.current.SetSelectedGameObject(launch.gameObject);
                Canvas.ForceUpdateCanvases();
                var visibleForecast = Named<Text>("Battle tool hint");
                Assert.That(visibleForecast.gameObject.activeInHierarchy, Is.True);
                Assert.That(visibleForecast.text, Does.Contain(forecast.text), "Keyboard focus must expose the full forecast.");
                AssertReadable(visibleForecast, "The next wave forecast at " + size);
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        [Test] public void IntermissionForecastUsesNextHordesRealGatesAndFitsTheSmallHeader()
        {
            StartBattle(); Size(new Vector2Int(800, 600));
            game.Drawing.SetBattleView(false);
            var session = game.Run.Session;
            var firstWest = session.Battlefield.Entrances.First(cell => session.Battlefield.Side(cell) == 1);
            var firstEast = session.Battlefield.Entrances.First(cell => session.Battlefield.Side(cell) == 2);
            int GateVertices(Cell gate)
            {
                var paper = game.World.transform.Find("Graph paper").GetComponent<MeshFilter>().sharedMesh;
                var center = new Vector2(gate.X + .5f, gate.Y + .5f);
                return paper.vertices.Count(vertex => ((Vector2)vertex - center).sqrMagnitude < 9);
            }
            int activeWest = 0, quietEast = 0;
            string[] approaches = { "с запада", "с востока", "с севера и юга", "со всех сторон" };
            for (int wave = 0; wave < approaches.Length; wave++)
            {
                var next = session.Balance.Waves[wave];
                Hud.Refresh(); Canvas.ForceUpdateCanvases(); game.World.SendMessage("LateUpdate");
                var caption = HudField<Text>("albumPhase");
                Assert.That(caption.text, Does.Contain(next.Count + " " + approaches[wave]));
                AssertReadable(caption, "Preparation forecast " + (wave + 1));
                Assert.That(Named<Button>("Launch wave").GetComponent<PlayerTooltip>().Message, Does.Contain(approaches[wave]));
                if (wave == 0) { activeWest = GateVertices(firstWest); quietEast = GateVertices(firstEast); }
                if (wave == 1)
                {
                    Assert.That(GateVertices(firstWest), Is.LessThan(activeWest), "Retire the previous chevron on the map.");
                    Assert.That(GateVertices(firstEast), Is.GreaterThan(quietEast), "Mark the actual incoming eastern gate.");
                }
                void AssertEdges()
                {
                    for (int side = 0; side < 4; side++)
                        Assert.That(Named<RectTransform>("Battle approach " + side).gameObject.activeSelf,
                            Is.EqualTo((next.Sides & (1 << side)) != 0), "Edge " + side + " on wave " + (wave + 1));
                }
                AssertEdges();
                game.Run.NextWave(); Hud.Refresh();
                AssertEdges(); // During the assault the arrows continue to show the attacking wave.
                if (wave == approaches.Length - 1) break;
                typeof(GameSession).GetProperty(nameof(GameSession.RemainingToSpawn)).SetValue(Session, 0);
                game.Run.StepSimulation();
                Assert.That(session.Phase, Is.EqualTo(RunPhase.Intermission));
            }
        }

        [UnityTest] public IEnumerator ColorStocksRemainExactInCountersAndInspectorAtTheMinimumWindow()
        {
            StartBattle(); Size(new Vector2Int(800, 600)); yield return null;
            var cell = BuildTower(); game.Drawing.Inspect(cell);
            var stock = (int[])typeof(GameSession).GetField("coloredInk", Private).GetValue(Session);
            foreach (int amount in new[] { 999, 100, 45 })
            {
                for (int color = 1; color <= 3; color++) stock[color] = amount;
                Hud.Refresh(); Canvas.ForceUpdateCanvases(); yield return null;
                for (int color = 1; color <= 3; color++)
                {
                    var buy = Named<Button>("Modernize color " + (InkColor)color);
                    UiScrollFocus.Reveal((RectTransform)buy.transform.parent);
                    Canvas.ForceUpdateCanvases(); yield return null;
                    var counter = buy.transform.parent.GetComponentsInChildren<Text>().Single(text => text.text.Contains("Запас "));
                    Assert.That(counter.text, Does.Contain("Запас " + amount));
                    AssertReadable(counter, "Color stock " + color + " = " + amount);
                    AssertInside(counter.rectTransform, (RectTransform)buy.transform.parent, "Counter inside its color row");
                    var chip = Named<Selectable>("Ink stock " + (InkColor)color);
                    Assert.That(chip.GetComponent<Button>(), Is.Null);
                    Assert.That(chip.GetComponent<PlayerTooltip>().Message, Does.Contain("запас " + amount));
                    var visible = chip.GetComponentInChildren<Text>();
                    Assert.That(visible.text, Is.EqualTo(amount.ToString()));
                    AssertReadable(visible, "Stock counter " + color + " = " + amount);
                }
            }
        }

        void EarnFlask()
        {
            game.Run.NextWave();
            Assert.That(Session.Phase, Is.EqualTo(RunPhase.Assault));
            // Shorten this UI fixture's wave; run the usual completion and award logic on the next simulation step.
            typeof(GameSession).GetProperty(nameof(GameSession.RemainingToSpawn)).SetValue(Session, 0);
            game.Run.StepSimulation();
            Hud.Refresh();
            Assert.That(Session.Phase, Is.EqualTo(RunPhase.Intermission));
        }

        [UnityTest] public IEnumerator WaveFlasksAreClaimableOnFirstPlayAndRetryAndRejectAPreviousRunPress()
        {
            StartBattle();
            Assert.That(Named<RectTransform>("Ink flask reward").gameObject.activeSelf, Is.False);
            EarnFlask();
            Click("Claim flask Green");
            Assert.That(game.Run.Session.ColoredInk(InkColor.Green), Is.EqualTo(Session.InkPerFlask));
            Assert.That(game.Run.Session.PendingInkFlasks, Is.Zero);
            yield return new WaitForSecondsRealtime(.45f);

            game.Retry(); EarnFlask();
            var firstRun = game.Run.Session.RunId;
            var old = Named<Button>("Claim flask Red");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(old.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            game.Retry(); EarnFlask();
            Assert.That(game.Run.Session.RunId, Is.Not.EqualTo(firstRun));
            ExecuteEvents.Execute(old.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(old.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(game.Run.Session.PendingInkFlasks, Is.EqualTo(1), "Holding the mouse over Retry must not claim the next attempt's identically numbered flask.");
            var fresh = Named<Button>("Claim flask Blue");
            EventSystem.current.SetSelectedGameObject(fresh.gameObject);
            ExecuteEvents.Execute(fresh.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(game.Run.Session.ColoredInk(InkColor.Blue), Is.EqualTo(Session.InkPerFlask));
            Assert.That(game.Run.Session.ColoredInk(InkColor.Red), Is.Zero);
            Assert.That(game.Run.Session.PendingInkFlasks, Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Named<Selectable>("Ink stock Blue").gameObject));
        }

        [UnityTest] public IEnumerator FlaskHotkeyWorksFromTheFieldWithoutASelectedControl()
        {
            using (new NativeInputIsolation())
            {
                var previous = InputSystem.settings;
                var settings = Object.Instantiate(previous);
                var flags = previous.hideFlags;
                settings.hideFlags = HideFlags.HideAndDontSave;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Keyboard keyboard = null;
                try
                {
                    previous.hideFlags = HideFlags.None;
                    InputSystem.settings = settings;
                    previous.hideFlags = flags;
                    keyboard = InputSystem.AddDevice<Keyboard>("Ink flask keyboard");
                    keyboard.MakeCurrent();
                    StartBattle(); EarnFlask();
                    EventSystem.current.SetSelectedGameObject(null);
                    shell.enabled = true;
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2));
                    yield return null; yield return null;
                    Assert.That(Session.PendingInkFlasks, Is.Zero);
                    Assert.That(Session.ColoredInk(InkColor.Blue), Is.EqualTo(Session.InkPerFlask));
                }
                finally
                {
                    shell.enabled = false;
                    if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                    InputSystem.settings = previous;
                    previous.hideFlags = flags;
                    Object.Destroy(settings);
                }
            }
        }

        [UnityTest] public IEnumerator BankedFlasksKeepTheFieldUsableAndPayOncePerClick()
        {
            StartBattle(); EarnFlask(); EarnFlask();
            Assert.That(Session.PendingInkFlasks, Is.EqualTo(2));
            Size(new Vector2Int(800, 600));
            game.Drawing.Inspect(BuildTower());
            Hud.Refresh(); yield return null; Canvas.ForceUpdateCanvases();
            var reward = Named<RectTransform>("Ink flask reward");
            var inspector = Named<RectTransform>("Battle context");
            Assert.That(reward.GetComponent<Graphic>().raycastTarget, Is.True);
            Assert.That(BattleHudThemeEvidence.Bounds(Battle, reward).Overlaps(BattleHudThemeEvidence.Bounds(Battle, inspector)), Is.False);
            Assert.That(Named<Text>("Ink flask count").text, Is.EqualTo("×2"));
            AssertReadable(Named<Text>("Ink flask prompt"), "Reward prompt at 800x600");
            var dose = Named<Text>("Ink flask yield");
            Assert.That(dose.text, Is.EqualTo("+" + Session.InkPerFlask + " чернил"));
            AssertReadable(dose, "Shared reward amount at 800x600");
            foreach (InkColor color in new[] { InkColor.Red, InkColor.Blue, InkColor.Green })
            {
                var button = Named<Button>("Claim flask " + color);
                AssertInside((RectTransform)button.transform, reward, "Accessible color choice");
                AssertReadable(button.GetComponentInChildren<Text>(), "Reward amount and color at 800x600");
            }
            game.Run.SetModalSuspended(true); Hud.Refresh();
            Named<Button>("Claim flask Red").onClick.Invoke();
            Assert.That(Session.PendingInkFlasks, Is.EqualTo(2));
            game.Run.SetModalSuspended(false);
            game.Run.TogglePause(); Hud.Refresh();
            Click("Claim flask Red"); Click("Claim flask Red");
            Assert.That(Session.ColoredInk(InkColor.Red), Is.EqualTo(Session.InkPerFlask));
            Assert.That(Session.PendingInkFlasks, Is.EqualTo(1), "A double click must not consume a second banked reward.");
            yield return new WaitForSecondsRealtime(.45f);
            Click("Claim flask Green");
            Assert.That(Session.ColoredInk(InkColor.Green), Is.EqualTo(Session.InkPerFlask));
            Assert.That(Session.PendingInkFlasks, Is.Zero);
            Assert.That(reward.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator CompactInspectorShowsLiveHealthAndConstructionStateBeforeScrolling()
        {
            StartBattle(); var cell = BuildTower();
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1280, 600), new Vector2Int(1600, 900) })
            {
                Size(size); game.Drawing.Inspect(cell); Hud.Refresh(); yield return null; Canvas.ForceUpdateCanvases();
                var scroll = Named<ScrollRect>("Selected structure details");
                if (scroll.content.rect.height > scroll.viewport.rect.height + 1)
                    Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.005f));
                var summary = Named<Text>("Modernization limits");
                Assert.That(summary.text, Does.Contain("HP ").And.Contain("Строится").And.Contain("Тех. I"));
                AssertInside(summary.rectTransform, scroll.viewport, "Live health and construction state at " + size);
                AssertReadable(summary, "Live health and construction state at " + size);
                var firstDirection = (RectTransform)Named<Button>("Modernize Power").transform.parent;
                Assert.That(BattleHudThemeEvidence.Bounds(scroll.content, firstDirection).yMax,
                    Is.LessThanOrEqualTo(BattleHudThemeEvidence.Bounds(scroll.content, summary.rectTransform).yMin - 1),
                    "Directions must start below the live state at " + size);
            }
        }

        Cell BuildTower()
        {
            for (int y = 2; y < game.Run.Session.Balance.Height - 2; y++)
            for (int x = 2; x < game.Run.Session.Balance.Width - 2; x++)
            {
                var cell = new Cell(x, y);
                if (!game.Run.QuoteEntry(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None).Valid) continue;
                Assert.That(game.Run.BuildEntry(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                return cell;
            }
            Assert.Fail("An affordable free cell is required for the production tower."); return default;
        }

        static void AssertReadable(Text text, string message)
        {
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), message);
            Assert.That(text.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(text.text.Length), message);
        }

        static void AssertInside(RectTransform target, RectTransform parent, string message)
        {
            var bounds = BattleHudThemeEvidence.Bounds(parent, target); var outer = parent.rect;
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 1), target.name + ": " + message);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(outer.xMax + 1), target.name + ": " + message);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 1), target.name + ": " + message);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(outer.yMax + 1), target.name + ": " + message);
        }
    }
}
#endif
