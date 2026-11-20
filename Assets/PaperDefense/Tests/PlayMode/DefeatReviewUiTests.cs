#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PaperDefense.Tests
{
    public sealed class DefeatReviewUiTests : BattleHudThemeFixture
    {
        GameSession Session => (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
        Button Action(string name) => root.GetComponentsInChildren<Button>().SingleOrDefault(button => button.name == name);
        Text Copy(string name) => root.GetComponentsInChildren<Text>().SingleOrDefault(text => text.name == name);
        void Press(string name)
        {
            var button = Action(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }
        void Finish(RunPhase phase)
        {
            typeof(GameSession).GetProperty("Phase").SetValue(Session, phase);
            game.Run.StepSimulation(); game.Navigate(PlayerPage.Result); Canvas.ForceUpdateCanvases();
        }

        void RecordCastleLoss(float damage, int wave, Point2? source = null)
        {
            typeof(GameSession).GetProperty("WaveIndex").SetValue(Session, wave);
            var method = typeof(GameSession).GetMethod("DamageObjective", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.GetParameters().Length, Is.EqualTo(3), "Castle losses must retain an optional attack origin.");
            method.Invoke(Session, new object[] { -1, damage, source });
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void ReviewExplainsObservedWaveAndAttackLocationWithoutChangingTheRun(int width, int height)
        {
            Size(new Vector2Int(width, height));
            StartBattle(); var runId = Session.RunId;
            var source = new Point2(Session.BaseCell.X - 5.5f, Session.BaseCell.Y + .5f);
            Session.Statistics.RecordPositionActive(999, new Cell(3, 3), "core.gun", 106);
            RecordCastleLoss(30, 1, source); RecordCastleLoss(220, 2, source); Finish(RunPhase.Defeat);
            int gold = game.Run.Workshop.Gold;
            Press("Result defeat review");
            Assert.That(Copy("Defeat review wave").text, Does.Contain("2"));
            Assert.That(Copy("Defeat review wave").text, Does.Contain("220"));
            Assert.That(Copy("Defeat review approach").text, Does.Contain("250"));
            Assert.That(Copy("Defeat review advice").text, Is.Not.Empty);
            var advice = Copy("Defeat review advice");
            Assert.That(advice.preferredHeight, Is.LessThanOrEqualTo(advice.rectTransform.rect.height + 1));
            var canvas = root.GetComponentsInChildren<RectTransform>().First(item => item.name == "Player pages");
            var adviceBounds = BattleHudThemeEvidence.Bounds(canvas, advice.rectTransform);
            Assert.That(adviceBounds.yMin, Is.GreaterThanOrEqualTo(canvas.rect.yMin - 1));
            Assert.That(adviceBounds.yMax, Is.LessThanOrEqualTo(canvas.rect.yMax + 1));
            foreach (var mask in advice.GetComponentsInParent<RectMask2D>())
            {
                var bounds = BattleHudThemeEvidence.Bounds(mask.rectTransform, advice.rectTransform);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(mask.rectTransform.rect.yMin - 1), "Advice must be visible without scrolling.");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(mask.rectTransform.rect.yMax + 1));
            }
            var legend = root.GetComponentsInChildren<Text>().Single(text => text.text.Contains("Жёлтое кольцо"));
            Assert.That(legend.preferredHeight, Is.LessThanOrEqualTo(legend.rectTransform.rect.height + 1));
            Assert.That(Session.RunId, Is.EqualTo(runId));
            Assert.That(Session.Statistics.BaseDamageTaken, Is.EqualTo(250));
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold));
        }

        [Test]
        public void PartialSourceDataDoesNotClaimThatTheKnownPointIsTheGlobalMaximum()
        {
            StartBattle();
            RecordCastleLoss(10, 1, new Point2(20, 20)); RecordCastleLoss(240, 1);
            Finish(RunPhase.Defeat); Press("Result defeat review");
            var text = root.GetComponentsInChildren<Text>().Select(item => item.text).ToArray();
            Assert.That(text, Does.Contain("Отмеченная точка атак"));
            Assert.That(text, Does.Not.Contain("Точка наибольшего урона"));
            Assert.That(Copy("Defeat review approach").text, Does.Contain("240"));
        }

        [Test]
        public void ResizePreservesOpenReviewAndEscapeRestoresResultControls()
        {
            Size(new Vector2Int(1600, 900)); StartBattle(); Finish(RunPhase.Defeat);
            var runId = Session.RunId;
            EventSystem.current.SetSelectedGameObject(Action("Result defeat review").gameObject);
            Press("Result defeat review"); Size(new Vector2Int(800, 600));
            Assert.That(Action("Defeat review close"), Is.Not.Null);
            Assert.That(Session.RunId, Is.EqualTo(runId));
            Assert.That(WorkshopModal.TryCloseTop(root.transform), Is.True);
            Assert.That(Action("Defeat review close"), Is.Null);
            Assert.That(Action("Result defeat review").IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Action("Result defeat review").gameObject));
        }

        [Test]
        public void PrepareKeepsTheSelectedMissionAndLoadout()
        {
            StartBattle(); string mission = game.Run.ActiveMission.Id;
            var loadout = game.Run.Workshop.LoadoutEntries.Select(item => item?.Id).ToArray();
            Finish(RunPhase.Defeat); Press("Result defeat review"); Press("Defeat review prepare");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(game.Run.SelectedMission.Id, Is.EqualTo(mission));
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(item => item?.Id), Is.EqualTo(loadout));
            Assert.That(Action("Defeat review close"), Is.Null);
        }

        [Test]
        public void RetryStartsANewAttemptWithoutPreviousLossesOrStaleReview()
        {
            StartBattle(); var runId = Session.RunId;
            RecordCastleLoss(250, 1); Finish(RunPhase.Defeat);
            Press("Result defeat review"); Press("Defeat review retry");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(Session.RunId, Is.Not.EqualTo(runId));
            Assert.That(Session.Statistics.BaseDamageTaken, Is.Zero);
            Assert.That(Action("Defeat review close"), Is.Null);
            Finish(RunPhase.Defeat);
            Assert.That(Action("Defeat review close"), Is.Null, "A different attempt must not automatically reopen the old report.");
        }

        [Test]
        public void ViewingReviewDoesNotWriteProfileOrReplaceRewardRetryControls()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home); StartBattle();
                typeof(GameSession).GetProperty("CompletedWaves").SetValue(Session, 1);
                fixture.Store.RefuseWrites = true; Finish(RunPhase.Defeat);
                var retrySave = Action("Result retry save");
                Assert.That(retrySave.gameObject.activeInHierarchy, Is.True);
                int writes = fixture.Store.SaveCalls;
                var bytes = fixture.SavedBytes;
                Press("Result defeat review"); Press("Defeat review close");
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                Assert.That(Action("Result retry save"), Is.SameAs(retrySave));
                Assert.That(retrySave.IsInteractable(), Is.True);
            }
        }

        [Test]
        public void VictoryDoesNotOfferDefeatReview()
        {
            StartBattle(); Finish(RunPhase.Victory);
            Assert.That(Action("Result defeat review"), Is.Null);
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void DefeatOffersAnHonestReviewAndKeepsPrimaryActionsVisible(int width, int height)
        {
            Size(new Vector2Int(width, height)); StartBattle(); Finish(RunPhase.Defeat);
            Press("Result defeat review");
            Assert.That(Copy("Defeat review wave"), Is.Not.Null);
            Assert.That(Copy("Defeat review approach"), Is.Not.Null);
            Assert.That(Copy("Defeat review positions"), Is.Not.Null);
            Assert.That(Copy("Defeat review advice"), Is.Not.Null);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Result));
            foreach (string name in new[] { "Defeat review close", "Defeat review prepare", "Defeat review retry" })
            {
                var button = Action(name);
                Assert.That(button, Is.Not.Null, name);
                Assert.That(button.IsInteractable(), Is.True, name);
                var canvas = root.GetComponentsInChildren<RectTransform>().First(item => item.name == "Player pages");
                var bounds = BattleHudThemeEvidence.Bounds(canvas, (RectTransform)button.transform);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(canvas.rect.xMin - .5f), name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(canvas.rect.xMax + .5f), name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(canvas.rect.yMin - .5f), name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(canvas.rect.yMax + .5f), name);
            }
            Assert.That(Copy("Defeat review wave").text, Does.Contain("Недостаточно данных"));
            Assert.That(Copy("Defeat review approach").text, Does.Contain("не зафиксированы"));
            foreach (string name in new[] { "Defeat review wave", "Defeat review approach", "Defeat review positions", "Defeat review advice" })
                Assert.That(Copy(name).preferredHeight, Is.LessThanOrEqualTo(Copy(name).rectTransform.rect.height + 1), name);
            Press("Defeat review close");
            Assert.That(Action("Result defeat review").IsInteractable(), Is.True);
            Assert.That(Action("Result retry").IsInteractable(), Is.True);
        }
    }
}
#endif
