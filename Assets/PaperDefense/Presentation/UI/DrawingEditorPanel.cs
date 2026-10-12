using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Workshop-only editor. Commands edit a private working drawing, never an album page.</summary>
    public sealed class DrawingEditorPanel
    {
        readonly GameObject shade, panel;
        readonly Action<DrawingSnapshot> apply;
        readonly Text message, rejectionMessage, partLabel;
        readonly Dictionary<DrawingPartKey, Button> parts = new Dictionary<DrawingPartKey, Button>();
        public DrawingEditSession Session { get; private set; }
        public DrawingCanvasGraphic Canvas { get; }
        public bool IsOpen => panel.activeSelf;

        public DrawingEditorPanel(Transform parent, Font font, Action<DrawingSnapshot> apply)
        {
            this.apply = apply;
            shade = Box(parent, "Drawing editor shade", new Rect(0, 0, 1600, 1000), new Color(.12f, .18f, .28f, .4f)).gameObject;
            panel = Box(parent, "Authored drawing editor", new Rect(90, 60, 1420, 880), NotebookPalette.Paper).gameObject;
            Label(panel.transform, font, "НАРИСУЙ СВОЮ БАШНЮ", new Rect(28, 16, 1300, 48), 32, NotebookPalette.Blue);
            Label(panel.transform, font, "Выбери часть. Голубой контур — образец; тёмные линии — твой рисунок. Каждый ствол хранится отдельно.", new Rect(30, 68, 1350, 50), 20, NotebookPalette.Navy);
            var sheet = Box(panel.transform, "Drawing viewport", new Rect(285, 130, 1095, 574), new Color(1, .99f, .93f));
            sheet.gameObject.AddComponent<RectMask2D>();
            var go = new GameObject("Drawing canvas", typeof(RectTransform), typeof(CanvasRenderer), typeof(DrawingCanvasGraphic));
            go.transform.SetParent(sheet, false); Place(go.GetComponent<RectTransform>(), new Rect(0, 0, 1095, 574));
            Canvas = go.GetComponent<DrawingCanvasGraphic>(); Canvas.raycastTarget = true;
            Canvas.Changed += Refresh;
            Canvas.Edited += ClearRejection;
            Canvas.Rejected += reason => rejectionMessage.text = reason;
            var keys = new[] { DrawingPartKey.Base, DrawingPartKey.Mount0, DrawingPartKey.Mount1,
                DrawingPartKey.Armor, DrawingPartKey.Feed, DrawingPartKey.Tracking, DrawingPartKey.Decoration, DrawingPartKey.Rail };
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                parts[key] = Button(panel.transform, font, "Draw part " + key, new Rect(28, 132 + i * 44, 231, 38), Name(key), () =>
                { Canvas.FinishStroke(); Canvas.SelectedPart = key; Refresh(); });
            }
            partLabel = Label(panel.transform, font, "", new Rect(28, 507, 234, 60), 18, NotebookPalette.Blue);
            Button(panel.transform, font, "Drawing pencil", new Rect(28, 575, 112, 44), "КАРАНДАШ", () => { Canvas.Eraser = false; Refresh(); });
            Button(panel.transform, font, "Drawing eraser", new Rect(147, 575, 112, 44), "ЛАСТИК", () => { Canvas.Eraser = true; Refresh(); });
            Button(panel.transform, font, "Drawing undo", new Rect(28, 631, 112, 44), "↶ НАЗАД", () => { Canvas.FinishStroke(); if (Session.Undo()) ClearRejection(); Refresh(); });
            Button(panel.transform, font, "Drawing redo", new Rect(147, 631, 112, 44), "ВПЕРЁД ↷", () => { Canvas.FinishStroke(); if (Session.Redo()) ClearRejection(); Refresh(); });
            Button(panel.transform, font, "Clear drawing part", new Rect(28, 689, 231, 44), "ОЧИСТИТЬ ЧАСТЬ", () => { Canvas.FinishStroke(); if (Session.ClearPart(Canvas.SelectedPart)) ClearRejection(); Refresh(); });
            Button(panel.transform, font, "Drawing zoom out", new Rect(291, 760, 160, 40), "− МАСШТАБ", () => Canvas.Zoom *= .8f);
            Button(panel.transform, font, "Drawing zoom in", new Rect(460, 760, 160, 40), "+ МАСШТАБ", () => Canvas.Zoom *= 1.25f);
            rejectionMessage = Label(panel.transform, font, "", new Rect(291, 710, 1089, 43), 17, NotebookPalette.Red);
            message = Label(panel.transform, font, "", new Rect(645, 759, 722, 43), 17, NotebookPalette.Navy);
            Button(panel.transform, font, "Cancel drawing", new Rect(30, 813, 450, 47), "ОТМЕНИТЬ ИЗМЕНЕНИЯ", () => Close(false));
            Button(panel.transform, font, "Apply drawing", new Rect(510, 813, 870, 47), "ПРИМЕНИТЬ К ЧЕРНОВИКУ  →", () => Close(true));
            Close(false);
        }
        public void Open(TowerBlueprint blueprint)
        {
            ClearRejection();
            Session = new DrawingEditSession(blueprint, blueprint.Drawing);
            Canvas.Bind(Session); Canvas.SelectedPart = DrawingPartKey.Base; Canvas.Zoom = 1;
            shade.SetActive(true); panel.SetActive(true); Refresh();
        }
        public void Close(bool commit)
        {
            if (commit && Session != null) { Canvas.FinishStroke(); apply(Session.Snapshot); }
            shade.SetActive(false); panel.SetActive(false);
        }
        void ClearRejection() => rejectionMessage.text = "";
        void Refresh()
        {
            if (Session == null) return;
            var enabled = DrawingTemplateCatalog.EnabledKeys(Session.Blueprint);
            foreach (var pair in parts)
            {
                bool present = pair.Key == DrawingPartKey.Decoration;
                foreach (var key in enabled) if (key == pair.Key) present = true;
                pair.Value.interactable = present;
                pair.Value.GetComponent<Image>().color = Canvas.SelectedPart == pair.Key ? new Color(.78f, .87f, .96f) : Color.white;
            }
            partLabel.text = Name(Canvas.SelectedPart) + "\n" + (Canvas.Eraser ? "Ластик: удаляет штрих" : "Карандаш: ЛКМ и движение");
            var assessment = Session.Blueprint.WithDrawing(Session.Snapshot).Assessment;
            string feedback = "Украшения не участвуют в оценке.";
            foreach (var part in assessment.PartFeedback) if (part.Part == Canvas.SelectedPart) feedback = part.Message;
            message.text = "Качество " + assessment.Quality.ToString("0") + "/100 · " + Session.Snapshot.PointCount + "/" + DrawingSnapshot.MaxPoints + " точек\n" + feedback;
            Canvas.SetVerticesDirty();
        }
        public static string Name(DrawingPartKey part)
        {
            switch (part)
            {
                case DrawingPartKey.Base: return "ОСНОВАНИЕ";
                case DrawingPartKey.Mount0: return "СТВОЛ 1";
                case DrawingPartKey.Mount1: return "СТВОЛ 2";
                case DrawingPartKey.Armor: return "БРОНЕПЛАСТИНЫ";
                case DrawingPartKey.Feed: return "ПОДАЧА";
                case DrawingPartKey.Tracking: return "НАВЕДЕНИЕ";
                case DrawingPartKey.Rail: return "НАПРАВЛЯЮЩАЯ";
                default: return "УКРАШЕНИЯ";
            }
        }
        static RectTransform Box(Transform parent, string name, Rect box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); Place(rect, box); var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = true; return rect;
        }
        static Text Label(Transform parent, Font font, string value, Rect box, int size, Color color)
        {
            var go = new GameObject("Drawing label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), box);
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.raycastTarget = false;
            text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        static Button Button(Transform parent, Font font, string name, Rect box, string caption, Action action)
        {
            var rect = Box(parent, name, box, Color.white); var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => action());
            var text = Label(rect, font, caption, new Rect(4, 2, box.width - 8, box.height - 4), 16, NotebookPalette.Navy); text.alignment = TextAnchor.MiddleCenter; return button;
        }
        static void Place(RectTransform rect, Rect box)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(box.x, -box.y); rect.sizeDelta = box.size; }
    }

    /// <summary>One Graphic and one event receiver for the entire drawing, independent of stroke count.</summary>
    public sealed class DrawingCanvasGraphic : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        DrawingEditSession session;
        readonly List<DrawingPoint> pending = new List<DrawingPoint>();
        DrawingPartKey selected;
        float zoom = 1;
        public event Action Changed;
        /// <summary>A committed edit, distinct from redraws and rejected or empty input.</summary>
        public event Action Edited;
        public event Action<string> Rejected;
        public bool Eraser { get; set; }
        public DrawingPartKey SelectedPart { get => selected; set { selected = value; SetVerticesDirty(); } }
        public float Zoom { get => zoom; set { zoom = Mathf.Clamp(value, .6f, 2.5f); SetVerticesDirty(); } }
        float Scale => Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .68f * zoom;
        public void Bind(DrawingEditSession value) { session = value; pending.Clear(); Eraser = false; SetVerticesDirty(); }
        public Vector2 LocalPointToScreen(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, rectTransform.TransformPoint(rectTransform.rect.center + local * Scale));
        bool Point(PointerEventData e, out DrawingPoint point)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, e.position, e.pressEventCamera))
            { point = default(DrawingPoint); return false; }
            Vector2 local;
            bool found = RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out local);
            local = (local - rectTransform.rect.center) / Mathf.Max(1, Scale);
            if (!found || Mathf.Abs(local.x) > 1 || Mathf.Abs(local.y) > 1) { point = default(DrawingPoint); return false; }
            point = new DrawingPoint(local.x, local.y);
            return true;
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (session == null || e.button != PointerEventData.InputButton.Left) return;
            pending.Clear(); AddPoint(e);
        }
        public void OnDrag(PointerEventData e) { if (session != null && e.button == PointerEventData.InputButton.Left) AddPoint(e); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) FinishStroke(); }
        public void OnScroll(PointerEventData e) { FinishStroke(); Zoom *= e.scrollDelta.y > 0 ? 1.12f : .89f; }
        void AddPoint(PointerEventData e)
        {
            DrawingPoint point;
            if (!Point(e, out point))
            { if (pending.Count > 0) FinishStroke(); return; }
            if (Eraser) { if (session.Erase(selected, point, .045f / zoom)) Edited?.Invoke(); Changed?.Invoke(); return; }
            if (pending.Count >= DrawingSnapshot.MaxPoints) { Rejected?.Invoke("Лимит точек штриха. Закончи его и удали лишнее."); return; }
            if (pending.Count > 0)
            { var last = pending[pending.Count - 1]; float dx = last.X - point.X, dy = last.Y - point.Y; if (dx * dx + dy * dy < .000016f) return; }
            pending.Add(point); SetVerticesDirty();
        }
        public void FinishStroke()
        {
            if (session != null && pending.Count >= 2)
            { string reason; if (session.AddStroke(selected, pending, out reason)) Edited?.Invoke(); else Rejected?.Invoke(reason); }
            pending.Clear(); Changed?.Invoke(); SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (session == null) return;
            Vector2 center = rectTransform.rect.center; float scale = Scale;
            Point2 activePivot = DrawingTemplateCatalog.Pivot(session.Blueprint, selected);
            foreach (var template in DrawingTemplateCatalog.Create(session.Blueprint))
            {
                Vector2 offset = new Vector2(template.Pivot.X - activePivot.X, template.Pivot.Y - activePivot.Y);
                Color color = template.Key == selected ? new Color(.28f, .58f, .85f, .33f) : new Color(.45f, .63f, .78f, .12f);
                foreach (var stroke in template.Contours) Draw(vh, stroke.Points, center + offset * scale, scale, color, .009f);
            }
            foreach (var part in session.Snapshot.Parts)
            {
                if (!DrawingTemplateCatalog.IsEnabled(session.Blueprint, part.Key)) continue;
                Point2 pivot = DrawingTemplateCatalog.Pivot(session.Blueprint, part.Key);
                Vector2 offset = new Vector2(pivot.X - activePivot.X, pivot.Y - activePivot.Y);
                foreach (var stroke in part.Strokes) Draw(vh, stroke.Points, center + offset * scale, scale,
                    part.Key == selected ? NotebookPalette.Navy : new Color(.22f, .35f, .5f, .35f), .018f);
            }
            Draw(vh, pending, center, scale, NotebookPalette.Navy, .018f);
            BlueprintSketch.Line(vh, center + Vector2.left * 7, center + Vector2.right * 7, 1, new Color(.3f, .5f, .7f, .5f));
            BlueprintSketch.Line(vh, center + Vector2.down * 7, center + Vector2.up * 7, 1, new Color(.3f, .5f, .7f, .5f));
        }
        internal static void Draw(VertexHelper vh, IReadOnlyList<DrawingPoint> points, Vector2 center, float scale, Color color, float width)
        {
            for (int i = 1; i < points.Count; i++)
                BlueprintSketch.Line(vh, center + new Vector2(points[i - 1].X, points[i - 1].Y) * scale,
                    center + new Vector2(points[i].X, points[i].Y) * scale, Mathf.Max(1, width * scale), color);
        }
    }
}
