using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Inspection, permanent purchase and installing a part are separate actions.</summary>
    public sealed partial class ComponentCatalogPanel
    {
        readonly WorkshopUi ui;
        readonly RunController run;
        readonly WorkshopDraftSession draft;
        readonly Action changed;
        string inspectedId;
        ComponentCategory previousCategory;
        bool hasCategory, showUnavailable, revealInspected;
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
        { this.width = width; this.height = height; revealInspected = true; Root.sizeDelta = new Vector2(width, height); Refresh(); }
        public void SetShowUnavailable(bool value) { showUnavailable = value; revealInspected = true; Refresh(); }
        public void InspectPart(string id)
        {
            if (!draft.CompatibleParts.Any(p => p.Id == id)) return;
            inspectedId = id; revealInspected = true; Refresh();
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
        string Contribution(ComponentDefinition part, bool compact = false) => Copy(compact ? "contribution_short" : "contribution", Signed(part.Get(AssemblyStat.Cost, ModifierOperation.Add)), Signed(part.Get(AssemblyStat.Load, ModifierOperation.Add)));

        ResolvedEntry InspectedResolution(ComponentDefinition part)
        {
            var recipe = draft.PreviewRecipe;
            if (recipe == null) return null;
            var replacement = new PartRef(part.Id, part.Revision);
            PartRef[] Replace(IEnumerable<PartRef> parts) => parts.Select(reference => Definition(reference.Id)?.Category == part.Category ? replacement : reference).ToArray();
            AssemblyRecipe candidate;
            if (recipe.Structure != null)
            {
                var old = recipe.Structure; var parts = Replace(old.Parts);
                candidate = new AssemblyRecipe(new StructureRecipe(old.Family, parts[0], parts[1], parts[2], parts[3]));
            }
            else if (draft.ActiveComponent == WorkshopComponentKind.Foundation)
            {
                var parts = Replace(recipe.Tower.Foundation.Parts);
                candidate = new AssemblyRecipe(new TowerRecipe(new FoundationRecipe(parts[0], parts[1], parts[2], parts[3], parts[4]), recipe.Tower.Mounts));
            }
            else
            {
                var old = recipe.Tower.Mounts.Single(mount => mount.SlotIndex == draft.ActiveMount).Weapon; var parts = Replace(old.Parts);
                if (part.Category == ComponentCategory.Mechanism)
                {
                    // Match SelectPart's compatible replacement choice without installing it.
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var attached = Definition(parts[i].Id);
                        if ((attached.WeaponFamilies & part.WeaponFamilies) != 0) continue;
                        var compatible = draft.Catalog.Definitions.Where(definition => definition.Category == attached.Category &&
                            (definition.WeaponFamilies & part.WeaponFamilies) != 0 && draft.IsOwned(definition.Id))
                            .OrderBy(definition => definition.Get(AssemblyStat.Cost, ModifierOperation.Add))
                            .ThenBy(definition => definition.Id, StringComparer.Ordinal).FirstOrDefault();
                        if (compatible == null) return null;
                        parts[i] = new PartRef(compatible.Id, compatible.Revision);
                    }
                }
                var weapon = new WeaponRecipe(parts[0], parts[1], parts[2], parts[3], parts[4]);
                candidate = new AssemblyRecipe(new TowerRecipe(recipe.Tower.Foundation, recipe.Tower.Mounts.Select(mount =>
                    mount.SlotIndex == draft.ActiveMount ? new WeaponMountRecipe(mount.SlotIndex, weapon) : mount)));
            }
            // An inspection owns no rights or draft changes. Only the candidate's numerical resolver
            // result is shown; actual installation and save continue through the existing command gate.
            var definitions = draft.Catalog.Definitions.Concat(draft.PreviewEntry.Snapshot.Definitions).Concat(new[] { part })
                .GroupBy(definition => (definition.Id, definition.Revision)).Select(group => group.Last());
            try
            {
                var snapshot = new RecipeSnapshot(candidate, draft.Catalog.BalanceRevision, definitions);
                return run.PreviewAlbumEntry(new AlbumEntry("inspection.candidate", "Inspection", 1, snapshot)).Value;
            }
            catch (ArgumentException) { return null; }
        }

        string Comparison(ResolvedEntry current, ResolvedEntry candidate)
        {
            string Number(float value) => value.ToString("0.###", ui.TextResolver.Culture);
            var before = current.Weapons.FirstOrDefault(weapon => weapon.SlotIndex == draft.ActiveMount);
            var after = candidate.Weapons.FirstOrDefault(weapon => weapon.SlotIndex == draft.ActiveMount);
            if (current.Support != null && candidate.Support != null)
                return Copy("comparison_support", Number(current.Health), Number(candidate.Health), Number(current.Support.Radius), Number(candidate.Support.Radius),
                    Number(current.Support.RepairPerSecond), Number(candidate.Support.RepairPerSecond), Number(current.Support.SlowSpeedMultiplier), Number(candidate.Support.SlowSpeedMultiplier),
                    current.Support.IncomePerWave, candidate.Support.IncomePerWave, current.Cost, candidate.Cost);
            if (draft.ActiveComponent == WorkshopComponentKind.Foundation)
                return Copy("comparison_foundation", Number(current.Health), Number(candidate.Health), current.Capacity, candidate.Capacity,
                    Number(current.Defense * 100), Number(candidate.Defense * 100), Number(current.FrontDefense * 100), Number(candidate.FrontDefense * 100),
                    run.PreviewFoundationCost(current.Snapshot),
                    run.PreviewFoundationCost(candidate.Snapshot), current.Load, candidate.Load);
            if (before != null && after != null && (before.Family == AssemblyWeaponFamily.Mortar || after.Family == AssemblyWeaponFamily.Mortar))
                return Copy("comparison_mortar", Number(before.Damage), Number(after.Damage), Number(before.Range), Number(after.Range), Number(before.Radius), Number(after.Radius),
                    Number(before.Interval), Number(after.Interval), current.Cost, candidate.Cost, current.Load, candidate.Load);
            if (draft.ActiveCategory == ComponentCategory.Magazine && before != null && after != null)
                return Copy("comparison_magazine_effect", Number(current.Dps), Number(candidate.Dps), current.Cost, candidate.Cost, current.Load, candidate.Load);
            if (before != null && after != null)
                return Copy("comparison_weapon", Number(before.Damage), Number(after.Damage), Number(before.Range), Number(after.Range),
                    Number(before.Interval), Number(after.Interval), current.Dps.ToString("0.##", ui.TextResolver.Culture), candidate.Dps.ToString("0.##", ui.TextResolver.Culture),
                    current.Cost, candidate.Cost, current.Load, candidate.Load);
            return Copy("comparison_foundation", Number(current.Health), Number(candidate.Health), current.Capacity, candidate.Capacity,
                Number(current.Defense * 100), Number(candidate.Defense * 100), Number(current.FrontDefense * 100), Number(candidate.FrontDefense * 100), current.Cost, candidate.Cost, current.Load, candidate.Load);
        }

        public void Refresh()
        {
            if (Root == null) return;
            WorkshopUi.ClearViews(Root);
            if (!draft.IsComponentOpen) return;
            var compatible = draft.CompatibleParts;
            if (!hasCategory || previousCategory != draft.ActiveCategory)
            { inspectedId = EquippedId(); previousCategory = draft.ActiveCategory; hasCategory = true; revealInspected = true; }
            if (!compatible.Any(p => p.Id == inspectedId)) inspectedId = EquippedId() ?? compatible.FirstOrDefault()?.Id;
            var inspected = compatible.FirstOrDefault(p => p.Id == inspectedId);
            var candidate = inspected == null ? null : InspectedResolution(inspected);
            var activeWeapon = (candidate ?? draft.Resolved)?.Weapons.FirstOrDefault(item => item.SlotIndex == draft.ActiveMount);
            bool familyLayout = draft.ActiveComponent == WorkshopComponentKind.Weapon && activeWeapon != null &&
                (activeWeapon.Family == AssemblyWeaponFamily.Mortar || activeWeapon.Family == AssemblyWeaponFamily.Fan);
            bool comparingHeads = inspected != null && inspected.Category == ComponentCategory.Head && inspected.Id != EquippedId() && candidate != null && !familyLayout;
            bool comparisonLayout = comparingHeads && draft.IsOwned(inspected.Id);
            bool catalogueLayout = comparingHeads && !draft.IsOwned(inspected.Id);
            bool armorLayout = draft.ActiveComponent == WorkshopComponentKind.Foundation && draft.ActiveCategory == ComponentCategory.Armor;
            bool mortarLayout = familyLayout && activeWeapon.Family == AssemblyWeaponFamily.Mortar && draft.ActiveCategory == ComponentCategory.Head;
            float listWidth = mortarLayout ? width * .27f - 16 : familyLayout ? width * .35f - 28 : Mathf.Clamp(width * .22f, 218, 300), gap = 16;
            bool secondaryAction = inspected != null && (!string.IsNullOrEmpty(draft.SuggestedPartId) ||
                !draft.IsOwned(inspected.Id) && (run.Workshop.Level < inspected.RequiredLevel || run.Workshop.Gold < inspected.UnlockGold));
            float variantY = Mathf.Min(draft.ActiveCategory == ComponentCategory.Magazine ? 294 : secondaryAction ? 264 : 284,
                height - (secondaryAction ? 116 : 66) - 48);
            var listBounds = mortarLayout ? new Rect(0, 40, listWidth, height - 48)
                : familyLayout ? new Rect(width * .65f + 6, variantY, listWidth, height - (secondaryAction ? 116 : 66) - variantY)
                : armorLayout ? new Rect(0, height * .53f, width * .65f - 14, height * .47f - 8)
                : comparisonLayout ? new Rect(0, height - 128, width * .65f - 14, 122)
                : catalogueLayout ? new Rect(0, 40, width * .65f - 14, height - 40)
                : new Rect(0, 40, listWidth, height - 40);
            if (inspected != null) Details(inspected, familyLayout ? new Rect(0, 0, width, height)
                : comparisonLayout || armorLayout ? new Rect(0, 0, width, height)
                : catalogueLayout ? new Rect(width * .65f, 0, width * .35f, height)
                : new Rect(listWidth + gap, 0, width - listWidth - gap, height), familyLayout, candidate);
            if (!familyLayout && !comparisonLayout && !catalogueLayout && !armorLayout) ui.Label(Root, "Component catalogue heading", Copy("catalog"), new Rect(0, 0, listWidth, 34), 23, true);
            var toggle = ui.Button(Root, "Component show unavailable", mortarLayout ? new Rect(0, 0, listWidth, 34) : familyLayout ? new Rect(width * .65f + 6, 4, listWidth, 38) : new Rect(width - 330, 0, 330, 34),
                Copy("show_unavailable", Copy(showUnavailable ? "yes" : "no")), () => SetShowUnavailable(!showUnavailable));
            ui.SetSelected(toggle, showUnavailable);
            var visible = compatible.Where(p => showUnavailable || draft.IsOwned(p.Id)).ToArray();
            float rowHeight = mortarLayout || armorLayout ? listBounds.height - 6 : familyLayout ? Mathf.Clamp(listBounds.height, 48, 118) : comparisonLayout ? 118 : catalogueLayout ? listBounds.height - 6 : Mathf.Clamp((height - 46) / 2.6f, 150, 230);
            int columns = mortarLayout ? 2 : comparisonLayout || catalogueLayout || armorLayout ? 3 : familyLayout && listWidth >= 340 ? 2 : 1;
            float rowWidth = (listBounds.width - 10 - (columns - 1) * 10) / columns;
            const float rowGap = 10;
            var list = new CatalogScrollList(Root, "Component catalogue list", listBounds, visible.Length, columns, rowHeight, rowGap);
            for (int i = 0; i < visible.Length; i++)
            {
                var part = visible[i]; bool owned = draft.IsOwned(part.Id);
                bool compact = comparisonLayout || familyLayout && rowHeight < 96;
                bool mini = familyLayout && rowHeight < 72;
                var button = ui.Button(list.Content, "Component inspect " + part.Id, new Rect(i % columns * (rowWidth + rowGap), i / columns * (rowHeight + rowGap), rowWidth, rowHeight), "", () => InspectPart(part.Id));
                ui.SetSelected(button, part.Id == inspectedId);
                var name = ui.Label(button.transform, "Component part name", part.Name, compact ? new Rect(74, mini ? 0 : 4, rowWidth - 82, mini ? 20 : 34) : new Rect(12, 4, rowWidth - 24, familyLayout ? 24 : 36), mini ? 15 : familyLayout ? 18 : 21, true);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = familyLayout ? 12 : 16; name.resizeTextMaxSize = mini ? 15 : familyLayout ? 18 : 21;
                DrawPart(button.transform, part, "Component part illustration", compact ? new Rect(6, 12, 62, rowHeight - 24) :
                    new Rect(10, familyLayout ? 30 : 38, rowWidth - 20, familyLayout ? Mathf.Max(20, rowHeight - 66) : rowHeight - 94));
                var availability = ui.Label(button.transform, "Component part availability", owned ? Copy(part.Id == EquippedId() ? "equipped" : "owned") : Copy(compact ? "conditions_short" : "conditions", part.RequiredLevel, part.UnlockGold),
                    compact ? new Rect(74, rowHeight - (mini ? 24 : 32), rowWidth - 82, 22) : new Rect(12, rowHeight - (familyLayout ? 36 : 66), rowWidth - 24, familyLayout ? 26 : 30),
                    compact ? 12 : familyLayout ? 13 : 16, color: owned ? WorkshopUi.Blue : WorkshopUi.Warning);
                if (mortarLayout)
                {
                    WorkshopUi.Place(availability.rectTransform, new Rect(10, rowHeight - 54, rowWidth - 20, 44));
                    availability.resizeTextForBestFit = true; availability.resizeTextMinSize = 10; availability.resizeTextMaxSize = 13;
                }
                if (!familyLayout) ui.Label(button.transform, "Component part contribution", Contribution(part, rowWidth < 280), compact ? new Rect(74, rowHeight - 62, rowWidth - 82, 28) : new Rect(12, rowHeight - 28, rowWidth - 24, 24), compact ? 12 : 14, color: WorkshopUi.Muted);
            }
            if (visible.Length == 0) ui.Label(list.Content, "Component empty catalogue", Copy("empty"), new Rect(16, 18, listWidth - 42, 120), 21);
            if (revealInspected)
            {
                int index = Array.FindIndex(visible, part => part.Id == inspectedId);
                float overflow = list.Content.sizeDelta.y - listBounds.height;
                if (index >= 0 && overflow > 0)
                    list.Scroll.verticalNormalizedPosition = 1 - Mathf.Clamp(index / columns * (rowHeight + rowGap) + rowHeight - listBounds.height, 0, overflow) / overflow;
                revealInspected = false;
            }
            ApplySketchLayout(inspected, candidate, familyLayout, comparisonLayout, catalogueLayout);
        }

        void Details(ComponentDefinition part, Rect bounds, bool familyLayout, ResolvedEntry candidate)
        {
            var details = ui.Panel(Root, "Component details", bounds, Color.white);
            float w = bounds.width, h = bounds.height, artWidth = w * (familyLayout ? .65f : .52f);
            bool owned = draft.IsOwned(part.Id);
            bool suggested = !string.IsNullOrEmpty(draft.SuggestedPartId);
            bool canBuy = !owned && run.Workshop.Level >= part.RequiredLevel && run.Workshop.Gold >= part.UnlockGold;
            bool secondaryAction = suggested || (!owned && !canBuy);
            var resolved = draft.Resolved;
            bool comparing = part.Id != EquippedId() && resolved != null && candidate != null;
            var weapon = resolved?.Weapons.FirstOrDefault(item => item.SlotIndex == draft.ActiveMount);
            var visualWeapon = candidate?.Weapons.FirstOrDefault(item => item.SlotIndex == draft.ActiveMount) ?? weapon;
            bool mechanic = draft.ActiveComponent == WorkshopComponentKind.Weapon && visualWeapon != null && (visualWeapon.Family == AssemblyWeaponFamily.Mortar || visualWeapon.Family == AssemblyWeaponFamily.Fan);
            var diagnostics = draft.Diagnostics.Concat(draft.LastResult.Success ? Array.Empty<AssemblyDiagnostic>() : draft.LastResult.Diagnostics)
                .GroupBy(d => d.Code).Select(g => g.First()).ToArray();
            float diagnosticY = h - (familyLayout ? 70 : secondaryAction ? 164 : 114);
            bool hasDiagnostic = diagnostics.Length > 0 || draft.ReplacedPartIds.Count > 0;
            float artHeight = Mathf.Max(80, (hasDiagnostic ? diagnosticY - 10 : h - (familyLayout ? 14 : secondaryAction ? 116 : 66)) - 54);
            string inspectedName = part.Name;
            if (draft.PreviewRecipe.Structure != null)
            {
                var family = draft.Catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Structure && entry.Snapshot.Recipe.Structure.Family == draft.PreviewRecipe.Structure.Family);
                inspectedName = ui.Resolve(family.Name) + " · " + ui.Resolve(part.Name);
            }
            var inspectedTitle = ui.Label(details, "Component inspected name", inspectedName, new Rect(16, 4, (familyLayout ? artWidth : w) - 32, 42), 26, true);
            inspectedTitle.resizeTextForBestFit = true; inspectedTitle.resizeTextMinSize = 20; inspectedTitle.resizeTextMaxSize = 26;
            var previewRect = new Rect(12, 54, artWidth - 16, artHeight);
            if (comparing && part.Category == ComponentCategory.Head && !mechanic)
            {
                var currentPart = Definition(EquippedId());
                float half = (previewRect.width - 12) * .5f;
                DrawPart(details, currentPart, "Component current part illustration", new Rect(previewRect.x, previewRect.y, half, previewRect.height - 38));
                DrawPart(details, part, "Component candidate part illustration", new Rect(previewRect.x + half + 12, previewRect.y, half, previewRect.height - 38));
                var currentName = ui.Label(details, "Component current part caption", currentPart.Name, new Rect(previewRect.x, previewRect.yMax - 36, half, 36), 17, true);
                var candidateName = ui.Label(details, "Component candidate part caption", part.Name, new Rect(previewRect.x + half + 12, previewRect.yMax - 36, half, 36), 17, true);
                currentName.alignment = candidateName.alignment = TextAnchor.MiddleCenter;
                foreach (var caption in new[] { currentName, candidateName })
                { caption.resizeTextForBestFit = true; caption.resizeTextMinSize = 13; caption.resizeTextMaxSize = 17; }
            }
            else DrawPreview(details, previewRect, part, candidate ?? resolved);
            ui.Label(details, "Component inspected conditions", owned ? Copy(part.Id == EquippedId() ? "equipped" : "owned") : Copy("conditions", part.RequiredLevel, part.UnlockGold),
                new Rect(artWidth + 6, familyLayout ? 48 : 50, w - artWidth - 24, familyLayout ? 28 : 38), 17);
            ui.Label(details, "Component inspected contribution", Contribution(part), new Rect(artWidth + 6, familyLayout ? 78 : 88, w - artWidth - 24, familyLayout ? 28 : 42), 17, color: WorkshopUi.Muted);
            var multipliers = part.Modifiers.Where(m => m.Operation == ModifierOperation.Multiply && m.Value != 1 &&
                (m.Stat == AssemblyStat.Damage || m.Stat == AssemblyStat.Range || m.Stat == AssemblyStat.Interval || m.Stat == AssemblyStat.Radius ||
                m.Stat == AssemblyStat.ReloadSeconds || m.Stat == AssemblyStat.MagazineSize || m.Stat == AssemblyStat.Feed || m.Stat == AssemblyStat.Defense ||
                m.Stat == AssemblyStat.Health || m.Stat == AssemblyStat.Cost || m.Stat == AssemblyStat.Load || m.Stat == AssemblyStat.SupportRate || m.Stat == AssemblyStat.Slow))
                .Select(m => Copy("stat." + m.Stat) + " ×" + m.Value.ToString("0.##", ui.TextResolver.Culture));
            string modifierText = string.Join(" · ", multipliers);
            bool magazine = part.Category == ComponentCategory.Magazine && weapon != null;
            float informationY = familyLayout ? 110 : 136;
            if (magazine)
            {
                var next = candidate?.Weapons.FirstOrDefault(item => item.SlotIndex == draft.ActiveMount) ?? weapon;
                string Number(float value) => value.ToString("0.###", ui.TextResolver.Culture);
                string cycle = comparing ? Copy("magazine_comparison", weapon.MagazineSize, next.MagazineSize, Number(weapon.Interval), Number(next.Interval), Number(weapon.ReloadSeconds), Number(next.ReloadSeconds))
                    : Copy("magazine_stats", weapon.MagazineSize, weapon.Interval, weapon.ReloadSeconds);
                var cycleLabel = WorkshopMetricText.Create(ui,details, "Component magazine cycle", cycle, new Rect(artWidth + 6, familyLayout ? 110 : 130, w - artWidth - 24, 84));
                cycleLabel.alignment = TextAnchor.UpperLeft;
                informationY = familyLayout ? 188 : 210;
            }
            float modifierHeight = magazine || mechanic || comparing || resolved?.Support != null ? 0 : Mathf.Max(42, h - 266 - (secondaryAction ? 50 : 0));
            ui.Label(details, "Component inspected modifiers", modifierText, new Rect(artWidth + 6, 130, w - artWidth - 24, modifierHeight), 17, color: WorkshopUi.Muted);
            string message = string.Join("\n", diagnostics.OrderBy(d => d.Code == "assembly.part.required" || d.Code == "assembly.load.exceeded" || d.Code == "profile.write_failed" ? 0 : 1).Take(1).Select(DiagnosticText));
            if (draft.ReplacedPartIds.Count > 0) message += (message.Length > 0 ? "\n" : "") + Copy("replaced", string.Join(", ", draft.ReplacedPartIds.Select(id => Definition(id)?.Name ?? "")));
            var overloaded = diagnostics.FirstOrDefault(d => d.Code == "assembly.load.exceeded");
            if (overloaded != null)
            {
                string contributions = string.Join("\n", draft.PreviewRecipe.Parts.Select(p => Definition(p.Id)).Where(d => d != null && d.Get(AssemblyStat.Load, ModifierOperation.Add) != 0)
                    .Select(d => d.Name + " " + Signed(d.Get(AssemblyStat.Load, ModifierOperation.Add))));
                var symbol = details.Find("Component resolved preview"); if (symbol != null) WorkshopUi.RemoveView(symbol.gameObject);
                ui.Label(details, "Component load contributions", Copy("load_parts", contributions), new Rect(16, 50, artWidth - 28, artHeight), 15);
            }
            string summary = "";
            if (resolved?.Support != null)
            {
                var support = resolved.Support;
                switch (support.Kind)
                {
                    case AssemblyStructureFamily.Generator: summary = Copy("generator_stats", resolved.Health, resolved.Cost, support.IncomePerWave, support.PaybackWaves(resolved.Cost), support.MaximumInstances); break;
                    case AssemblyStructureFamily.Repair: summary = Copy("repair_stats", resolved.Health, resolved.Cost, support.RepairPerSecond, support.Radius); break;
                    case AssemblyStructureFamily.Slow: summary = Copy("slow_stats", resolved.Health, resolved.Cost, support.SlowSpeedMultiplier, support.Radius, support.SlowSeconds); break;
                    default: summary = Copy("wall_stats", resolved.Health, resolved.Cost, resolved.Load, resolved.Capacity); break;
                }
            }
            else if (mechanic)
            {
                summary = magazine ? (visualWeapon.Family == AssemblyWeaponFamily.Mortar
                    ? Copy("magazine_mortar_effect", visualWeapon.Range, visualWeapon.Radius, (candidate ?? resolved).Cost)
                    : Copy("magazine_fan_effect", visualWeapon.Range, visualWeapon.TargetLimit, (candidate ?? resolved).Cost)) : visualWeapon.Family == AssemblyWeaponFamily.Mortar
                    ? Copy("mechanic_mortar_stats", visualWeapon.Damage, visualWeapon.Range, visualWeapon.MinimumRange, visualWeapon.Radius, visualWeapon.Interval, (candidate ?? resolved).Cost)
                    : Copy("mechanic_fan_stats", visualWeapon.Damage, visualWeapon.Range, visualWeapon.Interval, visualWeapon.TargetLimit, visualWeapon.ConeDegrees, (candidate ?? resolved).Cost);
            }
            else if (resolved != null && draft.ActiveComponent == WorkshopComponentKind.Foundation)
            {
                var foundation = resolved.Snapshot.Recipe.Tower.Foundation;
                var frame = Definition(foundation.Frame.Id);
                summary = Copy("foundation_stats", resolved.Health, resolved.Capacity, frame.Get(AssemblyStat.MountCount, ModifierOperation.Set),
                    resolved.FeedCapacity.ToString("0.##", ui.TextResolver.Culture),
                    run.PreviewFoundationCost(resolved.Snapshot));
            }
            else if (resolved != null && weapon != null)
            {
                string Number(float value) => value.ToString("0.###",ui.TextResolver.Culture);
                summary = magazine ? Copy("magazine_weapon_effect", Number(weapon.Damage),Number(weapon.Range),Number(weapon.NominalDps),run.PreviewWeaponCost(resolved.Snapshot,draft.ActiveMount))
                    : Copy("weapon_stats", Number(weapon.Damage), Number(weapon.Range), weapon.MagazineSize,
                        Number(weapon.ReloadSeconds), resolved.Load, run.PreviewWeaponCost(resolved.Snapshot,draft.ActiveMount));
            }
            else if (draft.PreviewMetrics != null)
            {
                var metrics = draft.PreviewMetrics;
                summary = Copy("preview_stats", metrics.Cost, metrics.Load, metrics.Capacity, metrics.Health);
            }
            float statisticsY = informationY + modifierHeight;
            float statisticsBottom = familyLayout ? Mathf.Min(284, h - (secondaryAction ? 116 : 66) - 72) - 6 : h - (secondaryAction ? 116 : 66);
            var statistics = WorkshopMetricText.Create(ui, details, "Component resolved statistics", summary, new Rect(artWidth + 6, statisticsY, w - artWidth - 24, Mathf.Max(44, statisticsBottom - statisticsY)));
            statistics.alignment = TextAnchor.UpperLeft;
            if (comparing)
            {
                statistics.gameObject.SetActive(false);
                var comparison = WorkshopMetricText.Create(ui, details, "Component comparison", Comparison(resolved, candidate), new Rect(artWidth + 6, informationY, w - artWidth - 24,
                    Mathf.Max(magazine ? 44 : 110, statisticsBottom - informationY)));
                comparison.alignment = TextAnchor.UpperLeft;
            }
            ui.Label(details, "Component diagnostics", message, new Rect(16, diagnosticY, artWidth - 24, 54), 16, color: WorkshopUi.Warning);
            float actionX = familyLayout ? artWidth + 6 : 16, actionWidth = familyLayout ? w - artWidth - 24 : w - 32;
            if (suggested) ui.Button(details, "Component apply suggested part", new Rect(actionX, h - 104, actionWidth, 44), Copy("stock_magazine"), () => {
                draft.SelectPart(draft.SuggestedPartId); Refresh(); changed?.Invoke(); }, WorkshopIcon.Copy);
            var buy = ui.Button(details, "Component purchase part", new Rect(actionX, h - 54, actionWidth, 46), Copy("purchase", part.UnlockGold), () => OpenPurchase(part), WorkshopIcon.Cost, true);
            buy.interactable = canBuy;
            buy.gameObject.SetActive(!owned);
            string selectCaption = part.Category == ComponentCategory.Mechanism ? Copy("select_mechanism") : Copy("select");
            var select = ui.Button(details, "Component select part", new Rect(actionX, h - 54, actionWidth, 46), selectCaption, () => {
                if (draft.IsOwned(part.Id) && draft.SelectPart(part.Id)) { Refresh(); changed?.Invoke(); }
            }, WorkshopIcon.Check, true);
            select.interactable = owned; select.gameObject.SetActive(owned || !buy.interactable);
            if (!owned && !buy.interactable)
            {
                WorkshopUi.Place(buy.GetComponent<RectTransform>(), new Rect(actionX, h - 104, actionWidth, 44));
                // Both disabled actions remain readable when the player has not reached the unlock conditions.
            }
        }
        void DrawPart(Transform parent, ComponentDefinition part, string name, Rect rect)
        {
            var reference = new PartRef(part.Id, part.Revision);
            if (!AssemblyVisualComposer.HasVisualMapping(reference)) return;
            var composition = AssemblyVisualComposer.ComposePart(reference);
            if (composition.Fragments.Count == 0)
            {
                var neutral = ui.Label(parent, name, part.Name, rect, 20, true, WorkshopUi.Muted);
                neutral.alignment = TextAnchor.MiddleCenter; return;
            }
            var art = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, name, rect);
            art.SetPart(reference);
        }
        string DiagnosticText(AssemblyDiagnostic issue)
        {
            string text = ui.Resolve(UserText.Message(issue.Code));
            if (issue.Code == "profile.write_failed" && issue.Arguments.Count > 0)
                text += "\n" + ui.Resolve(issue.Arguments[0]);
            return text;
        }
        void DrawPreview(Transform parent, Rect rect, ComponentDefinition inspected, ResolvedEntry preview = null)
        {
            var resolved = preview ?? draft.Resolved;
            var selectedWeapon = resolved?.Weapons.FirstOrDefault(item => item.SlotIndex == draft.ActiveMount);
            bool mechanic = draft.ActiveComponent == WorkshopComponentKind.Weapon && selectedWeapon != null && (selectedWeapon.Family == AssemblyWeaponFamily.Mortar || selectedWeapon.Family == AssemblyWeaponFamily.Fan);
            if (draft.PreviewMetrics?.Overloaded != true && draft.Diagnostics.Count == 0)
            {
                var scenery = WorkshopUi.Graphic<UnityEngine.UI.Image>(parent, "Component preview scenery", rect);
                scenery.sprite = Resources.Load<Sprite>("Art/Player/FoundationPreviewScenery");
                scenery.preserveAspect = !mechanic; scenery.raycastTarget = false; scenery.color = new Color(1, 1, 1, mechanic ? .55f : .85f);
            }
            if (mechanic)
            {
                var diagram = WorkshopUi.Graphic<AssemblyMechanicPreview>(parent, "Component mechanic preview", rect);
                try { diagram.Configure(resolved.Snapshot, selectedWeapon); return; }
                catch (ArgumentException) { WorkshopUi.RemoveView(diagram.gameObject); }
                catch (InvalidOperationException) { WorkshopUi.RemoveView(diagram.gameObject); }
                catch (NotSupportedException) { WorkshopUi.RemoveView(diagram.gameObject); }
            }
            if (resolved != null && resolved.Kind != AlbumEntryKind.Legacy)
            {
                var art = WorkshopUi.Graphic<WeaponAssemblyGraphic>(parent, "Component resolved preview", rect);
                try
                {
                    var recipe = resolved.Snapshot.Recipe;
                    if (draft.ActiveComponent == WorkshopComponentKind.Foundation) art.SetComposition(AssemblyVisualComposer.ComposeFoundation(recipe.Tower.Foundation));
                    else if (draft.ActiveComponent == WorkshopComponentKind.Weapon) art.SetComposition(AssemblyVisualComposer.ComposeWeapon(recipe.Tower.Mounts.Single(mount => mount.SlotIndex == draft.ActiveMount).Weapon, AssemblyPreviewPose.ForWeapon()));
                    else art.SetRecipe(resolved.Snapshot);
                    return;
                }
                catch (ArgumentException) { WorkshopUi.RemoveView(art.gameObject); }
                catch (InvalidOperationException) { WorkshopUi.RemoveView(art.gameObject); }
                catch (NotSupportedException) { WorkshopUi.RemoveView(art.gameObject); }
            }
            DrawPart(parent, inspected, "Component inspected illustration", rect);
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
            float cw = Mathf.Min(620, w - 40), ch = Mathf.Min(650, h - 40);
            var card = ui.Panel(shade, "Component purchase sheet", new Rect((w - cw) * .5f, (h - ch) * .5f, cw, ch), WorkshopUi.Paper, true);
            ui.Label(card, "Component purchase title", Copy("purchase_title", part.Name), new Rect(24, 16, cw - 48, 74), 30, true);
            var reference = new PartRef(part.Id, part.Revision);
            var drawing = AssemblyVisualComposer.ComposePart(reference);
            float illustrationHeight = Mathf.Clamp(ch * .3f, 96, 190);
            bool slender = drawing.Bounds.height > drawing.Bounds.width * 3;
            float illustrationSize = illustrationHeight * (slender ? 1.5f : 1);
            var illustration = WorkshopUi.Graphic<WeaponAssemblyGraphic>(card, "Component purchase illustration", new Rect(0, 0, illustrationSize, illustrationSize));
            illustration.SetPart(reference);
            illustration.rectTransform.pivot = new Vector2(.5f, .5f);
            illustration.rectTransform.anchoredPosition = new Vector2(cw * .5f, -100 - illustrationHeight * .5f);
            if (slender) illustration.rectTransform.localRotation = Quaternion.Euler(0, 0, -65);
            ui.Label(card, "Component purchase conditions", Copy("purchase_message", part.RequiredLevel, run.Workshop.Level, part.UnlockGold, run.Workshop.Gold, run.Workshop.Gold - part.UnlockGold),
                new Rect(24, 112 + illustrationHeight, cw - 48, ch - illustrationHeight - 204), 22);
            ui.Button(card, "Component purchase cancel", new Rect(24, ch - 72, (cw - 60) * .5f, 48), Copy("cancel"), ClosePurchase, WorkshopIcon.Back);
            var confirm = ui.Button(card, "Component purchase confirm", new Rect(36 + (cw - 60) * .5f, ch - 72, (cw - 60) * .5f, 48), Copy("purchase", part.UnlockGold), () => {
                draft.PurchasePart(part.Id, Guid.NewGuid()); ClosePurchase(); revealInspected = true; Refresh(); changed?.Invoke();
            }, WorkshopIcon.Cost, true);
            confirm.interactable = !draft.IsOwned(part.Id) && run.Workshop.Level >= part.RequiredLevel && run.Workshop.Gold >= part.UnlockGold;
            WorkshopModal.Open(shade.gameObject, ClosePurchase);
        }
        public void ClosePurchase()
        { if (purchase != null) { WorkshopUi.RemoveView(purchase); purchase = null; } }
    }

    /// <summary>A read-only rule illustration: the actual resolved radius or cone, with the same recipe artwork.</summary>
    public sealed class AssemblyMechanicPreview : NotebookAtlasGraphic
    {
        readonly MeshPen diagram = new MeshPen(null, 2048), assembly = new MeshPen(null, 4096);
        AssemblyVisualComposition composition;
        public RecipeSnapshot Recipe { get; private set; }
        public ResolvedWeapon Weapon { get; private set; }
        public void Configure(RecipeSnapshot snapshot, ResolvedWeapon weapon)
        {
            var recipe=snapshot.Recipe.Tower.Mounts.Single(mount => mount.SlotIndex == weapon.SlotIndex).Weapon;
            composition=weapon.Family==AssemblyWeaponFamily.Fan ? AssemblyVisualComposer.ComposeWeapon(recipe)
                : AssemblyVisualComposer.ComposeWeapon(recipe,AssemblyPreviewPose.ForWeapon());
            Recipe=snapshot; Weapon=weapon; SetVerticesDirty(); SetMaterialDirty();
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper target)
        {
            target.Clear(); if (composition == null || Weapon == null) return;
            var rect = rectTransform.rect; diagram.Clear();
            var blue = WorkshopUi.Blue;
            var artBounds=AssemblyPreviewPose.VisibleBounds(composition);
            float scale=Mathf.Min(rect.width*.58f/Mathf.Max(.001f,artBounds.width),rect.height*.34f/Mathf.Max(.001f,artBounds.height));
            Vector2? muzzleDestination=null;
            Vector2 artCenter;
            if (Weapon.Family == AssemblyWeaponFamily.Fan)
            {
                var origin = new Vector2(rect.center.x, rect.yMin + rect.height * .35f);
                float radius = Mathf.Min(rect.width * .48f, rect.height * .63f);
                float angle = Mathf.Clamp(Weapon.ConeDegrees, 0, 180) * Mathf.Deg2Rad;
                Vector2 Point(float fraction) => origin + new Vector2(Mathf.Cos(Mathf.PI * .5f - angle * .5f + angle * fraction), Mathf.Sin(Mathf.PI * .5f - angle * .5f + angle * fraction)) * radius;
                var previous = Point(0);
                for (int i = 1; i <= 32; i++)
                {
                    var next = Point(i / 32f);
                    diagram.Triangle(origin, previous, next, MeshPen.Alpha(blue, .20f));
                    if (i % 2 == 0) diagram.Line(origin, next, 1, MeshPen.Alpha(blue, .16f));
                    if (i % 3 != 0) diagram.Line(previous, next, 1.5f, MeshPen.Alpha(blue, .8f));
                    previous = next;
                }
                diagram.Line(origin, Point(0), 1.5f, blue); diagram.Line(origin, Point(1), 1.5f, blue);
                int targets = Mathf.Min(8, Weapon.TargetLimit);
                for (int i = 0; i < targets; i++)
                {
                    int row = i < 3 ? 0 : i < 6 ? 1 : 2;
                    int index = i < 3 ? i : i < 6 ? i - 3 : i - 6;
                    float fraction = row == 2 ? .36f + index * .28f : .2f + index * .3f;
                    var point = Vector2.Lerp(origin, Point(fraction), .42f + row * .20f);
                    Target(diagram, point, Mathf.Clamp(radius * .06f, 12, 16));
                }
                artCenter = new Vector2(rect.center.x, rect.yMin + rect.height * .17f);
                muzzleDestination=origin;
            }
            else
            {
                var point = new Vector2(rect.center.x + rect.width * .12f, rect.yMin + rect.height * .62f);
                // Radius is shown in one labelled world scale; wider heads visibly expand the footprint.
                float unit = Mathf.Min(rect.width / 12, rect.height / 10);
                float radius = Weapon.Radius * unit;
                diagram.Disc(point, radius, MeshPen.Alpha(blue, .18f), 48);
                var diagonal=new Vector2(.7071068f,.7071068f); var across=new Vector2(-diagonal.y,diagonal.x);
                for(float at=-radius+4;at<radius;at+=8)
                {
                    float half=Mathf.Sqrt(Mathf.Max(0,radius*radius-at*at)); var middle=point+across*at;
                    diagram.Line(middle-diagonal*half,middle+diagonal*half,1,MeshPen.Alpha(blue,.25f));
                }
                diagram.Ring(point, radius, 1.5f, blue, 48, dashed: true);
                for (int i = 0; i < 3; i++) Target(diagram, point + new Vector2(radius * (.8f + i * .1f), radius * (.95f - i * .95f)), 16);
                artCenter = new Vector2(rect.center.x - rect.width * .22f, rect.yMin + rect.height * .18f);
                var start = composition.Anchors.Count>0 ? artCenter+(composition.Anchors[0].Muzzle-artBounds.center)*scale
                    : artCenter+new Vector2(10,rect.height*.10f);
                Vector2 previous = start;
                for (int i = 1; i <= 32; i++)
                {
                    float t = i / 32f; var next = Vector2.Lerp(start, point, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * rect.height * .20f;
                    if (i % 3 != 0) diagram.Line(previous, next, 1.4f, blue);
                    previous = next;
                }
                diagram.Disc(point, 3, blue);
            }
            diagram.CopyTo(target, Vector2.zero, 1);
            assembly.Clear(); composition.Draw(assembly, Vector2.zero);
            var artOrigin=muzzleDestination.HasValue && composition.Anchors.Count>0
                ? muzzleDestination.Value-composition.Anchors[0].Muzzle*scale : artCenter-artBounds.center*scale;
            assembly.CopyTo(target,artOrigin,scale);
        }
        static void Target(MeshPen pen, Vector2 center, float radius)
        {
            var ink = WorkshopUi.Blue;
            var shoulderLeft = center + new Vector2(-radius, radius * .8f);
            var shoulderRight = center + new Vector2(radius, radius * .8f);
            var lowerLeft = center + new Vector2(-radius * .8f, -radius * .8f);
            var lowerRight = center + new Vector2(radius * .8f, -radius * .8f);
            var point = center + Vector2.down * radius * 1.35f;
            pen.Triangle(shoulderLeft, shoulderRight, point, MeshPen.Alpha(ink, .11f));
            pen.Line(shoulderLeft, shoulderRight, 1.5f, ink); pen.Line(shoulderLeft, lowerLeft, 1.5f, ink);
            pen.Line(shoulderRight, lowerRight, 1.5f, ink); pen.Line(lowerLeft, point, 1.5f, ink); pen.Line(lowerRight, point, 1.5f, ink);
            var head = center + Vector2.up * radius * 1.45f;
            pen.Ring(head, radius * .48f, 1.4f, ink, 12);
            pen.Line(head + new Vector2(-radius * .32f, 0), head + new Vector2(radius * .32f, 0), 1.4f, ink);
            for (int i = -1; i <= 1; i++) pen.Line(head + new Vector2(i * radius * .22f, radius * .35f), head + new Vector2(i * radius * .3f, radius * .8f), 1.2f, ink);
            pen.Line(center + Vector2.left * radius, center + Vector2.left * radius * 1.55f, 1.5f, ink);
            pen.Line(center + Vector2.right * radius, center + Vector2.right * radius * 1.55f, 1.5f, ink);
            pen.Ring(center, radius * .45f, 1.2f, ink, 12); pen.Disc(center, radius * .18f, NotebookPalette.Red, 8);
            pen.Line(point, center + Vector2.down * radius * 2.35f, 1.5f, ink);
            pen.Line(center + new Vector2(-radius * .45f, -radius * 2.35f), center + new Vector2(radius * .45f, -radius * 2.35f), 1.3f, ink);
        }
    }
}
