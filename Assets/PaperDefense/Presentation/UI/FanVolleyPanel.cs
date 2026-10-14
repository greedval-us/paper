using System;
using System.Collections.Generic;
using System.Text;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace PaperDefense.Presentation
{
    public sealed class FanVolleyPanel
    {
        readonly GameObject sheet;
        readonly Action<TowerBlueprint> change;
        readonly Func<TowerBlueprint,int,FanFormation,FanVolleyProbeReport> probe;
        readonly UserTextResolver resolver;
        readonly Text conditions,feedback,allocation,result,changes;
        FanVolley baseline;
        readonly Button[] selectors=new Button[FanVolley.MaximumGuides];
        readonly Button[] formations=new Button[3];
        readonly List<FanVolley> history=new List<FanVolley>();
        readonly FootprintProbeSketch replay;
        TowerBlueprint blueprint;WeaponDefinition weapon;FanFormation formation;int selected=-1;float started;bool hasRejected;
        public FanVolleyGraphic Canvas { get; }
        public FanVolleyGraphic Working { get; }
        public FanVolleyProbeReport Recording { get; private set; }
        public bool IsOpen=>sheet.activeSelf;
        public int Mount { get; private set; }
        FanVolley Current=>blueprint.GetFanVolley(Mount);
        string Resolve(string text)=>GameText.Resolve(resolver,text);
        public FanVolleyPanel(Transform parent,Font font,Action<TowerBlueprint> change,Func<TowerBlueprint,int,FanFormation,FanVolleyProbeReport> probe,UserTextResolver resolver=null)
        {
            this.change=change;this.probe=probe;this.resolver=resolver;
            sheet=new GameObject("Fan volley sheet",typeof(RectTransform),typeof(Image));sheet.transform.SetParent(parent,false);
            Place(sheet.GetComponent<RectTransform>(),new Rect(20,25,1560,950));sheet.GetComponent<Image>().color=NotebookPalette.Paper;
            Label(font,"Volley title",UserText.Message("volley.title"),new Rect(30,18,1480,44),30);
            conditions=Label(font,"Volley conditions","",new Rect(30,75,1480,75),19);
            Label(font,"Volley authored title",UserText.Message("volley.authored"),new Rect(30,164,470,35),23);
            Label(font,"Volley working title",UserText.Message("volley.working"),new Rect(530,164,370,35),23);
            Label(font,"Volley trial title",UserText.Message("volley.trial"),new Rect(940,164,590,35),23);
            Canvas=Graphic("Volley canvas",new Rect(30,210,470,310),true);
            Working=Graphic("Volley working",new Rect(530,210,370,310),false);
            changes=Label(font,"Volley change comparison","",new Rect(30,530,870,100),17);
            Canvas.Began=()=>{Clear();changes.text=Resolve(UserText.Message("change.waiting"));feedback.text=Resolve(UserText.Message("volley.drawing"));};Canvas.Finished=AddGuide;
            Canvas.Canceled=()=>{if(IsOpen&&blueprint!=null)Show();Clear();feedback.text=Resolve(UserText.Message("volley.canceled"));};Canvas.UndoRequested=Undo;
            Canvas.Selected=Select;Canvas.Rejected=message=>Reject(message,null);
            var view=new GameObject("Volley replay",typeof(RectTransform),typeof(CanvasRenderer),typeof(FootprintProbeSketch));view.transform.SetParent(sheet.transform,false);
            Place(view.GetComponent<RectTransform>(),new Rect(940,210,590,410));replay=view.GetComponent<FootprintProbeSketch>();replay.raycastTarget=false;
            feedback=Label(font,"Volley feedback","",new Rect(30,640,470,95),18);
            allocation=Label(font,"Volley allocation","",new Rect(530,640,370,150),17);
            for(int i=0;i<selectors.Length;i++){int index=i;selectors[i]=Button(font,"Volley select "+i,UserText.Message("volley.select",i+1),new Rect(30+i*78,740,70,40),()=>Select(index));}
            for(int i=0;i<3;i++)
            {
                int index=i;formations[i]=Button(font,"Volley formation "+i,UserText.Message(i==0?"volley.single":i==1?"volley.front_targets":"volley.split_targets"),new Rect(940+i*200,645,190,44),()=>{formation=(FanFormation)index;Record();});
                int count=i+1;Button(font,"Volley preset "+count,UserText.Message("volley.preset",count),new Rect(30+i*175,800,165,46),()=>Preset(count));
            }
            result=Label(font,"Volley result","",new Rect(940,705,590,85),19);
            Button(font,"Volley undo",UserText.Message("imprint.undo"),new Rect(565,800,160,46),Undo);
            Button(font,"Volley delete",UserText.Message("volley.delete"),new Rect(740,800,170,46),DeleteSelected);
            Button(font,"Volley fire",UserText.Message("imprint.fire"),new Rect(940,800,300,46),Record);
            Button(font,"Volley legacy",UserText.Message("volley.legacy_button"),new Rect(30,875,260,48),()=>Commit(null));
            Label(font,"Volley save hint",UserText.Message("volley.save"),new Rect(320,870,855,70),18);
            Button(font,"Close volley",UserText.Message("imprint.done"),new Rect(1220,875,310,48),Close);
            sheet.SetActive(false);
        }
        public void Open(TowerBlueprint value,int mount)
        {
            value.GetFanVolley(mount);Close();blueprint=value;Mount=mount;selected=-1;history.Clear();formation=FanFormation.Single;
            baseline=value.GetFanVolley(mount);
            weapon=(value.Content??GameBalance.CreateDefault().ResolveContent()).GetWeapon(value.GetWeaponId(mount));
            sheet.SetActive(true);sheet.transform.SetAsLastSibling();Show();Record();ConfineNavigation();
            var first=sheet.GetComponentsInChildren<Button>(true)[0];if(EventSystem.current!=null&&sheet.activeInHierarchy)EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        public void Close()
        {
            Canvas.CancelGesture();var focus=EventSystem.current?.currentSelectedGameObject;
            if(focus!=null&&focus.transform.IsChildOf(sheet.transform))EventSystem.current.SetSelectedGameObject(null);
            sheet.SetActive(false);Clear();
        }
        public void AddGuide(IReadOnlyList<Point2> points)
        {
            if(!IsOpen)return;
            try
            {
                var guide=new FanGuide(points);var guides=new List<FanGuide>(Current?.Guides??Array.Empty<FanGuide>());guides.Add(guide);
                var value=new FanVolley(guides);value.Validate(weapon);Commit(value);
                for(int i=0;i<value.Guides.Count;i++)if(ReferenceEquals(value.Guides[i],guide))Select(i);
            }
            catch(ArgumentException error){Reject(error.Message,points);}
        }
        public void Preset(int count)
        {
            if(!IsOpen)return;Canvas.CancelGesture();
            try
            {
                if(count<1||count>FanVolley.MaximumGuides)throw new ArgumentException(UserText.Message("volley.count"));
                float angle=Math.Min(30,weapon.Stats.ConeAngleDegrees/2);var guides=new List<FanGuide>();
                for(int i=0;i<count;i++)guides.Add(FanGuide.FromAngle(count==1?0:-angle+2*angle*i/(count-1)));
                Commit(new FanVolley(guides));
            }
            catch(ArgumentException error){Reject(error.Message,null);}
        }
        public void Undo()
        {
            if(!IsOpen)return;Canvas.CancelGesture();
            if(hasRejected){Show();Record();return;}
            if(history.Count==0){Show();Record();return;}
            var value=history[history.Count-1];history.RemoveAt(history.Count-1);Commit(value,false);
        }
        public void DeleteSelected()
        {
            if(!IsOpen||Current==null||selected<0||selected>=Current.Guides.Count)return;Canvas.CancelGesture();
            if(Current.Guides.Count==1){Reject(UserText.Message("volley.keep_one"),null);return;}
            var guides=new List<FanGuide>(Current.Guides);guides.RemoveAt(selected);Commit(new FanVolley(guides));
        }
        void Select(int index){if(!IsOpen)return;selected=index;Show();}
        void Commit(FanVolley value,bool remember=true)
        {
            if(!IsOpen)return;Canvas.CancelGesture();value?.Validate(weapon);
            if(remember){if(history.Count==32)history.RemoveAt(0);history.Add(Current);}
            blueprint=blueprint.WithFanVolley(Mount,value);change(blueprint);selected=-1;Show();Record();
        }
        void Show()
        {
            hasRejected=false;
            float angle=Math.Min(85,weapon.Stats.ConeAngleDegrees/2);
            Canvas.Show(Current,selected,weapon.VolleyPellets,angle);Working.Show(Current,selected,weapon.VolleyPellets,angle);
            conditions.text=Resolve(UserText.Message("volley.conditions",Mount+1,weapon.VolleyPellets,angle,weapon.VolleyHitRadius*2));
            feedback.color=NotebookPalette.Navy;feedback.text=Resolve(UserText.Message(Current==null?"volley.legacy":"volley.ready"));
            changes.text=Resolve(DesignChangeFeedback.Volley(baseline,Current,weapon));
            var text=new StringBuilder();
            if(Current!=null&&Current.Guides.Count<=weapon.VolleyPellets)
            {
                var counts=Current.Allocate(weapon.VolleyPellets);
                for(int i=0;i<counts.Length;i++){if(i>0)text.Append('\n');text.Append(UserText.Message("volley.allocation",i+1,Current.Guides[i].AngleDegrees,counts[i]));}
            }
            allocation.text=Resolve(text.ToString());
            for(int i=0;i<selectors.Length;i++){selectors[i].interactable=Current!=null&&i<Current.Guides.Count;selectors[i].GetComponent<Image>().color=i==selected?new Color(.83f,.90f,.94f):Color.white;}
            ConfineNavigation();
        }
        void Reject(string message,IReadOnlyList<Point2> points)
        {
            hasRejected=true;Clear();feedback.color=NotebookPalette.Red;feedback.text=Resolve(message);
            changes.text=Resolve(UserText.Message("change.invalid"));
            List<Point2> drawable=null;
            if(points!=null&&points.Count<=FanGuide.MaximumPoints+1)
            {
                drawable=new List<Point2>();foreach(var p in points){if(float.IsNaN(p.X)||float.IsNaN(p.Y)||float.IsInfinity(p.X)||float.IsInfinity(p.Y)||Math.Abs(p.X)>4||Math.Abs(p.Y)>4){drawable.Clear();break;}drawable.Add(p);}
            }
            Canvas.Show(Current,selected,weapon.VolleyPellets,Math.Min(85,weapon.Stats.ConeAngleDegrees/2),drawable);
        }
        void Clear(){Recording=null;replay.SetRecording(null,()=>0);result.text=Resolve(UserText.Message("volley.no_trial"));}
        public void Record()
        {
            if(!IsOpen||Canvas.HasGesture||hasRejected)return;
            try
            {
                Recording=probe(blueprint,Mount,formation);started=Time.unscaledTime;float range=Recording.Range;
                replay.SetFrames(Recording.Frames,()=>Time.unscaledTime-started,new Rect(9.5f,9.5f-range,range+2,range*2+2));
                result.text=Resolve(Recording.Completed?UserText.Message("volley.result",Recording.Hits,Recording.Targets,Recording.Pellets,Recording.Damage,Recording.Cost):UserText.Message("study.incomplete"));
            }
            catch(ArgumentException error){Clear();result.text=Resolve(error.Message);}
            for(int i=0;i<formations.Length;i++)formations[i].GetComponent<Image>().color=(int)formation==i?new Color(.83f,.90f,.94f):Color.white;
        }
        void ConfineNavigation()
        {
            var buttons=sheet.GetComponentsInChildren<Button>(true);
            foreach(var button in buttons)button.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=Neighbor(button,buttons,Vector2.up),selectOnDown=Neighbor(button,buttons,Vector2.down),selectOnLeft=Neighbor(button,buttons,Vector2.left),selectOnRight=Neighbor(button,buttons,Vector2.right)};
        }
        static Button Neighbor(Button from,Button[] buttons,Vector2 direction)
        {
            Button nearest=from;float best=0;var origin=Center(from);
            foreach(var candidate in buttons){if(candidate==from||!candidate.interactable)continue;var delta=Center(candidate)-origin;float score=Vector2.Dot(direction,delta)/delta.sqrMagnitude;if(score>best){best=score;nearest=candidate;}}
            return nearest;
        }
        static Vector2 Center(Button button){var rect=(RectTransform)button.transform;return rect.anchoredPosition+new Vector2(rect.rect.width/2,-rect.rect.height/2);}
        static void Place(RectTransform rect,Rect box)=>MortarFootprintPanel.Place(rect,box);
        FanVolleyGraphic Graphic(string name,Rect box,bool interactive)
        {var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(FanVolleyGraphic));go.transform.SetParent(sheet.transform,false);Place(go.GetComponent<RectTransform>(),box);var graphic=go.GetComponent<FanVolleyGraphic>();graphic.Interactive=interactive;graphic.raycastTarget=interactive;return graphic;}
        Text Label(Font font,string name,string text,Rect box,int size)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(sheet.transform,false);Place(go.GetComponent<RectTransform>(),box);var label=go.GetComponent<Text>();label.font=font;label.fontSize=size;label.text=Resolve(text);label.color=NotebookPalette.Navy;label.raycastTarget=false;return label;}
        Button Button(Font font,string name,string text,Rect box,Action action)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(sheet.transform,false);Place(go.GetComponent<RectTransform>(),box);go.GetComponent<Image>().color=Color.white;var button=go.GetComponent<Button>();button.onClick.AddListener(()=>action());var label=Label(font,name+" label",text,new Rect(5,4,box.width-10,box.height-8),18);label.transform.SetParent(go.transform,false);label.alignment=TextAnchor.MiddleCenter;return button;}
    }
}
