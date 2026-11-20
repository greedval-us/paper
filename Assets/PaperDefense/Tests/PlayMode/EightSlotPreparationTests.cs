#if UNITY_EDITOR
using System;
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
    public sealed class EightSlotPreparationTests
    {
        GameObject root;
        PlayerGameRoot game;
        PlayerShell shell;
        GameBalanceAsset balance;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            balance = Object.Instantiate(source); balance.planningSeconds = 1000;
            root = new GameObject("Eight slot preparation fixture"); root.SetActive(false);
            var camera = new GameObject("Eight slot fixture camera", typeof(Camera));
            camera.transform.SetParent(root.transform); camera.tag = "MainCamera";
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true); yield return null;
            game.Run.AutomaticSimulation = false;
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            game.Navigate(PlayerPage.Preparation); Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); Object.Destroy(balance); yield return null; }

        Button Find(string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        RectTransform Rect(string name) => root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == name);
        Text Copy(string name) => root.GetComponentsInChildren<Text>().Single(text => text.name == name);

        [TestCase(800, 600)] [TestCase(1024, 768)] [TestCase(1600, 900)]
        public void AllEightSlotsRemainReadableAndVisibleWithoutScrolling(int width, int height)
        {
            shell.ApplyLayout(width, height); UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
            var page = Rect("Page Preparation");
            int visibleSlotCount = Enumerable.Range(0, 8).Count(slot =>
                root.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Album loadout " + slot));
            Assert.That(visibleSlotCount, Is.EqualTo(8));
            Assert.That(Copy("Preparation loadout count").text, Is.EqualTo("Набор · 8 / 8"));
            var scroll = page.GetComponentsInChildren<ScrollRect>().Single(control => control.name == "Preparation loadout scroll");
            var grid = Rect("Assembly preparation loadout");
            AssertWithin(page, scroll.viewport);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            var bounds = Enumerable.Range(0, 8).Select(slot => Bounds(grid, Rect("Album loadout " + slot))).ToArray();
            for (int slot = 0; slot < 8; slot++)
            {
                var card = Rect("Album loadout " + slot);
                AssertWithin(grid, card);
                AssertWithin(scroll.viewport, card);
                Assert.That(Find("Replace album entry " + slot).IsInteractable(), Is.True);
                foreach (string action in new[] { "Replace album entry ", "Clear album entry " })
                {
                    var control = (RectTransform)Find(action + slot).transform;
                    AssertWithin(card, control);
                    Assert.That(control.rect.height, Is.GreaterThanOrEqualTo(32));
                    Assert.That(control.rect.width, Is.GreaterThanOrEqualTo(32));
                }
                var entry = game.Run.Workshop.LoadoutEntries[slot];
                Assert.That(entry, Is.Not.Null, "This fixture exercises eight occupied cards.");
                Assert.That(Rect("Album loadout number " + slot).GetComponentInChildren<Text>().text, Is.EqualTo((slot + 1).ToString()));
                Assert.That(Copy("Album loadout name " + slot).text, Is.Not.Empty);
                Assert.That(Find("Replace album entry " + slot).GetComponent<PlayerTooltip>().Message, Does.Contain(GameText.Resolve(entry.Name)));
                var quote = game.Run.PreviewAlbumEntry(entry, MaterialTier.Pencil);
                Assert.That(quote.Valid, Is.True);
                string cost = GameText.Resolve(UserText.Message("assembly.preparation.cost", quote.Value.Cost));
                if (entry.Snapshot.Recipe.Structure?.Family == AssemblyStructureFamily.Generator)
                    cost += "\n" + GameText.Resolve(UserText.Message("assembly.preparation.pencil_only"));
                Assert.That(Copy("Album loadout cost " + slot).text, Is.EqualTo(cost));
                foreach (var copy in new[] { Copy("Album loadout name " + slot), Copy("Album loadout cost " + slot) })
                { AssertWithin(card, copy.rectTransform); AssertReadable(copy); }
                var illustration = card.GetComponentsInChildren<Graphic>().Single(value => value.name == "Preparation certified assembly");
                AssertWithin(card, illustration.rectTransform);
                Assert.That(illustration.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0), "Slot " + slot + " needs its real assembly illustration.");
                for (int previous = 0; previous < slot; previous++) Assert.That(bounds[slot].Overlaps(bounds[previous]), Is.False);
            }
            Assert.That(scroll.content.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height + 1));
            foreach (string action in new[] { "Edit album", "Open preparation album", "Start battle" })
            {
                AssertWithin(page, (RectTransform)Find(action).transform);
                Assert.That(Bounds(page, scroll.viewport).Overlaps(Bounds(page, (RectTransform)Find(action).transform)), Is.False);
            }
        }

        [UnityTest] public IEnumerator AllEightPositionsRemainAssignableWithoutCompactingEmptySlots()
        {
            var entries = game.Run.Workshop.Entries.Take(8).ToArray();
            for (int slot = 0; slot < 8; slot++)
                Assert.That(game.Run.AssemblyCommands.AssignSlot(slot, null, Guid.NewGuid()).Success, Is.True);
            game.Navigate(PlayerPage.Preparation);
            for (int slot = 0; slot < 8; slot++)
            {
                Find("Replace album entry " + slot).onClick.Invoke();
                Find("Album choose " + entries[slot].Id).onClick.Invoke();
                Assert.That(game.Run.Workshop.LoadoutEntries[slot]?.Id, Is.EqualTo(entries[slot].Id), "Slot " + slot);
            }
            Find("Clear album entry 2").onClick.Invoke();
            Assert.That(game.Run.Workshop.LoadoutEntries[2], Is.Null);
            Assert.That(game.Run.Workshop.LoadoutEntries[3].Id, Is.EqualTo(entries[3].Id));
            Assert.That(game.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(entries[7].Id));
            Assert.That(Copy("Album loadout name 2").text, Does.Contain("Пуст"));
            yield return null;
        }

        [UnityTest] public IEnumerator SelectingAnOccupiedEntryRequiresConfirmationBeforeMovingIt()
        {
            var before = game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray();
            Find("Replace album entry 7").onClick.Invoke();
            Find("Album choose " + before[0]).onClick.Invoke();
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id), Is.EqualTo(before));
            Find("Album cancel move").onClick.Invoke();
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id), Is.EqualTo(before));
            Find("Album choose " + before[0]).onClick.Invoke(); Find("Album confirm move").onClick.Invoke();
            Assert.That(game.Run.Workshop.LoadoutEntries[0], Is.Null);
            Assert.That(game.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(before[0]));
            for (int slot = 1; slot < 7; slot++) Assert.That(game.Run.Workshop.LoadoutEntries[slot].Id, Is.EqualTo(before[slot]));
            yield return null;
        }

        [UnityTest] public IEnumerator SupportOnlyPreparationCannotStartAnEnemyDefenseRun()
        {
            foreach (int slot in Enumerable.Range(0, 8))
            {
                var entry = game.Run.Workshop.LoadoutEntries[slot];
                if (entry != null && entry.Kind != AlbumEntryKind.Structure)
                    Assert.That(game.Run.AssemblyCommands.AssignSlot(slot, null, Guid.NewGuid()).Success, Is.True);
            }
            game.Navigate(PlayerPage.Preparation);
            Assert.That(Find("Start battle").interactable, Is.False);
            Find("Start battle").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(Copy("Preparation readiness").text, Is.Not.Empty);
            yield return null;
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void OneAttackingPageInTheLastPositionStartsWithoutFillingOrCompactingTheOtherSlots(int width, int height)
        {
            using (var fixture = new AssemblyCommandFixture(configure: data => {
                data.Loadout = new string[ProfileLimits.LoadoutSlotCount]; data.Loadout[7] = "album.guard";
            }))
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Preparation);
                shell.ApplyLayout(width, height); UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
                var idle = game.Run.Session; var previousSnapshot = game.Run.RunLoadout;
                byte[] bytes = fixture.SavedBytes; int writes = fixture.Store.SaveCalls;
                Assert.That(game.Run.Workshop.LoadoutEntries.Take(7).All(entry => entry == null), Is.True);
                Assert.That(game.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo("album.guard"));
                Assert.That(Copy("Preparation loadout count").text, Is.EqualTo("Набор · 1 / 8"));
                Assert.That(game.Run.CanStartAlbumRun(out var reason), Is.True, reason);
                Assert.That(Find("Start battle").IsInteractable(), Is.True);
                Assert.That(Copy("Preparation readiness").text, Is.Not.Empty);
                Assert.That(Copy("Preparation starting ink").text,
                    Is.EqualTo(GameText.Resolve(UserText.Message("assembly.preparation.starting_ink", idle.Balance.StartingInk))));
                Assert.That(game.Run.Session, Is.SameAs(idle)); Assert.That(game.Run.RunLoadout, Is.SameAs(previousSnapshot));
                var start = Find("Start battle"); EventSystem.current.SetSelectedGameObject(start.gameObject);
                ExecuteEvents.Execute(start.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
                Assert.That(game.Run.InWorkshop, Is.False);
                Assert.That(game.Run.RunLoadout.Entries.Count, Is.EqualTo(8));
                Assert.That(game.Run.RunLoadout.Entries.Take(7).All(entry => entry == null), Is.True);
                Assert.That(game.Run.RunLoadout.Entries[7].Id, Is.EqualTo("album.guard"));
                Assert.That(game.Run.Session.TickIndex, Is.Zero);
                Assert.That(game.Run.Session.Balance.StartingInk, Is.EqualTo(idle.Balance.StartingInk));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void AnInvalidOrUnownedOccupiedPageBlocksStartEvenBesideAValidAttacker(bool unowned)
        {
            var catalog = game.Run.AssemblyCatalog;
            var source = catalog.Starters.Single(entry => entry.Id == "album.guard");
            var tower = source.Snapshot.Recipe.Tower; var oldWeapon = tower.Mounts.Single().Weapon;
            var weapon = new WeaponRecipe(oldWeapon.Mechanism, new PartRef(unowned ? "head.long" : oldWeapon.Head.Id, 1),
                oldWeapon.Sight, oldWeapon.Magazine, oldWeapon.Trigger);
            var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) }));
            var snapshot = new RecipeSnapshotData(new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions));
            if (!unowned)
                snapshot.Definitions.Single(part => part.Id == "core.gun").Modifiers.Single(modifier => modifier.Stat == AssemblyStat.Interval).Value = 0;
            var bad = new AlbumEntry("preparation.invalid", "Проверка недоступной сборки", 1, snapshot.CreateSnapshot());
            using (var fixture = new AssemblyCommandFixture(configure: data => {
                data.Entries = data.Entries.Concat(new[] { new AlbumEntryData(bad) }).ToArray();
                data.Loadout = new string[ProfileLimits.LoadoutSlotCount];
                data.Loadout[0] = "album.guard"; data.Loadout[7] = bad.Id;
            }))
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Preparation);
                shell.ApplyLayout(800, 600); UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
                byte[] bytes = fixture.SavedBytes; int writes = fixture.Store.SaveCalls;
                var idle = game.Run.Session; var previousSnapshot = game.Run.RunLoadout;
                Assert.That(game.Run.PreviewAlbumEntry(game.Run.Workshop.LoadoutEntries[0]).Valid, Is.True);
                Assert.That(game.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(bad.Id));
                Assert.That(game.Run.PreviewAlbumEntry(game.Run.Workshop.LoadoutEntries[7]).Valid, Is.EqualTo(unowned));
                if (unowned) Assert.That(game.Run.Workshop.IsUnlocked("head.long"), Is.False);
                Assert.That(game.Run.CanStartAlbumRun(out var reason), Is.False);
                Assert.That(Copy("Preparation readiness").text, Is.EqualTo(unowned ? GameText.Resolve(reason) : "Проверь чертежи набора"));
                Assert.That(Copy("Preparation readiness").GetComponent<WorkshopTooltip>().Hint, Is.EqualTo(GameText.Resolve(reason)));
                AssertReadable(Copy("Preparation readiness"));
                Assert.That(Find("Start battle").interactable, Is.False);
                Assert.That(Find("Start battle").GetComponent<PlayerTooltip>().Message, Is.EqualTo(GameText.Resolve(reason)));
                Find("Start battle").onClick.Invoke();
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation)); Assert.That(game.Run.InWorkshop, Is.True);
                Assert.That(game.Run.Session, Is.SameAs(idle)); Assert.That(game.Run.RunLoadout, Is.SameAs(previousSnapshot));
                Assert.That(game.Run.Session.TickIndex, Is.Zero);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
            }
        }

        [UnityTest] public IEnumerator AMaximumLengthNameStaysClearOfReplaceAndClearControlsAtEveryRequiredSize()
        {
            var source = game.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
            var draft = new AlbumEntry("draft.long.preparation", new string('Ш', 48), 1, source.Snapshot);
            var save = game.Run.AssemblyCommands.SaveEntryAsVariant(draft, Guid.NewGuid());
            Assert.That(save.Success, Is.True, save.Code); game.AssignAlbumEntry(7, save.ResultId);
            var generatorEntry = game.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Structure &&
                entry.Snapshot.Recipe.Structure.Family == AssemblyStructureFamily.Generator);
            game.AssignAlbumEntry(6, generatorEntry.Id);
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
            {
                shell.ApplyLayout(size.x, size.y); UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
                var card = Rect("Album loadout 7"); var text = Copy("Album loadout name 7");
                Assert.That(text.text, Is.Not.Empty);
                if (text.text != draft.Name)
                {
                    Assert.That(text.text, Does.EndWith("…"));
                    Assert.That(draft.Name, Does.StartWith(text.text.Substring(0, text.text.Length - 1)));
                }
                AssertWithin(card, text.rectTransform); AssertReadable(text);
                var replace = Find("Replace album entry 7");
                Assert.That(replace.GetComponent<PlayerTooltip>().Message, Does.Contain(draft.Name));
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(replace.gameObject); Canvas.ForceUpdateCanvases();
                var hint = root.GetComponentsInChildren<Text>().Single(copy => copy.name == "Hint");
                Assert.That(hint.text, Does.Contain(draft.Name), "Keyboard focus must expose the full saved identity.");
                foreach (string action in new[] { "Replace album entry 7", "Clear album entry 7" })
                    Assert.That(Bounds(card, text.rectTransform).Overlaps(Bounds(card, (RectTransform)Find(action).transform)), Is.False, size.ToString());
                var generator = game.Run.Workshop.LoadoutEntries.Select((entry, slot) => (entry, slot))
                    .First(pair => pair.entry != null && pair.entry.Kind == AlbumEntryKind.Structure &&
                        pair.entry.Snapshot.Recipe.Structure.Family == AssemblyStructureFamily.Generator);
                var generatorPrice = Copy("Album loadout cost " + generator.slot);
                Assert.That(generatorPrice.text, Does.Contain("карандаш").IgnoreCase);
                Assert.That(generatorPrice.preferredHeight, Is.LessThanOrEqualTo(generatorPrice.rectTransform.rect.height + 1),
                    "The price and pencil-only line must both fit their rendered area at " + size);
                Assert.That(generatorPrice.cachedTextGenerator.lineCount, Is.EqualTo(2), size.ToString());
                Assert.That(generatorPrice.fontSize * shell.Layout.Scale, Is.GreaterThanOrEqualTo(15), size.ToString());
            }
            yield return null;
        }

        void AssertReadable(Text label)
        {
            float scale = shell.Layout.Scale;
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size * scale);
            settings.scaleFactor = 1; settings.fontSize = Mathf.RoundToInt(label.fontSize * scale);
            settings.resizeTextMinSize = Mathf.CeilToInt(label.resizeTextMinSize * scale);
            settings.resizeTextMaxSize = Mathf.Max(settings.resizeTextMinSize, Mathf.RoundToInt(label.resizeTextMaxSize * scale));
            settings.generateOutOfBounds = false;
            using (var generated = new TextGenerator())
            {
                generated.Populate(label.text, settings);
                Assert.That(generated.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length), label.name + ": " + label.text);
                Assert.That(label.resizeTextForBestFit ? generated.fontSizeUsedForBestFit : settings.fontSize,
                    Is.GreaterThanOrEqualTo(14), label.name);
            }
        }

        [UnityTest] public IEnumerator CancellingThePickerReturnsKeyboardFocusToTheSameSlot()
        {
            var before = game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray();
            var replace = Find("Replace album entry 6"); EventSystem.current.SetSelectedGameObject(replace.gameObject);
            replace.onClick.Invoke(); Find("Album close").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id), Is.EqualTo(before));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Find("Replace album entry 6").gameObject));
            yield return null;
        }

        static Rect Bounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var minimum = (Vector2)parent.InverseTransformPoint(corners[0]);
            var maximum = (Vector2)parent.InverseTransformPoint(corners[2]);
            return new Rect(minimum, maximum - minimum);
        }
        static void AssertWithin(RectTransform parent, RectTransform child)
        {
            var bounds = Bounds(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 1), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 1), child.name);
        }
    }
}
#endif
