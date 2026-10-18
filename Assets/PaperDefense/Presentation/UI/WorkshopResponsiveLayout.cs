using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Reflows the existing editor views without replacing their draft or command session.</summary>
    public sealed class WorkshopResponsiveLayout : MonoBehaviour
    {
        RectTransform board, viewport, content;
        CanvasScaler scaler;
        PlayerGameRoot player;
        WorkshopWizard wizard;
        CompositeWorkshopPanel composite;
        int lastSignature;
        int visiblePage = -1;
        bool stencilWasOpen;
        bool foundationHeaderApplied;
        WizardWeaponHeader weaponHeader;
        readonly Dictionary<string, RectTransform> elements = new Dictionary<string, RectTransform>();
        readonly Dictionary<RectTransform, Vector2> sheetSizes = new Dictionary<RectTransform, Vector2>();
        readonly Dictionary<Graphic, HeaderGraphicState> headerStyles = new Dictionary<Graphic, HeaderGraphicState>();
        readonly List<FoundationDetailGraphic> headerDetails = new List<FoundationDetailGraphic>();
        public void Initialize(RectTransform root, PlayerGameRoot owner)
        {
            board = root; player = owner; scaler = GetComponent<CanvasScaler>();
            wizard = player == null ? null : player.GetComponentInChildren<WorkshopHud>(true)?.Guided;
            var ui = new PlayerUi();
            content = ui.Scroll(board, "Workshop responsive scroll", new Rect(210, 110, 1366, 780), 780);
            viewport = (RectTransform)content.parent;
            viewport.SetSiblingIndex(1);
            board.GetComponent<Image>().color = PlayerUi.Paper;
            Apply();
        }
        public void InitializeComposite(RectTransform root, CompositeWorkshopPanel panel)
        {
            board = root; composite = panel; scaler = GetComponent<CanvasScaler>();
            Apply();
        }
        void LateUpdate()
        {
            if (board == null) return;
            int signature = Screen.width * 397 ^ Screen.height;
            if (composite != null)
            {
                if (signature != lastSignature) { lastSignature = signature; Apply(); }
                return;
            }
            signature = unchecked(signature * 31 + (wizard == null ? 0 : wizard.Step));
            signature = unchecked(signature * 31 + (wizard == null ? 0 : wizard.Status.GetHashCode()));
            foreach (Transform child in board) signature = unchecked(signature * 31 + child.GetEntityId().GetHashCode() + (child.gameObject.activeSelf ? 1 : 0));
            foreach (Transform child in content) signature = unchecked(signature * 31 + child.GetEntityId().GetHashCode() + (child.gameObject.activeSelf ? 1 : 0));
            var stencil = Find("Guided drawing stencil catalogue");
            signature = unchecked(signature * 31 + (stencil != null && stencil.gameObject.activeSelf ? 1 : 0));
            if (signature == lastSignature) return;
            lastSignature = signature; Apply();
        }
        RectTransform Find(string name) => elements.TryGetValue(name, out var item) ? item : null;
        void Place(string name, Rect rect)
        { var item = Find(name); if (item != null) WorkshopUi.Place(item, rect); }
        void Visible(string name, bool visible)
        { var item = Find(name); if (item != null && item.gameObject.activeSelf != visible) item.gameObject.SetActive(visible); }

        public void Apply() => Apply(Screen.width, Screen.height);

        public void Apply(int width, int height)
        {
            if (composite != null)
            {
                var viewportLayout = new PlayerLayout(width, height);
                scaler.referenceResolution = new Vector2(viewportLayout.Width, viewportLayout.Height);
                board.sizeDelta = scaler.referenceResolution;
                composite.Reflow(viewportLayout.Width, viewportLayout.Height);
                return;
            }
            elements.Clear();
            foreach (var item in board.GetComponentsInChildren<RectTransform>(true)) if (!elements.ContainsKey(item.name)) elements[item.name] = item;
            var layout = new PlayerLayout(width, height);
            bool weapons = wizard != null && wizard.Step == 1;
            float pageScale = weapons ? Mathf.Max(1, 1024 / layout.Width, 850 / layout.Height) : 1;
            float fullWidth = layout.Width * pageScale;
            float w = Mathf.Min(1600, fullWidth), h = layout.Height * (fullWidth / layout.Width);
            float offset = (fullWidth - w) / 2;
            scaler.referenceResolution = new Vector2(fullWidth, h); board.sizeDelta = new Vector2(fullWidth, h);
            bool foundation = wizard != null && wizard.Step == 0;
            if (!weapons) weaponHeader?.Restore(wizard == null ? 0 : wizard.Step);
            viewport.gameObject.SetActive(!foundation && !weapons);
            Visible("Wizard back", true);
            Visible("Wizard navigation", !foundation && !weapons);
            if (foundation)
            {
                Foundation(w, h, offset, fullWidth);
                var hiddenScroll = viewport.GetComponent<ScrollRect>();
                hiddenScroll.StopMovement(); hiddenScroll.verticalNormalizedPosition = 1;
                visiblePage = 0; stencilWasOpen = false;
                AdaptSheets(fullWidth, h);
                return;
            }
            RestoreFoundationHeader();
            Visible("Wizard title", true);
            if (weapons)
            {
                bool compact = w < 1350;
                float top = compact ? 136 : 142, weaponBodyHeight = h - top - 82;
                Place("Guided workshop", new Rect(0, 0, fullWidth, h));
                Visible("Wizard assembled thumbnail", false); Visible("Wizard thumbnail caption", false);
                Visible("Wizard summary", false);
                for (int i = 0; i < 3; i++) Visible("Wizard connector " + i, false);
                if (weaponHeader == null) weaponHeader = new WizardWeaponHeader(board, Find);
                weaponHeader.Apply(w, h, offset, compact);
                wizard.ReflowWeapons(w - 48, weaponBodyHeight);
                Place("Wizard weapons", new Rect(offset + 24, top, w - 48, weaponBodyHeight));
                var hiddenScroll = viewport.GetComponent<ScrollRect>();
                hiddenScroll.StopMovement(); hiddenScroll.verticalNormalizedPosition = 1;
                visiblePage = 1; stencilWasOpen = false;
                AdaptSheets(fullWidth, h);
                foreach (var control in board.GetComponentsInChildren<Selectable>(true))
                    if (control.GetComponent<PlayerTooltip>() == null && control.GetComponent<WorkshopTooltip>() == null && control.GetComponent<UiScrollFocus>() == null)
                        control.gameObject.AddComponent<UiScrollFocus>();
                return;
            }
            bool verticalSteps = w >= 1450;
            bool shortPage = h < 850;
            float navigationY = shortPage ? 76 : 88;
            float x = verticalSteps ? 216 : 24, y = verticalSteps ? 108 : shortPage ? 136 : 152;
            float bodyW = w - x - 24, mainW, summaryW;
            bool sideBySide = bodyW >= 1120;
            summaryW = sideBySide ? 300 : Mathf.Min(400, bodyW);
            mainW = sideBySide ? bodyW - summaryW - 20 : bodyW;
            float mainScale = Mathf.Min(1.12f, mainW / 840), summaryScale = summaryW / 400;
            float bodyH = Mathf.Max(180, h - y - (shortPage ? 104 : 116));
            WorkshopUi.Place(viewport, new Rect(x + offset, y, bodyW, bodyH));
            // The summary follows the editor on compact windows; both remain reachable by scrolling.
            float extent = sideBySide ? Mathf.Max(780 * mainScale, 782 * summaryScale) : 780 * mainScale + 20 + 782 * summaryScale;
            Place("Guided workshop", new Rect(0, 0, fullWidth, h));
            bool narrow = w < 900;
            Place("Wizard title", new Rect(24, 16, narrow ? w - 340 : w - 550, 64));
            Find("Wizard title").GetComponent<Text>().fontSize = narrow ? 32 : 40;
            Place("Wizard gold", new Rect(w - (narrow ? 306 : 470), 24, narrow ? 130 : 200, 40));
            Place("Wizard album", new Rect(w - (narrow ? 166 : 250), 24, narrow ? 142 : 226, 48));
            Place("Wizard navigation", verticalSteps ? new Rect(24, 108, 172, h - 224) : new Rect(24, navigationY, w - 48, 56));
            for (int i = 0; i < WorkshopWizard.StepCount; i++)
            {
                float stepW = (w - 76) / WorkshopWizard.StepCount;
                Place("Wizard step " + i, verticalSteps ? new Rect(32, 148 + i * 100, 156, 54) : new Rect(30 + i * (stepW + 8), navigationY + 4, stepW, 48));
                Place("Wizard progress " + i, new Rect(40, 112 + i * 100, 30, 30));
                Place("Wizard completed " + i, new Rect(40, 116 + i * 100, 24, 24));
                // Numbers are redundant in the horizontal navigation, whose order is explicit.
                var number = Find("Wizard progress " + i); if (number != null) number.GetComponent<Text>().enabled = verticalSteps;
                var check = Find("Wizard completed " + i); if (check != null) check.GetComponent<WorkshopIconGraphic>().enabled = verticalSteps;
                Visible("Wizard connector " + i, false);
            }
            Visible("Wizard assembled thumbnail", verticalSteps && h > 820);
            Visible("Wizard thumbnail caption", verticalSteps && h > 820);
            Place("Wizard assembled thumbnail", new Rect(44, 580, 130, 145));
            Place("Wizard thumbnail caption", new Rect(32, 736, 156, 34));
            float backW = narrow ? 112 : 148, nextW = narrow ? Mathf.Min(270, w * .34f) : 270;
            Place("Wizard back", new Rect(24, h - 74, backW, 48));
            Place("Start run", new Rect(36 + backW, h - 74, narrow ? w - backW - nextW - 96 : 228, 48));
            Place("Wizard next", new Rect(w - 24 - nextW, h - 74, nextW, 48));
            Place("Wizard notice", new Rect(x, h - (shortPage ? 100 : 112), bodyW, shortPage ? 24 : 32));

            Arrange("Wizard assembly", new Vector2(840, 760), mainScale, 0, 0);
            Arrange("Wizard review", new Vector2(840, 760), mainScale, 0, 0);
            Arrange("Wizard summary", new Vector2(400, 782), summaryScale, sideBySide ? mainW + 20 : 0, sideBySide ? 0 : 780 * mainScale + 20);
            bool stencilOpen = false;
            content.sizeDelta = new Vector2(bodyW, Mathf.Max(bodyH, extent));
            int page = wizard == null ? 0 : wizard.Step;
            if (visiblePage != page || stencilWasOpen != stencilOpen)
            {
                var scroll = viewport.GetComponent<ScrollRect>();
                scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
            }
            visiblePage = page; stencilWasOpen = stencilOpen;
            // Keep the authored workspace together on ultrawide displays instead of separating
            // a capped drawing surface from a summary pinned at the far edge of the monitor.
            foreach (var item in elements.Values)
                if (item != null && item.parent == board && (item.name.StartsWith("Wizard ") &&
                    !item.name.EndsWith("overlay") || item.name == "Start run"))
                    item.anchoredPosition += new Vector2(offset, 0);
            AdaptSheets(fullWidth, h);
            // Input fields and embedded probe controls do not use either tooltip factory.
            foreach (var control in board.GetComponentsInChildren<Selectable>(true))
                if (control.GetComponent<PlayerTooltip>() == null && control.GetComponent<WorkshopTooltip>() == null && control.GetComponent<UiScrollFocus>() == null)
                    control.gameObject.AddComponent<UiScrollFocus>();
            foreach (var scroll in board.GetComponentsInChildren<ScrollRect>(true)) scroll.inertia = player == null || !player.ReducedMotion;
        }
        void Arrange(string name, Vector2 size, float scale, float x, float y)
        { var item = Find(name); if (item == null) return; if (item.parent != content) item.SetParent(content, false); Fit(item, size, scale, x, y); }
        void ScalePanel(string name, Vector2 size, float scale, float x, float y)
        { var item = Find(name); if (item != null) Fit(item, size, scale, x, y); }
        static void Fit(RectTransform item, Vector2 size, float scale, float x, float y)
        { WorkshopUi.Place(item, new Rect(x, y, size.x, size.y)); item.localScale = Vector3.one * scale; }

        void Foundation(float w, float h, float offset, float fullWidth)
        {
            bool compact = w < 1350;
            float top = compact ? 130 : 106, bodyH = h - top - (compact ? 84 : 104);
            Place("Guided workshop", new Rect(0, 0, fullWidth, h));
            FoundationHeader(w, offset, compact);
            Visible("Wizard navigation", false);
            Visible("Wizard assembled thumbnail", false); Visible("Wizard thumbnail caption", false);
            wizard.ReflowFoundation(w - 48, bodyH);
            Place("Wizard foundation", new Rect(offset + 24, top, w - 48, bodyH));
            float rightW = (w - 66) * .44f, actionW = (rightW - 14) / 2;
            Place("Start run", new Rect(offset + w - 24 - rightW, h - 74, actionW, 50));
            Place("Wizard next", new Rect(offset + w - 24 - actionW, h - 74, actionW, 50));
            var hand = Resources.Load<Font>("NotebookHand");
            StyleHeaderButton("Start run", null, hand, compact ? 24 : 29);
            var nextButton = Find("Wizard next");
            var nextCaption = nextButton.Find("Caption").GetComponent<Text>();
            if (hand != null) nextCaption.font = hand;
            nextCaption.resizeTextMinSize = compact ? 20 : 24; nextCaption.resizeTextMaxSize = compact ? 26 : 31;
            nextCaption.alignment = TextAnchor.MiddleCenter;
            WorkshopUi.Place(nextCaption.rectTransform, new Rect(10, 0, actionW - 64, 50));
            WorkshopUi.Place(nextButton.Find("Icon") as RectTransform, new Rect(actionW - 46, 10, 30, 30));
            var nextFrame = HeaderDetail(nextButton, "Foundation footer primary frame", FoundationDetailGraphic.Kind.Frame);
            WorkshopUi.Place(nextFrame.rectTransform, new Rect(0, 0, actionW, 50)); nextFrame.color = WorkshopUi.Blue; nextFrame.Fill = WorkshopUi.Blue;
            nextButton.GetComponent<WorkshopButton>().SetDecoration(nextFrame);
            float noticeW = w - 66 - rightW;
            var noticeBacking = HeaderDetail(board, "Foundation notice backing", FoundationDetailGraphic.Kind.Frame);
            WorkshopUi.Place(noticeBacking.rectTransform, new Rect(offset + 24, h - 74, noticeW, 50));
            noticeBacking.Fill = WorkshopUi.Paper; noticeBacking.color = WorkshopUi.Ink;
            noticeBacking.transform.SetSiblingIndex(Mathf.Max(2, Find("Wizard notice").GetSiblingIndex() - 1));
            noticeBacking.gameObject.SetActive(!string.IsNullOrWhiteSpace(wizard.Status));
            Place("Wizard notice", new Rect(offset + 36, h - 70, noticeW - 24, 42));
            foreach (var control in board.GetComponentsInChildren<Selectable>(true))
                if (control.GetComponent<PlayerTooltip>() == null && control.GetComponent<WorkshopTooltip>() == null && control.GetComponent<UiScrollFocus>() == null)
                    control.gameObject.AddComponent<UiScrollFocus>();
        }

        void FoundationHeader(float w, float offset, bool compact)
        {
            if (!foundationHeaderApplied)
            {
                headerStyles.Clear();
                string[] roots = { "Wizard back", "Wizard album", "Wizard gold", "Wizard progress 0", "Wizard progress 1", "Wizard progress 2",
                    "Wizard step 0", "Wizard step 1", "Wizard step 2", "Wizard completed 0", "Wizard completed 1", "Wizard completed 2", "Start run", "Wizard next" };
                foreach (string name in roots)
                {
                    var root = Find(name); if (root == null) continue;
                    foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                        if (!(graphic is FoundationDetailGraphic)) headerStyles[graphic] = new HeaderGraphicState(graphic, name == "Wizard back");
                }
                foundationHeaderApplied = true;
            }
            Visible("Wizard title", false);
            var hand = Resources.Load<Font>("NotebookHand");
            Place("Wizard back", new Rect(offset + 24, 8, 238, 52));
            StyleHeaderButton("Wizard back", "Мастерская", hand, 31);
            Place("Wizard album", new Rect(offset + w - 224, 8, 200, 52));
            StyleHeaderButton("Wizard album", null, hand, 29);

            var goldFrame = HeaderDetail(board, "Foundation header gold frame", FoundationDetailGraphic.Kind.Frame);
            WorkshopUi.Place(goldFrame.rectTransform, new Rect(offset + 286, 8, 206, 52));
            goldFrame.color = WorkshopUi.Ink; goldFrame.Fill = WorkshopUi.Paper;
            // Keep the live counter owned by WorkshopWizard.Refresh; only its presentation changes.
            var gold = Find("Wizard gold").GetComponent<Text>();
            Place("Wizard gold", new Rect(offset + 344, 11, 142, 46));
            if (hand != null) gold.font = hand;
            gold.fontSize = 28; gold.resizeTextForBestFit = true; gold.resizeTextMinSize = 21; gold.resizeTextMaxSize = 28;
            gold.alignment = TextAnchor.MiddleLeft; gold.color = WorkshopUi.Ink;
            var coins = HeaderDetail(board, "Foundation header gold illustration", FoundationDetailGraphic.Kind.GoldStack);
            WorkshopUi.Place(coins.rectTransform, new Rect(offset + 298, 15, 38, 38));
            coins.transform.SetSiblingIndex(goldFrame.transform.GetSiblingIndex() + 1);

            float stepsX = compact ? offset + Mathf.Max(24, (w - 640) * .5f) : offset + 540;
            float stepsW = compact ? Mathf.Min(640, w - 48) : 528;
            float stepW = stepsW / WorkshopWizard.StepCount, stepY = compact ? 63 : 7;
            float diameter = compact ? 32 : 44, numberSize = compact ? 22 : 31;
            // A separate thin stroke sits behind every circle, so the labels remain independent hit targets.
            var connector = HeaderDetail(board, "Foundation header progress connector", FoundationDetailGraphic.Kind.Divider);
            WorkshopUi.Place(connector.rectTransform, new Rect(stepsX + stepW * .5f, stepY + diameter * .5f - 1,
                stepW * (WorkshopWizard.StepCount - 1), 2));
            connector.color = new Color(WorkshopUi.Ink.r, WorkshopUi.Ink.g, WorkshopUi.Ink.b, .76f);
            connector.StrokeWidth = 1.2f;
            for (int i = 0; i < WorkshopWizard.StepCount; i++)
            {
                var step = Find("Wizard step " + i);
                WorkshopUi.Place(step, new Rect(stepsX + i * stepW, stepY, stepW, compact ? 63 : 77));
                var surface = step.GetComponent<WorkshopSurfaceGraphic>();
                surface.canvasRenderer.cullTransparentMesh = false;
                surface.canvasRenderer.SetAlpha(0);
                var icon = step.Find("Icon"); if (icon != null) icon.GetComponent<WorkshopIconGraphic>().enabled = false;
                var caption = step.Find("Caption").GetComponent<Text>();
                if (hand != null) caption.font = hand;
                caption.fontSize = compact ? 20 : 21; caption.resizeTextForBestFit = true;
                caption.resizeTextMinSize = 17; caption.resizeTextMaxSize = compact ? 20 : 21;
                caption.alignment = TextAnchor.MiddleCenter; caption.color = WorkshopUi.Ink;
                WorkshopUi.Place(caption.rectTransform, new Rect(0, diameter + 1, stepW, compact ? 30 : 32));
                var circle = HeaderDetail(board, "Foundation header step circle " + i, FoundationDetailGraphic.Kind.ProgressCircle);
                WorkshopUi.Place(circle.rectTransform, new Rect(stepsX + (i + .5f) * stepW - diameter * .5f, stepY, diameter, diameter));
                circle.Selected = i == 0; circle.color = WorkshopUi.Ink;
                step.GetComponent<WorkshopButton>().SetDecoration(circle);
                var number = Find("Wizard progress " + i).GetComponent<Text>();
                WorkshopUi.Place(number.rectTransform, new Rect(stepsX + (i + .5f) * stepW - diameter * .5f, stepY, diameter, diameter));
                number.enabled = true; if (hand != null) number.font = hand;
                number.fontSize = (int)numberSize; number.alignment = TextAnchor.MiddleCenter;
                number.color = i == 0 ? WorkshopUi.Paper : WorkshopUi.Ink;
                Find("Wizard completed " + i).GetComponent<WorkshopIconGraphic>().enabled = false;
                Visible("Wizard connector " + i, false);
            }
            var divider = HeaderDetail(board, "Foundation header divider", FoundationDetailGraphic.Kind.Divider);
            WorkshopUi.Place(divider.rectTransform, new Rect(offset, compact ? 127 : 87, w, 2));
            divider.color = new Color(WorkshopUi.Ink.r, WorkshopUi.Ink.g, WorkshopUi.Ink.b, .65f);
            divider.StrokeWidth = 1;
            connector.transform.SetSiblingIndex(2);
        }

        void StyleHeaderButton(string name, string text, Font hand, int fontSize)
        {
            var button = Find(name);
            button.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.cullTransparentMesh = false;
            button.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.SetAlpha(0);
            var frame = HeaderDetail(button, "Foundation header button frame", FoundationDetailGraphic.Kind.Frame);
            WorkshopUi.Place(frame.rectTransform, new Rect(0, 0, button.sizeDelta.x, button.sizeDelta.y));
            frame.color = WorkshopUi.Ink; frame.Fill = WorkshopUi.Paper;
            button.GetComponent<WorkshopButton>().SetDecoration(frame);
            var icon = button.Find("Icon").GetComponent<WorkshopIconGraphic>();
            icon.enabled = true; icon.color = WorkshopUi.Ink;
            WorkshopUi.Place(icon.rectTransform, new Rect(18, 9, 34, 34));
            var caption = button.Find("Caption").GetComponent<Text>();
            if (text != null) caption.text = text;
            if (hand != null) caption.font = hand;
            caption.fontSize = fontSize; caption.resizeTextMinSize = fontSize - 4; caption.resizeTextMaxSize = fontSize;
            caption.alignment = TextAnchor.MiddleCenter; caption.color = WorkshopUi.Ink;
            WorkshopUi.Place(caption.rectTransform, new Rect(61, 1, button.sizeDelta.x - 73, button.sizeDelta.y - 2));
        }

        FoundationDetailGraphic HeaderDetail(Transform parent, string name, FoundationDetailGraphic.Kind kind)
        {
            var child = parent.Find(name);
            var detail = child == null ? WorkshopUi.Graphic<FoundationDetailGraphic>(parent, name, new Rect(0, 0, 1, 1)) : child.GetComponent<FoundationDetailGraphic>();
            detail.Shape = kind; detail.raycastTarget = false; detail.gameObject.SetActive(true);
            if (!headerDetails.Contains(detail)) headerDetails.Add(detail);
            // Header linework stays below the existing captions, icons and progress numbers.
            detail.transform.SetAsFirstSibling();
            if (parent == board) detail.transform.SetSiblingIndex(Mathf.Min(2, board.childCount - 1));
            return detail;
        }

        void RestoreFoundationHeader()
        {
            if (!foundationHeaderApplied) return;
            foreach (var pair in headerStyles) if (pair.Key != null) pair.Value.Restore(pair.Key);
            foreach (var detail in headerDetails) if (detail != null) detail.gameObject.SetActive(false);
            foreach (string name in new[] { "Wizard back", "Wizard album", "Start run", "Wizard next", "Wizard step 0", "Wizard step 1", "Wizard step 2" })
                Find(name)?.GetComponent<WorkshopButton>()?.SetDecoration(null);
            // Refresh the Selectable colors after restoring typography: the next step may already be chosen.
            for (int i = 0; i < WorkshopWizard.StepCount; i++)
                Find("Wizard step " + i)?.GetComponent<WorkshopButton>()?.SetChosen(wizard != null && wizard.Step == i);
            Find("Wizard back")?.GetComponent<WorkshopButton>()?.SetChosen(false);
            Find("Wizard album")?.GetComponent<WorkshopButton>()?.SetChosen(false);
            foundationHeaderApplied = false;
            headerStyles.Clear();
        }

        sealed class HeaderGraphicState
        {
            readonly Vector2 anchorMin, anchorMax, pivot, position, size;
            readonly Color tint;
            readonly bool enabled, saveText, cullTransparent;
            readonly float alpha;
            readonly Font font;
            readonly int fontSize, minSize, maxSize;
            readonly bool bestFit;
            readonly TextAnchor alignment;
            readonly string text;
            public HeaderGraphicState(Graphic graphic, bool preserveText)
            {
                var rect = graphic.rectTransform;
                anchorMin = rect.anchorMin; anchorMax = rect.anchorMax; pivot = rect.pivot; position = rect.anchoredPosition; size = rect.sizeDelta;
                tint = graphic.color; enabled = graphic.enabled; alpha = graphic.canvasRenderer.GetAlpha(); cullTransparent = graphic.canvasRenderer.cullTransparentMesh;
                if (graphic is Text label)
                {
                    font = label.font; fontSize = label.fontSize; minSize = label.resizeTextMinSize; maxSize = label.resizeTextMaxSize;
                    bestFit = label.resizeTextForBestFit; alignment = label.alignment; saveText = preserveText; text = label.text;
                }
            }
            public void Restore(Graphic graphic)
            {
                var rect = graphic.rectTransform;
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
                graphic.color = tint; graphic.enabled = enabled; graphic.canvasRenderer.cullTransparentMesh = cullTransparent; graphic.canvasRenderer.SetAlpha(alpha);
                if (graphic is Text label)
                {
                    label.font = font; label.fontSize = fontSize; label.resizeTextMinSize = minSize; label.resizeTextMaxSize = maxSize;
                    label.resizeTextForBestFit = bestFit; label.alignment = alignment; if (saveText) label.text = text;
                }
            }
        }

        void AdaptSheets(float w, float h)
        {
            string[] overlays = { "Wizard album overlay", "Wizard trial overlay", "Wizard additions overlay", "Workshop confirmation overlay" };
            foreach (string name in overlays)
            {
                var overlay = Find(name); if (overlay == null) continue;
                WorkshopUi.Place(overlay, new Rect(0, 0, w, h));
                var sheet = overlay.Cast<Transform>().OfType<RectTransform>().FirstOrDefault();
                if (sheet != null)
                {
                    if (name == "Wizard album overlay" || name == "Wizard trial overlay")
                    {
                        var responsive = sheet.GetComponent<WorkshopOverlayLayout>() ?? sheet.gameObject.AddComponent<WorkshopOverlayLayout>();
                        responsive.Reflow(w, h);
                    }
                    else FitSheet(sheet, w, h);
                }
            }
            string[] specialists = { "Mortar footprint sheet", "Footprint study sheet", "Fan volley sheet", "Reinforcement sheet" };
            foreach (string name in specialists)
            {
                var sheet = Find(name); if (sheet == null) continue;
                var responsive = sheet.GetComponent<WorkshopSheetLayout>() ?? sheet.gameObject.AddComponent<WorkshopSheetLayout>();
                responsive.Reflow(w, h);
            }
        }
        void FitSheet(RectTransform sheet, float w, float h)
        {
            if (!sheetSizes.TryGetValue(sheet, out var size)) { size = sheet.sizeDelta; sheetSizes[sheet] = size; }
            float scale = Mathf.Min(1, (w - 32) / size.x, (h - 32) / size.y);
            Fit(sheet, size, scale, (w - size.x * scale) / 2, (h - size.y * scale) / 2);
        }
    }
}
