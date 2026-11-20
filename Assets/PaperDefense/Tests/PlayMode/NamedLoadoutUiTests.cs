#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class NamedLoadoutUiTests : BattleHudThemeFixture
    {
        T Control<T>(string name) where T : Component
        {
            var result = root.GetComponentsInChildren<T>().SingleOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, "Missing named-loadout control: " + name);
            return result;
        }
        void Press(string name)
        {
            var button = Control<Button>(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            button.onClick.Invoke(); UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
        }
        void Preparation(int width = 800, int height = 600)
        {
            game.Navigate(PlayerPage.Preparation); Size(new Vector2Int(width, height));
            UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
        }
        string[] Slots() => game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray();
        void Command(AssemblyCommandResult result) => Assert.That(result.Success, Is.True, result.Code);
        NamedLoadout SaveCurrent(string name)
        {
            Press("Named loadouts save"); Control<InputField>("Named loadouts name").text = name;
            Press("Named loadouts commit");
            return game.Run.Workshop.NamedLoadouts.Single(value => value.Name == name.Trim());
        }
        void Reopen(int width = 800, int height = 600)
        { Preparation(width, height); Press("Preparation named loadouts"); }
        static void Inside(RectTransform item, RectTransform outer)
        {
            var bounds = BattleHudThemeEvidence.Bounds(outer, item);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(outer.rect.xMin - 1), item.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(outer.rect.yMin - 1), item.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(outer.rect.xMax + 1), item.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(outer.rect.yMax + 1), item.name);
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void PreparationOpensNamedLoadoutsWithoutReducingTheEightVisibleSlots(int width, int height)
        {
            Preparation(width, height);
            var before = Slots();
            var viewport = Control<ScrollRect>("Preparation loadout scroll").viewport;
            for (int slot = 0; slot < 8; slot++) Inside(Control<RectTransform>("Album loadout " + slot), viewport);
            var entry = Control<Button>("Preparation named loadouts");
            Inside((RectTransform)entry.transform, Control<RectTransform>("Assembly preparation ribbon"));
            Press("Preparation named loadouts");
            var sheet = Control<RectTransform>("Named loadouts sheet");
            Inside(sheet, Control<RectTransform>("Named loadouts"));
            foreach (string action in new[] { "save", "close" })
                Inside((RectTransform)Control<Button>("Named loadouts " + action).transform, sheet);
            Assert.That(Control<Button>("Start battle").IsInteractable(), Is.False, "The modal must cover preparation keyboard actions too.");
            CollectionAssert.AreEqual(before, Slots());
            Press("Named loadouts close");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(entry.gameObject));
            CollectionAssert.AreEqual(before, Slots());
        }

        [Test]
        public void NamingDraftSurvivesResizeAndCancelDoesNotChangeTheCurrentSlots()
        {
            Preparation(); var before = Slots();
            Press("Preparation named loadouts"); Press("Named loadouts save");
            var input = Control<InputField>("Named loadouts name"); input.text = "Несколько фронтов";
            Size(new Vector2Int(1600, 900)); UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
            Assert.That(Control<InputField>("Named loadouts name").text, Is.EqualTo("Несколько фронтов"));
            Assert.That(Control<Button>("Named loadouts commit").IsInteractable(), Is.True);
            Press("Named loadouts cancel"); Press("Named loadouts close");
            CollectionAssert.AreEqual(before, Slots());
        }

        [Test]
        public void BlankAndMarkupNamesExplainWhyTheyCannotBeSaved()
        {
            Preparation(); Press("Preparation named loadouts"); Press("Named loadouts save");
            var input = Control<InputField>("Named loadouts name");
            foreach (string invalid in new[] { "   ", "<b>Броня</b>" })
            {
                input.text = invalid; Canvas.ForceUpdateCanvases();
                Assert.That(Control<Button>("Named loadouts commit").IsInteractable(), Is.False);
                Assert.That(Control<Text>("Named loadouts status").text, Is.Not.Empty);
            }
        }

        [Test]
        public void ControlCharactersAtTheNameEdgesAreRejectedBeforeCommit()
        {
            Preparation(); Press("Preparation named loadouts"); Press("Named loadouts save");
            // SingleLine InputField strips tabs/newlines before notifying us. A carriage
            // return survives SetText and exercises our own validation before name.Trim().
            const string raw = "\rБроня\r";
            var input = Control<InputField>("Named loadouts name"); input.text = raw;
            Assert.That(input.text, Is.EqualTo(raw), "The invalid characters must reach the application validator.");
            Assert.That(Control<Button>("Named loadouts commit").IsInteractable(), Is.False);
            Assert.That(Control<Text>("Named loadouts status").text, Is.Not.Empty);
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void SavePreviewAndSingleApplyPreserveAllEightPositionsIncludingHoles(int width, int height)
        {
            Preparation(width, height);
            Command(game.Run.AssemblyCommands.AssignSlot(2, null, Guid.NewGuid()));
            Command(game.Run.AssemblyCommands.AssignSlot(5, null, Guid.NewGuid()));
            var expected = Slots(); int pages = game.Run.Workshop.Entries.Count;
            Press("Preparation named loadouts"); var saved = SaveCurrent("Против брони");
            CollectionAssert.AreEqual(expected, saved.Slots);
            var preview = Control<RectTransform>("Named loadouts preview");
            for (int slot = 0; slot < 8; slot++) Inside(Control<RectTransform>("Named loadouts slot " + slot), preview);
            Assert.That(Control<Text>("Named loadouts slot name 2").text, Is.EqualTo("Пусто"));
            Assert.That(Control<Text>("Named loadouts slot name 5").text, Is.EqualTo("Пусто"));
            Assert.That(Control<Text>("Named loadouts match").text, Does.Contain("Против брони"));
            Press("Named loadouts close");
            Command(game.Run.AssemblyCommands.AssignSlot(7, expected[0], Guid.NewGuid()));
            var changed = Slots(); Reopen(width, height); Press("Named loadouts choose " + saved.Id);
            CollectionAssert.AreEqual(changed, Slots(), "Preview selection cannot replace the current loadout.");
            Press("Named loadouts apply");
            CollectionAssert.AreEqual(expected, Slots());
            Assert.That(root.GetComponentsInChildren<RectTransform>().Any(value => value.name == "Named loadouts"), Is.False);
            Assert.That(game.Run.Workshop.Entries.Count, Is.EqualTo(pages), "A set contains references, not copied album pages.");
        }

        [Test]
        public void RenameUpdateAndDeleteKeepTheirConfirmationBoundaries()
        {
            Reopen(); var saved = SaveCurrent("Мосты"); var original = Slots();
            Press("Named loadouts rename"); Control<InputField>("Named loadouts name").text = "Два моста";
            Press("Named loadouts commit");
            Assert.That(game.Run.Workshop.NamedLoadouts.Single().Id, Is.EqualTo(saved.Id));
            Assert.That(game.Run.Workshop.NamedLoadouts.Single().Name, Is.EqualTo("Два моста"));
            CollectionAssert.AreEqual(original, game.Run.Workshop.NamedLoadouts.Single().Slots);
            Press("Named loadouts close"); Command(game.Run.AssemblyCommands.AssignSlot(1, null, Guid.NewGuid()));
            var changed = Slots(); Reopen(); Press("Named loadouts update");
            CollectionAssert.AreEqual(original, game.Run.Workshop.NamedLoadouts.Single().Slots);
            Press("Named loadouts cancel"); CollectionAssert.AreEqual(original, game.Run.Workshop.NamedLoadouts.Single().Slots);
            Press("Named loadouts update"); Press("Named loadouts confirm");
            CollectionAssert.AreEqual(changed, game.Run.Workshop.NamedLoadouts.Single().Slots);
            var pages = game.Run.Workshop.Entries.Select(value => value.Id).ToArray();
            Press("Named loadouts delete"); Press("Named loadouts cancel");
            Assert.That(game.Run.Workshop.NamedLoadouts.Count, Is.EqualTo(1));
            Press("Named loadouts delete"); Press("Named loadouts confirm");
            Assert.That(game.Run.Workshop.NamedLoadouts, Is.Empty);
            CollectionAssert.AreEqual(changed, Slots());
            CollectionAssert.AreEqual(pages, game.Run.Workshop.Entries.Select(value => value.Id));
        }

        [Test]
        public void FailedSaveKeepsTheNameAcrossResizeAndFailedApplyKeepsTheDialogOpen()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home); Reopen();
                var original = Slots(); Press("Named loadouts save");
                Control<InputField>("Named loadouts name").text = "Несколько фронтов";
                fixture.Store.RefuseWrites = true; Press("Named loadouts commit");
                Assert.That(game.Run.Workshop.NamedLoadouts, Is.Empty);
                Size(new Vector2Int(1600, 900)); UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
                Assert.That(Control<InputField>("Named loadouts name").text, Is.EqualTo("Несколько фронтов"));
                Assert.That(Control<Text>("Named loadouts status").text, Is.Not.Empty);
                fixture.Store.RefuseWrites = false; Press("Named loadouts commit");
                Press("Named loadouts close"); Command(game.Run.AssemblyCommands.AssignSlot(0, null, Guid.NewGuid()));
                var changed = Slots(); Reopen(); fixture.Store.RefuseWrites = true;
                Press("Named loadouts apply");
                Assert.That(Control<RectTransform>("Named loadouts").gameObject.activeInHierarchy, Is.True);
                CollectionAssert.AreEqual(changed, Slots());
                Assert.That(Control<Text>("Named loadouts status").text, Is.Not.Empty);
                fixture.Store.RefuseWrites = false; Press("Named loadouts apply");
                CollectionAssert.AreEqual(original, Slots());
            }
        }

        [Test]
        public void TwelveSetsDisableCreationAndDuplicateRenameLeavesTheOriginalName()
        {
            Preparation();
            for (int i = 0; i < 12; i++) Command(game.Run.AssemblyCommands.SaveNamedLoadout("Набор " + (i + 1), Guid.NewGuid()));
            Press("Preparation named loadouts");
            Assert.That(Control<Button>("Named loadouts save").IsInteractable(), Is.False);
            Assert.That(Control<Text>("Named loadouts status").text, Does.Contain("12"));
            var selected = game.Run.Workshop.NamedLoadouts[0];
            Press("Named loadouts choose " + selected.Id); Press("Named loadouts rename");
            Control<InputField>("Named loadouts name").text = "  НАБОР 2  ";
            Assert.That(Control<Button>("Named loadouts commit").IsInteractable(), Is.False);
            Press("Named loadouts cancel");
            Assert.That(game.Run.Workshop.NamedLoadouts[0].Name, Is.EqualTo(selected.Name));
            Press("Named loadouts delete"); Press("Named loadouts confirm");
            Assert.That(Control<Button>("Named loadouts save").IsInteractable(), Is.True);
        }

        [Test]
        public void EmptySetCanBeSavedAndAppliedWhileTheExistingBattleGateStillRejectsIt()
        {
            Preparation(); string original = Slots()[0];
            for (int slot = 0; slot < 8; slot++) Command(game.Run.AssemblyCommands.AssignSlot(slot, null, Guid.NewGuid()));
            Press("Preparation named loadouts"); SaveCurrent("Свободные места");
            Press("Named loadouts close"); Command(game.Run.AssemblyCommands.AssignSlot(0, original, Guid.NewGuid()));
            Reopen(); Press("Named loadouts apply");
            Assert.That(Slots(), Is.All.Null);
            Assert.That(Control<Button>("Start battle").IsInteractable(), Is.False);
        }

        [Test]
        public void MissingConfirmationTargetCannotTurnIntoAnotherSetsDeletionAfterResize()
        {
            Preparation();
            Command(game.Run.AssemblyCommands.SaveNamedLoadout("Первый", Guid.NewGuid()));
            Command(game.Run.AssemblyCommands.SaveNamedLoadout("Второй", Guid.NewGuid()));
            var first = game.Run.Workshop.NamedLoadouts[0]; var second = game.Run.Workshop.NamedLoadouts[1];
            Press("Preparation named loadouts"); Press("Named loadouts choose " + second.Id); Press("Named loadouts delete");
            Command(game.Run.AssemblyCommands.DeleteNamedLoadout(second.Id, Guid.NewGuid()));
            Size(new Vector2Int(1600, 900)); UiMenuMotion.FinishIn(root.transform); Canvas.ForceUpdateCanvases();
            Assert.That(root.GetComponentsInChildren<Button>().Any(value => value.name == "Named loadouts confirm"), Is.False,
                "A missing confirmation target must cancel that operation, never retarget the first remaining set.");
            Assert.That(game.Run.Workshop.NamedLoadouts.Single().Id, Is.EqualTo(first.Id));
        }
    }
}
#endif
