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
        void Review(Transform parent, float w, float h)
        {
            bool reference = Route == AlbumEntryKind.Tower;
            if (!reference) ui.Label(parent, "Composite review heading", T("review"), new Rect(18, 8, w - 36, 44), 28, true);
            float illustrationHeight = Mathf.Max(190, h - (reference ? 142 : 158));
            if (Route == AlbumEntryKind.Legacy)
            {
                var picture = WorkshopUi.Graphic<BlueprintSketch>(parent, "Composite assembled preview", new Rect(14, 58, w - 28, illustrationHeight));
                picture.SetBlueprint(DraftSession.Draft.LegacyBlueprint);
            }
            else if (reference)
            {
                var art = new Rect(8, 4, w - 16, illustrationHeight);
                WorkshopScenery(parent, "Composite preview scenery", art, .78f);
                RecipePicture(parent, DraftSession.PreviewEntry.Snapshot, "Composite certified recipe preview", art);
                var name = ui.Label(parent, "Composite review drawing name", DraftSession.Name, new Rect(18, illustrationHeight + 8, w - 36, 38), 27, true);
                name.alignment = TextAnchor.MiddleCenter;
            }
            else DrawRecipePreview(parent, DraftSession.PreviewRecipe, w, illustrationHeight);
            float compositionY = illustrationHeight + (reference ? 50 : 64);
            float compositionHeight = Mathf.Max(60, h - compositionY - 8);
            var composition = new CatalogScrollList(parent, "Composite review composition viewport", new Rect(18, compositionY, w - 36, compositionHeight), 1, 1, h, 0);
            var compositionText = ui.Label(composition.Content, "Composite review composition", CompositionText(), new Rect(0, 0, w - 54, compositionHeight), reference ? 13 : 16);
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
            if (Route == AlbumEntryKind.Tower && Step == 0) { FoundationSummary(parent, bounds); return; }
            if (Route == AlbumEntryKind.Tower && Step == 1) { WeaponSummary(parent, bounds); return; }
            if (UsesReferenceReview) { ReferenceReviewSummary(parent, bounds); return; }
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

        bool UsesReferenceReview => Route == AlbumEntryKind.Tower && Step == 2 && !HasWriteFailure;

        void ReferenceReviewSummary(Transform parent, Rect bounds)
        {
            var sheet = ui.Panel(parent, "Composite summary", bounds, WorkshopUi.Paper);
            float w = bounds.width, h = bounds.height;
            var banner = ui.Label(sheet, "Composite preview title", DraftSession.CanSave ? "Готово к защите" : "Проверь сборку", new Rect(18, 6, w - 36, 44), 29, true);
            banner.alignment = TextAnchor.MiddleCenter;
            ReferenceDetail(sheet, "Composite review title line", new Rect(26, 49, w - 52, 2), FoundationDetailGraphic.Kind.Divider);
            ui.Label(sheet, "Composite name label", T("name"), new Rect(18, 58, w - 36, 26), 18);
            var fieldRoot = ui.Panel(sheet, "Composite name", new Rect(18, 88, w - 36, 48), WorkshopUi.Surface, true);
            var field = fieldRoot.gameObject.AddComponent<InputField>();
            var nameText = ui.Label(fieldRoot, "Composite name value", DraftSession.Name, new Rect(12, 2, w - 60, 44), 23);
            field.textComponent = nameText; field.text = DraftSession.Name; field.characterLimit = 0;
            var nameSession = DraftSession;
            field.onEndEdit.AddListener(value => {
                nameSession.Rename(value);
                if (ReferenceEquals(nameSession, DraftSession))
                {
                    feedback = ""; RefreshStatus();
                    var drawingName = Root.GetComponentsInChildren<Text>().FirstOrDefault(label => label.name == "Composite review drawing name");
                    if (drawingName != null) drawingName.text = DraftSession.Name;
                }
            });
            var metrics = DraftSession.PreviewMetrics;
            bool compatible = !DraftSession.Resolution.Diagnostics.Any(issue => issue.Code != "assembly.load.exceeded");
            bool owned = DraftSession.PreviewRecipe.Parts.All(reference => DraftSession.IsOwned(reference.Id));
            ReviewCheck(sheet, "Composite compatibility check", compatible ? "Все детали совместимы" : "Проверь совместимость деталей", 150, w, compatible);
            ReviewCheck(sheet, "Composite ownership check", owned ? "Детали открыты" : "Есть закрытые детали", 190, w, owned);
            bool loadOk = metrics != null && !metrics.Overloaded;
            string load = metrics == null ? T("clearance") : metrics.Overloaded ? T("load_cost", metrics.Load, metrics.Capacity, metrics.Cost) : "Нагрузка " + metrics.Load + "/" + metrics.Capacity;
            ReviewCheck(sheet, "Composite load cost", load, 230, w, loadOk);
            ReferenceDetail(sheet, "Composite review stats divider", new Rect(18, 268, w - 36, 2), FoundationDetailGraphic.Kind.Divider);
            ReviewValue(sheet, "cost", "Цена постройки", metrics == null ? "—" : metrics.Cost + " штр.", 278, w, WorkshopIcon.Pencil);
            ReviewValue(sheet, "health", "Прочность", metrics?.Health.ToString("0.#") ?? "—", 314, w, WorkshopIcon.Shield);
            ReviewValue(sheet, "dps", "DPS", DraftSession.Resolved?.Dps.ToString("0.##") ?? "—", 350, w, WorkshopIcon.Target);
            var validation = ui.Label(sheet, "Composite validation", HasStatusFailure ? StatusText() : "", new Rect(18, h - 101, w - 36, 34), 15,
                color: HasStatusFailure ? WorkshopUi.Warning : WorkshopUi.Success);
            validation.alignment = TextAnchor.UpperLeft;
            var trial = ui.Button(sheet, "Composite trial", new Rect(18, h - 60, w - 36, 46), T("trial"), OpenTrial, WorkshopIcon.Target);
            trial.interactable = DraftSession.CanTrial;
        }

        void ReviewCheck(Transform parent, string name, string caption, float y, float w, bool valid)
        {
            Color tint = valid ? (Color)new Color32(20, 145, 70, 255) : WorkshopUi.Warning;
            var disc = WorkshopUi.Graphic<WorkshopReviewCheckDisc>(parent, name + " disc", new Rect(20, y + 1, 32, 32));
            disc.color = tint;
            var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, name + " icon", new Rect(24, y + 5, 24, 24));
            icon.Icon = valid ? WorkshopIcon.Check : WorkshopIcon.Help; icon.color = Color.white;
            var label = ui.Label(parent, name, caption, new Rect(66, y, w - 84, 36), w < 400 ? 18 : 20, color: valid ? WorkshopUi.Ink : WorkshopUi.Warning);
            label.alignment = TextAnchor.MiddleLeft;
        }
        void ReviewValue(Transform parent, string name, string title, string value, float y, float w, WorkshopIcon kind)
        {
            var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, "Composite review " + name + " icon", new Rect(20, y + 4, 28, 28)); icon.Icon = kind; icon.color = WorkshopUi.Ink;
            ui.Label(parent, "Composite review " + name + " title", title, new Rect(58, y, w * .57f - 58, 36), w < 400 ? 17 : 19);
            var number = ui.Label(parent, "Composite review " + name + " value", value, new Rect(w * .57f, y, w * .43f - 20, 36), 23, true);
            number.alignment = TextAnchor.MiddleRight;
        }

    }

    public sealed class WorkshopReviewCheckDisc : MaskableGraphic
    {
        readonly MeshPen pen = new MeshPen(null, 384);
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); pen.Clear();
            var bounds = rectTransform.rect;
            float radius = Mathf.Max(0, Mathf.Min(bounds.width, bounds.height) * .5f - 1.5f);
            pen.Disc(bounds.center, radius, color, 40);
            pen.Ring(bounds.center, radius, 1.6f, Color.Lerp(color, Color.black, .35f), 40);
            pen.Ring(bounds.center + new Vector2(-.25f, .25f), Mathf.Max(0, radius - 2.7f), .7f, new Color(1, 1, 1, .28f), 40);
            pen.CopyTo(target, Vector2.zero, 1);
        }
    }
}
