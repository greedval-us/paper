using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        static string PreparationCopy(string key, params object[] args)
            => GameText.Resolve(UserText.Message("assembly.preparation." + key, args));

        void AssemblyPreparation()
        {
            NotebookPageBackdrop();
            string mission = game.Run.CampaignEnabled ? game.Run.SelectedMission.Title : "Боевой набор";
            CompactMenuHeader(PreparationCopy("title"), () => game.Navigate(PlayerPage.Map),
                mission + " · " + WaveCountLabel(game.Run.Session.Balance.Waves.Count),
                "Выбери до восьми башен и сооружений. Нужна хотя бы одна атакующая башня.\n\nКруговая стрелка меняет чертёж, плюс заполняет пустое место, корзина освобождает его. Пустые места можно оставить свободными. В мастерской можно собрать новый вариант.\n\n" +
                (game.Run.Session.Balance.Mechanics.FieldModernization
                    ? "Все копии начинают бой с базовыми параметрами. Развивай выбранную башню на поле за ресурсы; цвет закрепляется при первой установке. Полный состав доступен в альбоме."
                    : "Материал меняет цену и свойства строительства. Генератор строится только карандашом. Полный состав и характеристики доступны в альбоме."));
            bool desktop = W >= 1180 && H >= 660;
            float margin = Mathf.Min(40, W * .025f), gap = desktop ? 12 : 8;
            float availableWidth = W - margin * 2;
            float side = desktop ? Mathf.Clamp(availableWidth * .25f, 300, 390) : 0;
            float width = desktop ? availableWidth - side - gap * 2 : availableWidth, left = margin;
            bool ready = game.Run.CanStartAlbumRun(out var reason);
            bool modernization = game.Run.Session.Balance.Mechanics.FieldModernization;
            float footerHeight = desktop ? 52 : 44;
            float materialHeight = modernization ? 64 : 76;
            float ribbonHeight = desktop ? 48 : 40, top = W >= 1120 ? 88 : 72;
            int columns = 4;
            int rows = Mathf.CeilToInt(ProfileLimits.LoadoutSlotCount / (float)columns);
            float availableHeight = H - top - margin;
            float cardHeight = desktop ? (availableHeight - ribbonHeight - footerHeight - gap * (rows + 1)) / rows :
                Mathf.Clamp((availableHeight - ribbonHeight - materialHeight - footerHeight - gap * (rows + 2)) / rows, 144, 184);
            float extent = rows * (cardHeight + gap) - gap;
            float blockHeight = ribbonHeight + extent + materialHeight + footerHeight + gap * 3;
            float ribbonY = desktop ? top : top + Mathf.Max(0, (availableHeight - blockHeight) * .5f);
            float gridY = ribbonY + ribbonHeight + gap;
            float gridHeight = desktop ? extent : Mathf.Min(extent, Mathf.Max(80, H - margin - footerHeight - materialHeight - gap * 2 - gridY));
            float materialY = gridY + gridHeight + gap, footerY = desktop ? H - margin - footerHeight : materialY + materialHeight + gap;
            int count = game.Run.Workshop.LoadoutEntries.Count(entry => entry != null);
            var ribbon = ui.Panel(page, "Assembly preparation ribbon", new Rect(left, ribbonY, width, ribbonHeight), Color.clear);
            var sets = ui.IconButton(ribbon, "Preparation named loadouts", new Rect(0, 0, 174, ribbonHeight),
                PlayerIcon.Next, "Сохранённые боевые наборы: сохранить или заменить все восемь мест", OpenNamedLoadouts);
            PlayerUi.Place(sets.GetComponentInChildren<PlayerIconGraphic>().rectTransform, new Rect(150, (ribbonHeight - 16) * .5f, 16, 16));
            var countLabel = NotebookLabel(sets.transform, "Набор · " + count + " / " + ProfileLimits.LoadoutSlotCount,
                new Rect(8, 0, 138, ribbonHeight), 20);
            countLabel.name = "Preparation loadout count"; countLabel.resizeTextMinSize = 16;
            countLabel.fontStyle = FontStyle.Bold;
            float readyX = 186;
            var balance = game.Run.Session.Balance;
            bool scout = balance.Waves.Count > 0 && balance.Waves[0].IsHorde;
            if (desktop)
            {
                int structures = game.Run.Workshop.LoadoutEntries.Count(entry => entry?.Kind == AlbumEntryKind.Structure);
                NotebookLabel(ribbon, "Башни · " + (count - structures) + "     Сооружения · " + structures,
                    new Rect(186, 0, 258, ribbonHeight), 16, PlayerUi.Muted);
                readyX = 456;
            }
            else if (scout)
            {
                readyX = width - 190;
                MissionScoutButton(ribbon, "Preparation wave scout", new Rect(180, 2, readyX - 190, ribbonHeight - 4), balance);
            }
            string readinessCopy = ready ? "Готово к защите" :
                reason == UserText.Message("run.error.configuration") ? "Проверь чертежи набора" : GameText.Resolve(reason);
            var readiness = NotebookLabel(ribbon, readinessCopy,
                new Rect(readyX, 0, width - readyX, ribbonHeight), 16,
                ready ? FieldMenuSkin.Accent : NotebookPalette.Red, TextAnchor.MiddleRight);
            readiness.name = "Preparation readiness"; readiness.resizeTextMinSize = Mathf.CeilToInt(14 / Layout.Scale);
            contextHints.Tip(readiness.gameObject, ready ? "Можно начать защиту. Свободные места заполнять не обязательно." : GameText.Resolve(reason));
            var content = ui.Scroll(page, "Preparation loadout scroll", new Rect(left, gridY, width, gridHeight), extent);
            if (extent <= gridHeight + 1)
                content.GetComponentInParent<ScrollRect>().verticalScrollbar.gameObject.SetActive(false);
            var grid = ui.Panel(content, "Assembly preparation loadout", new Rect(0, 0, width - 10, extent), Color.clear);
            var flow = grid.gameObject.AddComponent<GridLayoutGroup>();
            float cardWidth = (width - 10 - gap * (columns - 1)) / columns;
            flow.cellSize = new Vector2(cardWidth, cardHeight); flow.spacing = new Vector2(gap, gap);
            flow.constraint = GridLayoutGroup.Constraint.FixedColumnCount; flow.constraintCount = columns;
            flow.startCorner = GridLayoutGroup.Corner.UpperLeft; flow.startAxis = GridLayoutGroup.Axis.Horizontal;
            flow.childAlignment = TextAnchor.UpperLeft;
            for (int slot = 0; slot < ProfileLimits.LoadoutSlotCount; slot++)
                PreparationAssemblyCard(grid, slot, cardWidth, cardHeight);

            var footer = ui.Panel(page, "Preparation navigation", new Rect(left, footerY, availableWidth, footerHeight), Color.clear);
            float startWidth = desktop ? side : Mathf.Min(224, width * .40f);
            if (desktop)
            {
                float sideX = W - margin - side;
                float summaryTop = footerY - gap - 72;
                float materialsTop = summaryTop - gap - materialHeight;
                var missionSheet = ui.Panel(page, "Preparation mission", new Rect(sideX, gridY, side, materialsTop - gridY - gap), PlayerUi.Paper);
                float sheetHeight = materialsTop - gridY - gap;
                MissionSectionTitle(missionSheet, game.Run.CampaignEnabled ? game.Run.SelectedMission.Title : ui.T("mission"),
                    new Rect(14, 8, side - 28, 40), 23);
                var field = PlayerUi.Graphic<NeutralMissionMapGraphic>(missionSheet, "Preparation mission map",
                    new Rect(8, 52, side - 16, sheetHeight - 106 - (scout ? 40 : 0)));
                field.Bind(game.Run.Session);
                if (scout) MissionScoutButton(missionSheet, "Preparation wave scout",
                    new Rect(12, sheetHeight - 82, side - 24, 34), balance);
                float half = (side - 28) * .5f;
                CampaignMetric(missionSheet, "Preparation waves", PlayerIcon.Flag, WaveCountLabel(game.Run.Session.Balance.Waves.Count), new Rect(14, sheetHeight - 42, half, 28));
                CampaignMetric(missionSheet, "Preparation castle", PlayerIcon.Shield, game.Run.Session.Balance.BaseHealth + " HP", new Rect(14 + half, sheetHeight - 42, half, 28));
                PreparationAssemblyMaterials(new Rect(sideX, materialsTop, side, materialHeight), new Rect(sideX, summaryTop, side, 72));
            }
            else
                PreparationAssemblyMaterials(new Rect(left, materialY, width - 238, materialHeight), new Rect(left + width - 230, materialY, 230, materialHeight));
            float secondaryWidth = desktop ? 164 : footerHeight;
            if (desktop)
            {
                CompactMenuAction(footer, "Edit album", new Rect(0, 0, secondaryWidth, footerHeight), PlayerIcon.Pencil, "Мастерская", () => game.Navigate(PlayerPage.Workshop));
                CompactMenuAction(footer, "Open preparation album", new Rect(secondaryWidth + gap, 0, secondaryWidth, footerHeight), PlayerIcon.Book, "Альбом", game.ShowAlbum);
            }
            else
            {
                ui.IconButton(footer, "Edit album", new Rect(0, 0, secondaryWidth, footerHeight), PlayerIcon.Pencil, "Открыть мастерскую", () => game.Navigate(PlayerPage.Workshop));
                ui.IconButton(footer, "Open preparation album", new Rect(secondaryWidth + gap, 0, secondaryWidth, footerHeight), PlayerIcon.Book, "Открыть альбом чертежей", game.ShowAlbum);
            }
            float statusX = secondaryWidth * 2 + gap * 2;
            status = NotebookLabel(footer, "", new Rect(statusX, 0, availableWidth - startWidth - statusX - gap, footerHeight),
                14, NotebookPalette.Red); status.name = "Preparation status"; status.resizeTextMinSize = 14;
            var start = CompactMenuAction(footer, "Start battle", new Rect(availableWidth - startWidth, 0, startWidth, footerHeight), PlayerIcon.Play,
                "Начать защиту", () => { if (game.Run.CanStartAlbumRun(out _)) game.StartBattle(); }, true);
            start.interactable = ready;
            if (!ready) start.GetComponent<PlayerTooltip>().Message = GameText.Resolve(reason);
            if (namedLoadoutsOpen) BuildNamedLoadouts();
        }

        void PreparationAssemblyCard(Transform parent, int slot, float width, float height)
        {
            var entry = game.Run.Workshop.LoadoutEntries[slot];
            var card = ui.Panel(parent, "Album loadout " + slot, new Rect(0, 0, width, height), PlayerUi.Card);
            string caption = entry == null ? PreparationCopy("empty") : GameText.Resolve(entry.Name);
            bool generator = entry?.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Generator;
            float actionHeight = height >= 176 ? 36 : 32;
            float nameHeight = generator ? 26 : 38;
            float costHeight = generator ? 48 : 22;
            float actionY = height - actionHeight - 8, priceY = actionY - costHeight - 4;
            float nameY = priceY - nameHeight - 2, artHeight = Mathf.Max(24, nameY - 8);
            bool largeCard = height >= 198;
            bool inlineControls = largeCard && width >= (generator ? 272 : 244);
            float actionX = inlineControls ? width - 2 * actionHeight - 20 : (width - actionHeight * 2 - 8) * .5f;
            float priceWidth = inlineControls ? actionX - 16 : width - 16;
            if (inlineControls) priceY = actionY + (actionHeight - costHeight) * .5f;
            Rect illustration = largeCard ? new Rect(8, 56, width - 16, priceY - 68) : new Rect(4, 4, width - 8, artHeight);
            PreparationAssemblyIllustration(card, entry, illustration);
            const float numberSize = 22;
            var number = ui.Panel(card, "Album loadout number " + slot, new Rect(8, 6, numberSize, numberSize), PlayerUi.Card);
            var position = NotebookLabel(number, (slot + 1).ToString(), new Rect(0, 0, numberSize, numberSize),
                16, alignment: TextAnchor.MiddleCenter);
            position.resizeTextMinSize = 14;
            var name = NotebookLabel(card, caption, largeCard ? new Rect(40, 6, width - 50, 44) : new Rect(8, nameY, width - 16, nameHeight),
                largeCard ? 18 : 16, alignment: largeCard ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            name.name = "Album loadout name " + slot; name.fontStyle = FontStyle.Bold;
            name.resizeTextForBestFit = false;
            PreparationAssemblyFitName(name, caption);
            contextHints.Tip(name.gameObject, "Место " + (slot + 1) + ": " + caption);

            var quote = entry == null ? null : game.Run.PreviewAlbumEntry(entry, generator ? MaterialTier.Pencil : game.Drawing.Tier);
            string cost = entry == null ? "—" : quote.Valid ? PreparationCopy("cost", quote.Value.Cost) : PreparationCopy("unavailable");
            if (generator) cost += "\n" + PreparationCopy("pencil_only");
            var price = NotebookLabel(card, cost, new Rect(8, priceY, priceWidth, costHeight), 15,
                quote != null && !quote.Valid ? NotebookPalette.Red : PlayerUi.Ink, inlineControls ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            price.name = "Album loadout cost " + slot; price.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            if (generator) { price.resizeTextForBestFit = false; price.fontSize = 15; }
            // The generator's second line is a construction rule. Keep the full card
            // width for it rather than fitting both lines to a measured price caption.
            if (entry != null && !generator)
            {
                float textWidth = Mathf.Min(priceWidth - 24, price.preferredWidth + 4);
                float priceX = inlineControls ? 32 : (width - textWidth + 22) * .5f;
                PlayerUi.Place(price.rectTransform, new Rect(priceX, priceY, textWidth, costHeight));
                var pencil = PlayerUi.Graphic<PlayerIconGraphic>(card, "Album loadout price pencil " + slot,
                    new Rect(priceX - 22, priceY + 2, 18, 18));
                pencil.Icon = PlayerIcon.Pencil; pencil.Sketch = true; pencil.color = PlayerUi.Ink;
                NotebookInkIcon(pencil, "icon.pencil");
            }
            contextHints.Tip(price.gameObject, cost);

            string slotHint = "Место " + (slot + 1) + ": ";
            ui.IconButton(card, "Replace album entry " + slot, new Rect(actionX, actionY, actionHeight, actionHeight),
                entry == null ? PlayerIcon.Plus : PlayerIcon.Restart,
                slotHint + (entry == null ? "выбрать чертёж из альбома" : "заменить «" + caption + "»") +
                (generator ? "\nГенератор строится только карандашом." : ""), () => OpenAssemblyPicker(slot));
            var clear = ui.IconButton(card, "Clear album entry " + slot,
                new Rect(actionX + actionHeight + 8, actionY, actionHeight, actionHeight), PlayerIcon.Trash,
                slotHint + "убрать чертёж из боевого набора", () => game.AssignAlbumEntry(slot, null));
            clear.interactable = entry != null;
        }

        void PreparationAssemblyFitName(Text text, string fullName)
        {
            text.text = fullName;
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.scaleFactor = 1; settings.resizeTextForBestFit = false; settings.generateOutOfBounds = false;
            var pixels = text.GetGenerationSettings(text.rectTransform.rect.size * Layout.Scale);
            pixels.scaleFactor = 1; pixels.fontSize = Mathf.RoundToInt(text.fontSize * Layout.Scale);
            pixels.resizeTextForBestFit = false; pixels.generateOutOfBounds = false;
            using (var generated = new TextGenerator())
            {
                bool Fits(string value)
                {
                    // Preferred height can still accept a final line whose glyphs are clipped.
                    // Check the actual bounded glyph output at logical and displayed pixel sizes.
                    generated.Populate(value, settings);
                    if (generated.characterCountVisible < value.Length) return false;
                    generated.Populate(value, pixels);
                    return generated.characterCountVisible >= value.Length;
                }
                if (Fits(fullName)) return;
                for (int length = fullName.Length - 1; length >= 0; length--)
                {
                    text.text = fullName.Substring(0, length).TrimEnd() + "…";
                    if (Fits(text.text)) return;
                }
            }
        }

        void PreparationAssemblyMaterials(Rect bounds, Rect summaryBounds)
        {
            float summaryWidth = summaryBounds.width, materialWidth = bounds.width;
            var materials = ui.Panel(page, "Preparation material", bounds, PlayerUi.Card);
            const float buttonHeight = 36;
            bool modernization = game.Run.Session.Balance.Mechanics.FieldModernization;
            var description = NotebookLabel(materials, PreparationCopy("material") + " " + ui.T("material_" + (int)game.Drawing.Tier),
                new Rect(10, 0, materialWidth - 20, 23), 14);
            description.name = "Preparation material description";
            description.resizeTextMinSize = Mathf.CeilToInt(14 / Layout.Scale);
            if (modernization)
            {
                description.text = "Базовое строительство · карандаш\nРазвитие и цвет — на поле.";
                PlayerUi.Place(description.rectTransform, new Rect(12, 8, materialWidth - 24, bounds.height - 16));
                description.resizeTextForBestFit = false; description.fontSize = 15;
            }
            float buttonWidth = (materialWidth - 28) / 3;
            string[] names = { "Карандаш", "Ручка", "Фломастер" };
            for (int i = 0; i < (modernization ? 0 : 3); i++)
            {
                int tier = i;
                var button = CompactMenuAction(materials, "Starting material " + i,
                    new Rect(8 + i * (buttonWidth + 6), 24, buttonWidth, buttonHeight), PlayerIcon.Pencil, names[i],
                    () => SelectPreparationMaterial((MaterialTier)tier));
                if (buttonWidth < 128)
                {
                    button.GetComponentInChildren<PlayerIconGraphic>().enabled = false;
                    var caption = button.GetComponentInChildren<Text>();
                    PlayerUi.Place(caption.rectTransform, new Rect(4, 2, buttonWidth - 8, buttonHeight - 4));
                    caption.alignment = TextAnchor.MiddleCenter;
                }
                ui.MarkSelected(button, (int)game.Drawing.Tier == i);
            }
            var summary = ui.Panel(page, "Preparation summary", summaryBounds, PlayerUi.Card);
            var ink = NotebookLabel(summary, PreparationCopy("starting_ink", game.Run.Session.Balance.StartingInk),
                new Rect(10, 6, summaryWidth - 20, 26), 17);
            ink.name = "Preparation starting ink"; ink.resizeTextMinSize = 15;
            var inkHint = NotebookLabel(summary, "В начале боя", new Rect(10, 32, summaryWidth - 20, 22), 14, PlayerUi.Muted);
            inkHint.resizeTextMinSize = 14;
        }

        void PreparationAssemblyIllustration(Transform parent, AlbumEntry entry, Rect bounds)
            => StandardAssemblyIllustration.Draw(parent, entry, bounds, "Preparation certified assembly");
    }
}
