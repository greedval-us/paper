using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Screen 06 layout around the existing drawing session and its input receiver.</summary>
    internal sealed class GuidedDrawingLayout
    {
        readonly Transform root;
        readonly WorkshopUi ui;
        readonly DrawingCanvasGraphic canvas;
        readonly RectTransform main, summary, viewport, catalogue, extras;
        readonly FoundationDetailGraphic pageFrame, canvasFrame;
        readonly Button more;
        readonly DrawingSummaryLayout readout;
        TowerBlueprint blueprint;
        float width = 1552, height = 674;

        internal GuidedDrawingLayout(Transform root, WorkshopUi ui, DrawingCanvasGraphic canvas,DrawingProbePanel probe)
        {
            this.root = root; this.ui = ui; this.canvas = canvas;
            canvas.DrawLocalGrid = false;
            canvas.EmphasizeBody = true;
            main = (RectTransform)root.Find("Guided drawing work area");
            summary = (RectTransform)root.Find("Guided drawing summary");
            viewport = (RectTransform)main.Find("Guided drawing viewport");
            catalogue = (RectTransform)root.Find("Guided drawing stencil catalogue");
            TransparentSurface(root); TransparentSurface(main); TransparentSurface(viewport);
            pageFrame = Frame(main, "Drawing work area frame"); pageFrame.transform.SetAsFirstSibling();
            canvasFrame = Frame(viewport, "Drawing paper border"); canvasFrame.Radius = 4; canvasFrame.transform.SetAsFirstSibling();
            var paperGrid=WorkshopUi.Graphic<DrawingPaperGridGraphic>(viewport,"Drawing paper grid",Rect.zero);
            paperGrid.Drawing=canvas;paperGrid.transform.SetSiblingIndex(1);
            pageFrame.Fill = new Color(1, 1, 1, .06f); canvasFrame.Fill = new Color(1, 1, 1, .025f);
            var hill = Illustration(viewport, "Drawing paper hill", "terrain.rock"); hill.color = new Color(1, 1, 1, .24f);
            var grass = Illustration(viewport, "Drawing paper grass", "terrain.shrub"); grass.color = new Color(1, 1, 1, .22f);
            hill.transform.SetSiblingIndex(1); grass.transform.SetSiblingIndex(2);
            var landscape = WorkshopUi.Graphic<Image>(root, "Drawing page landscape", Rect.zero);
            landscape.sprite = Resources.Load<Sprite>("Art/Player/FoundationPageDecor"); landscape.preserveAspect = true;
            landscape.transform.SetAsFirstSibling();
            Illustration(root,"Drawing footer mountain","terrain.mountain");
            for(int i=0;i<4;i++)Illustration(root,"Drawing footer pine "+i,"terrain.pine");
            main.Find("Guided drawing title").gameObject.SetActive(false);
            main.Find("Last drawing change").gameObject.SetActive(false);
            main.Find("Guided drawing active tool")?.gameObject.SetActive(false);
            viewport.Find("Guided drawing active tool")?.gameObject.SetActive(false);
            string[] toolNames = { "Карандаш", "Линия", "Прямоугольник", "Эллипс", "Ластик", "Переместить" };
            for (int i = 0; i < toolNames.Length; i++)
            {
                var button = main.Find("Guided drawing tool " + (DrawingTool)i).GetComponent<Button>();
                Style(button); var label = ui.Label(button.transform, "Drawing tool caption", toolNames[i], Rect.zero, 18);
                label.alignment = TextAnchor.MiddleCenter; label.resizeTextForBestFit = true; label.resizeTextMinSize = 9; label.resizeTextMaxSize = 18;
                if (i == 0 || i == 4)
                {
                    button.transform.Find("Icon").GetComponent<Graphic>().enabled = false;
                    Illustration(button.transform, "Drawing tool illustration", i == 0 ? "icon.pencil" : "icon.erase");
                }
            }
            foreach (var button in main.GetComponentsInChildren<Button>(true))
                if (!button.name.StartsWith("Guided drawing tool ")) Style(button);
            var stencil = main.Find("Guided drawing stencils");
            stencil.Find("Icon").GetComponent<WorkshopIconGraphic>().enabled = false;
            WorkshopUi.Graphic<DrawingStencilBoxGraphic>(stencil,"Drawing stencil box",Rect.zero);
            var drop = WorkshopUi.Graphic<WorkshopIconGraphic>(stencil, "Drawing stencil arrow", Rect.zero); drop.Icon = WorkshopIcon.Chevron; drop.color = WorkshopUi.Ink;

            extras = ui.Panel(main, "Drawing extra tools", new Rect(0, 0, 340, 104), WorkshopUi.Paper, true);
            var extraFrame = Frame(extras, "Drawing extra tools frame"); extraFrame.Fill = WorkshopUi.Paper; Stretch(extraFrame.rectTransform);
            string[] extraNames = { "mirror", "clear", "geometry", "fit", "next part" };
            foreach (string key in extraNames) main.Find("Guided drawing " + key).SetParent(extras, false);
            var extraTitle = ui.Label(extras, "Drawing extra tools title", "Дополнительные действия", new Rect(12, 2, 316, 30), 19, true);
            extraTitle.resizeTextForBestFit = true; extraTitle.resizeTextMinSize = 15; extraTitle.resizeTextMaxSize = 19;
            more = ui.Button(main, "Guided drawing more", Rect.zero, "···", () => ShowExtras(!extras.gameObject.activeSelf));
            Style(more); ui.Tip(more.gameObject, "Зеркальность, очистка, рабочая геометрия, вписать и следующая деталь.");
            extras.gameObject.SetActive(false);
            readout = new DrawingSummaryLayout(summary, ui,probe);
            Reflow(width, height);
        }

        internal void Reflow(float availableWidth, float availableHeight)
        {
            width = availableWidth; height = availableHeight;
            ((RectTransform)root).sizeDelta = new Vector2(width, height);
            float gap = 14, rightW = Mathf.Clamp(width * .278f, 320, 446), mainW = width - rightW - gap;
            WorkshopUi.Place(main, new Rect(0, 0, mainW, height));
            WorkshopUi.Place(summary, new Rect(mainW + gap, 0, rightW, height));
            WorkshopUi.Place(pageFrame.rectTransform, new Rect(0, 0, mainW, height));
            bool compact = mainW < 1000;
            float railW = compact ? 86 : 100, top = 70, bottom = 58;
            float paperH = height - top - bottom, paperW = mainW - railW - 20;
            WorkshopUi.Place(viewport, new Rect(railW + 12, top, paperW, paperH));
            WorkshopUi.Place(canvas.rectTransform, new Rect(0, 0, paperW, paperH));
            WorkshopUi.Place(canvasFrame.rectTransform, new Rect(0, 0, paperW, paperH));
            Place(viewport,"Drawing paper grid",new Rect(0,0,paperW,paperH));
            Place(viewport, "Drawing paper hill", new Rect(8, paperH - 100, 110, 76));
            Place(viewport, "Drawing paper grass", new Rect(paperW - 60, paperH - 80, 42, 50));
            float toolH = (paperH - 25) / 6;
            for (int i = 0; i < 6; i++)
            {
                var tool = main.Find("Guided drawing tool " + (DrawingTool)i);
                WorkshopUi.Place((RectTransform)tool, new Rect(6, top + i * (toolH + 5), railW, toolH));
                float iconH = Mathf.Min(38, toolH - 31);
                Place(tool, "Icon", new Rect((railW - iconH) * .5f, 7, iconH, iconH));
                Place(tool, "Drawing tool illustration", new Rect((railW - iconH - 6) * .5f, 5, iconH + 6, iconH + 6));
                Place(tool, "Drawing tool caption", new Rect(3, toolH - 28, railW - 6, 25));
            }

            float stencilW = compact ? 164 : 204, partW = mainW - stencilW - 20;
            var tabs = main.Find("Guided drawing parts");
            if (tabs != null)
            {
                WorkshopUi.Place((RectTransform)tabs, new Rect(5, 5, partW, 55));
                var buttons = tabs.GetComponentsInChildren<Button>();
                float tabW = (partW - (buttons.Length - 1) * 6) / buttons.Length;
                for (int i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i]; Style(button);
                    WorkshopUi.Place((RectTransform)button.transform, new Rect(i * (tabW + 6), 0, tabW, 55));
                    Place(button.transform, "Icon", new Rect(10, 10, 32, 32));
                    Caption(button, new Rect(47, 3, tabW - 52, 49), 15, 24, true);
                }
            }
            var stencil = main.Find("Guided drawing stencils");
            WorkshopUi.Place((RectTransform)stencil, new Rect(mainW - stencilW - 6, 5, stencilW, 55));
            Place(stencil, "Drawing stencil box", new Rect(9, 8, 38, 38));
            Place(stencil, "Drawing stencil arrow", new Rect(stencilW - 31, 17, 22, 22));
            Caption(stencil.GetComponent<Button>(), new Rect(52, 2, stencilW - 84, 51), 15, 22, true);

            Place(main, "Guided drawing hint", new Rect(railW + 24, height - 91, paperW - 24, 27));
            var hint = main.Find("Guided drawing hint").GetComponent<Text>();
            hint.fontSize = 16; hint.resizeTextForBestFit = true; hint.resizeTextMinSize = 13; hint.resizeTextMaxSize = 16;
            var empty = viewport.Find("Guided drawing empty state");
            float emptyW = Mathf.Min(460, paperW - 30);
            WorkshopUi.Place((RectTransform)empty, new Rect((paperW - emptyW) * .5f, (paperH - 156) * .5f, emptyW, 156));
            Place(empty, "Guided drawing empty title", new Rect(0, 0, emptyW, 48));
            Place(empty, "Guided drawing empty hint", new Rect(0, 50, emptyW, 48));
            var emptyHint = empty.Find("Guided drawing empty hint").GetComponent<Text>(); emptyHint.resizeTextForBestFit = true; emptyHint.resizeTextMinSize = 15; emptyHint.resizeTextMaxSize = 18;
            Place(empty, "Guided drawing empty stencil", new Rect((emptyW - 220) * .5f, 108, 220, 40));
            Toolbar(mainW, height - 53, compact);
            WorkshopUi.Place(extras, new Rect(mainW - 368, height - 180, 356, 118));
            string[] small = { "mirror", "clear", "geometry" };
            for (int i = 0; i < small.Length; i++)
            {
                var item = extras.Find("Guided drawing " + small[i]);
                WorkshopUi.Place((RectTransform)item, new Rect(12 + i * 55, 37, 45, 35));
                Place(item, "Icon", new Rect(10, 5, 25, 25));
            }
            Place(extras, "Guided drawing fit", new Rect(177, 37, 166, 35));
            Place(extras, "Guided drawing next part", new Rect(12, 77, 331, 32));
            readout.Reflow(rightW, height);
            ReflowCatalogue();
            Place(root, "Drawing page landscape", new Rect(30, height - 6, Mathf.Min(650, width * .48f), 75));
            Place(root,"Drawing footer mountain",new Rect(382,height+14,102,58));
            for(int i=0;i<4;i++)
            {
                float size=i==1?68:i==2?40:31;
                Place(root,"Drawing footer pine "+i,new Rect(256+i*84,height+72-size,size,size));
            }
            UpdateSelection();
        }

        void Toolbar(float mainW, float y, bool compact)
        {
            float x = 6, gap = 6;
            float[] sizes = compact ? new[] { 44f, 44f, 99f, 112f, 124f, 136f, 136f, 36f }
                : new[] { 104f, 104f, 110f, 120f, 140f, 156f, 156f, 38f };
            float scale = (mainW - 12 - gap * 7) / sizes.Sum();
            string[] keys = { "undo", "redo", "grid", "snap" };
            string[] captions = { "Отменить", "Повторить", "Сетка", "Привязка" };
            for (int i = 0; i < 4; i++)
            {
                float w = sizes[i] * scale; var button = main.Find("Guided drawing " + keys[i]).GetComponent<Button>();
                WorkshopUi.Place((RectTransform)button.transform, new Rect(x, y, w, 42));
                Place(button.transform, "Icon", new Rect(8, 10, 22, 22));
                if (button.transform.Find("Caption") == null) ui.Label(button.transform, "Caption", captions[i], Rect.zero, 17);
                var label = button.transform.Find("Caption").GetComponent<Text>();
                label.text = captions[i]; label.gameObject.SetActive(!(compact && i < 2));
                Caption(button, new Rect(35, 2, w - 41, 38), 13, 18, false);
                if(i==2)
                {
                    Caption(button,new Rect(35,2,w-65,38),13,18,false);
                    var check=button.transform.Find("Drawing grid check")?.GetComponent<WorkshopIconGraphic>();
                    if(check==null)
                    {
                        var box=Frame(button.transform,"Drawing grid check box");box.Radius=3;box.Fill=WorkshopUi.Paper;
                        check=WorkshopUi.Graphic<WorkshopIconGraphic>(button.transform,"Drawing grid check",Rect.zero);
                        check.Icon=WorkshopIcon.Check;check.color=WorkshopUi.Blue;
                    }
                    Place(button.transform,"Drawing grid check box",new Rect(w-27,11,20,20));
                    Place(button.transform,"Drawing grid check",new Rect(w-26,12,18,18));
                }
                if (compact && i < 2) Place(button.transform, "Icon", new Rect((w - 25) * .5f, 8, 25, 25));
                x += w + gap;
            }
            float zoomW = sizes[4] * scale;
            Place(main, "Guided drawing zoom_out", new Rect(x, y, 34, 42));
            Place(main, "Guided drawing zoom value", new Rect(x + 36, y, zoomW - 72, 42));
            Place(main, "Guided drawing zoom_in", new Rect(x + zoomW - 34, y, 34, 42));
            CenterIcon(main.Find("Guided drawing zoom_out"), 34, 42); CenterIcon(main.Find("Guided drawing zoom_in"), 34, 42);
            ZoomSign("zoom_out","−");ZoomSign("zoom_in","+");
            x += zoomW + gap;
            string[] axis = { "width", "height" };
            for (int i = 0; i < 2; i++)
            {
                float w = sizes[5 + i] * scale, captionW = w - 76;
                Place(main, "Guided drawing " + axis[i] + " label", new Rect(x, y, captionW, 42));
                var label = main.Find("Guided drawing " + axis[i] + " label").GetComponent<Text>(); label.fontSize = 17;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 13; label.resizeTextMaxSize = 17;
                Place(main, "Guided drawing " + axis[i] + " smaller", new Rect(x + captionW, y, 34, 42));
                Place(main, "Guided drawing " + axis[i] + " larger", new Rect(x + w - 34, y, 34, 42));
                x += w + gap;
            }
            WorkshopUi.Place((RectTransform)more.transform, new Rect(x, y, sizes[7] * scale, 42));
        }

        internal void Refresh(TowerPreview? preview, FreeformTowerDesign design, int mount,TowerBlueprint candidate,TowerPreview? previous=null)
        { blueprint=candidate; readout.Refresh(preview, design, mount,candidate.Content?.FreeformRules,previous); UpdateSelection(); }
        internal void ReflowCurrent()=>Reflow(width,height);

        internal void ShowExtras(bool show)
        {
            extras.gameObject.SetActive(show); ui.SetSelected(more, show);
            if (show) { extras.SetAsLastSibling(); EventSystem.current?.SetSelectedGameObject(extras.GetComponentInChildren<Button>().gameObject); }
            else if (EventSystem.current?.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(extras)) EventSystem.current.SetSelectedGameObject(more.gameObject);
        }

        void UpdateSelection()
        {
            var paperGrid=viewport.Find("Drawing paper grid").GetComponent<DrawingPaperGridGraphic>();
            paperGrid.enabled=canvas.ShowGrid;paperGrid.SetVerticesDirty();
            var gridCheck=main.Find("Guided drawing grid/Drawing grid check");
            if(gridCheck!=null)gridCheck.gameObject.SetActive(canvas.ShowGrid);
            var hint=main.Find("Guided drawing hint").GetComponent<Text>();
            hint.enabled=hint.color==WorkshopUi.Warning;
            foreach (var button in main.GetComponentsInChildren<Button>(true))
            {
                var frame = button.transform.Find("Drawing control frame")?.GetComponent<FoundationDetailGraphic>();
                if (frame == null) continue;
                bool selected = button.name == "Guided drawing tool " + canvas.Tool || button.name == "Guided drawing part " + canvas.SelectedBinding ||
                    button.name == "Guided drawing grid" && canvas.ShowGrid || button.name == "Guided drawing snap" && canvas.SnapToGrid;
                frame.Fill = selected ? WorkshopUi.Selected : WorkshopUi.Paper;
                if(button.name.StartsWith("Guided drawing part ")&&blueprint!=null)
                {
                    string binding=button.name.Substring("Guided drawing part ".Length);
                    button.transform.Find("Icon").GetComponent<Graphic>().enabled=false;
                    var art=button.transform.Find("Drawing layer illustration");
                    if(art==null)
                    {
                        if(binding==DrawingBindings.Decoration)
                        {
                            var star=WorkshopUi.Graphic<FoundationDetailGraphic>(button.transform,"Drawing layer illustration",Rect.zero);
                            star.Shape=FoundationDetailGraphic.Kind.Star;star.color=WorkshopUi.Blue;art=star.transform;
                        }
                        else art=Illustration(button.transform,"Drawing layer illustration",blueprint.ChassisId).transform;
                    }
                    var image=art.GetComponent<Image>();
                    if(image!=null)
                    {
                        string id=DrawingBindings.TryMount(binding,out _)?"icon.pen":blueprint.ChassisId;
                        if(NotebookSpriteCatalog.TryGet(id,out var sprite))image.sprite=sprite;
                    }
                    Place(button.transform,"Drawing layer illustration",new Rect(8,6,38,42));
                }
            }
        }

        internal void ReflowCatalogue()
        {
            if (catalogue.parent != root)
            {
                var shade = (RectTransform)catalogue.parent;
                var owner = (RectTransform)shade.parent;
                WorkshopUi.Place(shade, new Rect(0, 0, owner.rect.width, owner.rect.height));
                float scale = Mathf.Min(1, (owner.rect.width - 40) / 840, (owner.rect.height - 40) / 780);
                catalogue.localScale = Vector3.one * scale;
                WorkshopUi.Place(catalogue, new Rect((owner.rect.width - 840 * scale) / 2, (owner.rect.height - 780 * scale) / 2, 840, 780));
            }
            else
            {
                float scale = Mathf.Min(1, width / 840, height / 780); catalogue.localScale = Vector3.one * scale;
                WorkshopUi.Place(catalogue, new Rect((width - 840 * scale) / 2, (height - 780 * scale) / 2, 840, 780));
            }
        }

        void Style(Button button)
        {
            if (!(button is WorkshopButton)) return;
            var frame = button.transform.Find("Drawing control frame")?.GetComponent<FoundationDetailGraphic>();
            if (frame == null) frame = Frame(button.transform, "Drawing control frame");
            frame.Radius = 6; frame.Fill = WorkshopUi.Paper; Stretch(frame.rectTransform); frame.transform.SetAsFirstSibling();
            var surface = button.GetComponent<WorkshopSurfaceGraphic>(); surface.canvasRenderer.cullTransparentMesh = false; surface.canvasRenderer.SetAlpha(0);
            ((WorkshopButton)button).SetDecoration(frame);
        }
        static void TransparentSurface(Transform target)
        { var surface = target.GetComponent<Graphic>(); surface.canvasRenderer.cullTransparentMesh = false; surface.canvasRenderer.SetAlpha(0); }
        static FoundationDetailGraphic Frame(Transform owner, string name)
        { var frame = WorkshopUi.Graphic<FoundationDetailGraphic>(owner, name, Rect.zero); frame.Shape = FoundationDetailGraphic.Kind.Frame; frame.color = WorkshopUi.Blue; return frame; }
        static Image Illustration(Transform owner, string name, string id)
        { var image = WorkshopUi.Graphic<Image>(owner, name, Rect.zero); if (NotebookSpriteCatalog.TryGet(id, out var sprite)) image.sprite = sprite; else image.enabled = false; image.preserveAspect = true; return image; }
        static void Caption(Button button, Rect bounds, int min, int max, bool hand)
        {
            var label = button.transform.Find("Caption")?.GetComponent<Text>(); if (label == null) return;
            WorkshopUi.Place(label.rectTransform, bounds); label.font = hand ? Resources.Load<Font>("NotebookHand") : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.resizeTextForBestFit = true; label.resizeTextMinSize = min; label.resizeTextMaxSize = max; label.fontSize = max;
        }
        static void CenterIcon(Transform owner, float width, float height) => Place(owner, "Icon", new Rect((width - 22) / 2, (height - 22) / 2, 22, 22));
        void ZoomSign(string key,string sign)
        {
            var button=main.Find("Guided drawing "+key);
            button.Find("Icon").GetComponent<Graphic>().enabled=false;
            var label=button.Find("Drawing zoom sign")?.GetComponent<Text>();
            if(label==null)label=ui.Label(button,"Drawing zoom sign",sign,new Rect(0,0,34,42),27);
            label.alignment=TextAnchor.MiddleCenter;
        }
        static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Place(Transform owner, string name, Rect bounds)
        {
            var target = owner.Find(name) as RectTransform; if (target == null) return; WorkshopUi.Place(target, bounds);
            if (target.GetComponent<Image>() != null) { target.pivot = Vector2.one * .5f; target.anchoredPosition = new Vector2(bounds.center.x, -bounds.center.y); }
        }
    }

    public sealed class DrawingStencilBoxGraphic : MaskableGraphic
    {
        readonly MeshPen pen=new MeshPen(null,512);
        public override Texture mainTexture=>NotebookSpriteCatalog.Texture;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();pen.Clear();
            Color ink=WorkshopUi.Ink;
            Vector2[] front={new Vector2(3,9),new Vector2(12,13),new Vector2(21,9),new Vector2(21,20),new Vector2(12,24),new Vector2(3,20),new Vector2(3,9)};
            Path(front,ink);
            Path(new[]{new Vector2(3,9),new Vector2(2,5),new Vector2(11,8),new Vector2(12,13),new Vector2(13,7),new Vector2(22,4),new Vector2(21,9)},ink);
            Path(new[]{new Vector2(4,5),new Vector2(7,1),new Vector2(14,4),new Vector2(13,7)},ink);
            Path(new[]{new Vector2(12,13),new Vector2(12,24)},ink);
            Path(new[]{new Vector2(8,7),new Vector2(8,4),new Vector2(10,4),new Vector2(10,8)},ink);
            Path(new[]{new Vector2(16,7),new Vector2(17,2),new Vector2(20,3),new Vector2(19,6)},ink);
            for(int i=0;i<4;i++)pen.Line(new Vector2(14+i*1.5f,9+i*.5f),new Vector2(14+i*1.5f,4+i*.5f),.55f,MeshPen.Alpha(ink,.6f));
            pen.Ring(new Vector2(7,7),1.5f,.8f,ink,12);
            var r=rectTransform.rect;float scale=Mathf.Min(r.width,r.height)/26;
            pen.CopyTo(vh,r.center-new Vector2(12,12)*scale,scale);
        }
        void Path(Vector2[] points,Color ink)
        {for(int i=1;i<points.Length;i++)pen.Line(new Vector2(points[i-1].x,24-points[i-1].y),new Vector2(points[i].x,24-points[i].y),.85f,ink);}
    }

    public sealed class DrawingPaperGridGraphic : MaskableGraphic
    {
        public DrawingCanvasGraphic Drawing;
        Vector2 lastPan;
        float lastZoom;
        readonly MeshPen pen=new MeshPen(null,2048);
        public override Texture mainTexture=>NotebookSpriteCatalog.Texture;
        void LateUpdate()
        {
            if(Drawing==null||(lastPan==Drawing.Pan&&lastZoom==Drawing.Zoom))return;
            lastPan=Drawing.Pan;lastZoom=Drawing.Zoom;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();pen.Clear();if(Drawing==null)return;
            var r=rectTransform.rect;float step=Mathf.Max(8,Mathf.Min(r.width,r.height)*.44f*Drawing.Zoom*DrawingCanvasGraphic.GridSpacing);
            Vector2 center=r.center+Drawing.Pan;var ink=new Color(.24f,.48f,.72f,.13f);
            for(float x=center.x-Mathf.Ceil((center.x-r.xMin)/step)*step;x<r.xMax;x+=step)
                pen.Line(new Vector2(x,r.yMin),new Vector2(x,r.yMax),.65f,ink);
            for(float y=center.y-Mathf.Ceil((center.y-r.yMin)/step)*step;y<r.yMax;y+=step)
                pen.Line(new Vector2(r.xMin,y),new Vector2(r.xMax,y),.65f,ink);
            pen.CopyTo(vh,Vector2.zero,1);
        }
    }
}
