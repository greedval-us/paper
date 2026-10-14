using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace PaperDefense.Presentation
{
    public sealed class FanVolleyGraphic : MaskableGraphic,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        readonly List<Point2> pending=new List<Point2>();
        FanVolley volley;IReadOnlyList<Point2> rejected;int selected=-1,pointerId,pellets=6;
        bool drawing;float maximumAngle=45;InputSettings settings;
        public bool Interactive { get; set; }
        public bool HasGesture=>drawing;
        public Action Began,Canceled,UndoRequested;
        public Action<IReadOnlyList<Point2>> Finished;
        public Action<int> Selected;
        public Action<string> Rejected;
        float Scale=>Mathf.Min(rectTransform.rect.width/6.5f,(rectTransform.rect.height-50)/3.5f);
        Vector2 Origin=>new Vector2(rectTransform.rect.center.x,rectTransform.rect.yMin+30);
        Vector2 View(Point2 point)=>Origin+new Vector2(point.X,point.Y)*Scale;
        public Vector2 LocalPointToScreen(Point2 point)=>RectTransformUtility.WorldToScreenPoint(null,rectTransform.TransformPoint(View(point)));
        public void Show(FanVolley value,int selection,int count,float angle,IReadOnlyList<Point2> invalid=null)
        {volley=value;selected=selection;pellets=count;maximumAngle=angle;rejected=invalid;SetVerticesDirty();}
        public void CancelGesture(){bool notify=drawing;drawing=false;pending.Clear();SetVerticesDirty();if(notify)Canceled?.Invoke();}
        protected override void OnEnable(){base.OnEnable();if(Interactive)settings=PointerEventPolicy.Acquire();}
        protected override void OnDisable(){CancelGesture();if(settings!=null)PointerEventPolicy.Release(settings);settings=null;base.OnDisable();}
        void OnApplicationFocus(bool focused){if(!focused)CancelGesture();}
        void OnApplicationPause(bool paused){if(paused)CancelGesture();}
        void Update()
        {
            if(!Interactive||Keyboard.current==null)return;
            if(Keyboard.current.escapeKey.wasPressedThisFrame)CancelGesture();
            if(Keyboard.current.ctrlKey.isPressed&&Keyboard.current.zKey.wasPressedThisFrame){CancelGesture();UndoRequested?.Invoke();}
        }
        bool Read(PointerEventData data,out Point2 point)
        {
            point=default;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var local)||!rectTransform.rect.Contains(local))return false;
            var p=(local-Origin)/Scale;point=new Point2(p.x,p.y);return true;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(!Interactive||!isActiveAndEnabled||data.button!=PointerEventData.InputButton.Left||!Read(data,out var point))return;
            CancelGesture();
            if(point.X*point.X+point.Y*point.Y>.04f)
            {
                if(volley!=null)for(int i=0;i<volley.Guides.Count;i++)
                {
                    var points=volley.Guides[i].Points;
                    if(Vector2.Distance(View(point),View(points[points.Count-1]))<18){Selected?.Invoke(i);return;}
                }
                Rejected?.Invoke(UserText.Message("volley.start"));return;
            }
            drawing=true;pointerId=data.pointerId;pending.Add(default);Began?.Invoke();SetVerticesDirty();
        }
        void Add(Point2 point,bool final)
        {
            if(pending.Count>FanGuide.MaximumPoints)return;
            if(Vector2.Distance(View(point),View(pending[pending.Count-1]))>(final ? .1f : 4)){pending.Add(point);SetVerticesDirty();}
        }
        public void OnDrag(PointerEventData data)
        {if(!drawing||pointerId!=data.pointerId)return;if(!Read(data,out var point)){CancelGesture();return;}Add(point,false);}
        public void OnPointerUp(PointerEventData data)
        {
            if(!drawing||pointerId!=data.pointerId||data.button!=PointerEventData.InputButton.Left)return;
            if(!Read(data,out var point)){CancelGesture();return;}
            Add(point,true);drawing=false;var points=pending.ToArray();pending.Clear();Finished?.Invoke(points);SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;var grid=MeshPen.Alpha(NotebookPalette.Blue,.12f);
            for(int i=-3;i<=3;i++)BlueprintSketch.Line(vh,Origin+Vector2.right*i*Scale,Origin+new Vector2(i,3.2f)*Scale,1,grid);
            for(int i=0;i<=3;i++)BlueprintSketch.Line(vh,new Vector2(rect.xMin,Origin.y+i*Scale),new Vector2(rect.xMax,Origin.y+i*Scale),1,grid);
            for(int sign=-1;sign<=1;sign+=2)
            {
                float a=maximumAngle*Mathf.Deg2Rad*sign;var end=Origin+new Vector2(Mathf.Sin(a),Mathf.Cos(a))*Scale*3;
                BlueprintSketch.Line(vh,Origin,end,1.5f,MeshPen.Alpha(NotebookPalette.Red,.35f));
            }
            if(volley!=null)
            {
                var counts=volley.Guides.Count<=pellets?volley.Allocate(pellets):new int[volley.Guides.Count];
                for(int i=0;i<volley.Guides.Count;i++)
                {
                    var guide=volley.Guides[i];var color=i==selected?NotebookPalette.Marker:Interactive?NotebookPalette.Blue:NotebookPalette.Green;
                    if(Interactive)
                    {
                        Path(vh,guide.Points,color);BlueprintSketch.Ring(vh,View(guide.Points[guide.Points.Count-1]),6,i==selected?3:1.5f,color);
                    }
                    else
                    {
                        float a=guide.AngleDegrees*Mathf.Deg2Rad;var direction=new Vector2(Mathf.Sin(a),Mathf.Cos(a));var tip=Origin+direction*Scale*2.8f;
                        BlueprintSketch.Line(vh,Origin,tip,2.5f,color);var side=new Vector2(-direction.y,direction.x);
                        BlueprintSketch.Line(vh,tip,tip-direction*10+side*5,2,color);BlueprintSketch.Line(vh,tip,tip-direction*10-side*5,2,color);
                        int dots=Math.Min(10,counts[i]);for(int n=0;n<dots;n++)BlueprintSketch.Ring(vh,Origin+direction*Scale*(1.4f+n*.1f),3,2,color);
                    }
                }
            }
            if(rejected!=null)Path(vh,rejected,NotebookPalette.Red);
            if(drawing)Path(vh,pending,NotebookPalette.Blue);
            BlueprintSketch.Ring(vh,Origin,10,3,NotebookPalette.Navy);
            BlueprintSketch.Line(vh,Origin+Vector2.down*18,Origin+Vector2.down*7,4,NotebookPalette.Navy);
        }
        void Path(VertexHelper vh,IReadOnlyList<Point2> points,Color color)
        {for(int i=1;i<points.Count;i++)BlueprintSketch.Line(vh,View(points[i-1]),View(points[i]),2.5f,color);}
    }
}
