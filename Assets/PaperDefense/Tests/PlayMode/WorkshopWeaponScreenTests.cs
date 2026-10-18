#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
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
    public sealed class WorkshopWeaponScreenTests
    {
        GameObject root, events;
        RunController run;
        WorkshopWizard wizard;
        Store store;

        void Open(bool allWeaponsOwned = false, bool allChassisOwned = false)
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(balance, Is.Not.Null, "Exercise the main game's resolved catalog, including preset overrides.");
            var prepared = balance.CreateWorkshopBalance();
            var content = prepared.ResolveContent();
            var profile = new WorkshopProfile(content, prepared);
            profile.GrantGold(1000);
            if (allWeaponsOwned)
                foreach (var weapon in content.Weapons.Where(part => part.Enabled))
                    Assert.That(profile.Unlock(weapon.Id, out var weaponReason, prepared), Is.True, weaponReason);
            if (allChassisOwned)
                foreach (var chassis in content.Chassis.Where(part => part.Enabled))
                    Assert.That(profile.Unlock(chassis.Id, out var chassisReason, prepared), Is.True, chassisReason);

            root = new GameObject("Isolated weapon screen", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 1000);
            events = new GameObject("Isolated weapon events", typeof(EventSystem));
            var host = new GameObject("Isolated weapon controller");
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

        Button Button(string name)
        {
            var button = root.GetComponentsInChildren<Button>(true)
                .SingleOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
            Assert.That(button, Is.Not.Null, "The weapon screen must expose the action: " + name);
            return button;
        }

        Text Text(string name)
        {
            var label = root.GetComponentsInChildren<Text>(true)
                .SingleOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
            Assert.That(label, Is.Not.Null, "The weapon screen must expose the live value: " + name);
            return label;
        }

        void AssertWeaponPage()
        {
            var page = root.GetComponentsInChildren<RectTransform>(true)
                .SingleOrDefault(value => value.name == "Wizard weapons" && value.gameObject.activeInHierarchy);
            Assert.That(page, Is.Not.Null, "The weapon step must display its dedicated catalog and assembly composition.");
            Assert.That(wizard.Step, Is.EqualTo(1));
        }

        void AssertCurrentAssemblyPreview()
        {
            Assert.That(wizard.Draft.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard), "Every weapon or module choice must retain standard construction rules.");
            Assert.That(wizard.Draft.Drawing.IsEmpty, Is.True);
            Assert.That(wizard.Draft.FreeformDesign, Is.Null);
            var preview = root.GetComponentsInChildren<Graphic>(true)
                .SingleOrDefault(value => value.name == "Weapon assembly illustration" &&
                    value.GetType().Name == "WeaponAssemblyGraphic" && value.gameObject.activeInHierarchy);
            Assert.That(preview, Is.Not.Null, "The weapon step must display the current assembled draft.");
            var blueprint = preview.GetType().GetProperty("Blueprint", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(blueprint, Is.Not.Null, "The assembly illustration must expose the draft it renders.");
            Assert.That(blueprint.GetValue(preview), Is.SameAs(wizard.Draft), "The assembly illustration must refresh after every weapon choice.");
            var stats = run.PreviewBlueprint(wizard.Draft);
            Assert.That(Text("Weapon assembly load").text, Is.EqualTo(stats.Load + " / " + stats.Capacity));
        }

        string AlbumState() => string.Join("|", run.Workshop.Album.Select(page => page.Id + ":" + page.Name)) +
            ";" + string.Join("|", run.Workshop.Loadout.Select(page => page?.Id));

        [Test] public void EveryEnabledWeaponChangesOnlyTheSelectedMountWithoutSavingOrSpendingGold()
        {
            Open(allWeaponsOwned: true);
            Assert.That(wizard.Next(), Is.True);
            AssertWeaponPage();
            var enabled = run.Content.Weapons.Where(part => part.Enabled).ToArray();
            var cards = root.GetComponentsInChildren<RectTransform>(true)
                .Where(card => card.name.StartsWith("Wizard part weapon.") && card.gameObject.activeInHierarchy)
                .Select(card => card.name.Substring("Wizard part ".Length)).ToArray();
            Assert.That(cards, Is.EquivalentTo(enabled.Select(part => part.Id)));
            Assert.That(cards, Does.Contain("weapon.blotter"), "Support weapons remain selectable alongside combat weapons.");
            Button("Wizard mount 1").onClick.Invoke();
            Assert.That(Text("Weapon catalog heading").text, Does.Contain("2"));
            int gold = run.Workshop.Gold, writes = store.Writes;
            string draftId = wizard.Draft.Id, first = wizard.Draft.GetWeaponId(0), chassis = wizard.Draft.ChassisId;
            string album = AlbumState();
            var modules = wizard.Draft.ModuleSelections.Select(module => module.InstanceId + ":" + module.DefinitionId).ToArray();
            foreach (var weapon in enabled)
            {
                Button("Wizard choose " + weapon.Id).onClick.Invoke();
                Assert.That(wizard.Draft.GetWeaponId(1), Is.EqualTo(weapon.Id));
                Assert.That(wizard.Draft.GetWeaponId(0), Is.EqualTo(first));
                Assert.That(wizard.Draft.GetWeaponId(2), Is.Null);
                Assert.That(wizard.Draft.ChassisId, Is.EqualTo(chassis));
                Assert.That(wizard.Draft.ModuleSelections.Select(module => module.InstanceId + ":" + module.DefinitionId), Is.EqualTo(modules));
                Assert.That(wizard.Draft.Id, Is.EqualTo(draftId));
                Assert.That(wizard.Step, Is.EqualTo(1));
                Assert.That(Text("Weapon catalog heading").text, Does.Contain("2"));
                AssertCurrentAssemblyPreview();
                Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
                Assert.That(store.Writes, Is.EqualTo(writes));
                Assert.That(AlbumState(), Is.EqualTo(album));
            }
        }

        [Test] public void SmallerFoundationKeepsExcessMountsRemovableBeforeReview()
        {
            Open(allWeaponsOwned: true, allChassisOwned: true);
            wizard.Load(new TowerBlueprint("three-weapon-screen", "Three weapons", "chassis.battery", new[]
            {
                new WeaponMountSelection(0, "weapon.gun"),
                new WeaponMountSelection(1, "weapon.gun"),
                new WeaponMountSelection(2, "weapon.gun")
            }, content: run.Content));
            Assert.That(wizard.GoToStep(0), Is.True);
            Assert.That(wizard.SelectChassis("chassis.precision"), Is.True);
            Assert.That(wizard.Next(), Is.True);
            AssertWeaponPage();
            int gold = run.Workshop.Gold, writes = store.Writes;
            Assert.That(run.ValidateAssembly(wizard.Draft, out var reason), Is.False);
            Assert.That(wizard.Next(), Is.False);
            Assert.That(wizard.Status, Is.EqualTo(GameText.Resolve(reason)));
            foreach (int slot in new[] { 1, 2 })
            {
                Button("Wizard mount " + slot).onClick.Invoke();
                Assert.That(Text("Weapon catalog heading").text, Does.Contain((slot + 1).ToString()));
                foreach (var weapon in run.Content.Weapons.Where(part => part.Enabled))
                    Assert.That(Button("Wizard choose " + weapon.Id).IsInteractable(), Is.False,
                        "An excess mount may be removed but cannot accept another weapon.");
                Assert.That(Button("Wizard remove mount").IsInteractable(), Is.True);
                Button("Wizard remove mount").onClick.Invoke();
                Assert.That(wizard.Draft.GetWeaponId(slot), Is.Null);
                Assert.That(wizard.Draft.GetWeaponId(0), Is.EqualTo("weapon.gun"));
                if (slot == 1) Assert.That(wizard.Draft.GetWeaponId(2), Is.EqualTo("weapon.gun"));
            }
            Button("Wizard mount 0").onClick.Invoke();
            Assert.That(Text("Weapon catalog heading").text, Does.Contain("1"));
            Assert.That(root.GetComponentsInChildren<Button>(true)
                .Any(button => button.name == "Wizard remove mount" && button.gameObject.activeInHierarchy), Is.False);
            var before = wizard.Draft;
            Assert.That(wizard.SelectWeapon(0, null), Is.False);
            Assert.That(wizard.Draft, Is.SameAs(before));
            Assert.That(wizard.Draft.Drawing.IsEmpty, Is.True);
            AssertCurrentAssemblyPreview();
            Assert.That(wizard.Next(), Is.True, "A valid standard assembly must reach review with an empty drawing.");
            Assert.That(wizard.Step, Is.EqualTo(2));
            Assert.That(root.transform.Find("Wizard review"), Is.Not.Null);
            Assert.That(wizard.Drawing == null || !wizard.Drawing.Root.activeInHierarchy, Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [Test] public void WeaponPurchaseCancellationPreservesDraftAndUnlockRequiresSeparateChoice()
        {
            Open();
            Assert.That(wizard.Next(), Is.True);
            AssertWeaponPage();
            var locked = run.Content.Weapons.First(part => part.Enabled && !run.Workshop.IsUnlocked(part.Id));
            int price = run.Workshop.Parts.Single(part => part.Id == locked.Id).GoldCost;
            int gold = run.Workshop.Gold, writes = store.Writes;
            var draft = wizard.Draft;
            string album = AlbumState();
            Button("Wizard unlock " + locked.Id).onClick.Invoke();
            Assert.That(Button("Wizard next").IsInteractable(), Is.False);
            Button("Workshop cancel confirmation").onClick.Invoke();
            Assert.That(run.Workshop.IsUnlocked(locked.Id), Is.False);
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(wizard.Draft, Is.SameAs(draft));
            Assert.That(AlbumState(), Is.EqualTo(album));
            Assert.That(Button("Wizard next").IsInteractable(), Is.True);

            Button("Wizard unlock " + locked.Id).onClick.Invoke();
            Button("Workshop confirm action").onClick.Invoke();
            Assert.That(run.Workshop.IsUnlocked(locked.Id), Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold - price));
            Assert.That(store.Writes, Is.EqualTo(writes + 1));
            Assert.That(store.Saved.Unlocked, Does.Contain(locked.Id));
            Assert.That(wizard.Draft, Is.SameAs(draft), "Permanent ownership must not silently change the selected weapon.");
            Assert.That(AlbumState(), Is.EqualTo(album));
            AssertCurrentAssemblyPreview();
            Button("Wizard choose " + locked.Id).onClick.Invoke();
            Assert.That(wizard.Draft.GetWeaponId(0), Is.EqualTo(locked.Id));
            Assert.That(store.Writes, Is.EqualTo(writes + 1));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold - price));
            AssertCurrentAssemblyPreview();
        }

        [Test] public void FailedWeaponUnlockKeepsDraftAndGoldAvailableForExplicitRetry()
        {
            Open();
            Assert.That(wizard.Next(), Is.True);
            AssertWeaponPage();
            var locked = run.Content.Weapons.First(part => part.Enabled && !run.Workshop.IsUnlocked(part.Id));
            int price = run.Workshop.Parts.Single(part => part.Id == locked.Id).GoldCost;
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
            Assert.That(wizard.Step, Is.EqualTo(1));
            Assert.That(AlbumState(), Is.EqualTo(album));
            Assert.That(wizard.Status, Does.Contain(Store.WriteError));
            Assert.That(Button("Wizard unlock " + locked.Id).IsInteractable(), Is.True);
            AssertCurrentAssemblyPreview();

            store.FailWrites = false;
            Button("Wizard unlock " + locked.Id).onClick.Invoke();
            Button("Workshop confirm action").onClick.Invoke();
            Assert.That(store.Writes, Is.EqualTo(writes + 2));
            Assert.That(run.Workshop.IsUnlocked(locked.Id), Is.True);
            Assert.That(store.Saved.Unlocked, Does.Contain(locked.Id));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold - price));
            Assert.That(wizard.Draft, Is.SameAs(draft));
            Assert.That(AlbumState(), Is.EqualTo(album));
            AssertCurrentAssemblyPreview();
            Button("Wizard choose " + locked.Id).onClick.Invoke();
            Assert.That(wizard.Draft.GetWeaponId(0), Is.EqualTo(locked.Id));
            Assert.That(store.Writes, Is.EqualTo(writes + 2));
        }

        [Test] public void StandardMortarDoesNotOfferTheRetiredFootprintEditor()
        {
            Open(allWeaponsOwned: true);
            Assert.That(wizard.Next(), Is.True);
            Assert.That(wizard.SelectWeapon(0, "weapon.mortar"), Is.True);
            AssertWeaponPage();
            var page = root.transform.Find("Wizard weapons");
            var preview = page.GetComponentInChildren<WeaponAssemblyGraphic>();
            Assert.That(preview, Is.Not.Null);
            var next = Button("Wizard next");
            var before = wizard.Draft;
            int gold = run.Workshop.Gold, writes = store.Writes;
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(button =>
                button.name == "Wizard weapon geometry" && button.gameObject.activeInHierarchy), Is.False,
                "The standard route must not expose freeform footprint editing.");
            var open = typeof(WorkshopWizard).GetMethod("OpenGeometry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(open, Is.Not.Null);
            open.Invoke(wizard, new object[] { 0 });
            Assert.That(wizard.Mortar == null || !wizard.Mortar.IsOpen, Is.True,
                "The retired editor command must also reject direct invocation.");
            Assert.That(wizard.Draft, Is.SameAs(before));
            Assert.That(root.transform.Find("Wizard weapons"), Is.SameAs(page));
            Assert.That(page.GetComponentInChildren<WeaponAssemblyGraphic>(), Is.SameAs(preview));
            Assert.That(next.IsInteractable(), Is.True);
            Assert.That(wizard.Step, Is.EqualTo(1));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
            AssertCurrentAssemblyPreview();
        }

        [Test] public void AdditionsModuleChoiceRestoresTheOpeningSelectorAfterRebuildingTheWeaponPage()
        {
            Open(allWeaponsOwned: true);
            const string moduleId = "module.armor", selectorName = "Wizard weapon armor";
            if (!run.Content.GetModule(moduleId).InitiallyUnlocked && !run.Workshop.IsUnlocked(moduleId))
                Assert.That(run.UnlockPart(moduleId, out var reason), Is.True, reason);
            Assert.That(wizard.Next(), Is.True);
            AssertWeaponPage();
            var page = root.transform.Find("Wizard weapons");
            var originalSelector = Button(selectorName);
            var next = Button("Wizard next");
            var eventSystem = events.GetComponent<EventSystem>();
            eventSystem.SetSelectedGameObject(originalSelector.gameObject);
            int gold = run.Workshop.Gold, writes = store.Writes;

            originalSelector.onClick.Invoke();
            Assert.That(next.IsInteractable(), Is.False);
            var add = Button("Wizard module add " + moduleId);
            Assert.That(add.IsInteractable(), Is.True);
            eventSystem.SetSelectedGameObject(add.gameObject);
            add.onClick.Invoke();

            Assert.That(wizard.Draft.ModuleSelections.Any(module => module.DefinitionId == moduleId), Is.True);
            Assert.That(root.transform.Find("Wizard weapons"), Is.Not.SameAs(page),
                "A module choice exercises restoration against the replacement page.");
            var currentSelector = Button(selectorName);
            Assert.That(currentSelector, Is.Not.SameAs(originalSelector));
            Assert.That(currentSelector.IsInteractable(), Is.False);
            Assert.That(next.IsInteractable(), Is.False, "The reopened additions sheet must cover the replacement controls.");
            AssertCurrentAssemblyPreview();
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));

            Button("Close additions").onClick.Invoke();
            Assert.That(currentSelector.IsInteractable(), Is.True);
            Assert.That(next.IsInteractable(), Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(currentSelector.gameObject),
                "Closing additions must restore the opening selector by name on the current page.");
            Assert.That(wizard.Step, Is.EqualTo(1));
            AssertCurrentAssemblyPreview();
        }

        sealed class Store : IWorkshopProfileStore
        {
            public const string WriteError = "Injected weapon write failure";
            readonly WorkshopProfile loaded;
            public bool FailWrites;
            public int Writes;
            public WorkshopProfileData Saved;
            public Store(WorkshopProfile loaded) { this.loaded = loaded; }
            public ProfileLoadResult Load() => new ProfileLoadResult(loaded, ProfileLoadSource.Primary, "Isolated weapon profile", true);
            public ProfileSaveResult Save(WorkshopProfile value)
            {
                Writes++;
                if (FailWrites) return new ProfileSaveResult(false, WriteError);
                Saved = value.ToData();
                return new ProfileSaveResult(true, "Saved isolated weapon profile");
            }
        }
    }
}
#endif
