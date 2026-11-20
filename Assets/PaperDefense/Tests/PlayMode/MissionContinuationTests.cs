#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class MissionContinuationTests
    {
        static bool Call(RunController run, string name, out string reason)
        {
            var method = typeof(RunController).GetMethod(name);
            Assert.That(method, Is.Not.Null, "Missing mission continuation API: " + name);
            var arguments = new object[] { null };
            bool result = (bool)method.Invoke(run, arguments); reason = (string)arguments[0]; return result;
        }
        static T Value<T>(object owner, string name)
        {
            var property = owner.GetType().GetProperty(name); Assert.That(property, Is.Not.Null, "Missing continuation property: " + name);
            return (T)property.GetValue(owner);
        }
        static bool Has(RunController run) => Value<bool>(run, "HasMissionCheckpoint");
        static GameSession Session(RunController run) => (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
        static void SetSession(RunController run, string property, object value) => typeof(GameSession).GetProperty(property).SetValue(Session(run), value);
        static void StartIntermission(AssemblyCommandFixture fixture)
        {
            // This fixture verifies the transaction boundary; omit enemy spawns but use a real completed-wave step.
            Assert.That(typeof(RunController).GetMethod("TrySuspendMission"), Is.Not.Null, "Missing suspend transaction.");
            fixture.BalanceAsset.campaignEnabled = true; fixture.Restart();
            Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
            fixture.Run.NextWave(); SetSession(fixture.Run, "RemainingToSpawn", 0);
            fixture.Store.RefuseWrites = true; fixture.Run.StepSimulation(); fixture.Store.RefuseWrites = false;
            Assert.That(fixture.Run.Session.Phase, Is.EqualTo(RunPhase.Intermission));
            Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
            SetSession(fixture.Run, "BaseHealth", fixture.Run.Session.Balance.BaseHealth * .78f);
            SetSession(fixture.Run, "Ink", 123);
        }
        static void Suspend(AssemblyCommandFixture fixture)
        { Assert.That(Call(fixture.Run, "TrySuspendMission", out var reason), Is.True, reason); }
        static string[] Frozen(RunController run) => run.RunLoadout.Entries.Select(entry => entry?.Id).ToArray();

        [Test] public void SaveExitCommitsCheckpointAndUnpaidWaveRewardTogetherThenResumeConsumesBeforePublication()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Guid id = fixture.Run.Session.RunId; string[] slots = Frozen(fixture.Run);
                float health = fixture.Run.Session.BaseHealth; int writes = fixture.Store.SaveCalls;
                Assert.That(Call(fixture.Run, "CanSuspendMission", out _), Is.True); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Suspend(fixture);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 1), "Checkpoint and cumulative reward share one commit.");
                Assert.That(fixture.Run.InWorkshop, Is.True); Assert.That(Has(fixture.Run), Is.True);
                var checkpoint = Value<object>(fixture.Run, "MissionCheckpoint");
                Assert.That(Value<Guid>(checkpoint, "RunId"), Is.EqualTo(id));
                Assert.That(fixture.SavedData.CampaignRewards.Single(receipt => receipt.RunId == id.ToString("N")).CompletedWaves, Is.EqualTo(1));
                int gold = fixture.Run.Workshop.Gold, xp = fixture.Run.Workshop.TotalXp;
                Assert.That(gold, Is.GreaterThan(0)); Assert.That(xp, Is.GreaterThan(0));
                fixture.Restart();
                for (int slot = 0; slot < 8; slot++) Assert.That(fixture.Commands.AssignSlot(slot, null, Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Commands.DeleteEntry(slots[0], Guid.NewGuid()).Success, Is.True,
                    "A saved attempt owns its pages even when the album page is deleted later.");
                writes = fixture.Store.SaveCalls; Guid preview = fixture.Run.Session.RunId;
                Assert.That(Call(fixture.Run, "CheckMissionCheckpoint", out _), Is.True);
                Assert.That(fixture.Run.Session.RunId, Is.EqualTo(preview)); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                int historicalEvents = 0, publications = 0; bool consumedBeforePublication = true;
                fixture.Run.SimulationEventReceived += item => historicalEvents++;
                fixture.Run.SessionChanged += session => { publications++; consumedBeforePublication &= !Has(fixture.Run); };
                Assert.That(Call(fixture.Run, "TryResumeMission", out var reason), Is.True, reason);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 1)); Assert.That(publications, Is.EqualTo(1));
                Assert.That(consumedBeforePublication, Is.True, "Consume is durable before resumed state is visible.");
                Assert.That(Has(fixture.Run), Is.False); Assert.That(fixture.Run.InWorkshop, Is.False); Assert.That(fixture.Run.TacticalPaused, Is.True);
                Assert.That(fixture.Run.Session.RunId, Is.EqualTo(id)); Assert.That(fixture.Run.Session.CompletedWaves, Is.EqualTo(1));
                Assert.That(fixture.Run.Session.BaseHealth, Is.EqualTo(health)); Assert.That(fixture.Run.Session.Ink, Is.EqualTo(123));
                CollectionAssert.AreEqual(slots, Frozen(fixture.Run)); Assert.That(fixture.Run.ActiveMission.Id, Is.EqualTo("notebook-1-page-1"));
                Assert.That(fixture.Run.RunRewards.ApplyMission(fixture.Run.ActiveMission, id, 1, false, 0).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold)); Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(xp));
                Assert.That(fixture.Run.AdvanceFrame(1, false, 1), Is.Zero); Assert.That(historicalEvents, Is.Zero);
                Assert.That(Call(fixture.Run, "TryResumeMission", out _), Is.False); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 1));
            }
        }

        [Test] public void FailedSaveExitKeepsTheLiveAttemptAndRetryPaysTheWaveOnlyOnce()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Guid id = fixture.Run.Session.RunId; byte[] bytes = fixture.SavedBytes;
                int gold = fixture.Run.Workshop.Gold, writes = fixture.Store.SaveCalls;
                fixture.Store.RefuseWrites = true;
                Assert.That(Call(fixture.Run, "TrySuspendMission", out _), Is.False);
                Assert.That(fixture.Run.InWorkshop, Is.False); Assert.That(fixture.Run.Session.RunId, Is.EqualTo(id));
                Assert.That(Has(fixture.Run), Is.False); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold)); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 1));
                fixture.Store.RefuseWrites = false; Suspend(fixture);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 2));
                Assert.That(fixture.SavedData.CampaignRewards.Count(receipt => receipt.RunId == id.ToString("N")), Is.EqualTo(1));
            }
        }

        [Test] public void FailedConsumeKeepsCheckpointAndPreviewUntilTheSameResumeCanCommit()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Guid id = fixture.Run.Session.RunId; Suspend(fixture); fixture.Restart();
                Guid preview = fixture.Run.Session.RunId; byte[] bytes = fixture.SavedBytes; int publications = 0;
                fixture.Run.SessionChanged += session => publications++;
                fixture.Store.RefuseWrites = true;
                Assert.That(Call(fixture.Run, "TryResumeMission", out _), Is.False);
                Assert.That(Has(fixture.Run), Is.True); Assert.That(fixture.Run.InWorkshop, Is.True);
                Assert.That(fixture.Run.Session.RunId, Is.EqualTo(preview)); Assert.That(publications, Is.Zero);
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                fixture.Store.RefuseWrites = false;
                Assert.That(Call(fixture.Run, "TryResumeMission", out var reason), Is.True, reason);
                Assert.That(fixture.Run.Session.RunId, Is.EqualTo(id)); Assert.That(publications, Is.EqualTo(1));
            }
        }

        [Test] public void ReplacementPreparesAValidRunBeforeDiscardAndDoesNotPublishOnWriteFailure()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Guid saved = fixture.Run.Session.RunId; string attack = Frozen(fixture.Run)[0]; Suspend(fixture);
                Assert.That(fixture.Run.CanStartAlbumRun(out _), Is.True);
                Assert.That(fixture.Run.StartRun(out _), Is.False, "Ordinary start cannot silently discard a saved attempt.");
                for (int slot = 0; slot < 8; slot++) Assert.That(fixture.Commands.AssignSlot(slot, null, Guid.NewGuid()).Success, Is.True);
                int writes = fixture.Store.SaveCalls; byte[] bytes = fixture.SavedBytes;
                Assert.That(Call(fixture.Run, "StartRunReplacingCheckpoint", out _), Is.False);
                Assert.That(Has(fixture.Run), Is.True); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                Assert.That(fixture.Commands.AssignSlot(7, attack, Guid.NewGuid()).Success, Is.True);
                fixture.Store.RefuseWrites = true; Guid preview = fixture.Run.Session.RunId;
                Assert.That(Call(fixture.Run, "StartRunReplacingCheckpoint", out _), Is.False);
                Assert.That(Has(fixture.Run), Is.True); Assert.That(fixture.Run.Session.RunId, Is.EqualTo(preview));
                fixture.Store.RefuseWrites = false;
                Assert.That(Call(fixture.Run, "StartRunReplacingCheckpoint", out var reason), Is.True, reason);
                Assert.That(Has(fixture.Run), Is.False); Assert.That(fixture.Run.InWorkshop, Is.False);
                Assert.That(fixture.Run.Session.RunId, Is.Not.EqualTo(saved)); Assert.That(fixture.Run.Session.Phase, Is.EqualTo(RunPhase.Planning));
            }
        }

        [Test] public void DiscardIsTransactionalAndNeverRefundsOrRepaysMissionRewards()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Suspend(fixture); byte[] bytes = fixture.SavedBytes; int gold = fixture.Run.Workshop.Gold;
                fixture.Store.RefuseWrites = true; Assert.That(Call(fixture.Run, "TryDiscardMissionCheckpoint", out _), Is.False);
                Assert.That(Has(fixture.Run), Is.True); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                fixture.Store.RefuseWrites = false; Assert.That(Call(fixture.Run, "TryDiscardMissionCheckpoint", out _), Is.True);
                Assert.That(Has(fixture.Run), Is.False); Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold));
                fixture.Restart(); Assert.That(Has(fixture.Run), Is.False);
            }
        }

        [TestCase(RunPhase.Planning)] [TestCase(RunPhase.Assault)] [TestCase(RunPhase.Victory)] [TestCase(RunPhase.Defeat)]
        public void SaveExitRejectsEveryPhaseOutsideIntermissionWithoutWriting(RunPhase phase)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); SetSession(fixture.Run, "Phase", phase);
                int writes = fixture.Store.SaveCalls; Guid id = fixture.Run.Session.RunId;
                Assert.That(Call(fixture.Run, "CanSuspendMission", out _), Is.False);
                Assert.That(Call(fixture.Run, "TrySuspendMission", out _), Is.False);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(fixture.Run.Session.RunId, Is.EqualTo(id));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ConfigurationChangesRejectResumeWithoutConsumingOrBlockingPermanentProfileEdits(bool editWhileActive)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture);
                if (editWhileActive) fixture.Preset.rules.PassiveInkPerSecond += .125f;
                Suspend(fixture);
                if (!editWhileActive) fixture.Preset.rules.PassiveInkPerSecond += .125f;
                fixture.Restart();
                int writes = fixture.Store.SaveCalls; byte[] bytes = fixture.SavedBytes;
                Assert.That(Call(fixture.Run, "CheckMissionCheckpoint", out _), Is.False);
                Assert.That(Call(fixture.Run, "TryResumeMission", out _), Is.False);
                Assert.That(Has(fixture.Run), Is.True); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                Assert.That(fixture.Commands.AssignSlot(7, null, Guid.NewGuid()).Success, Is.True);
                Assert.That(Call(fixture.Run, "TryDiscardMissionCheckpoint", out _), Is.True);
            }
        }

        [Test] public void MalformedBattlePayloadRemainsOpaqueAndDoesNotDamagePermanentProfileEdits()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Suspend(fixture); var data = fixture.SavedData;
                var field = data.GetType().GetField("MissionCheckpoint"); Assert.That(field, Is.Not.Null);
                var envelope = field.GetValue(data); envelope.GetType().GetField("PayloadJson").SetValue(envelope, "{ invalid payload");
                File.WriteAllText(fixture.ProfilePath, JsonUtility.ToJson(data)); fixture.Restart();
                int gold = fixture.Run.Workshop.Gold, writes = fixture.Store.SaveCalls;
                Assert.That(Has(fixture.Run), Is.True); Assert.That(Call(fixture.Run, "CheckMissionCheckpoint", out _), Is.False);
                Assert.That(Call(fixture.Run, "TryResumeMission", out _), Is.False); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.Run.Workshop.SaveState, Is.Not.EqualTo(ProfileSaveState.RecoveryRequired));
                Assert.That(fixture.Commands.AssignSlot(7, null, Guid.NewGuid()).Success, Is.True); fixture.Restart();
                Assert.That(Value<string>(Value<object>(fixture.Run, "MissionCheckpoint"), "PayloadJson"), Is.EqualTo("{ invalid payload"));
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold)); Assert.That(Call(fixture.Run, "TryDiscardMissionCheckpoint", out _), Is.True);
            }
        }

        [Test] public void VersionTenActualFileMigratesOnSaveAndRetainsItsExactHistoricalBytes()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Assert.That(typeof(RunController).GetMethod("TrySuspendMission"), Is.Not.Null);
                var old = fixture.SavedData; old.Version = 10;
                File.WriteAllText(fixture.ProfilePath, JsonUtility.ToJson(old, true)); byte[] bytes = fixture.SavedBytes;
                fixture.Restart(); Assert.That(Has(fixture.Run), Is.False); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                StartIntermission(fixture); Suspend(fixture);
                Assert.That(File.ReadAllBytes(fixture.ProfilePath + ".v10-original"), Is.EqualTo(bytes));
                fixture.Restart(); Assert.That(Has(fixture.Run), Is.True); Assert.That(fixture.SavedData.Version, Is.EqualTo(11));
            }
        }

        [Test] public void SaveAndConsumeObserversCannotReenterContinuationOrPublishAnotherAttempt()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Guid runId = fixture.Run.Session.RunId;
                bool observe = true, reentered = false; int callbacks = 0;
                fixture.Run.WorkshopChanged += () =>
                {
                    if (!observe) return;
                    callbacks++;
                    reentered |= Call(fixture.Run, "TrySuspendMission", out _);
                    reentered |= Call(fixture.Run, "TryResumeMission", out _);
                    reentered |= Call(fixture.Run, "TryDiscardMissionCheckpoint", out _);
                    reentered |= Call(fixture.Run, "StartRunReplacingCheckpoint", out _);
                    reentered |= fixture.Run.StartRun(out _);
                };
                Suspend(fixture); Assert.That(callbacks, Is.GreaterThan(0)); callbacks = 0;
                Assert.That(Call(fixture.Run, "TryResumeMission", out var reason), Is.True, reason);
                observe = false; Assert.That(callbacks, Is.GreaterThan(0)); Assert.That(fixture.Run.Session.RunId, Is.EqualTo(runId));
                Assert.That(reentered, Is.False);
                Assert.That(Has(fixture.Run), Is.False); Assert.That(fixture.Run.TacticalPaused, Is.True);
            }
        }

        [Test] public void MemoryOnlySaveAndResumeNeverTouchTheInjectedDiskProfile()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Assert.That(typeof(RunController).GetMethod("TrySuspendMission"), Is.Not.Null);
                fixture.BalanceAsset.campaignEnabled = true; fixture.Run.InitializeWorkshop(false);
                Assert.That(fixture.Run.StartRun(out _), Is.True); fixture.Run.NextWave(); SetSession(fixture.Run, "RemainingToSpawn", 0); fixture.Run.StepSimulation();
                int writes = fixture.Store.SaveCalls; byte[] bytes = fixture.SavedBytes; Guid id = fixture.Run.Session.RunId;
                Suspend(fixture); Assert.That(fixture.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
                Assert.That(Call(fixture.Run, "TryResumeMission", out var reason), Is.True, reason);
                Assert.That(fixture.Run.Session.RunId, Is.EqualTo(id)); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
            }
        }

        [TestCase("TrySuspendMission")] [TestCase("TryResumeMission")] [TestCase("StartRunReplacingCheckpoint")]
        public void ThrowingProfileObserverCannotLeaveADurablyCommittedContinuationHalfPublished(string operation)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Guid oldRun = fixture.Run.Session.RunId;
                if (operation != "TrySuspendMission") Suspend(fixture);
                int writes = fixture.Store.SaveCalls, remainingObservers = 0;
                fixture.Run.WorkshopChanged += () => throw new InvalidOperationException("Injected continuation observer failure");
                fixture.Run.WorkshopChanged += () => remainingObservers++;
                Assert.That(Call(fixture.Run, operation, out var reason), Is.True, reason);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 1), "A throwing observer does not change a completed write into a rejected transaction.");
                Assert.That(remainingObservers, Is.GreaterThan(0), "Other views must still receive the committed state.");
                if (operation == "TrySuspendMission")
                {
                    Assert.That(Has(fixture.Run), Is.True); Assert.That(fixture.Run.InWorkshop, Is.True);
                    Assert.That(fixture.Run.Session.RunId, Is.Not.EqualTo(oldRun));
                }
                else
                {
                    Assert.That(Has(fixture.Run), Is.False); Assert.That(fixture.Run.InWorkshop, Is.False);
                    if (operation == "TryResumeMission")
                    {
                        Assert.That(fixture.Run.Session.RunId, Is.EqualTo(oldRun)); Assert.That(fixture.Run.TacticalPaused, Is.True);
                        Assert.That(fixture.Run.Session.Phase, Is.EqualTo(RunPhase.Intermission));
                    }
                    else
                    {
                        Assert.That(fixture.Run.Session.RunId, Is.Not.EqualTo(oldRun)); Assert.That(fixture.Run.Session.Phase, Is.EqualTo(RunPhase.Planning));
                    }
                }
            }
        }

        [Test] public void ThrowingSessionObserverCannotHideASuccessfullyConsumedResumeFromOtherViews()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Guid id = fixture.Run.Session.RunId; Suspend(fixture);
                int remainingObservers = 0;
                fixture.Run.SessionChanged += session => throw new InvalidOperationException("Injected resumed-session observer failure");
                fixture.Run.SessionChanged += session => remainingObservers++;
                Assert.That(Call(fixture.Run, "TryResumeMission", out var reason), Is.True, reason);
                Assert.That(remainingObservers, Is.EqualTo(1)); Assert.That(fixture.Run.Session.RunId, Is.EqualTo(id));
                Assert.That(fixture.Run.InWorkshop, Is.False); Assert.That(fixture.Run.TacticalPaused, Is.True); Assert.That(Has(fixture.Run), Is.False);
            }
        }

        [Test] public void InvalidCurrentPreviewConfigurationRejectsSaveExitBeforeAnyCheckpointCommit()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                StartIntermission(fixture); Guid id = fixture.Run.Session.RunId; byte[] bytes = fixture.SavedBytes;
                int writes = fixture.Store.SaveCalls;
                fixture.Preset.rules.PassiveInkPerSecond = float.NaN;
                Assert.That(Call(fixture.Run, "TrySuspendMission", out _), Is.False);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                Assert.That(Has(fixture.Run), Is.False); Assert.That(fixture.Run.InWorkshop, Is.False); Assert.That(fixture.Run.Session.RunId, Is.EqualTo(id));
            }
        }
    }
}
#endif
