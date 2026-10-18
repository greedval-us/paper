using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Inspection, permanent purchase and installing a part are separate actions.</summary>
    public sealed class ComponentCatalogPanel
    {
        readonly WorkshopUi ui;
        readonly RunController run;
        readonly WorkshopDraftSession draft;
        readonly Action changed;
        string inspectedId;
        ComponentCategory previousCategory;
        bool hasCategory, showUnavailable;
        float width = 976, height = 472;
        GameObject purchase;
        public RectTransform Root { get; }
        public bool ShowUnavailable => showUnavailable;
        string Copy(string key, params object[] args) => ui.Resolve(UserText.Message("assembly.component." + key, args));

        public ComponentCatalogPanel(Transform parent, RunController run, WorkshopDraftSession draft,
            UserTextResolver resolver, Action changed)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.draft = draft ?? throw new ArgumentNullException(nameof(draft)); this.changed = changed;
            ui = new WorkshopUi(resolver);
            Root = ui.Panel(parent, "Component catalog", new Rect(0, 0, width, height), Color.clear);
        }
        public void Reflow(float width, float height)
        { this.width = width; this.height = height; Root.sizeDelta = new Vector2(width, height); Refresh(); }
        public void SetShowUnavailable(bool value) { showUnavailable = value; Refresh(); }
        public void InspectPart(string id)
        {
            if (!draft.CompatibleParts.Any(p => p.Id == id)) return;
            inspectedId = id; Refresh();
        }
        ComponentDefinition Definition(string id) => draft.PreviewEntry?.Snapshot?.Definitions.FirstOrDefault(p => p.Id == id)
            ?? draft.Catalog?.Definitions.Where(p => p.Id == id).OrderByDescending(p => p.Revision).FirstOrDefault();
        IEnumerable<PartRef> ActiveParts()
        {
            var recipe = draft.PreviewRecipe;
            if (recipe == null) return Array.Empty<PartRef>();
            if (recipe.Structure != null) return recipe.Structure.Parts;
            return draft.ActiveComponent == WorkshopComponentKind.Foundation ? recipe.Tower.Foundation.Parts
                : recipe.Tower.Mounts.First(m => m.SlotIndex == draft.ActiveMount).Weapon.Parts;
        }
        string EquippedId() => ActiveParts().Select(p => Definition(p.Id)).FirstOrDefault(p => p?.Category == draft.ActiveCategory)?.Id;
        static string Signed(float number) => number.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
        string Contribution(ComponentDefinition part) => Copy("contribution", Signed(part.Get(AssemblyStat.Cost, ModifierOperation.Add)), Signed(part.Get(AssemblyStat.Load, ModifierOperation.Add)));

        public void Refresh()
        {
            if (Root == null) return;
            WorkshopUi.ClearViews(Root);
            if (!draft.IsComponentOpen) return;
            var compatible = draft.CompatibleParts;
            if (!hasCategory || previousCategory != draft.ActiveCategory)
            { inspectedId = EquippedId(); previousCategory = draft.ActiveCategory; hasCategory = true; }
            if (!compatible.Any(p => p.Id == inspectedId)) inspectedId = EquippedId() ?? compatible.FirstOrDefault()?.Id;
            float listWidth = Mathf.Clamp(width * .36f, 260, 410), gap = 18;
            ui.Label(Root, "Component catalogue heading", Copy("catalog"), new Rect(0, 0, listWidth, 42), 24, true);
            var toggle = ui.Button(Root, "Component show unavailable", new Rect(width - 350, 0, 350, 44),
                Copy("show_unavailable", Copy(showUnavailable ? "yes" : "no")), () => SetShowUnavailable(!showUnavailable));
            ui.SetSelected(toggle, showUnavailable);
            var visible = compatible.Where(p => showUnavailable || draft.IsOwned(p.Id)).ToArray();
            const float rowHeight = 146, rowGap = 10;
            var list = new CatalogScrollList(Root, "Component catalogue list", new Rect(0, 56, listWidth, height - 56), visible.Length, 1, rowHeight, rowGap);
            for (int i = 0; i < visible.Length; i++)
            {
                var part = visible[i]; bool owned = draft.IsOwned(part.Id);
                var button = ui.Button(list.Content, "Component inspect " + part.Id, new Rect(0, i * (rowHeight + rowGap), listWidth - 10, rowHeight), "", () => InspectPart(part.Id));
                ui.SetSelected(button, part.Id == inspectedId);
                ui.Label(button.transform, "Component part name", part.Name, new Rect(16, 8, listWidth - 46, 42), 22, true);
                ui.Label(button.transform, "Component part availability", owned ? Copy(part.Id == EquippedId() ? "equipped" : "owned") : Copy("conditions", part.RequiredLevel, part.UnlockGold),
                    new Rect(16, 54, listWidth - 46, 34), 17, color: owned ? WorkshopUi.Blue : WorkshopUi.Warning);
                ui.Label(button.transform, "Component part contribution", Contribution(part), new Rect(16, 92, listWidth - 46, 42), 17, color: WorkshopUi.Muted);
            }
            if (visible.Length == 0) ui.Label(list.Content, "Component empty catalogue", Copy("empty"), new Rect(16, 18, listWidth - 42, 120), 21);
            var inspected = compatible.FirstOrDefault(p => p.Id == inspectedId);
            if (inspected != null) Details(inspected, new Rect(listWidth + gap, 56, width - listWidth - gap, height - 56));
        }

        void Details(ComponentDefinition part, Rect bounds)
        {
            var details = ui.Panel(Root, "Component details", bounds, Color.white);
            float w = bounds.width, h = bounds.height, artWidth = w * .46f;
            bool owned = draft.IsOwned(part.Id);
            bool suggested = !string.IsNullOrEmpty(draft.SuggestedPartId);
            bool canBuy = !owned && run.Workshop.Level >= part.RequiredLevel && run.Workshop.Gold >= part.UnlockGold;
            bool secondaryAction = suggested || (!owned && !canBuy);
            float summaryY = Mathf.Max(154, h - 154 - (secondaryAction ? 50 : 0));
            float artHeight = summaryY - 54;
            ui.Label(details, "Component inspected name", part.Name, new Rect(16, 4, w - 32, 42), 26, true);
            DrawPreview(details, new Rect(12, 54, artWidth - 16, artHeight));
            ui.Label(details, "Component inspected conditions", Copy("conditions", part.RequiredLevel, part.UnlockGold), new Rect(artWidth + 6, 50, w - artWidth - 24, 38), 17);
            ui.Label(details, "Component inspected contribution", Contribution(part), new Rect(artWidth + 6, 88, w - artWidth - 24, 42), 17, color: WorkshopUi.Muted);
            var multipliers = part.Modifiers.Where(m => m.Operation == ModifierOperation.Multiply && m.Value != 1 &&
                (m.Stat == AssemblyStat.Damage || m.Stat == AssemblyStat.Range || m.Stat == AssemblyStat.Interval || m.Stat == AssemblyStat.Radius ||
                m.Stat == AssemblyStat.ReloadSeconds || m.Stat == AssemblyStat.MagazineSize || m.Stat == AssemblyStat.Feed || m.Stat == AssemblyStat.Defense ||
                m.Stat == AssemblyStat.Health || m.Stat == AssemblyStat.Cost || m.Stat == AssemblyStat.Load || m.Stat == AssemblyStat.SupportRate || m.Stat == AssemblyStat.Slow))
                .Select(m => Copy("stat." + m.Stat) + " ×" + m.Value.ToString("0.##", ui.TextResolver.Culture));
            string modifierText = string.Join(" · ", multipliers);
            if (part.Category == ComponentCategory.Magazine)
            {
                var weapon = draft.Resolved?.Weapons.FirstOrDefault(item => item.SlotIndex == draft.ActiveMount);
                if (weapon != null)
                    modifierText = Copy("magazine_stats", weapon.MagazineSize, weapon.Interval, weapon.ReloadSeconds);
            }
            ui.Label(details, "Component inspected modifiers", modifierText, new Rect(artWidth + 6, 130, w - artWidth - 24, Mathf.Max(20, summaryY - 134)), 16, color: WorkshopUi.Muted);
            var diagnostics = draft.Diagnostics.Concat(draft.LastResult.Success ? Array.Empty<AssemblyDiagnostic>() : draft.LastResult.Diagnostics)
                .GroupBy(d => d.Code).Select(g => g.First()).ToArray();
            string message = string.Join("\n", diagnostics.OrderBy(d => d.Code == "assembly.part.required" || d.Code == "assembly.load.exceeded" || d.Code == "profile.write_failed" ? 0 : 1).Take(1).Select(DiagnosticText));
            if (draft.ReplacedPartIds.Count > 0) message += (message.Length > 0 ? "\n" : "") + Copy("replaced", string.Join(", ", draft.ReplacedPartIds.Select(id => Definition(id)?.Name ?? "")));
            var overloaded = diagnostics.FirstOrDefault(d => d.Code == "assembly.load.exceeded");
            if (overloaded != null)
            {
                string contributions = string.Join("\n", draft.PreviewRecipe.Parts.Select(p => Definition(p.Id)).Where(d => d != null && d.Get(AssemblyStat.Load, ModifierOperation.Add) != 0)
                    .Select(d => d.Name + " " + Signed(d.Get(AssemblyStat.Load, ModifierOperation.Add))));
                var symbol = details.Find("Component preview symbol"); if (symbol != null) WorkshopUi.RemoveView(symbol.gameObject);
                ui.Label(details, "Component load contributions", Copy("load_parts", contributions), new Rect(16, 50, artWidth - 28, artHeight), 15);
            }
            var resolved = draft.Resolved;
            string summary = "";
            if (resolved?.Support != null)
                summary = Copy("resolved_support", resolved.Cost, resolved.Health, resolved.Support.Radius, resolved.Support.RepairPerSecond, resolved.Support.SlowSpeedMultiplier, resolved.Support.IncomePerWave);
            else if (resolved != null) summary = Copy("resolved_tower", resolved.Cost, resolved.Load, resolved.Capacity, resolved.Health, Math.Round(resolved.Dps, 2));
            else if (draft.PreviewMetrics != null)
            {
                var metrics = draft.PreviewMetrics;
                summary = Copy("preview_stats", metrics.Cost, metrics.Load, metrics.Capacity, metrics.Health);
            }
            ui.Label(details, "Component resolved statistics", summary, new Rect(16, summaryY, w - 32, 56), 18, color: WorkshopUi.Blue);
            ui.Label(details, "Component diagnostics", message, new Rect(16, summaryY + 58, w - 32, Mathf.Max(0, h - (secondaryAction ? 110 : 60) - summaryY - 58)), 16, color: WorkshopUi.Warning);
            if (suggested) ui.Button(details, "Component apply suggested part", new Rect(16, h - 104, w - 32, 44), Copy("stock_magazine"), () => {
                draft.SelectPart(draft.SuggestedPartId); Refresh(); changed?.Invoke(); }, WorkshopIcon.Copy);
            var buy = ui.Button(details, "Component purchase part", new Rect(16, h - 54, w - 32, 46), Copy("purchase", part.UnlockGold), () => OpenPurchase(part), WorkshopIcon.Cost, true);
            buy.interactable = canBuy;
            buy.gameObject.SetActive(!owned);
            string selectCaption = part.Category == ComponentCategory.Mechanism ? Copy("select_mechanism") : Copy("select");
            var select = ui.Button(details, "Component select part", new Rect(16, h - 54, w - 32, 46), selectCaption, () => {
                if (draft.IsOwned(part.Id) && draft.SelectPart(part.Id)) { Refresh(); changed?.Invoke(); }
            }, WorkshopIcon.Check, true);
            select.interactable = owned; select.gameObject.SetActive(owned || !buy.interactable);
            if (!owned && !buy.interactable)
            {
                WorkshopUi.Place(buy.GetComponent<RectTransform>(), new Rect(16, h - 104, w - 32, 44));
                // Both disabled actions remain readable when the player has not reached the unlock conditions.
            }
        }
        string DiagnosticText(AssemblyDiagnostic issue)
        {
            string text = ui.Resolve(UserText.Message(issue.Code));
            if (issue.Code == "profile.write_failed" && issue.Arguments.Count > 0)
                text += "\n" + ui.Resolve(issue.Arguments[0]);
            return text;
        }
        void DrawPreview(Transform parent, Rect rect)
        {
            var resolved = draft.Resolved;
            if (resolved != null && resolved.Kind == AlbumEntryKind.Tower)
            {
                var art = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, "Component resolved preview", rect);
                try { art.SetRecipe(resolved.Snapshot); return; }
                catch (ArgumentException) { WorkshopUi.RemoveView(art.gameObject); }
                catch (InvalidOperationException) { WorkshopUi.RemoveView(art.gameObject); }
                catch (NotSupportedException) { WorkshopUi.RemoveView(art.gameObject); }
            }
            var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, "Component preview symbol", rect);
            icon.Icon = draft.ActiveComponent == WorkshopComponentKind.Weapon ? WorkshopIcon.Weapon : WorkshopIcon.Tower;
            icon.color = WorkshopUi.Blue;
        }
        void OpenPurchase(ComponentDefinition part)
        {
            ClosePurchase(); ui.HideTooltip();
            var canvas = Root.GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.rootCanvas.transform : Root.parent;
            var parentRect = parent as RectTransform;
            float w = parentRect != null && parentRect.rect.width > 0 ? parentRect.rect.width : 1600;
            float h = parentRect != null && parentRect.rect.height > 0 ? parentRect.rect.height : 1000;
            var shade = ui.Panel(parent, "Component purchase overlay", new Rect(0, 0, w, h), new Color(.08f, .15f, .25f, .45f), true); purchase = shade.gameObject;
            float cw = Mathf.Min(720, w - 40), ch = Mathf.Min(460, h - 40);
            var card = ui.Panel(shade, "Component purchase sheet", new Rect((w - cw) * .5f, (h - ch) * .5f, cw, ch), WorkshopUi.Paper, true);
            ui.Label(card, "Component purchase title", Copy("purchase_title", part.Name), new Rect(24, 16, cw - 48, 74), 30, true);
            ui.Label(card, "Component purchase conditions", Copy("purchase_message", part.RequiredLevel, run.Workshop.Level, part.UnlockGold, run.Workshop.Gold, run.Workshop.Gold - part.UnlockGold),
                new Rect(24, 106, cw - 48, ch - 190), 22);
            ui.Button(card, "Component purchase cancel", new Rect(24, ch - 72, (cw - 60) * .5f, 48), Copy("cancel"), ClosePurchase, WorkshopIcon.Back);
            var confirm = ui.Button(card, "Component purchase confirm", new Rect(36 + (cw - 60) * .5f, ch - 72, (cw - 60) * .5f, 48), Copy("purchase", part.UnlockGold), () => {
                draft.PurchasePart(part.Id, Guid.NewGuid()); ClosePurchase(); Refresh(); changed?.Invoke();
            }, WorkshopIcon.Cost, true);
            confirm.interactable = !draft.IsOwned(part.Id) && run.Workshop.Level >= part.RequiredLevel && run.Workshop.Gold >= part.UnlockGold;
            WorkshopModal.Open(shade.gameObject, ClosePurchase);
        }
        public void ClosePurchase()
        { if (purchase != null) { WorkshopUi.RemoveView(purchase); purchase = null; } }
    }
}
