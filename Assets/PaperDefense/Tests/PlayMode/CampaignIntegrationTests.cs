#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class CampaignIntegrationTests
    {
        static GameSession Session(RunController run)=>(GameSession)typeof(RunController).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(run);
        static void Finish(RunController run,float health=1)
        {
            var session=Session(run);
            typeof(GameSession).GetProperty("CompletedWaves").SetValue(session,run.ActiveMission.WaveCount);
            typeof(GameSession).GetProperty("BaseHealth").SetValue(session,session.Balance.BaseHealth*health);
            typeof(GameSession).GetProperty("Phase").SetValue(session,RunPhase.Victory);
            run.StepSimulation();
        }

        [Test]
        public void CampaignRewardPollingDoesNotCloneTheConfigurationEverySimulationTick()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                fixture.BalanceAsset.campaignEnabled = true;
                fixture.Restart();
                Assert.That(fixture.Run.StartRun(out _), Is.True);
                // Bind once so the measured production polling path includes no reflection allocations.
                var method = typeof(RunController).GetMethod("DepositMissionReward", BindingFlags.Instance | BindingFlags.NonPublic);
                var poll = (Action)Delegate.CreateDelegate(typeof(Action), fixture.Run, method);
                for (int i = 0; i < 32; i++) poll();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 256; i++) poll();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.LessThan(4096), "Unchanged reward polling must not allocate full rules/map snapshots per tick.");
            }
        }
        [Test] public void LockedPreviewIsReadOnlyAndSelectionChangesTheActualStartedMapAndWaves()
        {
            using(var fixture=new AssemblyCommandFixture())
            {
                fixture.BalanceAsset.campaignEnabled=true;fixture.Restart();
                int writes=fixture.Store.SaveCalls;string state=fixture.State();
                var preview=fixture.Run.CreateMissionPreview("notebook-3-page-3");
                Assert.That(preview.Balance.Waves.Count,Is.EqualTo(10));
                Assert.That(fixture.Run.SelectedMission.Id,Is.EqualTo("notebook-1-page-1"));
                Assert.That(fixture.Run.SelectMission("notebook-3-page-3",out _),Is.False);
                Assert.That(fixture.State(),Is.EqualTo(state));Assert.That(fixture.Store.SaveCalls,Is.EqualTo(writes));
                Assert.That(fixture.Run.StartRun(out _),Is.True);
                Assert.That(fixture.Run.Session.Balance.Waves.Count,Is.EqualTo(4));Finish(fixture.Run);
                Assert.That(fixture.Run.MissionResult.FirstClear,Is.True);Assert.That(fixture.Run.IsCurrentMissionRewardSaved,Is.True);
                fixture.Run.OpenWorkshop();Assert.That(fixture.Run.SelectMission("notebook-1-page-2",out _),Is.True);
                Assert.That(fixture.Run.Session.Balance.Waves.Count,Is.EqualTo(5));
                Assert.That(fixture.Run.StartRun(out _),Is.True);
                Assert.That(fixture.Run.ActiveMission.Id,Is.EqualTo("notebook-1-page-2"));
                Assert.That(fixture.Run.Session.Balance.Waves.Count,Is.EqualTo(5));
            }
        }
        [Test] public void FailedVictoryCommitCannotPublishStarsUnlocksOrFirstClearMoneyAndRetryPaysOnce()
        {
            using(var fixture=new AssemblyCommandFixture())
            {
                fixture.BalanceAsset.campaignEnabled=true;fixture.Restart();
                Assert.That(fixture.Run.StartRun(out _),Is.True);
                var id=fixture.Run.Session.RunId;int gold=fixture.Run.Workshop.Gold;
                fixture.Store.RefuseWrites=true;Finish(fixture.Run,.75f);
                Assert.That(fixture.Run.HasPendingRunRewards,Is.True);Assert.That(fixture.Run.IsCurrentMissionRewardSaved,Is.False);
                Assert.That(fixture.Run.Workshop.Gold,Is.EqualTo(gold));
                Assert.That(fixture.Run.Campaign.Missions[0].BestStars,Is.Zero);Assert.That(fixture.Run.IsMissionUnlocked("notebook-1-page-2"),Is.False);
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"),Is.False);
                Assert.That(fixture.Run.MissionResult.FirstClear,Is.True);Assert.That(fixture.Run.MissionResult.Stars,Is.EqualTo(2));
                fixture.Store.RefuseWrites=false;Assert.That(fixture.Run.RetryPendingRewards(out _),Is.True);
                int paid=fixture.Run.Workshop.Gold;
                Assert.That(fixture.Run.MissionResult.FirstClear,Is.True);Assert.That(fixture.Run.IsCurrentMissionRewardSaved,Is.True);
                Assert.That(fixture.Run.Campaign.Missions[0].BestStars,Is.EqualTo(2));
                Assert.That(fixture.Run.IsMissionUnlocked("notebook-1-page-2"),Is.True);Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"),Is.True);
                Assert.That(fixture.Run.RunRewards.ApplyMission(fixture.Run.ActiveMission,id,4,true,2).Success,Is.True);
                Assert.That(fixture.Run.Workshop.Gold,Is.EqualTo(paid));
                fixture.Restart();Assert.That(fixture.Run.Campaign.Missions[0].BestStars,Is.EqualTo(2));
                Assert.That(fixture.Run.Workshop.Gold,Is.EqualTo(paid));
                Assert.That(fixture.Run.StartRun(out _),Is.True);Assert.That(fixture.Run.Session.RunId,Is.Not.EqualTo(id));Finish(fixture.Run);
                Assert.That(fixture.Run.MissionResult.FirstClear,Is.False);Assert.That(fixture.Run.MissionResult.Stars,Is.EqualTo(3));
                Assert.That(fixture.SavedData.CampaignRewards.Count(r=>r.FirstClear),Is.EqualTo(1));
            }
        }
        [Test] public void VersionEightMigrationAndFutureCampaignProtectionUseTheAtomicProfileStore()
        {
            using(var fixture=new AssemblyCommandFixture())
            {
                var old=fixture.SavedData;old.Version=8;old.Campaign=null;old.CampaignRewards=null;
                string json=JsonUtility.ToJson(old);System.IO.File.WriteAllText(fixture.ProfilePath,json);
                fixture.Restart();Assert.That(fixture.Run.SelectMission("notebook-1-page-1",out _),Is.False,
                    "The isolated classic balance does not silently enable campaign mode.");
                Assert.That(fixture.Commands.PurchasePart("head.long",Guid.NewGuid()).Success,Is.False,
                    "An unaffordable transaction does not force migration.");
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(),1,false).Success,Is.True);
                Assert.That(System.IO.File.ReadAllText(fixture.ProfilePath+".v8-original"),Is.EqualTo(json));
                var saved=fixture.SavedData;saved.Campaign.Version=99;
                string future=JsonUtility.ToJson(saved);System.IO.File.WriteAllText(fixture.ProfilePath,future);
                fixture.Restart();Assert.That(fixture.Run.Workshop.SaveState,Is.EqualTo(ProfileSaveState.RecoveryRequired));
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(),1,false).Success,Is.False);
                Assert.That(System.IO.File.ReadAllText(fixture.ProfilePath),Is.EqualTo(future));
            }
        }
    }
}
#endif
