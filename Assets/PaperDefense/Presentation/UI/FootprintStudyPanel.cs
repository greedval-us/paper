using System;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Controlled comparisons; results stay hidden until the player starts the replay.</summary>
    public sealed class FootprintStudyPanel
    {
        readonly GameObject sheet;
        readonly UserTextResolver resolver;
        readonly Func<TowerBlueprint,int,FootprintStudyReport> run;
        readonly Action<MortarFootprint> apply;
        readonly Text conditions,description,matrix;
        readonly Text[] metrics=new Text[3];
        readonly FootprintProbeSketch[] views=new FootprintProbeSketch[3];
        readonly FootprintCanvasGraphic[] shapes=new FootprintCanvasGraphic[3];
        readonly Button[] use=new Button[3];
        readonly Button[] scenarioButtons=new Button[3],repeatButtons=new Button[3];
        readonly Button startButton;
        readonly bool[] revealed=new bool[3];
        Rect bounds;
        float started,duration;
        bool playing;
        public FootprintStudyReport Report { get; private set; }
        public bool IsOpen => sheet.activeSelf;
        public FootprintStudyScenario Scenario { get; private set; }
        public int Repeat { get; private set; }
        public float PlaybackSeconds => !playing?0:Mathf.Min(Time.unscaledTime-started,duration);
        string Resolve(string value)=>GameText.Resolve(resolver,value);

        public FootprintStudyPanel(Transform parent,Font font,Func<TowerBlueprint,int,FootprintStudyReport> run,Action<MortarFootprint> apply,UserTextResolver resolver=null)
        {
            this.run=run;this.apply=apply;this.resolver=resolver;
            sheet=new GameObject("Footprint study sheet",typeof(RectTransform),typeof(Image));sheet.transform.SetParent(parent,false);
            Place(sheet.GetComponent<RectTransform>(),new Rect(20,25,1560,950));sheet.GetComponent<Image>().color=NotebookPalette.Paper;
            Label(font,"Study title",UserText.Message("study.title"),new Rect(30,18,1490,42),28);
            conditions=Label(font,"Study conditions","",new Rect(30,70,1490,65),19);
            for(int i=0;i<3;i++)
            {
                int index=i;
                scenarioButtons[i]=Button(font,"Study scenario "+i,ScenarioName((FootprintStudyScenario)i),new Rect(30+i*510,145,470,45),()=>SelectScenario((FootprintStudyScenario)index));
                Label(font,"Study form "+i,ShapeName((FootprintStudyShape)i),new Rect(30+i*510,250,470,35),23);
                var shape=new GameObject("Study shape "+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(FootprintCanvasGraphic));shape.transform.SetParent(sheet.transform,false);
                Place(shape.GetComponent<RectTransform>(),new Rect(30+i*510,290,470,110));shapes[i]=shape.GetComponent<FootprintCanvasGraphic>();shapes[i].raycastTarget=false;
                var view=new GameObject("Study replay "+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(FootprintProbeSketch));view.transform.SetParent(sheet.transform,false);
                Place(view.GetComponent<RectTransform>(),new Rect(30+i*510,410,470,225));views[i]=view.GetComponent<FootprintProbeSketch>();views[i].raycastTarget=false;
                metrics[i]=Label(font,"Study metrics "+i,"",new Rect(30+i*510,645,470,42),19);
                use[i]=Button(font,"Study use "+i,UserText.Message("study.use"),new Rect(30+i*510,695,470,40),()=>Use((FootprintStudyShape)index));
                repeatButtons[i]=Button(font,"Study repeat "+i,UserText.Message("study.repeat",i+1),new Rect(30+i*140,870,130,48),()=>SelectRepeat(index));
            }
            description=Label(font,"Study scenario description","",new Rect(30,205,1490,43),19);
            matrix=Label(font,"Study matrix","",new Rect(30,748,1490,112),18);
            startButton=Button(font,"Study run",UserText.Message("study.run"),new Rect(475,870,345,48),StartReplay);
            Label(font,"Study scope",UserText.Message("study.scope"),new Rect(850,865,350,60),17);
            Button(font,"Close study",UserText.Message("study.close"),new Rect(1220,870,310,48),Close);
            sheet.SetActive(false);
        }
        public void Open(TowerBlueprint source,int mount)
        {
            Close();Scenario=FootprintStudyScenario.Column;Repeat=0;Array.Clear(revealed,0,revealed.Length);
            sheet.SetActive(true);sheet.transform.SetAsLastSibling();
            try
            {
                Report=run(source,mount);bounds=Bounds(Report);
                conditions.text=Resolve(UserText.Message("study.conditions",mount+1,Report.Budget,Math.PI*Report.Radius*Report.Radius,Report.FlightSeconds));
                startButton.interactable=true;Refresh();
            }
            catch(ArgumentException error)
            {
                conditions.text=Resolve(UserText.Message("study.unavailable",error.Message));startButton.interactable=false;
                foreach(var button in use)button.interactable=false;
            }
            ConfineNavigation();
            if(EventSystem.current!=null&&sheet.activeInHierarchy)EventSystem.current.SetSelectedGameObject(scenarioButtons[0].gameObject);
        }
        public void Close()
        {
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&selected.transform.IsChildOf(sheet.transform))EventSystem.current.SetSelectedGameObject(null);
            playing=false;Report=null;sheet.SetActive(false);
            foreach(var view in views)if(view!=null)view.SetRecording(null,()=>0);
            foreach(var shape in shapes)if(shape!=null)shape.Show(Array.Empty<Point2>());
            foreach(var metric in metrics)if(metric!=null)metric.text="";
            if(matrix!=null)matrix.text="";if(description!=null)description.text="";
        }
        public void SelectScenario(FootprintStudyScenario value)
        {
            if(!Enum.IsDefined(typeof(FootprintStudyScenario),value))throw new ArgumentOutOfRangeException(nameof(value));
            if(!IsOpen||Report==null)return;Scenario=value;playing=false;Refresh();
        }
        public void SelectRepeat(int value)
        {
            if(value<0||value>=FootprintStudy.Repeats)throw new ArgumentOutOfRangeException(nameof(value));
            if(!IsOpen||Report==null)return;Repeat=value;playing=false;Refresh();
        }
        public void StartReplay()
        {
            if(!IsOpen||Report==null)return;revealed[(int)Scenario]=true;playing=true;started=Time.unscaledTime;Refresh();
        }
        void Use(FootprintStudyShape form)
        {
            if(!IsOpen||Report==null||!revealed[(int)Scenario])return;
            var row=Report.Find(Scenario,form);if(!row.Completed)return;
            var shape=row.Footprint;Close();apply(shape);
        }
        void Refresh()
        {
            duration=0;
            for(int i=0;i<3;i++)
            {
                var row=Report.Find(Scenario,(FootprintStudyShape)i);var sample=row.Samples[Repeat];var frames=sample.Recording.Frames;
                duration=Mathf.Max(duration,frames[frames.Count-1].Seconds);
                views[i].SetFrames(frames,()=>PlaybackSeconds,bounds);
                var geometry=FootprintGeometry.Resolve(row.Footprint,1,default,new Point2(0,1));var points=new Point2[geometry.OutlineCount];
                for(int p=0;p<points.Length;p++)points[p]=geometry.OutlineOffset(p);shapes[i].Show(points);
                metrics[i].text=Resolve(!revealed[(int)Scenario]?UserText.Message("study.predict"):
                    sample.Recording.Completed?UserText.Message("study.result",sample.Recording.HitCount,sample.Damage):UserText.Message("study.incomplete"));
                use[i].interactable=revealed[(int)Scenario]&&row.Completed;
                scenarioButtons[i].GetComponent<Image>().color=(int)Scenario==i?new Color(.83f,.90f,.94f):Color.white;
                repeatButtons[i].GetComponent<Image>().color=Repeat==i?new Color(.83f,.90f,.94f):Color.white;
            }
            description.text=Resolve(Scenario==FootprintStudyScenario.Turning
                ? UserText.Message("study.description.2",Report.Find(Scenario,FootprintStudyShape.Compact).Samples[Repeat].TurnSeconds)
                : UserText.Message(Scenario==FootprintStudyScenario.Column?"study.description.0":"study.description.1"));
            string text=UserText.Message("study.matrix_header");
            for(int s=0;s<3;s++)
            {
                var a=Report.Find((FootprintStudyScenario)s,FootprintStudyShape.Compact);var b=Report.Find((FootprintStudyScenario)s,FootprintStudyShape.Longitudinal);var c=Report.Find((FootprintStudyScenario)s,FootprintStudyShape.Transverse);
                text+="\n"+(!revealed[s]?UserText.Message("study.hidden_row",ScenarioName((FootprintStudyScenario)s)):
                    !a.Completed||!b.Completed||!c.Completed?UserText.Message("study.incomplete_row",ScenarioName((FootprintStudyScenario)s)):
                    UserText.Message("study.matrix_row",ScenarioName((FootprintStudyScenario)s),a.Hits,b.Hits,c.Hits));
            }
            matrix.text=Resolve(text);
            ConfineNavigation();
        }
        // Automatic navigation can select buttons in the covered editor or workshop.
        void ConfineNavigation()
        {
            var buttons=sheet.GetComponentsInChildren<Button>(true);
            foreach(var button in buttons)
                button.navigation=new Navigation { mode=Navigation.Mode.Explicit,
                    selectOnUp=Neighbor(button,buttons,Vector2.up),selectOnDown=Neighbor(button,buttons,Vector2.down),
                    selectOnLeft=Neighbor(button,buttons,Vector2.left),selectOnRight=Neighbor(button,buttons,Vector2.right) };
        }
        static Button Neighbor(Button from,Button[] candidates,Vector2 direction)
        {
            Button nearest=from;float best=0;var origin=Center(from);
            foreach(var candidate in candidates)
            {
                if(candidate==from||!candidate.interactable)continue;
                var delta=Center(candidate)-origin;float score=Vector2.Dot(direction,delta)/delta.sqrMagnitude;
                if(score>best){best=score;nearest=candidate;}
            }
            return nearest;
        }
        static Vector2 Center(Button button)
        {
            var rect=(RectTransform)button.transform;
            return rect.anchoredPosition+new Vector2(rect.rect.width/2,-rect.rect.height/2);
        }
        static Rect Bounds(FootprintStudyReport report)
        {
            float minX=10.5f,maxX=10.5f,minY=10.5f,maxY=10.5f;
            foreach(var row in report.Cases)foreach(var sample in row.Samples)foreach(var frame in sample.Recording.Frames)
            {
                foreach(var enemy in frame.Enemies){minX=Mathf.Min(minX,enemy.Position.X);maxX=Mathf.Max(maxX,enemy.Position.X);minY=Mathf.Min(minY,enemy.Position.Y);maxY=Mathf.Max(maxY,enemy.Position.Y);}
                foreach(var impact in frame.Impacts){float r=impact.ImpactFootprint?.BoundingRadius??impact.Radius;minX=Mathf.Min(minX,impact.To.X-r);maxX=Mathf.Max(maxX,impact.To.X+r);minY=Mathf.Min(minY,impact.To.Y-r);maxY=Mathf.Max(maxY,impact.To.Y+r);}
            }
            return Rect.MinMaxRect(minX-1,minY-1,maxX+1,maxY+1);
        }
        static string ShapeName(FootprintStudyShape value)=>UserText.Message(value==FootprintStudyShape.Compact?"study.compact":value==FootprintStudyShape.Longitudinal?"imprint.longitudinal":"imprint.transverse");
        static string ScenarioName(FootprintStudyScenario value)=>UserText.Message(value==FootprintStudyScenario.Column?"copy.322ef2503cd18ed7":value==FootprintStudyScenario.Dispersed?"study.dispersed":"study.turning");
        static void Place(RectTransform rect,Rect box)=>MortarFootprintPanel.Place(rect,box);
        Text Label(Font font,string name,string value,Rect box,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(sheet.transform,false);Place(go.GetComponent<RectTransform>(),box);
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.text=Resolve(value);text.color=NotebookPalette.Navy;text.raycastTarget=false;return text;
        }
        Button Button(Font font,string name,string value,Rect box,Action action)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(sheet.transform,false);Place(go.GetComponent<RectTransform>(),box);
            go.GetComponent<Image>().color=Color.white;var button=go.GetComponent<Button>();button.onClick.AddListener(()=>action());
            var label=Label(font,name+" label",value,new Rect(5,4,box.width-10,box.height-8),18);label.transform.SetParent(go.transform,false);label.alignment=TextAnchor.MiddleCenter;return button;
        }
    }
}
