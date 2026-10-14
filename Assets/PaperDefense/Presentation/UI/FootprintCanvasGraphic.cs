using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Author input and normalized preview share axes, but keep independent scales.</summary>
    public sealed class FootprintCanvasGraphic : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        readonly List<Point2> pending = new List<Point2>();
        IReadOnlyList<Point2> points = Array.Empty<Point2>();
        FootprintContourAssessment assessment;
        bool drawing, closed = true;
        int pointerId;
        double extent = 3.25;
        InputSettings inputSettings;
        public bool Interactive { get; set; }
        public bool HasPendingGesture => drawing;
        public Action Began { get; set; }
        public Action<IReadOnlyList<Point2>> Finished { get; set; }
        public Action Canceled { get; set; }
        public Action UndoRequested { get; set; }
        public IReadOnlyList<Point2> DisplayedPoints => points;
        double Scale => Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / (extent * 2);
        Vector2 Center => rectTransform.rect.center;
        Vector2 View(Point2 p) => Center + new Vector2((float)(p.X*Scale),(float)(p.Y*Scale));
        public Vector2 LocalPointToScreen(Point2 p) => RectTransformUtility.WorldToScreenPoint(null,rectTransform.TransformPoint(View(p)));

        public void Show(IReadOnlyList<Point2> value, FootprintContourAssessment result = null, bool isClosed = true)
        {
            points = value ?? Array.Empty<Point2>(); assessment = result; closed = isClosed; extent = 3.25f;
            foreach (var p in points) extent = Math.Max(extent,Math.Max(Math.Abs((double)p.X),Math.Abs((double)p.Y))*1.12);
            SetVerticesDirty();
        }
        public void CancelGesture()
        {
            bool notify = drawing; drawing = false; pending.Clear(); SetVerticesDirty();
            if (notify) Canceled?.Invoke();
        }
        protected override void OnEnable() { base.OnEnable(); if(Interactive) inputSettings=PointerEventPolicy.Acquire(); }
        protected override void OnDisable() { CancelGesture(); if(inputSettings!=null)PointerEventPolicy.Release(inputSettings);inputSettings=null;base.OnDisable(); }
        void OnApplicationFocus(bool focused) { if(!focused)CancelGesture(); }
        void OnApplicationPause(bool paused) { if(paused)CancelGesture(); }
        void Update()
        {
            if(!Interactive||Keyboard.current==null)return;
            var keyboard=Keyboard.current;
            if(keyboard.escapeKey.wasPressedThisFrame)CancelGesture();
            if(keyboard.ctrlKey.isPressed&&keyboard.zKey.wasPressedThisFrame){CancelGesture();UndoRequested?.Invoke();}
        }
        bool Read(PointerEventData data,out Point2 p)
        {
            p=default;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var local)||!rectTransform.rect.Contains(local))return false;
            double x=(local.x-Center.x)/Scale,y=(local.y-Center.y)/Scale;
            if(double.IsNaN(x)||double.IsNaN(y)||Math.Abs(x)>float.MaxValue||Math.Abs(y)>float.MaxValue)return false;
            p=new Point2((float)x,(float)y);return true;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(!Interactive||!isActiveAndEnabled||data.button!=PointerEventData.InputButton.Left||!Read(data,out var p))return;
            CancelGesture();drawing=true;pointerId=data.pointerId;pending.Add(p);Began?.Invoke();SetVerticesDirty();
        }
        public void OnDrag(PointerEventData data)
        {
            if(!drawing||data.pointerId!=pointerId)return;
            if(!Read(data,out var p)){CancelGesture();return;}
            Add(p,false);
        }
        void Add(Point2 p,bool final)
        {
            if(pending.Count>FootprintContour.MaximumPoints)return;
            var last=pending[pending.Count-1];double x=((double)p.X-last.X)*Scale,y=((double)p.Y-last.Y)*Scale;
            if(x*x+y*y>(final ? .01f : 16))pending.Add(p);
            SetVerticesDirty();
        }
        public void OnPointerUp(PointerEventData data)
        {
            if(!drawing||data.pointerId!=pointerId||data.button!=PointerEventData.InputButton.Left)return;
            if(!Read(data,out var p)){CancelGesture();return;}
            Add(p,true);drawing=false;var owned=pending.ToArray();pending.Clear();Finished?.Invoke(owned);SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;
            var grid=MeshPen.Alpha(NotebookPalette.Blue,.13f);
            double gridStep=Interactive ? extent/3.25 : 1;
            for(int i=-3;i<=3;i++)
            {
                float offset=(float)(i*gridStep*Scale);
                BlueprintSketch.Line(vh,new Vector2(Center.x+offset,rect.yMin),new Vector2(Center.x+offset,rect.yMax),1,grid);
                BlueprintSketch.Line(vh,new Vector2(rect.xMin,Center.y+offset),new Vector2(rect.xMax,Center.y+offset),1,grid);
            }
            IReadOnlyList<Point2> visible=drawing?pending:points;
            bool valid=!drawing&&(assessment==null||assessment.Valid)&&closed;
            if(!Interactive&&valid)Hatch(vh,visible);
            int edges=drawing||!closed?visible.Count-1:visible.Count;
            for(int i=0;i<edges;i++)
            {
                bool bad=!drawing&&assessment!=null&&!assessment.Valid&&(assessment.FirstEdge<0||assessment.FirstEdge==i||assessment.SecondEdge==i);
                Color ink=bad?NotebookPalette.Red:Interactive?NotebookPalette.Blue:NotebookPalette.Green;
                BlueprintSketch.Line(vh,View(visible[i]),View(visible[(i+1)%visible.Count]),bad?4:2.5f,ink);
            }
            if(!drawing&&!closed&&visible.Count>1)
            {
                var a=View(visible[visible.Count-1]);var b=View(visible[0]);
                for(int i=0;i<16;i+=2)BlueprintSketch.Line(vh,Vector2.Lerp(a,b,i/16f),Vector2.Lerp(a,b,(i+1)/16f),2,NotebookPalette.Red);
            }
            if(drawing&&visible.Count>0)BlueprintSketch.Ring(vh,View(visible[0]),8,2,NotebookPalette.Green);
            BlueprintSketch.Ring(vh,Center,6,2,NotebookPalette.Navy);
            BlueprintSketch.Line(vh,Center+Vector2.left*12,Center+Vector2.right*12,1.5f,NotebookPalette.Navy);
            BlueprintSketch.Line(vh,Center+Vector2.down*12,Center+Vector2.up*12,1.5f,NotebookPalette.Navy);
            var tip=new Vector2(Center.x,rect.yMax-14);
            BlueprintSketch.Line(vh,tip-Vector2.up*32,tip,2,NotebookPalette.Navy);
            BlueprintSketch.Line(vh,tip,tip+new Vector2(-7,-10),2,NotebookPalette.Navy);
            BlueprintSketch.Line(vh,tip,tip+new Vector2(7,-10),2,NotebookPalette.Navy);
            if(!Interactive)
            {
                var a=new Vector2(rect.xMin+14,rect.yMin+16);var b=a+Vector2.right*(float)Scale;
                BlueprintSketch.Line(vh,a,b,2,NotebookPalette.Navy);
                BlueprintSketch.Line(vh,a-Vector2.up*4,a+Vector2.up*4,2,NotebookPalette.Navy);
                BlueprintSketch.Line(vh,b-Vector2.up*4,b+Vector2.up*4,2,NotebookPalette.Navy);
            }
        }
        void Hatch(VertexHelper vh,IReadOnlyList<Point2> contour)
        {
            if(contour.Count<3)return;
            var crossings=new List<float>();
            for(float y=-3;y<=3;y+=.15f)
            {
                crossings.Clear();
                for(int i=0;i<contour.Count;i++)
                {
                    var a=contour[i];var b=contour[(i+1)%contour.Count];
                    if((a.Y>y)!=(b.Y>y))crossings.Add(a.X+(b.X-a.X)*(y-a.Y)/(b.Y-a.Y));
                }
                crossings.Sort();
                for(int i=0;i+1<crossings.Count;i+=2)BlueprintSketch.Line(vh,View(new Point2(crossings[i],y)),View(new Point2(crossings[i+1],y)),1,MeshPen.Alpha(NotebookPalette.Green,.22f));
            }
        }
    }
}
