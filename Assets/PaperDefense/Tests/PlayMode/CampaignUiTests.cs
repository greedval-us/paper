#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class CampaignUiTests : BattleHudThemeFixture
    {
        Button Action(string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        Text Copy(string name) => root.GetComponentsInChildren<Text>().Single(text => text.name == name);
        void Press(string name) { var button = Action(name); Assert.That(button.IsInteractable(), Is.True, name); button.onClick.Invoke(); Canvas.ForceUpdateCanvases(); }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void ScoutReportsRealArmorOnLockedMapsAndTheNextFormationDuringIntermission(int width, int height)
        {
            Size(new Vector2Int(width, height));
            game.OpenCampaign(); Press("Campaign notebook 3"); Press("Campaign page notebook-3-page-2");
            var armorWaves = game.Run.CreateMissionPreview("notebook-3-page-2").Balance.Waves;
            string Armor(int index) => "Броня ≈" + Mathf.RoundToInt(100 *
                (armorWaves[index].OutlinedFraction + armorWaves[index].HatchedFraction)) + "%";
            var button = Action("Campaign wave scout");
            Assert.That(button.GetComponentInChildren<Text>().text, Does.Contain(Armor(0)));
            AssertReadableLabel(button.GetComponentInChildren<Text>());
            Press("Campaign wave scout");
            string report = Copy("Context help text").text;
            Assert.That(report, Does.Contain("1. " + armorWaves[0].Count + " справа снизу · " + Armor(0)));
            Assert.That(report, Does.Contain("2. " + armorWaves[1].Count + " справа сверху · " + Armor(1)));
            Assert.That(report, Does.Contain("Зелёная краска"));
            Press("Close context help");

            Press("Campaign notebook 1"); Press("Campaign page notebook-1-page-1"); Press("Open mission"); Press("Prepare mission");
            AssertReadableLabel(Action("Preparation wave scout").GetComponentInChildren<Text>());
            Press("Preparation wave scout");
            Assert.That(Copy("Context help text").text, Does.Contain("Плотная толпа"));
            Press("Close context help"); Press("Start battle"); Hud.Refresh();
            var phase = (Text)typeof(PlayerBattleHud).GetField("albumPhase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Hud);
            Assert.That(phase.text, Does.Contain("Плотная толпа"));
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            game.Run.NextWave();
            typeof(GameSession).GetProperty(nameof(GameSession.RemainingToSpawn)).SetValue(session, 0);
            game.Run.StepSimulation(); Hud.Refresh(); Canvas.ForceUpdateCanvases();
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Intermission));
            string runners = "Бегуны ≈" + Mathf.RoundToInt(session.Balance.Waves[1].RunnerFraction * 100) + "%";
            Assert.That(phase.text, Does.Contain(runners));
            AssertReadableLabel(phase);
            var hint = Action("Launch wave").GetComponent<PlayerTooltip>().Message;
            Assert.That(hint, Does.Contain("с востока"));
            Assert.That(hint, Does.Contain(runners));
            Assert.That(hint, Does.Contain("темп огня"));
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void OpeningHordeFramesBothFrontsBeforeLaunchAndKeepsManualCastleFocus(int width, int height)
        {
            shell.ApplyLayout(width, height);
            StartBattle();
            var session = game.Run.Session;
            Assert.That(session.Balance.Waves[0].IsHorde, Is.True);
            var bootstrap = root.GetComponentInChildren<PaperDefenseBootstrap>();
            var camera = game.World.WorldCamera;
            var previous = camera.targetTexture;
            var screen = new RenderTexture(width, height, 24);
            try
            {
                camera.targetTexture = screen;
                bootstrap.ViewportOverride = shell.Layout.NormalizedField;
                bootstrap.SendMessage("LateUpdate");
                Assert.That(game.Drawing.Zoom, Is.GreaterThan(1.1f), "Use more pixels for the west front than in the whole-page view.");
                AssertOpeningVisible(camera, session);
                game.Drawing.SetBattleView(false);
                var focus = game.Drawing.CameraCenter;
                float zoom = game.Drawing.Zoom;
                game.Run.NextWave(); game.Run.StepSimulation(); bootstrap.SendMessage("LateUpdate");
                Assert.That(game.Drawing.CameraCenter, Is.EqualTo(focus));
                Assert.That(game.Drawing.Zoom, Is.EqualTo(zoom));
            }
            finally { camera.targetTexture = previous; UnityEngine.Object.DestroyImmediate(screen); }
        }

        static void AssertOpeningVisible(Camera camera, IRunReadModel session)
        {
            void InView(float x, float y)
            {
                var point = camera.WorldToViewportPoint(new Vector3(x, y, 0));
                Assert.That(point.x, Is.InRange(0f, 1f));
                Assert.That(point.y, Is.InRange(0f, 1f));
            }
            foreach (var entry in session.Battlefield.Entrances)
            {
                int side = session.Battlefield.Side(entry);
                if ((session.Balance.Waves[session.CompletedWaves].Sides & side) == 0) continue;
                var center = new Vector2(entry.X + .5f, entry.Y + .5f);
                var span = (side == 1 || side == 2 ? Vector2.up : Vector2.right) * session.Balance.EntranceHalfWidth;
                InView(center.x - span.x, center.y - span.y);
                InView(center.x + span.x, center.y + span.y);
            }
            InView(session.BaseCell.X + .5f, session.BaseCell.Y + .5f);
        }

        [Test] public void ResizingTheOpeningRefitsBothFrontsUntilThePlayerMovesTheCamera()
        {
            shell.ApplyLayout(1600, 900);
            StartBattle();
            var session = game.Run.Session;
            var camera = game.World.WorldCamera;
            var previous = camera.targetTexture;
            var wide = new RenderTexture(1600, 900, 24);
            var narrow = new RenderTexture(800, 600, 24);
            var bootstrap = root.GetComponentInChildren<PaperDefenseBootstrap>();
            void Resize(RenderTexture target)
            {
                camera.targetTexture = target;
                shell.ApplyLayout(target.width, target.height);
                bootstrap.ViewportOverride = shell.Layout.NormalizedField;
                bootstrap.SendMessage("LateUpdate");
            }
            try
            {
                Resize(wide);
                AssertOpeningVisible(camera, session);
                float wideCenter = game.Drawing.CameraCenter.x;
                Resize(narrow);
                AssertOpeningVisible(camera, session);
                Assert.That(game.Drawing.CameraCenter.x, Is.LessThan(wideCenter - 1));
                game.Drawing.Focus(session.BaseCell);
                var manualCenter = game.Drawing.CameraCenter;
                float manualZoom = game.Drawing.Zoom;
                Resize(wide);
                Assert.That(game.Drawing.CameraCenter, Is.EqualTo(manualCenter));
                Assert.That(game.Drawing.Zoom, Is.EqualTo(manualZoom));
            }
            finally
            {
                camera.targetTexture = previous;
                UnityEngine.Object.DestroyImmediate(wide);
                UnityEngine.Object.DestroyImmediate(narrow);
            }
        }

        [Test] public void IntermissionFramesTheIncomingFlankUntilThePlayerMovesTheCamera()
        {
            shell.ApplyLayout(1600, 900);
            StartBattle();
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            var camera = game.World.WorldCamera;
            var bootstrap = root.GetComponentInChildren<PaperDefenseBootstrap>();
            void FinishWave()
            {
                game.Run.NextWave();
                for (int second = 0; second < 25 && session.RemainingToSpawn > 0; second++)
                {
                    session.Tick(1f);
                    foreach (var enemy in session.Enemies) enemy.Health = 0;
                }
                Assert.That(session.RemainingToSpawn, Is.Zero);
                session.Tick(session.Balance.SimulationStep);
                Assert.That(session.Phase, Is.EqualTo(RunPhase.Intermission));
                game.SendMessage("FitViewport");
                bootstrap.SendMessage("LateUpdate");
            }
            var previous = camera.targetTexture;
            var target = new RenderTexture(1600, 900, 24);
            try
            {
                camera.targetTexture = target;
                float westCenter = game.Drawing.CameraCenter.x;
                FinishWave();
                Assert.That(game.Drawing.CameraCenter.x, Is.GreaterThan(westCenter + 2));
                AssertOpeningVisible(camera, game.Run.Session);
                game.Drawing.Focus(session.BaseCell);
                var center = game.Drawing.CameraCenter;
                float zoom = game.Drawing.Zoom;
                FinishWave();
                Assert.That(game.Drawing.CameraCenter, Is.EqualTo(center));
                Assert.That(game.Drawing.Zoom, Is.EqualTo(zoom));
            }
            finally { camera.targetTexture = previous; UnityEngine.Object.DestroyImmediate(target); }
        }

        [Test] public void HordeFrontShowsOnlyWoundedGruntsUpCloseAndRetainsPriorityHealthBars()
        {
            StartBattle();
            game.Run.NextWave();
            for (int tick = 0; tick < 8; tick++) game.Run.StepSimulation();
            var session = game.Run.Session;
            Assert.That(session.Enemies.Count, Is.GreaterThan(0));
            var lead = session.Enemies[0];
            var camera = game.World.WorldCamera;
            camera.transform.position = new Vector3(lead.Position.X, lead.Position.Y, -10);
            var mesh = game.World.transform.Find("Swarm, aiming weapons and construction").GetComponent<MeshFilter>().sharedMesh;
            int DrawWithBars(bool visible)
            {
                game.World.ShowEnemyHealthBars = visible;
                game.World.SendMessage("LateUpdate");
                return mesh.vertexCount;
            }

            camera.orthographicSize = 6;
            Assert.That(DrawWithBars(true), Is.EqualTo(DrawWithBars(false)), "Healthy grunts need no bars, even up close.");
            var simulation = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            var unit = simulation.Enemies[0];
            unit.Health = unit.MaxHealth * .5f;
            Assert.That(DrawWithBars(true) - DrawWithBars(false), Is.EqualTo(8), "One wounded grunt adds a slim track and fill.");
            camera.orthographicSize = 20;
            Assert.That(DrawWithBars(true), Is.EqualTo(DrawWithBars(false)), "Wounded grunt bars also disappear on the horde overview.");
            unit.Health = unit.MaxHealth;
            unit.Kind = EnemyKind.Brute;
            Assert.That(DrawWithBars(true) - DrawWithBars(false), Is.EqualTo(16), "A priority unit keeps the full bar at overview.");
        }

        [Test] public void CampaignBrowsingShowsNinePagesWithoutChangingTheSelectedMissionOrIdleSession()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                Assert.That(game.Run.CampaignEnabled, Is.True);
                var idle = game.Run.Session;
                var selected = game.Run.Campaign.SelectedMissionId;
                int goldBefore = game.Run.Workshop.Gold, xpBefore = game.Run.Workshop.TotalXp;
                int writes = fixture.Store.SaveCalls;
                byte[] saved = fixture.SavedBytes;
                var receipt = game.Run.MissionResult;
                var rights = game.Run.AssemblyCatalog.Definitions.Where(part => game.Run.Workshop.IsUnlocked(part.Id)).Select(part => part.Id).ToArray();
                var campaign = game.Run.Campaign.Missions.Select(mission => (mission.Definition.Id, mission.IsUnlocked, mission.IsCompleted)).ToArray();
                Press("Open campaign");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Campaign));
                Assert.That(game.Run.Campaign.Missions.Count, Is.EqualTo(9));
                for (int notebook = 1; notebook <= 3; notebook++)
                {
                    Press("Campaign notebook " + notebook);
                    foreach (var mission in game.Run.Campaign.Missions.Where(item => item.Definition.NotebookIndex == notebook))
                    {
                        Press("Campaign page " + mission.Definition.Id);
                        Assert.That(Action("Open mission").IsInteractable(), Is.EqualTo(mission.IsUnlocked));
                        var preview = root.GetComponentsInChildren<NeutralMissionMapGraphic>().Single();
                        var shown = (IRunReadModel)typeof(NeutralMissionMapGraphic).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
                        var expected = game.Run.CreateMissionPreview(mission.Definition.Id);
                        Assert.That(shown.Balance.Waves.Count, Is.EqualTo(mission.Definition.WaveCount), mission.Definition.Id);
                        Assert.That(shown.Balance.StartingInk, Is.EqualTo(expected.Balance.StartingInk), mission.Definition.Id);
                        Assert.That(shown.BaseCell, Is.EqualTo(expected.BaseCell), mission.Definition.Id);
                        Assert.That(shown.Balance.Width, Is.EqualTo(expected.Balance.Width));
                        Assert.That(shown.Balance.Height, Is.EqualTo(expected.Balance.Height));
                        Assert.That(Copy("Campaign waves").text, Is.EqualTo("Волны: " + expected.Balance.Waves.Count));
                        Assert.That(Copy("Campaign starting ink").text, Is.EqualTo("Старт: " + expected.Balance.StartingInk));
                        Assert.That(Copy("Campaign castle health").text, Is.EqualTo("Замок: " + expected.Balance.BaseHealth.ToString("0.##")));
                        Assert.That(Copy("Campaign objective").text, Is.EqualTo("Цель: сохранить замок после " + mission.Definition.WaveCount + " волн."));
                        AssertRewardPreview("Campaign", mission.Definition, mission.IsCompleted);
                    }
                }
                Assert.That(game.Run.Session, Is.SameAs(idle));
                Assert.That(game.Run.Session.TickIndex, Is.Zero);
                Assert.That(game.Run.Campaign.SelectedMissionId, Is.EqualTo(selected));
                Assert.That(game.Run.Workshop.Gold, Is.EqualTo(goldBefore));
                Assert.That(game.Run.Workshop.TotalXp, Is.EqualTo(xpBefore));
                Assert.That(game.Run.AssemblyCatalog.Definitions.Where(part => game.Run.Workshop.IsUnlocked(part.Id)).Select(part => part.Id), Is.EqualTo(rights));
                Assert.That(game.Run.Campaign.Missions.Select(mission => (mission.Definition.Id, mission.IsUnlocked, mission.IsCompleted)), Is.EqualTo(campaign));
                Assert.That(game.Run.MissionResult, Is.SameAs(receipt));
                Assert.That(game.Run.HasPendingRunRewards, Is.False);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
                game.Back(); Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            }
        }

        [Test] public void LockedMissionCannotBeOpenedThroughTheRootAndTheAvailablePageHasACompleteReturnRoute()
        {
            Press("Open campaign"); Press("Campaign notebook 3");
            Assert.That(game.OpenMission("notebook-3-page-3"), Is.False);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Campaign));
            Press("Close context help");
            Press("Campaign notebook 1"); Press("Campaign page notebook-1-page-1"); Press("Open mission");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Assert.That(game.Run.Session.Balance.Waves.Count, Is.EqualTo(4));
            Press("Prepare mission"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            game.Back(); Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            game.Back(); Assert.That(game.Page, Is.EqualTo(PlayerPage.Campaign));
            game.Back(); Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Assert.That(game.Run.InWorkshop, Is.True);
        }

        [TestCase(800, 600)] [TestCase(1280, 720)] [TestCase(1600, 900)]
        public void NotebookPagesAndFooterRemainReachableAtSupportedAspectRatios(int width, int height)
        {
            Size(new Vector2Int(width, height)); Press("Open campaign");
            for (int notebook = 1; notebook <= 3; notebook++)
            {
                Press("Campaign notebook " + notebook);
                UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
                var page = root.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Page Campaign");
                var corners = new Vector3[4];
                foreach (var button in page.GetComponentsInChildren<Button>())
                {
                    var rect = (RectTransform)button.transform;
                    Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(32), button.name);
                    rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var position = page.InverseTransformPoint(corner);
                        Assert.That(position.x, Is.InRange(page.rect.xMin - 1, page.rect.xMax + 1), button.name);
                        Assert.That(position.y, Is.InRange(page.rect.yMin - 1, page.rect.yMax + 1), button.name);
                    }
                }
                foreach (var mission in game.Run.Campaign.Missions.Where(item => item.Definition.NotebookIndex == notebook))
                {
                    var definition = mission.Definition;
                    var waves = Copy("Campaign page waves " + definition.Id);
                    var state = Copy("Campaign page state " + definition.Id);
                    string waveCount = definition.WaveCount == 4 ? "4 волны" : definition.WaveCount + " волн";
                    Assert.That(waves.text, Is.EqualTo(waveCount + (definition.PageIndex == 3 ? " · финал" : "")));
                    AssertReadableLabel(waves); AssertReadableLabel(state);
                    var card = (RectTransform)Action("Campaign page " + definition.Id).transform;
                    AssertInside(card, waves.rectTransform); AssertInside(card, state.rectTransform);
                    Assert.That(BattleHudThemeEvidence.Bounds(card, waves.rectTransform).Overlaps(BattleHudThemeEvidence.Bounds(card, state.rectTransform)),
                        Is.False, definition.Id + ": wave caption and availability must remain distinct.");
                }
                var preview = page.GetComponentInChildren<NeutralMissionMapGraphic>();
                Assert.That(preview.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0), "Preview must render the actual mission terrain.");
            }
        }

        [Test] public void ResultShowsTheCommittedMissionReceiptAndContinuesToTheUnlockedPage()
        {
            Assert.That(game.StartBattle(), Is.True);
            // This is a presentation fixture. Real enemy combat and mission difficulty are validated separately.
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            typeof(GameSession).GetProperty("CompletedWaves").SetValue(session, session.Balance.Waves.Count);
            typeof(GameSession).GetProperty("WaveIndex").SetValue(session, session.Balance.Waves.Count);
            typeof(GameSession).GetProperty("Phase").SetValue(session, RunPhase.Victory);
            game.Run.StepSimulation(); game.Navigate(PlayerPage.Result); Size(new Vector2Int(800, 600));
            var receipt = game.Run.MissionResult;
            Assert.That(receipt, Is.Not.Null);
            Assert.That(receipt.FirstClear, Is.True);
            Assert.That(root.GetComponentsInChildren<Text>().Single(text => text.name == "Result gold reward").text,
                Does.Contain(receipt.Gold.ToString()));
            Assert.That(root.GetComponentsInChildren<Text>().Single(text => text.name == "Campaign first clear reward").text,
                Does.StartWith("Первая победа"));
            Assert.That(Action("Result next mission").IsInteractable(), Is.True);
            Press("Result next mission");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Assert.That(game.Run.SelectedMission.Id, Is.EqualTo("notebook-1-page-2"));
            game.Back();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Campaign));
            Assert.That(Action("Campaign page notebook-1-page-2").GetComponent<PlayerCardGraphic>().Selected, Is.True);
        }

        void FinishCampaignVictory()
        {
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            typeof(GameSession).GetProperty("CompletedWaves").SetValue(session, game.Run.ActiveMission.WaveCount);
            typeof(GameSession).GetProperty("WaveIndex").SetValue(session, game.Run.ActiveMission.WaveCount);
            typeof(GameSession).GetProperty("Phase").SetValue(session, RunPhase.Victory);
            game.Run.StepSimulation(); game.Navigate(PlayerPage.Result); Canvas.ForceUpdateCanvases();
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void FirstVictoryOpensTheAffordableWeaponModuleOnAnEquippedTowerWithoutSpendingGold(int width, int height)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                Size(new Vector2Int(width, height));
                StartBattle(); FinishCampaignVictory();
                var part = game.Run.AssemblyCatalog.Definitions.Single(item => item.Id == "head.short");
                int gold = game.Run.Workshop.Gold;
                var quote = Action("Result inspect module");
                Assert.That(quote.GetComponentInChildren<Text>().text, Does.Contain(GameText.Resolve(part.Name)).And.Contain(part.UnlockGold.ToString()));
                Assert.That(Copy("Result module benefit").text, Does.Contain("→"));
                int writes = fixture.Store.SaveCalls;
                Press("Result inspect module");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Workshop));
                var panel = root.GetComponentInChildren<WorkshopHud>(true).Composite;
                Assert.That(game.Run.Workshop.LoadoutEntries.Any(entry => entry?.Id == panel.DraftSession.Draft.Id), Is.True);
                Assert.That(panel.DraftSession.ActiveCategory, Is.EqualTo(part.Category));
                Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                var id = panel.DraftSession.Draft.Id;
                string EquippedHead() => game.Run.Workshop.LoadoutEntries.First(entry => entry?.Id == id)
                    .Snapshot.Recipe.Tower.Mounts[0].Weapon.Head.Id;
                var originalHead = EquippedHead();
                Press("Sequence install head.short");
                Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold - part.UnlockGold));
                Assert.That(game.Run.Workshop.IsUnlocked(part.Id), Is.True);
                Assert.That(panel.DraftSession.PreviewRecipe.Tower.Mounts[0].Weapon.Head.Id, Is.EqualTo(part.Id));
                Assert.That(EquippedHead(), Is.EqualTo(originalHead));
                Press("Sequence next"); Press("Sequence next"); Press("Sequence save");
                Assert.That(EquippedHead(), Is.EqualTo(part.Id));
                game.ReturnFromWorkshop();
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Campaign));
                StartBattle(); FinishCampaignVictory();
                Assert.That(root.GetComponentsInChildren<Button>().Any(button => button.name == "Result inspect module" &&
                    button.GetComponentInChildren<Text>().text.Contains(GameText.Resolve(part.Name))), Is.False);
            }
        }

        [Test]
        public void ResultCanOfferAnAffordableSightFromTheCatalogOutsideTheFeaturedList()
        {
            const string id = "sight.tracking";
            var owned = game.Run.AssemblyCatalog.Definitions.Where(item => item.Id != id).Select(item => item.Id).ToArray();
            using (var fixture = new AssemblyCommandFixture(gold: 500, xp: 1100,
                configure: data => data.Unlocked = data.Unlocked.Concat(owned).Distinct().ToArray()))
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                StartBattle(); FinishCampaignVictory();
                var sight = game.Run.AssemblyCatalog.Definitions.Single(item => item.Id == id);
                Assert.That(Action("Result inspect module").GetComponentInChildren<Text>().text, Does.Contain(GameText.Resolve(sight.Name)));
                Assert.That(Copy("Result module benefit").text, Does.Contain("наведение"));
                Press("Result inspect module");
                var panel = root.GetComponentInChildren<WorkshopHud>(true).Composite;
                Assert.That(panel.DraftSession.ActiveCategory, Is.EqualTo(ComponentCategory.Sight));
                Press("Sequence install " + id);
                Assert.That(panel.DraftSession.PreviewRecipe.Tower.Mounts[0].Weapon.Sight.Id, Is.EqualTo(id));
            }
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void CampaignAndMapForecastTheRealFirstVictoryAndThenExcludeItsSavedBonus(int width, int height)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                Size(new Vector2Int(width, height)); Press("Open campaign");
                var mission = game.Run.SelectedMission;
                string firstTotal = null;
                for (int visit = 0; visit < 2; visit++)
                {
                    bool completed = visit == 1;
                    Assert.That(game.Run.Campaign.Missions.Single(item => item.Definition.Id == mission.Id).IsCompleted, Is.EqualTo(completed));
                    int writes = fixture.Store.SaveCalls, gold = game.Run.Workshop.Gold, xp = game.Run.Workshop.TotalXp;
                    byte[] bytes = fixture.SavedBytes; var idle = game.Run.Session;
                    var loadout = game.Run.RunLoadout;
                    Press("Campaign page " + mission.Id);
                    UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
                    AssertRewardPreview("Campaign", mission, completed);
                    AssertRewardRowsVisible("Campaign");
                    AssertFirstPageUnlocks("Campaign", completed);
                    string total = Copy("Campaign reward total").text;
                    Assert.That(total, Is.EqualTo(completed ? "Итого при победе: 43 зол. · 60 XP" : "Итого при победе: 63 зол. · 80 XP"));
                    if (!completed) firstTotal = total;
                    else Assert.That(total, Is.Not.EqualTo(firstTotal), "A persisted first-clear bonus cannot be promised again.");

                    var open = Action("Open mission"); EventSystem.current.SetSelectedGameObject(open.gameObject);
                    ExecuteEvents.Execute(open.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
                    Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
                    AssertRewardPreview("Mission", mission, completed);
                    AssertRewardRowsVisible("Mission");
                    AssertFirstPageUnlocks("Mission", completed);
                    Assert.That(Copy("Mission objective").text, Is.EqualTo("Выдержать все волны.\nЗамок должен уцелеть."));
                    Assert.That(Copy("Mission waves").text, Is.EqualTo("4 волны"));
                    Assert.That(Copy("Mission starting ink").text, Is.EqualTo("Старт · " + idle.Balance.StartingInk + " штр."));
                    Assert.That(Copy("Mission castle health").text, Is.EqualTo(idle.Balance.BaseHealth + " HP замка"));
                    var help = Action("Screen help"); EventSystem.current.SetSelectedGameObject(help.gameObject);
                    ExecuteEvents.Execute(help.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    Canvas.ForceUpdateCanvases();
                    string explanation = Copy("Context help text").text;
                    foreach (string id in mission.UnlockPartIds)
                        Assert.That(explanation, Does.Contain(GameText.Resolve(game.Run.AssemblyCatalog.Definitions.Single(part => part.Id == id).Name)));
                    Assert.That(explanation, Does.Contain(completed ? "Технология II уже открыта." : "Технология II открывается после сохранённой победы."));
                    Assert.That(explanation, Does.Contain(completed ? "Итого за полную победу в новой попытке: 43 зол. · 60 XP" : "Итого за полную победу в новой попытке: 63 зол. · 80 XP"));
                    Press("Close context help");
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(help.gameObject));
                    Assert.That(game.Run.Session, Is.SameAs(idle)); Assert.That(game.Run.RunLoadout, Is.SameAs(loadout));
                    Assert.That(game.Run.Session.TickIndex, Is.Zero);
                    Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold)); Assert.That(game.Run.Workshop.TotalXp, Is.EqualTo(xp));
                    Assert.That(game.Run.HasPendingRunRewards, Is.False);
                    Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                    if (!completed)
                    {
                        Assert.That(game.StartBattle(), Is.True); FinishCampaignVictory();
                        Assert.That(game.Run.IsCurrentMissionRewardSaved, Is.True);
                        Assert.That(game.Run.MissionResult.FirstClear, Is.True);
                        Assert.That(game.Run.MissionResult.Gold, Is.EqualTo(63));
                        Assert.That(game.Run.MissionResult.TotalXp, Is.EqualTo(80));
                        game.OpenCampaign();
                    }
                }
            }
        }

        void AssertRewardPreview(string prefix, CampaignMissionDefinition mission, bool completed)
        {
            var forecast = CampaignMissionResult.Preview(mission, new Guid("f1c6a063-a54d-4f70-961c-f7a6965893bb"), mission.WaveCount, true, 1, !completed);
            Assert.That(Copy(prefix + " reward ordinary").text,
                Is.EqualTo("Обычная победа: " + (forecast.WaveGold + forecast.CompletionGold) + " зол. · " + (forecast.WaveXp + forecast.CompletionXp) + " XP"));
            Assert.That(Copy(prefix + " reward first").text, Is.EqualTo(completed ? "Первый бонус уже получен" :
                "Первый бонус: +" + forecast.FirstClearGold + " зол. · +" + forecast.FirstClearXp + " XP"));
            Assert.That(Copy(prefix + " reward total").text, Is.EqualTo("Итого при победе: " + forecast.Gold + " зол. · " + forecast.TotalXp + " XP"));
            Assert.That(Copy(prefix + " reward unlocks").text, Is.Not.Empty);
        }

        void AssertFirstPageUnlocks(string prefix, bool completed)
        {
            Assert.That(game.Run.UnlockedModernizationRank, Is.EqualTo(completed ? 2 : 1));
            var unlocks = Copy(prefix + " reward unlocks");
            Assert.That(unlocks.text, Is.EqualTo(completed ? "Открытия первого прохождения получены" : "Открытия: 2 детали · Тех. II"));
            string hint = unlocks.GetComponent<WorkshopTooltip>().Hint;
            foreach (string id in new[] { "head.long", "sight.snap" })
            {
                Assert.That(game.Run.Workshop.IsUnlocked(id), Is.EqualTo(completed), id);
                string name = GameText.Resolve(game.Run.AssemblyCatalog.Definitions.Single(part => part.Id == id).Name);
                Assert.That(hint, Does.Contain(name + (completed ? " — уже открыта" : " — открывается")));
            }
            Assert.That(hint, Does.Contain(completed ? "Технология II уже открыта." : "Технология II открывается после сохранённой победы."));
        }

        void AssertRewardRowsVisible(string prefix)
        {
            var page = root.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Page " + game.Page);
            foreach (string suffix in new[] { "ordinary", "first", "total", "unlocks" })
            {
                var copy = Copy(prefix + " reward " + suffix);
                Assert.That(copy.canvasRenderer.cull, Is.False, copy.name);
                AssertInside(page, copy.rectTransform);
                foreach (var scroll in copy.GetComponentsInParent<ScrollRect>()) AssertInside(scroll.viewport, copy.rectTransform);
                AssertReadableLabel(copy);
            }
        }

        void AssertReadableLabel(Text copy)
        {
            var settings = copy.GetGenerationSettings(copy.rectTransform.rect.size * shell.Layout.Scale);
            settings.scaleFactor = 1; settings.fontSize = Mathf.RoundToInt(copy.fontSize * shell.Layout.Scale);
            settings.resizeTextMinSize = Mathf.CeilToInt(copy.resizeTextMinSize * shell.Layout.Scale);
            settings.resizeTextMaxSize = Mathf.Max(settings.resizeTextMinSize, Mathf.RoundToInt(copy.resizeTextMaxSize * shell.Layout.Scale));
            settings.generateOutOfBounds = false;
            using (var generated = new TextGenerator())
            {
                generated.Populate(copy.text, settings);
                Assert.That(generated.characterCountVisible, Is.GreaterThanOrEqualTo(copy.text.Length), copy.name + ": " + copy.text);
                Assert.That(copy.resizeTextForBestFit ? generated.fontSizeUsedForBestFit : settings.fontSize, Is.GreaterThanOrEqualTo(14), copy.name);
            }
        }

        static void AssertInside(RectTransform parent, RectTransform child)
        {
            var bounds = BattleHudThemeEvidence.Bounds(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 1), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 1), child.name);
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void MissionSelectionWriteFailureShowsLocalizedReasonWithoutChangingTheMission(int width, int height)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                Size(new Vector2Int(width, height));
                Assert.That(game.StartBattle(), Is.True); FinishCampaignVictory();
                game.OpenCampaign(); Press("Campaign page notebook-1-page-2");
                string selected = game.Run.SelectedMission.Id;
                var before = fixture.SavedBytes;
                fixture.Store.RefuseWrites = true;
                EventSystem.current.SetSelectedGameObject(Action("Open mission").gameObject);
                Press("Open mission");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Campaign));
                Assert.That(game.Run.SelectedMission.Id, Is.EqualTo(selected));
                Assert.That(fixture.SavedBytes, Is.EqualTo(before));
                string expected = GameText.Resolve(game.Run.Workshop.SaveMessage);
                var message = root.GetComponentsInChildren<Text>().SingleOrDefault(text => text.name == "Context help text");
                Assert.That(message, Is.Not.Null, "A rejected selection must show a visible reason on the campaign screen.");
                Assert.That(message.text, Is.EqualTo(expected));
                Assert.That(message.text, Does.Not.Contain("copy.0563e105e83c1577"), "Resource messages must be resolved before display.");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Action("Close context help").gameObject));
                Press("Close context help");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Action("Open mission").gameObject));
            }
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void SuccessfulRewardRetryRefreshesProgressAndFocusWithoutPayingTwice(int width, int height)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                Size(new Vector2Int(width, height));
                Assert.That(game.StartBattle(), Is.True);
                fixture.Store.RefuseWrites = true; FinishCampaignVictory();
                Assert.That(root.GetComponentsInChildren<Button>().Any(button => button.name == "Result inspect module"), Is.False);
                var progress = root.GetComponentsInChildren<Text>().Single(text => text.name == "Result progress");
                string before = progress.text;
                Assert.That(Action("Result next mission").IsInteractable(), Is.False);
                var retry = Action("Result retry save");
                EventSystem.current.SetSelectedGameObject(retry.gameObject);
                fixture.Store.RefuseWrites = false; retry.onClick.Invoke(); Canvas.ForceUpdateCanvases();
                Assert.That(game.Run.IsCurrentMissionRewardSaved, Is.True);
                Assert.That(Action("Result inspect module").IsInteractable(), Is.True);
                Assert.That(game.Run.HasPendingRunRewards, Is.False);
                string expected = GameText.Resolve(UserText.Message("assembly.player.progress", game.Run.Workshop.Level, game.Run.Workshop.TotalXp));
                Assert.That(progress.text, Is.EqualTo(expected));
                Assert.That(progress.text, Is.Not.EqualTo(before));
                Assert.That(retry.gameObject.activeSelf, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Action("Result next mission").gameObject));
                int gold = game.Run.Workshop.Gold, xp = game.Run.Workshop.TotalXp;
                Assert.That(game.Run.RetrySave(out _), Is.True);
                Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold));
                Assert.That(game.Run.Workshop.TotalXp, Is.EqualTo(xp));
            }
        }
    }
}
#endif
