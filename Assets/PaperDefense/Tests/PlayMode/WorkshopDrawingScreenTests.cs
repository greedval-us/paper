#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class WorkshopDrawingScreenTests
    {
        GameObject root, events;
        RunController run;
        WorkshopWizard wizard;
        WorkshopResponsiveLayout responsive;
        Store store;

        void Open(bool threeMounts = false)
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(balance, Is.Not.Null, "Exercise the main game's catalog and workshop rules.");
            var prepared = balance.CreateWorkshopBalance();
            var profile = new WorkshopProfile(prepared.ResolveContent(), prepared);
            profile.GrantGold(1000);
            if (threeMounts)
                Assert.That(profile.Unlock("chassis.battery", out var reason, prepared), Is.True, reason);

            root = new GameObject("Isolated standard review player"); root.SetActive(false);
            events = new GameObject("Isolated standard review events", typeof(EventSystem));
            var game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true);
            run = game.Run; run.AutomaticSimulation = false;
            store = new Store(profile); run.InitializeWorkshop(store);
            game.Navigate(PlayerPage.Workshop);
            wizard = root.GetComponentInChildren<WorkshopHud>(true).Guided;
            wizard.NewDraft();
            if (threeMounts) Assert.That(wizard.SelectChassis("chassis.battery"), Is.True);
            Assert.That(wizard.Next(), Is.True, wizard.Status);
            if (threeMounts)
            {
                Assert.That(wizard.SelectWeapon(1, "weapon.gun"), Is.True);
                Assert.That(wizard.SelectWeapon(2, "weapon.gun"), Is.True);
            }
            Assert.That(wizard.Next(), Is.True, wizard.Status);
            Assert.That(wizard.Step, Is.EqualTo(2));
            responsive = root.GetComponentInChildren<WorkshopResponsiveLayout>(true);
            Assert.That(responsive, Is.Not.Null);
            // Keep the requested pixel dimensions while a test yields to focus and Canvas updates.
            responsive.enabled = false;
            Apply(1600, 1000);
            AssertStandard();
        }

        [TearDown] public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (events != null) Object.DestroyImmediate(events);
        }

        Button Button(string name)
        {
            var button = root.GetComponentsInChildren<Button>(true)
                .SingleOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
            Assert.That(button, Is.Not.Null, "The standard review must retain its action: " + name);
            return button;
        }

        void Click(string name)
        {
            var button = Button(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        RectTransform Rect(string name) => root.GetComponentsInChildren<RectTransform>(true)
            .Single(value => value.name == name && value.gameObject.activeInHierarchy);
        ScrollRect Scroll => root.GetComponentsInChildren<ScrollRect>(true)
            .Single(value => value.name == "Workshop responsive scroll");
        static string Snapshot(TowerBlueprint page) => JsonUtility.ToJson(new BlueprintData(page));
        string AlbumState() => string.Join("|", run.Workshop.Album.Select(page => page.Id + ":" + page.Name)) +
            ";" + string.Join("|", run.Workshop.Loadout.Select(page => page?.Id));

        void Apply(int width, int height)
        {
            responsive.Apply(width, height);
            Canvas.ForceUpdateCanvases();
        }

        void AssertStandard()
        {
            Assert.That(wizard.Draft.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(wizard.Draft.Drawing.IsEmpty, Is.True);
            Assert.That(wizard.Draft.FreeformDesign, Is.Null);
            Assert.That(wizard.Drawing, Is.Null, "The standard workshop must not create a drawing editor.");
            Assert.That(root.GetComponentsInChildren<RectTransform>(true).Any(value => value.name == "Guided drawing"), Is.False);
        }

        static void AssertInside(RectTransform parent, RectTransform child)
        {
            Assert.That(child.gameObject.activeInHierarchy, Is.True, child.name);
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = parent.InverseTransformPoint(corner);
                Assert.That(point.x, Is.InRange(parent.rect.xMin - 1, parent.rect.xMax + 1), child.name + " horizontal visibility");
                Assert.That(point.y, Is.InRange(parent.rect.yMin - 1, parent.rect.yMax + 1), child.name + " vertical visibility");
            }
        }

        void AssertVisible(RectTransform child)
        {
            var canvas = child.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
            AssertInside(canvas, child);
        }

        void AssertReviewAndNavigationVisible()
        {
            Assert.That(wizard.Step, Is.EqualTo(2));
            Assert.That(Scroll.gameObject.activeInHierarchy, Is.True);
            Assert.That(Rect("Wizard review").IsChildOf(Scroll.content), Is.True);
            AssertInside(Scroll.viewport, Rect("Review heading"));
            AssertVisible(Rect("Review heading"));
            foreach (string name in new[] { "Wizard back", "Wizard next", "Wizard step 0", "Wizard step 1", "Wizard step 2" })
            {
                var button = Button(name);
                Assert.That(button.IsInteractable(), Is.True, name);
                AssertVisible((RectTransform)button.transform);
            }
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(value => value.name == "Wizard step 3"), Is.False);
        }

        [UnityTest] public IEnumerator WideReviewAndThreeStepNavigationRemainVisibleWithoutSavingOrSpending()
            => ReviewAndNavigationRemainVisible(1600, 1000);

        [UnityTest] public IEnumerator CompactReviewAndThreeStepNavigationRemainVisibleWithoutSavingOrSpending()
            => ReviewAndNavigationRemainVisible(1024, 768);

        IEnumerator ReviewAndNavigationRemainVisible(int width, int height)
        {
            Open();
            string draft = Snapshot(wizard.Draft), album = AlbumState();
            int gold = run.Workshop.Gold, writes = store.Writes;
            Apply(width, height); yield return null; Apply(width, height);
            AssertReviewAndNavigationVisible(); AssertStandard();
            Assert.That(Snapshot(wizard.Draft), Is.EqualTo(draft));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(AlbumState(), Is.EqualTo(album));
        }

        [UnityTest] public IEnumerator CompactReviewNameFocusRevealsTheFieldAfterScrollingToTheEnd()
        {
            Open(); Apply(1024, 768); yield return null; Apply(1024, 768);
            var field = Rect("Wizard name").GetComponent<InputField>();
            string draft = Snapshot(wizard.Draft);
            int writes = store.Writes;
            Scroll.verticalNormalizedPosition = 0;
            EventSystem.current.SetSelectedGameObject(field.gameObject);
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(field.gameObject));
            AssertInside(Scroll.viewport, field.GetComponent<RectTransform>());
            AssertVisible(field.GetComponent<RectTransform>());
            Assert.That(Snapshot(wizard.Draft), Is.EqualTo(draft));
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [Test] public void BackAndReturnToReviewPreservesTheStandardAssemblyWithoutSaving()
        {
            Open();
            string draft = Snapshot(wizard.Draft), album = AlbumState();
            int gold = run.Workshop.Gold, writes = store.Writes;
            wizard.Back(); Assert.That(wizard.Step, Is.EqualTo(1));
            Assert.That(wizard.Next(), Is.True, wizard.Status);
            Apply(1024, 768); AssertReviewAndNavigationVisible(); AssertStandard();
            Assert.That(Snapshot(wizard.Draft), Is.EqualTo(draft));
            Assert.That(AlbumState(), Is.EqualTo(album));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [UnityTest] public IEnumerator ThreeMountTrialUsesTheStandardAssemblyAndReturnsToReview()
        {
            Open(threeMounts: true); Apply(1024, 768);
            string draft = Snapshot(wizard.Draft), album = AlbumState();
            int gold = run.Workshop.Gold, writes = store.Writes;
            EventSystem.current.SetSelectedGameObject(Button("Wizard full trial").gameObject);
            var recording = wizard.RunTrial();
            Assert.That(recording, Is.Not.Null);
            Assert.That(recording.First.Frames, Is.Not.Empty);
            var towers = recording.First.Frames.First().Towers;
            Assert.That(towers, Is.Not.Empty);
            foreach (var tower in towers)
            {
                Assert.That(tower.Blueprint.WeaponMounts.Select(mount => mount.SlotIndex), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(Snapshot(tower.Blueprint), Is.EqualTo(draft));
            }
            yield return null;
            Click("Wizard close trial"); Apply(1024, 768);
            Assert.That(wizard.Step, Is.EqualTo(2)); AssertStandard();
            Assert.That(Snapshot(wizard.Draft), Is.EqualTo(draft));
            Assert.That(AlbumState(), Is.EqualTo(album));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Button("Wizard full trial").gameObject));
        }

        [Test] public void SaveFromReviewWritesExactlyTheNamedStandardAssembly()
        {
            Open();
            var field = Rect("Wizard name").GetComponent<InputField>();
            field.text = "Стандартная башня"; field.onEndEdit.Invoke(field.text);
            string draft = Snapshot(wizard.Draft);
            int gold = run.Workshop.Gold, writes = store.Writes;
            Click("Wizard next");
            Assert.That(wizard.Step, Is.EqualTo(2)); AssertStandard();
            Assert.That(store.Writes, Is.EqualTo(writes + 1));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            var saved = run.Workshop.Album.Single(page => page.Id == wizard.Draft.Id);
            Assert.That(Snapshot(saved), Is.EqualTo(draft));
            Assert.That(JsonUtility.ToJson(store.Saved.Album.Single(page => page.Id == saved.Id)), Is.EqualTo(draft));
        }

        [UnityTest] public IEnumerator ReviewRefreshAndResizePreserveTheStandardDtoAndThreeStepNavigation()
        {
            Open(threeMounts: true);
            string draft = Snapshot(wizard.Draft), status = wizard.Status;
            int writes = store.Writes, gold = run.Workshop.Gold;
            foreach (var size in new[] { new Vector2Int(1024, 768), new Vector2Int(1920, 1080), new Vector2Int(1024, 768) })
            {
                Apply(size.x, size.y); wizard.Refresh();
                yield return null; Apply(size.x, size.y);
                AssertReviewAndNavigationVisible(); AssertStandard();
                Assert.That(Snapshot(wizard.Draft), Is.EqualTo(draft));
                Assert.That(wizard.Status, Is.EqualTo(status));
            }
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
        }

        sealed class Store : IWorkshopProfileStore
        {
            readonly WorkshopProfile loaded;
            public int Writes;
            public WorkshopProfileData Saved;
            public Store(WorkshopProfile profile) { loaded = profile; }
            public ProfileLoadResult Load() => new ProfileLoadResult(loaded, ProfileLoadSource.Primary, "Isolated standard review profile", true);
            public ProfileSaveResult Save(WorkshopProfile profile)
            {
                Writes++; Saved = profile.ToData();
                return new ProfileSaveResult(true, "Saved isolated standard review profile");
            }
        }
    }
}
#endif
