using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PaperDefense.Presentation
{
    public enum DrawingMode { Build, Erase, Upgrade }

    public sealed class DrawingInput : MonoBehaviour
    {
        public StructureKind Tool { get; private set; } = StructureKind.Wall;
        public MaterialTier Tier { get; private set; } = MaterialTier.Pencil;
        public DrawingMode Mode { get; private set; }
        public string Message { get; private set; } = "Зажми ЛКМ и нарисуй первую стену. Отпусти — начнётся стройка.";
        public int PreviewCost { get; private set; }
        public string PreviewReason { get; private set; } = "";
        public Cell HoverCell { get; private set; }
        public bool HasHover { get; private set; }
        public bool ShowRanges { get; private set; }
        public float Zoom { get; private set; } = 1;
        public Vector2 CameraCenter { get; private set; }
        public event Action SelectionChanged;
        RunController run;
        NotebookWorldView view;
        Camera worldCamera;
        readonly List<Cell> stroke = new List<Cell>();
        readonly HashSet<Cell> visited = new HashSet<Cell>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        readonly Cell[] hoverPreview = new Cell[1];
        EventSystem pointerEventSystem;
        PointerEventData pointerEventData;
        bool drawing;
        Cell last;

        public void Initialize(RunController controller, NotebookWorldView world, Camera camera)
        { run = controller; view = world; worldCamera = camera; CameraCenter=new Vector2(run.Session.Balance.Width*.5f,run.Session.Balance.Height*.5f); run.SessionChanged += OnRestart; }
        void OnDestroy() { if (run != null) run.SessionChanged -= OnRestart; }
        void OnRestart(IRunReadModel session) { Cancel(); Zoom=1;CameraCenter=new Vector2(session.Balance.Width*.5f,session.Balance.Height*.5f); Message = "Чистый лист. Попробуй другую оборону!"; }

        public void SelectTool(StructureKind kind)
        { Cancel(); Tool = kind; Mode = DrawingMode.Build; SelectionChanged?.Invoke(); }
        public void SelectTier(MaterialTier tier)
        { Cancel(); Tier = tier; SelectionChanged?.Invoke(); }
        public void SelectMode(DrawingMode mode)
        { Cancel(); Mode = mode; SelectionChanged?.Invoke(); }
        public void SetMessage(string message) { Message = message; }

        void Update()
        {
            if (run == null || run.Session == null) return;
            ReadKeys();
            if (run.Session.PendingUpgrade || run.Session.Phase == RunPhase.Victory || run.Session.Phase == RunPhase.Defeat)
            { Cancel(); HasHover=false; return; }
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { Cancel(); return; }
            Vector2 screen = mouse.position.ReadValue();
            Navigate(mouse,screen);
            HasHover = TryCell(screen, out var cell) && !OverUi(screen);
            if (HasHover) HoverCell = cell;
            if (mouse.leftButton.wasPressedThisFrame && HasHover && !run.Session.PendingUpgrade)
            {
                if (Mode == DrawingMode.Upgrade)
                { Message = run.Upgrade(cell, out var reason) ? "Обведено! Постройка стала прочнее и мощнее." : reason; }
                else if (Mode == DrawingMode.Erase) Erase(cell);
                else { drawing = true; stroke.Clear(); visited.Clear(); last = cell; Add(cell); }
            }
            if (mouse.leftButton.isPressed && HasHover)
            {
                if (Mode == DrawingMode.Erase) Erase(cell);
                if (drawing)
                {
                    if (Tool == StructureKind.Wall || Tool == StructureKind.Spikes) AddLine(last, cell);
                    else { stroke.Clear(); visited.Clear(); Add(cell); }
                    last = cell;
                }
            }
            if (drawing && mouse.leftButton.wasReleasedThisFrame)
            {
                if (HasHover)
                {
                    bool success = run.Build(stroke, Tool, Tier, out var reason);
                    Message = success ? (run.Paused ? "Чертёж принят. Стройка продолжится после паузы." : "Штрих принят — строим!") : reason;
                }
                Cancel();
            }
            if (!HasHover || Mode != DrawingMode.Build) { view.ClearPreview(); PreviewCost = 0; PreviewReason = ""; return; }
            hoverPreview[0] = cell;
            IReadOnlyList<Cell> cells = drawing ? stroke : hoverPreview;
            var quote = run.Session.QuoteBuild(cells, Tool, Tier);
            PreviewCost = quote.Cost;
            PreviewReason = quote.Valid ? "" : quote.Reason;
            view.SetPreview(quote.Cells, Tool, Tier, quote.Valid);
        }

        void Navigate(Mouse mouse,Vector2 screen)
        {
            if (!worldCamera.pixelRect.Contains(screen) || OverUi(screen)) return;
            float wheel=mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel)>.01f)
            {
                var before=worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));
                float old=Zoom;Zoom=Mathf.Clamp(Zoom*Mathf.Exp(wheel*.0015f),1,3);
                worldCamera.orthographicSize*=old/Zoom;
                var after=worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));
                CameraCenter+=new Vector2(before.x-after.x,before.y-after.y);
            }
            if(mouse.middleButton.isPressed)
                CameraCenter-=mouse.delta.ReadValue()*(worldCamera.orthographicSize*2/worldCamera.pixelHeight);
            float halfY=worldCamera.orthographicSize,halfX=halfY*worldCamera.aspect;
            var b=run.Session.Balance;
            CameraCenter=new Vector2(halfX>=b.Width*.5f ? b.Width*.5f : Mathf.Clamp(CameraCenter.x,halfX,b.Width-halfX),
                halfY>=b.Height*.5f ? b.Height*.5f : Mathf.Clamp(CameraCenter.y,halfY,b.Height-halfY));
            worldCamera.transform.position=new Vector3(CameraCenter.x,CameraCenter.y,-10);
        }

        void Erase(Cell cell)
        { if (run.Erase(cell, out int refund)) Message = $"Стерто. Возвращено штрихов: {refund}."; }
        void Add(Cell cell) { if (visited.Add(cell)) stroke.Add(cell); }
        void AddLine(Cell a, Cell b)
        {
            int x = a.X, y = a.Y, dx = Math.Abs(b.X - x), sx = x < b.X ? 1 : -1;
            int dy = -Math.Abs(b.Y - y), sy = y < b.Y ? 1 : -1, err = dx + dy;
            while (true)
            {
                Add(new Cell(x, y));
                if (x == b.X && y == b.Y) break;
                int e2 = err * 2;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }
        public void Cancel() { drawing = false; stroke.Clear(); visited.Clear(); PreviewCost = 0; PreviewReason = ""; if (view != null) view.ClearPreview(); }

        bool TryCell(Vector2 screen, out Cell cell)
        {
            cell = default;
            if (worldCamera == null || !worldCamera.pixelRect.Contains(screen)) return false;
            Vector3 p = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            cell = new Cell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
            return cell.X >= 0 && cell.X < run.Session.Balance.Width && cell.Y >= 0 && cell.Y < run.Session.Balance.Height;
        }
        bool OverUi(Vector2 position)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            if (pointerEventSystem != system || pointerEventData == null)
            {
                pointerEventSystem = system;
                pointerEventData = new PointerEventData(system);
            }
            pointerEventData.Reset();
            pointerEventData.position = position;
            uiHits.Clear();
            system.RaycastAll(pointerEventData, uiHits);
            return uiHits.Count > 0;
        }
        void ReadKeys()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.digit1Key.wasPressedThisFrame) SelectTool(StructureKind.Wall);
            if (k.digit2Key.wasPressedThisFrame) SelectTool(StructureKind.Gun);
            if (k.digit3Key.wasPressedThisFrame) SelectTool(StructureKind.Piercer);
            if (k.digit4Key.wasPressedThisFrame) SelectTool(StructureKind.Mortar);
            if (k.digit5Key.wasPressedThisFrame) SelectTool(StructureKind.Spikes);
            if (k.eKey.wasPressedThisFrame) SelectMode(DrawingMode.Erase);
            if (k.uKey.wasPressedThisFrame) SelectMode(DrawingMode.Upgrade);
            if (k.qKey.wasPressedThisFrame) SelectTier((MaterialTier)(((int)Tier + 1) % 3));
            if (k.spaceKey.wasPressedThisFrame) run.TogglePause();
            if (k.nKey.wasPressedThisFrame) run.NextWave();
            if (k.fKey.wasPressedThisFrame) run.ToggleSpeed();
            if (k.rKey.wasPressedThisFrame) run.Restart();
            if (k.escapeKey.wasPressedThisFrame) Cancel();
            if (k.tabKey.wasPressedThisFrame) { ShowRanges = !ShowRanges; view.ShowRanges = ShowRanges; }
            if (k.pKey.wasPressedThisFrame && HasHover)
            {
                var structure = run.Session.StructureAt(HoverCell);
                if (structure != null)
                {
                    var priority = (TargetPriority)(((int)structure.Value.Priority + 1) % 3);
                    if (run.SetPriority(HoverCell, priority)) Message = "Приоритет: " + (priority == TargetPriority.Nearest ? "ближайшие" : priority == TargetPriority.Strongest ? "прочные" : "грызуны");
                }
            }
        }
    }
}
