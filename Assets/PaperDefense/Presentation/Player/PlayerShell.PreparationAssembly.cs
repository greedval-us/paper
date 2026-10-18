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
            NotebookPageBackdrop("Art/Player/PreparationLandscape");
            NotebookScreenHeader(PreparationCopy("title"), () => game.Navigate(PlayerPage.Map),
                ProgressCopy(), true);
            progress = page.Find("Notebook header/Mission heading").GetComponentInChildren<Text>();
            progress.name = "Player progress";

            float scale = Mathf.Min(W / 1600f, H / 1000f);
            float margin = Mathf.Max(16, 28 * scale), gap = Mathf.Max(8, 12 * scale);
            float width = W - margin * 2;
            float footerHeight = Mathf.Max(44, 56 * scale), footerY = H - margin - footerHeight;
            float materialHeight = Mathf.Max(70, 78 * scale), materialY = footerY - gap - materialHeight;
            float ribbonHeight = Mathf.Max(36, 48 * scale), ribbonY = Mathf.Max(90, H * .15f);
            float gridY = ribbonY + ribbonHeight + gap;
            float gridHeight = materialY - gap - gridY;
            float cardWidth = (width - gap * 3) / 4, cardHeight = (gridHeight - gap) / 2;

            var ribbon = ui.Panel(page, "Assembly preparation ribbon", new Rect(margin + width * .19f, ribbonY, width * .62f, ribbonHeight), PlayerUi.Card);
            NotebookLabel(ribbon, PreparationCopy("ribbon"), new Rect(8, 0, width * .62f - 16, ribbonHeight),
                Mathf.Max(22, Mathf.RoundToInt(32 * scale)), alignment: TextAnchor.MiddleCenter);
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
            PreparationAssemblyMaterials(new Rect(margin, materialY, width, materialHeight), scale, count, ready ? "" : GameText.Resolve(reason));

            var footer = ui.Panel(page, "Preparation navigation", new Rect(margin, footerY, width, footerHeight), Color.clear);
            float workshopWidth = Mathf.Max(190, 230 * scale), albumWidth = Mathf.Max(142, 190 * scale);
            float startWidth = Mathf.Max(230, 294 * scale);
            NotebookAction(footer, "Edit album", new Rect(0, 0, workshopWidth, footerHeight), PlayerIcon.Pencil,
                "В мастерскую", () => game.Navigate(PlayerPage.Workshop));
            NotebookAction(footer, "Open preparation album", new Rect(workshopWidth + gap, 0, albumWidth, footerHeight), PlayerIcon.Book,
                "Альбом", game.ShowAlbum);
            float statusX = workshopWidth + albumWidth + gap * 2;
            status = NotebookLabel(footer, "", new Rect(statusX, 0, Mathf.Max(0, width - startWidth - gap - statusX), footerHeight),
                17, NotebookPalette.Red, TextAnchor.MiddleCenter); status.name = "Preparation status";
            var start = NotebookAction(footer, "Start battle", new Rect(width - startWidth, 0, startWidth, footerHeight), PlayerIcon.Play,
                "Начать защиту", () => { if (game.Run.CanStartAlbumRun(out _)) game.StartBattle(); }, true);
            start.interactable = ready;
            if (!ready) start.GetComponent<PlayerTooltip>().Message = GameText.Resolve(reason);
        }

        void PreparationAssemblyCard(Transform parent, int slot, float width, float height, float scale)
        {
            var entry = game.Run.Workshop.LoadoutEntries[slot];
            var card = ui.Panel(parent, "Album loadout " + slot, new Rect(0, 0, width, height), PlayerUi.Card);
            string caption = entry == null ? PreparationCopy("empty") : GameText.Resolve(entry.Name);
            bool generator = entry?.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Generator;
            float actionHeight = Mathf.Max(32, 40 * scale);
            float nameHeight = caption.Length > 26 ? Mathf.Max(54, 58 * scale) : Mathf.Max(32, 36 * scale);
            float costHeight = generator ? 50 : 28;
            float artHeight = Mathf.Max(16, height - actionHeight - nameHeight - costHeight - 20);
            PreparationAssemblyIllustration(card, entry, new Rect(10, 8, width - 20, artHeight));
            var number = ui.Panel(card, "Album loadout number " + slot, new Rect(8, 6, 28, 28), PlayerUi.Card);
            NotebookLabel(number, (slot + 1).ToString(), new Rect(0, 0, 28, 28), 21, alignment: TextAnchor.MiddleCenter);
            var name = NotebookLabel(card, caption,
                new Rect(8, artHeight + 10, width - 16, nameHeight), 22, alignment: TextAnchor.MiddleCenter);
            name.name = "Album loadout name " + slot; name.fontStyle = FontStyle.Bold;
            name.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            name.verticalOverflow = VerticalWrapMode.Truncate; name.horizontalOverflow = HorizontalWrapMode.Wrap;

            var quote = entry == null ? null : game.Run.PreviewAlbumEntry(entry, generator ? MaterialTier.Pencil : game.Drawing.Tier);
            string cost = entry == null ? "—" : quote.Valid ? PreparationCopy("cost", quote.Value.Cost) : PreparationCopy("unavailable");
            if (generator) cost += "\n" + PreparationCopy("pencil_only");
            var price = NotebookLabel(card, cost, new Rect(8, artHeight + nameHeight + 10, width - 16, costHeight), 20,
                quote != null && !quote.Valid ? NotebookPalette.Red : PlayerUi.Ink, TextAnchor.MiddleCenter);
            price.name = "Album loadout cost " + slot; price.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            if (generator) { price.resizeTextForBestFit = false; price.fontSize = 18; }

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
            float summaryWidth = Mathf.Max(230, 280 * scale), gap = Mathf.Max(8, 12 * scale);
            float materialWidth = bounds.width - summaryWidth - gap;
            var materials = ui.Panel(page, "Preparation material", new Rect(bounds.x, bounds.y, materialWidth, bounds.height), PlayerUi.Card);
            float captionWidth = Mathf.Max(126, 160 * scale), buttonHeight = Mathf.Max(34, 42 * scale);
            NotebookLabel(materials, PreparationCopy("material"), new Rect(10, 4, captionWidth - 14, buttonHeight), 20);
            float buttonWidth = (materialWidth - captionWidth - 22) / 3;
            string[] names = { "Карандаш", "Ручка", "Фломастер" };
            for (int i = 0; i < 3; i++)
            {
                int tier = i;
                var button = NotebookAction(materials, "Starting material " + i,
                    new Rect(captionWidth + i * (buttonWidth + 6), 4, buttonWidth, buttonHeight), PlayerIcon.Pencil, names[i],
                    () => SelectPreparationMaterial((MaterialTier)tier));
                ui.MarkSelected(button, (int)game.Drawing.Tier == i);
            }
            var description = NotebookLabel(materials, ui.T("material_" + (int)game.Drawing.Tier),
                new Rect(10, buttonHeight + 7, materialWidth - 20, bounds.height - buttonHeight - 10), 17);
            description.name = "Preparation material description";
            description.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            var summary = ui.Panel(page, "Preparation summary", new Rect(bounds.xMax - summaryWidth, bounds.y, summaryWidth, bounds.height), PlayerUi.Card);
            NotebookLabel(summary, PreparationCopy("starting_ink", game.Run.Session.Balance.StartingInk),
                new Rect(10, 2, summaryWidth - 20, 26), 21).name = "Preparation starting ink";
            NotebookLabel(summary, PreparationCopy("count", count), new Rect(10, 28, summaryWidth - 20, 24), 19).name = "Preparation loadout count";
            if (readiness.Length > 0)
                NotebookLabel(summary, readiness, new Rect(10, 49, summaryWidth - 20, bounds.height - 51), 16,
                    NotebookPalette.Red).name = "Preparation readiness";
        }

        void PreparationAssemblyIllustration(Transform parent, AlbumEntry entry, Rect bounds)
        {
            if (entry == null) return;
            if (entry.Kind == AlbumEntryKind.Legacy)
            { NotebookBlueprintIllustration(parent, entry.LegacyBlueprint, bounds); return; }
            if (entry.Kind == AlbumEntryKind.Tower)
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
