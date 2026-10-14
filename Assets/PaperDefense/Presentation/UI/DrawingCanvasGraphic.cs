using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public enum DrawingTool { Pencil, Line, Rectangle, Ellipse, Eraser, Move }

    /// <summary>One Graphic and one event receiver. Pending gestures never mutate the saved snapshot.</summary>
    public sealed class DrawingCanvasGraphic : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        public const float GridSpacing = .1f;
        DrawingEditSession session;
        readonly List<DrawingPoint> pending = new List<DrawingPoint>();
        readonly List<DrawingPoint> shape = new List<DrawingPoint>(33);
        string selected = DrawingBindings.Base;
        DrawingTool tool;
        float zoom = 1;
        bool grid = true, snap, drawing, panning;
        int pointerId;
        Vector2 pan, panStart, pointerStart;
        InputSettings inputSettings;
        public event Action Changed;
        public event Action Edited;
        public event Action<string> Rejected;
        public event Action EscapeRequested;
        public bool HasPendingGesture => drawing || panning;
        public DrawingTool Tool { get => tool; set { if (tool == value) return; CancelGesture(); tool = value; Changed?.Invoke(); } }
        public bool Eraser { get => Tool == DrawingTool.Eraser; set => Tool = value ? DrawingTool.Eraser : DrawingTool.Pencil; }
        public bool ShowGrid { get => grid; set { grid = value; SetVerticesDirty(); Changed?.Invoke(); } }
        public bool SnapToGrid { get => snap; set { CancelGesture(); snap = value; Changed?.Invoke(); } }
        public DrawingPartKey SelectedPart { get { DrawingBindings.TryLegacy(selected, out var key); return key; } set => SelectedBinding = DrawingBindings.For(value); }
        public string SelectedBinding
        {
            get => selected;
            set { DrawingBindings.Validate(value); if (selected == value) return; CancelGesture(); selected = value; Changed?.Invoke(); SetVerticesDirty(); }
        }
        public float Zoom
        {
            get => zoom;
            set { CancelGesture(); zoom = Mathf.Clamp(value, .6f, 3f); SetVerticesDirty(); Changed?.Invoke(); }
        }
        public Vector2 Pan => pan;
        float Scale => Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .44f * zoom;
        Vector2 Center => rectTransform.rect.center + pan;

        public void Bind(DrawingEditSession value)
        { CancelGesture(); session = value; tool = DrawingTool.Pencil; selected = DrawingBindings.Base; zoom = 1; pan = Vector2.zero; SetVerticesDirty(); }
        public void Fit() { CancelGesture(); zoom = 1; pan = Vector2.zero; SetVerticesDirty(); Changed?.Invoke(); }
        public Vector2 LocalPointToScreen(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, rectTransform.TransformPoint(Center + local * Scale));

        protected override void OnEnable() { base.OnEnable(); inputSettings = PointerEventPolicy.Acquire(); }
        protected override void OnDisable() { CancelGesture(); PointerEventPolicy.Release(inputSettings); inputSettings = null; base.OnDisable(); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelGesture(); }
        void OnApplicationPause(bool paused) { if (paused) CancelGesture(); }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || session == null) return;
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused != null && (focused.GetComponentInParent<UnityEngine.UI.InputField>() != null || focused.GetComponent("TMP_InputField") != null)) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            { if (HasPendingGesture) CancelGesture(); else EscapeRequested?.Invoke(); return; }
            if (!keyboard.ctrlKey.isPressed && !keyboard.leftMetaKey.isPressed && !keyboard.rightMetaKey.isPressed) return;
            if (keyboard.yKey.wasPressedThisFrame || (keyboard.shiftKey.isPressed && keyboard.zKey.wasPressedThisFrame)) Redo();
            else if (keyboard.zKey.wasPressedThisFrame) Undo();
        }

        public void Undo() { CancelGesture(); if (session != null && session.Undo()) Edited?.Invoke(); Changed?.Invoke(); SetVerticesDirty(); }
        public void Redo() { CancelGesture(); if (session != null && session.Redo()) Edited?.Invoke(); Changed?.Invoke(); SetVerticesDirty(); }
        public void CancelGesture() { drawing = panning = false; pending.Clear(); SetVerticesDirty(); }

        bool Local(PointerEventData e, out Vector2 local) => RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out local);
        bool Point(PointerEventData e, bool allowOutside, out DrawingPoint point)
        {
            point = default;
            if (!Local(e, out var local)) return false;
            local = (local - Center) / Mathf.Max(1, Scale);
            if (Mathf.Abs(local.x) > 1 || Mathf.Abs(local.y) > 1 || (!allowOutside && !RectTransformUtility.RectangleContainsScreenPoint(rectTransform, e.position, e.pressEventCamera))) return false;
            if (snap && tool != DrawingTool.Eraser) local = new Vector2(Mathf.Round(local.x / GridSpacing) * GridSpacing, Mathf.Round(local.y / GridSpacing) * GridSpacing);
            point = new DrawingPoint(local.x, local.y); return true;
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (!isActiveAndEnabled || session == null || (e.button != PointerEventData.InputButton.Left && e.button != PointerEventData.InputButton.Middle)) return;
            CancelGesture(); pointerId = e.pointerId;
            if (e.button == PointerEventData.InputButton.Middle)
            { if (!Local(e, out pointerStart)) return; panning = true; panStart = pan; return; }
            if (!Point(e, false, out var point)) return;
            drawing = true; pending.Add(point); SetVerticesDirty();
        }
        public void OnDrag(PointerEventData e)
        {
            if (!isActiveAndEnabled || e.pointerId != pointerId) return;
            if (panning && e.button == PointerEventData.InputButton.Middle && Local(e, out var local))
            {
                pan = panStart + local - pointerStart;
                pan.x = Mathf.Clamp(pan.x, -rectTransform.rect.width, rectTransform.rect.width);
                pan.y = Mathf.Clamp(pan.y, -rectTransform.rect.height, rectTransform.rect.height);
                SetVerticesDirty(); return;
            }
            if (drawing && e.button == PointerEventData.InputButton.Left) AddPoint(e, false);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (!isActiveAndEnabled || e.pointerId != pointerId) return;
            if (panning && e.button == PointerEventData.InputButton.Middle) { OnDrag(e); panning = false; return; }
            if (!drawing || e.button != PointerEventData.InputButton.Left) return;
            AddPoint(e, true); FinishStroke();
        }
        public void OnScroll(PointerEventData e)
        { if (e.scrollDelta.y != 0) Zoom *= Mathf.Pow(1.12f, e.scrollDelta.y); }

        void AddPoint(PointerEventData e, bool final)
        {
            if (!Point(e, tool == DrawingTool.Move, out var point))
            { CancelGesture(); Rejected?.Invoke(UserText.Message("drawing.command.bounds")); return; }
            if (tool != DrawingTool.Pencil && tool != DrawingTool.Eraser)
            { if (pending.Count == 1) pending.Add(point); else pending[1] = point; SetVerticesDirty(); return; }
            var last = pending[pending.Count - 1];
            float dx = last.X - point.X, dy = last.Y - point.Y;
            if (dx * dx + dy * dy < (final ? .000000001f : .000016f)) return;
            if (pending.Count >= DrawingSnapshot.MaxPoints)
            { CancelGesture(); Rejected?.Invoke(UserText.Message("copy.2bd5dc0f361ed385")); return; }
            pending.Add(point); SetVerticesDirty();
        }

        public void FinishStroke()
        {
            if (session == null) return;
            // Existing callers also use this entry point after editing the public session directly.
            if (!drawing) { Changed?.Invoke(); SetVerticesDirty(); return; }
            bool edited = false; string reason = "";
            if (tool == DrawingTool.Eraser) edited = session.ErasePath(selected, pending, .045f / zoom, out reason);
            else if (pending.Count >= 2)
            {
                var start = pending[0]; var end = pending[pending.Count - 1];
                switch (tool)
                {
                    case DrawingTool.Pencil: edited = session.AddStroke(selected, pending, out reason); break;
                    case DrawingTool.Line: edited = session.AddLine(selected, start, end, out reason); break;
                    case DrawingTool.Rectangle: edited = session.AddRectangle(selected, start, end, out reason); break;
                    case DrawingTool.Ellipse: edited = session.AddEllipse(selected, start, end, out reason); break;
                    case DrawingTool.Move: edited = session.TranslatePart(selected, end.X - start.X, end.Y - start.Y, out reason); break;
                }
            }
            CancelGesture();
            if (edited) Edited?.Invoke(); else if (!string.IsNullOrEmpty(reason)) Rejected?.Invoke(reason);
            Changed?.Invoke();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (session == null) return;
            var center = Center; float scale = Scale;
            if (grid)
            {
                for (int n = -10; n <= 10; n++)
                {
                    float p = n * GridSpacing * scale;
                    var ink = new Color(.28f, .51f, .68f, n == 0 ? .25f : .085f);
                    Line(vh, center + new Vector2(-scale, p), center + new Vector2(scale, p), 1, ink);
                    Line(vh, center + new Vector2(p, -scale), center + new Vector2(p, scale), 1, ink);
                }
            }
            var border = new Color(.28f, .51f, .68f, .23f);
            Line(vh, center + new Vector2(-scale, -scale), center + new Vector2(scale, -scale), 1, border);
            Line(vh, center + new Vector2(-scale, scale), center + new Vector2(scale, scale), 1, border);
            Line(vh, center + new Vector2(-scale, -scale), center + new Vector2(-scale, scale), 1, border);
            Line(vh, center + new Vector2(scale, -scale), center + new Vector2(scale, scale), 1, border);
            Point2 activePivot = DrawingTemplateCatalog.Pivot(session.Blueprint, selected);
            Vector2 move = Vector2.zero;
            bool moving = drawing && tool == DrawingTool.Move && pending.Count > 1;
            if (moving) move = new Vector2(pending[1].X - pending[0].X, pending[1].Y - pending[0].Y);
            bool outside = false;
            if (moving) foreach (var stroke in session.Snapshot.GetPart(selected).Strokes) foreach (var p in stroke.Points)
                if (Mathf.Abs(p.X + move.x) > 1 || Mathf.Abs(p.Y + move.y) > 1) outside = true;
            foreach (var part in session.Snapshot.Parts)
            {
                if (!DrawingTemplateCatalog.IsEnabled(session.Blueprint, part.BindingId)) continue;
                Point2 pivot = DrawingTemplateCatalog.Pivot(session.Blueprint, part.BindingId);
                Vector2 offset = new Vector2(pivot.X - activePivot.X, pivot.Y - activePivot.Y);
                bool active = part.BindingId == selected;
                foreach (var stroke in part.Strokes)
                {
                    if (active && moving) Draw(vh, stroke.Points, center + offset * scale, scale, new Color(.2f, .35f, .5f, .18f), .009f, MinimumStrokeWidth(canvas));
                    Draw(vh, stroke.Points, center + (offset + (active ? move : Vector2.zero)) * scale, scale,
                        active ? (moving ? (outside ? NotebookPalette.Red : NotebookPalette.Blue) : NotebookPalette.Navy) : new Color(.22f, .35f, .5f, .24f), active ? .012f : .009f, MinimumStrokeWidth(canvas));
                }
            }
            if (drawing && !moving)
            {
                BuildShape();
                Draw(vh, shape, center, scale, tool == DrawingTool.Eraser ? new Color(.8f, .24f, .22f, .45f) : NotebookPalette.Blue, tool == DrawingTool.Eraser ? .035f / zoom : .01f, MinimumStrokeWidth(canvas));
            }
            var axis = new Color(.25f, .45f, .67f, .65f);
            Line(vh, center + Vector2.left * 8, center + Vector2.right * 8, 1.5f, axis);
            Line(vh, center + Vector2.down * 8, center + Vector2.up * 8, 1.5f, axis);
            if (DrawingBindings.TryMount(selected, out _))
            {
                var tip = center + Vector2.up * 38;
                Line(vh, center, tip, 1.5f, NotebookPalette.Blue);
                Line(vh, tip, tip + new Vector2(-5, -8), 1.5f, NotebookPalette.Blue);
                Line(vh, tip, tip + new Vector2(5, -8), 1.5f, NotebookPalette.Blue);
            }
        }
        void BuildShape()
        {
            shape.Clear(); if (pending.Count == 0) return;
            var a = pending[0]; var b = pending[pending.Count - 1];
            if (tool == DrawingTool.Rectangle)
            { shape.Add(a); shape.Add(new DrawingPoint(b.X, a.Y)); shape.Add(b); shape.Add(new DrawingPoint(a.X, b.Y)); shape.Add(a); }
            else if (tool == DrawingTool.Ellipse)
            {
                for (int i = 0; i <= 32; i++)
                { float angle = i * Mathf.PI / 16; shape.Add(new DrawingPoint((a.X + b.X) * .5f + Mathf.Abs(b.X - a.X) * .5f * Mathf.Cos(angle), (a.Y + b.Y) * .5f + Mathf.Abs(b.Y - a.Y) * .5f * Mathf.Sin(angle))); }
            }
            else shape.AddRange(pending);
        }
        internal static float MinimumStrokeWidth(UnityEngine.Canvas rootCanvas) => 1.25f / Mathf.Max(.1f, rootCanvas != null ? rootCanvas.scaleFactor : 1);
        void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color ink)
            => BlueprintSketch.Line(vh, a, b, Mathf.Max(width, MinimumStrokeWidth(canvas)), ink);
        internal static void Draw(VertexHelper vh, IReadOnlyList<DrawingPoint> points, Vector2 center, float scale, Color color, float width, float minimumWidth = 1)
        {
            for (int i = 1; i < points.Count; i++)
                BlueprintSketch.Line(vh, center + new Vector2(points[i - 1].X, points[i - 1].Y) * scale,
                    center + new Vector2(points[i].X, points[i].Y) * scale, Mathf.Max(minimumWidth, width * scale), color);
        }
    }
}
