using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PaperDefense.Presentation
{
    public enum DrawingMode { Build, Erase, Upgrade, Blotter, Paint }

    public sealed class DrawingInput : MonoBehaviour
    {
        public UserTextResolver TextResolver { get; set; }
        public bool ConstructionOnly { get; set; }
        string Resolve(string value) => GameText.Resolve(TextResolver, value);
        public StructureKind Tool { get; private set; } = StructureKind.Wall;
        public MaterialTier Tier { get; private set; } = MaterialTier.Pencil;
        public InkColor Color { get; private set; } = InkColor.None;
        public int BlueprintSlot { get; private set; } = -1;
        public DrawingMode Mode { get; private set; }
        string message = UserText.Message("copy.462015bfa97612f9");
        public string Message { get => Resolve(message); private set => message = value; }
        public int PreviewCost { get; private set; }
        public string PreviewReason { get; private set; } = "";
        public Cell HoverCell { get; private set; }
        public bool HasHover { get; private set; }
        public bool ShowRanges => view != null && view.ShowRanges;
        public float Zoom { get; private set; } = 1;
        public Vector2 CameraCenter { get; private set; }
        public event Action SelectionChanged;
        RunController run;
        NotebookWorldView view;
        Camera worldCamera;
        public NotebookViewSettingsAsset ViewSettings;
        NotebookViewSettingsAsset CameraSettings => ViewSettings != null ? ViewSettings : NotebookViewSettingsAsset.Default;
        readonly ConstructionGesture gesture = new ConstructionGesture();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        readonly Cell[] hoverPreview = new Cell[1];
        EventSystem pointerEventSystem;
        PointerEventData pointerEventData;
        int erasedCells;
        InputAction constructionPress;
        InputSettings pointerSettings;
        Mouse pressMouse;
        Vector2 pressPosition;
        bool hasPressOrigin;

        void OnEnable()
        {
            pointerSettings = PointerEventPolicy.Acquire();
            if (constructionPress == null)
            {
                constructionPress = new InputAction("Construction press", InputActionType.Button, "<Mouse>/leftButton");
                constructionPress.performed += CapturePressOrigin;
            }
            constructionPress.Enable();
        }

        void CapturePressOrigin(InputAction.CallbackContext context)
        {
            pressMouse = context.control.device as Mouse;
            hasPressOrigin = pressMouse != null;
            if (hasPressOrigin) pressPosition = pressMouse.position.ReadValue();
        }

        public void Initialize(RunController controller, NotebookWorldView world, Camera camera)
        {
            if (run != null) run.SessionChanged -= OnRestart;
            Cancel();
            run = controller; view = world; worldCamera = camera;
            CameraCenter = new Vector2(run.Session.Balance.Width * .5f, run.Session.Balance.Height * .5f);
            ResetUnavailableSelections();
            run.SessionChanged += OnRestart;
        }
        void OnDestroy()
        {
            if (run != null) run.SessionChanged -= OnRestart;
            if (constructionPress != null)
            {
                constructionPress.performed -= CapturePressOrigin;
                constructionPress.Dispose();
            }
        }
        void OnDisable()
        {
            constructionPress?.Disable();
            PointerEventPolicy.Release(pointerSettings);
            pointerSettings = null;
            Cancel(); HasHover = false;
        }
        void OnApplicationFocus(bool focused) { if (!focused) { Cancel(); HasHover = false; } }
        void OnRestart(IRunReadModel session)
        {
            Cancel(); Zoom = 1; CameraCenter = new Vector2(session.Balance.Width * .5f, session.Balance.Height * .5f);
            ResetUnavailableSelections();
            if (BlueprintSlot >= 0)
            {
                if (BlueprintSlot < run.BuildLoadout.Count && run.BuildLoadout[BlueprintSlot] != null)
                    Tool = run.BuildLoadout[BlueprintSlot].FirstWeapon;
                else { BlueprintSlot = -1; Tool = StructureKind.Wall; }
            }
            Message = UserText.Message("copy.330da6cb58f99c22");
            SelectionChanged?.Invoke();
        }

        public void SelectTool(StructureKind kind)
        {
            if (!Enum.IsDefined(typeof(StructureKind), kind)) return;
            if (ConstructionOnly && !ConstructionToolRules.IsTool(kind)) return;
            if (run != null && run.Session != null && !run.Session.Balance.HasStructure(kind)) return;
            Cancel(); BlueprintSlot = -1; Tool = kind; Mode = DrawingMode.Build; SelectionChanged?.Invoke();
        }
        public void SelectSlot(int slot)
        {
            if (ConstructionOnly) return;
            if (run == null || slot < 0 || slot >= run.BuildLoadout.Count) return;
            if (run.BuildLoadout[slot] == null) { Message = UserText.Message("copy.39c29e0231f823f1"); return; }
            Cancel(); BlueprintSlot = slot; Tool = run.BuildLoadout[slot].FirstWeapon;
            Mode = DrawingMode.Build; SelectionChanged?.Invoke();
        }
        public void SelectTier(MaterialTier tier)
        { Cancel(); Tier = tier; SelectionChanged?.Invoke(); }
        public void SelectColor(InkColor color)
        {
            if (ConstructionOnly && color != InkColor.None) return;
            if (!Enum.IsDefined(typeof(InkColor), color)) return;
            if (color != InkColor.None && run != null && run.Session != null && !run.Session.Balance.Mechanics.ColorCartridges)
            { Message = UserText.Message("copy.0a21e662d398deaa"); return; }
            Cancel(); Color = color; SelectionChanged?.Invoke();
        }
        public void SelectMode(DrawingMode mode)
        {
            if (ConstructionOnly && mode != DrawingMode.Build && mode != DrawingMode.Erase && mode != DrawingMode.Upgrade) return;
            if (!Enum.IsDefined(typeof(DrawingMode), mode)) return;
            if (run != null && run.Session != null)
            {
                var mechanics = run.Session.Balance.Mechanics;
                if (mode == DrawingMode.Blotter && !mechanics.CorpseBlots)
                { Message = UserText.Message("copy.babf0ea42d60454a"); return; }
                if (mode == DrawingMode.Paint && !mechanics.ColorCartridges)
                { Message = UserText.Message("copy.0a21e662d398deaa"); return; }
            }
            Cancel(); Mode = mode; SelectionChanged?.Invoke();
        }
        void ResetUnavailableSelections()
        {
            var mechanics = run.Session.Balance.Mechanics;
            if (!run.Session.Balance.HasStructure(Tool)) { BlueprintSlot = -1; Tool = StructureKind.Wall; }
            if (!mechanics.ColorCartridges) Color = InkColor.None;
            if ((Mode == DrawingMode.Paint && !mechanics.ColorCartridges) ||
                (Mode == DrawingMode.Blotter && !mechanics.CorpseBlots)) Mode = DrawingMode.Build;
        }
        public void SetMessage(string message) { Message = message; }

        void Update()
        {
            if (run == null || run.Session == null) return;
            if (run.InWorkshop) { Cancel(); HasHover = false; return; }
            var mouse = Mouse.current;
            Vector2 screen = mouse != null ? mouse.position.ReadValue() : default;
            Cell cell = default;
            HasHover = mouse != null && TryCell(screen, out cell) && !OverUi(screen);
            if (HasHover) HoverCell = cell;
            ReadKeys();
            if (run.InWorkshop || run.Session.PendingUpgrade || run.Session.Phase == RunPhase.Victory || run.Session.Phase == RunPhase.Defeat)
            { Cancel(); HasHover=false; return; }
            if (mouse == null) { Cancel(); HasHover = false; return; }
            if (mouse.rightButton.wasPressedThisFrame) { Cancel(); return; }
            Navigate(mouse,screen);
            HasHover = TryCell(screen, out cell) && !OverUi(screen);
            if (HasHover) HoverCell = cell;
            if (!HasHover) Cancel();
            // Button callbacks preserve the down position even when several pointer events arrive in one frame.
            if (mouse.leftButton.wasPressedThisFrame && HasHover && hasPressOrigin && pressMouse == mouse &&
                TryCell(pressPosition, out var startCell) && !OverUi(pressPosition))
            {
                if (Mode == DrawingMode.Upgrade)
                { Message = run.Upgrade(startCell, out var reason) ? UserText.Message("copy.c21e825dfc5ef9fd") : reason; }
                else if (Mode == DrawingMode.Erase) { erasedCells = 0; gesture.Begin(startCell, continuous: true); }
                else if (Mode == DrawingMode.Blotter)
                { Message = run.Blotter(startCell, out var reason) ? UserText.Message("copy.1aa1c191d8f61cbe") : reason; }
                else if (Mode == DrawingMode.Paint)
                { Message = run.Paint(startCell, Color, out var reason) ? UserText.Message("copy.d28731380a0c6a83") : reason; }
                else gesture.Begin(startCell, BlueprintSlot < 0 && ConstructionToolRules.IsTool(Tool));
            }
            hasPressOrigin = false;
            if (gesture.Active && HasHover && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
            {
                gesture.Move(cell);
                if (Mode == DrawingMode.Erase)
                    while (erasedCells < gesture.Cells.Count) Erase(gesture.Cells[erasedCells++]);
            }
            if (gesture.Active && mouse.leftButton.wasReleasedThisFrame)
            {
                if (HasHover && Mode == DrawingMode.Build)
                {
                    string reason;
                    bool success = BlueprintSlot >= 0 ? run.BuildBlueprint(gesture.Cells, BlueprintSlot, Tier, Color, out reason) : run.Build(gesture.Cells, Tool, Tier, out reason);
                    Message = success ? (run.Paused ? UserText.Message("copy.5550d1f4c6ac852a") : UserText.Message("copy.6d7492b5fc3888c5")) : reason;
                }
                Cancel();
            }
            if (!mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame) Cancel();
            if (!HasHover || Mode != DrawingMode.Build) { view.ClearPreview(); PreviewCost = 0; PreviewReason = ""; return; }
            hoverPreview[0] = cell;
            IReadOnlyList<Cell> cells = gesture.Active ? gesture.Cells : hoverPreview;
            var quote = BlueprintSlot >= 0 ? run.QuoteBlueprint(cells, BlueprintSlot, Tier, Color) : run.QuoteBuild(cells, Tool, Tier);
            PreviewCost = quote.Cost;
            PreviewReason = quote.Valid ? "" : quote.Reason;
            var blueprint = BlueprintSlot >= 0 && BlueprintSlot < run.BuildLoadout.Count ? run.BuildLoadout[BlueprintSlot] : null;
            float range = blueprint != null ? run.PreviewBlueprint(blueprint, Tier).Range : -1;
            view.SetPreview(quote.Cells, Tool, Tier, quote.Valid, range, blueprint: blueprint);
        }

        void Navigate(Mouse mouse,Vector2 screen)
        {
            if (!worldCamera.pixelRect.Contains(screen) || OverUi(screen)) return;
            float wheel = ScrollNotches(mouse.scroll.ReadValue().y, InputSystem.settings.scrollDeltaBehavior, UnityEngine.Application.platform);
            if (Mathf.Abs(wheel) > .00001f)
            {
                var before=worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));
                float old = Zoom; Zoom = CameraSettings.ZoomAfterScroll(Zoom, wheel);
                worldCamera.orthographicSize*=old/Zoom;
                var after=worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));
                CameraCenter+=new Vector2(before.x-after.x,before.y-after.y);
            }
            if(mouse.middleButton.isPressed)
                CameraCenter-=mouse.delta.ReadValue()*(worldCamera.orthographicSize*2/worldCamera.pixelHeight);
            ClampCamera();
        }

        void ClampCamera()
        {
            float halfY=worldCamera.orthographicSize,halfX=halfY*worldCamera.aspect;
            var b=run.Session.Balance;
            CameraCenter=new Vector2(halfX>=b.Width*.5f ? b.Width*.5f : Mathf.Clamp(CameraCenter.x,halfX,b.Width-halfX),
                halfY>=b.Height*.5f ? b.Height*.5f : Mathf.Clamp(CameraCenter.y,halfY,b.Height-halfY));
            worldCamera.transform.position=new Vector3(CameraCenter.x,CameraCenter.y,-10);
        }

        internal static float ScrollNotches(float delta, InputSettings.ScrollDeltaBehavior behavior, RuntimePlatform platform)
        {
            // Modern Input System already returns notches. Legacy Windows input alone uses 120 units.
            bool windows = platform == RuntimePlatform.WindowsEditor || platform == RuntimePlatform.WindowsPlayer;
            return behavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange && windows ? delta / 120f : delta;
        }

        void Erase(Cell cell)
        { if (run.Erase(cell, out int refund)) Message = UserText.Message("copy.5520b3d1e73f8b1d", refund); }
        public void Cancel() { gesture.Cancel(); erasedCells = 0; hasPressOrigin = false; PreviewCost = 0; PreviewReason = ""; if (view != null) view.ClearPreview(); }
        public void Focus(Cell cell)
        {
            Cancel(); float old = Zoom; Zoom = Mathf.Clamp(Mathf.Max(1.5f, Zoom), 1, Mathf.Max(1, CameraSettings.MaximumZoom));
            CameraCenter = new Vector2(cell.X + .5f, cell.Y + .5f);
            if (worldCamera != null) { worldCamera.orthographicSize *= old / Zoom; ClampCamera(); }
        }

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
            if (ConstructionOnly)
            {
                if (k.digit5Key.wasPressedThisFrame) SelectTool(StructureKind.Wall);
                if (k.digit6Key.wasPressedThisFrame) SelectTool(StructureKind.Spikes);
                if (k.digit7Key.wasPressedThisFrame) SelectTool(StructureKind.TarPit);
                if (k.digit8Key.wasPressedThisFrame) SelectTool(StructureKind.RepairPost);
                if (k.eKey.wasPressedThisFrame) SelectMode(DrawingMode.Erase);
                if (k.uKey.wasPressedThisFrame) SelectMode(DrawingMode.Upgrade);
                if (k.qKey.wasPressedThisFrame) SelectTier((MaterialTier)(((int)Tier + 1) % 3));
                if (k.spaceKey.wasPressedThisFrame) run.TogglePause();
                if (k.escapeKey.wasPressedThisFrame) Cancel();
                if (k.tabKey.wasPressedThisFrame) view.ShowRanges = !view.ShowRanges;
                return;
            }
            if (k.digit1Key.wasPressedThisFrame) SelectSlot(0);
            if (k.digit2Key.wasPressedThisFrame) SelectSlot(1);
            if (k.digit3Key.wasPressedThisFrame) SelectSlot(2);
            if (k.digit4Key.wasPressedThisFrame) SelectSlot(3);
            if (k.digit5Key.wasPressedThisFrame) SelectTool(StructureKind.Wall);
            if (k.digit6Key.wasPressedThisFrame) SelectTool(StructureKind.Spikes);
            if (k.digit7Key.wasPressedThisFrame) SelectTool(StructureKind.TarPit);
            if (k.digit8Key.wasPressedThisFrame) SelectTool(StructureKind.RepairPost);
            if (k.eKey.wasPressedThisFrame) SelectMode(DrawingMode.Erase);
            if (k.uKey.wasPressedThisFrame) SelectMode(DrawingMode.Upgrade);
            if (k.bKey.wasPressedThisFrame) SelectMode(DrawingMode.Blotter);
            if (k.cKey.wasPressedThisFrame) SelectMode(DrawingMode.Paint);
            if (k.qKey.wasPressedThisFrame) SelectTier((MaterialTier)(((int)Tier + 1) % 3));
            if (k.spaceKey.wasPressedThisFrame) run.TogglePause();
            if (k.nKey.wasPressedThisFrame) run.NextWave();
            if (k.fKey.wasPressedThisFrame) run.ToggleSpeed();
            if (k.rKey.wasPressedThisFrame) run.Restart();
            if (k.escapeKey.wasPressedThisFrame) Cancel();
            if (k.tabKey.wasPressedThisFrame) view.ShowRanges = !view.ShowRanges;
            if (k.pKey.wasPressedThisFrame && HasHover)
            {
                var structure = run.Session.StructureAt(HoverCell);
                if (structure != null)
                {
                    var priority = (TargetPriority)(((int)structure.Value.Priority + 1) % 3);
                    if (run.SetPriority(HoverCell, priority)) Message = UserText.Message("copy.3c108bd2fc41bdfe", (priority == TargetPriority.Nearest ? UserText.Message("copy.e8587d93da4cf0eb") : priority == TargetPriority.Strongest ? UserText.Message("copy.38d5a59f100989bb") : UserText.Message("copy.ae092c38e45a8570")));
                }
            }
        }
    }
}
