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
        void ApplySketchLayout(ComponentDefinition inspected, ResolvedEntry candidate, bool family, bool comparison, bool catalogue)
        {
            if (inspected == null) return;
            var details = Root.Find("Component details") as RectTransform;
            if (details == null) return;
            float w = details.rect.width, h = details.rect.height;
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
                var toggle = Root.Find("Component show unavailable") as RectTransform;
                WorkshopUi.Place(toggle, new Rect(0, 0, width * .65f - 18, 34));
                Move("Component inspected name", new Rect(12, 4, w-24, 34), 24);
                Hide("Component current part illustration"); Hide("Component current part caption"); Hide("Component candidate part caption");
                float actionTop=h-54;
                foreach (string action in new[] { "Component select part", "Component purchase part", "Component apply suggested part" })
                {
                    var item=Find(action) as RectTransform;
                    if(item != null && item.gameObject.activeSelf) actionTop=Mathf.Min(actionTop,-item.anchoredPosition.y);
                }
                float comparisonY=Mathf.Min(182,actionTop-196);
                Art("Component candidate part illustration", new Rect(w*.22f,40,w*.56f,Mathf.Max(48,comparisonY-48)), true);
                Move("Component inspected conditions", new Rect(12,comparisonY+166,w-24,24), 16);
                Hide("Component inspected contribution");
                Move("Component comparison", new Rect(44,comparisonY+2,w-56,162), 20);
                Hide("Component inspected modifiers");
                FrameRows(details,Find("Component comparison")?.GetComponent<Text>(),12,comparisonY,w-24,27);
                foreach (string action in new[] { "Component select part", "Component purchase part", "Component apply suggested part" })
                {
                    var item = Find(action) as RectTransform; if (item != null) Move(action,new Rect(12,item.anchoredPosition.y*-1,w-24,item.sizeDelta.y));
                }
                foreach (var illustration in Root.GetComponentsInChildren<WeaponAssemblyGraphic>().Where(value => value.name == "Component part illustration"))
                {
                    var rect = illustration.rectTransform; var area = rect.rect; float side = Mathf.Min(area.width,area.height)*.80f;
                    var center = rect.anchoredPosition + new Vector2(area.width*.5f,-area.height*.5f);
                    rect.pivot = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(side,side);
                    rect.anchoredPosition = center; rect.localEulerAngles = new Vector3(0,0,-38);
                }
                return;
            }
            if (comparison)
            {
                float half = (right-36)*.5f, artHeight = Mathf.Max(80,h-196);
                Move("Component inspected name",new Rect(right+16,42,w-right-32,34),24);
                var heading = ui.Label(details,"Component comparison page heading",Copy("comparison_heading"),new Rect(14,4,right-28,34),24,true);
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
