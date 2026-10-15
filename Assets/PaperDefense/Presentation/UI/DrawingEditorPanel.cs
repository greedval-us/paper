using System;
using System.Collections.Generic;
using PaperDefense.Core;
using PaperDefense.Application;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Workshop orchestration. Geometry belongs to the session; purchases belong to Application callbacks.</summary>
    public sealed class DrawingEditorPanel
    {
        readonly UserTextResolver textResolver;
        readonly DrawingWorkbenchUi ui;
        readonly GameObject shade, panel;
        readonly Action<DrawingSnapshot> apply;
        readonly Func<TowerBlueprint, string> preview;
        readonly DrawingEditorContext context;
        readonly UnityEngine.UI.Text message, rejectionMessage, partLabel, inkLabel, selectedStats, goal, help, zoomLabel, introduction, workingGeometry, statsHeading;
        readonly UnityEngine.UI.Button applyButton, undo, redo, grid, snap, geometry;
        FreeformTowerDesign previousDesign;
        TowerPreview? previousPreview;
        TowerPreview? openingPreview;
        string previousHash, changeBinding, changeMessage;
        readonly RectTransform inkFill;
        readonly Dictionary<string, UnityEngine.UI.Button> parts = new Dictionary<string, UnityEngine.UI.Button>(StringComparer.Ordinal);
        readonly Dictionary<DrawingTool, UnityEngine.UI.Button> tools = new Dictionary<DrawingTool, UnityEngine.UI.Button>();
        readonly DrawingStencilBrowser stencils;
        readonly UnityEngine.UI.Button stencilTab,probeTab;
        int probeMount;
        public DrawingProbePanel Probe { get; }
        GameObject partList;
        string partListSignature;
        public DrawingEditSession Session { get; private set; }
        public DrawingCanvasGraphic Canvas { get; }
        public bool IsOpen => panel.activeSelf;
        string Resolve(string value) => ui.Resolve(value);
        static string Copy(string key) => UserText.Message("drawing.workbench." + key);

        public DrawingEditorPanel(Transform parent, Font font, Action<DrawingSnapshot> apply, UserTextResolver textResolver = null,
            Func<TowerBlueprint, string> preview = null, DrawingEditorContext context = null)
        {
            this.textResolver = textResolver; this.apply = apply; this.preview = preview; this.context = context;
            ui = new DrawingWorkbenchUi(font, textResolver);
            shade = DrawingWorkbenchUi.Box(parent, "Drawing editor shade", new Rect(0, 0, 1600, 1000), new Color(.12f, .18f, .28f, .4f), true).gameObject;
            panel = DrawingWorkbenchUi.Box(parent, "Authored drawing editor", new Rect(20, 30, 1560, 940), NotebookPalette.Paper, true).gameObject;
            DrawingWorkbenchUi.Box(panel.transform, "Drawing header rule", new Rect(24, 96, 1512, 2), new Color(.35f, .5f, .65f, .3f));
            ui.Label(panel.transform, "Drawing title", Copy("title"), new Rect(24, 5, 1512, 52), 33, NotebookPalette.Blue);
            introduction = ui.Label(panel.transform, "Drawing introduction", Copy("intro"), new Rect(24, 57, 1512, 34), 22);
            ui.Label(panel.transform, "Drawing part heading", Copy("parts"), new Rect(24, 109, 218, 32), 21, NotebookPalette.Blue);
            ui.Label(panel.transform, "Drawing tool heading", Copy("tools"), new Rect(24, 295, 218, 32), 21, NotebookPalette.Blue);
            for (int i = 0; i < 6; i++)
            {
                var tool = (DrawingTool)i;
                tools.Add(tool, ui.Button(panel.transform, "Drawing " + tool.ToString().ToLowerInvariant(), new Rect(24, 332 + i * 44, 218, 38), ToolName(tool), () => Canvas.Tool = tool));
            }
            undo = ui.Button(panel.transform, "Drawing undo", new Rect(24, 608, 105, 40), Copy("undo"), () => Canvas.Undo());
            redo = ui.Button(panel.transform, "Drawing redo", new Rect(137, 608, 105, 40), Copy("redo"), () => Canvas.Redo());
            ui.Button(panel.transform, "Drawing mirror", new Rect(24, 660, 218, 40), Copy("mirror"), Mirror).GetComponentInChildren<UnityEngine.UI.Text>().fontSize = 17;
            ui.Label(panel.transform, "Drawing width label", Copy("width"), new Rect(24, 715, 115, 30), 20);
            ui.Button(panel.transform, "Drawing width smaller", new Rect(140, 708, 46, 38), Copy("smaller"), () => Resize(.9f, 1));
            ui.Button(panel.transform, "Drawing width larger", new Rect(196, 708, 46, 38), Copy("larger"), () => Resize(1.1f, 1));
            ui.Label(panel.transform, "Drawing height label", Copy("height"), new Rect(24, 761, 115, 30), 20);
            ui.Button(panel.transform, "Drawing height smaller", new Rect(140, 754, 46, 38), Copy("smaller"), () => Resize(1, .9f));
            ui.Button(panel.transform, "Drawing height larger", new Rect(196, 754, 46, 38), Copy("larger"), () => Resize(1, 1.1f));
            ui.Button(panel.transform, "Clear drawing part", new Rect(24, 807, 218, 40), Copy("clear"), () =>
            { Canvas.CancelGesture(); if (Session.ClearPart(Canvas.SelectedBinding)) ClearRejection(); Refresh(); });
            grid = ui.Button(panel.transform, "Drawing grid", new Rect(264, 112, 119, 40), Copy("grid"), () => Canvas.ShowGrid = !Canvas.ShowGrid);
            snap = ui.Button(panel.transform, "Drawing snap", new Rect(391, 112, 136, 40), Copy("snap"), () => Canvas.SnapToGrid = !Canvas.SnapToGrid);
            geometry = ui.Button(panel.transform, "Drawing geometry", new Rect(535, 112, 136, 40), Copy("geometry"), () => Canvas.ShowWorkingGeometry = !Canvas.ShowWorkingGeometry);
            ui.Button(panel.transform, "Drawing zoom out", new Rect(679, 112, 44, 40), Copy("smaller"), () => Canvas.Zoom *= .8f);
            zoomLabel = ui.Label(panel.transform, "Drawing zoom value", "", new Rect(732, 112, 95, 40), 21); zoomLabel.alignment = TextAnchor.MiddleCenter;
            ui.Button(panel.transform, "Drawing zoom in", new Rect(836, 112, 44, 40), Copy("larger"), () => Canvas.Zoom *= 1.25f);
            ui.Button(panel.transform, "Drawing fit", new Rect(888, 112, 184, 40), Copy("fit"), () => Canvas.Fit());
            var sheet = DrawingWorkbenchUi.Box(panel.transform, "Drawing viewport", new Rect(264, 165, 808, 496), new Color(1, .995f, .95f));
            sheet.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var canvas = new GameObject("Drawing canvas", typeof(RectTransform), typeof(CanvasRenderer), typeof(DrawingCanvasGraphic));
            canvas.transform.SetParent(sheet, false); DrawingWorkbenchUi.Place(canvas.GetComponent<RectTransform>(), new Rect(0, 0, 808, 496));
            Canvas = canvas.GetComponent<DrawingCanvasGraphic>(); Canvas.raycastTarget = true;
            workingGeometry = ui.Label(panel.transform, "Drawing working geometry", "", new Rect(264, 669, 808, 108), 18, NotebookPalette.Blue);
            partLabel = ui.Label(panel.transform, "Drawing selected part and tool", "", new Rect(264, 779, 808, 32), 21, NotebookPalette.Blue);
            rejectionMessage = ui.Label(panel.transform, "Drawing rejection", "", new Rect(264, 814, 808, 73), 19, NotebookPalette.Red);
            help = ui.Label(panel.transform, "Drawing gesture help", "", new Rect(264, 858, 808, 28), 17);
            ui.Label(panel.transform, "Drawing ink heading", Copy("ink_title"), new Rect(1096, 107, 440, 34), 22, NotebookPalette.Blue);
            inkLabel = ui.Label(panel.transform, "Drawing ink usage", "", new Rect(1096, 145, 440, 31), 21);
            var inkTrack = DrawingWorkbenchUi.Box(panel.transform, "Drawing ink capacity", new Rect(1096, 180, 440, 12), new Color(.25f, .42f, .61f, .15f));
            inkFill = DrawingWorkbenchUi.Box(inkTrack, "Drawing ink used", new Rect(0, 0, 0, 12), NotebookPalette.Blue);
            ui.Label(panel.transform, "Drawing ink explanation", Copy("ink_note"), new Rect(1096, 199, 440, 26), 17);
            statsHeading = ui.Label(panel.transform, "Drawing stats heading", Copy("stats_title"), new Rect(1096, 231, 440, 34), 22, NotebookPalette.Blue);
            message = ui.Label(panel.transform, "Drawing live stats", "", new Rect(1096, 267, 440, 90), 18);
            selectedStats = ui.Label(panel.transform, "Drawing selected mount stats", "", new Rect(1096, 359, 440, 100), 18);
            goal = ui.Label(panel.transform, "Drawing goal guidance", "", new Rect(1096, 465, 440, 82), 19);
            stencils = new DrawingStencilBrowser(panel.transform, ui, context, Insert, Feedback,context?.Probe==null);
            if(context?.Probe!=null)
            {
                Probe=new DrawingProbePanel(panel.transform,ui,context.Probe,()=>Canvas.HasPendingGesture,Refresh);
                stencilTab=ui.Button(panel.transform,"Drawing stencils tab",new Rect(1096,553,214,34),UserText.Message("drawing.probe.stencils"),()=>ShowProbe(false));
                probeTab=ui.Button(panel.transform,"Drawing probe tab",new Rect(1322,553,214,34),UserText.Message("drawing.probe.tab"),()=>ShowProbe(true));
                Canvas.GestureBegan+=Probe.Invalidate;
                ShowProbe(false);
            }
            ui.Button(panel.transform, "Cancel drawing", new Rect(24, 894, 218, 34), Copy("cancel"), () => Close(false));
            applyButton = ui.Button(panel.transform, "Apply drawing", new Rect(754, 894, 318, 34), Copy("apply"), () => Close(true), true);
            ui.Label(panel.transform, "Drawing shortcut hint", Copy("shortcuts"), new Rect(264, 889, 480, 48), 15);
            Canvas.Changed += Refresh; Canvas.Edited += ClearRejection; Canvas.Rejected += Feedback; Canvas.EscapeRequested += () => Close(false);
            Close(false);
        }
        public void Open(TowerBlueprint blueprint)
        {
            Probe?.Reset();probeMount=0;
            if(blueprint.WeaponMounts.Count>0)probeMount=blueprint.WeaponMounts[0].SlotIndex;
            ClearRejection();
            previousDesign = null; previousPreview = null; previousHash = changeBinding = changeMessage = null;
            var content = context?.Workshop?.Invoke()?.Content ?? blueprint.Content;
            blueprint = (content == null ? blueprint : blueprint.WithContent(content)).AsFreeform();
            openingPreview = context?.Preview?.Invoke(blueprint);
            introduction.text = Resolve(Copy(blueprint.FreeformRuleVersion < FreeformTowerDesign.RuleVersion ? "upgrade" : "intro"));
            Session = new DrawingEditSession(blueprint, blueprint.Drawing); Canvas.Bind(Session);
            RebuildParts();
            stencils.Bind((blueprint.Content ?? GameBalance.CreateDefault().ResolveContent()).DrawingUnlocks);
            shade.SetActive(true); panel.SetActive(true); Refresh();
        }
        public void Close(bool commit)
        {
            if (commit && Session != null)
            {
                Canvas.FinishStroke();
                var candidate = Session.Blueprint.WithFreeformDrawing(Session.Snapshot);
                var resolved = context?.Preview?.Invoke(candidate);
                var design = resolved?.FreeformDesign ?? candidate.FreeformDesign;
                if (!design.IsUsable) { Feedback(design.Message); Refresh(); return; }
                apply?.Invoke(Session.Snapshot);
            }
            Canvas.CancelGesture();Probe?.Reset();shade.SetActive(false); panel.SetActive(false);
        }
        public void ShowProbe(bool visible)
        {
            if(Probe==null)return;
            Probe.Show(visible);stencils.Show(!visible);
            stencilTab.GetComponent<UnityEngine.UI.Image>().color=visible?Color.white:DrawingWorkbenchUi.Selection;
            probeTab.GetComponent<UnityEngine.UI.Image>().color=visible?DrawingWorkbenchUi.Selection:Color.white;
        }
        void ClearRejection() { rejectionMessage.text = ""; PositionHelp(); }
        void Feedback(string reason) { Probe?.Invalidate();rejectionMessage.text = Resolve(reason); PositionHelp(); }
        void PositionHelp()
        {
            if (help == null) return;
            DrawingWorkbenchUi.Place(help.rectTransform, new Rect(264, 814, 808, 73));
            help.gameObject.SetActive(string.IsNullOrEmpty(rejectionMessage.text));
        }
        void Mirror()
        { Canvas.CancelGesture(); Command(Session.MirrorPartHorizontal(Canvas.SelectedBinding, out string reason), reason); }
        void Resize(float x, float y)
        { Canvas.CancelGesture(); Command(Session.ScalePart(Canvas.SelectedBinding, x, y, out string reason), reason); }
        void Insert(DrawingUnlockDefinition definition)
        { Canvas.CancelGesture(); Command(Session.ReplaceFromStencil(Canvas.SelectedBinding, definition, out string reason), reason); }
        void Command(bool changed, string reason)
        { if (changed) ClearRejection(); else if (!string.IsNullOrEmpty(reason)) Feedback(reason); Refresh(); }
        void Refresh()
        {
            if (Session == null) return;
            string signature = string.Join("|", DrawingTemplateCatalog.EnabledBindings(Session.Blueprint));
            if (signature != partListSignature) RebuildParts();
            foreach (var pair in parts) pair.Value.GetComponent<UnityEngine.UI.Image>().color = Canvas.SelectedBinding == pair.Key ? DrawingWorkbenchUi.Selection : Color.white;
            foreach (var pair in tools) pair.Value.GetComponent<UnityEngine.UI.Image>().color = Canvas.Tool == pair.Key ? DrawingWorkbenchUi.Selection : Color.white;
            grid.GetComponent<UnityEngine.UI.Image>().color = Canvas.ShowGrid ? DrawingWorkbenchUi.Selection : Color.white;
            snap.GetComponent<UnityEngine.UI.Image>().color = Canvas.SnapToGrid ? DrawingWorkbenchUi.Selection : Color.white;
            geometry.GetComponent<UnityEngine.UI.Image>().color = Canvas.ShowWorkingGeometry ? DrawingWorkbenchUi.Selection : Color.white;
            zoomLabel.text = Resolve(UserText.Message("drawing.workbench.zoom", Canvas.Zoom * 100));
            partLabel.text = Resolve(UserText.Message("drawing.workbench.selected", Name(Session.Blueprint, Canvas.SelectedBinding, textResolver), ToolName(Canvas.Tool)));
            undo.interactable = Session.CanUndo; redo.interactable = Session.CanRedo;
            var candidate = Session.Blueprint.WithFreeformDrawing(Session.Snapshot);
            var resolved = context?.Preview?.Invoke(candidate);
            var design = resolved?.FreeformDesign ?? candidate.FreeformDesign;
            Canvas.SetWorkingDesign(design);
            applyButton.interactable = design.IsUsable;
            inkLabel.text = Resolve(UserText.Message("drawing.workbench.ink", design.InkUsed, design.InkBudget, Copy(design.IsWithinBudget ? "ink_fit" : "ink_over")));
            inkLabel.color = design.IsWithinBudget ? NotebookPalette.Navy : NotebookPalette.Red;
            inkFill.sizeDelta = new Vector2(440 * Mathf.Clamp01(design.InkUsed / Mathf.Max(.01f, design.InkBudget)), 12);
            inkFill.GetComponent<UnityEngine.UI.Image>().color = design.IsWithinBudget ? NotebookPalette.Blue : NotebookPalette.Red;
            selectedStats.text = Resolve(Copy("select_mount"));
            if (resolved.HasValue)
            {
                var stats = resolved.Value;
                message.text = Resolve(UserText.Message("drawing.workbench.stats", stats.Health, stats.Load, stats.Capacity, stats.Cost, stats.BuildSeconds, stats.Range, stats.FireRate));
                bool comparable=openingPreview.HasValue&&openingPreview.Value.Valid&&stats.Valid;
                statsHeading.text=Resolve(comparable?UserText.Message("change.opening"):Copy("stats_title"));
                if(comparable)message.text=Resolve(DesignChangeFeedback.Tower(openingPreview.Value,stats));
                if (DrawingBindings.TryMount(Canvas.SelectedBinding, out int slot) && stats.Weapons != null)
                    foreach (var mount in stats.Weapons) if (mount.SlotIndex == slot)
                    {
                        selectedStats.text = Resolve(UserText.Message("drawing.workbench.mount_stats", slot + 1, mount.Damage, mount.Range, mount.FireRate, mount.Interval));
                        var original=comparable?DesignChangeFeedback.FindMount(openingPreview.Value,slot):null;
                        if(original.HasValue)selectedStats.text=Resolve(DesignChangeFeedback.Mount(original.Value,mount));
                    }
                if (stats.FeedMultiplier > 0 && stats.FeedMultiplier < .999f)
                    selectedStats.text += "\n" + Resolve(UserText.Message("drawing.workbench.feed", stats.FeedMultiplier));
            }
            else { statsHeading.text=Resolve(Copy("stats_title"));message.text = Resolve(preview != null ? preview(candidate) : design.Message); }
            goal.text = Resolve(Copy(Canvas.SelectedBinding == DrawingBindings.Base ? "body_goal" : DrawingBindings.TryMount(Canvas.SelectedBinding, out _) ? "barrel_goal" : "decoration_goal"));
            RefreshGeometry(candidate, design, resolved);
            help.text = Resolve(Copy(Canvas.Tool == DrawingTool.Pencil ? "pencil_hint" : Canvas.Tool == DrawingTool.Eraser ? "eraser_hint" : Canvas.Tool == DrawingTool.Move ? "move_hint" : "shape_hint"));
            if (!design.IsUsable) help.text = Resolve(Session.Snapshot.StrokeCount == 0 ? Copy("beginner") : design.Message);
            stencils.Refresh(Canvas.SelectedBinding); Canvas.SetVerticesDirty();
            if(DrawingBindings.TryMount(Canvas.SelectedBinding,out int selectedMount))probeMount=selectedMount;
            Probe?.Bind(candidate,probeMount,!design.IsUsable?design.Message:resolved.HasValue&&!resolved.Value.Valid?resolved.Value.Reason:null);
        }
        void RefreshGeometry(TowerBlueprint candidate, FreeformTowerDesign design, TowerPreview? resolved)
        {
            var rules = candidate.Content?.FreeformRules ?? FreeformRules.Default;
            string binding = Canvas.SelectedBinding;
            if (binding == DrawingBindings.Base)
                workingGeometry.text = Resolve(UserText.Message("drawing.workbench.body_geometry", design.BodyArea, rules.BodyAreaReference,
                    Copy(design.BodyGrowth >= 1 ? "body_limit" : "body_growth")));
            else if (DrawingBindings.TryMount(binding, out int slot) && design.GetBarrel(slot) != null)
            {
                var barrel = design.GetBarrel(slot);
                workingGeometry.text = Resolve(UserText.Message("drawing.workbench.barrel_geometry", barrel.Length, rules.BarrelLengthReference,
                    barrel.Width, rules.BarrelWidthReference, Copy(barrel.LengthGrowth >= 1 ? "length_limit" : "length_growth"),
                    Copy(barrel.WidthGrowth >= 1 ? "width_limit" : "width_growth")));
            }
            else workingGeometry.text = Resolve(Copy("decoration_geometry"));
            if (previousDesign != null && previousHash != candidate.Drawing.GeometryHash)
            {
                changeBinding = binding; changeMessage = null;
                if (binding == DrawingBindings.Base)
                    changeMessage = resolved.HasValue && previousPreview.HasValue
                        ? UserText.Message("drawing.workbench.body_change", Change(previousPreview.Value.Health, resolved.Value.Health),
                            Change(previousPreview.Value.Capacity, resolved.Value.Capacity), Change(previousDesign.InkUsed, design.InkUsed))
                        : UserText.Message("drawing.workbench.geometry_change", Change(previousDesign.BodyArea, design.BodyArea), Change(previousDesign.InkUsed, design.InkUsed));
                else if (DrawingBindings.TryMount(binding, out int mount))
                {
                    var before = previousDesign.GetBarrel(mount); var after = design.GetBarrel(mount);
                    var oldStats=previousPreview.HasValue?DesignChangeFeedback.FindMount(previousPreview.Value,mount):null;
                    var newStats=resolved.HasValue?DesignChangeFeedback.FindMount(resolved.Value,mount):null;
                    if(oldStats.HasValue&&newStats.HasValue)
                        changeMessage=UserText.Message("drawing.workbench.barrel_change",Change(oldStats.Value.Range,newStats.Value.Range),Change(oldStats.Value.Interval,newStats.Value.Interval),Change(oldStats.Value.Damage,newStats.Value.Damage));
                    else if (before != null && after != null && !resolved.HasValue) changeMessage = UserText.Message("drawing.workbench.barrel_change",
                        Change(before.RangeMultiplier, after.RangeMultiplier), Change(before.IntervalMultiplier, after.IntervalMultiplier), Change(before.DamageMultiplier, after.DamageMultiplier));
                }
            }
            if (changeBinding == binding && changeMessage != null) goal.text = Resolve(changeMessage);
            previousDesign = design; previousPreview = resolved; previousHash = candidate.Drawing.GeometryHash;
        }
        static string Change(float before, float after) => Copy(Mathf.Abs(after - before) < .00001f ? "unchanged" : after > before ? "increased" : "decreased");
        void RebuildParts()
        {
            if (partList != null) UnityEngine.Object.DestroyImmediate(partList);
            parts.Clear(); var templates = DrawingTemplateCatalog.Create(Session.Blueprint);
            var list = new CatalogScrollList(panel.transform, "Drawing parts", new Rect(24, 146, 218, 132), templates.Count, 1, 36, 6);
            partList = list.Scroll.gameObject; int i = 0;
            foreach (var template in templates)
            {
                string binding = template.BindingId;
                if (binding != DrawingBindings.Base && binding != DrawingBindings.Decoration && !DrawingBindings.TryMount(binding, out _)) continue;
                string suffix = DrawingBindings.TryLegacy(binding, out var key) ? key.ToString() : binding;
                var button = ui.Button(list.Content, "Draw part " + suffix, new Rect(0, i++ * 42, 210, 36), Name(Session.Blueprint, binding, textResolver), () => Canvas.SelectedBinding = binding);
                parts.Add(binding, button);
            }
            list.Content.sizeDelta = new Vector2(0, Mathf.Max(132, i * 42 - 6));
            partListSignature = string.Join("|", DrawingTemplateCatalog.EnabledBindings(Session.Blueprint));
            if (!parts.ContainsKey(Canvas.SelectedBinding)) Canvas.SelectedBinding = DrawingBindings.Base;
        }
        static string ToolName(DrawingTool tool) => Copy(tool.ToString().ToLowerInvariant());
        public static string Name(TowerBlueprint blueprint, string binding, UserTextResolver resolver = null)
        {
            if (binding == DrawingBindings.Base) return GameText.Resolve(resolver, UserText.Message("freeform.body"));
            if (DrawingBindings.TryMount(binding, out int slot)) return GameText.Resolve(resolver, UserText.Message("freeform.barrel", slot + 1));
            return GameText.Resolve(resolver, UserText.Message("freeform.decoration"));
        }
        public static string Name(DrawingPartKey part)
        {
            switch (part)
            {
                case DrawingPartKey.Base: return UserText.Message("copy.2b2ecd55ef989aaa");
                case DrawingPartKey.Mount0: return UserText.Message("copy.38309971cda7d7a9");
                case DrawingPartKey.Mount1: return UserText.Message("copy.fa4056869626bb97");
                case DrawingPartKey.Armor: return UserText.Message("copy.db1ac2c742f148a7");
                case DrawingPartKey.Feed: return UserText.Message("copy.6e4cee2bfd96ebbf");
                case DrawingPartKey.Tracking: return UserText.Message("copy.d31f6b535fce1f84");
                case DrawingPartKey.Rail: return UserText.Message("copy.82d9894b407a611a");
                default: return UserText.Message("copy.cab08f8099c1ba76");
            }
        }
    }
}
