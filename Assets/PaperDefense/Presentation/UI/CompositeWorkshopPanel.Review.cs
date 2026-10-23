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
            bool wideReference = reference && width >= 1400;
            float illustrationHeight = Mathf.Max(190, h - (reference ? 166 : 158));
            if (Route == AlbumEntryKind.Legacy)
            {
                var picture = WorkshopUi.Graphic<BlueprintSketch>(parent, "Composite assembled preview", new Rect(14, 58, w - 28, illustrationHeight));
                picture.SetBlueprint(DraftSession.Draft.LegacyBlueprint);
            }
            else if (reference)
            {
                float artWidth = wideReference ? w * .71f : w - 20;
                illustrationHeight = wideReference ? Mathf.Min(600, illustrationHeight) : illustrationHeight;
                var art = new Rect(wideReference ? w * .225f : (w - artWidth) * .5f, wideReference ? h * .08f : 8, artWidth, illustrationHeight);
                WorkshopScenery(parent, "Composite preview scenery", art, .62f);
                RecipePicture(parent, DraftSession.PreviewEntry.Snapshot, "Composite certified recipe preview", art);
                illustrationHeight = art.yMax;
                var name = ui.Label(parent, "Composite review drawing name", DraftSession.Name, new Rect(18, illustrationHeight + 8, w - 36, 42), 28, true);
                name.alignment = TextAnchor.MiddleCenter;
            }
            else DrawRecipePreview(parent, DraftSession.PreviewRecipe, w, illustrationHeight);
            float compositionY = illustrationHeight + (reference ? 60 : 64);
            float compositionHeight = Mathf.Max(60, h - compositionY - (reference ? 12 : 8));
            if (reference)
            {
                var frame = ReferenceDetail(parent, "Composite review recipe frame", new Rect(18, compositionY - 4, w - 36, compositionHeight + 8), FoundationDetailGraphic.Kind.Frame);
                frame.Fill = new Color(WorkshopUi.Paper.r, WorkshopUi.Paper.g, WorkshopUi.Paper.b, .84f); frame.color = WorkshopUi.Muted;
            }
            float compositionWidth = w - (reference ? 76 : 54);
            var composition = new CatalogScrollList(parent, "Composite review composition viewport",
                reference ? new Rect(26, compositionY + 4, w - 52, compositionHeight - 8) : new Rect(18, compositionY, w - 36, compositionHeight), 1, 1, h, 0);
            var compositionText = ui.Label(composition.Content, "Composite review composition", CompositionText(), new Rect(0, 0, compositionWidth, compositionHeight), reference ? 15 : 16, color: reference ? WorkshopUi.Muted : WorkshopUi.Ink);
            compositionText.alignment = TextAnchor.UpperLeft;
            float contentHeight = Mathf.Max(80, compositionText.preferredHeight + 12);
            WorkshopUi.Place(compositionText.rectTransform, new Rect(0, 0, compositionWidth, contentHeight));
            if (DraftSession.PreviewMetrics?.Overloaded == true)
            {
                var entry = DraftSession.PreviewEntry;
                var contributors = DraftSession.PreviewRecipe.Parts.Select(reference => entry.Snapshot.Definitions.First(part => part.Id == reference.Id && part.Revision == reference.Revision))
                    .Where(part => part.Get(AssemblyStat.Load, ModifierOperation.Add) != 0)
                    .Select(part => ui.Resolve(part.Name) + " " + part.Get(AssemblyStat.Load, ModifierOperation.Add).ToString("+0.##;-0.##;0"));
                var label = ui.Label(composition.Content, "Composite load contributors", UserText.Message("assembly.component.load_parts", string.Join(" · ", contributors)),
                    new Rect(0, contentHeight + 6, compositionWidth, 100), 18, color: WorkshopUi.Warning);
                label.alignment = TextAnchor.UpperLeft;
                float contributionHeight = Mathf.Max(56, label.preferredHeight + 12);
                WorkshopUi.Place(label.rectTransform, new Rect(0, contentHeight + 6, compositionWidth, contributionHeight));
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

        bool UsesReferenceReview => Route == AlbumEntryKind.Tower && Step == 2;

        void BuildSaveFailureSurface()
        {
            var overlay = ui.Panel(Root, "Composite save failure overlay", new Rect(0, 0, width, height), new Color(.12f, .14f, .18f, .12f), true);
            float sheetWidth = Mathf.Min(560, width - 64), sheetHeight = 374;
            float x = Mathf.Min(width - sheetWidth - 36, width * .64f), y = Mathf.Clamp(height * .34f, 132, height - sheetHeight - 88);
            var sheet = ui.Panel(overlay, "Composite save failure sheet", new Rect(x, y, sheetWidth, sheetHeight), Color.clear, true);
            var edge = WorkshopUi.Graphic<WorkshopSaveFailurePaper>(sheet, "Composite save failure edge", new Rect(0, 0, sheetWidth, sheetHeight));
            edge.color = new Color32(152, 42, 43, 255);
            WorkshopUi.Graphic<WorkshopSaveFailureMark>(sheet, "Composite save failure mark", new Rect((sheetWidth - 66) * .5f, 16, 66, 66));
            var title = ui.Label(sheet, "Composite save failure title", "Не удалось сохранить", new Rect(24, 86, sheetWidth - 48, 48), 32, true, edge.color);
            title.alignment = TextAnchor.MiddleCenter;
            var detail = ui.Label(sheet, "Composite save failure detail", StatusText(), new Rect(32, 142, sheetWidth - 64, 70), 20);
            detail.alignment = TextAnchor.MiddleCenter;
            ui.Button(sheet, "Composite save failure retry", new Rect(32, 230, sheetWidth - 64, 54), T("retry_save"), () => Save(saveFailureVariant), WorkshopIcon.Save, true)
                .interactable = DraftSession.CanSave;
            Action dismiss = () => { saveFailureDismissed = true; Refresh(); };
            ui.Button(sheet, "Composite save failure continue", new Rect(32, 298, sheetWidth - 64, 50), "Продолжить сборку", dismiss, WorkshopIcon.Back);
            var save = Root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Composite save");
            WorkshopModal.Open(overlay.gameObject, dismiss, save?.gameObject);
        }

        void ReferenceReviewSummary(Transform parent, Rect bounds)
        {
            if (width >= 1400)
            {
                float cardWidth = Mathf.Max(380, Mathf.Min(580, bounds.width * .8f)), cardHeight = Mathf.Min(648, bounds.height - 60);
                bounds = new Rect(bounds.x, bounds.y + (bounds.height - cardHeight) * .45f, cardWidth, cardHeight);
            }
            var sheet = ui.Panel(parent, "Composite summary", bounds, WorkshopUi.Paper);
            float w = bounds.width, h = bounds.height;
            bool compact = h < 580;
            var bannerFrame = ReferenceDetail(sheet, "Composite review banner frame", new Rect(24, 10, w - 48, compact ? 42 : 52), FoundationDetailGraphic.Kind.Frame);
            bannerFrame.Fill = WorkshopUi.Surface; bannerFrame.Radius = 7;
            var banner = ui.Label(sheet, "Composite preview title", DraftSession.CanSave ? "Готово к защите" : "Проверь сборку", new Rect(28, compact ? 10 : 14, w - 56, 42), compact ? 28 : 32, true);
            banner.alignment = TextAnchor.MiddleCenter;
            ui.Label(sheet, "Composite name label", T("name"), new Rect(22, compact ? 56 : 72, w - 44, 26), 20);
            var fieldRoot = ui.Panel(sheet, "Composite name", new Rect(22, compact ? 84 : 104, w - 44, 48), WorkshopUi.Surface, true);
            ReferenceDetail(fieldRoot, "Composite review name frame", new Rect(0, 0, w - 44, 48), FoundationDetailGraphic.Kind.Frame);
            var field = fieldRoot.gameObject.AddComponent<InputField>();
            var nameText = ui.Label(fieldRoot, "Composite name value", DraftSession.Name, new Rect(12, 2, w - 100, 44), compact ? 23 : 25);
            var pencil = WorkshopUi.Graphic<WorkshopIconGraphic>(fieldRoot, "Composite review name pencil", new Rect(w - 82, 11, 27, 27));
            pencil.Icon = WorkshopIcon.Pencil; pencil.color = WorkshopUi.Ink;
            field.textComponent = nameText; field.text = DraftSession.Name; field.characterLimit = 0;
            var nameSession = DraftSession;
            field.onEndEdit.AddListener(value => {
                nameSession.Rename(value);
                if (ReferenceEquals(nameSession, DraftSession))
                {
                    feedback = ""; RefreshStatus();
                    var drawingName = Root.GetComponentsInChildren<Text>().FirstOrDefault(label => label.name == "Composite review drawing name");
                    if (drawingName != null) drawingName.text = DraftSession.Name;
                    banner.text = DraftSession.CanSave ? "Готово к защите" : "Проверь сборку";
                    var saveNote = Root.Find("Composite content/Composite summary/Composite free save note");
                    if (saveNote != null) saveNote.gameObject.SetActive(!HasStatusFailure);
                }
            });
            var metrics = DraftSession.PreviewMetrics;
            bool compatible = !DraftSession.Resolution.Diagnostics.Any(issue => issue.Code != "assembly.load.exceeded");
            bool owned = DraftSession.PreviewRecipe.Parts.All(reference => DraftSession.IsOwned(reference.Id));
            float checksY = compact ? 142 : 168, checkPitch = compact ? 42 : 48;
            ReviewCheck(sheet, "Composite compatibility check", compatible ? "Все детали совместимы" : "Проверь совместимость деталей", checksY, w, compatible);
            ReviewCheck(sheet, "Composite ownership check", owned ? "Детали открыты" : "Есть закрытые детали", checksY + checkPitch, w, owned);
            bool loadOk = metrics != null && !metrics.Overloaded;
            string load = metrics == null ? T("clearance") : metrics.Overloaded ? T("load_cost", metrics.Load, metrics.Capacity, metrics.Cost) : "Нагрузка " + metrics.Load + "/" + metrics.Capacity;
            ReviewCheck(sheet, "Composite load cost", load, checksY + checkPitch * 2, w, loadOk);
            float valuesY = compact ? 276 : 330, valuePitch = compact ? 38 : 46;
            ReferenceDetail(sheet, "Composite review stats divider", new Rect(22, valuesY - 10, w - 44, 2), FoundationDetailGraphic.Kind.Divider);
            ReviewValue(sheet, "cost", "Цена постройки", metrics == null ? "—" : metrics.Cost + " штр.", valuesY, w, WorkshopIcon.Pencil);
            ReviewValue(sheet, "health", "Прочность", metrics?.Health.ToString("0.#") ?? "—", valuesY + valuePitch, w, WorkshopIcon.Shield);
            ReviewValue(sheet, "dps", "DPS", DraftSession.Resolved?.Dps.ToString("0.##") ?? "—", valuesY + valuePitch * 2, w, WorkshopIcon.Target);
            var validation = ui.Label(sheet, "Composite validation", HasStatusFailure ? StatusText() : "", new Rect(18, h - 101, w - 36, 34), 15,
                color: HasStatusFailure ? WorkshopUi.Warning : WorkshopUi.Success);
            validation.alignment = TextAnchor.UpperLeft;
            var freeSaveNote = ui.Label(sheet, "Composite free save note", "Сохранение бесплатно · Альбом " + run.Workshop.Entries.Count + "/" + ProfileLimits.MaximumAlbumPages,
                new Rect(22, h - 92, w - 44, 28), w < 420 ? 14 : 17, color: WorkshopUi.Muted);
            freeSaveNote.alignment = TextAnchor.MiddleCenter; freeSaveNote.gameObject.SetActive(!HasStatusFailure);
            var trial = ui.Button(sheet, "Composite trial", new Rect(18, h - 60, w - 36, 46), T("trial"), OpenTrial, WorkshopIcon.Target);
            trial.interactable = DraftSession.CanTrial;
        }

        void ReviewCheck(Transform parent, string name, string caption, float y, float w, bool valid)
        {
            Color tint = valid ? (Color)new Color32(20, 145, 70, 255) : WorkshopUi.Warning;
            var disc = WorkshopUi.Graphic<WorkshopReviewCheckDisc>(parent, name + " disc", new Rect(24, y + 2, 36, 36));
            disc.color = tint;
            var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, name + " icon", new Rect(29, y + 7, 26, 26));
            icon.Icon = valid ? WorkshopIcon.Check : WorkshopIcon.Help; icon.color = Color.white;
            var label = ui.Label(parent, name, caption, new Rect(74, y, w - 96, 40), w < 420 ? 19 : 23, color: valid ? WorkshopUi.Ink : WorkshopUi.Warning);
            label.alignment = TextAnchor.MiddleLeft;
        }
        void ReviewValue(Transform parent, string name, string title, string value, float y, float w, WorkshopIcon kind)
        {
            var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, "Composite review " + name + " icon", new Rect(24, y + 3, 30, 30)); icon.Icon = kind; icon.color = WorkshopUi.Ink;
            ui.Label(parent, "Composite review " + name + " title", title, new Rect(68, y, w * .65f - 68, 38), w < 420 ? 18 : 22);
            var number = ui.Label(parent, "Composite review " + name + " value", value, new Rect(w * .65f, y, w * .35f - 24, 38), w < 420 ? 24 : 28, true);
            number.alignment = TextAnchor.MiddleRight;
        }

    }

    /// <summary>The red pencil sheet belongs only to the recoverable save warning.</summary>
    public sealed class WorkshopSaveFailurePaper : MaskableGraphic
    {
        readonly MeshPen pen = new MeshPen(null, 1536);

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); pen.Clear();
            var r = rectTransform.rect;
            if (r.width < 24 || r.height < 24) return;
            var edge = new[] {
                new Vector2(r.xMin + 7, r.yMin + 4), new Vector2(r.xMin + r.width * .33f, r.yMin + 6),
                new Vector2(r.xMin + r.width * .68f, r.yMin + 3), new Vector2(r.xMax - 9, r.yMin + 6),
                new Vector2(r.xMax - 4, r.yMin + 14), new Vector2(r.xMax - 6, r.yMin + r.height * .52f),
                new Vector2(r.xMax - 3, r.yMax - 12), new Vector2(r.xMax - 11, r.yMax - 5),
                new Vector2(r.xMin + r.width * .64f, r.yMax - 7), new Vector2(r.xMin + r.width * .27f, r.yMax - 4),
                new Vector2(r.xMin + 10, r.yMax - 6), new Vector2(r.xMin + 4, r.yMax - 14),
                new Vector2(r.xMin + 6, r.yMin + r.height * .46f)
            };
            var shadow = new Color(.2f, .14f, .14f, .12f);
            var offset = new Vector2(2, -3);
            for (int i = 0; i < edge.Length; i++)
                pen.Triangle(r.center + offset, edge[i] + offset, edge[(i + 1) % edge.Length] + offset, shadow);
            var light = color; light.a *= .35f;
            for (int i = 0; i < edge.Length; i++)
            {
                var a = edge[i]; var b = edge[(i + 1) % edge.Length];
                pen.Triangle(r.center, a, b, WorkshopUi.Paper);
                Stroke(a, b, 2.15f, color, i);
                var innerA = a + (r.center - a).normalized * 3.2f;
                var innerB = b + (r.center - b).normalized * 3.2f;
                Stroke(innerA, innerB, .8f, light, i + 5);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = r.xMin + 13 + i * 5.5f;
                pen.Line(new Vector2(x, r.yMax - 13), new Vector2(x + 6, r.yMax - 20 - i * .18f), .75f, light);
                x = r.xMax - 78 + i * 5.5f;
                pen.Line(new Vector2(x, r.yMin + 13), new Vector2(x + 6, r.yMin + 20), .75f, light);
            }
            pen.CopyTo(target, Vector2.zero, 1);
        }

        void Stroke(Vector2 from, Vector2 to, float width, Color ink, int phase)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / 20));
            var normal = new Vector2(-(to - from).y, (to - from).x).normalized;
            var previous = from;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                var next = Vector2.Lerp(from, to, t) + normal * (Mathf.Sin(i * 2.1f + phase) * Mathf.Sin(t * Mathf.PI) * .4f);
                pen.Line(previous, next, width, ink); previous = next;
            }
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
