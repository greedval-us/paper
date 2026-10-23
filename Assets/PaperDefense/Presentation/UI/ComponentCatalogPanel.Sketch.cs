using System;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class ComponentCatalogPanel
    {
        bool UsesWeaponDrawing(ComponentDefinition inspected, bool family) => inspected != null && !family &&
            draft.ActiveComponent == WorkshopComponentKind.Weapon && inspected.Category == ComponentCategory.Mechanism &&
            draft.IsOwned(inspected.Id) && inspected.Id == EquippedId() &&
            draft.Resolved != null && draft.Diagnostics.Count == 0 && draft.LastResult.Diagnostics.Count == 0 &&
            draft.ReplacedPartIds.Count == 0 && string.IsNullOrEmpty(draft.SuggestedPartId);

        void ApplySketchLayout(ComponentDefinition inspected, ResolvedEntry candidate, bool family, bool comparison, bool catalogue)
        {
            if (inspected == null) return;
            var details = Root.Find("Component details") as RectTransform;
            if (details == null) return;
            float w = details.rect.width, h = details.rect.height;
            bool commandFailure = draft.LastResult.Success == false && draft.LastResult.Diagnostics.Count > 0;
            var shownWeapon = (candidate ?? draft.Resolved)?.Weapons.FirstOrDefault(value => value.SlotIndex == draft.ActiveMount);
            if (draft.Diagnostics.Any(issue => issue.Code == "assembly.part.required"))
            { LayoutInvalidParts(details, inspected, w, h); return; }
            if (!commandFailure && family && shownWeapon?.Family == AssemblyWeaponFamily.Mortar && inspected.Category == ComponentCategory.Head)
            { LayoutMortar(details, w, h); return; }
            if (!commandFailure && family && shownWeapon?.Family == AssemblyWeaponFamily.Fan && inspected.Category == ComponentCategory.Mechanism)
            { LayoutFan(details, shownWeapon, w, h); return; }
            if (!commandFailure && draft.ActiveComponent == WorkshopComponentKind.Foundation && inspected.Category == ComponentCategory.Armor && draft.Resolved != null)
            { LayoutArmor(details, candidate, w, h); return; }
            if (!commandFailure && draft.ActiveComponent == WorkshopComponentKind.Structure && draft.Resolved != null)
            { LayoutStructure(details, inspected, candidate, w, h); return; }
            if (!commandFailure && draft.ActiveComponent == WorkshopComponentKind.Foundation && draft.Resolved != null && draft.Diagnostics.Count == 0)
            { LayoutFoundationParts(details, candidate, w, h); return; }
            Transform Find(string name) => details.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == name);
            void Move(string name, Rect bounds, int size = 0)
            {
                var item = Find(name) as RectTransform; if (item == null) return;
                WorkshopUi.Place(item, bounds);
                var text = item.GetComponent<Text>();
                if (text != null && size > 0)
                { text.fontSize = text is WorkshopMetricText ? 20 : size; text.lineSpacing = text is WorkshopMetricText ? 1 : 1.12f; }
            }
            void Hide(string name) { var item = Find(name); if (item != null) item.gameObject.SetActive(false); }
            void Art(string name, Rect bounds, bool tilt = false)
            {
                Move(name, bounds);
                var item = Find(name) as RectTransform;
                if (item != null && tilt)
                {
                    item.pivot = new Vector2(.5f,.5f);
                    item.anchoredPosition += new Vector2(bounds.width*.5f,-bounds.height*.5f);
                    item.localEulerAngles = new Vector3(0,0,-38);
                }
            }
            float right = comparison || family ? w * .65f : w * .60f;
            if (draft.ActiveComponent == WorkshopComponentKind.Foundation && inspected.Category == ComponentCategory.Armor)
            {
                right = w*.65f;
                Move("Component inspected name",new Rect(14,4,right-28,34),24);
                var foundation = (candidate ?? draft.Resolved)?.Snapshot.Recipe.Tower.Foundation;
                var stage = new Rect(36,40,right-72,h*.48f-38);
                Move("Component resolved preview",stage); Move("Component preview scenery",stage);
                Move("Component inspected conditions",new Rect(right+12,40,w-right-24,28),17);
                Move("Component inspected contribution",new Rect(right+12,72,w-right-24,32),16);
                Hide("Component inspected modifiers");
                ui.Label(details,"Component protection heading",Copy("protection_heading"),new Rect(right+12,108,w-right-24,28),22,true);
                float actionTop=h-54;
                foreach (string action in new[] { "Component select part", "Component purchase part", "Component apply suggested part" })
                {
                    var item=Find(action) as RectTransform;
                    if(item != null && item.gameObject.activeSelf) actionTop=Mathf.Min(actionTop,-item.anchoredPosition.y);
                }
                bool compactProtection=actionTop<h-60;
                if (compactProtection) Hide("Component protection heading");
                var directionRect = new Rect(right+38,compactProtection?110:140,w-right-76,compactProtection?36:64);
                WorkshopUi.Graphic<WorkshopProtectionDirections>(details,"Component protection directions",directionRect);
                if (foundation != null)
                {
                    var preview = WorkshopUi.Graphic<WeaponAssemblyGraphic>(details,"Component protection foundation",new Rect(right+70,compactProtection?116:150,w-right-140,compactProtection?24:44));
                    preview.SetComposition(AssemblyVisualComposer.ComposeFoundation(foundation));
                }
                float statsY = Mathf.Min(compactProtection?158:208,actionTop-8-6*WorkshopMetricText.Baseline);
                Move("Component resolved statistics",new Rect(right+40,statsY,w-right-54,actionTop-statsY-8),17);
                Move("Component comparison",new Rect(right+40,statsY,w-right-54,actionTop-statsY-8),17);
                var statistics = Find("Component comparison")?.GetComponent<Text>() ?? Find("Component resolved statistics")?.GetComponent<Text>();
                FrameRows(details,statistics,right+8,statsY-2,w-right-16,23);
                foreach (string action in new[] { "Component select part", "Component purchase part", "Component apply suggested part" })
                {
                    var item = Find(action) as RectTransform; if (item != null) Move(action,new Rect(right+12,item.anchoredPosition.y*-1,w-right-24,item.sizeDelta.y));
                }
                return;
            }
            if (catalogue)
            {
                LayoutLockedHeadCatalog(details, w, h);
                return;
            }
            if (comparison && draft.Diagnostics.Count == 0 && draft.LastResult.Diagnostics.Count == 0 &&
                draft.ReplacedPartIds.Count == 0 && string.IsNullOrEmpty(draft.SuggestedPartId))
            {
                LayoutOwnedHeadComparison(details, inspected, candidate, w, h);
                return;
            }
            if (comparison)
            {
                float half = (right-36)*.5f, artHeight = Mathf.Max(80,h-196);
                Move("Component inspected name",new Rect(right+16,42,w-right-32,34),24);
                ui.Label(details,"Component comparison page heading",Copy("comparison_heading"),new Rect(14,4,right-28,34),24,true);
                Art("Component current part illustration",new Rect(24,58,half-24,artHeight),true);
                Art("Component candidate part illustration",new Rect(half+24,58,half-24,artHeight),true);
                Move("Component current part caption",new Rect(14,h-169,half,34),18);
                Move("Component candidate part caption",new Rect(half+18,h-169,half-4,34),18);
                Move("Component inspected conditions",new Rect(right+16,80,w-right-32,26),17);
                Move("Component inspected contribution",new Rect(right+16,108,w-right-32,38),16);
                Move("Component comparison",new Rect(right+48,154,w-right-64,Mathf.Max(110,h-222)),18);
                foreach (string action in new[] { "Component select part", "Component purchase part", "Component apply suggested part" })
                {
                    var item = Find(action) as RectTransform; if (item != null) Move(action,new Rect(right+12,item.anchoredPosition.y*-1,w-right-24,item.sizeDelta.y));
                }
                var toggle = Root.Find("Component show unavailable") as RectTransform;
                WorkshopUi.Place(toggle,new Rect(width-330,0,330,34));
                FrameRows(details,Find("Component comparison")?.GetComponent<Text>(),right+12,152,w-right-24,27);
                return;
            }
            if (UsesWeaponDrawing(inspected, family))
            {
                LayoutWeaponDrawing(details, inspected, candidate, w, h);
                return;
            }
            if (!family)
            {
                Move("Component inspected name",new Rect(14,4,right-20,34),24);
                Move("Component inspected conditions",new Rect(right+12,46,w-right-24,30),18);
                Move("Component inspected contribution",new Rect(right+12,80,w-right-24,42),16);
                Move("Component inspected modifiers",new Rect(right+12,124,w-right-24,28),16);
                if(inspected.Category==ComponentCategory.Magazine) Hide("Component inspected modifiers");
                var stats = Find("Component resolved statistics")?.GetComponent<Text>();
                var comparisonText = Find("Component comparison")?.GetComponent<Text>();
                float statsY = inspected.Category == ComponentCategory.Magazine ? 224 : 156;
                Move("Component resolved statistics",new Rect(right+42,statsY,w-right-56,Mathf.Max(90,h-statsY-64)),18);
                Move("Component comparison",new Rect(right+42,statsY,w-right-56,Mathf.Max(110,h-statsY-64)),18);
                Move("Component magazine cycle",new Rect(right+12,132,w-right-24,82),18);
                (Find("Component magazine cycle")?.GetComponent<Text>() as WorkshopMetricText)?.FitRows();
                var snapshot = (candidate ?? draft.Resolved)?.Snapshot;
                bool diagram = snapshot != null && inspected.Category != ComponentCategory.Magazine && inspected.Category != ComponentCategory.Armor && draft.Diagnostics.Count == 0;
                var stage = new Rect(8,44,right-18,h-112);
                if (diagram)
                {
                    Hide("Component resolved preview"); Hide("Component inspected illustration");
                    var exploded = WorkshopUi.Graphic<WorkshopAssemblyDiagram>(details,"Component assembly diagram",stage);
                    exploded.Configure(snapshot.Recipe,draft.ActiveComponent,draft.ActiveMount);
                    var caption = ui.Label(details,"Component diagram caption",Copy("diagram"),new Rect(10,h-88,right-20,24),16,true,WorkshopUi.Muted);
                    caption.alignment = TextAnchor.MiddleCenter;
                }
                else Move("Component resolved preview",stage);
                Move("Component preview scenery",stage);
                foreach (string action in new[] { "Component select part", "Component purchase part", "Component apply suggested part" })
                {
                    var item = Find(action) as RectTransform; if (item != null) Move(action,new Rect(right+12,item.anchoredPosition.y*-1,w-right-24,item.sizeDelta.y));
                }
                var visible = comparisonText != null && comparisonText.gameObject.activeSelf ? comparisonText : stats;
                if (visible == stats && draft.Resolved?.Support?.Kind == AssemblyStructureFamily.Generator && visible is WorkshopMetricText generator)
                {
                    var rows=generator.text.TrimEnd('\r','\n').Split('\n');
                    generator.text=string.Join("\n",rows.Take(4));
                    WorkshopUi.Place(generator.rectTransform,new Rect(right+42,statsY,w-right-56,108));
                    ui.Label(details,"Component placement rules heading",Copy("placement_rules"),new Rect(right+12,statsY+112,w-right-24,28),21,true);
                    var rules=WorkshopMetricText.Create(ui,details,"Component placement rules",string.Join("\n",rows.Skip(4)),new Rect(right+42,statsY+144,w-right-56,54));
                    FrameRows(details,rules,right+10,statsY+141,w-right-20,27);
                }
                if (visible != null) FrameRows(details,visible,right+10,statsY-3,w-right-20,26);
            }
            else
            {
                var stats = Find("Component comparison")?.GetComponent<Text>() ?? Find("Component resolved statistics")?.GetComponent<Text>();
                float actionTop=h-54;
                foreach (string action in new[] { "Component select part", "Component purchase part", "Component apply suggested part" })
                {
                    var item=Find(action) as RectTransform;
                    if(item != null && item.gameObject.activeSelf) actionTop=Mathf.Min(actionTop,-item.anchoredPosition.y);
                }
                bool secondaryAction=actionTop<h-60;
                float statsY=inspected.Category == ComponentCategory.Magazine?(secondaryAction?184:202):(secondaryAction?100:110);
                if(secondaryAction)
                {
                    Move("Component inspected conditions",new Rect(right+6,42,w-right-24,28),17);
                    Move("Component inspected contribution",new Rect(right+6,70,w-right-24,28),16);
                }
                if(inspected.Category == ComponentCategory.Magazine)
                {
                    Move("Component magazine cycle",new Rect(right+6,secondaryAction?100:110,w-right-24,84),20);
                    (Find("Component magazine cycle")?.GetComponent<Text>() as WorkshopMetricText)?.FitRows();
                }
                if(stats != null) Move(stats.name,new Rect(right+36,statsY,w-right-52,inspected.Category == ComponentCategory.Magazine?84:164),20);
                if (stats != null && stats.gameObject.activeSelf) FrameRows(details,stats,right+4,statsY,w-right-16,27);
                var weapon = (candidate ?? draft.Resolved)?.Weapons.FirstOrDefault(value => value.SlotIndex == draft.ActiveMount);
                if (weapon?.Family == AssemblyWeaponFamily.Mortar && inspected.Category == ComponentCategory.Head)
                {
                    var field = new Rect(w*.28f,44,w*.36f,h-58);
                    Move("Component mechanic preview",field); Move("Component preview scenery",field);
                    Move("Component inspected name",new Rect(w*.28f,4,w*.36f,36),22);
                }
                if (weapon?.Family == AssemblyWeaponFamily.Fan)
                {
                    var angle = ui.Label(details,"Component sector angle",weapon.ConeDegrees.ToString("0.#",ui.TextResolver.Culture)+"°",new Rect(w*.22f,38,w*.22f,34),26,true);
                    angle.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        Transform Detail(Transform details, string name) => details.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
        void MoveDetail(Transform details, string name, Rect bounds)
        { var item = Detail(details, name) as RectTransform; if (item != null) WorkshopUi.Place(item, bounds); }
        void HideDetail(Transform details, string name)
        { var item = Detail(details, name); if (item != null) item.gameObject.SetActive(false); }
        RectTransform LocalFrame(Transform parent, string name, Rect bounds, Color? outline = null)
        {
            var frame = ui.Panel(parent, name, bounds, Color.white);
            if (outline.HasValue) frame.GetComponent<WorkshopSurfaceGraphic>().Outline(1.5f, outline.Value);
            frame.SetAsFirstSibling(); return frame;
        }
        Text VisibleStatistics(Transform details) => new[] { "Component comparison", "Component resolved statistics" }
            .Select(name => Detail(details, name)?.GetComponent<Text>()).FirstOrDefault(text => text != null && text.gameObject.activeSelf);
        float ActionTop(Transform details, float h) => h - 8 - new[] { "Component apply suggested part", "Component purchase part", "Component select part" }
            .Count(name => Detail(details, name)?.gameObject.activeSelf == true) * 50;
        void StackActions(Transform details, float x, float y, float w)
        {
            foreach (string name in new[] { "Component apply suggested part", "Component purchase part", "Component select part" })
            {
                var action = Detail(details, name) as RectTransform;
                if (action == null || !action.gameObject.activeSelf) continue;
                WorkshopUi.Place(action, new Rect(x, y, w, 46)); y += 50;
            }
        }
        void MetricRegion(Transform parent, Text text, Rect bounds, float pitch = 32, int size = 21)
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text)) return;
            int rows = text.text.TrimEnd('\r', '\n').Split('\n').Length;
            WorkshopUi.Place(text.rectTransform, new Rect(bounds.x + 34, bounds.y + 2, bounds.width - 40, rows * pitch));
            FitPitchedMetrics(text, size, pitch);
            var rules = WorkshopUi.Graphic<WorkshopMetricRules>(parent, "Component framed rules " + text.name, new Rect(bounds.x, bounds.y, bounds.width, rows * pitch)); rules.Rows = rows;
            for (int i = 0; i < rows; i++)
            {
                var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, "Component framed icon " + text.name + i, new Rect(bounds.x + 4, bounds.y + i * pitch + (pitch - 22) * .5f, 22, 22));
                string row = text.text.Split('\n')[i];
                icon.Icon = row.Contains("Урон") || row.Contains("DPS") ? WorkshopIcon.Damage : row.Contains("Дальн") || row.Contains("Радиус") ? WorkshopIcon.Target
                    : row.Contains("Цен") || row.Contains("Доход") ? WorkshopIcon.Cost : row.Contains("Прочн") || row.Contains("защит") ? WorkshopIcon.Shield : WorkshopIcon.Settings;
                icon.color = WorkshopUi.Blue;
            }
        }
        void TiltPart(RectTransform rect)
        {
            var area = rect.rect; float side = Mathf.Min(area.width * 1.15f, area.height * .92f);
            var center = rect.anchoredPosition + new Vector2(area.width * .5f, -area.height * .5f);
            rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(side, side);
            rect.anchoredPosition = center; rect.localEulerAngles = new Vector3(0, 0, -38);
        }
        void LayoutLockedHeadCatalog(RectTransform details, float w, float h)
        {
            float inset = width * .12f;
            LocalFrame(Root, "Component inset parts sheet", new Rect(inset, 0, width - inset, height));
            var context = WorkshopUi.Graphic<WorkshopAssemblyDiagram>(Root, "Component workshop recipe context", new Rect(0, 40, Mathf.Max(60, inset - 12), height - 80));
            context.Configure(draft.PreviewRecipe, draft.ActiveComponent, draft.ActiveMount);
            context.transform.SetAsFirstSibling(); context.Opacity = .35f;
            ui.Label(Root, "Component inset catalogue heading", Copy("catalog"), new Rect(inset + 14, 3, width * .5f, 34), 25, true);
            MoveDetail(details, "Component inspected name", new Rect(12, 2, w - 24, 36));
            HideDetail(details, "Component current part illustration"); HideDetail(details, "Component current part caption"); HideDetail(details, "Component candidate part caption");
            HideDetail(details, "Component inspected contribution"); HideDetail(details, "Component inspected modifiers");
            float actionY = ActionTop(details, h), statsY = Mathf.Min(172, actionY - 206);
            var art = Detail(details, "Component candidate part illustration") as RectTransform;
            if (art != null)
            {
                const float artTop = 42, tableGap = 8;
                float availableHeight = statsY - tableGap - artTop;
                // The full rotated square must fit above the first row. The catalogue
                // already owns the main part art, so omit this subordinate view if short.
                if (availableHeight < 64) art.gameObject.SetActive(false);
                else
                {
                    float radians = 38 * Mathf.Deg2Rad;
                    float rotatedExtent = Mathf.Cos(radians) + Mathf.Sin(radians);
                    float side = Mathf.Min(w * .64f, availableHeight) / rotatedExtent;
                    WorkshopUi.Place(art, new Rect((w - side) * .5f, artTop + (availableHeight - side) * .5f, side, side));
                    art.pivot = new Vector2(.5f, .5f);
                    art.anchoredPosition += new Vector2(side * .5f, -side * .5f);
                    art.localEulerAngles = new Vector3(0, 0, -38);
                }
            }
            if (!draft.LastResult.Success && draft.LastResult.Diagnostics.Count > 0)
            {
                if (art != null) art.gameObject.SetActive(false);
                MoveDetail(details, "Component diagnostics", new Rect(12, 42, w - 24, Mathf.Max(54, statsY - 48)));
            }
            MetricRegion(details, VisibleStatistics(details), new Rect(12, statsY, w - 24, 162), 27, 20);
            MoveDetail(details, "Component inspected conditions", new Rect(12, statsY + 166, w - 24, 28));
            StackActions(details, 12, actionY, w - 24);
            WorkshopUi.Place(Root.Find("Component show unavailable") as RectTransform, new Rect(inset + 14, 42, width * .65f - inset - 28, 30));
            foreach (var illustration in Root.GetComponentsInChildren<WeaponAssemblyGraphic>().Where(value => value.name == "Component part illustration")) TiltPart(illustration.rectTransform);
        }
        void LayoutMortar(RectTransform details, float w, float h)
        {
            details.GetComponent<WorkshopSurfaceGraphic>().color = Color.clear;
            float fieldX = w * .32f, right = w * .68f;
            LocalFrame(details, "Component mortar choices frame", new Rect(0, 0, fieldX - 10, h));
            LocalFrame(details, "Component mortar field frame", new Rect(fieldX, 0, right - fieldX - 12, h));
            LocalFrame(details, "Component mortar statistics frame", new Rect(right, 0, w - right, h));
            ui.Label(details, "Component mortar choices heading", Copy("catalog"), new Rect(12, 0, fieldX - 32, 34), 23, true);
            MoveDetail(details, "Component inspected name", new Rect(fieldX + 12, 0, right - fieldX - 36, 42));
            var field = new Rect(fieldX + 12, 52, right - fieldX - 36, h - 104);
            MoveDetail(details, "Component mechanic preview", field); MoveDetail(details, "Component preview scenery", field);
            var scenery = Detail(details, "Component preview scenery")?.GetComponent<Image>(); if (scenery != null) scenery.color = new Color(1, 1, 1, .22f);
            ui.Label(details, "Component mortar rule caption", "Схема зоны попадания", new Rect(fieldX + 14, h - 45, right - fieldX - 40, 32), 18, true, WorkshopUi.Muted);
            ui.Label(details, "Component mortar statistics heading", Copy("characteristics"), new Rect(right + 12, 2, w - right - 24, 34), 24, true);
            MoveDetail(details, "Component inspected conditions", new Rect(right + 12, 43, w - right - 24, 26));
            MoveDetail(details, "Component inspected contribution", new Rect(right + 12, 72, w - right - 24, 30));
            HideDetail(details, "Component inspected modifiers");
            var stats = VisibleStatistics(details); int rows = stats?.text.TrimEnd().Split('\n').Length ?? 6;
            float actionY = ActionTop(details, h), pitch = Mathf.Clamp((actionY - 122) / rows, 25, 44);
            MetricRegion(details, stats, new Rect(right + 10, 112, w - right - 20, rows * pitch), pitch, 21);
            StackActions(details, right + 10, actionY, w - right - 20);
            WorkshopUi.Place(Root.Find("Component show unavailable") as RectTransform, new Rect(12, 38, fieldX - 34, 30));
        }
        void LayoutFan(RectTransform details, ResolvedWeapon weapon, float w, float h)
        {
            details.GetComponent<WorkshopSurfaceGraphic>().color = Color.clear;
            float right = w * .65f;
            LocalFrame(details, "Component fan field frame", new Rect(0, 0, right - 12, h));
            LocalFrame(details, "Component fan statistics frame", new Rect(right, 0, w - right, Mathf.Min(282, h - 152)));
            MoveDetail(details, "Component inspected name", new Rect(14, 2, right - 42, 38));
            MoveDetail(details, "Component mechanic preview", new Rect(14, 46, right - 40, h - 98));
            MoveDetail(details, "Component preview scenery", new Rect(18, 52, right - 48, h - 104));
            var scenery = Detail(details, "Component preview scenery")?.GetComponent<Image>(); if (scenery != null) scenery.color = new Color(1, 1, 1, .16f);
            var angle = ui.Label(details, "Component sector angle", weapon.ConeDegrees.ToString("0.#", ui.TextResolver.Culture) + "°", new Rect(right * .42f, 40, right * .16f, 36), 27, true); angle.alignment = TextAnchor.MiddleCenter;
            MoveDetail(details, "Component inspected conditions", new Rect(right + 12, 42, w - right - 24, 26));
            MoveDetail(details, "Component inspected contribution", new Rect(right + 12, 72, w - right - 24, 28));
            HideDetail(details, "Component inspected modifiers");
            MetricRegion(details, VisibleStatistics(details), new Rect(right + 8, 110, w - right - 16, 162), 27, 21);
            ui.Label(details, "Component fan rule caption", "Схема сектора", new Rect(16, h - 42, 180, 30), 18, true, WorkshopUi.Muted);
            ui.Button(details, "Component fan sight navigation", new Rect(right - 216, h - 42, 196, 32), ui.Resolve(UserText.Message("assembly.component.category.Sight")), () => { draft.SelectCategory(ComponentCategory.Sight); Refresh(); }, WorkshopIcon.Target);
        }
        void LayoutArmor(RectTransform details, ResolvedEntry candidate, float w, float h)
        {
            details.GetComponent<WorkshopSurfaceGraphic>().color = Color.clear;
            float right = w * .65f;
            LocalFrame(details, "Component armor foundation frame", new Rect(0, 0, right - 12, h * .52f));
            LocalFrame(details, "Component armor rule frame", new Rect(right, 0, w - right, h));
            MoveDetail(details, "Component inspected name", new Rect(14, 0, right - 30, 38));
            const float titleY = 40, titleHeight = 34, titleArtGap = 10;
            float previewY = titleY + titleHeight + titleArtGap;
            float previewHeightAvailable = Mathf.Max(0, h * .52f - 12 - previewY);
            // Fit the complete real foundation below the title's owned band. The former
            // y44 preview started inside that band and let its left collar cross the text.
            MoveDetail(details, "Component resolved preview", new Rect(24, previewY, right - 64, previewHeightAvailable));
            MoveDetail(details, "Component preview scenery", new Rect(18, previewY, right - 48, previewHeightAvailable));
            var scenery = Detail(details, "Component preview scenery")?.GetComponent<Image>(); if (scenery != null) scenery.color = new Color(1, 1, 1, .15f);
            MoveDetail(details, "Component inspected conditions", new Rect(right + 12, 42, w - right - 24, 26));
            MoveDetail(details, "Component inspected contribution", new Rect(right + 12, 72, w - right - 24, 30)); HideDetail(details, "Component inspected modifiers");
            ui.Label(details, "Component armor rule heading", "Схема защиты", new Rect(right + 12, 2, w - right - 24, 34), 25, true);
            float actionY = ActionTop(details, h); var stats = VisibleStatistics(details); int rows = stats?.text.TrimEnd().Split('\n').Length ?? 6;
            const float diagramY = 108, diagramGap = 10, actionGap = 10;
            float diagramHeight = Mathf.Clamp(actionY - diagramY - rows * 24 - diagramGap - actionGap, 40, 84);
            WorkshopUi.Graphic<WorkshopProtectionDirections>(details, "Component protection directions", new Rect(right + 22, diagramY, w - right - 44, diagramHeight));
            float previewHeight = Mathf.Max(24, diagramHeight - 20);
            var preview = WorkshopUi.Graphic<WeaponAssemblyGraphic>(details, "Component protection foundation", new Rect(right + 64, diagramY + (diagramHeight - previewHeight) * .5f, w - right - 128, previewHeight));
            preview.SetComposition(AssemblyVisualComposer.ComposeFoundation((candidate ?? draft.Resolved).Snapshot.Recipe.Tower.Foundation));
            float statsY = diagramY + diagramHeight + diagramGap;
            float pitch = Mathf.Min(27, (actionY - actionGap - statsY) / Mathf.Max(1, rows));
            MetricRegion(details, stats, new Rect(right + 10, statsY, w - right - 20, rows * pitch), pitch, pitch >= 26.5f ? 20 : 18);
            StackActions(details, right + 10, actionY, w - right - 20);
            WorkshopUi.Place(Root.Find("Component show unavailable") as RectTransform, new Rect(12, 4, Mathf.Min(330, right - 28), 30));
            MoveDetail(details, "Component inspected name", new Rect(14, titleY, right - 34, titleHeight));
        }
        void LayoutDiagramSheet(RectTransform details, ResolvedEntry candidate, float w, float h, string frameName)
        {
            details.GetComponent<WorkshopSurfaceGraphic>().color = Color.clear;
            float right = w * .62f;
            LocalFrame(details, frameName + " stage", new Rect(0, 0, right - 12, h));
            MoveDetail(details, "Component inspected name", new Rect(right + 12, 2, w - right - 24, 56));
            MoveDetail(details, "Component inspected conditions", new Rect(right + 12, 62, w - right - 24, 24));
            MoveDetail(details, "Component inspected contribution", new Rect(right + 12, 90, w - right - 24, 30));
            HideDetail(details, "Component resolved preview"); HideDetail(details, "Component inspected illustration"); HideDetail(details, "Component inspected modifiers");
            var scenery = Detail(details, "Component preview scenery")?.GetComponent<Image>();
            if (scenery != null) { WorkshopUi.Place(scenery.rectTransform, new Rect(12, h - 140, right - 36, 104)); scenery.color = new Color(1, 1, 1, .12f); }
            var diagram = WorkshopUi.Graphic<WorkshopAssemblyDiagram>(details, "Component assembly diagram", new Rect(12, 48, right - 36, h - 110));
            diagram.Configure((candidate ?? draft.Resolved).Snapshot.Recipe, draft.ActiveComponent, draft.ActiveMount);
            ui.Label(details, "Component diagram caption", Copy("diagram"), new Rect(14, h - 48, right - 40, 32), 18, true, WorkshopUi.Muted).alignment = TextAnchor.MiddleCenter;
            WorkshopUi.Place(Root.Find("Component show unavailable") as RectTransform, new Rect(details.anchoredPosition.x + right - Mathf.Min(308, right - 16), 3, Mathf.Min(300, right - 24), 30));
        }
        void LayoutFoundationParts(RectTransform details, ResolvedEntry candidate, float w, float h)
        {
            LayoutDiagramSheet(details, candidate, w, h, "Component foundation parts");
            float right = w * .62f, actionY = ActionTop(details, h); var stats = VisibleStatistics(details); int rows = stats?.text.TrimEnd().Split('\n').Length ?? 5;
            float pitch = Mathf.Clamp((actionY - 142) / rows, 29, 46);
            LocalFrame(details, "Component foundation statistics frame", new Rect(right, 0, w - right, 138 + rows * pitch));
            MetricRegion(details, stats, new Rect(right + 10, 128, w - right - 20, rows * pitch), pitch, 21);
            StackActions(details, right + 8, actionY, w - right - 16);
        }
        void LayoutStructure(RectTransform details, ComponentDefinition inspected, ResolvedEntry candidate, float w, float h)
        {
            LayoutDiagramSheet(details, candidate, w, h, "Component structure parts");
            float right = w * .62f, actionY = ActionTop(details, h); var stats = VisibleStatistics(details);
            var support = (candidate ?? draft.Resolved).Support;
            string[] rows = (stats?.text ?? "").TrimEnd().Split('\n');
            int metricCount = support.Kind == AssemblyStructureFamily.Generator ? 4 : support.Kind == AssemblyStructureFamily.Wall ? 3 : support.Kind == AssemblyStructureFamily.Repair ? 4 : rows.Length;
            if (stats?.name == "Component comparison") metricCount = rows.Length;
            if (stats != null && stats.name == "Component resolved statistics") stats.text = string.Join("\n", rows.Take(metricCount));
            float metricsPitch = Mathf.Clamp((actionY - 254) / Mathf.Max(1, metricCount), 27, 38);
            float bottom = 128 + metricCount * metricsPitch;
            LocalFrame(details, "Component structure statistics frame", new Rect(right, 0, w - right, bottom + 10));
            MetricRegion(details, stats, new Rect(right + 10, 128, w - right - 20, metricCount * metricsPitch), metricsPitch, 21);
            string ruleText = string.Join("\n", rows.Skip(metricCount));
            if (support.Kind == AssemblyStructureFamily.Generator) ruleText += "\nДоход за полную пережитую волну";
            if (support.Kind == AssemblyStructureFamily.Slow) ruleText = "Замедляет врагов в указанном радиусе";
            if (!string.IsNullOrWhiteSpace(ruleText) && stats?.name != "Component comparison")
            {
                float rulesY = bottom + 20;
                LocalFrame(details, "Component placement rules frame", new Rect(right, rulesY, w - right, Mathf.Max(52, actionY - rulesY - 10)));
                ui.Label(details, "Component placement rules heading", Copy("placement_rules"), new Rect(right + 12, rulesY + 2, w - right - 24, 30), 21, true);
                var rules = ui.Label(details, "Component placement rules", ruleText, new Rect(right + 14, rulesY + 34, w - right - 28, Mathf.Max(24, actionY - rulesY - 48)), 18);
                rules.resizeTextForBestFit = true; rules.resizeTextMinSize = 15; rules.resizeTextMaxSize = 18;
            }
            StackActions(details, right + 8, actionY, w - right - 16);
        }
        void LayoutInvalidParts(RectTransform details, ComponentDefinition inspected, float w, float h)
        {
            float right = w * .60f; details.GetComponent<WorkshopSurfaceGraphic>().color = Color.clear;
            LocalFrame(details, "Component invalid parts frame", new Rect(0, 0, right - 12, h));
            LocalFrame(details, "Component conflict inspector", new Rect(right, 0, w - right, h), NotebookPalette.Red);
            HideDetail(details, "Component resolved preview"); HideDetail(details, "Component inspected illustration"); HideDetail(details, "Component preview scenery");
            HideDetail(details, "Component inspected conditions"); HideDetail(details, "Component inspected contribution"); HideDetail(details, "Component inspected modifiers");
            HideDetail(details, "Component resolved statistics"); HideDetail(details, "Component comparison"); HideDetail(details, "Component magazine cycle"); HideDetail(details, "Component diagnostics");
            MoveDetail(details, "Component inspected name", new Rect(14, 2, right - 34, 38));
            var diagram = WorkshopUi.Graphic<WorkshopAssemblyDiagram>(details, "Component invalid parts diagram", new Rect(18, 88, right - 50, h - 142));
            diagram.Configure(draft.PreviewRecipe, draft.ActiveComponent, draft.ActiveMount);
            ui.Label(details, "Component invalid parts caption", Copy("diagram"), new Rect(14, h - 42, right - 40, 30), 18, true, WorkshopUi.Muted);
            ui.Label(details, "Component conflict heading", "Детали несовместимы", new Rect(right + 12, 2, w - right - 24, 42), w - right > 400 ? 28 : 23, true, NotebookPalette.Red);
            var issue = draft.Diagnostics.First(value => value.Code == "assembly.part.required");
            var requiring = issue.Arguments.Count > 0 ? Definition(issue.Arguments[0]) : inspected;
            var required = issue.Arguments.Count > 1 ? Definition(issue.Arguments[1]) : null;
            var conflict = required == null ? null : ActiveParts().Select(part => Definition(part.Id)).FirstOrDefault(part => part?.Category == required.Category);
            float pairWidth = (w - right - 70) * .5f;
            if (conflict != null) DrawPart(details, conflict, "Component conflict selected part", new Rect(right + 12, 50, pairWidth, 96));
            if (requiring != null) DrawPart(details, requiring, "Component conflict requiring part", new Rect(w - pairWidth - 12, 50, pairWidth, 96));
            ui.Label(details, "Component conflict cross", "×", new Rect(right + (w - right) * .5f - 16, 73, 32, 48), 42, true, NotebookPalette.Red).alignment = TextAnchor.MiddleCenter;
            string names = (conflict == null ? "" : ui.Resolve(conflict.Name)) + " + " + (requiring == null ? "" : ui.Resolve(requiring.Name));
            ui.Label(details, "Component conflict selected names", names, new Rect(right + 14, 150, w - right - 28, 48), 19, true);
            string message = DiagnosticText(issue);
            if (requiring != null && required != null) message = ui.Resolve(requiring.Name) + " · требуется: " + ui.Resolve(required.Name);
            ui.Label(details, "Component conflict requirement", message, new Rect(right + 14, 206, w - right - 28, Mathf.Max(60, ActionTop(details, h) - 218)), 21, color: NotebookPalette.Red);
            StackActions(details, right + 10, ActionTop(details, h), w - right - 20);
            WorkshopUi.Place(Root.Find("Component show unavailable") as RectTransform, new Rect(details.anchoredPosition.x + 12, 4, Mathf.Min(300, right - 36), 30));
            MoveDetail(details, "Component inspected name", new Rect(14, 38, right - 34, 38));
        }

        // These notebook pages deliberately own their frames and row pitch. Other component,
        // family, diagnostic and purchase pages keep the existing layout and command owners.
        void LayoutWeaponDrawing(RectTransform details, ComponentDefinition inspected, ResolvedEntry candidate, float w, float h)
        {
            Transform Find(string name) => details.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
            void Place(string name, Rect bounds) { var item = Find(name) as RectTransform; if (item != null) WorkshopUi.Place(item, bounds); }
            void Hide(string name) { var item = Find(name); if (item != null) item.gameObject.SetActive(false); }
            float right = w * .62f, inset = 14;
            details.GetComponent<WorkshopSurfaceGraphic>().color = Color.clear;
            var stageFrame = ui.Panel(details, "Component drawing stage frame", new Rect(0, 0, right - 12, h), Color.white);
            stageFrame.SetAsFirstSibling();
            float statsY = 128, pitch = Mathf.Clamp((h - statsY - 92) / 6, 34, 46);
            float tableBottom = statsY + pitch * 6;
            var statisticsFrame = ui.Panel(details, "Component drawing statistics frame", new Rect(right, 0, w - right, tableBottom + 14), Color.white);
            statisticsFrame.SetAsFirstSibling();
            Place("Component inspected name", new Rect(right + inset, 4, w - right - inset * 2, 38));
            Place("Component inspected conditions", new Rect(right + inset, 45, w - right - inset * 2, 28));
            Place("Component inspected contribution", new Rect(right + inset, 77, w - right - inset * 2, 42));
            Hide("Component inspected modifiers"); Hide("Component resolved preview"); Hide("Component inspected illustration");
            var scenery = Find("Component preview scenery")?.GetComponent<Image>();
            if (scenery != null)
            {
                // The same project scenery stays decorative; it must not compete with the parts.
                WorkshopUi.Place(scenery.rectTransform, new Rect(12, h - 132, right - 36, 112));
                scenery.color = new Color(1, 1, 1, .16f);
            }
            var diagram = WorkshopUi.Graphic<WorkshopAssemblyDiagram>(details, "Component assembly diagram", new Rect(18, 54, right - 48, h - 132));
            diagram.Configure((candidate ?? draft.Resolved).Snapshot.Recipe, draft.ActiveComponent, draft.ActiveMount);
            var caption = ui.Label(details, "Component diagram caption", Copy("diagram"), new Rect(inset, h - 58, right - inset * 2 - 12, 34), 18, true, WorkshopUi.Muted);
            caption.alignment = TextAnchor.MiddleCenter;
            var statistics = Find("Component comparison")?.GetComponent<Text>() ?? Find("Component resolved statistics")?.GetComponent<Text>();
            if (statistics != null)
            {
                WorkshopUi.Place(statistics.rectTransform, new Rect(right + 44, statsY + 2, w - right - 58, pitch * 6));
                FitPitchedMetrics(statistics, w - right > 430 ? 24 : 21, pitch);
                var rules = WorkshopUi.Graphic<WorkshopMetricRules>(details, "Component drawing metric rules", new Rect(right + 10, statsY, w - right - 20, pitch * 6));
                rules.Rows = 6; rules.transform.SetSiblingIndex(2);
                var icons = new[] { WorkshopIcon.Damage, WorkshopIcon.Target, WorkshopIcon.Settings, WorkshopIcon.Redo, WorkshopIcon.Settings, WorkshopIcon.Cost };
                for (int i = 0; i < icons.Length; i++)
                {
                    var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(details, "Component drawing metric icon " + i, new Rect(right + 14, statsY + i * pitch + 7, 24, 24));
                    icon.Icon = icons[i]; icon.color = WorkshopUi.Blue;
                }
            }
            Place("Component select part", new Rect(right + 4, tableBottom + 26, w - right - 8, 46));
            Place("Component purchase part", new Rect(right + 4, tableBottom + 26, w - right - 8, 46));
            // The local editor has no trial action. Save/cancel remain on the enclosing sheet.
            var toggle = Root.Find("Component show unavailable") as RectTransform;
            WorkshopUi.Place(toggle, new Rect(details.anchoredPosition.x + right - Mathf.Min(308, right - 16), 2, Mathf.Min(300, right - 24), 32));
        }

        static void FitPitchedMetrics(Text text, int size, float pitch)
        {
            text.fontSize = size; text.resizeTextForBestFit = false; text.lineSpacing = 1;
            using (var measurement = new TextGenerator())
            {
                var settings = text.GetGenerationSettings(new Vector2(2000, 1000));
                settings.resizeTextForBestFit = false; settings.lineSpacing = 1;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                measurement.Populate("А\nА", settings);
                if (measurement.lines.Count >= 2)
                {
                    float native = Mathf.Abs(measurement.lines[1].topY - measurement.lines[0].topY) / text.pixelsPerUnit;
                    if (native > .001f) text.lineSpacing = pitch / native;
                }
            }
        }

        void LayoutOwnedHeadComparison(RectTransform details, ComponentDefinition inspected, ResolvedEntry candidate, float w, float h)
        {
            Transform Find(string name) => details.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
            void Hide(string name) { var item = Find(name); if (item != null) item.gameObject.SetActive(false); }
            float right = w * .65f, leftWidth = right - 14, half = (leftWidth - 12) * .5f;
            float stageTop = 42, stageHeight = h - 182, captionTop = stageTop + stageHeight - 48;
            details.GetComponent<WorkshopSurfaceGraphic>().color = Color.clear;
            for (int i = 0; i < 2; i++)
            {
                var frame = ui.Panel(details, "Component comparison art frame " + i, new Rect(i * (half + 12), stageTop, half, stageHeight), Color.white);
                frame.SetAsFirstSibling();
                string artName = i == 0 ? "Component current part illustration" : "Component candidate part illustration";
                var art = Find(artName) as RectTransform;
                if (art != null)
                {
                    float side = Mathf.Min(half - 36, stageHeight - 78);
                    WorkshopUi.Place(art, new Rect(i * (half + 12) + (half - side) * .5f, stageTop + (stageHeight - 48 - side) * .5f, side, side));
                    art.pivot = new Vector2(.5f, .5f);
                    art.anchoredPosition += new Vector2(side * .5f, -side * .5f);
                    art.localEulerAngles = new Vector3(0, 0, -38);
                }
                string captionName = i == 0 ? "Component current part caption" : "Component candidate part caption";
                var captionFrame = ui.Panel(details, captionName + " frame", new Rect(i * (half + 12) + 12, captionTop, half - 24, 38), WorkshopUi.Surface);
                captionFrame.GetComponent<WorkshopSurfaceGraphic>().Outline(1, WorkshopUi.Blue);
                var caption = Find(captionName)?.GetComponent<Text>();
                if (caption != null)
                {
                    WorkshopUi.Place(caption.rectTransform, new Rect(i * (half + 12) + 20, captionTop, half - 40, 38));
                    caption.fontSize = 21; caption.resizeTextMinSize = 16; caption.resizeTextMaxSize = 21;
                    caption.transform.SetAsLastSibling();
                }
            }
            Hide("Component inspected name"); Hide("Component inspected conditions"); Hide("Component inspected contribution");
            Hide("Component inspected modifiers"); Hide("Component comparison");
            float tableWidth = w - right, header = 60, pitch = Mathf.Clamp((h - header - 114) / 6, 40, 52);
            var table = ui.Panel(details, "Component comparison table", new Rect(right, 42, tableWidth, header + pitch * 6), Color.white);
            ui.Label(details, "Component comparison page heading", Copy("comparison_heading"), new Rect(right + 10, 0, tableWidth - 20, 36), tableWidth > 400 ? 25 : 22, true);
            var current = draft.Resolved;
            var before = current?.Weapons.FirstOrDefault(item => item.SlotIndex == draft.ActiveMount);
            var after = candidate?.Weapons.FirstOrDefault(item => item.SlotIndex == draft.ActiveMount);
            if (before == null || after == null) return;
            var currentPart = Definition(EquippedId());
            Text Cell(string name, string value, Rect bounds, int size, Color color, TextAnchor alignment)
            {
                var label = ui.Label(table, name, value, bounds, size, color: color);
                label.alignment = alignment; label.resizeTextForBestFit = true;
                label.resizeTextMinSize = size - 3; label.resizeTextMaxSize = size; return label;
            }
            Cell("Component comparison current heading", ui.Resolve(currentPart.Name), new Rect(tableWidth * .43f, 2, tableWidth * .23f, header - 4), 18, WorkshopUi.Ink, TextAnchor.MiddleCenter);
            Cell("Component comparison candidate heading", ui.Resolve(inspected.Name), new Rect(tableWidth * .73f, 2, tableWidth * .25f - 4, header - 4), 18, WorkshopUi.Ink, TextAnchor.MiddleCenter);
            var rules = WorkshopUi.Graphic<WorkshopMetricRules>(table, "Component comparison table rules", new Rect(8, header, tableWidth - 16, pitch * 6)); rules.Rows = 6;
            string Number(float value) => value.ToString("0.###", ui.TextResolver.Culture);
            var labels = new[] { Copy("stat.Damage"), Copy("stat.Range"), Copy("stat.Interval"), "DPS", Copy("stat.Cost"), Copy("stat.Load") };
            var oldValues = new[] { before.Damage, before.Range, before.Interval, current.Dps, current.Cost, current.Load };
            var newValues = new[] { after.Damage, after.Range, after.Interval, candidate.Dps, candidate.Cost, candidate.Load };
            var icons = new[] { WorkshopIcon.Damage, WorkshopIcon.Target, WorkshopIcon.Redo, WorkshopIcon.Damage, WorkshopIcon.Cost, WorkshopIcon.Settings };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = header + i * pitch;
                var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(table, "Component comparison row icon " + i, new Rect(12, y + (pitch - 24) * .5f, 24, 24)); icon.Icon = icons[i]; icon.color = WorkshopUi.Blue;
                Cell("Component comparison row label " + i, labels[i], new Rect(42, y, tableWidth * .43f - 44, pitch), tableWidth > 430 ? 24 : 20, WorkshopUi.Ink, TextAnchor.MiddleLeft);
                bool equal = Mathf.Approximately(oldValues[i], newValues[i]);
                bool improved = i == 2 || i >= 4 ? newValues[i] < oldValues[i] : newValues[i] > oldValues[i];
                Color outcome = equal ? WorkshopUi.Blue : improved ? new Color32(27, 122, 79, 255) : i >= 4 ? NotebookPalette.Red : WorkshopUi.Warning;
                string oldValue = i == 3 ? oldValues[i].ToString("0.##", ui.TextResolver.Culture) : Number(oldValues[i]);
                string newValue = i == 3 ? newValues[i].ToString("0.##", ui.TextResolver.Culture) : Number(newValues[i]);
                if (i == 2) newValue += " с";
                if (i == 4) newValue += " штр.";
                Cell("Component comparison current value " + i, oldValue, new Rect(tableWidth * .43f, y, tableWidth * .21f, pitch), 24, WorkshopUi.Blue, TextAnchor.MiddleCenter);
                Cell("Component comparison arrow " + i, "→", new Rect(tableWidth * .65f, y, tableWidth * .08f, pitch), 24, WorkshopUi.Blue, TextAnchor.MiddleCenter);
                Cell("Component comparison candidate value " + i, newValue, new Rect(tableWidth * .73f, y, tableWidth * .25f - 4, pitch), 24, outcome, TextAnchor.MiddleCenter);
            }
            float actionY = 42 + header + pitch * 6 + 14;
            foreach (string name in new[] { "Component select part", "Component purchase part", "Component apply suggested part" })
            {
                var action = Find(name) as RectTransform;
                if (action != null) WorkshopUi.Place(action, new Rect(right + 4, actionY, tableWidth - 8, 46));
            }
            var toggle = Root.Find("Component show unavailable") as RectTransform;
            WorkshopUi.Place(toggle, new Rect(0, 2, Mathf.Min(330, leftWidth), 32));
        }

        void FrameRows(Transform parent, Text text, float x, float y, float width, float rowHeight)
        {
            if (text == null || !text.gameObject.activeSelf || string.IsNullOrWhiteSpace(text.text)) return;
            rowHeight=WorkshopMetricText.Baseline;
            var rows = text.text.TrimEnd('\r','\n').Split('\n');
            (text as WorkshopMetricText)?.FitRows();
            var lines = WorkshopUi.Graphic<WorkshopMetricRules>(parent,"Component metric rules "+text.name,new Rect(x,y,width,Mathf.Max(rowHeight,rows.Length*rowHeight)));
            lines.Rows=rows.Length; lines.raycastTarget=false; lines.transform.SetSiblingIndex(0);
            for(int i=0;i<rows.Length;i++)
            {
                float offset=i*rowHeight;
                var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent,"Component metric icon "+i,new Rect(x+3,y+offset+3,20,20));
                string row=rows[i];
                icon.Icon = row.Contains("Урон") || row.Contains("урон") || row.Contains("DPS") ? WorkshopIcon.Damage
                    : row.Contains("Прочность") || row.Contains("защит") || row.Contains("Фронт") ? WorkshopIcon.Shield
                    : row.Contains("Дальн") || row.Contains("Радиус") ? WorkshopIcon.Target
                    : row.Contains("Цен") || row.Contains("Доход") ? WorkshopIcon.Cost : WorkshopIcon.Settings;
                icon.color=WorkshopUi.Ink; icon.raycastTarget = false;
            }
        }
    }

    public sealed class WorkshopNeutralPartGraphic : NotebookAtlasGraphic
    {
        readonly MeshPen pen = new MeshPen(null, 128);
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); pen.Clear(); var r = rectTransform.rect;
            float side = Mathf.Min(r.width, r.height) * .65f; var center = r.center;
            var ink = MeshPen.Alpha(WorkshopUi.Blue, .35f);
            pen.Line(center + new Vector2(-side * .5f, 0), center + new Vector2(0, side * .28f), 2, ink);
            pen.Line(center + new Vector2(0, side * .28f), center + new Vector2(side * .5f, 0), 2, ink);
            pen.Line(center + new Vector2(side * .5f, 0), center + new Vector2(0, -side * .28f), 2, ink);
            pen.Line(center + new Vector2(0, -side * .28f), center + new Vector2(-side * .5f, 0), 2, ink);
            pen.Line(center + new Vector2(-side * .18f, 0), center + new Vector2(-side * .02f, -side * .13f), 2.5f, WorkshopUi.Blue);
            pen.Line(center + new Vector2(-side * .02f, -side * .13f), center + new Vector2(side * .25f, side * .14f), 2.5f, WorkshopUi.Blue);
            pen.CopyTo(target, Vector2.zero, 1);
        }
    }
    public sealed class WorkshopPurchasePaperEdge : NotebookAtlasGraphic
    {
        readonly MeshPen pen = new MeshPen(null, 1024);
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); pen.Clear(); var r = rectTransform.rect;
            for (int edge = 0; edge < 4; edge++)
            {
                Vector2 a = edge == 0 ? new Vector2(r.xMin + 3, r.yMin + 3) : edge == 1 ? new Vector2(r.xMax - 3, r.yMin + 3)
                    : edge == 2 ? new Vector2(r.xMax - 3, r.yMax - 3) : new Vector2(r.xMin + 3, r.yMax - 3);
                Vector2 b = edge == 0 ? new Vector2(r.xMax - 3, r.yMin + 3) : edge == 1 ? new Vector2(r.xMax - 3, r.yMax - 3)
                    : edge == 2 ? new Vector2(r.xMin + 3, r.yMax - 3) : new Vector2(r.xMin + 3, r.yMin + 3);
                var normal = new Vector2(-(b - a).normalized.y, (b - a).normalized.x);
                int count = Mathf.CeilToInt(Vector2.Distance(a, b) / 12); Vector2 previous = a;
                for (int i = 1; i <= count; i++)
                {
                    var next = Vector2.Lerp(a, b, i / (float)count) + normal * Mathf.Sin(i * 2.1f + edge) * 1.5f;
                    pen.Line(previous, next, 1.4f, WorkshopUi.Blue); previous = next;
                }
            }
            pen.CopyTo(target, Vector2.zero, 1);
        }
    }
    public sealed class WorkshopMetricRules : NotebookAtlasGraphic
    {
        readonly MeshPen pen = new MeshPen(null,512);
        public int Rows;
        public bool DivideColumns;
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); pen.Clear(); if(Rows<1)return; var r=rectTransform.rect;
            for(int i=0;i<=Rows;i++)
            {
                float y=r.yMax-r.height*i/Rows;
                pen.Line(new Vector2(r.xMin,y),new Vector2(r.xMax,y),1,MeshPen.Alpha(WorkshopUi.Blue,.35f));
            }
            if(DivideColumns) pen.Line(new Vector2(r.center.x,r.yMin),new Vector2(r.center.x,r.yMax),1,MeshPen.Alpha(WorkshopUi.Blue,.45f));
            pen.CopyTo(target,Vector2.zero,1);
        }
    }
}
