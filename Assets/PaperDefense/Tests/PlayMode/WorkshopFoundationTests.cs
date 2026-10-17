#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class WorkshopFoundationTests
    {
        GameObject root, events;
        RunController run;
        WorkshopWizard wizard;
        Store store;

        void Open(bool allChassisOwned = false)
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(balance, Is.Not.Null, "Exercise the main game's resolved catalog, including preset overrides.");
            var prepared = balance.CreateWorkshopBalance();
            var profile = new WorkshopProfile(prepared.ResolveContent(), prepared);
            profile.GrantGold(1000);
            if (allChassisOwned)
                foreach (var chassis in prepared.ResolveContent().Chassis.Where(part => part.Enabled))
                    Assert.That(profile.Unlock(chassis.Id, out var reason, prepared), Is.True, reason);

            root = new GameObject("Isolated foundation screen", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 1000);
            events = new GameObject("Isolated foundation events", typeof(EventSystem));
            var host = new GameObject("Isolated foundation controller");
            host.transform.SetParent(root.transform, false); host.SetActive(false);
            run = host.AddComponent<RunController>(); run.balanceAsset = balance;
            store = new Store(profile); run.InitializeWorkshop(store);
            wizard = new WorkshopWizard(root.transform, run, null, _ => { }, () => { });
            Canvas.ForceUpdateCanvases();
        }

        [TearDown] public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (events != null) Object.DestroyImmediate(events);
        }

        Button Button(string name) => root.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == name && button.gameObject.activeInHierarchy);
        Text Text(string name) => root.GetComponentsInChildren<Text>(true)
            .Single(label => label.name == name && label.gameObject.activeInHierarchy);
        Image Preview()
        {
            var preview = root.GetComponentsInChildren<Image>(true)
                .SingleOrDefault(image => image.name == "Foundation preview illustration" && image.gameObject.activeInHierarchy);
            Assert.That(preview, Is.Not.Null, "The foundation step must display a dedicated illustration of its selected chassis.");
            return preview;
        }
        string AlbumState() => string.Join("|", run.Workshop.Album.Select(page => page.Id + ":" + page.Name)) +
            ";" + string.Join("|", run.Workshop.Loadout.Select(page => page?.Id));

        void AssertSelectedFoundation(string id)
        {
            var chassis = run.Content.GetChassis(id);
            Assert.That(wizard.Draft.ChassisId, Is.EqualTo(id));
            Assert.That(Preview().sprite, Is.Not.Null, id);
            Assert.That(Preview().sprite.name, Is.EqualTo(id), "Show the chosen foundation without its weapon assembly.");
            Assert.That(Text("Foundation mount value").text, Is.EqualTo(chassis.MountCount.ToString()), id);
            Assert.That(Text("Foundation capacity value").text, Is.EqualTo(chassis.Capacity.ToString()), id);
            Assert.That(Text("Foundation body description").text, Is.EqualTo(GameText.Resolve(chassis.Description)), id);
        }

        [Test] public void EveryEnabledFoundationUsesLiveCatalogValuesAndSelectionDoesNotSaveOrSpendGold()
        {
            Open(allChassisOwned: true);
            var enabled = run.Content.Chassis.Where(part => part.Enabled).ToArray();
            var cards = root.GetComponentsInChildren<RectTransform>(true)
                .Where(card => card.name.StartsWith("Wizard part chassis.") && card.gameObject.activeInHierarchy)
                .Select(card => card.name.Substring("Wizard part ".Length)).ToArray();
            Assert.That(cards, Is.EquivalentTo(enabled.Select(part => part.Id)), "Only enabled resolved entries belong in the foundation catalog.");
            int gold = run.Workshop.Gold, writes = store.Writes;
            string draftId = wizard.Draft.Id, album = AlbumState();
            foreach (var chassis in enabled)
            {
                Button("Wizard choose " + chassis.Id).onClick.Invoke();
                AssertSelectedFoundation(chassis.Id);
                Assert.That(wizard.Step, Is.Zero);
                Assert.That(wizard.Draft.Id, Is.EqualTo(draftId));
                Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
                Assert.That(store.Writes, Is.EqualTo(writes));
                Assert.That(AlbumState(), Is.EqualTo(album));
            }
        }

        [Test] public void PurchaseCancellationPreservesSelectionAndSuccessfulUnlockRequiresSeparateChoice()
        {
            Open();
            var locked = run.Content.Chassis.First(part => part.Enabled && !run.Workshop.IsUnlocked(part.Id));
            int price = run.Workshop.Parts.Single(part => part.Id == locked.Id).GoldCost;
            int gold = run.Workshop.Gold, writes = store.Writes;
            var draft = wizard.Draft;
            string album = AlbumState();
            Button("Wizard unlock " + locked.Id).onClick.Invoke();
            Assert.That(Button("Wizard next").IsInteractable(), Is.False, "The purchase confirmation must block the workshop behind it.");
            Button("Workshop cancel confirmation").onClick.Invoke();
            Assert.That(run.Workshop.IsUnlocked(locked.Id), Is.False);
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(wizard.Draft, Is.SameAs(draft));
            Assert.That(Button("Wizard next").IsInteractable(), Is.True);

            Button("Wizard unlock " + locked.Id).onClick.Invoke();
            Button("Workshop confirm action").onClick.Invoke();
            Assert.That(run.Workshop.IsUnlocked(locked.Id), Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold - price));
            Assert.That(store.Writes, Is.EqualTo(writes + 1));
            Assert.That(store.Saved.Unlocked, Does.Contain(locked.Id));
            Assert.That(wizard.Draft, Is.SameAs(draft), "A permanent unlock must not silently change the current assembly.");
            Assert.That(AlbumState(), Is.EqualTo(album));
            AssertSelectedFoundation(draft.ChassisId);
            Button("Wizard choose " + locked.Id).onClick.Invoke();
            AssertSelectedFoundation(locked.Id);
            Assert.That(store.Writes, Is.EqualTo(writes + 1), "Selecting the now-owned foundation must not write again.");
        }

        [Test] public void FailedUnlockCommitKeepsTheFoundationLockedAndTheDraftAvailable()
        {
            Open();
            var locked = run.Content.Chassis.First(part => part.Enabled && !run.Workshop.IsUnlocked(part.Id));
            int gold = run.Workshop.Gold, writes = store.Writes;
            var draft = wizard.Draft;
            string album = AlbumState();
            store.FailWrites = true;
            Button("Wizard unlock " + locked.Id).onClick.Invoke();
            Button("Workshop confirm action").onClick.Invoke();
            Assert.That(store.Writes, Is.EqualTo(writes + 1));
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.WriteFailed));
            Assert.That(run.Workshop.IsUnlocked(locked.Id), Is.False);
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(wizard.Draft, Is.SameAs(draft));
            Assert.That(wizard.Step, Is.Zero);
            Assert.That(AlbumState(), Is.EqualTo(album));
            Assert.That(Button("Wizard unlock " + locked.Id).IsInteractable(), Is.True, "The failed purchase remains available for an explicit retry.");
            Assert.That(wizard.Status, Does.Contain(Store.WriteError));
            AssertSelectedFoundation(draft.ChassisId);
        }

        sealed class Store : IWorkshopProfileStore
        {
            public const string WriteError = "Injected foundation write failure";
            readonly WorkshopProfile loaded;
            public bool FailWrites;
            public int Writes;
            public WorkshopProfileData Saved;
            public Store(WorkshopProfile loaded) { this.loaded = loaded; }
            public ProfileLoadResult Load() => new ProfileLoadResult(loaded, ProfileLoadSource.Primary, "Isolated foundation profile", true);
            public ProfileSaveResult Save(WorkshopProfile value)
            {
                Writes++;
                if (FailWrites) return new ProfileSaveResult(false, WriteError);
                Saved = value.ToData();
                return new ProfileSaveResult(true, "Saved isolated foundation profile");
            }
        }
    }
}
#endif
