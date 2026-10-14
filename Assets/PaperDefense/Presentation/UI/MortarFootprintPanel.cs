using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
namespace PaperDefense.Presentation
{
    public sealed class MortarFootprintPanel
    {
        readonly UserTextResolver textResolver;
        string Resolve(string value) => GameText.Resolve(textResolver, value);
        readonly GameObject sheet;
        readonly Func<TowerBlueprint, string> validationReason;
        readonly Action<TowerBlueprint> changeDraft;
        readonly Func<TowerBlueprint, int, MortarFootprint, MortarFootprint, FootprintFormation, FootprintProbeComparison> compare;
        readonly FootprintProbeSketch first, second;
        readonly UnityEngine.UI.Text summary, feedback, instructions, scaleLabel, changes;
        readonly Stack<MortarFootprint> history = new Stack<MortarFootprint>();
        float maximumAspect = MortarFootprint.MaximumAspectRatio, radius;
        TowerBlueprint blueprint;
        FootprintFormation formation;
        float started;
        IReadOnlyList<Point2> rejected;
        public bool IsOpen => sheet.activeSelf;
        public bool IsDragging { get; private set; }
        public int Mount { get; private set; }
        public MortarFootprint Baseline { get; private set; }
        public FootprintProbeComparison Recording { get; private set; }
        public FootprintAxisGraphic Axis { get; }
        public FootprintCanvasGraphic Canvas { get; }
        public FootprintCanvasGraphic Working { get; }
        public FootprintContourAssessment Assessment { get; private set; }
        public FootprintStudyPanel Study { get; }
        public float PlaybackSeconds => Recording == null ? 0 : Mathf.Repeat(Time.unscaledTime - started, Duration + 1.4f);
        float Duration => Recording == null ? 0 : Mathf.Max(Recording.First.Frames[Recording.First.Frames.Count - 1].Seconds,
            Recording.Second.Frames[Recording.Second.Frames.Count - 1].Seconds);

        public MortarFootprintPanel(Transform parent, Font font, Action<TowerBlueprint> changeDraft,
            Func<TowerBlueprint, int, MortarFootprint, MortarFootprint, FootprintFormation, FootprintProbeComparison> compare,
            Func<TowerBlueprint, string> validationReason, UserTextResolver textResolver = null,
            Func<TowerBlueprint,int,FootprintStudyReport> study = null)
        {
            this.textResolver=textResolver;this.changeDraft=changeDraft;this.compare=compare;this.validationReason=validationReason;
            sheet=new GameObject("Mortar footprint sheet",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            sheet.transform.SetParent(parent,false);Place(sheet.GetComponent<RectTransform>(),new Rect(20,25,1560,950));
            sheet.GetComponent<UnityEngine.UI.Image>().color=NotebookPalette.Paper;
            Text(font,UserText.Message("copy.90b777be2577be25"),new Rect(30,18,1480,44),30);
            if(study!=null)
            {
                Study=new FootprintStudyPanel(parent,font,study,value=>{if(IsOpen){StopGesture();CommitShape(value);}},textResolver);
                Button(font,"Footprint study",UserText.Message("study.open"),new Rect(1220,18,310,44),OpenStudy);
            }
            instructions=Text(font,UserText.Message("imprint.instructions"),new Rect(30,72,1480,56),20);
            Text(font,UserText.Message("imprint.authored"),new Rect(30,140,470,30),21);
            Text(font,UserText.Message("imprint.working"),new Rect(530,140,300,30),21);
            Text(font,UserText.Message("copy.74f7ef7fd7c9851c"),new Rect(880,140,310,30),20);
            Text(font,UserText.Message("copy.2a5e89a7fb3c531b"),new Rect(1220,140,310,30),20);
            Canvas=Surface("Footprint contour canvas",new Rect(30,180,470,350),true);
            Working=Surface("Footprint working geometry",new Rect(530,180,300,350),false);
            changes=Text(font,"",new Rect(30,540,800,90),17);changes.name="Footprint change comparison";
            Canvas.Began=BeginDrag;Canvas.Finished=points=>SetContour(points,false);Canvas.Canceled=CancelDrawing;Canvas.UndoRequested=Undo;
            var axis=new GameObject("Footprint axis",typeof(RectTransform),typeof(CanvasRenderer),typeof(FootprintAxisGraphic));
            axis.transform.SetParent(sheet.transform,false);Place(axis.GetComponent<RectTransform>(),new Rect(30,180,470,350));
            Axis=axis.GetComponent<FootprintAxisGraphic>();Axis.Configure(BeginDrag,CommitEllipse);
            Axis.AspectChanged=q=> { if(IsDragging)feedback.text=Resolve(UserText.Message("imprint.ellipse_drag",q,maximumAspect)); };
            Axis.gameObject.SetActive(false);
            first=Sketch("Footprint replay A",new Rect(880,180,310,450));
            second=Sketch("Footprint replay B",new Rect(1220,180,310,450));
            scaleLabel=Text(font,"",new Rect(530,638,300,60),18);
            feedback=Text(font,"",new Rect(30,705,800,72),19);feedback.name="Footprint shape feedback";
            summary=Text(font,"",new Rect(880,645,650,115),19);summary.name="Footprint probe feedback";
            Text(font,UserText.Message("imprint.pen_hint"),new Rect(30,640,470,58),18);
            Button(font,"Footprint circle",UserText.Message("copy.bbe6ba35fc18081c"),new Rect(30,790,130,45),SetCircle);
            Button(font,"Footprint longitudinal",UserText.Message("imprint.longitudinal"),new Rect(170,790,160,45),()=>Preset(false));
            Button(font,"Footprint transverse",UserText.Message("imprint.transverse"),new Rect(340,790,160,45),()=>Preset(true));
            Button(font,"Footprint rotate",UserText.Message("imprint.rotate"),new Rect(510,790,145,45),Rotate);
            Button(font,"Footprint undo",UserText.Message("imprint.undo"),new Rect(665,790,165,45),Undo);
            Button(font,"Footprint group",UserText.Message("copy.907efbd4ce48253b"),new Rect(880,790,205,45),()=>Formation(FootprintFormation.Group));
            Button(font,"Footprint column",UserText.Message("copy.322ef2503cd18ed7"),new Rect(1100,790,205,45),()=>Formation(FootprintFormation.Column));
            Button(font,"Footprint line",UserText.Message("imprint.line"),new Rect(1320,790,210,45),()=>Formation(FootprintFormation.Line));
            Button(font,"Footprint freehand",UserText.Message("imprint.freehand"),new Rect(30,855,205,50),UseFreehandTool);
            Button(font,"Footprint ellipse",UserText.Message("imprint.ellipse"),new Rect(250,855,155,50),UseEllipseTool);
            Button(font,"Footprint close contour",UserText.Message("imprint.close_contour"),new Rect(420,855,245,50),CloseContour);
            Button(font,"Footprint pin A",UserText.Message("imprint.pin"),new Rect(680,855,150,50),PinBaseline);
            Button(font,"Footprint repeat",UserText.Message("imprint.fire"),new Rect(880,855,315,50),Record);
            Button(font,"Close footprint",UserText.Message("imprint.done"),new Rect(1220,855,310,50),Close);
            sheet.SetActive(false);
        }
        public void Open(TowerBlueprint value,int mount)
        {
            value.GetMortarFootprint(mount);Study?.Close();StopGesture();blueprint=value;Mount=mount;history.Clear();rejected=null;Assessment=null;
            var weapon=(value.Content??GameBalance.CreateDefault().ResolveContent()).GetWeapon(value.GetWeaponId(mount));
            maximumAspect=weapon.MaximumFootprintAspectRatio;radius=weapon.Stats.Radius;Axis.SetMaximum(maximumAspect);
            Baseline=value.GetMortarFootprint(mount);IsDragging=false;
            sheet.SetActive(true);sheet.transform.SetAsLastSibling();UseFreehandTool();ShowAccepted();Record();
        }
        public void Close()
        {
            Study?.Close();IsDragging=false;Axis.Cancel();Canvas.CancelGesture();sheet.SetActive(false);Clear();
        }
        void OpenStudy()
        {
            if(!IsOpen||IsDragging||rejected!=null||Study==null)return;
            string reason=validationReason(blueprint);
            if(!string.IsNullOrEmpty(reason)){Clear();summary.text=Resolve(UserText.Message("study.unavailable",reason));return;}
            Study.Open(blueprint,Mount);
        }
        public void UseFreehandTool()
        {
            Axis.Cancel();IsDragging=false;Axis.gameObject.SetActive(false);Canvas.gameObject.SetActive(true);
            if(IsOpen&&blueprint!=null){ShowAccepted();Record();}
        }
        public void UseEllipseTool()
        {
            Canvas.CancelGesture();IsDragging=false;Canvas.gameObject.SetActive(false);Axis.gameObject.SetActive(true);
            if(IsOpen&&blueprint!=null)
            {
                var current=blueprint.GetMortarFootprint(Mount);
                if(current.IsContour)CommitEllipse(Mathf.Min(current.AspectRatio,maximumAspect));
                else { ShowAccepted();Record(); }
            }
        }
        public void PinBaseline()
        {
            if(!IsOpen||IsDragging||rejected!=null)return;
            Baseline=blueprint.GetMortarFootprint(Mount);ShowChanges();Record();
        }
        public void SetCircle() { if(IsOpen){StopGesture();CommitShape(MortarFootprint.Circle);UseFreehandTool();} }
        void Preset(bool transverse)
        {
            if(!IsOpen)return;StopGesture();UseFreehandTool();
            SetContour(EllipsePoints(Mathf.Min(2.4f,maximumAspect),transverse));
        }
        void Formation(FootprintFormation value) { formation=value;Record(); }
        void BeginDrag() { IsDragging=true;Clear();changes.text=Resolve(UserText.Message("change.waiting"));Working.Show(Array.Empty<Point2>());feedback.color=NotebookPalette.Navy;feedback.text=Resolve(UserText.Message("imprint.drawing")); }
        void CancelDrawing()
        {
            IsDragging=false;if(!IsOpen||blueprint==null)return;ShowAccepted();Clear();
            feedback.text=Resolve(UserText.Message("imprint.canceled"));
        }
        void StopGesture() { Axis.Cancel();Canvas.CancelGesture();IsDragging=false; }
        void CommitEllipse(float q)
        {
            if(!IsOpen)return;IsDragging=false;CommitShape(new MortarFootprint(q,maximumAspect));
        }
        public void SetContour(IReadOnlyList<Point2> points,bool closed=true)
        {
            if(!IsOpen)return;IsDragging=false;
            var result=FootprintContour.Analyze(points,closed,maximumAspect);
            Assessment=result;
            if(!result.Valid)
            {
                rejected=new List<Point2>(points??Array.Empty<Point2>()).AsReadOnly();Clear();
                Canvas.Show(result.Issue==FootprintContourIssue.Nonfinite||result.Issue==FootprintContourIssue.PointCount?Array.Empty<Point2>():rejected,result,result.Issue!=FootprintContourIssue.Open);
                Working.Show(result.Issue==FootprintContourIssue.TooFar?Array.Empty<Point2>():result.Normalized,result);
                feedback.color=NotebookPalette.Red;feedback.text=Resolve(UserText.Message("imprint.invalid",IssueMessage(result.Issue)));
                changes.text=Resolve(UserText.Message("change.invalid"));
                return;
            }
            CommitShape(MortarFootprint.FromContour(result.Authored,maximumAspect));
            Assessment=result;
            if(result.ClosedGap)feedback.text=Resolve(UserText.Message("imprint.closed_gap"))+"\n"+feedback.text;
        }
        void CommitShape(MortarFootprint value,bool remember=true)
        {
            var previous=blueprint.GetMortarFootprint(Mount);
            if(remember&&!previous.SameShape(value))history.Push(previous);
            blueprint=blueprint.WithMortarFootprint(Mount,value);changeDraft(blueprint);
            if(value.IsContour){Axis.gameObject.SetActive(false);Canvas.gameObject.SetActive(true);}
            ShowAccepted();Record();
        }
        void ShowAccepted()
        {
            rejected=null;Assessment=null;var shape=blueprint.GetMortarFootprint(Mount);
            Canvas.Show(shape.IsContour?shape.Authored:EllipsePoints(shape.AspectRatio,false));
            Working.Show(shape.IsContour?shape.Normalized:EllipsePoints(shape.AspectRatio,false));
            Axis.SetAspect(shape.AspectRatio);
            feedback.color=NotebookPalette.Navy;
            feedback.text=Resolve(UserText.Message("imprint.accepted",Math.PI*radius*radius,maximumAspect,FootprintContour.MinimumWidth*radius));
            scaleLabel.text=Resolve(UserText.Message("imprint.scale",radius));
            ShowChanges();
        }
        void ShowChanges()=>changes.text=Resolve(DesignChangeFeedback.Footprint(Baseline,blueprint.GetMortarFootprint(Mount),radius));
        public void Undo()
        {
            if(!IsOpen||Study?.IsOpen==true)return;bool invalid=rejected!=null;StopGesture();
            if(invalid){ShowAccepted();Record();return;}
            if(history.Count>0)CommitShape(history.Pop(),false);
        }
        void CloseContour() { if(IsOpen&&rejected!=null&&Assessment?.Issue==FootprintContourIssue.Open)SetContour(rejected,true); }
        void Rotate()
        {
            if(!IsOpen)return;StopGesture();UseFreehandTool();var current=blueprint.GetMortarFootprint(Mount);
            var source=current.IsContour?current.Authored:EllipsePoints(current.AspectRatio,false);var result=new Point2[source.Count];
            for(int i=0;i<result.Length;i++)result[i]=new Point2(source[i].Y,-source[i].X);SetContour(result);
        }
        static Point2[] EllipsePoints(float aspect,bool transverse)
        {
            var result=new Point2[64];float along=Mathf.Sqrt(aspect),across=1/along;
            for(int i=0;i<result.Length;i++){float a=i*Mathf.PI*2/result.Length;float x=Mathf.Cos(a)*across,y=Mathf.Sin(a)*along;result[i]=transverse?new Point2(y,-x):new Point2(x,y);}
            return result;
        }
        string IssueMessage(FootprintContourIssue issue)
        {
            switch(issue)
            {
                case FootprintContourIssue.PointCount:return UserText.Message("imprint.error.points",FootprintContour.MaximumPoints);
                case FootprintContourIssue.Nonfinite:return UserText.Message("imprint.error.coordinates");
                case FootprintContourIssue.Open:return UserText.Message("imprint.error.open");
                case FootprintContourIssue.Intersection:return UserText.Message("imprint.error.crossing");
                case FootprintContourIssue.Area:return UserText.Message("imprint.error.area");
                case FootprintContourIssue.AimOutside:return UserText.Message("imprint.error.aim");
                case FootprintContourIssue.Elongated:return UserText.Message("imprint.error.aspect",maximumAspect);
                case FootprintContourIssue.TooFar:return UserText.Message("imprint.error.reach",FootprintContour.MaximumReach);
                case FootprintContourIssue.Narrow:return UserText.Message("imprint.error.width",FootprintContour.MinimumWidth);
                default:return "";
            }
        }
        void Clear()
        {
            Recording=null;first.SetRecording(null,()=>0);second.SetRecording(null,()=>0);
            summary.text=Resolve(UserText.Message("imprint.no_trial"));
        }
        void Record()
        {
            if(!IsOpen||IsDragging||rejected!=null)return;
            string reason=validationReason(blueprint);
            if(!string.IsNullOrEmpty(reason)){Clear();summary.text=Resolve(UserText.Message("copy.e7e92489666048de",reason));return;}
            if(!Baseline.FitsAspectLimit(maximumAspect)){Clear();summary.text=Resolve(UserText.Message("imprint.baseline_invalid"));return;}
            Recording=compare(blueprint,Mount,Baseline,blueprint.GetMortarFootprint(Mount),formation);started=Time.unscaledTime;
            first.SetRecording(Recording.First,()=>PlaybackSeconds);second.SetRecording(Recording.Second,()=>PlaybackSeconds);
            summary.text=Resolve(Values(UserText.Message("common.replay_a"),Recording.First)+"\n"+Values(UserText.Message("common.replay_b"),Recording.Second));
        }
        static string Values(string name,FootprintProbeReport report)
        {
            string result=UserText.Message("copy.9f7043ce99bb7fc9",name,report.HitCount,report.MissedCount,report.Cost);
            return report.Completed?result:UserText.Message("common.with_detail",result,report.Message);
        }
        FootprintCanvasGraphic Surface(string name,Rect box,bool interactive)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(FootprintCanvasGraphic));go.transform.SetParent(sheet.transform,false);
            Place(go.GetComponent<RectTransform>(),box);var result=go.GetComponent<FootprintCanvasGraphic>();result.Interactive=interactive;result.raycastTarget=interactive;return result;
        }
        FootprintProbeSketch Sketch(string name,Rect box)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(FootprintProbeSketch));go.transform.SetParent(sheet.transform,false);
            Place(go.GetComponent<RectTransform>(),box);var result=go.GetComponent<FootprintProbeSketch>();result.raycastTarget=false;return result;
        }
        UnityEngine.UI.Text Text(Font font,string value,Rect box,int size)
        {
            var go=new GameObject("Footprint text",typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(sheet.transform,false);
            Place(go.GetComponent<RectTransform>(),box);var text=go.GetComponent<UnityEngine.UI.Text>();text.font=font;text.fontSize=size;text.text=Resolve(value);text.color=NotebookPalette.Navy;text.raycastTarget=false;return text;
        }
        void Button(Font font,string name,string value,Rect box,Action action)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(sheet.transform,false);
            Place(go.GetComponent<RectTransform>(),box);go.GetComponent<UnityEngine.UI.Image>().color=Color.white;go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>action());
            var label=Text(font,value,new Rect(5,5,box.width-10,box.height-10),18);label.transform.SetParent(go.transform,false);label.alignment=TextAnchor.MiddleCenter;
        }
        internal static void Place(RectTransform rect,Rect box)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(box.x,-box.y);rect.sizeDelta=box.size;}
    }
    public sealed class FootprintAxisGraphic : UnityEngine.UI.Graphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<float> AspectChanged { get; set; }
        Action begin;
        Action<float> commit;
        bool dragging;
        int pointerId;
        const float Radius = 82;
        float maximumAspect = MortarFootprint.MaximumAspectRatio;
        float DisplayRadius => Mathf.Min(Radius, rectTransform.rect.height * .43f / Mathf.Sqrt(maximumAspect));
        public float AspectRatio { get; private set; } = 1;
        public Vector2 Endpoint => rectTransform.rect.center + Vector2.up * DisplayRadius * Mathf.Sqrt(AspectRatio);
        internal void SetMaximum(float maximum) { maximumAspect = maximum; SetVerticesDirty(); }
        public Vector2 LocalPointToScreen(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, rectTransform.TransformPoint(local));
        internal void Configure(Action start, Action<float> end)
        {
            begin = start;
            commit = end;
            color = NotebookPalette.Blue;
        }
        internal void SetAspect(float q)
        {
            AspectRatio = q;
            AspectChanged?.Invoke(q);
            SetVerticesDirty();
        }
        internal void Cancel()
        {
            dragging = false;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out var p) || (p - Endpoint).sqrMagnitude>28 * 28)
            {
                return;
            }
            dragging = true;
            pointerId = data.pointerId;
            begin?.Invoke();
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != pointerId)
            {
                return;
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out var p))
            {
                float forwardDistance = Mathf.Max(0, p.y - rectTransform.rect.center.y);
                float q = Mathf.Pow(forwardDistance / DisplayRadius, 2);
                SetAspect(Mathf.Clamp(q, 1, maximumAspect));
            }
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (!dragging || data.pointerId != pointerId)
            {
                return;
            }
            dragging = false;
            commit?.Invoke(AspectRatio);
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            var c = rectTransform.rect.center;
            float along = DisplayRadius * Mathf.Sqrt(AspectRatio);
            float across = DisplayRadius / Mathf.Sqrt(AspectRatio);
            for (int i = 0; i<64; i++)
            {
                float a = i * Mathf.PI / 32, b = (i + 1) * Mathf.PI / 32;
                BlueprintSketch.Line(vh, c + new Vector2(Mathf.Cos(a) * across, Mathf.Sin(a) * along), c + new Vector2(Mathf.Cos(b) * across, Mathf.Sin(b) * along), 3, NotebookPalette.Blue);
            }
            BlueprintSketch.Line(vh, c, c + Vector2.up * 175, 2, NotebookPalette.Navy);
            var tip = c + Vector2.up * 175;
            BlueprintSketch.Line(vh, tip, tip + new Vector2(-9, -14), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, tip, tip + new Vector2(9, -14), 2, NotebookPalette.Navy);
            BlueprintSketch.Ring(vh, c, 5, 3, NotebookPalette.Navy);
            BlueprintSketch.Ring(vh, Endpoint, 9, 5, NotebookPalette.Red);
            BlueprintSketch.Ring(vh, c + Vector2.up * DisplayRadius, 3, 2, NotebookPalette.Navy);
            BlueprintSketch.Ring(vh, c + Vector2.up * DisplayRadius * Mathf.Sqrt(maximumAspect), 3, 2, NotebookPalette.Navy);
        }
    }
}
