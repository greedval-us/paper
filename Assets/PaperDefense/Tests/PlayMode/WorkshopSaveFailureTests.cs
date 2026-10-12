using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class WorkshopSaveFailureTests
    {
        GameObject root;
        GameBalanceAsset balance;
        RunController run;
        FakeStore store;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Isolated workshop save failures"); root.SetActive(false);
            run = root.AddComponent<RunController>();
            var profile = new WorkshopProfile(); profile.GrantGold(200);
            Assert.That(profile.SaveBlueprint(NewBlueprint("custom", "Existing page"), out _), Is.True);
            store = new FakeStore(profile);
            run.InitializeWorkshop(store, allowLegacyFixtureDefaults: true);
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);
            if (balance != null) Object.DestroyImmediate(balance);
        }

        static TowerBlueprint NewBlueprint(string id, string name)
            => new TowerBlueprint(id, name, ChassisKind.Standard, StructureKind.Gun);

        string State() => run.Workshop.Gold + "|" + string.Join(",", run.Workshop.Album.Select(p => p.Id + ":" + p.Name)) +
            "|" + string.Join(",", run.Workshop.Loadout.Select(p => p.Id)) + "|" +
            string.Join(",", run.Workshop.Parts.Where(p => run.Workshop.IsUnlocked(p.Id)).Select(p => p.Id));

        [TestCase("save")]
        [TestCase("delete")]
        [TestCase("assign")]
        [TestCase("unlock")]
        public void FailedManualCommitLeavesGoldAlbumAndSlotsUnchanged(string command)
        {
            string before = State(); int published = 0; run.WorkshopChanged += () => published++;
            store.FailWrites = true;
            bool success; string reason;
            switch (command)
            {
                case "save": success = run.SaveBlueprint(NewBlueprint("draft", "Unsaved draft"), out reason); break;
                case "delete": success = run.DeleteBlueprint("custom", out reason); break;
                case "assign": success = run.AssignBlueprint(0, "custom", out reason); break;
                default: success = run.UnlockPart("module.feed", out reason); break;
            }
            Assert.That(success, Is.False);
            Assert.That(TextResourcesAsset.Default.Resolve(reason), Does.Contain(FakeStore.WriteError));
            Assert.That(store.SaveCalls, Is.EqualTo(1));
            Assert.That(State(), Is.EqualTo(before));
            Assert.That(published, Is.Zero, "A rejected candidate is never published.");
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.WriteFailed));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.False, "Rejected manual changes are not dirty profile data.");
            Assert.That(TextResourcesAsset.Default.Resolve(run.Workshop.LastWriteError), Does.Contain(FakeStore.WriteError));
        }

        [Test] public void RetryWritesCurrentProfileWithoutReplayingRejectedPurchase()
        {
            store.FailWrites = true;
            Assert.That(run.UnlockPart("module.feed", out _), Is.False);
            store.FailWrites = false;
            Assert.That(run.RetrySave(out _), Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(200));
            Assert.That(run.Workshop.IsUnlocked("module.feed"), Is.False);
            Assert.That(store.Saved.Gold, Is.EqualTo(200));
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.Saved));
            Assert.That(run.UnlockPart("module.feed", out _), Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(140));
            Assert.That(run.Workshop.IsUnlocked("module.feed"), Is.True);
        }

        [Test] public void WorkshopKeepsDraftAndDisplaysTheWriteFailure()
        {
            var hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
            var name = root.GetComponentsInChildren<InputField>(true).Single(); name.text = "My crooked draft";
            store.FailWrites = true;
            root.GetComponentsInChildren<Button>(true).Single(b => b.name == "Save blueprint").onClick.Invoke();
            Assert.That(hud.Draft.Name, Is.EqualTo("My crooked draft"));
            Assert.That(run.Workshop.Album[0].Name, Is.Not.EqualTo("My crooked draft"));
            Assert.That(hud.Status, Does.Contain(FakeStore.WriteError));
            Assert.That(root.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains(FakeStore.WriteError)), Is.True);
        }

        [Test] public void ProtectedLoadRejectsRetryTransactionsAndQuitWithoutStoreWrites()
        {
            store = new FakeStore(new WorkshopProfile(), ProfileLoadSource.RecoveryRequired, false);
            run.InitializeWorkshop(store, allowLegacyFixtureDefaults: true);
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.RecoveryRequired));
            Assert.That(run.SaveBlueprint(NewBlueprint("draft", "Protected"), out _), Is.False);
            Assert.That(run.RetrySave(out _), Is.False);
            root.SetActive(true);
            root.SendMessage("OnApplicationQuit", SendMessageOptions.DontRequireReceiver);
            Assert.That(store.SaveCalls, Is.Zero);
        }

        [Test] public void NewProfileIsDirtyAndIsSavedOnQuitWithoutChangingTheRealProfilePath()
        {
            store = new FakeStore(new WorkshopProfile(), ProfileLoadSource.NewProfile);
            run.InitializeWorkshop(store, allowLegacyFixtureDefaults: true);
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.Unsaved));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.True);
            root.SetActive(true);
            root.SendMessage("OnApplicationQuit", SendMessageOptions.DontRequireReceiver);
            Assert.That(store.SaveCalls, Is.EqualTo(1));
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.Saved));
        }

        [Test] public void MemoryOnlyModeAllowsEditsAndClearlyReportsItsScope()
        {
            run.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
            Assert.That(run.SaveBlueprint(NewBlueprint("memory", "Local experiment"), out _), Is.True);
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            Assert.That(run.RetrySave(out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(store.SaveCalls, Is.Zero);
        }

        void SetRewardBalance()
        {
            balance = ScriptableObject.CreateInstance<GameBalanceAsset>();
            balance.passiveInkPerSecond = 0; balance.earlyLaunchInkPerSecond = 0; balance.planningSeconds = 1000;
            balance.baseHealth = 1; balance.workshopStartingInk = 1000; balance.waveGold = 8; balance.victoryGold = 30;
            balance.weapons = new[] { new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun, cost = 40,
                health = 55, buildSeconds = 0, range = 200, damage = 999, shotInterval = .05f } };
            balance.enemies = new[] { new GameBalanceAsset.EnemySettings { kind = EnemyKind.Doodle,
                health = 1, speed = 80, damage = 10, attackInterval = .1f, reward = 0 } };
            balance.waves = new[] { new GameBalanceAsset.WaveSettings { count = 1, sides = 1 }, new GameBalanceAsset.WaveSettings { count = 1, sides = 1 } };
            run.balanceAsset = balance; run.InitializeWorkshop(store);
        }

        void Frame(int count) { for (int i = 0; i < count; i++) run.AdvanceFrame(1f / 30, false, 1); }
        void WinFirstWave()
        {
            Assert.That(run.StartRun(out _), Is.True);
            Assert.That(run.BuildBlueprint(new[] { new Cell(48, 24) }, 0, MaterialTier.Pencil, InkColor.None, out _), Is.True);
            run.NextWave(); Frame(15);
            Assert.That(run.Session.Phase, Is.EqualTo(RunPhase.Intermission));
        }

        [Test] public void FailedRewardWriteKeepsGoldOnceAcrossRetryWorkshopAndNewRun()
        {
            SetRewardBalance(); store.FailWrites = true; WinFirstWave();
            Assert.That(run.Workshop.Gold, Is.EqualTo(208));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.True);
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.WriteFailed));
            Frame(30); Assert.That(run.Workshop.Gold, Is.EqualTo(208));
            Assert.That(run.RetrySave(out _), Is.False);
            Assert.That(run.Workshop.Gold, Is.EqualTo(208));
            run.OpenWorkshop(); Assert.That(run.Workshop.Gold, Is.EqualTo(208));
            Assert.That(run.StartRun(out _), Is.True); Frame(3);
            Assert.That(run.Workshop.Gold, Is.EqualTo(208));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.True);
            store.FailWrites = false; Assert.That(run.RetrySave(out _), Is.True);
            Assert.That(store.Saved.Gold, Is.EqualTo(208));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.False);
            Frame(10); Assert.That(run.Workshop.Gold, Is.EqualTo(208));
        }

        [Test] public void PurchaseCommitsPreviouslyUnsavedRewardAndItsOwnCostExactlyOnce()
        {
            SetRewardBalance(); store.FailWrites = true; WinFirstWave(); run.OpenWorkshop();
            Assert.That(run.UnlockPart("module.feed", out _), Is.False);
            Assert.That(run.Workshop.Gold, Is.EqualTo(208));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.True);
            store.FailWrites = false;
            Assert.That(run.UnlockPart("module.feed", out _), Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(148));
            Assert.That(store.Saved.Gold, Is.EqualTo(148));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.False);
            Assert.That(run.RetrySave(out _), Is.True);
            Assert.That(store.Saved.Gold, Is.EqualTo(148));
        }

        [Test] public void DirtyRewardHudOffersRetryAndDoesNotReplayTheAward()
        {
            SetRewardBalance();
            var hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
            store.FailWrites = true; WinFirstWave(); run.OpenWorkshop();
            var retry = root.GetComponentsInChildren<Button>(true).Single(b => b.name == "Retry profile save");
            Assert.That(retry.gameObject.activeSelf, Is.True);
            var label = root.GetComponentsInChildren<Text>(true).Single(t => t.name == "Profile save status");
            Assert.That(label.text, Does.Contain(FakeStore.WriteError));
            Assert.That(label.text, Does.Contain("памяти"));
            store.FailWrites = false; retry.onClick.Invoke();
            Assert.That(run.Workshop.Gold, Is.EqualTo(208));
            Assert.That(store.Saved.Gold, Is.EqualTo(208));
            Assert.That(retry.gameObject.activeSelf, Is.False);
            Assert.That(label.text, Does.Contain("СОХРАНЕНО"));
        }

        [Test] public void SourceBecomingProtectedDuringSaveKeepsDirtyGoldAndBlocksFurtherWrites()
        {
            SetRewardBalance(); store.FailWrites = true; WinFirstWave(); run.OpenWorkshop();
            store.FailWrites = false; store.ProtectOnSave = true;
            Assert.That(run.UnlockPart("module.feed", out var reason), Is.False);
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.RecoveryRequired));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(208));
            Assert.That(run.Workshop.IsUnlocked("module.feed"), Is.False);
            Assert.That(TextResourcesAsset.Default.Resolve(reason), Does.Contain("Recovery required"));
            int writes = store.SaveCalls;
            Assert.That(run.RetrySave(out _), Is.False);
            root.SetActive(true);
            root.SendMessage("OnApplicationQuit", SendMessageOptions.DontRequireReceiver);
            Assert.That(store.SaveCalls, Is.EqualTo(writes));
        }

        sealed class FakeStore : IWorkshopProfileStore
        {
            public const string WriteError = "Injected disk write failure";
            readonly WorkshopProfile loaded;
            ProfileLoadSource source;
            bool canWrite;
            public bool FailWrites;
            public bool ProtectOnSave;
            public int SaveCalls;
            public WorkshopProfileData Saved;
            public FakeStore(WorkshopProfile profile, ProfileLoadSource source = ProfileLoadSource.Primary, bool canWrite = true)
            { loaded = profile; this.source = source; this.canWrite = canWrite; }
            public ProfileLoadResult Load() => new ProfileLoadResult(loaded, source, canWrite?"Isolated test store":"Recovery required: isolated source protected", canWrite);
            public ProfileSaveResult Save(WorkshopProfile profile)
            {
                SaveCalls++;
                if (ProtectOnSave)
                { canWrite = false; source = ProfileLoadSource.RecoveryRequired; return new ProfileSaveResult(false, "Source became protected"); }
                if (FailWrites) return new ProfileSaveResult(false, WriteError);
                Saved = profile.ToData(); return new ProfileSaveResult(true, "Saved isolated profile");
            }
        }
    }
}
