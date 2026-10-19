using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>The album editor owns one draft; nested components and durable commands stay explicit.</summary>
    public sealed partial class CompositeWorkshopPanel
    {
        readonly RunController run;
        readonly WorkshopUi ui;
        readonly Action prepare;
        readonly Dictionary<AlbumEntryKind, RouteDraft> routes = new Dictionary<AlbumEntryKind, RouteDraft>();
        sealed class RouteDraft { public WorkshopDraftSession Session; public int Step, Mount; }
        float width = 1600, height = 1000;
        int mount;
        bool libraryOpen;
        bool rebuilding;
        string feedback = "";
        public RectTransform Root { get; }
        public WorkshopDraftSession DraftSession { get; private set; }
        public int Step { get; private set; }
        public AlbumEntryKind Route => DraftSession.Draft.Kind;
        public string Status { get; private set; } = "";
        public TrialReport LastTrial { get; private set; }
        public Action BackRequested { get; set; }
        public void BeginNewEntry() => NewEntry(AlbumEntryKind.Tower);
        string T(string key, params object[] args) => ui.Resolve(UserText.Message("assembly.workshop." + key, args));
        bool HasStatusFailure => !(DraftSession.IsComponentOpen ? DraftSession.CanSaveComponent : DraftSession.CanSave) || DraftSession.LastResult?.Success == false;
        bool HasWriteFailure => DraftSession.LastResult?.Success == false && DraftSession.LastResult.Code == "profile.write_failed";
        string NoticeCopy => HasStatusFailure && !DraftSession.IsComponentOpen && !libraryOpen && (Step == 2 || Route == AlbumEntryKind.Legacy) ? "" : Status;
        string SaveCaption => T(HasWriteFailure ? "retry_save" : "save");

        public CompositeWorkshopPanel(Transform parent, RunController run, UserTextResolver resolver, Action prepare)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.prepare = prepare; ui = new WorkshopUi(resolver);
            Root = ui.Panel(parent, "Composite workshop", new Rect(0, 0, width, height), WorkshopUi.Paper, true);
            DraftSession = new WorkshopDraftSession(run);
            NewEntry(AlbumEntryKind.Tower);
        }

        public void OpenEntry(AlbumEntry entry)
        {
            if (entry == null) return;
            ResetAssemblyTrial();
            RememberRoute();
            DraftSession = new WorkshopDraftSession(run, DraftSession.Catalog);
            DraftSession.Begin(entry); Step = 2; mount = 0; feedback = ""; libraryOpen = false; LastTrial = null;
            Root.gameObject.SetActive(true); Refresh();
        }
        public void Suspend() { ui.HideTooltip(); Root.gameObject.SetActive(false); }
        public void Resume() { Root.gameObject.SetActive(true); Refresh(); }
        public void Reflow(float width, float height)
        {
            this.width = Mathf.Max(800, width); this.height = Mathf.Max(600, height);
            WorkshopUi.Place(Root, new Rect(0, 0, this.width, this.height)); Refresh();
        }
        void NewEntry(AlbumEntryKind kind)
        {
            ResetAssemblyTrial();
            var source = DraftSession.Catalog.Starters.First(entry => entry.Kind == kind);
            DraftSession = new WorkshopDraftSession(run, DraftSession.Catalog);
            DraftSession.Begin(new AlbumEntry("entry." + Guid.NewGuid().ToString("N"),
                T(kind == AlbumEntryKind.Tower ? "new_tower" : "new_structure"), 1, source.Snapshot));
            Step = 0; mount = 0; feedback = ""; libraryOpen = false; LastTrial = null; Refresh();
        }
        void RememberRoute()
        {
            if (DraftSession.Draft != null && Route != AlbumEntryKind.Legacy)
                routes[Route] = new RouteDraft { Session = DraftSession, Step = Step, Mount = mount };
        }
        void SwitchRoute(AlbumEntryKind kind)
        {
            if (DraftSession.IsComponentOpen) return;
            if (Route == kind) { libraryOpen = false; Refresh(); return; }
            ResetAssemblyTrial();
            RememberRoute();
            if (!routes.TryGetValue(kind, out var previous)) { NewEntry(kind); return; }
            DraftSession = previous.Session; Step = previous.Step; mount = previous.Mount;
            libraryOpen = false; feedback = ""; LastTrial = null; Refresh();
        }

        public void Refresh()
        {
            if (rebuilding) return;
            rebuilding = true;
            try
            {
            CaptureAssemblyTrialPlayback();
            var currentFocus = EventSystem.current?.currentSelectedGameObject;
            string focusName = currentFocus != null && currentFocus.transform.IsChildOf(Root) ? currentFocus.name : null;
            ui.HideTooltip();
            WorkshopUi.ClearViews(Root);
            var pageDecor = WorkshopUi.Graphic<Image>(Root, "Composite page landscape", new Rect(-12, height - 228, width + 24, 228));
            pageDecor.sprite = Resources.Load<Sprite>("Art/Player/FoundationPageDecor");
            pageDecor.raycastTarget = false; pageDecor.color = new Color(1, 1, 1, .75f);
            Header();
            float bodyY = 124, bodyHeight = height - 232;
            var body = ui.Panel(Root, "Composite content", new Rect(24, bodyY, width - 48, bodyHeight), Color.clear);
            if (DraftSession.IsComponentOpen)
            {
                if (DraftSession.ActiveComponent == WorkshopComponentKind.Structure)
                {
                    var panel = new StructureAssemblyPanel(body, run, DraftSession, ui.TextResolver, RefreshStatus);
                    panel.Reflow(width - 48, bodyHeight);
                }
                else
                {
                    var panel = new ComponentAssemblyPanel(body, run, DraftSession, ui.TextResolver, RefreshStatus);
                    panel.Reflow(width - 48, bodyHeight);
                }
            }
            else if (libraryOpen) Library(body, width - 48, bodyHeight);
            else
            {
                float summaryWidth = (width - 64) * .39f;
                float pageWidth = width - 64 - summaryWidth;
                bool weaponsLayout = Route == AlbumEntryKind.Tower && Step == 1;
                var page = ui.Panel(body, "Composite page", new Rect(weaponsLayout ? summaryWidth + 16 : 0, 0, pageWidth, bodyHeight), WorkshopUi.Surface);
                if (Route == AlbumEntryKind.Legacy || Step == 2) Review(page, pageWidth, bodyHeight);
                else if (Route == AlbumEntryKind.Structure) Structures(page, pageWidth, bodyHeight);
                else if (Step == 0) Foundations(page, pageWidth, bodyHeight);
                else Weapons(page, pageWidth, bodyHeight);
                Summary(body, new Rect(weaponsLayout ? 0 : pageWidth + 16, 0, summaryWidth, bodyHeight));
            }
            Status = StatusText();
            var notice = ui.Label(Root, "Composite notice", NoticeCopy, new Rect(26, height - 100, width - 52, 28), 17,
                color: HasStatusFailure ? WorkshopUi.Warning : WorkshopUi.Muted);
            notice.verticalOverflow = VerticalWrapMode.Truncate;
            Footer();
            if (assemblyTrialOpen) BuildAssemblyTrialSurface();
            ui.EnsureTooltip(Root);
            if (focusName != null && EventSystem.current != null)
            {
                var controls = Root.GetComponentsInChildren<Selectable>();
                var focus = controls.FirstOrDefault(item => item.name == focusName && item.IsInteractable())
                    ?? controls.FirstOrDefault(item => item.IsInteractable() &&
                        item.name == (DraftSession.IsComponentOpen ? "Composite cancel component" : Step == 2 ? "Composite save" : "Composite next"));
                if (focus != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
            }
            }
            finally { rebuilding = false; }
        }

        public void RefreshStatus()
        {
            if (Root == null || rebuilding) return;
            var resources = Root.Find("Composite resources")?.GetComponent<Text>();
            if (resources != null) resources.text = T("resources", run.Workshop.Gold, run.Workshop.Level, run.Workshop.TotalXp);
            Status = StatusText();
            var notice = Root.Find("Composite notice")?.GetComponent<Text>();
            if (notice != null) { notice.text = NoticeCopy; notice.color = HasStatusFailure ? WorkshopUi.Warning : WorkshopUi.Muted; }
            var validation = Root.Find("Composite content/Composite summary/Composite validation")?.GetComponent<Text>();
            if (validation != null)
            {
                validation.text = Status; validation.color = HasStatusFailure ? WorkshopUi.Warning : WorkshopUi.Success;
                float summaryWidth = ((RectTransform)validation.transform.parent).rect.width;
                WorkshopUi.Place(validation.rectTransform, HasWriteFailure ? new Rect(78, 148, summaryWidth - 104, 64) : new Rect(18, 150, summaryWidth - 36, 60));
            }
            foreach (string name in new[] { "Composite save warning", "Composite save warning mark" })
            {
                var warning = Root.Find("Composite content/Composite summary/" + name);
                if (warning != null) warning.gameObject.SetActive(HasWriteFailure);
            }
            var previewTitle = Root.Find("Composite content/Composite summary/Composite preview title")?.GetComponent<Text>();
            if (previewTitle != null) previewTitle.text = DraftSession.Name;
            foreach (var button in Root.GetComponentsInChildren<Button>())
            {
                switch (button.name)
                {
                    case "Composite save": button.interactable = DraftSession.CanSave; button.GetComponentInChildren<Text>().text = SaveCaption; break;
                    case "Composite variant": button.interactable = DraftSession.CanSave; break;
                    case "Composite save component": button.interactable = DraftSession.CanSaveComponent; break;
                    case "Composite trial": button.interactable = Route != AlbumEntryKind.Legacy && DraftSession.CanTrial; break;
                    case "Composite route tower": case "Composite route structure": case "Composite library": button.interactable = !DraftSession.IsComponentOpen; break;
                }
            }
        }

        void Header()
        {
            ui.Label(Root, "Composite title", T("title"), new Rect(24, 8, width * .35f, 50), 32, true);
            var resources = ui.Label(Root, "Composite resources", T("resources", run.Workshop.Gold, run.Workshop.Level, run.Workshop.TotalXp),
                new Rect(width * .38f, 12, width * .62f - 244, 38), 18);
            resources.alignment = TextAnchor.MiddleRight;
            float routeX = BackRequested == null ? 24 : 82;
            var tower = ui.Button(Root, "Composite route tower", new Rect(routeX, 66, 130, 44), T("tower"), () => SwitchRoute(AlbumEntryKind.Tower), WorkshopIcon.Tower);
            var support = ui.Button(Root, "Composite route structure", new Rect(routeX + 142, 66, 150, 44), T("structure"), () => SwitchRoute(AlbumEntryKind.Structure), WorkshopIcon.Shield);
            tower.interactable = support.interactable = !DraftSession.IsComponentOpen;
            ui.SetSelected(tower, Route == AlbumEntryKind.Tower); ui.SetSelected(support, Route == AlbumEntryKind.Structure);
            ui.Button(Root, "Composite library", new Rect(width - 220, 8, 196, 44), T("library"), () => { libraryOpen = !libraryOpen; Refresh(); }, WorkshopIcon.Book)
                .interactable = !DraftSession.IsComponentOpen;
            if (BackRequested != null)
                ui.IconButton(Root, "Composite return", new Rect(24, 66, 46, 44), WorkshopIcon.Back,
                    UserText.Message("assembly.workshop.return"), BackRequested)
                    .interactable = !DraftSession.IsComponentOpen;
            string[] stages = Route == AlbumEntryKind.Structure ? new[] { "family", "composition", "review" } : new[] { "foundation", "weapons", "review" };
            float stepX = routeX + 316;
            float stepWidth = (width - stepX - 48) / 3;
            for (int i = 0; i < 3; i++)
            {
                int target = i;
                var button = ui.Button(Root, "Composite step " + i, new Rect(stepX + i * (stepWidth + 12), 66, stepWidth, 44),
                    (i + 1) + "  " + T(stages[i]), () => { if (!DraftSession.IsComponentOpen) { Step = target; libraryOpen = false; Refresh(); } });
                button.interactable = !DraftSession.IsComponentOpen && (Route != AlbumEntryKind.Legacy || i == 2);
                ui.SetSelected(button, i == Step);
            }
        }

        void Footer()
        {
            var footer = ui.Panel(Root, "Composite footer", new Rect(24, height - 72, width - 48, 56), WorkshopUi.Paper);
            float available = width - 48, third = (available - 24) / 3;
            if (DraftSession.IsComponentOpen)
            {
                ui.Button(footer, "Composite cancel component", new Rect(0, 0, third, 56), T("cancel"), () => { DraftSession.CancelComponent(); feedback = ""; Refresh(); }, WorkshopIcon.Back);
                ui.Button(footer, "Composite save component", new Rect(available - third * 1.7f, 0, third * 1.7f, 56),
                    T(DraftSession.ActiveComponent == WorkshopComponentKind.Structure ? "apply_structure" : "save_component"), () => {
                        DraftSession.SaveComponent(Guid.NewGuid()); feedback = ""; Refresh();
                    }, WorkshopIcon.Save, true).interactable = DraftSession.CanSaveComponent;
                return;
            }
            if (libraryOpen)
            {
                ui.Button(footer, "Composite library back", new Rect(0, 0, third, 56), T("library_back"), () => { libraryOpen = false; Refresh(); }, WorkshopIcon.Back);
                return;
            }
            ui.Button(footer, "Composite back", new Rect(0, 0, third, 56), T("back"), () => { if (Step > 0 && Route != AlbumEntryKind.Legacy) Step--; Refresh(); }, WorkshopIcon.Back)
                .interactable = Step > 0 && Route != AlbumEntryKind.Legacy;
            if (Step == 2)
            {
                ui.Button(footer, "Composite variant", new Rect(third + 12, 0, third, 56), T("variant"), () => Save(true), WorkshopIcon.Copy)
                    .interactable = DraftSession.CanSave;
                ui.Button(footer, "Composite save", new Rect((third + 12) * 2, 0, third, 56), SaveCaption, () => Save(false), WorkshopIcon.Save, true)
                    .interactable = DraftSession.CanSave;
            }
            else
            {
                ui.Button(footer, "Composite preparation", new Rect(third + 12, 0, third, 56), T("preparation"), () => { Suspend(); prepare?.Invoke(); }, WorkshopIcon.Book);
                ui.Button(footer, "Composite next", new Rect((third + 12) * 2, 0, third, 56), T(Step == 1 ? "review" : "next"), () => { Step = Mathf.Min(2, Step + 1); feedback = ""; Refresh(); }, WorkshopIcon.Next, true);
            }
        }

        void Save(bool variant)
        {
            var result = DraftSession.SaveEntry(Guid.NewGuid(), variant);
            feedback = result.Success ? T("saved") : ""; Refresh();
        }

        void Foundations(Transform parent, float w, float h)
        {
            ui.Label(parent, "Composite foundation heading", T("foundation"), new Rect(18, 8, w - 36, 44), 28, true);
            ui.Label(parent, "Composite foundation hint", T("foundation_hint"), new Rect(18, 48, w - 36, 32), 17);
            ui.Button(parent, "Composite edit foundation", new Rect(18, h - 60, w - 36, 44), T("edit_foundation"), () => { DraftSession.OpenFoundation(); Refresh(); }, WorkshopIcon.Settings);
            var frames = DraftSession.Catalog.Definitions.Where(part => part.Category == ComponentCategory.Frame).ToArray();
            float cardHeight = Mathf.Max(154, (h - 174) * .5f);
            var list = new CatalogScrollList(parent, "Composite foundation catalog", new Rect(18, 88, w - 36, h - 162), frames.Length, 2, cardHeight, 12);
            float cardWidth = (w - 60) / 2;
            for (int i = 0; i < frames.Length; i++)
            {
                var part = frames[i]; var card = ui.Panel(list.Content, "Composite frame " + part.Id, new Rect(i % 2 * (cardWidth + 12), i / 2 * (cardHeight + 12), cardWidth, cardHeight), WorkshopUi.Paper);
                ui.Label(card, "Composite frame name", part.Name, new Rect(12, 2, cardWidth - 24, 26), 21, true);
                var selected = DraftSession.PreviewRecipe.Tower.Foundation;
                FoundationPicture(card, new FoundationRecipe(new PartRef(part.Id, part.Revision), selected.Hull, selected.Armor, selected.Feed, selected.Turn),
                    "Composite frame illustration", new Rect(10, 30, cardWidth - 20, cardHeight - 80));
                ui.Label(card, "Composite frame values", T("foundation_values", part.Get(AssemblyStat.MountCount, ModifierOperation.Set), part.Get(AssemblyStat.Capacity, ModifierOperation.Set)), new Rect(12, cardHeight - 50, cardWidth - 24, 22), 15);
                bool owned = DraftSession.IsOwned(part.Id);
                ui.Button(card, "Composite choose frame " + part.Id, new Rect(12, cardHeight - 28, cardWidth - 24, 26), T(owned ? "choose" : "composition"), () => {
                    if (owned) DraftSession.ApplyPreset(FoundationPreset(part), -1);
                    else DraftSession.OpenFoundation();
                    feedback = ""; Refresh();
                }, owned ? WorkshopIcon.Check : WorkshopIcon.Settings);
                ui.SetSelected(card.GetComponentInChildren<Button>(), DraftSession.PreviewRecipe.Tower.Foundation.Frame.Id == part.Id);
            }
        }

        AlbumEntry FoundationPreset(ComponentDefinition frame)
        {
            var current = DraftSession.PreviewRecipe.Tower;
            var old = current.Foundation;
            return Preset(new AssemblyRecipe(new TowerRecipe(new FoundationRecipe(new PartRef(frame.Id, frame.Revision), old.Hull, old.Armor, old.Feed, old.Turn), current.Mounts)), frame.Name);
        }
        AlbumEntry Preset(AssemblyRecipe recipe, string name)
            => new AlbumEntry("preset." + Guid.NewGuid().ToString("N"), name, 1, new RecipeSnapshot(recipe, DraftSession.Catalog.BalanceRevision, DraftSession.Catalog.Definitions));
        WeaponRecipe WeaponPreset(ComponentDefinition mechanism)
        {
            PartRef Part(ComponentCategory category)
            {
                var part = DraftSession.Catalog.Definitions.Where(item => item.Category == category && (item.WeaponFamilies & mechanism.WeaponFamilies) != 0)
                    .OrderBy(item => item.UnlockGold).ThenBy(item => item.RequiredLevel).First();
                return new PartRef(part.Id, part.Revision);
            }
            return new WeaponRecipe(new PartRef(mechanism.Id, mechanism.Revision), Part(ComponentCategory.Head), Part(ComponentCategory.Sight), Part(ComponentCategory.Magazine), Part(ComponentCategory.Trigger));
        }

        void Weapons(Transform parent, float w, float h)
        {
            ui.Label(parent, "Composite weapon heading", T("weapons"), new Rect(18, 8, w - 36, 44), 28, true);
            var tower = DraftSession.PreviewRecipe.Tower;
            var frame = DraftSession.PreviewEntry.Snapshot.Definitions.First(part => part.Id == tower.Foundation.Frame.Id);
            int count = Mathf.Clamp((int)frame.Get(AssemblyStat.MountCount, ModifierOperation.Set), 1, 3);
            mount = Mathf.Clamp(mount, 0, count - 1); float slotWidth = (w - 48) / count;
            for (int i = 0; i < count; i++)
            {
                int slot = i; var button = ui.Button(parent, "Composite mount " + i, new Rect(18 + i * (slotWidth + 6), 58, slotWidth, 42), T("select_mount", i + 1), () => { mount = slot; Refresh(); }); ui.SetSelected(button, mount == i);
            }
            var edit = ui.Button(parent, "Composite edit weapon", new Rect(18, h - 60, w - 36 - (mount > 0 ? 64 : 0), 44), T("edit_weapon", mount + 1), () => { DraftSession.OpenWeapon(mount); Refresh(); }, WorkshopIcon.Settings);
            edit.interactable = tower.Mounts.Any(item => item.SlotIndex == mount);
            if (mount > 0) ui.IconButton(parent, "Composite remove weapon", new Rect(w - 70, h - 60, 52, 44), WorkshopIcon.Trash, T("remove_weapon", mount + 1), () => { DraftSession.RemoveWeapon(mount); Refresh(); });
            var mechanisms = DraftSession.Catalog.Definitions.Where(part => part.Category == ComponentCategory.Mechanism).ToArray();
            int columns = w >= 520 ? 4 : 3;
            float cardHeight = Mathf.Max(142, (h - 190) * .5f);
            var list = new CatalogScrollList(parent, "Composite weapon catalog", new Rect(18, 106, w - 36, h - 178), mechanisms.Length, columns, cardHeight, 12);
            for (int i = 0; i < mechanisms.Length; i++)
            {
                int rowColumns = columns == 4 && i >= 4 ? 3 : columns;
                int column = columns == 4 && i >= 4 ? i - 4 : i % columns;
                float cardWidth = (w - 48 - 12 * (rowColumns - 1)) / rowColumns;
                var part = mechanisms[i]; var card = ui.Panel(list.Content, "Composite weapon " + part.Id, new Rect(column * (cardWidth + 12), i / columns * (cardHeight + 12), cardWidth, cardHeight), WorkshopUi.Paper);
                WeaponPicture(card, WeaponPreset(part), "Composite weapon illustration", new Rect(8, 6, cardWidth - 16, cardHeight - 68));
                var name = ui.Label(card, "Composite weapon name", part.Name, new Rect(8, cardHeight - 62, cardWidth - 16, 28), 21, true);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 15; name.resizeTextMaxSize = 21;
                name.alignment = TextAnchor.MiddleCenter;
                bool owned = DraftSession.IsOwned(part.Id);
                ui.Button(card, "Composite choose weapon " + part.Id, new Rect(12, cardHeight - 30, cardWidth - 24, 28), T(owned ? "choose" : "composition"), () => {
                    if (owned)
                    {
                        var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, WeaponPreset(part)) }));
                        DraftSession.ApplyPreset(Preset(recipe, part.Name), mount);
                    }
                    else if (tower.Mounts.Any(item => item.SlotIndex == mount)) DraftSession.OpenWeapon(mount);
                    feedback = ""; Refresh();
                }, WorkshopIcon.None);
            }
        }

        void Structures(Transform parent, float w, float h)
        {
            ui.Label(parent, "Composite structure heading", T(Step == 0 ? "family" : "composition"), new Rect(18, 8, w - 36, 44), 28, true);
            ui.Label(parent, "Composite structure hint", T("structure_hint"), new Rect(18, 54, w - 36, 44), 18);
            var starters = DraftSession.Catalog.Starters.Where(entry => entry.Kind == AlbumEntryKind.Structure).ToArray();
            float cardHeight = Mathf.Max(180, (h - 192) * .5f);
            var list = new CatalogScrollList(parent, "Composite structure families", new Rect(18, 108, w - 36, h - 184), starters.Length, 2, cardHeight, 12);
            float cardWidth = (w - 60) / 2;
            for (int i = 0; i < starters.Length; i++)
            {
                var source = starters[i]; var card = ui.Panel(list.Content, "Composite family " + source.Snapshot.Recipe.Structure.Family, new Rect(i % 2 * (cardWidth + 12), i / 2 * (cardHeight + 12), cardWidth, cardHeight), WorkshopUi.Paper);
                ui.Label(card, "Composite family name", source.Name, new Rect(12, 6, cardWidth - 24, 40), 24, true);
                RecipePicture(card, source.Snapshot, "Composite structure illustration", new Rect(12, 48, cardWidth - 24, cardHeight - 98));
                ui.Button(card, "Composite choose family " + source.Snapshot.Recipe.Structure.Family, new Rect(12, cardHeight - 42, cardWidth - 24, 34), T("choose"), () => {
                    var old = DraftSession.Draft;
                    string pendingName = DraftSession.Name;
                    DraftSession.Begin(new AlbumEntry(old.Id, old.Name, old.Revision, source.Snapshot));
                    DraftSession.Rename(pendingName); feedback = ""; Refresh();
                }, WorkshopIcon.Check);
            }
            ui.Button(parent, "Composite edit structure", new Rect(18, h - 60, w - 36, 44), T("composition"), () => { DraftSession.OpenStructure(); Refresh(); }, WorkshopIcon.Settings);
        }

        void Review(Transform parent, float w, float h)
        {
            ui.Label(parent, "Composite review heading", T("review"), new Rect(18, 8, w - 36, 44), 28, true);
            float illustrationHeight = Mathf.Max(190, h - 158);
            if (Route == AlbumEntryKind.Legacy)
            {
                var picture = WorkshopUi.Graphic<BlueprintSketch>(parent, "Composite assembled preview", new Rect(14, 58, w - 28, illustrationHeight));
                picture.SetBlueprint(DraftSession.Draft.LegacyBlueprint);
            }
            else DrawRecipePreview(parent, DraftSession.PreviewRecipe, w, illustrationHeight);
            float compositionHeight = Mathf.Max(60, h - illustrationHeight - 70);
            var composition = new CatalogScrollList(parent, "Composite review composition viewport", new Rect(18, illustrationHeight + 64, w - 36, compositionHeight), 1, 1, h, 0);
            var compositionText = ui.Label(composition.Content, "Composite review composition", CompositionText(), new Rect(0, 0, w - 54, compositionHeight), 16);
            compositionText.alignment = TextAnchor.UpperLeft;
            float contentHeight = Mathf.Max(80, compositionText.preferredHeight + 12);
            WorkshopUi.Place(compositionText.rectTransform, new Rect(0, 0, w - 54, contentHeight));
            if (DraftSession.PreviewMetrics?.Overloaded == true)
            {
                var entry = DraftSession.PreviewEntry;
                var contributors = DraftSession.PreviewRecipe.Parts.Select(reference => entry.Snapshot.Definitions.First(part => part.Id == reference.Id && part.Revision == reference.Revision))
                    .Where(part => part.Get(AssemblyStat.Load, ModifierOperation.Add) != 0)
                    .Select(part => ui.Resolve(part.Name) + " " + part.Get(AssemblyStat.Load, ModifierOperation.Add).ToString("+0.##;-0.##;0"));
                var label = ui.Label(composition.Content, "Composite load contributors", UserText.Message("assembly.component.load_parts", string.Join(" · ", contributors)),
                    new Rect(0, contentHeight + 6, w - 54, 100), 18, color: WorkshopUi.Warning);
                label.alignment = TextAnchor.UpperLeft;
                float contributionHeight = Mathf.Max(56, label.preferredHeight + 12);
                WorkshopUi.Place(label.rectTransform, new Rect(0, contentHeight + 6, w - 54, contributionHeight));
                contentHeight += contributionHeight + 6;
            }
            composition.Content.sizeDelta = new Vector2(0, Mathf.Max(compositionHeight, contentHeight));
        }

        void Summary(Transform parent, Rect bounds)
        {
            var sheet = ui.Panel(parent, "Composite summary", bounds, WorkshopUi.Surface);
            float w = bounds.width, h = bounds.height;
            ui.Label(sheet, "Composite preview title", DraftSession.Name, new Rect(18, 8, w - 36, 44), 27, true);
            bool review = Step == 2 || Route == AlbumEntryKind.Legacy;
            float imageHeight = Mathf.Max(150, h - 246);
            var recipe = DraftSession.PreviewRecipe;
            if (review)
            {
                ui.Label(sheet, "Composite name label", T("name"), new Rect(18, 58, w - 36, 28), 18);
                var fieldRoot = ui.Panel(sheet, "Composite name", new Rect(18, 90, w - 36, 48), WorkshopUi.Paper, true);
                var field = fieldRoot.gameObject.AddComponent<InputField>();
                var text = ui.Label(fieldRoot, "Composite name value", DraftSession.Name, new Rect(12, 2, w - 60, 44), 23);
                field.textComponent = text; field.text = DraftSession.Name; field.characterLimit = 0;
                field.interactable = Route != AlbumEntryKind.Legacy;
                var nameSession = DraftSession;
                field.onEndEdit.AddListener(value => {
                    nameSession.Rename(value);
                    if (ReferenceEquals(nameSession, DraftSession)) { feedback = ""; RefreshStatus(); }
                });
                var warning = ui.Panel(sheet, "Composite save warning", new Rect(18, 146, w - 36, 68), new Color32(255, 232, 211, 255));
                warning.gameObject.SetActive(HasWriteFailure);
                var warningMark = WorkshopUi.Graphic<WorkshopSaveFailureMark>(sheet, "Composite save warning mark", new Rect(28, 161, 40, 40));
                warningMark.gameObject.SetActive(HasWriteFailure);
                var ready = ui.Label(sheet, "Composite validation", StatusText(), HasWriteFailure ? new Rect(78, 148, w - 104, 64) : new Rect(18, 150, w - 36, 60), HasWriteFailure ? 17 : 20,
                    color: HasStatusFailure ? WorkshopUi.Warning : WorkshopUi.Success);
                ready.alignment = TextAnchor.MiddleLeft;
            }
            else DrawRecipePreview(sheet, recipe, w, imageHeight);
            var resolved = DraftSession.Resolved;
            var metrics = DraftSession.PreviewMetrics;
            float y = review ? 218 : 68 + imageHeight;
            string loadCost = resolved != null ? T("load_cost", resolved.Load, resolved.Capacity, resolved.Cost)
                : metrics != null ? T("load_cost", metrics.Load, metrics.Capacity, metrics.Cost) : T("clearance");
            string health = resolved != null ? T("health", resolved.Health.ToString("0.#"), resolved.BuildSeconds.ToString("0.##"))
                : metrics != null ? T("health", metrics.Health.ToString("0.#"), metrics.BuildSeconds.ToString("0.##")) : "";
            ui.Label(sheet, "Composite load cost", loadCost, new Rect(18, y, w - 36, 34), 21);
            ui.Label(sheet, "Composite health", health, new Rect(18, y + 38, w - 36, 32), 18);
            string effect = "";
            if (resolved?.Support != null)
            {
                var support = resolved.Support;
                if (support.Kind == AssemblyStructureFamily.Generator) effect = T("generator", support.IncomePerWave, support.PaybackWaves(resolved.Cost));
                else if (support.Kind == AssemblyStructureFamily.Repair) effect = T("repair", support.RepairPerSecond, support.Radius);
                else if (support.Kind == AssemblyStructureFamily.Slow) effect = T("slow", support.SlowSpeedMultiplier, support.Radius, support.SlowSeconds);
                else effect = T("wall");
            }
            else if (resolved != null) effect = T("dps", resolved.Dps.ToString("0.##"));
            ui.Label(sheet, "Composite effect", effect, new Rect(18, y + 76, w - 36, review ? Mathf.Max(50, h - y - 152) : 38), 18);
            if (!review)
            {
                var composition = ui.Label(sheet, "Composite selected composition", CompositionText(true), new Rect(18, y + 118, w - 36, Mathf.Max(0, h - y - 124)), 15);
                composition.alignment = TextAnchor.UpperLeft;
            }
            else
            {
                var trial = ui.Button(sheet, "Composite trial", new Rect(18, h - 64, w - 36, 48), T("trial"), OpenTrial, WorkshopIcon.Target);
                trial.interactable = Route != AlbumEntryKind.Legacy && DraftSession.CanTrial;
            }
        }

        void DrawRecipePreview(Transform parent, AssemblyRecipe recipe, float w, float imageHeight)
        {
            var bounds = new Rect(14, 58, w - 28, imageHeight);
            WorkshopScenery(parent, "Composite preview scenery", bounds, .55f);
            if (recipe.Tower != null && Step == 0 && FoundationPicture(parent, recipe.Tower.Foundation, "Composite certified foundation preview", bounds)) return;
            if (RecipePicture(parent, DraftSession.PreviewEntry.Snapshot, "Composite certified recipe preview", new Rect(14, 58, w - 28, imageHeight))) return;
            if (recipe.Tower != null)
            {
                var exact = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, "Composite certified recipe preview", new Rect(14, 58, w - 28, imageHeight));
                bool supported = false;
                try { exact.SetRecipe(DraftSession.PreviewEntry.Snapshot); supported = true; }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                catch (NotSupportedException) { }
                if (supported) return;
                WorkshopUi.RemoveView(exact.gameObject);
                var schematic = WorkshopUi.Graphic<BlueprintSketch>(parent, "Composite family schematic", new Rect(14, 58, w - 28, imageHeight - 30));
                schematic.SetBlueprint(PreviewBlueprint(recipe.Tower));
            }
            else StructurePicture(parent, recipe.Structure.Family, new Rect(14, 58, w - 28, imageHeight - 30));
            var caption = ui.Label(parent, "Composite schematic caption", T("neutral"), new Rect(18, 58 + imageHeight - 28, w - 36, 26), 17, color: WorkshopUi.Muted);
            caption.alignment = TextAnchor.MiddleCenter;
        }

        bool RecipePicture(Transform parent, RecipeSnapshot snapshot, string name, Rect bounds)
        {
            var picture = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, name, bounds);
            try { picture.SetRecipe(snapshot); return true; }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            WorkshopUi.RemoveView(picture.gameObject); return false;
        }

        void PartPicture(Transform parent, ComponentDefinition part, string name, Rect bounds)
        {
            var reference = new PartRef(part.Id, part.Revision);
            if (!AssemblyVisualComposer.HasVisualMapping(reference)) return;
            var picture = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, name, bounds);
            picture.SetPart(reference);
        }

        void WeaponPicture(Transform parent, WeaponRecipe weapon, string name, Rect bounds)
        {
            var picture = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, name, bounds);
            try { picture.SetComposition(AssemblyVisualComposer.ComposeWeapon(weapon)); }
            catch (ArgumentException) { WorkshopUi.RemoveView(picture.gameObject); }
            catch (InvalidOperationException) { WorkshopUi.RemoveView(picture.gameObject); }
            catch (NotSupportedException) { WorkshopUi.RemoveView(picture.gameObject); }
        }

        bool FoundationPicture(Transform parent, FoundationRecipe foundation, string name, Rect bounds)
        {
            var picture = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, name, bounds);
            try { picture.SetComposition(AssemblyVisualComposer.ComposeFoundation(foundation)); return true; }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            WorkshopUi.RemoveView(picture.gameObject); return false;
        }

        static void WorkshopScenery(Transform parent, string name, Rect bounds, float opacity)
        {
            var scenery = WorkshopUi.Graphic<Image>(parent, name, bounds);
            scenery.sprite = Resources.Load<Sprite>("Art/Player/FoundationPreviewScenery");
            scenery.preserveAspect = true; scenery.raycastTarget = false; scenery.color = new Color(1, 1, 1, opacity);
        }

        TowerBlueprint PreviewBlueprint(TowerRecipe tower)
        {
            string frame = "chassis." + tower.Foundation.Frame.Id.Substring("frame.".Length);
            var weapons = tower.Mounts.Select(item => new WeaponMountSelection(item.SlotIndex, "weapon." + item.Weapon.Mechanism.Id.Substring("core.".Length)));
            var modules = tower.Foundation.Armor.Id == "armor.none" ? Array.Empty<ModuleSelection>() : new[] { new ModuleSelection("preview.armor", "module.armor") };
            return new TowerBlueprint("preview.assembly", "Preview", frame, weapons, modules, content: run.Content);
        }
        void StructurePicture(Transform parent, AssemblyStructureFamily family, Rect bounds)
        {
            if (family == AssemblyStructureFamily.Generator)
            {
                var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, "Composite generator schematic", bounds); icon.Icon = WorkshopIcon.Ink; icon.color = WorkshopUi.Blue;
            }
            else
            {
                var image = WorkshopUi.Graphic<NotebookArtGraphic>(parent, "Composite structure illustration", bounds);
                image.Bind(family == AssemblyStructureFamily.Wall ? "object.barricade-site" : family == AssemblyStructureFamily.Repair ? "object.repair" : "object.tar");
            }
        }

        string CompositionText(bool activeOnly = false)
        {
            if (Route == AlbumEntryKind.Legacy) return T("readonly");
            var entry = DraftSession.PreviewEntry; var recipe = DraftSession.PreviewRecipe;
            string PartName(PartRef part)
            {
                var definition = entry.Snapshot.Definitions.FirstOrDefault(item => item.Id == part.Id && item.Revision == part.Revision);
                if (definition == null) DraftSession.Catalog.TryGet(part.Id, part.Revision, out definition);
                return definition == null ? T("clearance") : ui.Resolve(definition.Name);
            }
            if (recipe.Structure != null) return string.Join("\n", recipe.Structure.Parts.Select(PartName));
            if (activeOnly)
            {
                var selected = Step == 0 ? recipe.Tower.Foundation.Parts : recipe.Tower.Mounts.FirstOrDefault(item => item.SlotIndex == mount)?.Weapon.Parts;
                return selected == null ? T("no_weapon") : string.Join(" · ", selected.Select(PartName));
            }
            var lines = new List<string> { T("foundation") + ": " + string.Join(" · ", recipe.Tower.Foundation.Parts.Select(PartName)) };
            foreach (var weapon in recipe.Tower.Mounts) lines.Add(T("select_mount", weapon.SlotIndex + 1) + ": " + string.Join(" · ", weapon.Weapon.Parts.Select(PartName)));
            return string.Join("\n\n", lines);
        }

        string StatusText()
        {
            if (!string.IsNullOrEmpty(feedback)) return feedback;
            if (Route == AlbumEntryKind.Legacy) return T("readonly");
            var failure = DraftSession.LastResult;
            if (failure != null && !failure.Success) return Diagnostic(failure.Code);
            if (DraftSession.Diagnostics.Count > 0) return string.Join(" · ", DraftSession.Diagnostics.Select(item => Diagnostic(item.Code)).Distinct().Take(2));
            return T("ready");
        }
        string Diagnostic(string code) => ui.Resolve(UserText.Message(code));

        void Library(Transform parent, float w, float h)
        {
            ui.Label(parent, "Composite library title", T("library_title"), new Rect(18, 8, w - 36, 44), 28, true);
            var entries = run.Workshop.Library.ToArray();
            if (entries.Length == 0) { ui.Label(parent, "Composite library empty", T("library_empty"), new Rect(30, 84, w - 60, 120), 24); return; }
            const float cardHeight = 224;
            var list = new CatalogScrollList(parent, "Composite component library", new Rect(18, 72, w - 36, h - 90), entries.Length, 1, cardHeight, 12);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i]; var card = ui.Panel(list.Content, "Composite library " + entry.Id, new Rect(0, i * (cardHeight + 12), w - 54, cardHeight), WorkshopUi.Surface);
                var picture = WorkshopUi.Graphic<WeaponAssemblyGraphic>(card, "Composite library illustration", new Rect(16, 12, 180, 144));
                try { picture.SetComposition(entry.Kind == ComponentLibraryKind.Foundation ? AssemblyVisualComposer.ComposeFoundation(entry.Snapshot.Foundation) : AssemblyVisualComposer.ComposeWeapon(entry.Snapshot.Weapon)); }
                catch (ArgumentException) { WorkshopUi.RemoveView(picture.gameObject); }
                catch (InvalidOperationException) { WorkshopUi.RemoveView(picture.gameObject); }
                catch (NotSupportedException) { WorkshopUi.RemoveView(picture.gameObject); }
                ui.Label(card, "Composite library name", entry.Name, new Rect(212, 4, w - 282, 40), 25, true);
                ui.Label(card, "Composite library kind", T(entry.Kind == ComponentLibraryKind.Foundation ? "foundation" : "weapons"), new Rect(212, 46, w - 282, 30), 18);
                ui.Label(card, "Composite library owned parts", string.Join(" · ", entry.Snapshot.Definitions.Select(part => ui.Resolve(part.Name))), new Rect(212, 82, w - 282, 76), 17);
                ui.Button(card, "Composite apply library " + entry.Id, new Rect(16, 174, (w - 110) * .6f, 42), T("library_choose"), () => {
                    DraftSession.ApplyLibrary(entry, entry.Kind == ComponentLibraryKind.Foundation ? -1 : mount); libraryOpen = false; feedback = ""; Refresh();
                }, WorkshopIcon.Check).interactable = Route == AlbumEntryKind.Tower && !DraftSession.IsComponentOpen;
                ui.Button(card, "Composite delete library " + entry.Id, new Rect(32 + (w - 110) * .6f, 174, (w - 110) * .4f, 42), T("library_delete"), () => ConfirmDelete(entry), WorkshopIcon.Trash);
            }
        }
        void ConfirmDelete(ComponentLibraryEntry entry)
        {
            var shade = ui.Panel(Root, "Composite delete overlay", new Rect(0, 0, width, height), new Color(.08f, .15f, .25f, .45f), true);
            float w = Mathf.Min(720, width - 80);
            var card = ui.Panel(shade, "Composite delete confirmation", new Rect((width - w) / 2, (height - 280) / 2, w, 280), WorkshopUi.Paper, true);
            ui.Label(card, "Composite delete question", T("delete_question", entry.Name), new Rect(28, 20, w - 56, 152), 23);
            Action close = () => { if (shade != null) WorkshopUi.RemoveView(shade.gameObject); };
            ui.Button(card, "Composite cancel delete", new Rect(28, 196, (w - 68) / 2, 52), T("cancel"), close, WorkshopIcon.Back);
            ui.Button(card, "Composite confirm delete", new Rect(40 + (w - 68) / 2, 196, (w - 68) / 2, 52), T("library_delete"), () => {
                var result = run.AssemblyCommands.DeleteLibraryEntry(entry.Id, Guid.NewGuid());
                feedback = result.Success ? T("library_deleted") : Diagnostic(result.Code); Refresh();
            }, WorkshopIcon.Trash, true);
            WorkshopModal.Open(shade.gameObject, close);
        }

    }

    /// <summary>A font-independent warning mark for the failed-save card.</summary>
    public sealed class WorkshopSaveFailureMark : MaskableGraphic
    {
        readonly MeshPen pen = new MeshPen(null, 64);
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); pen.Clear();
            var rect = rectTransform.rect; var center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .43f;
            var ink = new Color32(152, 42, 43, 255);
            var top = center + Vector2.up * radius;
            var left = center + new Vector2(-radius, -radius * .8f);
            var right = center + new Vector2(radius, -radius * .8f);
            pen.Line(top, left, 2.3f, ink); pen.Line(left, right, 2.3f, ink); pen.Line(right, top, 2.3f, ink);
            pen.Line(center + Vector2.up * radius * .38f, center - Vector2.up * radius * .16f, 2.5f, ink);
            pen.Disc(center - Vector2.up * radius * .47f, radius * .085f, ink, 10);
            pen.CopyTo(target, Vector2.zero, 1);
        }
    }
}
