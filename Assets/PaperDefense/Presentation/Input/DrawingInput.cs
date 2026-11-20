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
        public bool HandleSessionHotkeys { get; set; } = true;
        string Resolve(string value) => GameText.Resolve(TextResolver, value);
        public StructureKind Tool { get; private set; } = StructureKind.Wall;
        public MaterialTier Tier { get; private set; } = MaterialTier.Pencil;
        public InkColor Color { get; private set; } = InkColor.None;
        public int BlueprintSlot { get; private set; } = -1;
        public bool IsGameplayInputBlocked
        {
            get
            {
                var selected = EventSystem.current?.currentSelectedGameObject;
                for (var current = selected != null && selected.activeInHierarchy ? selected.transform : null; current != null; current = current.parent)
                {
                    var input = current.GetComponent<UnityEngine.UI.InputField>();
                    var tmp = current.GetComponent("TMP_InputField") as Behaviour;
                    if ((input != null && input.isActiveAndEnabled) || (tmp != null && tmp.isActiveAndEnabled))
                        return true;
                }
                foreach (var modal in FindObjectsByType<WorkshopModal>(FindObjectsSortMode.None))
                    if (modal.isActiveAndEnabled) return true;
                return false;
            }
        }
        public DrawingMode Mode { get; private set; }
        static readonly string AlbumPlanningMessage = UserText.Message("assembly.battle.planning_hint");
        string message = UserText.Message("copy.462015bfa97612f9");
        internal bool HasInitialPlanningInstruction => !IsPlacingTower && message == AlbumPlanningMessage;
        public string Message
        {
            get => IsPlacingTower ? (PreviewReason.Length > 0 ? Resolve(PreviewReason) : "Наведи сектор · ЛКМ — " + PreviewCost + " штр.") + " · ПКМ / Esc — отмена"
                : HasInitialPlanningInstruction && run?.Session?.WaveIndex > 0 ? "" : Resolve(message);
            private set => message = value;
        }
        public int PreviewCost { get; private set; }
        public string PreviewReason { get; private set; } = "";
        public Cell HoverCell { get; private set; }
        public bool HasHover { get; private set; }
        public Cell? InspectedCell { get; private set; }
        public Cell? PlacementCell { get; private set; }
        public bool IsPlacingTower => PlacementCell.HasValue;
        float placementHeading;
        Vector2 placementStart;
        public void Inspect(Cell? cell)
        {
            Cancel();
            InspectedCell = cell;
            view?.SetInspectedSector(cell);
        }

        public bool CancelPlacement()
        {
            if (!IsPlacingTower) return false;
            Cancel();
            return true;
        }
        public bool ShowRanges => view != null && view.ShowRanges;
        public float Zoom { get; private set; } = 1;
        public Vector2 CameraCenter { get; private set; }
        bool followHordeFront;
        int framedSides, framedBands;
        Rect framedField;
        public event Action SelectionChanged;
        RunController run;
        NotebookWorldView view;
        RunLoadoutSnapshot previewLoadout;
        int previewSlot = -1;
        MaterialTier previewMaterial;
        ResolvedEntry cachedEntry;
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
            Inspect(null);
            followHordeFront = false;
            Zoom = 1; CameraCenter = new Vector2(session.Balance.Width * .5f, session.Balance.Height * .5f);
            ResetUnavailableSelections();
            if (BlueprintSlot >= 0)
            {
                if (run.UsesAlbumSlots) BindAlbumSlot(BlueprintSlot);
                else if (BlueprintSlot < run.BuildLoadout.Count && run.BuildLoadout[BlueprintSlot] != null)
                    Tool = run.BuildLoadout[BlueprintSlot].FirstWeapon;
                else { BlueprintSlot = -1; Tool = StructureKind.Wall; }
            }
            Message = UserText.Message(run.UsesAlbumSlots ? "assembly.battle.planning_hint" : "copy.330da6cb58f99c22");
            SelectionChanged?.Invoke();
        }

        public void SelectTool(StructureKind kind)
        {
            if (!Enum.IsDefined(typeof(StructureKind), kind)) return;
            if (ConstructionOnly && !ConstructionToolRules.IsTool(kind)) return;
            if (run != null && run.Session != null && !run.Session.Balance.HasStructure(kind)) return;
            Inspect(null); BlueprintSlot = -1; Tool = kind; Mode = DrawingMode.Build; SelectionChanged?.Invoke();
        }
        public void SelectSlot(int slot)
        {
            if (ConstructionOnly) return;
            if (run != null && run.UsesAlbumSlots)
            {
                if (slot < 0 || slot >= run.RunLoadout.Entries.Count) return;
                Inspect(null); BindAlbumSlot(slot); Mode = DrawingMode.Build; SelectionChanged?.Invoke(); return;
            }
            if (run == null || slot < 0 || slot >= run.BuildLoadout.Count) return;
            if (run.BuildLoadout[slot] == null) { Message = UserText.Message("copy.39c29e0231f823f1"); return; }
            Inspect(null); BlueprintSlot = slot; Tool = run.BuildLoadout[slot].FirstWeapon;
            Mode = DrawingMode.Build; SelectionChanged?.Invoke();
        }
        void BindAlbumSlot(int slot)
        {
            BlueprintSlot = slot;
            var resolved = SelectedAlbumEntry(MaterialTier.Pencil);
            if (resolved == null) { Message = UserText.Message("copy.39c29e0231f823f1"); return; }
            var page = run.RunLoadout.Entries[slot];
            Tool = page.Kind == AlbumEntryKind.Legacy ? page.LegacyBlueprint.FirstWeapon : new StructureState(resolved).Kind;
            if (resolved.Support?.Kind == AssemblyStructureFamily.Generator) Tier = MaterialTier.Pencil;
            if (resolved.Kind == AlbumEntryKind.Structure) Color = InkColor.None;
        }
        ResolvedEntry SelectedAlbumEntry(MaterialTier material)
        {
            if (run == null || !run.UsesAlbumSlots || run.RunLoadout == null || BlueprintSlot < 0 || BlueprintSlot >= run.RunLoadout.Entries.Count) return null;
            if (previewLoadout == run.RunLoadout && previewSlot == BlueprintSlot && previewMaterial == material) return cachedEntry;
            previewLoadout = run.RunLoadout; previewSlot = BlueprintSlot; previewMaterial = material;
            try { cachedEntry = previewLoadout.ResolveSlot(BlueprintSlot, material); }
            catch (ArgumentException) { cachedEntry = null; }
            return cachedEntry;
        }
        public void SelectTier(MaterialTier tier)
        {
            if (!Enum.IsDefined(typeof(MaterialTier), tier)) return;
            if (run?.Session?.Balance.Mechanics.FieldModernization == true) tier = MaterialTier.Pencil;
            if (SelectedAlbumEntry(MaterialTier.Pencil)?.Support?.Kind == AssemblyStructureFamily.Generator) tier = MaterialTier.Pencil;
            Cancel(); Tier = tier; SelectionChanged?.Invoke();
        }
        public void SelectColor(InkColor color)
        {
            if (run?.Session?.Balance.Mechanics.FieldModernization == true)
            { Color = InkColor.None; return; }
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
            if (run?.Session?.Balance.Mechanics.FieldModernization == true &&
                (mode == DrawingMode.Paint || mode == DrawingMode.Upgrade))
            {
                Cancel(); Mode = DrawingMode.Build;
                Message = "ЛКМ по башне — улучшения справа. Space — пауза.";
                SelectionChanged?.Invoke(); return;
            }
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
            if (mechanics.FieldModernization)
            {
                Tier = MaterialTier.Pencil; Color = InkColor.None;
                if (Mode == DrawingMode.Paint || Mode == DrawingMode.Upgrade) Mode = DrawingMode.Build;
            }
            if (!run.Session.Balance.HasStructure(Tool)) { BlueprintSlot = -1; Tool = StructureKind.Wall; }
            if (!mechanics.ColorCartridges) Color = InkColor.None;
            if ((Mode == DrawingMode.Paint && !mechanics.ColorCartridges) ||
                (Mode == DrawingMode.Blotter && !mechanics.CorpseBlots)) Mode = DrawingMode.Build;
        }
        public void SetMessage(string message) { Message = message; }

        void Update()
        {
            if (run == null || run.Session == null) return;
            if (run.InWorkshop || run.CommandsSuspended || IsGameplayInputBlocked) { Cancel(); HasHover = false; return; }
            var mouse = Mouse.current;
            Vector2 screen = mouse != null ? mouse.position.ReadValue() : default;
            Cell cell = default;
            HasHover = mouse != null && TryCell(screen, out cell) && !OverUi(screen);
            if (HasHover) HoverCell = cell;
            ReadKeys();
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) return;
            if (run.InWorkshop || run.Session.PendingUpgrade || run.Session.Phase == RunPhase.Victory || run.Session.Phase == RunPhase.Defeat)
            { Cancel(); HasHover=false; return; }
            if (mouse == null) { Cancel(); HasHover = false; return; }
            if (mouse.rightButton.wasPressedThisFrame) { Cancel(); return; }
            Navigate(mouse,screen);
            HasHover = TryCell(screen, out cell) && !OverUi(screen);
            if (HasHover) HoverCell = cell;
            if (IsPlacingTower)
            {
                UpdatePlacement(mouse, screen);
                return;
            }
            if (!HasHover) Cancel();
            // Button callbacks preserve the down position even when several pointer events arrive in one frame.
            if (mouse.leftButton.wasPressedThisFrame && HasHover && hasPressOrigin && pressMouse == mouse &&
                TryCell(pressPosition, out var startCell) && !OverUi(pressPosition))
            {
                Inspect(null);
                if (Mode == DrawingMode.Upgrade)
                { Message = run.Upgrade(startCell, out var reason) ? UserText.Message("copy.c21e825dfc5ef9fd") : reason; }
                else if (Mode == DrawingMode.Erase) { erasedCells = 0; gesture.Begin(startCell, continuous: true); }
                else if (Mode == DrawingMode.Blotter)
                { Message = run.Blotter(startCell, out var reason) ? UserText.Message("copy.1aa1c191d8f61cbe") : reason; }
                else if (Mode == DrawingMode.Paint)
                { Message = run.Paint(startCell, Color, out var reason) ? UserText.Message("copy.d28731380a0c6a83") : reason; }
                else if (DirectionalSelection())
                {
                    if (run.Session.StructureAt(startCell).HasValue) { Inspect(startCell); return; }
                    hoverPreview[0] = startCell;
                    var quote = Quote(hoverPreview);
                    if (quote.Valid)
                    {
                        Message = "";
                        PlacementCell = startCell;
                        placementStart = pressPosition;
                        placementHeading = run.Session.DefaultFacingDegrees(startCell, Tool, SelectedAlbumEntry(Tier), SelectedBlueprint());
                        DrawBuildPreview(hoverPreview, quote, placementHeading);
                        return;
                    }
                    Message = quote.Reason;
                }
                else if (run.UsesAlbumSlots)
                {
                    var selected = SelectedAlbumEntry(Tier);
                    // Inspection belongs to the placed object, even when the selected album slot is empty.
                    if (selected == null && run.Session.StructureAt(startCell).HasValue) gesture.Begin(startCell, false);
                    else gesture.Begin(startCell, selected);
                }
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
                    if (gesture.Cells.Count == 1 && run.Session.StructureAt(cell).HasValue) Inspect(cell);
                    else
                    {
                        CommitBuild(gesture.Cells);
                    }
                }
                Cancel();
            }
            if (gesture.Active && !mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame) Cancel();
            if (!HasHover || Mode != DrawingMode.Build) { view.ClearPreview(); PreviewCost = 0; PreviewReason = ""; return; }
            if (InspectedCell.HasValue && InspectedCell.Value.Equals(cell))
            { view.ClearPreview(); PreviewCost = 0; PreviewReason = ""; return; }
            hoverPreview[0] = cell;
            IReadOnlyList<Cell> cells = gesture.Active ? gesture.Cells : hoverPreview;
            var quotePreview = Quote(cells);
            DrawBuildPreview(quotePreview.Cells, quotePreview);
        }

        BuildQuote Quote(IReadOnlyList<Cell> cells) => run.UsesAlbumSlots ? run.QuoteEntry(cells, BlueprintSlot, Tier, Color)
            : BlueprintSlot >= 0 ? run.QuoteBlueprint(cells, BlueprintSlot, Tier, Color) : run.QuoteBuild(cells, Tool, Tier);

        bool CommitBuild(IReadOnlyList<Cell> cells, float? heading = null)
        {
            string reason;
            bool built = run.UsesAlbumSlots ? run.BuildEntry(cells, BlueprintSlot, Tier, Color, out reason, heading)
                : BlueprintSlot >= 0 ? run.BuildBlueprint(cells, BlueprintSlot, Tier, Color, out reason, heading)
                : run.Build(cells, Tool, Tier, out reason, heading);
            Message = built ? UserText.Message(run.Paused ? "copy.5550d1f4c6ac852a" : "copy.6d7492b5fc3888c5") : reason;
            return built;
        }

        TowerBlueprint SelectedBlueprint() => run.UsesAlbumSlots && BlueprintSlot >= 0 && BlueprintSlot < run.RunLoadout.Entries.Count
            ? run.RunLoadout.Entries[BlueprintSlot]?.LegacyBlueprint
            : BlueprintSlot >= 0 && BlueprintSlot < run.BuildLoadout.Count ? run.BuildLoadout[BlueprintSlot] : null;

        bool DirectionalSelection()
        {
            var entry = SelectedAlbumEntry(Tier);
            return (!run.UsesAlbumSlots || entry != null) &&
                NotebookMountLayout.HasDirectionalMount(Tool, entry, SelectedBlueprint(), run.Session.Balance.Content);
        }

        void DrawBuildPreview(IReadOnlyList<Cell> cells, BuildQuote quote, float? heading = null)
        {
            PreviewCost = quote.Cost;
            PreviewReason = quote.Valid ? "" : quote.Reason;
            var blueprint = SelectedBlueprint();
            var selectedEntry = SelectedAlbumEntry(Tier);
            float range = blueprint != null ? run.PreviewBlueprint(blueprint, Tier).Range : -1;
            if (selectedEntry != null)
            {
                range = selectedEntry.Support?.Radius ?? 0;
                foreach (var weapon in selectedEntry.Weapons) range = Mathf.Max(range, weapon.Range);
            }
            view.SetPreview(cells, Tool, Tier, quote.Valid, range, blueprint: blueprint,
                recipe: selectedEntry?.Kind == AlbumEntryKind.Legacy ? null : selectedEntry?.Snapshot, entry: selectedEntry, facingDegrees: heading);
        }

        void UpdatePlacement(Mouse mouse, Vector2 screen)
        {
            bool pressed = mouse.leftButton.wasPressedThisFrame && hasPressOrigin && pressMouse == mouse;
            hasPressOrigin = false;
            Vector2 point = pressed ? pressPosition : screen;
            bool onField = HasHover && (!pressed || (TryCell(point, out _) && !OverUi(point)));
            var cell = PlacementCell.Value;
            hoverPreview[0] = cell;
            if (onField && (point - placementStart).sqrMagnitude > 1)
            {
                Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(point.x, point.y, -worldCamera.transform.position.z));
                var delta = new Vector2(world.x - cell.X - .5f, world.y - cell.Y - .5f);
                if (delta.sqrMagnitude >= .0025f)
                    placementHeading = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 360);
            }
            var quote = Quote(hoverPreview);
            DrawBuildPreview(hoverPreview, quote, placementHeading);
            if (onField && pressed && quote.Valid && CommitBuild(hoverPreview, placementHeading)) Cancel();
        }

        void Navigate(Mouse mouse,Vector2 screen)
        {
            if (!worldCamera.pixelRect.Contains(screen) || OverUi(screen)) return;
            float wheel = ScrollNotches(mouse.scroll.ReadValue().y, InputSystem.settings.scrollDeltaBehavior, UnityEngine.Application.platform);
            if (Mathf.Abs(wheel) > .00001f)
            {
                var before=worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));
                float old = Zoom; Zoom = CameraSettings.ZoomAfterScroll(Zoom, wheel);
                if (old != Zoom) followHordeFront = false;
                worldCamera.orthographicSize*=old/Zoom;
                var after=worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));
                CameraCenter+=new Vector2(before.x-after.x,before.y-after.y);
            }
            if (mouse.middleButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0) followHordeFront = false;
                CameraCenter -= delta * (worldCamera.orthographicSize * 2 / worldCamera.pixelHeight);
            }
            if (!followHordeFront) ClampCamera();
        }

        void ClampCamera()
        {
            ClampCamera(worldCamera.orthographicSize, worldCamera.aspect);
        }

        void ClampCamera(float halfY, float aspect)
        {
            float halfX = halfY * aspect;
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
        public void Cancel()
        {
            PlacementCell = null;
            gesture.Cancel(); erasedCells = 0; hasPressOrigin = false; PreviewCost = 0; PreviewReason = "";
            if (view != null) view.ClearPreview();
        }
        public void Focus(Cell cell)
        {
            followHordeFront = false;
            Cancel(); float old = Zoom; Zoom = Mathf.Clamp(Mathf.Max(1.5f, Zoom), 1, Mathf.Max(1, CameraSettings.MaximumZoom));
            CameraCenter = new Vector2(cell.X + .5f, cell.Y + .5f);
            if (worldCamera != null) { worldCamera.orthographicSize *= old / Zoom; ClampCamera(); }
        }

        /// <summary>Presentation framing only. The attempt, simulation clock and frozen loadout are unchanged.</summary>
        public void SetBattleView(bool overview)
        {
            if (run == null) return;
            followHordeFront = false;
            Cancel(); float previous = Zoom;
            Zoom = overview ? 1 : Mathf.Clamp(6, 1, Mathf.Max(1, CameraSettings.MaximumZoom));
            CameraCenter = overview
                ? new Vector2(run.Session.Balance.Width * .5f, run.Session.Balance.Height * .5f)
                : new Vector2(run.Session.BaseCell.X + .5f, run.Session.BaseCell.Y + .5f);
            if (worldCamera != null) { worldCamera.orthographicSize *= previous / Zoom; ClampCamera(); }
        }

        internal void FrameBattleOpening(Rect field)
        {
            var session = run.Session;
            followHordeFront = session.Balance.Waves.Count > 0 && session.Balance.Waves[0].IsHorde && session.Battlefield != null && worldCamera != null;
            if (!followHordeFront)
            {
                SetBattleView(false);
                return;
            }
            framedSides = 0;
            UpdateHordeFraming(field);
        }

        internal void UpdateHordeFraming(Rect field)
        {
            if (!followHordeFront || IsPlacingTower || gesture.Active) return;
            var session = run.Session;
            var balance = session.Balance;
            var wave = HordeWaveForecast.DisplayedWave(session);
            int sides = wave.Sides;
            if (sides == 0 || (framedSides == sides && framedBands == wave.EntranceBands && framedField == field)) return;
            framedBands = wave.EntranceBands;
            framedSides = sides;
            framedField = field;
            var map = session.Battlefield;
            // Use the new field aspect before the camera applies its new viewport in LateUpdate.
            Vector2 home = new Vector2(session.BaseCell.X + .5f, session.BaseCell.Y + .5f);
            Vector2 min = home - Vector2.one * 3, max = home + Vector2.one * 3;
            for (int gate = 0; gate < map.Entrances.Count; gate++)
            {
                if (!map.IsWaveEntrance(gate, sides, wave.EntranceBands)) continue;
                var entry = map.Entrances[gate];
                int side = map.Side(entry);
                var point = new Vector2(entry.X + .5f, entry.Y + .5f);
                Vector2 alongEdge = side == 1 || side == 2 ? Vector2.up : Vector2.right;
                Vector2 span = alongEdge * balance.EntranceHalfWidth + Vector2.one * 2;
                min = Vector2.Min(min, point - span);
                max = Vector2.Max(max, point + span);
            }
            float aspect = field.width / Mathf.Max(1, field.height);
            float wholeHalfHeight = Mathf.Max(balance.Height * .5f + 1, balance.Width * .5f / aspect + 1);
            Vector2 spanToFit = max - min;
            float halfHeight = .5f * Mathf.Max(spanToFit.y, spanToFit.x / aspect);
            Zoom = Mathf.Clamp(wholeHalfHeight / halfHeight, 1, Mathf.Max(1, CameraSettings.MaximumZoom));
            CameraCenter = (min + max) * .5f;
            worldCamera.orthographicSize = wholeHalfHeight / Zoom;
            ClampCamera(worldCamera.orthographicSize, aspect);
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
            if (run.UsesAlbumSlots)
            {
                if (k.digit5Key.wasPressedThisFrame) SelectSlot(4);
                if (k.digit6Key.wasPressedThisFrame) SelectSlot(5);
                if (k.digit7Key.wasPressedThisFrame) SelectSlot(6);
                if (k.digit8Key.wasPressedThisFrame) SelectSlot(7);
            }
            else
            {
                if (k.digit5Key.wasPressedThisFrame) SelectTool(StructureKind.Wall);
                if (k.digit6Key.wasPressedThisFrame) SelectTool(StructureKind.Spikes);
                if (k.digit7Key.wasPressedThisFrame) SelectTool(StructureKind.TarPit);
                if (k.digit8Key.wasPressedThisFrame) SelectTool(StructureKind.RepairPost);
            }
            if (k.eKey.wasPressedThisFrame) SelectMode(DrawingMode.Erase);
            if (k.uKey.wasPressedThisFrame) SelectMode(DrawingMode.Upgrade);
            if (k.bKey.wasPressedThisFrame) SelectMode(DrawingMode.Blotter);
            if (k.cKey.wasPressedThisFrame) SelectMode(DrawingMode.Paint);
            if (k.qKey.wasPressedThisFrame) SelectTier((MaterialTier)(((int)Tier + 1) % 3));
            if (HandleSessionHotkeys)
            {
                if (k.spaceKey.wasPressedThisFrame) run.TogglePause();
                if (k.nKey.wasPressedThisFrame) run.NextWave();
                if (k.fKey.wasPressedThisFrame) run.ToggleSpeed();
            }
            if (k.rKey.wasPressedThisFrame && HandleSessionHotkeys && !InspectedCell.HasValue && !IsPlacingTower) run.Restart();
            if (k.escapeKey.wasPressedThisFrame && (HandleSessionHotkeys || !IsPlacingTower)) Cancel();
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
