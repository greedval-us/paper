using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    // Serialized historical mode values remain readable; every released entrance uses the album.
    public enum WorkshopHudMode { Full = 0, Assembly = 1, Drawing = 2, Profile = 3 }

    /// <summary>Hosts the composite album for player and historical bootstrap entrances.</summary>
    public sealed class WorkshopHud : MonoBehaviour
    {
        public UserTextResolver TextResolver { get; set; }
        public WorkshopHudMode Mode { get; set; }
        public Action StartRequested { get; set; }
        public bool UseGuidedFlow { get; set; }
        public WorkshopWizard Guided => null;
        public CompositeWorkshopPanel Composite { get; private set; }
        public TowerBlueprint Draft { get; private set; }
        public string Status => Composite?.Status ?? unavailableStatus;
        string unavailableStatus = "";
        RunController run;
        GameObject canvasObject;
        RectTransform board;
        bool presentationVisible = true;
        float refresh;

        public bool PresentationVisible
        {
            get => presentationVisible;
            set
            {
                bool changed = presentationVisible != value;
                presentationVisible = value;
                if (canvasObject != null) canvasObject.SetActive(value && run.InWorkshop);
                if (!changed || Composite == null) return;
                if (value) Composite.Resume(); else Composite.Suspend();
            }
        }

        public void Initialize(RunController controller)
        {
            run = controller ?? throw new ArgumentNullException(nameof(controller));
            canvasObject = new GameObject("Workshop HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var paper = new GameObject("Workshop paper", typeof(RectTransform), typeof(Image));
            paper.transform.SetParent(canvasObject.transform, false); board = paper.GetComponent<RectTransform>();
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(.5f, .5f); board.sizeDelta = new Vector2(1600, 1000);
            paper.GetComponent<Image>().color = NotebookPalette.Paper; paper.GetComponent<Image>().raycastTarget = true;
            Draft = run.Workshop.Album.Count > 0 ? run.Workshop.Album[0] : null;
            if (run.AssemblyCatalog != null)
                Composite = new CompositeWorkshopPanel(board, run, TextResolver, Prepare);
            else
            {
                // Historical content can still be read and rendered, but cannot reopen retired editors.
                unavailableStatus = "Для мастерской нужен каталог составного альбома. Откройте основную игру.";
                new WorkshopUi(TextResolver).Label(board, "Album unavailable", Status, new Rect(160, 360, 1280, 180), 32);
            }
            run.WorkshopChanged += Refresh;
            Refresh();
        }

        void Prepare()
        {
            if (StartRequested != null) { StartRequested(); return; }
            if (!run.StartRun(out var reason)) Composite.ResumeWithFeedback(reason);
        }

        public void SetPlayerLayout()
        {
            if (Composite == null) return;
            var responsive = canvasObject.GetComponent<WorkshopResponsiveLayout>() ?? canvasObject.AddComponent<WorkshopResponsiveLayout>();
            responsive.InitializeComposite(board, Composite);
        }

        void Refresh()
        {
            if (canvasObject == null) return;
            canvasObject.SetActive(run.InWorkshop && PresentationVisible);
            if (!run.InWorkshop) Composite?.Suspend();
            else Composite?.RefreshStatus();
        }
        void Update()
        {
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0) return;
            refresh = .15f; Refresh();
        }
        void OnDestroy() { if (run != null) run.WorkshopChanged -= Refresh; }

        internal static string WeaponName(StructureKind kind)
            => PartCatalog.TryGetMount(kind, out var part) ? part.ButtonLabel : kind.ToString().ToUpperInvariant();
        internal static string ColorName(InkColor color) => color == InkColor.Red ? UserText.Message("copy.bd395e180a485a0a") : color == InkColor.Blue ? UserText.Message("copy.3b6332e897ab71c4") : color == InkColor.Green ? UserText.Message("copy.05142bc07be813fc") : UserText.Message("copy.715c066d1a65e690");
    }

    /// <summary>A live hand-drawn diagram, including both weapon mounts and armor plates.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BlueprintSketch : NotebookAtlasGraphic
    {
        TowerBlueprint blueprint;
        NotebookAnimationState previewAnimation;
        Func<int, Point2> previewAim;
        double previewSeconds;
        public void SetPreviewPose(NotebookAnimationState animation, double seconds, Func<int, Point2> aim)
        { previewAnimation = animation; previewSeconds = seconds; previewAim = aim; SetVerticesDirty(); }
        public bool ShowReference { get; set; }
        public bool ShowReinforcement { get; set; } = true;
        public void SetBlueprint(TowerBlueprint value) { if (blueprint == value) return; blueprint = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (blueprint == null || DrawingTemplateCatalog.Create(blueprint).Count == 0) return;
            Vector2 center = rectTransform.rect.center;
            if (!ShowReference)
            {
                float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .68f;
                if (NotebookMountLayout.Supports(blueprint) && NotebookTowerComposition.TryGetBounds(blueprint, out var bounds))
                {
                    size = .9f * Mathf.Min(rectTransform.rect.width / Mathf.Max(.001f, bounds.width),
                        rectTransform.rect.height / Mathf.Max(.001f, bounds.height));
                    center -= bounds.center * size;
                }
                if (ShowReinforcement && blueprint.Reinforcement != null)
                    DrawSectors(vh, center, NotebookMountLayout.Supports(blueprint)
                        ? size * .625f : Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .42f, blueprint.Reinforcement);
                if (previewAnimation != null)
                    NotebookTowerComposition.DrawPreviewUI(vh, blueprint, center, size, previewAnimation, 1, previewSeconds, previewAim);
                else NotebookTowerComposition.DrawUI(vh, blueprint, center, size);
                return;
            }
            // Historical profile previews retain their reference contours. Runtime battlefield
            // objects and released album assemblies use the atlas illustrations.
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .68f;
            if (ShowReinforcement && blueprint.Reinforcement != null)
                DrawSectors(vh, center, Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .42f, blueprint.Reinforcement);
            var drawing = ReferenceDrawingCache.For(blueprint);
            foreach (var template in DrawingTemplateCatalog.Create(blueprint))
            {
                var part = drawing.GetPart(template.BindingId);
                Point2 pivot = template.Pivot;
                foreach (var stroke in part.Strokes)
                    Draw(vh, stroke.Points, center + new Vector2(pivot.X, pivot.Y) * scale, scale, NotebookPalette.Blue, .018f);
            }
        }
        internal static void Draw(VertexHelper vh, IReadOnlyList<DrawingPoint> points, Vector2 center, float scale, Color color, float width, float minimumWidth = 1)
        {
            for (int i = 1; i < points.Count; i++)
                BlueprintSketch.Line(vh, center + new Vector2(points[i - 1].X, points[i - 1].Y) * scale,
                    center + new Vector2(points[i].X, points[i].Y) * scale, Mathf.Max(minimumWidth, width * scale), color);
        }
        internal static void DrawSectors(VertexHelper vh,Vector2 center,float radius,ArmorDistribution current,int selected=-1)
        {
            int facing=current?.FacingQuarterTurns??0;
            for(int sector=0;sector<4;sector++)
            {
                var color=sector==selected?NotebookPalette.Marker:NotebookPalette.Navy;
                float angle=(sector+facing)*90;int count=current?.Points[sector]??0;
                for(int line=0;line<Math.Max(1,count);line++)
                {
                    float r=radius*(.88f+line*.025f);Vector2 previous=default;
                    for(int step=0;step<=12;step++)
                    {
                        float a=(angle-38+76*step/12)*Mathf.Deg2Rad;var next=center+new Vector2(Mathf.Sin(a),Mathf.Cos(a))*r;
                        if(step>0)BlueprintSketch.Line(vh,previous,next,count==0?1:2,count==0?MeshPen.Alpha(color,.15f):color);previous=next;
                    }
                }
            }
            float front=facing*90*Mathf.Deg2Rad;var d=new Vector2(Mathf.Sin(front),Mathf.Cos(front));var tip=center+d*radius*.74f;
            BlueprintSketch.Line(vh,center+d*radius*.5f,tip,3,NotebookPalette.Blue);
            var side=new Vector2(-d.y,d.x);BlueprintSketch.Line(vh,tip,tip-d*8+side*4,2,NotebookPalette.Blue);BlueprintSketch.Line(vh,tip,tip-d*8-side*4,2,NotebookPalette.Blue);
        }
        internal static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color color)
        { for (int i = 0; i < 32; i++) { float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16; Line(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color); } }
        internal static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = to - from; if (direction.sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector2.zero); vh.AddVert(to - normal, color, Vector2.zero);
            vh.AddVert(to + normal, color, Vector2.zero); vh.AddVert(from + normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopMapSketch : NotebookAtlasGraphic
    {
        IRunReadModel session;
        public void SetSession(IRunReadModel value) { if (session == value) return; session = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (session == null) return;
            Rect rect = rectTransform.rect; Vector2 low = rect.min + Vector2.one * 8, high = rect.max - Vector2.one * 8;
            BlueprintSketch.Line(vh, low, new Vector2(high.x, low.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, low, new Vector2(low.x, high.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, high, new Vector2(high.x, low.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, high, new Vector2(low.x, high.y), 2, NotebookPalette.Navy);
            var battlefield = session.Battlefield;
            if (battlefield != null)
            {
                Vector2 cellSize = new Vector2((high.x - low.x) / session.Balance.Width, (high.y - low.y) / session.Balance.Height);
                foreach (var prop in battlefield.Props)
                {
                    Vector2 at = Point(prop.Cell, low, high) - cellSize * .5f;
                    Vector2 size = Vector2.Scale(cellSize, new Vector2(prop.Width, prop.Height));
                    string id = prop.Kind == TerrainPropKind.Mountain ? "terrain.mountain" : prop.Kind == TerrainPropKind.Pine ? "terrain.pine"
                        : prop.Kind == TerrainPropKind.Broadleaf ? "terrain.oak" : prop.Kind == TerrainPropKind.Rock ? "terrain.rock" : "terrain.shrub";
                    MiniArt(vh, id, new Rect(at, size));
                }
                foreach (var cell in battlefield.Roads)
                {
                    Vector2 at = Point(cell, low, high);
                    var east = new Cell(cell.X + 1, cell.Y); var north = new Cell(cell.X, cell.Y + 1);
                    if (battlefield.IsRoad(east)) BlueprintSketch.Line(vh, at, Point(east, low, high), 4, NotebookPalette.Pencil);
                    if (battlefield.IsRoad(north)) BlueprintSketch.Line(vh, at, Point(north, low, high), 4, NotebookPalette.Pencil);
                }
                foreach (var cell in battlefield.BarricadeSites)
                    BlueprintSketch.Ring(vh, Point(cell, low, high), 3, 1.5f, NotebookPalette.Blue);
                foreach (var cell in battlefield.Entrances)
                    Arrow(vh, Point(cell, low, high), cell.X == 0 ? Vector2.right : cell.X == session.Balance.Width - 1 ? Vector2.left : cell.Y == 0 ? Vector2.up : Vector2.down);
            }
            Vector2 home = Point(session.BaseCell, low, high);
            MiniArt(vh, "object.castle", new Rect(home - Vector2.one * 17, Vector2.one * 34));
            for (int i = 0; i < session.Wells.Count; i++)
            {
                var well = session.Wells[i]; var at = Point(well.Cell, low, high);
                string id = well.Color == InkColor.Red ? "object.well.red" : well.Color == InkColor.Blue ? "object.well.blue" : "object.well.green";
                MiniArt(vh, id, new Rect(at - Vector2.one * 12, Vector2.one * 24));
            }
            int sides = battlefield != null ? 0 : session.Balance.Waves.Count > 0 ? session.Balance.Waves[0].Sides : 1;
            if ((sides & 1) != 0) Arrow(vh, new Vector2(low.x + 2, home.y), Vector2.right);
            if ((sides & 2) != 0) Arrow(vh, new Vector2(high.x - 2, home.y), Vector2.left);
            if ((sides & 4) != 0) Arrow(vh, new Vector2(home.x, high.y - 2), Vector2.down);
            if ((sides & 8) != 0) Arrow(vh, new Vector2(home.x, low.y + 2), Vector2.up);
        }
        Vector2 Point(Cell cell, Vector2 low, Vector2 high) => new Vector2(Mathf.Lerp(low.x, high.x, (cell.X + .5f) / session.Balance.Width), Mathf.Lerp(low.y, high.y, (cell.Y + .5f) / session.Balance.Height));
        static void MiniArt(VertexHelper vh, string id, Rect available)
        {
            if (!NotebookSpriteCatalog.TryGetBounds(id, out var bounds)) return;
            float scale = Mathf.Min(available.width / bounds.width, available.height / bounds.height);
            NotebookSpriteCatalog.TryDrawUI(vh, id, available.center - bounds.center * scale, scale);
        }
        static void Arrow(VertexHelper vh, Vector2 at, Vector2 direction)
        {
            Vector2 tip = at + direction * 20, side = new Vector2(-direction.y, direction.x) * 6;
            BlueprintSketch.Line(vh, at, tip, 3, NotebookPalette.Red);
            BlueprintSketch.Line(vh, tip, tip - direction * 8 + side, 3, NotebookPalette.Red);
            BlueprintSketch.Line(vh, tip, tip - direction * 8 - side, 3, NotebookPalette.Red);
        }
    }

}
