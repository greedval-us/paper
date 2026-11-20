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
    public sealed class MissionContinuationUiTests : BattleHudThemeFixture
    {
        T Control<T>(string name) where T : Component
        {
            var result = root.GetComponentsInChildren<T>().SingleOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, "Missing mission-continuation control: " + name);
            return result;
        }

        void Press(string name)
        {
            var button = Control<Button>(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            button.onClick.Invoke(); Settle();
        }

        void Settle() { UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases(); }

        bool HasCheckpoint()
        {
            var property = typeof(RunController).GetProperty("HasMissionCheckpoint");
            Assert.That(property, Is.Not.Null, "RunController must expose the saved mission without writing it.");
            return (bool)property.GetValue(game.Run);
        }

        void Start(AssemblyCommandFixture fixture, int width = 800, int height = 600)
        {
            game.Run.InitializeWorkshop(fixture.Store);
            game.Navigate(PlayerPage.Preparation); Size(new Vector2Int(width, height));
            Assert.That(game.StartBattle(), Is.True, game.Status); Settle();
        }

        void CompleteFirstWave()
        {
            game.Run.NextWave();
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            // UI coverage needs a real intermission transition, without waiting for the combat fixture.
            typeof(GameSession).GetProperty(nameof(GameSession.RemainingToSpawn)).SetValue(session, 0);
            game.Run.StepSimulation();
            Assert.That(game.Run.Session.Phase, Is.EqualTo(RunPhase.Intermission));
            Assert.That(game.Run.Session.CompletedWaves, Is.EqualTo(1));
            game.Pause(); Settle();
        }

        static void Inside(RectTransform child, RectTransform parent)
        {
            var bounds = BattleHudThemeEvidence.Bounds(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 1), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 1), child.name);
        }

        void Readable(Text copy)
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

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void IntermissionSaveAndHomeContinuationFitAndResumeTheSameAttemptPaused(int width, int height)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture, width, height); CompleteFirstWave();
                Guid runId = game.Run.Session.RunId;
                var paper = Control<RectTransform>("Pause paper");
                foreach (string name in new[] { "Resume", "Pause save and exit", "Pause settings", "Restart battle", "Leave battle", "Pause checkpoint status" })
                    Inside(Control<RectTransform>(name), paper);
                Press("Pause save and exit");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Home)); Assert.That(game.Run.InWorkshop, Is.True);
                Assert.That(HasCheckpoint(), Is.True);
                var page = Control<RectTransform>("Page Home");
                foreach (string name in new[] { "Home continue mission", "Home checkpoint summary", "Home discard mission", "Open campaign", "exit" })
                    Inside(Control<RectTransform>(name), page);
                Assert.That(Control<Text>("Home checkpoint summary").text, Does.Contain("1 / " + game.Run.SelectedMission.WaveCount));
                Readable(Control<Text>("Home checkpoint summary"));
                Press("Home continue mission");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
                Assert.That(game.Run.Session.RunId, Is.EqualTo(runId));
                Assert.That(game.Run.Session.CompletedWaves, Is.EqualTo(1));
                Assert.That(game.Run.TacticalPaused, Is.True);
                Assert.That(game.Run.InWorkshop, Is.False);
            }
        }

        [Test]
        public void AssaultCannotSaveAndExplainsTheIntermissionRequirement()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); game.Run.NextWave(); game.Pause(); Settle();
                Assert.That(game.Run.Session.Phase, Is.EqualTo(RunPhase.Assault));
                Assert.That(Control<Button>("Pause save and exit").IsInteractable(), Is.False);
                Assert.That(Control<Text>("Pause checkpoint status").text, Is.Not.Empty);
                var session = game.Run.Session; int writes = fixture.Store.SaveCalls;
                var action = typeof(PlayerGameRoot).GetMethod("SaveAndExitMission");
                Assert.That(action, Is.Not.Null, "The action must recheck eligibility even if a stale callback is invoked.");
                action.Invoke(game, null);
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause)); Assert.That(game.Run.Session, Is.SameAs(session));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(HasCheckpoint(), Is.False);
            }
        }

        [Test]
        public void FailedSaveKeepsThePausedAttemptAndVisibleErrorAcrossResize()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); CompleteFirstWave(); var session = game.Run.Session;
                long tick = session.TickIndex; fixture.Store.RefuseWrites = true;
                Press("Pause save and exit");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause)); Assert.That(game.Run.Session, Is.SameAs(session));
                Assert.That(game.Run.Paused, Is.True); Assert.That(HasCheckpoint(), Is.False);
                string error = Control<Text>("Pause checkpoint status").text; Assert.That(error, Is.Not.Empty);
                Readable(Control<Text>("Pause checkpoint status"));
                Assert.That(Control<WorkshopTooltip>("Pause checkpoint status").Hint,
                    Is.EqualTo(GameText.Resolve(game.Run.Workshop.SaveMessage)), "Retain the complete localized storage reason even when its visible summary is shorter.");
                int writes = fixture.Store.SaveCalls;
                Size(new Vector2Int(1600, 900)); Size(new Vector2Int(800, 600)); Settle();
                Assert.That(Control<Text>("Pause checkpoint status").text, Is.EqualTo(error));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
                Assert.That(Control<Button>("Pause save and exit").IsInteractable(), Is.True);
                fixture.Store.RefuseWrites = false; Press("Pause save and exit");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Home)); Assert.That(HasCheckpoint(), Is.True);
            }
        }

        [Test]
        public void ViewingMenusAndCancellingANewBattlePreserveTheSavedAttemptWithoutWrites()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); CompleteFirstWave(); Press("Pause save and exit");
                var before = fixture.SavedBytes; int writes = fixture.Store.SaveCalls;
                Size(new Vector2Int(1600, 900)); Size(new Vector2Int(800, 600));
                game.OpenAlbum(); game.CloseAlbum(); game.OpenCampaign();
                game.Navigate(PlayerPage.Preparation); Settle();
                Assert.That(game.StartBattle(), Is.False, "Starting a different attempt must wait for explicit replacement confirmation.");
                Assert.That(shell.HasConfirmation, Is.True); Assert.That(HasCheckpoint(), Is.True);
                Press("Cancel confirmation");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation)); Assert.That(game.Run.InWorkshop, Is.True);
                Assert.That(HasCheckpoint(), Is.True); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.SavedBytes, Is.EqualTo(before));
            }
        }

        [Test]
        public void ConfirmedReplacementKeepsTheSavedMissionWhenTheWriteFails()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); CompleteFirstWave(); Guid previous = game.Run.Session.RunId;
                Press("Pause save and exit"); game.Navigate(PlayerPage.Preparation); Settle();
                fixture.Store.RefuseWrites = true;
                Assert.That(game.StartBattle(), Is.False); Press("Confirm action");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation)); Assert.That(game.Run.InWorkshop, Is.True);
                Assert.That(HasCheckpoint(), Is.True); Assert.That(game.Status, Is.Not.Empty);
                Assert.That(root.GetComponentsInChildren<Text>().Any(text => text.text == game.Status), Is.True,
                    "The replacement failure must be visible on preparation.");
                fixture.Store.RefuseWrites = false;
                Assert.That(game.StartBattle(), Is.False); Press("Confirm action");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle)); Assert.That(HasCheckpoint(), Is.False);
                Assert.That(game.Run.Session.RunId, Is.Not.EqualTo(previous));
                Assert.That(game.Run.Session.Phase, Is.EqualTo(RunPhase.Planning));
            }
        }

        [Test]
        public void DiscardRequiresConfirmationAndKeepsTheHomeErrorUntilRetrySucceeds()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Start(fixture); CompleteFirstWave(); Press("Pause save and exit");
                Press("Home discard mission"); Press("Cancel confirmation"); Assert.That(HasCheckpoint(), Is.True);
                fixture.Store.RefuseWrites = true; Press("Home discard mission"); Press("Confirm action");
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Home)); Assert.That(HasCheckpoint(), Is.True);
                string error = Control<Text>("Home checkpoint status").text; Assert.That(error, Is.Not.Empty);
                Readable(Control<Text>("Home checkpoint status"));
                Assert.That(Control<WorkshopTooltip>("Home checkpoint status").Hint,
                    Is.EqualTo(GameText.Resolve(game.Run.Workshop.SaveMessage)));
                int writes = fixture.Store.SaveCalls;
                Size(new Vector2Int(1600, 900)); Size(new Vector2Int(800, 600)); Settle();
                Assert.That(Control<Text>("Home checkpoint status").text, Is.EqualTo(error));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                fixture.Store.RefuseWrites = false; Press("Home discard mission"); Press("Confirm action");
                Assert.That(HasCheckpoint(), Is.False); Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
                Assert.That(root.GetComponentsInChildren<Button>().Any(button => button.name == "Home continue mission"), Is.False);
                Assert.That(Control<Button>("Open campaign").IsInteractable(), Is.True);
            }
        }
    }
}
#endif
