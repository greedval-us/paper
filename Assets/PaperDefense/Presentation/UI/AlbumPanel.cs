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
                Header(); Slots(); Filters();
                float bodyY = 254, bodyHeight = height - bodyY - 106;
                float catalogWidth = (width - 64) * .7f;
                var entries = run.Workshop.Entries.Where(Matches).ToArray();
                if (entries.Length == 0) Empty(new Rect(24, bodyY, catalogWidth, bodyHeight));
                else Cards(entries, new Rect(24, bodyY, catalogWidth, bodyHeight));
                Inspector(new Rect(40 + catalogWidth, bodyY, width - catalogWidth - 64, bodyHeight));
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
            ui.Label(Root, "Album title", destinationSlot.HasValue ? T("destination", destinationSlot.Value + 1) : T("title"),
                new Rect(24, 10, width * .48f, 48), 32, true);
            var resources = ui.Label(Root, "Album resources", T("resources", run.Workshop.Gold, run.Workshop.Level, run.Workshop.TotalXp),
                new Rect(width * .49f, 16, width * .51f - 24, 40), 19, color: WorkshopUi.Muted);
            resources.alignment = TextAnchor.MiddleRight;
        }

        void Slots()
        {
            int columns = width < 1250 ? 4 : 8;
            float gap = 8, slotWidth = (width - 48 - gap * (columns - 1)) / columns;
            float slotHeight = columns == 4 ? 48 : 104;
            for (int index = 0; index < 8; index++)
            {
                int slot = index; var entry = run.Workshop.LoadoutEntries[index];
                string caption = entry == null ? T("slot_empty", index + 1) : T("slot", index + 1, ui.Resolve(entry.Name));
                var button = ui.Button(Root, "Album assign " + index,
                    new Rect(24 + index % columns * (slotWidth + gap), 72 + index / columns * 56, slotWidth, slotHeight),
                    caption, () => RequestAssign(slot), WorkshopIcon.Book);
                button.interactable = !destinationSlot.HasValue && Selected != null;
                ui.SetSelected(button, destinationSlot == index || SelectedId != null && entry?.Id == SelectedId);
            }
        }

        void Filters()
        {
            string[] keys = { "all", "towers", "structures", "standard", "mine" };
            float gap = 10, tabWidth = (width - 48 - gap * 4) / 5;
            for (int index = 0; index < keys.Length; index++)
            {
                var filter = (AlbumFilter)index;
                var button = ui.Button(Root, "Album filter " + filter,
                    new Rect(24 + index * (tabWidth + gap), 198, tabWidth, 44), T("filter." + keys[index]), () => SetFilter(filter));
                ui.SetSelected(button, Filter == filter);
            }
        }

        void Cards(AlbumEntry[] entries, Rect bounds)
        {
            int columns = width >= 1500 ? 4 : 2;
            const float gap = 12;
            float cardWidth = (bounds.width - gap * (columns - 1) - 8) / columns;
            float cardHeight = Mathf.Clamp((bounds.height - gap) / 2, 168, 246);
            var list = new CatalogScrollList(Root, "Album catalogue", bounds, entries.Length, columns, cardHeight, gap);
            for (int index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                var card = ui.Panel(list.Content, "Album card " + entry.Id,
                    new Rect(index % columns * (cardWidth + gap), index / columns * (cardHeight + gap), cardWidth, cardHeight),
                    SelectedId == entry.Id ? WorkshopUi.Selected : Color.white, true);
                // The full card is the choose target; decorative children never intercept pointer input.
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card.GetComponent<WorkshopSurfaceGraphic>();
                card.gameObject.AddComponent<UiScrollFocus>();
                button.name = "Album choose " + entry.Id;
                button.onClick.AddListener(() => { SelectedId = entry.Id; feedback = ""; if (destinationSlot.HasValue) RequestAssign(destinationSlot.Value); else Refresh(); });
                DrawEntry(card, entry, new Rect(8, 6, cardWidth - 16, cardHeight - 92));
                var title = ui.Label(card, "Album card name", entry.Name, new Rect(10, cardHeight - 82, cardWidth - 20, 46), 19, true);
                title.resizeTextForBestFit = true; title.resizeTextMinSize = 15; title.resizeTextMaxSize = 19;
                var resolved = run.PreviewAlbumEntry(entry, MaterialTier.Pencil);
                ui.Label(card, "Album card price", resolved.Valid ? T("price", resolved.Value.Cost) : T("unavailable"),
                    new Rect(10, cardHeight - 32, cardWidth - 20, 26), 16, color: WorkshopUi.Muted);
            }
        }

        void Empty(Rect bounds)
        {
            var sheet = ui.Panel(Root, "Album empty sheet", bounds, Color.white);
            float imageHeight = Mathf.Min(130, bounds.height * .3f);
            var illustration = WorkshopUi.Graphic<WorkshopIconGraphic>(sheet, "Album empty notebook", new Rect(bounds.width * .35f, 12, bounds.width * .3f, imageHeight));
            illustration.Icon = WorkshopIcon.Book; illustration.color = WorkshopUi.Blue;
            var title = ui.Label(sheet, "Album empty", T(Filter == AlbumFilter.Mine ? "empty_mine" : "empty"),
                new Rect(24, imageHeight + 14, bounds.width - 48, 52), 27, true); title.alignment = TextAnchor.MiddleCenter;
            var hint = ui.Label(sheet, "Album empty explanation", T("empty_hint"), new Rect(24, imageHeight + 72, bounds.width - 48, 60), 19, color: WorkshopUi.Muted);
            hint.alignment = TextAnchor.MiddleCenter;
            float actionWidth = Mathf.Min(320, bounds.width - 48);
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
            ui.Label(sheet, "Album selected name", entry.Name, new Rect(16, 4, w - 32, 62), 24, true);
            float artHeight = Mathf.Max(64, h - 282);
            DrawEntry(sheet, entry, new Rect(16, 70, w - 32, artHeight));
            var resolved = run.PreviewAlbumEntry(entry, MaterialTier.Pencil);
            string statistics = resolved.Valid ? T("statistics", resolved.Value.Cost, resolved.Value.Health.ToString("0.##"), resolved.Value.Load, resolved.Value.Capacity) : T("unavailable");
            ui.Label(sheet, "Album selected statistics", statistics, new Rect(16, h - 204, w - 32, 60), 17, color: WorkshopUi.Muted);
            var editButton = ui.Button(sheet, "Album edit", new Rect(16, h - 140, w - 32, 42), T("edit"), () => edit?.Invoke(Selected), WorkshopIcon.Pencil);
            editButton.interactable = edit != null;
            ui.Button(sheet, "Album variant", new Rect(16, h - 94, w - 32, 42), T("variant"), () => {
                var result = run.AssemblyCommands.SaveEntryAsVariant(Selected, Guid.NewGuid());
                if (result.Success) SelectedId = result.ResultId;
                feedback = ResultText(result, "variant_saved"); Refresh();
            }, WorkshopIcon.Copy).interactable = entry.Kind != AlbumEntryKind.Legacy;
            ui.Button(sheet, "Album delete", new Rect(16, h - 48, w - 32, 42), T("delete"), RequestDelete, WorkshopIcon.Trash);
        }

        void DrawEntry(Transform parent, AlbumEntry entry, Rect bounds)
        {
            if (entry.Kind == AlbumEntryKind.Legacy)
            {
                var legacy = WorkshopUi.Graphic<BlueprintSketch>(parent, "Album legacy illustration", bounds); legacy.SetBlueprint(entry.LegacyBlueprint); return;
            }
            if (entry.Kind == AlbumEntryKind.Tower)
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
            if (string.IsNullOrEmpty(status) && Selected != null && !destinationSlot.HasValue) status = T("choose_slot");
            ui.Label(Root, "Album status", status, new Rect(24, height - 98, width - 48, 34), 17, color: WorkshopUi.Warning);
            ui.Button(Root, "Album new", new Rect(24, height - 58, 190, 44), T("new"), () => edit?.Invoke(null), WorkshopIcon.Plus, true).interactable = edit != null;
            ui.Button(Root, "Album restore", new Rect(226, height - 58, 310, 44), T("restore"), () => {
                feedback = ResultText(run.AssemblyCommands.RestoreStarterEntries(Guid.NewGuid()), "restored"); Refresh();
            }, WorkshopIcon.Undo);
            ui.Button(Root, "Album close", new Rect(width - 204, height - 58, 180, 44), T("close"), close, WorkshopIcon.Back);
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
