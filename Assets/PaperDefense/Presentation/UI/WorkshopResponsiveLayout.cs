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
        int lastSignature;
        int visiblePage = -1;
        bool stencilWasOpen;
        readonly Dictionary<string, RectTransform> elements = new Dictionary<string, RectTransform>();
        readonly Dictionary<RectTransform, Vector2> sheetSizes = new Dictionary<RectTransform, Vector2>();
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
        void LateUpdate()
        {
            if (board == null) return;
            int signature = Screen.width * 397 ^ Screen.height ^ Mathf.RoundToInt((player == null ? 1 : player.InterfaceScale) * 100);
            signature = unchecked(signature * 31 + (wizard == null ? 0 : wizard.Step));
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

        public void Apply()
        {
            elements.Clear();
            foreach (var item in board.GetComponentsInChildren<RectTransform>(true)) if (!elements.ContainsKey(item.name)) elements[item.name] = item;
            var layout = new PlayerLayout(Screen.width, Screen.height, player == null ? 1 : player.InterfaceScale);
            float fullWidth = layout.Width, w = Mathf.Min(1600, fullWidth), h = layout.Height;
            float offset = (fullWidth - w) / 2;
            scaler.referenceResolution = new Vector2(fullWidth, h); board.sizeDelta = new Vector2(fullWidth, h);
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
            for (int i = 0; i < 4; i++)
            {
                float stepW = (w - 84) / 4;
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
            // Keep the drawing container as the owner so Hide/Open and input blocking keep their semantics.
            var drawing = Find("Guided drawing");
            var catalogue = Find("Guided drawing stencil catalogue");
            bool stencilOpen = drawing != null && drawing.gameObject.activeSelf && catalogue != null && catalogue.gameObject.activeSelf;
            if (drawing != null)
            {
                drawing.SetParent(content, false); WorkshopUi.Place(drawing, new Rect(0, 0, bodyW, extent));
                float drawingScale = bodyH >= 400 ? Mathf.Min(mainScale, bodyH / 450) : mainScale;
                ScalePanel("Guided drawing work area", new Vector2(840, 780), drawingScale, 0, 0);
                ScalePanel("Guided drawing stencil catalogue", new Vector2(840, 780), mainScale, 0, 0);
                wizard?.Drawing.Reflow(bodyH / drawingScale);
                var workArea = Find("Guided drawing work area");
                float workHeight = workArea.rect.height * drawingScale;
                if (stencilOpen) workHeight = Mathf.Max(workHeight, catalogue.rect.height * mainScale);
                float summaryY = sideBySide ? 0 : workHeight + 20;
                ScalePanel("Guided drawing summary", new Vector2(400, 780), summaryScale, sideBySide ? mainW + 20 : 0, summaryY);
                float drawingExtent = Mathf.Max(workHeight, summaryY + 780 * summaryScale);
                drawing.sizeDelta = new Vector2(bodyW, drawingExtent);
                if (drawing.gameObject.activeSelf) extent = drawingExtent;
            }
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
