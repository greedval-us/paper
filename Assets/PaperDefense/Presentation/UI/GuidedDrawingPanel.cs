using System;
using System.Collections.Generic;
using System.Text;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>A drawing step over the existing command session. Navigation owns saving and deployment.</summary>
    public sealed class GuidedDrawingPanel
    {
        sealed class StencilCard
        {
            public DrawingUnlockDefinition Definition;
            public UnityEngine.UI.Button Buy, Insert;
            public UnityEngine.UI.Text Ownership;
        }

        readonly WorkshopUi ui;
        readonly DrawingEditorContext context;
        readonly Action<TowerBlueprint> changed;
        readonly Transform main, summary;
        readonly GameObject emptyState, details, stencilPane;
        readonly RectTransform inkFill;
        readonly UnityEngine.UI.Text hint, emptyTitle, activeTool, ink, stats, mountStats, status, zoom, detailText, stencilTitle, stencilGold;
        readonly UnityEngine.UI.Button undo, redo, grid, snap, geometry, nextPart, detailsButton;
        readonly Dictionary<string, UnityEngine.UI.Button> parts = new Dictionary<string, UnityEngine.UI.Button>(StringComparer.Ordinal);
        readonly Dictionary<DrawingTool, UnityEngine.UI.Button> tools = new Dictionary<DrawingTool, UnityEngine.UI.Button>();
        readonly List<string> partOrder = new List<string>();
        readonly List<StencilCard> stencilCards = new List<StencilCard>();
        readonly UnityEngine.UI.ScrollRect detailScroll;
        GameObject partRoot, stencilList;
        string emittedHash, rejection, partSignature, stencilRole, stencilFeedback, previousGeometry;
        int rejectedCommands, probeMount;
        bool refreshing, detailsVisible;
        TowerPreview? openingPreview, previousPreview, lastChangePreview;

        public GameObject Root { get; }
        public DrawingEditSession Session { get; private set; }
        public DrawingCanvasGraphic Canvas { get; }
        public TowerBlueprint Candidate { get; private set; }
        public DrawingProbePanel Probe { get; }
        public event Action BackRequested;

        static string Copy(string key, params object[] args) => UserText.Message("wizard.draw." + key, args);
        string Resolve(string value) => ui.Resolve(value);

        public GuidedDrawingPanel(Transform parent, WorkshopUi ui, DrawingEditorContext context, Action<TowerBlueprint> changed)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.context = context; this.changed = changed;
            Root = ui.Panel(parent, "Guided drawing", new Rect(0, 0, 1600, 1000), Color.clear).gameObject;
            main = ui.Panel(Root.transform, "Guided drawing work area", new Rect(300, 110, 840, 780), Color.white, true);
            summary = ui.Panel(Root.transform, "Guided drawing summary", new Rect(1170, 110, 400, 780), WorkshopUi.Surface, true);
            ui.Label(main, "Guided drawing title", Copy("title"), new Rect(24, 14, 530, 44), 32, true);
            ui.Button(main, "Guided drawing stencils", new Rect(602, 18, 214, 42), Copy("stencils"), () => ShowStencils(true), WorkshopIcon.Book);
            hint = ui.Label(main, "Guided drawing hint", "", new Rect(24, 68, 792, 44), 19, color: WorkshopUi.Muted);

            WorkshopIcon[] toolIcons = { WorkshopIcon.Pencil, WorkshopIcon.Line, WorkshopIcon.Rectangle, WorkshopIcon.Ellipse, WorkshopIcon.Eraser, WorkshopIcon.Move };
            for (int i = 0; i < 6; i++)
            {
                var tool = (DrawingTool)i;
                string key = tool.ToString().ToLowerInvariant();
                var button = ui.IconButton(main, "Guided drawing tool " + tool, new Rect(24, 180 + i * 64, 52, 52), toolIcons[i],
                    Copy(key) + "\n" + Copy("tip_" + key), () => Canvas.Tool = tool);
                tools.Add(tool, button);
            }

            var viewport = ui.Panel(main, "Guided drawing viewport", new Rect(90, 174, 726, 470), WorkshopUi.Paper);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var canvasObject = new GameObject("Guided drawing canvas", typeof(RectTransform), typeof(CanvasRenderer), typeof(DrawingCanvasGraphic));
            canvasObject.transform.SetParent(viewport, false);
            WorkshopUi.Place(canvasObject.GetComponent<RectTransform>(), new Rect(0, 0, 726, 470));
            Canvas = canvasObject.GetComponent<DrawingCanvasGraphic>(); Canvas.raycastTarget = true;
            emptyState = ui.Panel(viewport, "Guided drawing empty state", new Rect(133, 158, 460, 154), Color.clear).gameObject;
            emptyTitle = ui.Label(emptyState.transform, "Guided drawing empty title", "", new Rect(0, 0, 460, 48), 27, true, WorkshopUi.Muted);
            emptyTitle.alignment = TextAnchor.MiddleCenter;
            var emptyHint = ui.Label(emptyState.transform, "Guided drawing empty hint", Copy("empty_hint"), new Rect(0, 52, 460, 28), 18, color: WorkshopUi.Muted);
            emptyHint.alignment = TextAnchor.MiddleCenter;
            ui.Button(emptyState.transform, "Guided drawing empty stencil", new Rect(120, 98, 220, 42), Copy("take_stencil"), () => ShowStencils(true), WorkshopIcon.Book);
            activeTool = ui.Label(viewport, "Guided drawing active tool", "", new Rect(18, 436, 250, 24), 17, color: WorkshopUi.Muted);

            undo = Icon(main, "undo", 24, WorkshopIcon.Undo, () => Canvas.Undo());
            redo = Icon(main, "redo", 72, WorkshopIcon.Redo, () => Canvas.Redo());
            Icon(main, "mirror", 134, WorkshopIcon.Mirror, Mirror);
            Icon(main, "clear", 182, WorkshopIcon.Trash, ClearPart);
            grid = Icon(main, "grid", 260, WorkshopIcon.Grid, () => Canvas.ShowGrid = !Canvas.ShowGrid);
            snap = Icon(main, "snap", 308, WorkshopIcon.Magnet, () => Canvas.SnapToGrid = !Canvas.SnapToGrid);
            geometry = Icon(main, "geometry", 356, WorkshopIcon.Tower, () => Canvas.ShowWorkingGeometry = !Canvas.ShowWorkingGeometry);
            Icon(main, "zoom_out", 470, WorkshopIcon.ZoomOut, () => Canvas.Zoom *= .8f);
            zoom = ui.Label(main, "Guided drawing zoom value", "", new Rect(516, 658, 72, 38), 18);
            zoom.alignment = TextAnchor.MiddleCenter;
            Icon(main, "zoom_in", 590, WorkshopIcon.ZoomIn, () => Canvas.Zoom *= 1.25f);
            ui.Button(main, "Guided drawing fit", new Rect(650, 658, 166, 38), Copy("fit"), () => Canvas.Fit(), WorkshopIcon.Fit);
            ui.Label(main, "Guided drawing width label", Copy("width"), new Rect(24, 719, 92, 34), 18);
            SmallButton("width smaller", new Rect(118, 710, 40, 44), Copy("minus"), () => Resize(.9f, 1), Copy("width_smaller"));
            SmallButton("width larger", new Rect(164, 710, 40, 44), Copy("plus"), () => Resize(1.1f, 1), Copy("width_larger"));
            ui.Label(main, "Guided drawing height label", Copy("height"), new Rect(238, 719, 92, 34), 18);
            SmallButton("height smaller", new Rect(332, 710, 40, 44), Copy("minus"), () => Resize(1, .9f), Copy("height_smaller"));
            SmallButton("height larger", new Rect(378, 710, 40, 44), Copy("plus"), () => Resize(1, 1.1f), Copy("height_larger"));
            nextPart = ui.Button(main, "Guided drawing next part", new Rect(548, 710, 268, 44), Copy("next_part"), SelectNextPart, WorkshopIcon.Next);

            ui.Label(summary, "Guided drawing summary title", Copy("summary"), new Rect(24, 18, 352, 42), 29, true);
            ink = ui.Label(summary, "Guided drawing ink", "", new Rect(24, 72, 352, 30), 20);
            var track = ui.Panel(summary, "Guided drawing ink track", new Rect(24, 110, 352, 8), WorkshopUi.Line);
            inkFill = ui.Panel(track, "Guided drawing ink fill", new Rect(0, 0, 0, 8), WorkshopUi.Blue);
            stats = ui.Label(summary, "Guided drawing tower stats", "", new Rect(24, 138, 352, 72), 20);
            mountStats = ui.Label(summary, "Guided drawing mount stats", "", new Rect(24, 218, 352, 72), 19);
            status = ui.Label(summary, "Guided drawing status", "", new Rect(24, 302, 352, 44), 18, color: WorkshopUi.Muted);
            detailsButton = ui.Button(summary, "Guided drawing details toggle", new Rect(24, 354, 352, 40), Copy("details"), () => ShowDetails(!detailsVisible), WorkshopIcon.Chevron);
            var detailList = new CatalogScrollList(summary, "Guided drawing details", new Rect(24, 406, 352, 350), 2, 1, 350, 0);
            detailScroll = detailList.Scroll; details = detailScroll.gameObject;
            detailText = ui.Label(detailList.Content, "Guided drawing detailed values", "", new Rect(0, 0, 334, 350), 18);
            if (context?.Probe != null)
            {
                // Reuse the same one-shot simulation and invalidation policy as the full editor.
                Probe = new DrawingProbePanel(summary, new DrawingWorkbenchUi(ui.BodyFont, ui.TextResolver),
                    context.Probe, () => Canvas.HasPendingGesture, Refresh, new Rect(24, 406, 352, 350));
                StyleProbe();
            }

            stencilPane = ui.Panel(Root.transform, "Guided drawing stencil catalogue", new Rect(300, 110, 840, 780), WorkshopUi.Paper, true).gameObject;
            stencilTitle = ui.Label(stencilPane.transform, "Guided drawing stencil title", "", new Rect(24, 20, 720, 44), 30, true);
            ui.IconButton(stencilPane.transform, "Guided drawing close stencils", new Rect(768, 22, 48, 42), WorkshopIcon.Close, Copy("close_stencils"), () => ShowStencils(false));
            ui.Label(stencilPane.transform, "Guided drawing stencil guidance", Copy("stencil_hint"), new Rect(24, 78, 792, 42), 18, color: WorkshopUi.Muted);
            stencilGold = ui.Label(stencilPane.transform, "Guided drawing stencil gold", "", new Rect(24, 120, 792, 40), 18);
            ui.Label(stencilPane.transform, "Guided drawing stencil replacement", Copy("stencil_replaces"), new Rect(24, 734, 792, 36), 17, color: WorkshopUi.Muted);
            Canvas.Changed += Refresh;
            Canvas.Edited += () => rejection = null;
            Canvas.Rejected += Reject;
            Canvas.GestureBegan += () => { Probe?.Invalidate(); emptyState.SetActive(false); };
            Canvas.EscapeRequested += () => BackRequested?.Invoke();
            ShowDetails(false); stencilPane.SetActive(false); Root.SetActive(false);
        }

        UnityEngine.UI.Button Icon(Transform parent, string key, float x, WorkshopIcon icon, Action action)
            => ui.IconButton(parent, "Guided drawing " + key, new Rect(x, 658, 42, 38), icon, Copy(key), action);

        void SmallButton(string name, Rect box, string caption, Action action, string tip)
        { var button = ui.Button(main, "Guided drawing " + name, box, caption, action); ui.Tip(button.gameObject, tip); }

        void StyleProbe()
        {
            foreach (var label in summary.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (label.name.StartsWith("Drawing probe", StringComparison.Ordinal)) { label.font = ui.BodyFont; label.color = WorkshopUi.Ink; }
            foreach (var button in summary.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button.name.StartsWith("Drawing probe", StringComparison.Ordinal))
                {
                    button.GetComponent<UnityEngine.UI.Image>().color = button.name == "Drawing probe fire" ? WorkshopUi.Blue : Color.white;
                    if (button.name == "Drawing probe fire") button.GetComponentInChildren<UnityEngine.UI.Text>().color = Color.white;
                }
            var viewport = summary.Find("Drawing probe pane/Drawing probe viewport");
            if (viewport != null) viewport.GetComponent<UnityEngine.UI.Image>().color = WorkshopUi.Paper;
        }

        public void Open(TowerBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            var content = context?.Workshop?.Invoke()?.Content ?? blueprint.Content;
            blueprint = (content == null ? blueprint : blueprint.WithContent(content)).AsFreeform();
            Canvas.CancelGesture(); rejection = null;
            if (Session == null || !SameDraft(Candidate, blueprint))
            {
                openingPreview = context?.Preview?.Invoke(blueprint);
                previousPreview = lastChangePreview = null; previousGeometry = null;
                Session = new DrawingEditSession(blueprint, blueprint.Drawing); Canvas.Bind(Session);
                Candidate = blueprint; emittedHash = Session.Snapshot.GeometryHash;
                probeMount = blueprint.WeaponMounts[0].SlotIndex; Probe?.Reset();
                partSignature = null; stencilRole = null;
            }
            Root.SetActive(true); ShowStencils(false); ShowDetails(false); Refresh();
        }

        public void Hide()
        {
            Canvas.CancelGesture(); Probe?.Reset(); ui.HideTooltip(); stencilPane.SetActive(false); Root.SetActive(false);
        }

        public bool TryFinish(out TowerBlueprint candidate, out string reason)
        {
            candidate = null; reason = "";
            if (Session == null) { reason = Copy("not_open"); return false; }
            bool pending = Canvas.HasPendingGesture; string before = Session.Snapshot.GeometryHash; int rejected = rejectedCommands;
            Canvas.FinishStroke(); Canvas.CancelGesture(); Refresh();
            if (rejectedCommands != rejected) { reason = rejection; return false; }
            if (pending && before == Session.Snapshot.GeometryHash) { reason = Copy("unfinished"); Reject(reason); Refresh(); return false; }
            var resolved = context?.Preview?.Invoke(Candidate);
            var design = resolved?.FreeformDesign ?? Candidate.FreeformDesign;
            if (!design.IsUsable) reason = design.Message;
            else if (resolved.HasValue && !resolved.Value.Valid) reason = resolved.Value.Reason;
            if (!string.IsNullOrEmpty(reason)) { Reject(reason); Refresh(); return false; }
            candidate = Candidate; return true;
        }

        public void ShowDetails(bool visible)
        {
            detailsVisible = visible; details.SetActive(visible); Probe?.Show(!visible);
            ui.SetSelected(detailsButton, visible); ui.HideTooltip();
        }

        public void ShowStencils(bool visible)
        {
            if (visible && !stencilPane.activeSelf) stencilFeedback = null;
            Canvas.CancelGesture(); ui.HideTooltip(); stencilPane.SetActive(visible);
            // The covered drawing Graphic must not receive keyboard edits behind the catalogue.
            Canvas.gameObject.SetActive(!visible);
            if (visible && Session != null) { RefreshStencils(); WorkshopModal.Open(stencilPane, () => ShowStencils(false)); ui.HideTooltip(); }
        }

        public void SelectNextPart()
        {
            if (Session == null || partOrder.Count == 0) return;
            int selected = partOrder.IndexOf(Canvas.SelectedBinding);
            Canvas.SelectedBinding = partOrder[(selected + 1) % partOrder.Count];
            Canvas.Fit(); rejection = null; Refresh();
        }

        void Reject(string reason)
        { rejectedCommands++; rejection = reason; Probe?.Invalidate(); Refresh(); }

        void Mirror()
        { if (Session == null) return; Canvas.CancelGesture(); bool success = Session.MirrorPartHorizontal(Canvas.SelectedBinding, out string reason); Command(success, reason); }
        void Resize(float width, float height)
        { if (Session == null) return; Canvas.CancelGesture(); bool success = Session.ScalePart(Canvas.SelectedBinding, width, height, out string reason); Command(success, reason); }
        void ClearPart()
        { if (Session == null) return; Canvas.CancelGesture(); Command(Session.ClearPart(Canvas.SelectedBinding), ""); }
        void Command(bool success, string reason)
        { if (success) rejection = null; else if (!string.IsNullOrEmpty(reason)) Reject(reason); Refresh(); }

        public void Refresh()
        {
            if (Session == null || refreshing) return;
            TowerBlueprint notify = null; refreshing = true;
            try
            {
                string signature = string.Join("|", DrawingTemplateCatalog.EnabledBindings(Session.Blueprint));
                if (signature != partSignature) RebuildParts(signature);
                Candidate = Session.Blueprint.WithFreeformDrawing(Session.Snapshot);
                var resolved = context?.Preview?.Invoke(Candidate);
                var design = resolved?.FreeformDesign ?? Candidate.FreeformDesign;
                if (previousGeometry != Session.Snapshot.GeometryHash)
                {
                    if (previousPreview.HasValue && previousPreview.Value.Valid) lastChangePreview = previousPreview;
                    previousPreview = resolved; previousGeometry = Session.Snapshot.GeometryHash;
                }
                Canvas.SetWorkingDesign(design);
                if (DrawingBindings.TryMount(Canvas.SelectedBinding, out int mount)) probeMount = mount;
                foreach (var part in parts) ui.SetSelected(part.Value, part.Key == Canvas.SelectedBinding);
                foreach (var tool in tools) ui.SetSelected(tool.Value, tool.Key == Canvas.Tool);
                activeTool.text = Resolve(Copy(Canvas.Tool.ToString().ToLowerInvariant()));
                ui.SetSelected(grid, Canvas.ShowGrid); ui.SetSelected(snap, Canvas.SnapToGrid); ui.SetSelected(geometry, Canvas.ShowWorkingGeometry);
                undo.interactable = Session.CanUndo; redo.interactable = Session.CanRedo;
                zoom.text = Resolve(Copy("zoom", Canvas.Zoom * 100));
                hint.text = Resolve(!string.IsNullOrEmpty(rejection) ? rejection : Guidance(design));
                hint.color = string.IsNullOrEmpty(rejection) ? WorkshopUi.Muted : WorkshopUi.Warning;
                bool empty = Session.Snapshot.GetPart(Canvas.SelectedBinding).Strokes.Count == 0;
                emptyState.SetActive(empty && !Canvas.HasPendingGesture);
                emptyTitle.text = Resolve(Copy(Canvas.SelectedBinding == DrawingBindings.Base ? "empty_body" : DrawingBindings.TryMount(Canvas.SelectedBinding, out _) ? "empty_barrel" : "empty_decoration"));
                ink.text = Resolve(Copy("ink", design.InkUsed, design.InkBudget));
                ink.color = design.IsWithinBudget ? WorkshopUi.Ink : WorkshopUi.Warning;
                inkFill.sizeDelta = new Vector2(352 * Mathf.Clamp01(design.InkUsed / Mathf.Max(.01f, design.InkBudget)), 8);
                inkFill.GetComponent<UnityEngine.UI.Graphic>().color = design.IsWithinBudget ? WorkshopUi.Blue : WorkshopUi.Warning;
                stats.text = resolved.HasValue ? Resolve(Copy("tower_stats", resolved.Value.Health, resolved.Value.Load, resolved.Value.Capacity, resolved.Value.Cost)) : Resolve(Copy("geometry_stats", design.BodyArea));
                var weapon = resolved.HasValue ? DesignChangeFeedback.FindMount(resolved.Value, probeMount) : null;
                mountStats.text = weapon.HasValue ? Resolve(Copy("mount_stats", probeMount + 1, weapon.Value.Damage, weapon.Value.Range, weapon.Value.Interval)) : Resolve(Copy("mount_waiting"));
                bool valid = design.IsUsable && (!resolved.HasValue || resolved.Value.Valid);
                status.text = Resolve(valid ? Copy("ready") : !design.IsWithinBudget ? Copy("over_budget") : !design.IsComplete ? Copy("missing_parts") : resolved.Value.Reason);
                status.color = valid ? WorkshopUi.Success : WorkshopUi.Muted;
                int next = (partOrder.IndexOf(Canvas.SelectedBinding) + 1) % partOrder.Count;
                nextPart.GetComponentInChildren<UnityEngine.UI.Text>().text = Resolve(Copy("to_part", PartName(partOrder[next])));
                RefreshDetails(design, resolved);
                if (stencilPane.activeSelf) RefreshStencils();
                Probe?.Bind(Candidate, probeMount, !design.IsUsable ? design.Message : resolved.HasValue && !resolved.Value.Valid ? resolved.Value.Reason : null);
                if (emittedHash != Session.Snapshot.GeometryHash)
                { emittedHash = Session.Snapshot.GeometryHash; notify = Candidate; }
            }
            finally { refreshing = false; }
            if (notify != null) changed?.Invoke(notify);
        }

        string Guidance(FreeformTowerDesign design)
        {
            if (!design.IsWithinBudget) return Copy("hint_ink");
            if (Canvas.Tool == DrawingTool.Eraser || Canvas.Tool == DrawingTool.Move) return Copy("tip_" + Canvas.Tool.ToString().ToLowerInvariant());
            if (Canvas.SelectedBinding == DrawingBindings.Base) return Copy("hint_body");
            if (DrawingBindings.TryMount(Canvas.SelectedBinding, out int slot)) return Copy("hint_barrel", slot + 1);
            return Copy("hint_decoration");
        }

        void RebuildParts(string signature)
        {
            if (partRoot != null) UnityEngine.Object.DestroyImmediate(partRoot);
            parts.Clear(); partOrder.Clear();
            partOrder.Add(DrawingBindings.Base);
            foreach (var mount in Session.Blueprint.WeaponMounts) partOrder.Add(DrawingBindings.Mount(mount.SlotIndex));
            partOrder.Add(DrawingBindings.Decoration);
            partRoot = ui.Panel(main, "Guided drawing parts", new Rect(24, 116, 792, 42), Color.clear).gameObject;
            float width = (792 - (partOrder.Count - 1) * 8) / partOrder.Count;
            for (int i = 0; i < partOrder.Count; i++)
            {
                string binding = partOrder[i];
                var button = ui.Button(partRoot.transform, "Guided drawing part " + binding, new Rect(i * (width + 8), 0, width, 42), PartName(binding),
                    () => { rejection = null; Canvas.SelectedBinding = binding; Canvas.Fit(); },
                    binding == DrawingBindings.Base ? WorkshopIcon.Tower : binding == DrawingBindings.Decoration ? WorkshopIcon.Pencil : WorkshopIcon.Weapon);
                button.GetComponentInChildren<UnityEngine.UI.Text>().fontSize = 17;
                parts.Add(binding, button);
            }
            partSignature = signature;
            if (!parts.ContainsKey(Canvas.SelectedBinding)) Canvas.SelectedBinding = DrawingBindings.Base;
        }

        static string PartName(string binding)
            => binding == DrawingBindings.Base ? Copy("body") : DrawingBindings.TryMount(binding, out int slot) ? Copy("barrel", slot + 1) : Copy("decoration");

        void RefreshDetails(FreeformTowerDesign design, TowerPreview? resolved)
        {
            var text = new StringBuilder();
            if (resolved.HasValue)
            {
                bool compareOpening = openingPreview.HasValue && openingPreview.Value.Valid;
                var comparison = compareOpening ? openingPreview : lastChangePreview;
                if (comparison.HasValue && comparison.Value.Valid && resolved.Value.Valid)
                {
                    text.AppendLine(Resolve(Copy(compareOpening ? "before_after" : "last_change")));
                    text.AppendLine(Resolve(DesignChangeFeedback.Tower(comparison.Value, resolved.Value)));
                    var before = DesignChangeFeedback.FindMount(comparison.Value, probeMount);
                    var after = DesignChangeFeedback.FindMount(resolved.Value, probeMount);
                    if (before.HasValue && after.HasValue) text.AppendLine(Resolve(DesignChangeFeedback.Mount(before.Value, after.Value)));
                }
                else text.AppendLine(Resolve(UserText.Message("drawing.workbench.stats", resolved.Value.Health, resolved.Value.Load, resolved.Value.Capacity,
                    resolved.Value.Cost, resolved.Value.BuildSeconds, resolved.Value.Range, resolved.Value.FireRate)));
                if (resolved.Value.FeedMultiplier > 0 && resolved.Value.FeedMultiplier < .999f)
                    text.AppendLine(Resolve(UserText.Message("drawing.workbench.feed", resolved.Value.FeedMultiplier)));
            }
            var rules = Candidate.Content?.FreeformRules ?? FreeformRules.Default;
            text.AppendLine(); text.AppendLine(Resolve(Copy("working_geometry")));
            text.AppendLine(Resolve(Copy("body_geometry", design.BodyArea, rules.BodyAreaReference)));
            foreach (var mount in Candidate.WeaponMounts)
            {
                var barrel = design.GetBarrel(mount.SlotIndex);
                if (barrel != null) text.AppendLine(Resolve(Copy("barrel_geometry", mount.SlotIndex + 1, barrel.Length, rules.BarrelLengthReference, barrel.Width, rules.BarrelWidthReference)));
            }
            text.AppendLine(); text.AppendLine(Resolve(Copy("rules")));
            text.AppendLine(Resolve(Copy("minimums", rules.MinimumBodyArea, rules.MinimumBarrelLength, rules.MinimumMuzzleForward)));
            if (!design.IsUsable) text.AppendLine(Resolve(design.Message));
            text.AppendLine(); text.Append(Resolve(Copy("shortcuts")));
            detailText.text = text.ToString();
            float height = Mathf.Max(350, detailText.preferredHeight + 12);
            detailText.rectTransform.sizeDelta = new Vector2(334, height);
            detailScroll.content.sizeDelta = new Vector2(0, height);
        }

        void RefreshStencils()
        {
            string role = Canvas.SelectedBinding == DrawingBindings.Base ? "body" : DrawingBindings.TryMount(Canvas.SelectedBinding, out _) ? "barrel" : "decoration";
            var workshop = context?.Workshop?.Invoke();
            stencilTitle.text = Resolve(Copy("stencil_title", PartName(Canvas.SelectedBinding)));
            stencilGold.text = Resolve(stencilFeedback ?? (workshop != null ? Copy("gold", workshop.Gold) : Copy("shop_offline")));
            stencilGold.color = string.IsNullOrEmpty(stencilFeedback) ? WorkshopUi.Ink : WorkshopUi.Warning;
            if (stencilRole != role)
            {
                if (stencilList != null) UnityEngine.Object.DestroyImmediate(stencilList);
                stencilCards.Clear(); stencilRole = role;
                var definitions = new List<DrawingUnlockDefinition>();
                foreach (var definition in (Candidate.Content ?? GameBalance.CreateDefault().ResolveContent()).DrawingUnlocks)
                    if ((role == "body" && definition.Role == DrawingStencilRole.Body) || (role == "barrel" && definition.Role == DrawingStencilRole.Barrel)) definitions.Add(definition);
                var list = new CatalogScrollList(stencilPane.transform, "Guided drawing stencil list", new Rect(24, 168, 792, 552), definitions.Count, 2, 258, 16);
                stencilList = list.Scroll.gameObject;
                if (definitions.Count == 0) ui.Label(list.Content, "Guided drawing no stencils", Copy("no_stencils"), new Rect(24, 30, 730, 90), 22);
                for (int i = 0; i < definitions.Count; i++) CreateStencilCard(list.Content, definitions[i], i);
            }
            foreach (var card in stencilCards)
            {
                bool owned = card.Definition.InitiallyUnlocked || (workshop != null && workshop.IsDrawingUnlocked(card.Definition.Id));
                card.Ownership.text = Resolve(owned ? Copy(card.Definition.InitiallyUnlocked ? "free" : "owned") : Copy("price", card.Definition.GoldCost));
                card.Buy.gameObject.SetActive(!owned);
                card.Buy.interactable = !owned && context?.Purchase != null && workshop != null && workshop.Gold >= card.Definition.GoldCost;
                card.Insert.interactable = owned;
            }
        }

        void CreateStencilCard(Transform parent, DrawingUnlockDefinition definition, int index)
        {
            var box = ui.Panel(parent, "Guided stencil " + definition.Id, new Rect((index % 2) * 398, (index / 2) * 274, 386, 258), Color.white, true);
            var go = new GameObject("Guided stencil preview " + definition.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(DrawingStencilGraphic));
            go.transform.SetParent(box, false); WorkshopUi.Place(go.GetComponent<RectTransform>(), new Rect(14, 18, 78, 120));
            var graphic = go.GetComponent<DrawingStencilGraphic>(); graphic.Strokes = definition.Strokes; graphic.raycastTarget = false;
            ui.Label(box, "Guided stencil name", definition.Name, new Rect(108, 16, 260, 52), 20, color: WorkshopUi.Blue);
            ui.Label(box, "Guided stencil description", definition.Description, new Rect(108, 72, 260, 112), 17);
            var card = new StencilCard { Definition = definition };
            card.Ownership = ui.Label(box, "Guided stencil ownership", "", new Rect(16, 182, 352, 27), 18, color: WorkshopUi.Muted);
            card.Buy = ui.Button(box, "Guided buy stencil " + definition.Id, new Rect(16, 214, 168, 36), Copy("buy"), () => PurchaseStencil(definition), WorkshopIcon.Cost);
            card.Insert = ui.Button(box, "Guided insert stencil " + definition.Id, new Rect(200, 214, 168, 36), Copy("insert"), () => InsertStencil(definition), WorkshopIcon.Plus, true);
            stencilCards.Add(card);
        }

        void PurchaseStencil(DrawingUnlockDefinition definition)
        {
            var workshop = context?.Workshop?.Invoke();
            if (definition.InitiallyUnlocked || (workshop != null && workshop.IsDrawingUnlocked(definition.Id))) return;
            if (workshop == null || context?.Purchase == null) { stencilFeedback = Copy("shop_offline"); Reject(stencilFeedback); return; }
            if (!context.Purchase(definition.Id, out string reason))
            { stencilFeedback = reason; Reject(reason); return; }
            stencilFeedback = null; rejection = null;
            Refresh();
        }

        void InsertStencil(DrawingUnlockDefinition definition)
        {
            var workshop = context?.Workshop?.Invoke();
            if (!definition.InitiallyUnlocked && (workshop == null || !workshop.IsDrawingUnlocked(definition.Id)))
            { stencilFeedback = Copy("locked"); RefreshStencils(); return; }
            Canvas.CancelGesture();
            bool success = Session.ReplaceFromStencil(Canvas.SelectedBinding, definition, out string reason);
            ShowStencils(false); Command(success, reason);
        }

        static bool SameDraft(TowerBlueprint before, TowerBlueprint after)
        {
            if (before == null || before.Id != after.Id || before.Name != after.Name || before.ChassisId != after.ChassisId ||
                before.Drawing.GeometryHash != after.Drawing.GeometryHash || before.FreeformRuleVersion != after.FreeformRuleVersion ||
                !ReferenceEquals(before.Content, after.Content) || !ReferenceEquals(before.Reinforcement, after.Reinforcement) ||
                before.WeaponMounts.Count != after.WeaponMounts.Count || before.ModuleSelections.Count != after.ModuleSelections.Count) return false;
            for (int i = 0; i < before.WeaponMounts.Count; i++)
            {
                var a = before.WeaponMounts[i]; var b = after.WeaponMounts[i];
                if (a.SlotIndex != b.SlotIndex || a.WeaponId != b.WeaponId || !a.Footprint.SameShape(b.Footprint) || !ReferenceEquals(a.Volley, b.Volley)) return false;
            }
            for (int i = 0; i < before.ModuleSelections.Count; i++)
                if (before.ModuleSelections[i].InstanceId != after.ModuleSelections[i].InstanceId || before.ModuleSelections[i].DefinitionId != after.ModuleSelections[i].DefinitionId) return false;
            return true;
        }
    }

}
