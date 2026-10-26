using System;
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
            Rect landscape = NotebookPageBackdrop("Art/Player/PreparationLandscape");
            // Keep the whole illustration, but align this screen's scenery to the header.
            // Centering a 16:9 page in 4:3 otherwise leaves a blank band above the castle.
            landscape.y = 0;
            PlayerUi.Place(page.Find("Notebook landscape").GetComponent<RectTransform>(), landscape);
            NotebookScreenHeader(PreparationCopy("title"), () => game.Navigate(PlayerPage.Map),
                ProgressCopy(), true, currencyCoin: true);
            progress = page.Find("Notebook header/Mission heading").GetComponentInChildren<Text>();
            progress.name = "Player progress";

            float scale = Mathf.Min(W / 1600f, H / 900f);
            float margin = Mathf.Max(16, 28 * scale), gap = Mathf.Max(8, 12 * scale);
            float width = W - margin * 2;
            bool inlineFooter = W >= 1250;
            float footerHeight = inlineFooter ? Mathf.Max(62, 68 * scale) : Mathf.Max(44, 56 * scale);
            float footerY = H - margin - footerHeight;
            float materialHeight = Mathf.Max(58, 72 * scale), materialY = inlineFooter ? footerY : footerY - gap - materialHeight;
            float ribbonHeight = Mathf.Max(36, 56 * scale), ribbonY = landscape.height * (207f / 941f);
            float gridY = ribbonY + ribbonHeight + gap;
            float gridHeight = materialY - gap - gridY;
            float cardWidth = (width - gap * 3) / 4, cardHeight = (gridHeight - gap) / 2;

            float ribbonWidth = width * .46f;
            var ribbon = ui.Panel(page, "Assembly preparation ribbon", new Rect((W - ribbonWidth) * .5f, ribbonY, ribbonWidth, ribbonHeight), PlayerUi.Card);
            NotebookLabel(ribbon, PreparationCopy("ribbon"), new Rect(8, 0, ribbonWidth - 16, ribbonHeight),
                Mathf.Max(25, Mathf.RoundToInt(37 * scale)), alignment: TextAnchor.MiddleCenter);
            var grid = ui.Panel(page, "Assembly preparation loadout", new Rect(margin, gridY, width, gridHeight), Color.clear);
            var flow = grid.gameObject.AddComponent<GridLayoutGroup>();
            flow.cellSize = new Vector2(cardWidth, cardHeight); flow.spacing = new Vector2(gap, gap);
            flow.constraint = GridLayoutGroup.Constraint.FixedColumnCount; flow.constraintCount = 4;
            flow.startCorner = GridLayoutGroup.Corner.UpperLeft; flow.startAxis = GridLayoutGroup.Axis.Horizontal;
            flow.childAlignment = TextAnchor.UpperLeft;
            for (int slot = 0; slot < ProfileLimits.LoadoutSlotCount; slot++)
                PreparationAssemblyCard(grid, slot, cardWidth, cardHeight, scale);

            bool ready = game.Run.CanStartAlbumRun(out var reason);
            int count = game.Run.Workshop.LoadoutEntries.Count(entry => entry != null);
            var footer = ui.Panel(page, "Preparation navigation", new Rect(margin, footerY, width, footerHeight), Color.clear);
            float workshopWidth = Mathf.Max(180, 246 * scale), albumWidth = Mathf.Max(136, 178 * scale);
            float startWidth = Mathf.Max(225, 300 * scale);
            float statusX = workshopWidth + albumWidth + gap * 2;
            float materialX = inlineFooter ? margin + statusX : margin;
            float materialWidth = inlineFooter ? width - statusX - startWidth - gap : width;
            PreparationAssemblyMaterials(new Rect(materialX, materialY, materialWidth, inlineFooter ? footerHeight : materialHeight),
                scale, count, ready ? "" : GameText.Resolve(reason));
            PreparationFooterCaption(NotebookAction(footer, "Edit album", new Rect(0, 0, workshopWidth, footerHeight), PlayerIcon.Pencil,
                "В мастерскую", () => game.Navigate(PlayerPage.Workshop)));
            var album = NotebookAction(footer, "Open preparation album", new Rect(workshopWidth + gap, 0, albumWidth, footerHeight), PlayerIcon.Book,
                "Альбом", game.ShowAlbum);
            PreparationFooterCaption(album);
            NotebookControlIcon(album, "icon.help");
            status = NotebookLabel(page, "", new Rect(margin, ribbonY - 26, width, 24),
                17, NotebookPalette.Red, TextAnchor.MiddleCenter); status.name = "Preparation status";
            var start = NotebookAction(footer, "Start battle", new Rect(width - startWidth, 0, startWidth, footerHeight), PlayerIcon.Play,
                "Начать защиту", () => { if (game.Run.CanStartAlbumRun(out _)) game.StartBattle(); }, true);
            PreparationFooterCaption(start, true);
            start.interactable = ready;
            if (!ready) start.GetComponent<PlayerTooltip>().Message = GameText.Resolve(reason);
        }

        void PreparationFooterCaption(Button button, bool primary = false)
        {
            var bounds = ((RectTransform)button.transform).rect;
            float symbolSize = Mathf.Min(36, bounds.height * .56f);
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            PlayerUi.Place(symbol.rectTransform, new Rect(10, (bounds.height - symbolSize) * .5f, symbolSize, symbolSize));
            foreach (var illustration in symbol.GetComponentsInChildren<Image>())
                PlayerUi.Place(illustration.rectTransform, NotebookFitRect(illustration.sprite, new Rect(0, 0, symbolSize, symbolSize)));
            var copy = button.GetComponentInChildren<Text>();
            float lineHeight = Mathf.Min(30, bounds.height - 10);
            PlayerUi.Place(copy.rectTransform, new Rect(symbolSize + 18, (bounds.height - lineHeight) * .5f,
                bounds.width - symbolSize - 26, lineHeight));
            copy.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            copy.resizeTextMaxSize = primary ? 26 : 22;
        }

        void PreparationAssemblyCard(Transform parent, int slot, float width, float height, float scale)
        {
            var entry = game.Run.Workshop.LoadoutEntries[slot];
            var card = ui.Panel(parent, "Album loadout " + slot, new Rect(0, 0, width, height), PlayerUi.Card);
            string caption = entry == null ? PreparationCopy("empty") : GameText.Resolve(entry.Name);
            bool generator = entry?.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Generator;
            float actionHeight = Mathf.Max(32, 34 * scale);
            float nameHeight = caption.Length > 26 ? Mathf.Max(54, 58 * scale) : Mathf.Max(28, 34 * scale);
            float costHeight = generator ? 50 : 24;
            float artHeight = Mathf.Max(16, height - actionHeight - nameHeight - costHeight - 18);
            PreparationAssemblyIllustration(card, entry, new Rect(4, 5, width - 8, artHeight));
            float numberSize = Mathf.Max(28, 40 * scale);
            var number = ui.Panel(card, "Album loadout number " + slot, new Rect(8, 6, numberSize, numberSize), PlayerUi.Card);
            NotebookLabel(number, (slot + 1).ToString(), new Rect(0, 0, numberSize, numberSize),
                Mathf.Max(21, Mathf.RoundToInt(29 * scale)), alignment: TextAnchor.MiddleCenter);
            var name = NotebookLabel(card, caption,
                new Rect(8, artHeight + 7, width - 16, nameHeight), Mathf.Max(23, Mathf.RoundToInt(27 * scale)), alignment: TextAnchor.MiddleCenter);
            name.name = "Album loadout name " + slot; name.fontStyle = FontStyle.Bold;
            name.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            name.verticalOverflow = VerticalWrapMode.Truncate; name.horizontalOverflow = HorizontalWrapMode.Wrap;

            var quote = entry == null ? null : game.Run.PreviewAlbumEntry(entry, generator ? MaterialTier.Pencil : game.Drawing.Tier);
            string cost = entry == null ? "—" : quote.Valid ? PreparationCopy("cost", quote.Value.Cost) : PreparationCopy("unavailable");
            if (generator) cost += "\n" + PreparationCopy("pencil_only");
            var price = NotebookLabel(card, cost, new Rect(8, artHeight + nameHeight + 7, width - 16, costHeight), 20,
                quote != null && !quote.Valid ? NotebookPalette.Red : PlayerUi.Ink, TextAnchor.MiddleCenter);
            price.name = "Album loadout cost " + slot; price.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            if (generator) { price.resizeTextForBestFit = false; price.fontSize = 18; }
            // The generator's second line is a construction rule. Keep the full card
            // width for it rather than fitting both lines to a measured price caption.
            if (entry != null && !generator)
            {
                float textWidth = Mathf.Min(width - 48, price.preferredWidth + 4);
                float priceX = (width - textWidth + 26) * .5f;
                float priceY = artHeight + nameHeight + 7;
                PlayerUi.Place(price.rectTransform, new Rect(priceX, priceY, textWidth, costHeight));
                var pencil = PlayerUi.Graphic<PlayerIconGraphic>(card, "Album loadout price pencil " + slot,
                    new Rect(priceX - 26, priceY + 1, 22, 22));
                pencil.Icon = PlayerIcon.Pencil; pencil.Sketch = true; pencil.color = PlayerUi.Ink;
                NotebookInkIcon(pencil, "icon.pencil");
            }

            float clearWidth = actionHeight, replaceWidth = width - 24 - clearWidth;
            NotebookAction(card, "Replace album entry " + slot, new Rect(8, height - actionHeight - 8, replaceWidth, actionHeight),
                PlayerIcon.Restart, PreparationCopy(entry == null ? "choose" : "replace"), () => OpenAssemblyPicker(slot));
            var clear = ui.IconButton(card, "Clear album entry " + slot,
                new Rect(width - clearWidth - 8, height - actionHeight - 8, clearWidth, actionHeight), PlayerIcon.Trash,
                PreparationCopy("clear"), () => game.AssignAlbumEntry(slot, null));
            clear.interactable = entry != null;
        }

        void PreparationAssemblyMaterials(Rect bounds, float scale, int count, string readiness)
        {
            float summaryWidth = Mathf.Max(170, 230 * scale), gap = Mathf.Max(8, 12 * scale);
            float materialWidth = bounds.width - summaryWidth - gap;
            var materials = ui.Panel(page, "Preparation material", new Rect(bounds.x, bounds.y, materialWidth, bounds.height), PlayerUi.Card);
            float buttonHeight = Mathf.Max(32, bounds.height - 26);
            var description = NotebookLabel(materials, PreparationCopy("material") + " " + ui.T("material_" + (int)game.Drawing.Tier),
                new Rect(10, 0, materialWidth - 20, 23), 17);
            description.name = "Preparation material description";
            description.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            float buttonWidth = (materialWidth - 28) / 3;
            string[] names = { "Карандаш", "Ручка", "Фломастер" };
            for (int i = 0; i < 3; i++)
            {
                int tier = i;
                var button = NotebookAction(materials, "Starting material " + i,
                    new Rect(8 + i * (buttonWidth + 6), 24, buttonWidth, buttonHeight), PlayerIcon.Pencil, names[i],
                    () => SelectPreparationMaterial((MaterialTier)tier));
                ui.MarkSelected(button, (int)game.Drawing.Tier == i);
            }
            var summary = ui.Panel(page, "Preparation summary", new Rect(bounds.xMax - summaryWidth, bounds.y, summaryWidth, bounds.height), PlayerUi.Card);
            NotebookLabel(summary, PreparationCopy("starting_ink", game.Run.Session.Balance.StartingInk),
                new Rect(8, 1, summaryWidth - 16, readiness.Length > 0 ? 18 : 28), 20).name = "Preparation starting ink";
            NotebookLabel(summary, PreparationCopy("count", count), new Rect(8, readiness.Length > 0 ? 18 : 30, summaryWidth - 16, readiness.Length > 0 ? 20 : 24), 19).name = "Preparation loadout count";
            if (readiness.Length > 0)
                NotebookLabel(summary, readiness, new Rect(8, 38, summaryWidth - 16, bounds.height - 38), 16,
                    NotebookPalette.Red).name = "Preparation readiness";
        }

        void PreparationAssemblyIllustration(Transform parent, AlbumEntry entry, Rect bounds)
        {
            if (entry == null) return;
            if (entry.Kind == AlbumEntryKind.Legacy)
            { NotebookBlueprintIllustration(parent, entry.LegacyBlueprint, bounds); return; }
            if (entry.Kind == AlbumEntryKind.Tower || entry.Kind == AlbumEntryKind.Structure)
            {
                var exact = PlayerUi.Graphic<WeaponAssemblyGraphic>(parent, "Preparation certified assembly", bounds);
                try { exact.SetRecipe(entry.Snapshot); return; }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                catch (NotSupportedException) { }
                WorkshopUi.RemoveView(exact.gameObject);
            }
            var schematicBounds = new Rect(bounds.x, bounds.y, bounds.width, Mathf.Max(1, bounds.height - 18));
            if (entry.Kind == AlbumEntryKind.Tower)
            {
                string mechanism = entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id;
                string family = mechanism.StartsWith("core.", StringComparison.Ordinal) ? mechanism.Substring("core.".Length) : mechanism;
                var art = PlayerUi.Graphic<NotebookArtGraphic>(parent, "Preparation weapon family schematic", schematicBounds);
                art.Bind("ui.weapon." + family);
            }
            else if (entry.Kind == AlbumEntryKind.Structure && entry.Snapshot.Recipe.Structure.Family != AssemblyStructureFamily.Generator)
            {
                var family = entry.Snapshot.Recipe.Structure.Family;
                var art = PlayerUi.Graphic<NotebookArtGraphic>(parent, "Preparation support schematic", schematicBounds);
                art.Bind(family == AssemblyStructureFamily.Wall ? "object.barricade-site" : family == AssemblyStructureFamily.Repair ? "object.repair" : "object.tar");
            }
            else
            {
                var icon = PlayerUi.Graphic<WorkshopIconGraphic>(parent, "Preparation family schematic", schematicBounds);
                icon.Icon = entry.Kind == AlbumEntryKind.Tower ? WorkshopIcon.Tower : WorkshopIcon.Ink; icon.color = PlayerUi.Blue;
            }
            NotebookLabel(parent, PreparationCopy("neutral"), new Rect(bounds.x, bounds.yMax - 18, bounds.width, 18), 15,
                PlayerUi.Muted, TextAnchor.MiddleCenter).name = "Preparation schematic caption";
        }
    }
}
