using System;
using PaperDefense.Core;
using UnityEngine;
namespace PaperDefense.Presentation
{
    /// <summary>A transient recording of one selected mount; it never applies the drawing.</summary>
    public sealed class DrawingProbePanel
    {
        readonly DrawingWorkbenchUi ui;
        readonly Func<TowerBlueprint,int,float,DrawingProbeFormation,DrawingProbeReport> probe;
        readonly Func<bool> pending;
        readonly GameObject root;
        readonly UnityEngine.UI.Text conditions, distanceLabel, result, formationLabel;
        readonly UnityEngine.UI.Button fire;
        readonly FootprintProbeSketch sketch;
        TowerBlueprint candidate;
        string hash, blocked;
        float started;
        public DrawingProbeReport Recording { get; private set; }
        public float Distance { get; private set; }=6;
        public int Mount { get; private set; }
        public DrawingProbeFormation Formation { get; private set; }
        internal DrawingProbePanel(Transform parent,DrawingWorkbenchUi ui,
            Func<TowerBlueprint,int,float,DrawingProbeFormation,DrawingProbeReport> probe,Func<bool> pending,Action refresh)
        {
            this.ui=ui;this.probe=probe;this.pending=pending;
            root=DrawingWorkbenchUi.Box(parent,"Drawing probe pane",new Rect(1096,595,440,333),Color.clear).gameObject;
            conditions=ui.Label(root.transform,"Drawing probe conditions","",new Rect(0,0,440,46),16);
            ui.Button(root.transform,"Drawing probe nearer",new Rect(0,50,40,32),"−",()=>SetDistance(Distance-1));
            distanceLabel=ui.Label(root.transform,"Drawing probe distance","",new Rect(47,50,340,32),18);
            distanceLabel.alignment=TextAnchor.MiddleCenter;
            ui.Button(root.transform,"Drawing probe farther",new Rect(400,50,40,32),"+",()=>SetDistance(Distance+1));
            var view=DrawingWorkbenchUi.Box(root.transform,"Drawing probe viewport",new Rect(0,88,440,128),new Color(1,.995f,.95f));
            view.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var go=new GameObject("Drawing probe sketch",typeof(RectTransform),typeof(CanvasRenderer),typeof(FootprintProbeSketch));
            go.transform.SetParent(view,false);DrawingWorkbenchUi.Place(go.GetComponent<RectTransform>(),new Rect(0,0,440,128));
            sketch=go.GetComponent<FootprintProbeSketch>();sketch.raycastTarget=false;
            result=ui.Label(root.transform,"Drawing probe result","",new Rect(0,220,440,72),16);
            fire=ui.Button(root.transform,"Drawing probe fire",new Rect(0,297,215,36),Copy("fire"),()=>{refresh();Record();},true);
            formationLabel=ui.Button(root.transform,"Drawing probe formation",new Rect(225,297,215,36),"",()=>SetFormation((DrawingProbeFormation)(((int)Formation+1)%3))).GetComponentInChildren<UnityEngine.UI.Text>();
        }
        static string Copy(string key)=>UserText.Message("drawing.probe."+key);
        internal void Reset(){candidate=null;hash=null;blocked=null;Recording=null;Distance=6;Formation=DrawingProbeFormation.Single;Mount=0;sketch.SetFrames(null,null,default);}
        internal void Show(bool visible)=>root.SetActive(visible);
        internal void Bind(TowerBlueprint value,int mount,string reason)
        {
            bool changed=hash!=value.Drawing.GeometryHash||Mount!=mount||blocked!=reason;
            candidate=value;hash=value.Drawing.GeometryHash;Mount=mount;blocked=reason;
            var content=value.Content??GameBalance.CreateDefault().ResolveContent();
            string id=value.GetWeaponId(mount);
            if(id==null||!content.TryGetWeapon(id,out var weapon))blocked=Copy("no_mount");
            else if(weapon.Attack==AttackKind.Cleanup)blocked=Copy("cleanup");
            conditions.text=ui.Resolve(UserText.Message("drawing.probe.conditions",mount+1));
            fire.interactable=string.IsNullOrEmpty(blocked);
            if(changed||Recording==null)Invalidate();
        }
        public void SetDistance(float distance)
        {
            if(float.IsNaN(distance)||float.IsInfinity(distance))return;
            float next=Mathf.Clamp(distance,1,DrawingProbe.MaximumDistance);if(next==Distance)return;
            Distance=next;Invalidate();
        }
        public void SetFormation(DrawingProbeFormation formation)
        {
            if(!Enum.IsDefined(typeof(DrawingProbeFormation),formation)||Formation==formation)return;
            Formation=formation;Invalidate();
        }
        public void Invalidate()
        {
            Recording=null;
            distanceLabel.text=ui.Resolve(UserText.Message("drawing.probe.distance",Distance));
            formationLabel.text=ui.Resolve(Copy(Formation.ToString().ToLowerInvariant()));
            result.text=ui.Resolve(string.IsNullOrEmpty(blocked)?Copy("ready"):blocked);
            result.color=string.IsNullOrEmpty(blocked)?NotebookPalette.Navy:NotebookPalette.Red;
            sketch.SetFrames(candidate==null?null:new[]{DrawingProbe.Preview(candidate,Distance,Formation)},()=>0,Bounds());
        }
        Rect Bounds()=>new Rect(9,7.5f,Distance+6,6);
        public void Record()
        {
            if(candidate==null)return;
            if(pending()){Invalidate();result.text=ui.Resolve(Copy("pending"));return;}
            if(!string.IsNullOrEmpty(blocked)){Invalidate();return;}
            try
            {
                Recording=probe(candidate,Mount,Distance,Formation);started=Time.unscaledTime;
                sketch.SetFrames(Recording.Frames,()=>Time.unscaledTime-started,Bounds());
                result.text=ui.Resolve(UserText.Message("drawing.probe.result",Recording.Hits,Recording.Targets,Recording.Damage,Recording.Cost,Recording.Message));
                result.color=Recording.Completed?NotebookPalette.Navy:NotebookPalette.Red;
            }
            catch(ArgumentException error){Invalidate();result.text=ui.Resolve(error.Message);result.color=NotebookPalette.Red;}
        }
    }
}
