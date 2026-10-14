using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed class ArmorReinforcementPanel
    {
        readonly GameObject sheet;
        readonly Action<TowerBlueprint> change;
        readonly Func<TowerBlueprint,int,ArmorProbeReport> probe;
        readonly Func<TowerBlueprint,float> defense;
        readonly float maximumDefense;
        readonly UserTextResolver resolver;
        readonly Text conditions,budget,feedback,result,changes;
        ArmorDistribution baseline;
        readonly Button[] selectors=new Button[4];
        readonly List<ArmorDistribution> history=new List<ArmorDistribution>();
        readonly BlueprintSketch authored,trialBody;
        readonly ArmorReinforcementGraphic trial;
        TowerBlueprint blueprint;int selected,worldSide=-1;
        public ArmorReinforcementGraphic Canvas { get; }
        public ArmorProbeReport Recording { get; private set; }
        public bool IsOpen=>sheet.activeSelf;
        ArmorDistribution Current=>blueprint.Reinforcement;
        string Resolve(string message)=>GameText.Resolve(resolver,message);
        string Text(string key,params object[] args)=>Resolve(UserText.Message(key,args));
        public ArmorReinforcementPanel(Transform parent,Font font,Action<TowerBlueprint> change,
            Func<TowerBlueprint,int,ArmorProbeReport> probe,Func<TowerBlueprint,float> defense,float maximumDefense,UserTextResolver resolver=null)
        {
            this.change=change;this.probe=probe;this.defense=defense;this.maximumDefense=maximumDefense;this.resolver=resolver;
            sheet=new GameObject("Reinforcement sheet",typeof(RectTransform),typeof(Image));sheet.transform.SetParent(parent,false);
            Place(sheet.GetComponent<RectTransform>(),new Rect(20,25,1560,950));sheet.GetComponent<Image>().color=NotebookPalette.Paper;
            Label(font,"Armor title",Text("reinforce.title"),new Rect(30,18,1480,44),30);
            conditions=Label(font,"Armor conditions","",new Rect(30,75,1480,87),19);
            Label(font,"Armor author heading",Text("reinforce.authored"),new Rect(30,170,470,38),23);
            Label(font,"Armor probe heading",Text("reinforce.trial"),new Rect(810,170,680,38),23);
            Canvas=Graphic("Armor canvas",new Rect(30,218,680,280),true);authored=Body(Canvas.transform,font);
            changes=Label(font,"Armor change comparison","",new Rect(30,510,680,102),17);
            trial=Graphic("Armor probe",new Rect(810,218,680,390),false);trialBody=Body(trial.transform,font);
            Canvas.Began=Clear;Canvas.Preview=Readout;Canvas.Finished=value=>Commit(value);
            Canvas.Selected=index=>selected=index;
            Canvas.Canceled=()=>{if(IsOpen&&blueprint!=null){Show();Clear();feedback.text=Text("reinforce.canceled");}};
            Canvas.Feedback=message=>feedback.text=Resolve(message);Canvas.UndoRequested=Undo;
            Button(font,"Armor rotate",Text("reinforce.rotate"),new Rect(500,170,210,42),Rotate);
            for(int i=0;i<4;i++)
            {
                int index=i;
                selectors[i]=Button(font,"Armor sector "+i,"",new Rect(30+i*175,622,165,64),()=>Select(index));
                Button(font,"Armor attack "+i,Text("reinforce.attack",Side(i)),new Rect(810+i*175,622,165,64),()=>Record(index));
            }
            Button(font,"Armor add",Text("reinforce.add"),new Rect(30,770,100,45),()=>Adjust(1));
            Button(font,"Armor erase",Text("reinforce.erase"),new Rect(142,770,100,45),()=>Adjust(-1));
            budget=Label(font,"Armor budget","",new Rect(30,695,680,65),19);
            feedback=Label(font,"Armor feedback","",new Rect(265,770,445,55),17);
            Button(font,"Armor uniform",Text("reinforce.uniform"),new Rect(30,835,210,45),()=>Commit(ArmorDistribution.Uniform(Current?.FacingQuarterTurns??0)));
            Button(font,"Armor front",Text("reinforce.front"),new Rect(255,835,210,45),()=>Commit(new ArmorDistribution(new[]{12,0,0,0},Current?.FacingQuarterTurns??0)));
            Button(font,"Armor undo",Text("imprint.undo"),new Rect(480,835,230,45),Undo);
            Button(font,"Armor surround",Text("reinforce.surround"),new Rect(810,695,330,45),()=>Record(-1));
            Button(font,"Armor legacy",Text("reinforce.legacy_button"),new Rect(1155,695,335,45),()=>Commit(null));
            result=Label(font,"Armor result","",new Rect(810,758,680,110),19);
            Label(font,"Armor save",Text("reinforce.save"),new Rect(30,885,1130,50),18);
            Button(font,"Close reinforcement",Text("imprint.done"),new Rect(1220,880,270,48),Close);
            sheet.SetActive(false);
        }
        public void Open(TowerBlueprint value)
        {
            if(value==null||!value.HasArmor)throw new ArgumentException(UserText.Message("reinforce.need_armor"));
            Close();blueprint=value;baseline=value.Reinforcement;selected=0;worldSide=-1;history.Clear();sheet.SetActive(true);sheet.transform.SetAsLastSibling();Show();Record(-1);
            if(EventSystem.current!=null&&sheet.activeInHierarchy)EventSystem.current.SetSelectedGameObject(selectors[0].gameObject);
        }
        public void Close()
        {
            Canvas.CancelGesture();var focus=EventSystem.current?.currentSelectedGameObject;
            if(focus!=null&&focus.transform.IsChildOf(sheet.transform))EventSystem.current.SetSelectedGameObject(null);
            sheet.SetActive(false);Clear();
        }
        public void Select(int index)
        {if(!IsOpen||index<0||index>3)return;Canvas.CancelGesture();selected=index;Show();}
        public void Adjust(int delta)
        {
            if(!IsOpen)return;Canvas.CancelGesture();var current=Current??new ArmorDistribution(new[]{0,0,0,0});
            if(delta!=1&&delta!=-1)return;
            if(delta>0?current.Remaining==0:current.Points[selected]==0)
            {feedback.text=Text(delta>0?"reinforce.no_stock":"reinforce.empty");return;}
            Commit(current.WithPoint(selected,delta));
        }
        public void Rotate(){if(!IsOpen)return;Commit((Current??ArmorDistribution.Uniform()).Rotate());}
        public void Undo()
        {
            if(!IsOpen)return;if(Canvas.HasGesture){Canvas.CancelGesture();return;}
            if(history.Count==0)return;var value=history[history.Count-1];history.RemoveAt(history.Count-1);Commit(value,false);
        }
        void Commit(ArmorDistribution value,bool remember=true)
        {
            if(!IsOpen)return;Canvas.CancelGesture();
            if(remember){if(history.Count==32)history.RemoveAt(0);history.Add(Current);}
            blueprint=blueprint.WithReinforcement(value);change(blueprint);Show();Record(worldSide);
        }
        void Show()
        {
            Canvas.Show(Current,selected);trial.Show(Current??ArmorDistribution.Uniform(),-1,Recording);
            authored.SetBlueprint(blueprint);trialBody.SetBlueprint(blueprint);
            conditions.text=Text("reinforce.conditions",ArmorDistribution.Budget,Side(Current?.FacingQuarterTurns??0),maximumDefense*100);
            feedback.text=Text(Current==null?"reinforce.legacy":"reinforce.paint");Readout(Current);ConfineNavigation();
        }
        void Readout(ArmorDistribution value)
        {
            float normal=defense(blueprint);
            changes.text=Resolve(DesignChangeFeedback.Armor(baseline,value,normal,maximumDefense,Canvas.HasGesture));
            for(int i=0;i<4;i++)
            {
                int points=value?.Points[i]??3;float effective=DamageRules.EffectiveDefense(normal*(value==null?1:points/3f),maximumDefense);
                selectors[i].GetComponentInChildren<Text>().text=Text("reinforce.sector",Text("reinforce.local"+i),points,effective*100);
                selectors[i].GetComponent<Image>().color=i==selected?new Color(.83f,.90f,.94f):Color.white;
            }
            budget.text=Text(value==null?"reinforce.budget_legacy":"reinforce.budget",value?.Remaining??12,normal*100);
        }
        void Clear(){Recording=null;trial.Show(blueprint?.Reinforcement);result.text=Text("reinforce.no_trial");}
        public void Record(int side=-1)
        {
            if(!IsOpen||Canvas.HasGesture)return;worldSide=side;
            try
            {
                Recording=probe(blueprint,side);trial.Show(Current??ArmorDistribution.Uniform(),-1,Recording);
                var rows=new System.Text.StringBuilder();float loss=0;
                foreach(var hit in Recording.Hits){if(rows.Length>0)rows.Append(" · ");rows.Append(Text("reinforce.hit",Side(hit.WorldSide),hit.Damage));loss+=hit.Damage;}
                result.text=Text("reinforce.result",rows.ToString(),ArmorProbe.RawDamage,Recording.InitialHealth-loss,Recording.InitialHealth,Recording.Cost);
            }
            catch(ArgumentException error){Clear();result.text=Resolve(error.Message);}
        }
        string Side(int side)=>Text("reinforce.side"+side);
        void ConfineNavigation()
        {
            var buttons=sheet.GetComponentsInChildren<Button>(true);
            for(int i=0;i<buttons.Length;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=buttons[(i+1)%buttons.Length],selectOnRight=buttons[(i+1)%buttons.Length],selectOnUp=buttons[(i+buttons.Length-1)%buttons.Length],selectOnLeft=buttons[(i+buttons.Length-1)%buttons.Length]};
        }
        static void Place(RectTransform rect,Rect box)=>MortarFootprintPanel.Place(rect,box);
        ArmorReinforcementGraphic Graphic(string name,Rect box,bool interactive)
        {var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(ArmorReinforcementGraphic));go.transform.SetParent(sheet.transform,false);Place(go.GetComponent<RectTransform>(),box);var graphic=go.GetComponent<ArmorReinforcementGraphic>();graphic.Interactive=interactive;graphic.raycastTarget=interactive;return graphic;}
        BlueprintSketch Body(Transform parent,Font font)
        {var go=new GameObject("Body silhouette",typeof(RectTransform),typeof(CanvasRenderer),typeof(BlueprintSketch));go.transform.SetParent(parent,false);Place(go.GetComponent<RectTransform>(),new Rect(270,((RectTransform)parent).rect.height/2-70,140,140));var sketch=go.GetComponent<BlueprintSketch>();sketch.raycastTarget=false;sketch.ShowReinforcement=false;return sketch;}
        Text Label(Font font,string name,string text,Rect box,int size)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(sheet.transform,false);Place(go.GetComponent<RectTransform>(),box);var label=go.GetComponent<Text>();label.font=font;label.fontSize=size;label.text=text;label.color=NotebookPalette.Navy;label.raycastTarget=false;return label;}
        Button Button(Font font,string name,string text,Rect box,Action action)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(sheet.transform,false);Place(go.GetComponent<RectTransform>(),box);go.GetComponent<Image>().color=Color.white;var button=go.GetComponent<Button>();button.onClick.AddListener(()=>action());var label=Label(font,name+" label",text,new Rect(5,3,box.width-10,box.height-6),17);label.transform.SetParent(go.transform,false);label.alignment=TextAnchor.MiddleCenter;return button;}
    }
}
