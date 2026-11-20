using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public enum AlbumFilter { All, Towers, Structures, Standard, Mine }

    /// <summary>One entry album for browsing, editing and assigning permanent page identities.</summary>
    public sealed class AlbumPanel
    {
        readonly RunController run;
        readonly WorkshopUi ui;
        readonly Action<AlbumEntry> edit;
        readonly Action close, assigned;
        readonly int? destinationSlot;
        float width = 1024, height = 768;
        string feedback = "";
        bool refreshing;
        GameObject confirmation;
        RectTransform workspace;
        InputField search;
        Button clearSearch;
        float searchPosition = 1;
        ScrollRect catalogScroll;
        AlbumFilter renderedFilter;
        readonly Dictionary<AlbumFilter, float> catalogPositions = new Dictionary<AlbumFilter, float>();
        public RectTransform Root { get; }
        public AlbumFilter Filter { get; private set; }
        public string SelectedId { get; private set; }
        public string ReferenceId { get; private set; }
        public string SearchQuery { get; private set; }
        public float CatalogScrollPosition
        {
            get => ReadCatalogPosition();
            set
            {
                float position = catalogScroll != null && catalogScroll.content.rect.height <= catalogScroll.viewport.rect.height + 1 ? 1 : Mathf.Clamp01(value);
                if (HasQuery) searchPosition = position; else catalogPositions[Filter] = position;
                if (catalogScroll != null && renderedFilter == Filter) catalogScroll.verticalNormalizedPosition = position;
            }
        }
        string T(string key, params object[] args) => ui.Resolve(UserText.Message("assembly.album." + key, args));
        AlbumEntry Selected => run.Workshop.Entries.FirstOrDefault(entry => entry.Id == SelectedId);
        AlbumEntry Reference => run.Workshop.Entries.FirstOrDefault(entry => entry.Id == ReferenceId);
        float PageInset => Mathf.Min(28, Mathf.Max(12, width * .02f));
        bool HasQuery => !string.IsNullOrWhiteSpace(SearchQuery);
        float SavedCatalogPosition => HasQuery ? searchPosition : catalogPositions.TryGetValue(Filter, out float position) ? position : 1;
        float ReadCatalogPosition() => catalogScroll == null ? SavedCatalogPosition :
            catalogScroll.content.rect.height <= catalogScroll.viewport.rect.height + 1 ? 1 : catalogScroll.verticalNormalizedPosition;
        internal void HideHints() => ui.HideTooltip();
        bool Wide => width >= 1150;
        float HeaderHeight => Wide ? 78 : 66;
        float DockHeight => Wide ? 90 : 72;
        float DockTop => height - DockHeight - 58;

        public AlbumPanel(Transform parent, RunController run, UserTextResolver resolver,
            Action<AlbumEntry> edit, Action close, Action assigned = null, int? destinationSlot = null, string initialQuery = null)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            if (destinationSlot.HasValue && (destinationSlot < 0 || destinationSlot > 7)) throw new ArgumentOutOfRangeException(nameof(destinationSlot));
            ui = new WorkshopUi(resolver); this.edit = edit; this.close = close; this.assigned = assigned; this.destinationSlot = destinationSlot;
            SearchQuery = initialQuery ?? "";
            SelectedId = (destinationSlot.HasValue ? run.Workshop.LoadoutEntries[destinationSlot.Value] : null)?.Id
                ?? run.Workshop.Entries.FirstOrDefault(Matches)?.Id;
            Root = ui.Panel(parent, "Album panel", new Rect(0, 0, width, height), WorkshopUi.Paper, true);
            // Album refreshes replace Root's children; the disposable host page owns its hint overlay.
            ui.EnsureTooltip(parent);
            Refresh();
        }

        public void Reflow(float width, float height)
        {
            this.width = Mathf.Max(1, width); this.height = Mathf.Max(1, height);
            WorkshopUi.Place(Root, new Rect(0, 0, this.width, this.height)); Refresh();
        }

        public void SetFilter(AlbumFilter value)
        {
            Filter = value; feedback = "";
            if (Selected == null || !Matches(Selected)) SelectedId = run.Workshop.Entries.FirstOrDefault(Matches)?.Id;
            Refresh();
        }
        public void SelectEntry(string id)
        {
            SelectedId = run.Workshop.Entries.Any(entry => entry.Id == id) ? id : null;
            Refresh();
        }

        bool IsStandard(AlbumEntry entry) => run.AssemblyCatalog?.Starters.Any(starter => starter.Id == entry.Id) == true;
        bool Matches(AlbumEntry entry)
        {
            if (HasQuery && ui.Resolve(entry.Name).IndexOf(SearchQuery.Trim(), StringComparison.CurrentCultureIgnoreCase) < 0) return false;
            switch (Filter)
            {
                case AlbumFilter.Towers: return entry.Kind != AlbumEntryKind.Structure;
                case AlbumFilter.Structures: return entry.Kind == AlbumEntryKind.Structure;
                case AlbumFilter.Standard: return IsStandard(entry);
                case AlbumFilter.Mine: return !IsStandard(entry);
                default: return true;
            }
        }

        public void Refresh()
        {
            if (refreshing || Root == null) return;
            refreshing = true;
            try
            {
                var previous = EventSystem.current?.currentSelectedGameObject;
                string focus = previous != null && previous.transform.IsChildOf(Root) ? previous.name : null;
                if (catalogScroll != null)
                {
                    if (HasQuery) searchPosition = ReadCatalogPosition();
                    else catalogPositions[renderedFilter] = ReadCatalogPosition();
                }
                if (SelectedId != null && Selected == null) SelectedId = null;
                if (ReferenceId != null && Reference == null) ReferenceId = null;
                WorkshopUi.ClearViews(Root); confirmation = null;
                catalogScroll = null; workspace = null;
                Header(); BuildWorkspace();
                FieldMenuTheme.ApplyTo(Root);
                if (focus != null && EventSystem.current != null)
                {
                    var next = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(control => control.name == focus && control.IsInteractable());
                    if (next != null) EventSystem.current.SetSelectedGameObject(next.gameObject);
                }
                // Recreated keyboard focus may reveal its card. The user's existing viewport wins;
                // ordinary keyboard movement already revealed the control before this refresh.
                if (catalogScroll != null) catalogScroll.verticalNormalizedPosition = SavedCatalogPosition;
                ui.HideTooltip();
            }
            finally { refreshing = false; }
        }

        void BuildWorkspace()
        {
            if (workspace != null) WorkshopUi.RemoveView(workspace.gameObject);
            catalogScroll = null;
            workspace = ui.Panel(Root, "Album workspace", new Rect(0, 0, width, height), Color.clear);
            float innerWidth = width - PageInset * 2;
            float inspectorWidth = Mathf.Clamp(innerWidth * (Wide ? .33f : .40f), 230, 520);
            float gap = Wide ? 18 : 10;
            float catalogWidth = innerWidth - inspectorWidth - gap;
            float bodyY = HeaderHeight + 48;
            float bottom = DockTop - 12;
            var entries = run.Workshop.Entries.Where(Matches).ToArray();
            Filters(Wide ? catalogWidth : innerWidth, entries.Length);
            if (entries.Length == 0) Empty(new Rect(PageInset, bodyY, innerWidth, Mathf.Max(40, bottom - bodyY)));
            else
            {
                Cards(entries, new Rect(PageInset, bodyY, catalogWidth, Mathf.Max(40, bottom - bodyY)));
                float inspectorTop = Wide ? HeaderHeight : bodyY;
                Inspector(new Rect(PageInset + catalogWidth + gap, inspectorTop, inspectorWidth, Mathf.Max(40, bottom - inspectorTop)));
            }
            Slots(); Footer();
        }

        void Header()
        {
            float right = width - PageInset;
            float searchWidth = Mathf.Clamp(width * .24f, 154, 330);
            float searchX = right - 188 - searchWidth;
            float titleWidth = Mathf.Max(80, searchX - PageInset - 10);
            var title = ui.Label(Root, "Album title", destinationSlot.HasValue ? T("destination", destinationSlot.Value + 1) : T("title"),
                new Rect(PageInset, 3, titleWidth, 40), Wide ? 32 : 25, true);
            FitAlbumSummary(title, Wide ? 32 : 25);
            ui.Label(Root, "Album resources", T("resources", run.Workshop.Gold, run.Workshop.Level, run.Workshop.TotalXp),
                new Rect(PageInset, Wide ? 48 : 39, titleWidth, 23), 15, color: WorkshopUi.Muted);
            var field = ui.Panel(Root, "Album search", new Rect(searchX, 12, searchWidth, 40), WorkshopUi.Surface, true);
            var shortcuts = field.gameObject.AddComponent<AlbumSearchInput>();
            search = shortcuts;
            search.targetGraphic = field.GetComponent<Graphic>(); search.lineType = InputField.LineType.SingleLine;
            search.characterLimit = ProfileLimits.MaximumBlueprintNameLength;
            WorkshopUi.Graphic<AlbumSearchLens>(field, "Album search symbol", new Rect(10, 10, 20, 20));
            var inputRect = new Rect(37, 4, searchWidth - 75, 32);
            search.textComponent = ui.Label(field, "Album search text", "", inputRect, 16);
            search.placeholder = ui.Label(field, "Album search placeholder", searchWidth >= 200 ? "Поиск  ·  Ctrl+F" : "Поиск", inputRect, 15, color: WorkshopUi.Muted);
            search.SetTextWithoutNotify(SearchQuery);
            shortcuts.RestoreFocus = () => workspace?.GetComponentsInChildren<Button>()
                .FirstOrDefault(button => button.name == "Album filter " + Filter)?.gameObject;
            clearSearch = ui.IconButton(field, "Album clear search", new Rect(searchWidth - 33, 4, 30, 32),
                WorkshopIcon.Close, "Очистить поиск", () => { search.text = ""; shortcuts.Focus(); });
            clearSearch.gameObject.SetActive(SearchQuery.Length > 0);
            search.onValueChanged.AddListener(ChangeSearch);
            var create = ui.CompactButton(Root, "Album new", new Rect(width - PageInset - 176, 8, 128, 40),
                "Создать", () => edit?.Invoke(null), WorkshopIcon.Plus, true);
            create.interactable = edit != null;
            ui.Tip(create.gameObject, T("new"));
            ui.IconButton(Root, "Album help", new Rect(width - PageInset - 40, 8, 40, 40), WorkshopIcon.Help,
                "Как пользоваться альбомом", () => UiHelp.Show(Root, "Альбом",
                    "Выбери страницу в каталоге. Затем нажми место в текущем наборе, чтобы назначить её для боя.\n\n" +
                    "Одна страница занимает одно место. Перенос между местами требует подтверждения.\n\n" +
                    "Закрепи выбранную сборку кнопкой «Сравнить», затем выбери другую. Слева в числах — образец, справа — выбранная сборка; Δ показывает разницу. Сравнение ничего не меняет в наборе.\n\n" +
                    "Фильтры отделяют башни, сооружения, стандартные и твои страницы. Подробности показывают полный состав и характеристики.\n\n" +
                    "Изменить открывает мастерскую. Копия сохраняет отдельную страницу. Удаление требует подтверждения."));
        }

        void ChangeSearch(string value)
        {
            if (refreshing || SearchQuery == value) return;
            if (!HasQuery && catalogScroll != null) catalogPositions[renderedFilter] = ReadCatalogPosition();
            SearchQuery = value; searchPosition = 1; feedback = "";
            if (Selected == null || !Matches(Selected)) SelectedId = run.Workshop.Entries.FirstOrDefault(Matches)?.Id;
            clearSearch.gameObject.SetActive(value.Length > 0);
            BuildWorkspace();
            FieldMenuTheme.ApplyTo(Root);
            if (catalogScroll != null) catalogScroll.verticalNormalizedPosition = SavedCatalogPosition;
            ui.HideTooltip();
        }

        void Slots()
        {
            float dockWidth = width - PageInset * 2;
            var dock = ui.Panel(workspace, "Album loadout dock", new Rect(PageInset, DockTop, dockWidth, DockHeight), WorkshopUi.Paper);
            float gap = Wide ? 8 : 4, slotStart = Wide ? 158 : 78;
            float slotWidth = (dockWidth - slotStart - 10 - gap * 7) / 8;
            float slotHeight = DockHeight - 16;
            var loadout = run.Workshop.LoadoutEntries;
            ui.Label(dock, "Album current loadout", Wide ? "Боевой набор" : "Набор", new Rect(12, 9, slotStart - 18, 30), Wide ? 19 : 16, true);
            ui.Label(dock, "Album filled slots", loadout.Count(entry => entry != null) + " / 8", new Rect(12, 38, slotStart - 18, 22), 15, color: WorkshopUi.Muted);
            for (int index = 0; index < 8; index++)
            {
                int slot = index; var entry = loadout[index];
                string caption = entry == null ? T("slot_empty", index + 1) : T("slot", index + 1, ui.Resolve(entry.Name));
                var button = ui.Button(dock, "Album assign " + index,
                    new Rect(slotStart + index * (slotWidth + gap), 8, slotWidth, slotHeight),
                    (index + 1) + ".", () => RequestAssign(slot));
                var number = button.GetComponentInChildren<Text>();
                WorkshopUi.Place(number.rectTransform, new Rect(5, 3, 25, 22));
                number.resizeTextMinSize = number.resizeTextMaxSize = 15;
                if (entry != null)
                {
                    DrawEntry(button.transform, entry, new Rect(25, 4, slotWidth - 31, Wide ? slotHeight - 31 : slotHeight - 9));
                    if (Wide)
                    {
                        var captionLabel = ui.Label(button.transform, "Album equipped name", entry.Name,
                            new Rect(7, slotHeight - 26, slotWidth - 14, 23), 15);
                        FitAlbumSummary(captionLabel, 15);
                    }
                }
                else
                {
                    var empty = WorkshopUi.Graphic<WorkshopIconGraphic>(button.transform, "Album slot empty " + index,
                        new Rect((slotWidth - 20) * .5f, 28, 20, 20));
                    empty.Icon = WorkshopIcon.Plus; empty.color = WorkshopUi.Muted;
                }
                ui.Tip(button.gameObject, caption);
                button.interactable = !destinationSlot.HasValue && Selected != null;
                ui.SetSelected(button, destinationSlot == index || SelectedId != null && entry?.Id == SelectedId);
            }
        }

        void Filters(float areaWidth, int count)
        {
            string[] keys = { "all", "towers", "structures", "standard", "mine" };
            float gap = 3;
            float tabWidth = Mathf.Min(Wide ? 150 : 175, (areaWidth - gap * 4) / 5);
            for (int index = 0; index < keys.Length; index++)
            {
                var filter = (AlbumFilter)index;
                var button = ui.CompactButton(workspace, "Album filter " + filter,
                    new Rect(PageInset + index * (tabWidth + gap), HeaderHeight, tabWidth, 38), T("filter." + keys[index]), () => SetFilter(filter));
                button.GetComponentInChildren<Text>().resizeTextMaxSize = 18;
                FieldMenuSkin.Tab(button);
                ui.SetSelected(button, Filter == filter);
            }
            if (Wide && areaWidth - (tabWidth + gap) * 5 > 85)
            {
                var total = ui.Label(workspace, "Album filtered count", count + " / " + run.Workshop.Entries.Count,
                    new Rect(PageInset + areaWidth - 79, HeaderHeight, 72, 38), 16, color: WorkshopUi.Muted);
                total.alignment = TextAnchor.MiddleRight;
            }
        }

        void Cards(AlbumEntry[] entries, Rect bounds)
        {
            float gap = Wide ? 12 : 8;
            int columns = Mathf.Clamp(Mathf.FloorToInt((bounds.width - 8 + gap) / (192 + gap)), 1, 4);
            float cardWidth = (bounds.width - gap * (columns - 1) - 8) / columns;
            int rows = Mathf.CeilToInt(entries.Length / (float)columns);
            float cardHeight = Wide ? Mathf.Clamp((bounds.height - gap * (rows - 1)) / rows, 142, 248) : 138;
            var list = new CatalogScrollList(workspace, "Album catalogue", bounds, entries.Length, columns, cardHeight, gap);
            catalogScroll = list.Scroll; renderedFilter = Filter;
            for (int index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                var card = ui.Panel(list.Content, "Album card " + entry.Id,
                    new Rect(index % columns * (cardWidth + gap), index / columns * (cardHeight + gap), cardWidth, cardHeight),
                    SelectedId == entry.Id ? WorkshopUi.Selected : Color.white, true);
                // The full card is the choose target; decorative children never intercept pointer input.
                var surface = card.GetComponent<WorkshopSurfaceGraphic>();
                surface.IsControl = true; surface.Chosen = SelectedId == entry.Id;
                if (SelectedId == entry.Id)
                {
                    var selection = WorkshopUi.Graphic<FoundationDetailGraphic>(card, "Album selected card frame", new Rect(1, 1, cardWidth - 2, cardHeight - 2));
                    selection.Shape = FoundationDetailGraphic.Kind.Frame; selection.Selected = true; selection.StrokeWidth = 2.2f;
                }
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = surface;
                card.gameObject.AddComponent<UiScrollFocus>();
                button.name = "Album choose " + entry.Id;
                button.onClick.AddListener(() => { SelectedId = entry.Id; feedback = ""; if (destinationSlot.HasValue) RequestAssign(destinationSlot.Value); else Refresh(); });
                float footerHeight = Wide ? 77 : 65;
                float previewHeight = cardHeight - footerHeight - 8;
                DrawEntry(card, entry, new Rect(7, 6, cardWidth - 14, previewHeight));
                float titleY = cardHeight - footerHeight;
                var title = ui.Label(card, "Album card name", entry.Name, new Rect(12, titleY + 2, cardWidth - 24, Wide ? 39 : 34), Wide ? 20 : 17);
                title.fontStyle = FontStyle.Bold;
                FitAlbumSummary(title, Wide ? 20 : 17);
                ui.Tip(card.gameObject, entry.Name);
                var resolved = run.PreviewAlbumEntry(entry, MaterialTier.Pencil);
                var pencil = WorkshopUi.Graphic<WorkshopIconGraphic>(card, "Album card pencil", new Rect(10, cardHeight - 29, 20, 20));
                pencil.Icon = WorkshopIcon.Pencil; pencil.color = WorkshopUi.Ink;
                var price = ui.Label(card, "Album card price", resolved.Valid ? T("price", resolved.Value.Cost) : T("unavailable"),
                    new Rect(36, cardHeight - 32, cardWidth - 70, 26), 16, color: WorkshopUi.Muted);
                price.resizeTextForBestFit = true; price.resizeTextMinSize = 14; price.resizeTextMaxSize = 16;
                price.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (ReferenceId == entry.Id)
                {
                    var pin = WorkshopUi.Graphic<WorkshopIconGraphic>(card, "Album reference marker", new Rect(cardWidth - 30, cardHeight - 29, 20, 20));
                    pin.Icon = WorkshopIcon.Pin; pin.color = WorkshopUi.Blue;
                    ui.Tip(card.gameObject, ui.Resolve(entry.Name) + " · образец для сравнения");
                }
            }
        }

        static void FitAlbumSummary(Text label, int maximumSize)
        {
            const int minimumSize = 15;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = minimumSize; label.resizeTextMaxSize = maximumSize;
            string full = label.text;
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.scaleFactor = 1; settings.fontSize = minimumSize;
            settings.resizeTextForBestFit = false; settings.generateOutOfBounds = false;
            using (var generated = new TextGenerator())
            {
                bool Fits(string value)
                {
                    generated.Populate(value, settings);
                    return generated.characterCountVisible >= value.Length;
                }
                if (Fits(full)) return;
                int low = 0, high = full.Length;
                while (low < high)
                {
                    int middle = (low + high + 1) / 2;
                    if (Fits(full.Substring(0, middle).TrimEnd() + "…")) low = middle;
                    else high = middle - 1;
                }
                label.text = full.Substring(0, low).TrimEnd() + "…";
            }
        }

        void Empty(Rect bounds)
        {
            var sheet = ui.Panel(workspace, "Album empty sheet", bounds, WorkshopUi.Paper);
            const float contentHeight = 240;
            Transform content = sheet;
            if (bounds.height < contentHeight)
                content = new CatalogScrollList(sheet, "Album empty scroll", new Rect(0, 0, bounds.width, bounds.height), 1, 1, contentHeight, 0).Content;
            var title = ui.Label(sheet, "Album empty", HasQuery ? "Ничего не найдено" : T(Filter == AlbumFilter.Mine ? "empty_mine" : "empty"),
                new Rect(24, 0, bounds.width - 48, 44), 26, true); title.alignment = TextAnchor.MiddleCenter;
            if (content != sheet) title.transform.SetParent(content, false);
            var hint = ui.Label(content, "Album empty explanation", HasQuery ? "Попробуй другое название или выбери другой фильтр." : T("empty_hint"), new Rect(24, 50, bounds.width - 48, 64), 19, color: WorkshopUi.Muted);
            hint.alignment = TextAnchor.MiddleCenter;
            float actionWidth = Mathf.Min(320, bounds.width - 48);
            ui.Button(content, "Album empty create", new Rect((bounds.width - actionWidth) / 2, 128, actionWidth, 44),
                T("new"), () => edit?.Invoke(null), WorkshopIcon.Pencil, true).interactable = edit != null;
            if (HasQuery)
                ui.Button(content, "Album reset query", new Rect((bounds.width - actionWidth) / 2, 184, actionWidth, 44),
                    "Очистить поиск", () => { search.text = ""; search.Select(); search.ActivateInputField(); }, WorkshopIcon.Close);
            else
                ui.Button(content, "Album show standard", new Rect((bounds.width - actionWidth) / 2, 184, actionWidth, 44),
                    T("show_standard"), () => SetFilter(AlbumFilter.Standard), WorkshopIcon.Book);
        }

        void Inspector(Rect bounds)
        {
            var sheet = ui.Panel(workspace, "Album inspector", bounds, WorkshopUi.Paper);
            var entry = Selected;
            if (entry == null)
            {
                ui.Label(sheet, "Album selection hint", "Выбери сборку", new Rect(16, 18, bounds.width - 32, 56), 20, true);
                return;
            }
            float w = bounds.width;
            float inset = Wide ? 18 : 12;
            float titleY = Wide ? 32 : 6;
            var name = ui.Label(sheet, "Album selected name", entry.Name, new Rect(inset, titleY, w - 2 * inset - 8, 30), Wide ? 27 : 21, true);
            float nameHeight = Mathf.Max(28, Mathf.Ceil(name.preferredHeight) + 4);
            WorkshopUi.Place(name.rectTransform, new Rect(inset, titleY, w - 2 * inset - 8, nameHeight));
            var reference = Reference;
            var resolved = run.PreviewAlbumEntry(entry, MaterialTier.Pencil);
            var baseline = reference == null ? null : run.PreviewAlbumEntry(reference, MaterialTier.Pencil);
            bool comparing = reference != null && reference.Id != entry.Id && resolved.Valid && baseline.Valid;
            var metrics = new List<AlbumMetric>();
            string comparisonStatus = "";
            if (resolved.Valid) BuildMetrics(resolved.Value, comparing ? baseline.Value : null, metrics, out comparisonStatus);
            if (reference != null && reference.Id != entry.Id && !baseline.Valid)
                comparisonStatus = "Образец недоступен. Показаны параметры выбранной сборки.";
            if (reference == null && resolved.Valid) AddRoleSummary(resolved.Value, metrics);
            int summaryRows = Wide && reference == null ? metrics.Count : 3;
            float contextHeight = reference == null ? 0 : 46;
            float coreHeight = resolved.Valid ? metrics.Take(summaryRows).Sum(metric => metric.Height) : 56;
            float viewportHeight = Mathf.Max(36, bounds.height - 58);
            // Comparisons keep their first three rows above the fold even on a compact display.
            float artY = titleY + nameHeight + 5;
            float free = viewportHeight - artY - 40 - contextHeight - coreHeight - 12;
            float artHeight = free >= 62 ? Mathf.Min(free, Wide ? 248 : 108) : 0;
            float pinY = artY + artHeight + (artHeight > 0 ? 8 : 0);
            float statisticsY = pinY + 40 + contextHeight;
            float extrasY = statisticsY + coreHeight + 8;
            float statusHeight = comparisonStatus.Length > 0 ? 50 : 0;
            float contentHeight = extrasY + statusHeight + metrics.Skip(summaryRows).Sum(metric => metric.Height) + 8;
            Transform content = sheet;
            if (viewportHeight < contentHeight)
                content = new CatalogScrollList(sheet, "Album inspector scroll", new Rect(0, 0, w, viewportHeight), 1, 1, contentHeight, 0).Content;
            if (content != sheet) name.transform.SetParent(content, false);
            ui.Tip(name.gameObject, entry.Name);
            if (Wide)
            {
                string kind = entry.Kind == AlbumEntryKind.Structure ? "Сооружение" : "Башня";
                var type = ui.Label(content, "Album selected category", kind, new Rect(inset, 7, w - 2 * inset, 24), 15, color: WorkshopUi.Muted);
                type.fontStyle = FontStyle.Bold;
            }
            if (artHeight > 0)
                StandardAssemblyIllustration.Draw(content, entry, new Rect(inset, artY, w - 2 * inset - 5, artHeight),
                    "Album inspected drawing");
            bool pinned = ReferenceId == entry.Id;
            float pinWidth = w - 28 - (reference != null && !pinned ? 42 : 0);
            var pin = ui.CompactButton(content, "Album compare", new Rect(12, pinY, pinWidth, 36),
                reference == null ? "Сравнить" : pinned ? "Снять образец" : "Заменить образец", ToggleComparison, WorkshopIcon.Pin);
            ui.SetSelected(pin, pinned);
            ui.Tip(pin.gameObject, pinned ? "Снять образец «" + ui.Resolve(entry.Name) + "»" :
                "Закрепить «" + ui.Resolve(entry.Name) + "» как образец, затем выбрать другую сборку для сравнения");
            if (reference != null && !pinned)
                ui.IconButton(content, "Album compare clear", new Rect(w - 52, pinY, 36, 36), WorkshopIcon.Close,
                    "Снять образец «" + ui.Resolve(reference.Name) + "»", () => { ReferenceId = null; Refresh(); });
            if (reference != null)
            {
                var referenceName = ui.Label(content, "Album comparison reference", ui.Resolve(reference.Name),
                    new Rect(12, pinY + 37, w - 28, 25), 15);
                FitAlbumSummary(referenceName, 15);
                ui.Tip(referenceName.gameObject, "Образец: " + ui.Resolve(reference.Name));
                bool commonOnly = comparisonStatus.StartsWith("Разные типы", StringComparison.Ordinal);
                var direction = ui.Label(content, "Album comparison direction", pinned ? "Выбери другую сборку" :
                    commonOnly ? "Общие: образец → выбранная" : "Образец → выбранная",
                    new Rect(12, pinY + 62, w - 28, 20), 14, color: WorkshopUi.Muted);
                if (commonOnly) ui.Tip(direction.gameObject, comparisonStatus);
            }
            var rule = WorkshopUi.Graphic<Image>(content, "Album selected title rule", new Rect(12, statisticsY - 4, w - 28, 1));
            rule.color = new Color(.12f, .28f, .52f, .55f);
            if (resolved.Valid)
            {
                float y = statisticsY;
                foreach (var metric in metrics.Take(summaryRows)) { InspectorMetric(content, metric, y, w); y += metric.Height; }
            }
            else ui.Label(content, "Album selected statistics", T("unavailable"), new Rect(16, statisticsY, w - 32, 56), 16, color: WorkshopUi.Warning);
            const float actionSize = 36, actionGap = 6;
            var actions = ui.Panel(sheet, "Album inspector actions", new Rect(0, bounds.height - 48, w, 40), Color.clear);
            float editWidth = w - 24 - (actionSize + actionGap) * 3;
            var editButton = ui.CompactButton(actions, "Album edit", new Rect(12, 0, editWidth, 38), "Изменить",
                () => edit?.Invoke(Selected), WorkshopIcon.Pencil);
            ui.Tip(editButton.gameObject, "Изменить «" + ui.Resolve(entry.Name) + "» в мастерской");
            editButton.interactable = edit != null;
            float toolsX = 12 + editWidth + actionGap;
            ui.IconButton(actions, "Album details", new Rect(toolsX, 0, actionSize, 38), WorkshopIcon.Help,
                "Полный состав и характеристики «" + ui.Resolve(entry.Name) + "»", ShowDetails);
            ui.IconButton(actions, "Album variant", new Rect(toolsX + actionSize + actionGap, 0, actionSize, 38),
                WorkshopIcon.Copy, "Сохранить отдельную копию «" + ui.Resolve(entry.Name) + "»", () => {
                var result = run.AssemblyCommands.SaveEntryAsVariant(Selected, Guid.NewGuid());
                if (result.Success) SelectedId = result.ResultId;
                feedback = ResultText(result, "variant_saved"); Refresh();
            }).interactable = entry.Kind != AlbumEntryKind.Legacy;
            ui.IconButton(actions, "Album delete", new Rect(toolsX + (actionSize + actionGap) * 2, 0, actionSize, 38),
                WorkshopIcon.Trash, "Удалить «" + ui.Resolve(entry.Name) + "» · потребуется подтверждение", RequestDelete);
            if (comparisonStatus.Length > 0)
                ui.Label(content, "Album comparison status", comparisonStatus, new Rect(12, extrasY, w - 28, statusHeight), 14, color: WorkshopUi.Muted);
            float detailsY = extrasY + statusHeight;
            foreach (var metric in metrics.Skip(summaryRows)) { InspectorMetric(content, metric, detailsY, w); detailsY += metric.Height; }
        }

        void ToggleComparison()
        {
            if (Selected == null) return;
            ReferenceId = ReferenceId == SelectedId ? null : SelectedId;
            Refresh();
        }

        void ShowDetails()
        {
            var entry = Selected; if (entry == null) return;
            var copy = new StringBuilder(ui.Resolve(entry.Name)).Append("\n\n");
            var resolved = run.PreviewAlbumEntry(entry, MaterialTier.Pencil);
            if (resolved.Valid)
            {
                var value = resolved.Value;
                copy.Append("Карандаш · ").Append(T("price", value.Cost)).Append("\n")
                    .Append("Прочность: ").Append(Number(value.Health)).Append(" HP\n")
                    .Append("Нагрузка: ").Append(value.Load).Append(" / ").Append(value.Capacity).Append("\n")
                    .Append("Постройка: ").Append(Number(value.BuildSeconds)).Append(" с\n")
                    .Append("Защита: ").Append(Number(value.Defense * 100)).Append("%\n");
                if (value.FrontDefense > 0)
                    copy.Append("Защита спереди: ").Append(Number(value.FrontDefense * 100)).Append("% · сектор ").Append(Number(value.FrontDefenseDegrees)).Append("°\n");
                if (value.Weapons.Count > 0)
                {
                    copy.Append("\nПодача при работе всех орудий: ×").Append(Number(value.FeedMultiplier)).Append("\n");
                    foreach (var weapon in value.Weapons.OrderBy(item => item.SlotIndex))
                    {
                        copy.Append("\nОружие ").Append(weapon.SlotIndex + 1).Append(weapon.Attack == AttackKind.Cleanup ? " · очистка\n" : "\n");
                        if (weapon.Attack != AttackKind.Cleanup) copy.Append("Урон: ").Append(Number(weapon.Damage)).Append("\n");
                        copy.Append("Дальность: ").Append(Number(weapon.Range)).Append(" кл.");
                        if (weapon.Attack != AttackKind.Radial)
                            copy.Append(" · сектор обстрела: ").Append(Number(weapon.FiringArcDegrees)).Append("°");
                        if (weapon.MinimumRange > 0) copy.Append(" · мёртвая зона ").Append(Number(weapon.MinimumRange));
                        copy.Append("\nМежду выстрелами: ").Append(Number(weapon.IntervalAtFeed(value.FeedMultiplier))).Append(" с\n")
                            .Append("Наведение: ").Append(Number(weapon.AcquisitionSeconds)).Append(" с\n");
                        if (!weapon.LegacyContinuous)
                            copy.Append("Обойма: ").Append(weapon.MagazineSize).Append(" · смена: ")
                                .Append(Number(weapon.ReloadAtFeed(value.FeedMultiplier))).Append(" с\n");
                        if (weapon.TargetLimit > 1) copy.Append("Целей: ").Append(weapon.TargetLimit).Append("\n");
                        if (weapon.Radius > 0) copy.Append("Радиус действия: ").Append(Number(weapon.Radius)).Append("\n");
                        if (weapon.JumpRange > 0) copy.Append("Дальность рикошета: ").Append(Number(weapon.JumpRange)).Append("\n");
                        if (weapon.ConeDegrees > 0) copy.Append("Угол залпа: ").Append(Number(weapon.ConeDegrees)).Append("°\n");
                        if (weapon.FlightSeconds > 0) copy.Append("Полёт снаряда: ").Append(Number(weapon.FlightSeconds)).Append(" с\n");
                        if (weapon.SlowSeconds > 0)
                            copy.Append("Скорость врага: ").Append(Number(weapon.SlowSpeedMultiplier * 100)).Append("% · ")
                                .Append(Number(weapon.SlowSeconds)).Append(" с\n");
                    }
                }
                if (value.Support != null)
                {
                    var support = value.Support;
                    if (GameText.TrapOrBeaconEffect(support) is string fieldEffect) copy.AppendLine(ui.Resolve(fieldEffect));
                    else if (support.Radius > 0) copy.Append("Радиус поддержки: ").Append(Number(support.Radius)).Append("\n");
                    if (support.Kind == AssemblyStructureFamily.Repair)
                        copy.Append("Ремонт: ").Append(Number(support.RepairPerSecond)).Append(" HP/с\n");
                    if (support.Kind == AssemblyStructureFamily.Slow)
                        copy.Append("Скорость врага: ").Append(Number(support.SlowSpeedMultiplier * 100)).Append("% · ")
                            .Append(Number(support.SlowSeconds)).Append(" с\n");
                    if (support.Kind == AssemblyStructureFamily.Generator)
                        copy.Append("Доход: ").Append(support.IncomePerWave).Append(" штр. за полную волну\n")
                            .Append("Окупаемость: ").Append(support.PaybackWaves(value.Cost)).Append(" волн\n")
                            .Append("Не более ").Append(support.MaximumInstances).Append(" генераторов · только карандаш\n");
                }
            }
            else
            {
                copy.Append(T("unavailable")).Append("\n");
                foreach (var diagnostic in resolved.Diagnostics)
                    copy.Append(ui.Resolve(UserText.Message(diagnostic.Code, diagnostic.Arguments.Select(argument => (object)argument).ToArray()))).Append("\n");
            }
            if (entry.Snapshot != null)
            {
                copy.Append("\nСостав:\n");
                foreach (var reference in entry.Snapshot.Recipe.Parts)
                {
                    var definition = entry.Snapshot.Definitions.FirstOrDefault(part => part.Id == reference.Id && part.Revision == reference.Revision);
                    copy.Append("• ").Append(definition == null ? reference.Id : ui.Resolve(definition.Name)).Append("\n");
                }
            }
            copy.Append("\nЦена строительства расходуется в бою. Открытые детали доступны во всех подходящих сборках.");
            UiHelp.Show(Root, "Подробности страницы", copy.ToString());
        }

        sealed class AlbumMetric
        {
            public string Id, Label, Value, Delta;
            public WorkshopIcon Icon;
            public bool? Improved;
            public float Height => Delta == null ? 34 : 48;
        }

        string Number(double value) => value.ToString("0.###", ui.TextResolver.Culture);
        string Delta(double value) => value.ToString("+0.###;−0.###;0", ui.TextResolver.Culture);

        AlbumMetric Metric(string id, string label, WorkshopIcon icon, double value, double? before = null,
            bool lowerIsBetter = false, string unit = "")
        {
            double difference = before.HasValue ? value - before.Value : 0;
            return new AlbumMetric {
                Id = id, Label = label, Icon = icon,
                Value = before.HasValue ? Number(before.Value) + " → " + Number(value) : Number(value) + unit,
                Delta = before.HasValue ? "Δ " + Delta(difference) + unit : null,
                Improved = !before.HasValue || Math.Abs(difference) < .0001 ? (bool?)null : lowerIsBetter ? difference < 0 : difference > 0
            };
        }

        void AddRoleSummary(ResolvedEntry value, List<AlbumMetric> rows)
        {
            var support = value.Support;
            if (support == null && value.Weapons.Count > 0)
            {
                var offensive = value.Weapons.Where(weapon => weapon.Attack != AttackKind.Cleanup).ToArray();
                if (offensive.Length > 0)
                {
                    rows.Add(WeaponSpan("Range", "Дальность огня", WorkshopIcon.Range, offensive.Select(weapon => weapon.Range), " кл."));
                    var directional = value.Weapons.Where(weapon => weapon.Attack != AttackKind.Radial).ToArray();
                    if (directional.Length > 0)
                        rows.Add(WeaponSpan("FiringArc", "Сектор обстрела", WorkshopIcon.FiringArc, directional.Select(weapon => weapon.FiringArcDegrees), "°"));
                    rows.Add(Metric("Dps", "Расчётный урон/с", WorkshopIcon.Damage, value.Dps));
                }
                else
                {
                    rows.Add(WeaponSpan("Range", "Дальность очистки", WorkshopIcon.Range, value.Weapons.Select(weapon => weapon.Range), " кл."));
                    rows.Add(WeaponSpan("FiringArc", "Сектор обстрела", WorkshopIcon.FiringArc, value.Weapons.Select(weapon => weapon.FiringArcDegrees), "°"));
                    rows.Add(Metric("Cleanup Radius", "Радиус очистки", WorkshopIcon.Eraser, value.Weapons.Max(weapon => weapon.Radius), unit: " кл."));
                }
                return;
            }
            switch (support?.Kind)
            {
                case AssemblyStructureFamily.Generator:
                    rows.Add(Metric("Support Income", "Доход за волну", WorkshopIcon.Pencil, support.IncomePerWave, unit: " штр."));
                    rows.Add(Metric("Support Payback", "Волн до окупаемости", WorkshopIcon.Clock, support.PaybackWaves(value.Cost)));
                    break;
                case AssemblyStructureFamily.Repair:
                    rows.Add(Metric("Support Repair", "Ремонт", WorkshopIcon.Heart, support.RepairPerSecond, unit: " HP/с"));
                    rows.Add(Metric("Support Radius", "Радиус ремонта", WorkshopIcon.Range, support.Radius, unit: " кл."));
                    break;
                case AssemblyStructureFamily.Slow:
                    rows.Add(Metric("Support Slow", "Замедление", WorkshopIcon.Target, (1 - support.SlowSpeedMultiplier) * 100, unit: "%"));
                    rows.Add(Metric("Support Radius", "Радиус действия", WorkshopIcon.Range, support.Radius, unit: " кл."));
                    break;
                case AssemblyStructureFamily.Spikes:
                    rows.Add(Metric("Spike Damage", "Урон за удар", WorkshopIcon.Damage, support.SpikeDamage));
                    rows.Add(Metric("Support Radius", "Радиус удара", WorkshopIcon.Range, support.Radius, unit: " кл."));
                    break;
                case AssemblyStructureFamily.Prism:
                    rows.Add(Metric("Prism Burn", "Ожог", WorkshopIcon.Damage, support.BurnDamagePerSecond, unit: " HP/с"));
                    rows.Add(Metric("Prism Slow", "Замедление", WorkshopIcon.Target, (1 - support.SlowSpeedMultiplier) * 100, unit: "%"));
                    rows.Add(Metric("Prism Armor", "Ослабление брони", WorkshopIcon.Armor, support.ArmorReduction * 100, unit: " п. п."));
                    rows.Add(Metric("Support Radius", "Радиус", WorkshopIcon.Range, support.Radius, unit: " кл."));
                    break;
                case AssemblyStructureFamily.Beacon:
                    rows.Add(Metric("Armor Reduction", "Снижение защиты", WorkshopIcon.Armor, support.ArmorReduction * 100, unit: " п. п."));
                    rows.Add(Metric("Support Radius", "Радиус метки", WorkshopIcon.Range, support.Radius, unit: " кл."));
                    break;
                default:
                    rows.Add(Metric("BuildSeconds", "Постройка", WorkshopIcon.Clock, value.BuildSeconds, unit: " с"));
                    rows.Add(Metric("Defense", "Защита", WorkshopIcon.Armor, value.Defense * 100, unit: "%"));
                    break;
            }
        }

        void BuildMetrics(ResolvedEntry value, ResolvedEntry before, List<AlbumMetric> rows, out string comparisonStatus)
        {
            comparisonStatus = "";
            rows.Add(Metric("Health", "Прочность", WorkshopIcon.Heart, value.Health, before?.Health));
            rows.Add(new AlbumMetric {
                Id = "Load", Label = "Вес / предел", Icon = WorkshopIcon.Weight,
                Value = before == null ? value.Load + " / " + value.Capacity : before.Load + "/" + before.Capacity + " → " + value.Load + "/" + value.Capacity,
                Delta = before == null ? null : "Δ веса " + Delta(value.Load - before.Load) + " · предела " + Delta(value.Capacity - before.Capacity)
            });
            rows.Add(Metric("Cost", "Цена", WorkshopIcon.Pencil, value.Cost, before?.Cost, true, " штр."));
            if (before == null) return;

            rows.Add(Metric("BuildSeconds", "Постройка, с", WorkshopIcon.Clock, value.BuildSeconds, before.BuildSeconds, true));
            rows.Add(Metric("Defense", "Защита, %", WorkshopIcon.Armor, value.Defense * 100, before.Defense * 100));
            if (value.FrontDefense > 0 || before.FrontDefense > 0)
                rows.Add(Metric("FrontDefense", "Спереди, %", WorkshopIcon.Shield, value.FrontDefense * 100, before.FrontDefense * 100));
            if (value.Support != null || before.Support != null)
            {
                if (value.Support == null || before.Support == null || value.Support.Kind != before.Support.Kind)
                {
                    comparisonStatus = "Разные типы сборок: сравниваются только общие параметры.";
                    return;
                }
                var currentSupport = value.Support; var oldSupport = before.Support;
                if (currentSupport.Kind != AssemblyStructureFamily.Wall && currentSupport.Kind != AssemblyStructureFamily.Generator)
                    rows.Add(Metric("Support Radius", "Радиус", WorkshopIcon.Range, currentSupport.Radius, oldSupport.Radius));
                if (currentSupport.Kind == AssemblyStructureFamily.Repair)
                    rows.Add(Metric("Support Repair", "Ремонт, HP/с", WorkshopIcon.Heart, currentSupport.RepairPerSecond, oldSupport.RepairPerSecond));
                if (currentSupport.Kind == AssemblyStructureFamily.Spikes)
                    rows.Add(Metric("Spike Damage", "Урон за удар", WorkshopIcon.Damage, currentSupport.SpikeDamage, oldSupport.SpikeDamage));
                if (currentSupport.Kind == AssemblyStructureFamily.Prism)
                {
                    rows.Add(Metric("Prism Burn", "Ожог, HP/с", WorkshopIcon.Damage, currentSupport.BurnDamagePerSecond, oldSupport.BurnDamagePerSecond));
                    rows.Add(Metric("Prism Slow", "Скорость, %", WorkshopIcon.Target, currentSupport.SlowSpeedMultiplier * 100, oldSupport.SlowSpeedMultiplier * 100, true));
                }
                if (currentSupport.Kind == AssemblyStructureFamily.Beacon || currentSupport.Kind == AssemblyStructureFamily.Prism)
                    rows.Add(Metric("Armor Reduction", "Снижение защиты, п. п.", WorkshopIcon.Armor, currentSupport.ArmorReduction * 100, oldSupport.ArmorReduction * 100));
                if (currentSupport.Kind == AssemblyStructureFamily.Slow)
                {
                    rows.Add(Metric("Support Slow", "Скорость, %", WorkshopIcon.Target, currentSupport.SlowSpeedMultiplier * 100, oldSupport.SlowSpeedMultiplier * 100, true));
                    rows.Add(Metric("Support Duration", "Действие, с", WorkshopIcon.Clock, currentSupport.SlowSeconds, oldSupport.SlowSeconds));
                    comparisonStatus = "Скорость врага после замедления: меньше — сильнее эффект.";
                }
                if (currentSupport.Kind == AssemblyStructureFamily.Generator)
                {
                    rows.Add(Metric("Support Income", "Штрихов/волну", WorkshopIcon.Pencil, currentSupport.IncomePerWave, oldSupport.IncomePerWave));
                    rows.Add(Metric("Support Payback", "Окупаемость", WorkshopIcon.Clock, currentSupport.PaybackWaves(value.Cost), oldSupport.PaybackWaves(before.Cost), true, " волн"));
                    comparisonStatus = "Доход за полную волну. Окупаемость округляется вверх.";
                }
                return;
            }

            var weaponCount = Metric("WeaponCount", "Орудия", WorkshopIcon.Weapon, value.Weapons.Count, before.Weapons.Count);
            weaponCount.Improved = null;
            rows.Add(weaponCount);
            int matched = 0;
            foreach (var weapon in value.Weapons.OrderBy(item => item.SlotIndex))
            {
                var oldWeapon = before.Weapons.FirstOrDefault(item => item.SlotIndex == weapon.SlotIndex &&
                    item.Family == weapon.Family && item.Attack == weapon.Attack && item.LegacyContinuous == weapon.LegacyContinuous);
                if (oldWeapon == null) continue;
                matched++;
                string id = "Weapon " + weapon.SlotIndex + " ";
                string label = (weapon.SlotIndex + 1) + " · ";
                if (weapon.Attack != AttackKind.Cleanup)
                    rows.Add(Metric(id + "Damage", label + "Урон", WorkshopIcon.Damage, weapon.Damage, oldWeapon.Damage));
                rows.Add(Metric(id + "Range", label + "Дальность", WorkshopIcon.Range, weapon.Range, oldWeapon.Range));
                if (weapon.Attack != AttackKind.Radial)
                    rows.Add(Metric(id + "FiringArc", label + "Сектор обстрела, °", WorkshopIcon.FiringArc, weapon.FiringArcDegrees, oldWeapon.FiringArcDegrees));
                if (weapon.MinimumRange > 0 || oldWeapon.MinimumRange > 0)
                    rows.Add(Metric(id + "MinimumRange", label + "Мин. радиус", WorkshopIcon.Range, weapon.MinimumRange, oldWeapon.MinimumRange, true));
                if (weapon.Radius > 0 || oldWeapon.Radius > 0)
                    rows.Add(Metric(id + "Radius", label + "Радиус", WorkshopIcon.Target, weapon.Radius, oldWeapon.Radius));
                rows.Add(Metric(id + "Interval", label + "Интервал, с", WorkshopIcon.Clock,
                    weapon.IntervalAtFeed(value.FeedMultiplier), oldWeapon.IntervalAtFeed(before.FeedMultiplier), true));
                rows.Add(Metric(id + "Acquisition", label + "Прицел, с", WorkshopIcon.Target,
                    weapon.AcquisitionSeconds, oldWeapon.AcquisitionSeconds, true));
                if (weapon.TargetLimit > 1 || oldWeapon.TargetLimit > 1)
                    rows.Add(Metric(id + "Targets", label + "Целей", WorkshopIcon.Target, weapon.TargetLimit, oldWeapon.TargetLimit));
                if (weapon.JumpRange > 0 || oldWeapon.JumpRange > 0)
                    rows.Add(Metric(id + "JumpRange", label + "Рикошет", WorkshopIcon.Range, weapon.JumpRange, oldWeapon.JumpRange));
                if (weapon.ConeDegrees > 0 || oldWeapon.ConeDegrees > 0)
                    rows.Add(Metric(id + "Cone", label + "Угол залпа, °", WorkshopIcon.Target, weapon.ConeDegrees, oldWeapon.ConeDegrees));
                if (!weapon.LegacyContinuous)
                {
                    rows.Add(Metric(id + "Magazine", label + "Обойма", WorkshopIcon.Magazine, weapon.MagazineSize, oldWeapon.MagazineSize));
                    rows.Add(Metric(id + "Reload", label + "Смена, с", WorkshopIcon.Reload,
                        weapon.ReloadAtFeed(value.FeedMultiplier), oldWeapon.ReloadAtFeed(before.FeedMultiplier), true));
                }
            }
            bool allMatched = matched == value.Weapons.Count && matched == before.Weapons.Count;
            comparisonStatus = matched == 0 ? "Разные типы оружия: сравниваются только общие параметры." :
                (allMatched ? "" : "Несовпадающие орудия не сравниваются. ") + "Номер — место оружия. Подача при работе всех орудий.";
        }

        AlbumMetric WeaponSpan(string id, string label, WorkshopIcon icon, IEnumerable<float> values, string unit)
        {
            float min = values.Min(), max = values.Max();
            var row = Metric(id, label, icon, min, unit: unit);
            if (!Mathf.Approximately(min, max)) row.Value = Number(min) + "–" + Number(max) + unit;
            return row;
        }

        void InspectorMetric(Transform parent, AlbumMetric metric, float y, float w)
        {
            var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, "Album metric icon " + metric.Id, new Rect(12, y + 5, 20, 20));
            icon.Icon = metric.Icon; icon.color = WorkshopUi.Blue;
            float valueWidth = Mathf.Max(96, w * .48f);
            float valueX = w - valueWidth - 16;
            var label = ui.Label(parent, "Album metric label " + metric.Id, metric.Label, new Rect(38, y + 1, valueX - 44, 28), 15);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = 15;
            var number = ui.Label(parent, "Album metric value " + metric.Id, metric.Value, new Rect(valueX, y + 1, valueWidth, 28), metric.Delta == null ? 18 : 16);
            number.alignment = TextAnchor.MiddleRight; number.resizeTextForBestFit = true; number.resizeTextMinSize = 14;
            number.resizeTextMaxSize = metric.Delta == null ? 18 : 16;
            if (metric.Delta != null)
            {
                var delta = ui.Label(parent, "Album metric delta " + metric.Id, metric.Delta,
                    new Rect(38, y + 27, w - 54, 18), 14,
                    color: metric.Improved.HasValue ? metric.Improved.Value ? WorkshopUi.Success : WorkshopUi.Warning : WorkshopUi.Muted);
                delta.alignment = TextAnchor.MiddleRight;
            }
            var rule = WorkshopUi.Graphic<Image>(parent, "Album metric rule " + metric.Id, new Rect(12, y + metric.Height - 1, w - 28, 1));
            rule.color = new Color(.12f, .28f, .52f, .35f);
        }

        void DrawEntry(Transform parent, AlbumEntry entry, Rect bounds)
            => StandardAssemblyIllustration.Draw(parent, entry, bounds,
                entry.Kind == AlbumEntryKind.Legacy ? "Album legacy illustration" : "Album certified illustration");

        void Footer()
        {
            string status = feedback;
            if (string.IsNullOrEmpty(status) && Selected != null && Matches(Selected) && !destinationSlot.HasValue) status = "Выбери место в наборе";
            ui.Label(workspace, "Album status", status, new Rect(PageInset + 50, height - 50, width - PageInset * 2 - 190, 36), 16,
                color: feedback.Length > 0 ? WorkshopUi.Warning : WorkshopUi.Muted);
            ui.IconButton(workspace, "Album restore", new Rect(PageInset, height - 48, 38, 38), WorkshopIcon.Undo, T("restore"), () => {
                feedback = ResultText(run.AssemblyCommands.RestoreStarterEntries(Guid.NewGuid()), "restored"); Refresh();
            });
            ui.CompactButton(workspace, "Album close", new Rect(width - PageInset - 128, height - 48, 128, 38), T("close"), close, WorkshopIcon.Back);
        }

        string ResultText(AssemblyCommandResult result, string success)
        {
            if (result.Success) return T(success);
            var issue = result.Diagnostics.FirstOrDefault();
            object[] arguments = issue?.Arguments.Select(argument => (object)argument).ToArray() ?? Array.Empty<object>();
            string message = ui.Resolve(UserText.Message(result.Code, result.Code == "profile.entry.restore_space" ? arguments : Array.Empty<object>()));
            if (result.Code == "profile.write_failed" && issue?.Arguments.Count > 0) message += "\n" + ui.Resolve(issue.Arguments[0]);
            return message;
        }

        void RequestAssign(int slot)
        {
            var entry = Selected; if (entry == null) return;
            int previous = -1;
            for (int index = 0; index < 8; index++) if (run.Workshop.LoadoutEntries[index]?.Id == entry.Id) previous = index;
            Action assign = () => {
                var result = run.AssemblyCommands.AssignSlot(slot, entry.Id, Guid.NewGuid());
                feedback = result.Success ? T("assigned", slot + 1) : ResultText(result, "assigned"); Refresh();
                if (result.Success) assigned?.Invoke();
            };
            if (previous >= 0 && previous != slot)
                Confirm("move", T("move_title"), T("move_message", ui.Resolve(entry.Name), previous + 1, slot + 1), T("move"), assign);
            else assign();
        }

        void RequestDelete()
        {
            var entry = Selected; if (entry == null) return;
            var slots = Enumerable.Range(0, 8).Where(slot => run.Workshop.LoadoutEntries[slot]?.Id == entry.Id).Select(slot => (slot + 1).ToString()).ToArray();
            string message = slots.Length == 0 ? T("delete_unused", ui.Resolve(entry.Name)) : T("delete_message", ui.Resolve(entry.Name), string.Join(", ", slots));
            int savedSets = run.Workshop.NamedLoadouts.Count(set => set.Slots.Contains(entry.Id));
            if (savedSets > 0) message += "\n\n" + T("delete_saved_sets", savedSets);
            Confirm("delete", T("delete_title"), message, T("delete"), () => {
                var result = run.AssemblyCommands.DeleteEntry(entry.Id, Guid.NewGuid());
                if (result.Success) SelectedId = null;
                feedback = ResultText(result, "deleted"); Refresh();
            });
        }

        void Confirm(string kind, string title, string message, string actionText, Action action)
        {
            CloseConfirmation();
            var shade = ui.Panel(Root, "Album confirmation overlay", new Rect(0, 0, width, height), new Color(.08f, .15f, .25f, .45f), true);
            confirmation = shade.gameObject;
            float w = Mathf.Min(680, width - 48), h = Mathf.Min(350, height - 48);
            var sheet = ui.Panel(shade, "Album confirmation", new Rect((width - w) / 2, (height - h) / 2, w, h), Color.white, true);
            ui.Label(sheet, "Album confirmation title", title, new Rect(24, 16, w - 48, 64), 30, true);
            ui.Label(sheet, "Album confirmation message", message, new Rect(24, 94, w - 48, h - 178), 21);
            ui.Button(sheet, "Album cancel " + kind, new Rect(24, h - 66, (w - 60) / 2, 46), T("cancel"), CloseConfirmation, WorkshopIcon.Back);
            ui.Button(sheet, "Album confirm " + kind, new Rect((w + 12) / 2, h - 66, (w - 60) / 2, 46), actionText,
                () => { CloseConfirmation(); action(); }, WorkshopIcon.Check, true);
            WorkshopModal.Open(confirmation, CloseConfirmation);
        }
        void CloseConfirmation() { if (confirmation != null) WorkshopUi.RemoveView(confirmation); confirmation = null; }
    }
}
