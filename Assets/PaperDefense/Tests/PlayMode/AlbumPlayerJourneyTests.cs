#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AlbumPlayerJourneyTests
    {
        AssemblyCommandFixture fixture;
        GameObject root;
        PlayerGameRoot game;
        PlayerShell shell;

        [UnitySetUp] public IEnumerator SetUp()
        {
            fixture = new AssemblyCommandFixture();
            var rules = fixture.Preset.rules;
            rules.PlanningSeconds = rules.IntermissionSeconds = 10000;
            rules.PassiveInkPerSecond = rules.EarlyLaunchInkPerSecond = 0;
            rules.Progression.UpgradeEveryWaves = 2;
            rules.Waves = Enumerable.Range(0, 8).Select(i => new WaveDefinition { Count = i == 5 ? 1 : 0, InkGrant = 0 }).ToList();
            root = new GameObject("Isolated album player journey"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(fixture.BalanceAsset, true);
            root.SetActive(true); game.Run.AutomaticSimulation = false; game.Run.InitializeWorkshop(fixture.Store);
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            game.Navigate(PlayerPage.Home); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); yield return null; fixture.Dispose(); }
        Button Button(string name) => root.GetComponentsInChildren<Button>().Single(b => b.name == name);
        Text Text(string name) => root.GetComponentsInChildren<Text>().Single(t => t.name == name);
        void Click(string name) => Button(name).onClick.Invoke();
        void Submit(string name)
        {
            var button = Button(name); EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
        IEnumerator CompleteWave()
        {
            game.Run.NextWave(); game.Run.AdvanceFrame(1f / 30, false, 1); yield return null;
            if (game.Run.Session.PendingUpgrade)
            {
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Upgrades));
                Click("Choose upgrade 0"); yield return null;
            }
        }
        IEnumerator CompleteFive()
        { for (int i = 0; i < 5; i++) yield return CompleteWave(); }
        void KillObjective()
        {
            var session = typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            typeof(GameSession).GetMethod("DamageObjective", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { -1, 100000f });
            game.Run.AdvanceFrame(1f / 30, false, 1);
        }

        [UnityTest] public IEnumerator NewProfileCompletesTheAlbumPlayerJourneyAndReloadsOneVictoryReceipt()
        {
            // Removing Result home, modern reward totals, or the return chain breaks the player journey.
            fixture.Preset.rules.Waves[5].Count = 0;
            Click("Open campaign"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Click("Prepare mission"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Click("Edit album"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Workshop));
            Click("Composite return"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Click("Open preparation album"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));
            Click("Album close"); Click("Start battle"); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle)); var session = game.Run.Session; var runId = session.RunId;
            Click("Battle pause"); Click("Pause settings"); yield return null;
            Assert.That(game.Run.Paused, Is.True); Click("Open help"); Click("Close help");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings)); Click("Close settings"); Click("Resume");
            Assert.That(game.Run.Session, Is.SameAs(session)); Assert.That(game.Run.Paused, Is.False);
            // The configured empty waves still publish through the production reward service.
            for (int i = 0; i < 8; i++) yield return CompleteWave();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Result));
            Assert.That(session.Statistics.Upgrades.Count, Is.EqualTo(3));
            Assert.That(Text("Result reward totals").text, Does.Contain("94").And.Contain("120"));
            Assert.That(Text("Result reward status").text, Does.Contain("сохранена"));
            Assert.That(Text("Result progress").text, Does.Contain("2").And.Contain("120"));
            Click("Result home"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(94)); Assert.That(game.Run.Workshop.TotalXp, Is.EqualTo(120));
            Assert.That(fixture.SavedData.Rewards.Count(r => r.RunId == runId.ToString("N")), Is.EqualTo(1));
            Assert.That(fixture.SavedData.Rewards.Single().VictoryBonus, Is.True);
        }

        [UnityTest] public IEnumerator DefeatInSixthShowsOnlyFiveCompletedWaveRewardsAndReturnsToTheAlbum()
        {
            Assert.That(game.StartBattle(), Is.True); yield return CompleteFive();
            game.Run.NextWave(); KillObjective(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Result)); Assert.That(game.Run.Session.Phase, Is.EqualTo(RunPhase.Defeat));
            Assert.That(Text("Result completed waves").text, Does.Contain("5").And.Contain("8"));
            Assert.That(Text("Result reward totals").text, Does.Contain("40").And.Contain("50"));
            Assert.That(Text("Result defeat wave").text, Does.Contain("6"));
            Click("Result album"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Album));
            Assert.That(game.Run.InWorkshop, Is.True); Assert.That(game.Run.Workshop.Gold, Is.EqualTo(40));
            Assert.That(game.Run.Workshop.TotalXp, Is.EqualTo(50)); Assert.That(fixture.SavedData.Rewards.Single().VictoryBonus, Is.False);
        }

        [UnityTest] public IEnumerator DefeatShowsActualLossesAndKeepsTruncatedHistoryHonest()
        {
            Assert.That(game.StartBattle(), Is.True); game.Run.NextWave();
            var actual = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            foreach (var kind in new[] { StructureKind.Gun, StructureKind.Mortar, StructureKind.Wall, StructureKind.RepairPost })
                actual.Statistics.RecordLoss(new Cell(1, 1), 1, 0, kind, MaterialTier.Pencil, 10);
            KillObjective(); yield return null;
            Assert.That(Text("Result lost towers").text, Does.EndWith("2"));
            Assert.That(Text("Result lost supports").text, Does.EndWith("2"));
            Assert.That(Text("Result reward totals").text.Split('\n'), Has.Length.EqualTo(2));
            var rubble = root.GetComponentsInChildren<Image>().Single(image => image.name == "Defeat castle rubble illustration");
            Assert.That(rubble.sprite, Is.Not.Null); Assert.That(rubble.sprite.name, Is.EqualTo("DefeatCastleRubble"));
            for (int i = 0; i < 35; i++)
                actual.Statistics.RecordLoss(new Cell(1, 1), i + 2, 0, StructureKind.Wall, MaterialTier.Pencil, 2);
            shell.Show(PlayerPage.Result);
            Assert.That(Text("Result total losses").text, Does.EndWith("39"));
            Assert.That(Text("Result lost investment").text, Does.EndWith("110"));
            Assert.That(root.GetComponentsInChildren<Text>().Any(text => text.name == "Result lost towers"), Is.False,
                "The last32 breaches cannot supply a complete tower/support split after39 losses.");
        }

        [UnityTest] public IEnumerator CancelledExitKeepsTheSameAttemptAndRestoresPauseKeyboardFocus()
        {
            Assert.That(game.StartBattle(), Is.True); yield return CompleteFive();
            var session = game.Run.Session; long tick = session.TickIndex; int ink = session.Ink;
            game.Pause(); Submit("Leave battle");
            Assert.That(shell.HasConfirmation, Is.True); Assert.That(game.Run.InWorkshop, Is.False);
            var scope = root.GetComponentsInChildren<WorkshopModal>().Single(m => m.name == "Confirmation shield");
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(scope.transform), Is.True);
            Assert.That(Button("Resume").IsInteractable(), Is.False);
            Assert.That(string.Join(" ", scope.GetComponentsInChildren<Text>().Select(t => t.text)), Does.Contain("завершённые волны"));
            Submit("Cancel confirmation"); Assert.That(shell.HasConfirmation, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("Leave battle").gameObject));
            Submit("Resume"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.Session, Is.SameAs(session)); Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.Run.Session.Ink, Is.EqualTo(ink)); yield return null;
        }

        [UnityTest] public IEnumerator ConfirmedExitDuringSixthRetainsFiveWaveReceiptAndTheEightAssignedPages()
        {
            string[] loadout = game.Run.Workshop.LoadoutEntries.Select(e => e?.Id).ToArray();
            Assert.That(game.StartBattle(), Is.True); yield return CompleteFive(); game.Run.NextWave(); game.Pause();
            Submit("Leave battle"); Submit("Confirm action"); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map)); Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(40)); Assert.That(game.Run.Workshop.TotalXp, Is.EqualTo(50));
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(e => e?.Id), Is.EqualTo(loadout));
            var receipt = fixture.SavedData.Rewards.Single(); Assert.That(receipt.Waves, Is.EqualTo(5)); Assert.That(receipt.VictoryBonus, Is.False);
        }

        [UnityTest] public IEnumerator PendingResultCanRetrySavingWithoutDuplicatingRewardsOrRebuildingTheAttempt()
        {
            fixture.Preset.rules.Waves[5].Count = 0;
            Assert.That(game.StartBattle(), Is.True);
            fixture.Store.RefuseWrites = true;
            for (int i = 0; i < 8; i++) yield return CompleteWave();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Result)); Assert.That(game.Run.HasPendingRunRewards, Is.True);
            Assert.That(Text("Result reward totals").text, Does.Contain("94").And.Contain("120"));
            Assert.That(Text("Result reward status").text, Does.Not.Contain("Награда сохранена"));
            Assert.That(Button("Result retry save").IsInteractable(), Is.True); var session = game.Run.Session;
            int writes = fixture.Store.SaveCalls;
            shell.Show(PlayerPage.Result); shell.ApplyLayout(1024, 768); shell.ApplyLayout(1920, 1080);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(game.Run.Workshop.Gold, Is.Zero);
            fixture.Store.RefuseWrites = false; Click("Result retry save");
            Assert.That(game.Run.HasPendingRunRewards, Is.False); Assert.That(game.Run.Session, Is.SameAs(session));
            Assert.That(Text("Result reward status").text, Does.Contain("сохранена"));
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(94)); Assert.That(game.Run.Workshop.TotalXp, Is.EqualTo(120));
            int savedWrites = fixture.Store.SaveCalls; shell.Show(PlayerPage.Result); shell.Show(PlayerPage.Result);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(savedWrites));
            Assert.That(fixture.SavedData.Rewards.Length, Is.EqualTo(1)); Click("Result home");
            game.Run.InitializeWorkshop(fixture.Store); Assert.That(game.Run.Workshop.Gold, Is.EqualTo(94));
        }

        [UnityTest] public IEnumerator ModernHelpDescribesComponentsEightSlotsAndSeparateActions()
        {
            game.OpenSettings(true); yield return null;
            string copy = string.Join(" ", root.GetComponentsInChildren<Text>().Select(t => t.text));
            Assert.That(copy, Does.Contain("компонент").And.Contain("8").And.Contain("1–8").And.Contain("Q").And.Contain("C").And.Contain("E"));
            Assert.That(copy, Does.Contain("Материал").And.Contain("Цвет").And.Contain("Снос"));
            Assert.That(copy, Does.Not.Contain("четыре чертежа"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("Close help").gameObject));
            Click("Close help"); Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
        }

        [TestCase(1024, 768)] [TestCase(1280, 720)] [TestCase(1920, 1080)]
        public void PauseAndUpgradesKeepTheirActionsVisibleAndKeyboardFocusInsideTheSheet(int width, int height)
        {
            Assert.That(game.StartBattle(), Is.True); game.Pause(); shell.ApplyLayout(width, height); Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "Resume", "Pause settings", "Restart battle", "Leave battle" })
                AssertInsideActivePage(Button(name).GetComponent<RectTransform>(), PlayerPage.Pause);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("Resume").gameObject));
            game.Resume(); game.Run.NextWave(); game.Run.AdvanceFrame(1f / 30, false, 1);
            game.Run.NextWave(); game.Run.AdvanceFrame(1f / 30, false, 1); game.Navigate(PlayerPage.Upgrades); Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 3; i++) AssertInsideActivePage(Button("Choose upgrade " + i).GetComponent<RectTransform>(), PlayerPage.Upgrades);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("Choose upgrade 0").gameObject));
        }

        void AssertInsideActivePage(RectTransform control, PlayerPage destination)
        {
            var page = root.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Page " + destination);
            var corners = new Vector3[4]; control.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = page.InverseTransformPoint(corner);
                Assert.That(point.x, Is.InRange(page.rect.xMin - 1, page.rect.xMax + 1), control.name);
                Assert.That(point.y, Is.InRange(page.rect.yMin - 1, page.rect.yMax + 1), control.name);
            }
            foreach (var scroll in control.GetComponentsInParent<ScrollRect>())
            {
                var viewport = scroll.viewport;
                foreach (var corner in corners)
                {
                    var point = viewport.InverseTransformPoint(corner);
                    Assert.That(point.y, Is.InRange(viewport.rect.yMin - 1, viewport.rect.yMax + 1), control.name + " must not require scrolling");
                }
            }
        }
    }
}
#endif
