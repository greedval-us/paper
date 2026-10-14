using System;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed class ArmorReinforcementGraphic : MaskableGraphic,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        ArmorDistribution value,pending;ArmorProbeReport report;
        readonly bool[] visited=new bool[ArmorDistribution.SectorCount];
        bool drawing,changed,erase;int pointerId,selected=-1;float began;
        InputSettings settings;
        public bool Interactive { get; set; }
        public bool HasGesture=>drawing;
        public Action Began,Canceled,UndoRequested;
        public Action<int> Selected;
        public Action<ArmorDistribution> Preview,Finished;
        public Action<string> Feedback;
        float Radius=>Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.31f;
        public Vector2 LocalPointToScreen(Point2 offset)=>RectTransformUtility.WorldToScreenPoint(null,rectTransform.TransformPoint(rectTransform.rect.center+new Vector2(offset.X,offset.Y)*Radius));
        public void Show(ArmorDistribution distribution,int selection=-1,ArmorProbeReport recording=null)
        {value=distribution;selected=selection;report=recording;began=Time.unscaledTime;SetVerticesDirty();}
        protected override void OnEnable(){base.OnEnable();if(Interactive)settings=PointerEventPolicy.Acquire();}
        protected override void OnDisable(){CancelGesture();if(settings!=null)PointerEventPolicy.Release(settings);settings=null;base.OnDisable();}
        void OnApplicationFocus(bool focused){if(!focused)CancelGesture();}
        void OnApplicationPause(bool paused){if(paused)CancelGesture();}
        void Update()
        {
            if(report!=null&&Time.unscaledTime-began<3)SetVerticesDirty();
            if(!Interactive||Keyboard.current==null)return;
            if(Keyboard.current.escapeKey.wasPressedThisFrame)CancelGesture();
            if(Keyboard.current.ctrlKey.isPressed&&Keyboard.current.zKey.wasPressedThisFrame)UndoRequested?.Invoke();
        }
        public void CancelGesture()
        {bool notify=drawing;drawing=false;pending=null;SetVerticesDirty();if(notify)Canceled?.Invoke();}
        bool Read(PointerEventData data,out Point2 offset)
        {
            offset=default;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var local)||!rectTransform.rect.Contains(local))return false;
            var point=(local-rectTransform.rect.center)/Radius;offset=new Point2(point.x,point.y);return true;
        }
        static bool OnRing(Point2 p){float squared=p.X*p.X+p.Y*p.Y;return squared>=.36f&&squared<=2.4f;}
        public void OnPointerDown(PointerEventData data)
        {
            if(!Interactive||!isActiveAndEnabled||(data.button!=PointerEventData.InputButton.Left&&data.button!=PointerEventData.InputButton.Right)||!Read(data,out var point)||!OnRing(point))return;
            CancelGesture();drawing=true;changed=false;erase=data.button==PointerEventData.InputButton.Right;pointerId=data.pointerId;
            pending=value??new ArmorDistribution(new[]{0,0,0,0});Array.Clear(visited,0,visited.Length);Began?.Invoke();Visit(point);
        }
        void Visit(Point2 point)
        {
            if(!OnRing(point))return;
            int sector=pending.Sector(point);selected=sector;SetVerticesDirty();Selected?.Invoke(sector);
            if(visited[sector]){Preview?.Invoke(pending);return;}visited[sector]=true;
            if(erase?pending.Points[sector]==0:pending.Remaining==0){Preview?.Invoke(pending);Feedback?.Invoke(UserText.Message(erase?"reinforce.empty":"reinforce.no_stock"));return;}
            pending=pending.WithPoint(sector,erase?-1:1);changed=true;Preview?.Invoke(pending);SetVerticesDirty();
        }
        public void OnDrag(PointerEventData data)
        {if(!drawing||pointerId!=data.pointerId)return;if(!Read(data,out var point)){CancelGesture();return;}Visit(point);}
        public void OnPointerUp(PointerEventData data)
        {
            if(!drawing||pointerId!=data.pointerId||data.button!=(erase?PointerEventData.InputButton.Right:PointerEventData.InputButton.Left))return;
            if(!Read(data,out var point)){CancelGesture();return;}Visit(point);drawing=false;var accepted=pending;pending=null;
            if(changed)Finished?.Invoke(accepted);else Canceled?.Invoke();SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var center=rectTransform.rect.center;float radius=Radius;
            var current=drawing?pending:value;var outline=MeshPen.Alpha(NotebookPalette.Navy,.22f);
            for(int i=0;i<4;i++)
            {
                float a=(45+i*90)*Mathf.Deg2Rad;var direction=new Vector2(Mathf.Sin(a),Mathf.Cos(a));
                BlueprintSketch.Line(vh,center+direction*radius*.55f,center+direction*radius*1.27f,1,outline);
            }
            DrawSectors(vh,center,radius,current,selected);
            if(report!=null)
            {
                float elapsed=Time.unscaledTime-began;
                for(int i=0;i<report.Hits.Count;i++)
                {
                    if(elapsed<i*.5f)continue;var hit=report.Hits[i];var point=ArmorProbe.Direction(hit.WorldSide);var d=new Vector2(point.X,point.Y);
                    var from=center+d*radius*1.5f;var to=center+d*radius*.83f;
                    BlueprintSketch.Line(vh,from,to,3,NotebookPalette.Red);
                    var tangent=new Vector2(-d.y,d.x);BlueprintSketch.Line(vh,to,to+d*10+tangent*5,3,NotebookPalette.Red);BlueprintSketch.Line(vh,to,to+d*10-tangent*5,3,NotebookPalette.Red);
                }
            }
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
    }
}
