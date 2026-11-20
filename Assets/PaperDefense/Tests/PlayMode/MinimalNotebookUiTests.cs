#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class MinimalNotebookUiTests : BattleHudThemeFixture
    {
        static readonly Vector2Int MinimumSize = new Vector2Int(800, 600);

        [UnityTest] public IEnumerator EightRealRecipesPricesAndHotkeysFitWithoutScrollingAt800()
        {
            StartBattle(); Size(MinimumSize); yield return null; Canvas.ForceUpdateCanvases();
            var dock = Named<RectTransform>("Construction dock");
            Assert.That(dock.GetComponentsInChildren<ScrollRect>(), Is.Empty);
            for (int slot = 0; slot < 8; slot++)
            {
                var card = Named<Button>("Build tool " + slot);
                var art = card.GetComponentInChildren<WeaponAssemblyGraphic>();
                Assert.That(art, Is.Not.Null, "Slot " + slot + " must draw its actual recipe, not a generic icon.");
                Assert.That(art.Recipe, Is.SameAs(game.Run.RunLoadout.Entries[slot].Snapshot));
                Assert.That(art.canvasRenderer.GetMesh()?.vertexCount ?? 0, Is.GreaterThan(0));
                AssertInside((RectTransform)card.transform, dock, "Eight visible slots");
                AssertInside(art.rectTransform, (RectTransform)card.transform, "Recipe thumbnail");
                var price = Named<Text>("Album battle price " + slot);
                Assert.That(price.text, Is.EqualTo(game.Run.RunLoadout.ResolveSlot(slot, MaterialTier.Pencil).Cost.ToString()));
                AssertReadable(price, 14);
                var key = card.GetComponentsInChildren<Text>().Single(t => t.text == (slot + 1).ToString() && t != price);
                AssertReadable(key, 12);
                AssertInside(key.rectTransform, (RectTransform)card.transform, "Visible hotkey");
                AssertInside(price.rectTransform, (RectTransform)card.transform, "Visible exact price");
                EventSystem.current.SetSelectedGameObject(card.gameObject);
                Canvas.ForceUpdateCanvases();
                var hint = Named<Text>("Battle tool hint");
                Assert.That(hint.text, Does.Contain(GameText.Resolve(game.Run.RunLoadout.Entries[slot].Name)));
                Assert.That(hint.text, Does.Contain(price.text + " штр."));
                AssertReadable(hint, 14);
                Submit(card);
                Assert.That(game.Drawing.BlueprintSlot, Is.EqualTo(slot), "Keyboard submit selects the matching slot.");
            }
        }

        [UnityTest] public IEnumerator ThreeCompleteDirectionRowsAreVisibleAt800BeforeAnyScroll()
        {
            StartBattle(); var tower = BuildReadyTower();
            Size(MinimumSize); game.Drawing.Inspect(tower.Cell); Hud.Refresh();
            yield return null; Canvas.ForceUpdateCanvases();
            var scroll = Named<ScrollRect>("Selected structure details");
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.005f));
            Assert.That(Named<Text>("Album battle structure details").gameObject.activeSelf, Is.False);
            foreach (string direction in new[] { "Power", "Mechanics", "Range" })
            {
                var row = Named<RectTransform>("Modernize " + direction + " row");
                AssertInside(row, scroll.viewport, "The entire " + direction + " row is visible without selecting/revealing it.");
                foreach (var text in row.GetComponentsInChildren<Text>()) AssertReadable(text, 14);
                var buy = Named<Button>("Modernize " + direction);
                Assert.That(buy.IsInteractable(), Is.True);
                Assert.That(((RectTransform)buy.transform).rect.height, Is.GreaterThanOrEqualTo(32));
            }
        }

        [UnityTest] public IEnumerator LockedBuyRetainsKeyboardReasonAndDoesNotSpendOnInfoSubmit()
        {
            StartBattle(); var tower = BuildReadyTower();
            Assert.That(game.Run.Modernize(game.Run.QuoteModernization(tower.Id, ModernizationDirection.Power), out var reason), Is.True, reason);
            for (int step = 0; step < 140; step++) game.Run.StepSimulation();
            Size(MinimumSize); game.Drawing.Inspect(tower.Cell); Hud.Refresh(); yield return null;
            var quote = game.Run.QuoteModernization(tower.Id, ModernizationDirection.Power);
            Assert.That(quote.Valid, Is.False); Assert.That(quote.NextRank, Is.EqualTo(2));
            var buy = Named<Button>("Modernize Power");
            Assert.That(buy.IsInteractable(), Is.False);
            Assert.That(buy.GetComponentInChildren<PlayerIconGraphic>().Icon, Is.EqualTo(PlayerIcon.Lock));
            var row = Named<Button>("Modernize Power row");
            Assert.That(row.IsInteractable(), Is.True, "Information must remain focusable when payment is locked.");
            Assert.That(row.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None));
            int ink = game.Run.Session.Ink; long tick = game.Run.Session.TickIndex;
            int revision = game.Run.Session.StructureAt(tower.Cell).Value.Modernization.Revision;
            EventSystem.current.SetSelectedGameObject(row.gameObject);
            Canvas.ForceUpdateCanvases();
            var hint = Named<Text>("Battle tool hint");
            Assert.That(hint.gameObject.activeInHierarchy, Is.True);
            Assert.That(hint.text, Does.Contain("Мощность").And.Contain(quote.Reason));
            Assert.That(hint.text, Does.Contain(quote.Cost + " штрихов"));
            AssertReadable(hint, 14);
            AssertInside((RectTransform)row.transform, row.GetComponentInParent<ScrollRect>().viewport, "Focused locked row");
            Submit(row);
            Assert.That(game.Run.Session.Ink, Is.EqualTo(ink));
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.Run.Session.StructureAt(tower.Cell).Value.Modernization.Revision, Is.EqualTo(revision));
            Assert.That(game.Run.Session.StructureAt(tower.Cell).Value.Modernization.IsWorking, Is.False);
        }

        [UnityTest] public IEnumerator DetailsDisclosureChangesOnlyPresentationAndResetsForAnotherTower()
        {
            StartBattle(); var first = BuildReadyTower(); var second = BuildReadyTower();
            Size(MinimumSize); game.Drawing.Inspect(first.Cell); Hud.Refresh(); yield return null;
            var details = Named<Text>("Album battle structure details");
            var toggle = Named<Button>("Battle details toggle");
            Assert.That(details.gameObject.activeSelf, Is.False);
            Assert.That(toggle.GetComponentInChildren<PlayerIconGraphic>().Icon, Is.EqualTo(PlayerIcon.ChevronDown));
            int ink = game.Run.Session.Ink; long tick = game.Run.Session.TickIndex;
            int speed = game.Run.Speed; bool paused = game.Run.Paused;
            var before = game.Run.Session.StructureAt(first.Cell).Value;
            EventSystem.current.SetSelectedGameObject(toggle.gameObject); Submit(toggle);
            Assert.That(details.gameObject.activeInHierarchy, Is.True);
            Assert.That(toggle.GetComponentInChildren<PlayerIconGraphic>().Icon, Is.EqualTo(PlayerIcon.ChevronUp));
            Assert.That(details.text, Does.Contain("Орудие 1").And.Contain("перезарядка"));
            AssertReadable(details, 14);
            var scroll = Named<ScrollRect>("Selected structure details");
            var detailBounds = BattleHudThemeEvidence.Bounds(scroll.viewport, details.rectTransform);
            Assert.That(detailBounds.yMax, Is.InRange(scroll.viewport.rect.yMin + 14, scroll.viewport.rect.yMax + 1), "Opening must reveal the beginning of details.");
            Submit(toggle);
            Assert.That(details.gameObject.activeSelf, Is.False);
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.005f));
            Submit(toggle); game.Drawing.Inspect(second.Cell); Hud.Refresh();
            Assert.That(details.gameObject.activeSelf, Is.False, "Another instance starts with concise details.");
            var after = game.Run.Session.StructureAt(first.Cell).Value;
            Assert.That(game.Run.Session.Ink, Is.EqualTo(ink)); Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.Run.Speed, Is.EqualTo(speed)); Assert.That(game.Run.Paused, Is.EqualTo(paused));
            Assert.That(after.Health, Is.EqualTo(before.Health)); Assert.That(after.Priority, Is.EqualTo(before.Priority));
            Assert.That(after.ResolvedEntry, Is.SameAs(before.ResolvedEntry));
            Assert.That(after.Modernization.Revision, Is.EqualTo(before.Modernization.Revision));
            Assert.That(after.Color, Is.EqualTo(before.Color));
        }

        [UnityTest] public IEnumerator SecondaryIconsExposePointerAndKeyboardNamesAndSubmitTheirActions()
        {
            StartBattle(); Size(MinimumSize); yield return null; Canvas.ForceUpdateCanvases();
            foreach (string action in new[] { "Album battle erase", "Album battle blotter", "Battle context toggle", "Battle overview", "Battle ranges", "Tactical pause", "Battle pause" })
            {
                var button = Named<Button>(action); var rect = (RectTransform)button.transform;
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(32)); Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(32));
                var glyph = button.GetComponentInChildren<PlayerIconGraphic>();
                Assert.That(glyph, Is.Not.Null); Assert.That(glyph.canvasRenderer.GetMesh()?.vertexCount ?? 0, Is.GreaterThan(0));
                var tooltip = button.GetComponent<PlayerTooltip>(); Assert.That(tooltip.Message, Is.Not.Empty);
                ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Canvas.ForceUpdateCanvases();
                Assert.That(Named<Text>("Battle tool hint").text, Is.EqualTo(tooltip.Message));
                ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                EventSystem.current.SetSelectedGameObject(button.gameObject); Canvas.ForceUpdateCanvases();
                Assert.That(Named<Text>("Battle tool hint").text, Is.EqualTo(tooltip.Message));
                AssertReadable(Named<Text>("Battle tool hint"), 14);
            }
            EventSystem.current.SetSelectedGameObject(null);
            Submit(Named<Button>("Album battle erase")); Assert.That(game.Drawing.Mode, Is.EqualTo(DrawingMode.Erase));
            Submit(Named<Button>("Album battle blotter")); Assert.That(game.Drawing.Mode, Is.EqualTo(DrawingMode.Blotter));
            bool ranges = game.World.ShowRanges; Submit(Named<Button>("Battle ranges")); Assert.That(game.World.ShowRanges, Is.EqualTo(!ranges));
            Submit(Named<Button>("Tactical pause")); Assert.That(game.Run.TacticalPaused, Is.True); Assert.That(game.CanBuild, Is.True);
            Submit(Named<Button>("Battle pause")); Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause)); Assert.That(game.Run.CommandsSuspended, Is.True);
            game.Resume(); Assert.That(game.Run.TacticalPaused, Is.True);
        }

        StructureView BuildReadyTower()
        {
            for (int y = 2; y < game.Run.Session.Balance.Height - 2; y++)
            for (int x = 2; x < game.Run.Session.Balance.Width - 2; x++)
            {
                var cell = new Cell(x, y); var cells = new[] { cell };
                if (!game.Run.QuoteEntry(cells, 0, MaterialTier.Pencil, InkColor.None).Valid) continue;
                Assert.That(game.Run.BuildEntry(cells, 0, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                for (int step = 0; step < 160; step++) game.Run.StepSimulation();
                var tower = game.Run.Session.StructureAt(cell).Value;
                Assert.That(tower.IsComplete, Is.True); return tower;
            }
            Assert.Fail("No affordable legal tower position."); return default;
        }

        void Submit(Button button)
        {
            Assert.That(button.IsInteractable(), Is.True, button.name);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Hud?.Refresh(); Canvas.ForceUpdateCanvases();
        }
        static void AssertReadable(Text text, int minimumSize)
        {
            Assert.That(text.gameObject.activeInHierarchy, Is.True, text.name);
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(minimumSize), text.name);
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name + ": " + text.text);
            Assert.That(text.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(text.text.Length), text.name + ": " + text.text);
        }
        static void AssertInside(RectTransform target, RectTransform parent, string message)
        {
            var bounds = BattleHudThemeEvidence.Bounds(parent, target); var outer = parent.rect;
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 1), message);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(outer.xMax + 1), message);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 1), message);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(outer.yMax + 1), message);
        }
    }
}
#endif
