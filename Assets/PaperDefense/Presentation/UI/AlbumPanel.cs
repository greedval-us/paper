using System;
using System.Linq;
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
        public RectTransform Root { get; }
        public AlbumFilter Filter { get; private set; }
        public string SelectedId { get; private set; }
        string T(string key, params object[] args) => ui.Resolve(UserText.Message("assembly.album." + key, args));
        AlbumEntry Selected => run.Workshop.Entries.FirstOrDefault(entry => entry.Id == SelectedId);
        float PageInset => width >= 1250 ? Mathf.Min(112, width * .075f) : 24;

        public AlbumPanel(Transform parent, RunController run, UserTextResolver resolver,
            Action<AlbumEntry> edit, Action close, Action assigned = null, int? destinationSlot = null)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            if (destinationSlot.HasValue && (destinationSlot < 0 || destinationSlot > 7)) throw new ArgumentOutOfRangeException(nameof(destinationSlot));
            ui = new WorkshopUi(resolver); this.edit = edit; this.close = close; this.assigned = assigned; this.destinationSlot = destinationSlot;
            Root = ui.Panel(parent, "Album panel", new Rect(0, 0, width, height), WorkshopUi.Paper, true);
            Refresh();
        }

        public void Reflow(float width, float height)
        {
            this.width = Mathf.Max(800, width); this.height = Mathf.Max(600, height);
            WorkshopUi.Place(Root, new Rect(0, 0, this.width, this.height)); Refresh();
        }

        public void SetFilter(AlbumFilter value) { Filter = value; feedback = ""; Refresh(); }
        public void SelectEntry(string id)
        {
            SelectedId = run.Workshop.Entries.Any(entry => entry.Id == id) ? id : null;
            Refresh();
        }

        bool IsStandard(AlbumEntry entry) => run.AssemblyCatalog?.Starters.Any(starter => starter.Id == entry.Id) == true;
        bool Matches(AlbumEntry entry)
        {
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
                if (SelectedId != null && Selected == null) SelectedId = null;
                WorkshopUi.ClearViews(Root); confirmation = null;
                Periphery(); Header(); Slots(); Filters();
                float bodyY = 232, bodyHeight = height - bodyY - 92;
                float innerWidth = width - PageInset * 2;
                float catalogWidth = (innerWidth - 16) * .72f;
                var entries = run.Workshop.Entries.Where(Matches).ToArray();
                if (entries.Length == 0) Empty(new Rect(PageInset, bodyY, innerWidth, bodyHeight));
                else
                {
                    Cards(entries, new Rect(PageInset, bodyY, catalogWidth, bodyHeight));
                    Inspector(new Rect(PageInset + catalogWidth + 16, bodyY, innerWidth - catalogWidth - 16, bodyHeight));
                }
                Footer();
                if (focus != null && EventSystem.current != null)
                {
                    var next = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(control => control.name == focus && control.IsInteractable());
                    if (next != null) EventSystem.current.SetSelectedGameObject(next.gameObject);
                }
            }
            finally { refreshing = false; }
        }

        void Header()
        {
            float innerWidth = width - PageInset * 2;
            ui.Label(Root, "Album title", destinationSlot.HasValue ? T("destination", destinationSlot.Value + 1) : T("title"),
                new Rect(PageInset, 10, innerWidth * .33f, 48), 32, true);
            var resources = ui.Label(Root, "Album resources", T("resources", run.Workshop.Gold, run.Workshop.Level, run.Workshop.TotalXp),
                new Rect(PageInset + innerWidth * .34f, 16, innerWidth * .4f - 12, 40), 19, color: WorkshopUi.Muted);
            resources.alignment = TextAnchor.MiddleCenter;
            ui.Button(Root, "Album new", new Rect(PageInset + innerWidth * .75f, 14, innerWidth * .25f, 44),
                T("new"), () => edit?.Invoke(null), WorkshopIcon.Plus, true).interactable = edit != null;
        }

        void Periphery()
        {
            float edge = Mathf.Max(58, PageInset + 12);
            void Art(string id, Rect bounds)
            {
                var art = WorkshopUi.Graphic<NotebookArtGraphic>(Root, "Album margin " + id, bounds);
                art.Bind(id); art.color = new Color(1, 1, 1, .95f);
            }
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -edge * .27f : width - edge * .78f;
                for (int tree = 0; tree < 5; tree++)
                {
                    float treeWidth = edge * (tree % 2 == 0 ? 1.02f : .72f);
                    float y = 188 + tree * (height - 270) / 5;
                    Art("terrain.pine", new Rect(x + tree % 2 * edge * .18f, y, treeWidth, treeWidth * 1.48f));
                    if (tree % 2 == 1) Art("terrain.rock", new Rect(x, y + treeWidth, edge, edge * .56f));
                }
            }
            if (PageInset >= 64)
            {
                var house = WorkshopUi.Graphic<Image>(Root, "Album workshop in margin", new Rect(4, 60, PageInset - 12, 122));
                house.sprite = Resources.Load<Sprite>("Art/Player/DrawingWorkshopHouse"); house.preserveAspect = true;
            }
            Art("object.castle", new Rect(width - 192, 60, 188, 118));
            Art("icon.flag", new Rect(width - edge + 8, height * .57f, edge * .6f, edge));
            Art("terrain.shrub", new Rect(0, height - 100, edge * 1.55f, 92));
            Art("terrain.rock", new Rect(width - edge * 1.4f, height - 92, edge * 1.45f, 88));
        }

        void Slots()
        {
            int columns = width < 1250 ? 4 : 8;
            float gap = 8, slotStart = PageInset + (columns == 8 ? 190 : 170);
            float slotEnd = width - PageInset - (columns == 8 ? 120 : 100);
            float slotWidth = (slotEnd - slotStart - gap * (columns - 1)) / columns;
            float slotHeight = columns == 4 ? 48 : 104;
            ui.Label(Root, "Album current loadout", "Текущий набор:", new Rect(PageInset, 86, slotStart - PageInset - 12, 58), 23, true);
            for (int index = 0; index < 8; index++)
            {
                int slot = index; var entry = run.Workshop.LoadoutEntries[index];
                string caption = entry == null ? T("slot_empty", index + 1) : T("slot", index + 1, ui.Resolve(entry.Name));
                var button = ui.Button(Root, "Album assign " + index,
                    new Rect(slotStart + index % columns * (slotWidth + gap), 72 + index / columns * 56, slotWidth, slotHeight),
                    caption, () => RequestAssign(slot), WorkshopIcon.Book);
                if (columns == 8 && entry != null)
                {
                    WorkshopUi.RemoveView(button.GetComponentInChildren<WorkshopIconGraphic>().gameObject);
                    var name = button.GetComponentInChildren<Text>();
                    WorkshopUi.Place(name.rectTransform, new Rect(6, 64, slotWidth - 12, 36));
                    name.text = ui.Resolve(entry.Name); name.fontSize = 14;
                    name.resizeTextForBestFit = true; name.resizeTextMinSize = 13; name.resizeTextMaxSize = 14;
                    name.alignment = TextAnchor.MiddleCenter;
                    DrawEntry(button.transform, entry, new Rect(8, 3, slotWidth - 16, 58));
                    var number = ui.Label(button.transform, "Album slot number " + index, (index + 1).ToString(), new Rect(6, 2, 22, 22), 15);
                    number.alignment = TextAnchor.UpperLeft;
                }
                button.interactable = !destinationSlot.HasValue && Selected != null;
                ui.SetSelected(button, destinationSlot == index || SelectedId != null && entry?.Id == SelectedId);
            }
        }

        void Filters()
        {
            string[] keys = { "all", "towers", "structures", "standard", "mine" };
            float gap = 8, tabWidth = (width - PageInset * 2 - gap * 4) * .72f / 5;
            for (int index = 0; index < keys.Length; index++)
            {
                var filter = (AlbumFilter)index;
                var button = ui.Button(Root, "Album filter " + filter,
                    new Rect(PageInset + index * (tabWidth + gap), 186, tabWidth, 38), T("filter." + keys[index]), () => SetFilter(filter));
                ui.SetSelected(button, Filter == filter);
            }
        }

        void Cards(AlbumEntry[] entries, Rect bounds)
        {
            int columns = width >= 1024 ? 4 : 2;
            const float gap = 12;
            float cardWidth = (bounds.width - gap * (columns - 1) - 8) / columns;
            float cardHeight = Mathf.Clamp((bounds.height - gap) / 2, 168, 340);
            var list = new CatalogScrollList(Root, "Album catalogue", bounds, entries.Length, columns, cardHeight, gap);
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
                var title = ui.Label(card, "Album card name", entry.Name, new Rect(10, 4, cardWidth - 20, 38), 21, true);
                title.resizeTextForBestFit = true; title.resizeTextMinSize = 15; title.resizeTextMaxSize = 19;
                var rule = WorkshopUi.Graphic<Image>(card, "Album card title rule", new Rect(10, 43, cardWidth - 20, 1));
                rule.color = new Color(.12f, .28f, .52f, .55f);
                DrawEntry(card, entry, new Rect(5, 48, cardWidth - 10, cardHeight - 82));
                var resolved = run.PreviewAlbumEntry(entry, MaterialTier.Pencil);
                AlbumIcon(card, "Album card pencil", "icon.pencil", new Rect(10, cardHeight - 34, 25, 28));
                var price = ui.Label(card, "Album card price", resolved.Valid ? T("price", resolved.Value.Cost) : T("unavailable"),
                    new Rect(40, cardHeight - 34, cardWidth - 48, 28), 18);
                price.resizeTextForBestFit = true; price.resizeTextMinSize = 14; price.resizeTextMaxSize = 18;
                price.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        void Empty(Rect bounds)
        {
            var sheet = ui.Panel(Root, "Album empty sheet", bounds, Color.clear);
            float imageHeight = Mathf.Max(120, Mathf.Min(430, bounds.height - 200));
            float imageWidth = Mathf.Min(imageHeight * (1862f / 845f), bounds.width * .88f);
            imageHeight = imageWidth * (845f / 1862f);
            float imageX = (bounds.width - imageWidth) * .5f;
            var title = ui.Label(sheet, "Album empty", T(Filter == AlbumFilter.Mine ? "empty_mine" : "empty"),
                new Rect(24, 0, bounds.width - 48, 50), 34, true); title.alignment = TextAnchor.MiddleCenter;
            var hint = ui.Label(sheet, "Album empty explanation", T("empty_hint"), new Rect(24, 44, bounds.width - 48, 42), 19, color: WorkshopUi.Muted);
            hint.alignment = TextAnchor.MiddleCenter;
            var illustration = WorkshopUi.Graphic<Image>(sheet, "Album empty notebook", new Rect(imageX, 80, imageWidth, imageHeight));
            illustration.sprite = Resources.Load<Sprite>("Art/Player/AlbumOpenBookV2"); illustration.preserveAspect = true;
            illustration.rectTransform.pivot = new Vector2(.5f, .5f);
            illustration.rectTransform.anchoredPosition = new Vector2(imageX + imageWidth * .5f, -80 - imageHeight * .5f);
            var starter = run.AssemblyCatalog?.Starters.FirstOrDefault(entry => entry.Kind == AlbumEntryKind.Tower);
            if (starter != null)
            {
                var left = WorkshopUi.Graphic<WorkshopAssemblyDiagram>(illustration.transform, "Album empty foundation study",
                    new Rect(imageWidth * .10f, imageHeight * .12f, imageWidth * .34f, imageHeight * .70f));
                left.Configure(starter.Snapshot.Recipe, WorkshopComponentKind.Weapon, starter.Snapshot.Recipe.Tower.Mounts[0].SlotIndex);
                var right = WorkshopUi.Graphic<WeaponAssemblyGraphic>(illustration.transform, "Album empty assembled study",
                    new Rect(imageWidth * .55f, imageHeight * .16f, imageWidth * .34f, imageHeight * .66f));
                right.SetRecipe(starter.Snapshot);
            }
            if (NotebookSpriteCatalog.TryGet("icon.ink-blue", out var inkBottle))
            {
                var ink = WorkshopUi.Graphic<Image>(sheet, "Album empty ink bottle",
                    new Rect(Mathf.Max(16, imageX - 24), 84 + imageHeight - 56, 62, 76));
                ink.sprite = inkBottle; ink.preserveAspect = true;
            }
            float actionWidth = Mathf.Min(320, bounds.width - 48);
            ui.Button(sheet, "Album empty create", new Rect((bounds.width - actionWidth) / 2, bounds.height - 110, actionWidth, 48),
                T("new"), () => edit?.Invoke(null), WorkshopIcon.Pencil, true).interactable = edit != null;
            ui.Button(sheet, "Album show standard", new Rect((bounds.width - actionWidth) / 2, bounds.height - 58, actionWidth, 44),
                T("show_standard"), () => SetFilter(AlbumFilter.Standard), WorkshopIcon.Book);
        }

        void Inspector(Rect bounds)
        {
            var sheet = ui.Panel(Root, "Album inspector", bounds, Color.white);
            var entry = Selected;
            if (entry == null)
            {
                ui.Label(sheet, "Album selection hint", T("choose"), new Rect(16, 18, bounds.width - 32, 80), 25, true);
                return;
            }
            float w = bounds.width, h = bounds.height;
            ui.Label(sheet, "Album selected name", entry.Name, new Rect(16, 4, w - 32, 46), 27, true);
            var rule = WorkshopUi.Graphic<Image>(sheet, "Album selected title rule", new Rect(16, 51, w - 32, 1));
            rule.color = new Color(.12f, .28f, .52f, .55f);
            float statisticsY = h - 254;
            float artHeight = Mathf.Max(68, statisticsY - 64);
            DrawEntry(sheet, entry, new Rect(8, 56, w - 16, artHeight));
            var resolved = run.PreviewAlbumEntry(entry, MaterialTier.Pencil);
            if (resolved.Valid)
            {
                InspectorMetric(sheet, "Health", "icon.heart", null, resolved.Value.Health.ToString("0.##"), statisticsY, w);
                InspectorMetric(sheet, "Load", null, WorkshopIcon.Shield, resolved.Value.Load + " / " + resolved.Value.Capacity, statisticsY + 40, w);
                InspectorMetric(sheet, "Cost", "icon.pencil", null, T("price", resolved.Value.Cost), statisticsY + 80, w);
            }
            else ui.Label(sheet, "Album selected statistics", T("unavailable"), new Rect(16, statisticsY, w - 32, 112), 19, color: WorkshopUi.Warning);
            var editButton = ui.Button(sheet, "Album edit", new Rect(16, h - 120, w - 32, 36), T("edit"), () => edit?.Invoke(Selected), WorkshopIcon.Pencil);
            editButton.interactable = edit != null;
            ui.Button(sheet, "Album variant", new Rect(16, h - 80, w - 32, 36), T("variant"), () => {
                var result = run.AssemblyCommands.SaveEntryAsVariant(Selected, Guid.NewGuid());
                if (result.Success) SelectedId = result.ResultId;
                feedback = ResultText(result, "variant_saved"); Refresh();
            }, WorkshopIcon.Copy).interactable = entry.Kind != AlbumEntryKind.Legacy;
            ui.Button(sheet, "Album delete", new Rect(16, h - 40, w - 32, 36), T("delete"), RequestDelete, WorkshopIcon.Trash);
        }

        void InspectorMetric(Transform parent, string stat, string artId, WorkshopIcon? symbol, string value, float y, float w)
        {
            if (artId != null)
            {
                AlbumIcon(parent, "Album metric icon " + stat, artId, new Rect(14, y + 5, 30, 29));
            }
            else
            {
                var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, "Album metric icon " + stat, new Rect(17, y + 8, 24, 24)); icon.Icon = symbol.Value; icon.color = WorkshopUi.Blue;
            }
            float valueWidth = Mathf.Clamp(w * .42f, 86, 166);
            var label = ui.Label(parent, "Album metric label " + stat, UserText.Message("assembly.component.stat." + stat), new Rect(50, y + 2, w - valueWidth - 68, 36), 21);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 16; label.resizeTextMaxSize = 21;
            var number = ui.Label(parent, "Album metric value " + stat, value, new Rect(w - valueWidth - 16, y + 2, valueWidth, 36), 25);
            number.alignment = TextAnchor.MiddleRight; number.resizeTextForBestFit = true; number.resizeTextMinSize = 17; number.resizeTextMaxSize = 25;
            var rule = WorkshopUi.Graphic<Image>(parent, "Album metric rule " + stat, new Rect(16, y + 39, w - 32, 1)); rule.color = new Color(.12f, .28f, .52f, .35f);
        }

        static void AlbumIcon(Transform parent, string name, string id, Rect bounds)
        {
            var image = WorkshopUi.Graphic<Image>(parent, name, bounds);
            if (NotebookSpriteCatalog.TryGet(id, out var sprite)) image.sprite = sprite; else image.enabled = false;
            image.preserveAspect = true;
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition = new Vector2(bounds.center.x, -bounds.center.y);
        }

        void DrawEntry(Transform parent, AlbumEntry entry, Rect bounds)
        {
            if (entry.Kind == AlbumEntryKind.Legacy)
            {
                var legacy = WorkshopUi.Graphic<BlueprintSketch>(parent, "Album legacy illustration", bounds); legacy.SetBlueprint(entry.LegacyBlueprint); return;
            }
            if (entry.Kind == AlbumEntryKind.Tower || entry.Kind == AlbumEntryKind.Structure)
            {
                var exact = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, "Album certified illustration", bounds);
                try { exact.SetRecipe(entry.Snapshot); return; }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                catch (NotSupportedException) { }
                WorkshopUi.RemoveView(exact.gameObject);
            }
            string art = entry.Kind == AlbumEntryKind.Structure ? entry.Snapshot.Recipe.Structure.Family switch {
                AssemblyStructureFamily.Wall => "ui.tool.wall", AssemblyStructureFamily.Slow => "ui.tool.tar",
                AssemblyStructureFamily.Repair => "ui.tool.repair", _ => "ui.material.pencil" }
                : "ui.weapon." + entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id.Substring("core.".Length);
            var schematic = WorkshopUi.Graphic<NotebookArtGraphic>(parent, "Album family illustration", new Rect(bounds.x, bounds.y, bounds.width, Mathf.Max(16, bounds.height - 22)));
            schematic.Bind(art);
            var caption = ui.Label(parent, "Album schematic caption", T("schematic"), new Rect(bounds.x, bounds.yMax - 22, bounds.width, 22), 15, color: WorkshopUi.Muted);
            caption.alignment = TextAnchor.MiddleCenter;
        }

        void Footer()
        {
            string status = feedback;
            if (string.IsNullOrEmpty(status) && Selected != null && Matches(Selected) && !destinationSlot.HasValue) status = T("choose_slot");
            ui.Label(Root, "Album status", status, new Rect(PageInset, height - 86, width - PageInset * 2, 30), 17, color: WorkshopUi.Warning);
            ui.Button(Root, "Album restore", new Rect(PageInset, height - 52, 310, 42), T("restore"), () => {
                feedback = ResultText(run.AssemblyCommands.RestoreStarterEntries(Guid.NewGuid()), "restored"); Refresh();
            }, WorkshopIcon.Undo);
            ui.Button(Root, "Album close", new Rect(width - PageInset - 220, height - 52, 220, 42), T("close"), close, WorkshopIcon.Back);
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
