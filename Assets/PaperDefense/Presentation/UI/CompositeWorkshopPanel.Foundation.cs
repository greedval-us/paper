using System;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class CompositeWorkshopPanel
    {
        void Foundations(Transform parent, float w, float h)
        {
            bool inlineHeading = w >= 850;
            ui.Label(parent, "Composite foundation heading", "Выбери основу башни", new Rect(18, 2, inlineHeading ? 340 : w - 36, 42), 27, true);
            ui.Label(parent, "Composite foundation hint", "Основа определяет число креплений и вместимость башни.",
                inlineHeading ? new Rect(372, 8, w - 390, 36) : new Rect(18, 40, w - 36, 26), w < 570 ? 15 : 17);
            var frames = DraftSession.Catalog.Definitions.Where(part => part.Category == ComponentCategory.Frame).ToArray();
            float catalogY = inlineHeading ? 56 : 70, catalogHeight = h - catalogY - 12;
            float cardHeight = Mathf.Max(184, (catalogHeight - 12) * .5f);
            var list = new CatalogScrollList(parent, "Composite foundation catalog", new Rect(18, catalogY, w - 36, catalogHeight), frames.Length, 2, cardHeight, 12);
            float cardWidth = (w - 60) / 2;
            for (int i = 0; i < frames.Length; i++)
            {
                var part = frames[i]; var card = ui.Panel(list.Content, "Composite frame " + part.Id, new Rect(i % 2 * (cardWidth + 12), i / 2 * (cardHeight + 12), cardWidth, cardHeight), WorkshopUi.Paper);
                bool selectedFrame = DraftSession.PreviewRecipe.Tower.Foundation.Frame.Id == part.Id;
                var outline = ReferenceDetail(card, "Composite frame border", new Rect(0, 0, cardWidth, cardHeight), FoundationDetailGraphic.Kind.Frame);
                outline.Selected = selectedFrame; outline.StrokeWidth = selectedFrame ? 2.4f : 1.2f;
                ui.Label(card, "Composite frame name", FrameTitle(part), new Rect(14, 2, cardWidth - 46, 32), 23, true);
                if (selectedFrame) ReferenceDetail(card, "Composite selected frame mark", new Rect(cardWidth - 38, 0, 38, 38), FoundationDetailGraphic.Kind.SelectedCorner);
                float imageY = 38, imageHeight = cardHeight - 124;
                float descriptionWidth = cardWidth * .36f;
                var description = ui.Label(card, "Composite frame description", FrameDescription(part), new Rect(14, imageY, descriptionWidth - 14, imageHeight), cardWidth < 270 ? 14 : 16);
                description.alignment = TextAnchor.UpperLeft;
                var selected = DraftSession.PreviewRecipe.Tower.Foundation;
                FoundationPicture(card, new FoundationRecipe(new PartRef(part.Id, part.Revision), selected.Hull, selected.Armor, selected.Feed, selected.Turn),
                    "Composite frame illustration", new Rect(descriptionWidth, imageY, cardWidth - descriptionWidth - 10, imageHeight));
                int mounts = (int)part.Get(AssemblyStat.MountCount, ModifierOperation.Set);
                ui.Label(card, "Composite frame values", "Крепления:", new Rect(14, cardHeight - 80, 72, 24), cardWidth < 270 ? 13 : 15);
                var pips = ReferenceDetail(card, "Composite frame mount pips", new Rect(88, cardHeight - 75, 42, 17), FoundationDetailGraphic.Kind.MountPips);
                pips.Count = mounts; pips.Total = 3;
                var capacity = ui.Label(card, "Composite frame capacity", "Вместимость: " + part.Get(AssemblyStat.Capacity, ModifierOperation.Set),
                    new Rect(cardWidth * .55f, cardHeight - 80, cardWidth * .45f - 12, 24), cardWidth < 270 ? 13 : 15);
                capacity.alignment = TextAnchor.MiddleRight;
                bool owned = DraftSession.IsOwned(part.Id);
                string choice = owned ? selectedFrame ? "Выбрано" : T("choose") : "Ур. " + part.RequiredLevel + " · " + part.UnlockGold + " золота";
                var choose = ui.Button(card, "Composite choose frame " + part.Id, new Rect(14, cardHeight - 51, cardWidth - 28, 44), choice, () => {
                    if (owned) DraftSession.ApplyPreset(FoundationPreset(part), -1);
                    else DraftSession.OpenFoundation();
                    feedback = ""; Refresh();
                }, owned ? WorkshopIcon.Check : WorkshopIcon.None);
                if (!owned)
                {
                    ReferenceDetail(choose.transform, "Composite foundation unlock gold", new Rect(10, 6, 30, 30), FoundationDetailGraphic.Kind.GoldStack);
                    var caption = choose.GetComponentInChildren<Text>();
                    caption.rectTransform.offsetMin = new Vector2(44, 4);
                }
                ((WorkshopButton)choose).SetDecoration(outline);
                ui.SetSelected(choose, selectedFrame);
            }
        }

        string FrameTitle(ComponentDefinition part)
            => ui.Resolve(part.Name).Replace(" рамка", "").Replace("Рамка ", "");
        static string FrameDescription(ComponentDefinition part)
        {
            switch (part.Id)
            {
                case "frame.heavy": return "Прочная основа.\nДля большой нагрузки.";
                case "frame.precision": return "Одно крепление.\nДля точной стрельбы.";
                case "frame.battery": return "Три крепления\nна общей площадке.";
                default: return "Надёжная основа.\nДля начала обороны.";
            }
        }
        static FoundationDetailGraphic ReferenceDetail(Transform parent, string name, Rect bounds, FoundationDetailGraphic.Kind kind)
        {
            var graphic = WorkshopUi.Graphic<FoundationDetailGraphic>(parent, name, bounds);
            graphic.Shape = kind; graphic.color = WorkshopUi.Ink; return graphic;
        }

        void FoundationSummary(Transform parent, Rect bounds)
        {
            var sheet = ui.Panel(parent, "Composite summary", bounds, WorkshopUi.Paper);
            float w = bounds.width, h = bounds.height;
            ui.Label(sheet, "Composite preview title", "Предпросмотр", new Rect(18, 4, w - 36, 42), 29, true);
            ui.Label(sheet, "Composite foundation preview hint", "Так будет выглядеть выбранная основа.", new Rect(18, 44, w - 36, 32), 16);
            var art = new Rect(12, 80, w - 24, Mathf.Max(120, h - 332));
            WorkshopScenery(sheet, "Composite preview scenery", art, .8f);
            FoundationPicture(sheet, DraftSession.PreviewRecipe.Tower.Foundation, "Composite certified foundation preview", art);
            var foundation = DraftSession.PreviewRecipe.Tower.Foundation;
            DraftSession.Catalog.TryGet(foundation.Frame.Id, foundation.Frame.Revision, out var frame);
            var composition = AssemblyVisualComposer.ComposeFoundation(foundation);
            var visible = AssemblyPreviewPose.VisibleBounds(composition);
            float scale = .9f * Mathf.Min(art.width / visible.width, art.height / visible.height);
            foreach (int slot in composition.Fragments.Where(fragment => fragment.SlotIndex >= 0).Select(fragment => fragment.SlotIndex).Distinct())
            {
                var seat = composition.Fragments.Where(fragment => fragment.SlotIndex == slot).ToArray();
                var points = seat.SelectMany(fragment => new[] { fragment.Transform(fragment.Region.min), fragment.Transform(fragment.Region.max) }).ToArray();
                var point = new Vector2(points.Average(value => value.x), points.Max(value => value.y));
                float x = art.center.x + (point.x - visible.center.x) * scale, y = art.center.y - (point.y - visible.center.y) * scale;
                ui.Label(sheet, "Composite mount callout " + slot, T("select_mount", slot + 1), new Rect(Mathf.Clamp(x - 75, 18, w - 154), Mathf.Max(78, y - 66), 136, 24), 16);
                ReferenceDetail(sheet, "Composite mount arrow " + slot, new Rect(x - 26, Mathf.Max(102, y - 40), 32, 36), FoundationDetailGraphic.Kind.Arrow);
            }
            var statistics = ui.Panel(sheet, "Composite foundation characteristics", new Rect(12, h - 244, w - 24, 172), WorkshopUi.Surface);
            ui.Label(statistics, "Composite foundation characteristics title", "Характеристики", new Rect(14, 0, w - 52, 34), 23, true);
            float column = (w - 40) / 4;
            string[] titles = { "Крепления", "Вместимость", "Прочность", "Цена основы" };
            var resolved = DraftSession.Resolved; var metrics = DraftSession.PreviewMetrics;
            int cost = run.PreviewFoundationCost(DraftSession.PreviewEntry.Snapshot);
            string[] values = { frame.Get(AssemblyStat.MountCount, ModifierOperation.Set).ToString(),
                (resolved?.Capacity ?? metrics?.Capacity ?? 0).ToString(), (resolved?.Health ?? metrics?.Health ?? 0).ToString("0.#") + " HP", cost + " штр." };
            string[] names = { "mount", "capacity", "health", "cost" };
            string[] captions = { "Слоты оружия", "Предел нагрузки", "Здоровье основы", "Постройка за штрихи" };
            WorkshopIcon[] icons = { WorkshopIcon.Settings, WorkshopIcon.Rectangle, WorkshopIcon.Shield, WorkshopIcon.Pencil };
            for (int i = 0; i < 4; i++)
            {
                float x = 8 + i * column;
                if (i > 0) ReferenceDetail(statistics, "Composite foundation metric divider " + i, new Rect(x - 3, 42, 1, 116), FoundationDetailGraphic.Kind.Divider);
                var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(statistics, "Composite foundation " + names[i] + " icon", new Rect(x + column * .5f - 18, 38, 36, 36)); icon.Icon = icons[i]; icon.color = WorkshopUi.Ink;
                var title = ui.Label(statistics, "Composite foundation " + names[i] + " title", titles[i], new Rect(x, 76, column - 4, 24), w < 460 ? 13 : 16); title.alignment = TextAnchor.MiddleCenter;
                var value = ui.Label(statistics, "Composite foundation " + names[i] + " value", values[i], new Rect(x, 100, column - 4, 44), w < 460 ? 26 : 32, true); value.alignment = TextAnchor.MiddleCenter;
                value.resizeTextForBestFit = true; value.resizeTextMinSize = 20; value.resizeTextMaxSize = w < 460 ? 26 : 32;
                var caption = ui.Label(statistics, i == 3 ? "Composite load cost" : "Composite foundation " + names[i] + " caption", captions[i], new Rect(x, 148, column - 4, 20), w < 460 ? 11 : 14, color: WorkshopUi.Muted);
                caption.alignment = TextAnchor.MiddleCenter;
            }
            ui.Button(sheet, "Composite edit foundation", new Rect(24, h - 62, w - 48, 50), "Собрать свою основу", () => { DraftSession.OpenFoundation(); Refresh(); }, WorkshopIcon.Settings);
        }

    }
}
