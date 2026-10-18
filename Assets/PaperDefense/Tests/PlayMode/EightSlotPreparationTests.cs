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

        [UnityTest] public IEnumerator At1024x768_AllEightSlotsVisible()
        {
            shell.ApplyLayout(1024, 768); Canvas.ForceUpdateCanvases();
            var page = Rect("Page Preparation");
            int visibleSlotCount = Enumerable.Range(0, 8).Count(slot =>
                root.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Album loadout " + slot));
            Assert.That(visibleSlotCount, Is.EqualTo(8));
            Assert.That(page.GetComponentsInChildren<ScrollRect>(), Is.Empty);
            var bounds = Enumerable.Range(0, 8).Select(slot => Bounds(page, Rect("Album loadout " + slot))).ToArray();
            for (int slot = 0; slot < 8; slot++)
            {
                AssertWithin(page, Rect("Album loadout " + slot));
                Assert.That(Find("Replace album entry " + slot).IsInteractable(), Is.True);
                AssertWithin(Rect("Album loadout " + slot), (RectTransform)Find("Replace album entry " + slot).transform);
                for (int previous = 0; previous < slot; previous++) Assert.That(bounds[slot].Overlaps(bounds[previous]), Is.False);
            }
            Assert.That(bounds[0].center.y, Is.EqualTo(bounds[3].center.y).Within(1));
            Assert.That(bounds[4].center.y, Is.EqualTo(bounds[7].center.y).Within(1));
            Assert.That(bounds[0].center.y, Is.Not.EqualTo(bounds[4].center.y).Within(1));
            foreach (string action in new[] { "Edit album", "Open preparation album", "Start battle" })
            {
                AssertWithin(page, (RectTransform)Find(action).transform);
                foreach (var card in bounds) Assert.That(card.Overlaps(Bounds(page, (RectTransform)Find(action).transform)), Is.False);
            }
            yield return null;
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
            foreach (var size in new[] { new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                shell.ApplyLayout(size.x, size.y); Canvas.ForceUpdateCanvases();
                var card = Rect("Album loadout 7"); var text = Copy("Album loadout name 7");
                Assert.That(text.text, Is.EqualTo(draft.Name)); AssertWithin(card, text.rectTransform);
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
