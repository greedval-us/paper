using System;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
namespace PaperDefense.Presentation
{
    internal sealed class WizardTrialPanel
    {
        readonly WorkshopUi ui;readonly RunController run;readonly Transform parent;
        GameObject root;TowerBlueprint source;TrialLaneSketch first,second;
        UnityEngine.UI.Text firstResult,secondResult,conditions,detailText;GameObject detailRoot;
        TrialScenario scenario;int budgetIndex;bool counterfire,details;
        static string T(string key,params object[] args)=>WorkshopWizard.Copy(key,args);
        public TrialComparison Recording {get;private set;}
        public WizardTrialPanel(Transform parent,WorkshopUi ui,RunController run)
        {
            this.parent=parent;this.ui=ui;this.run=run;
            var rules=run.Session.Balance.Trials;
            for(int i=0;i<rules.BudgetChoices.Count;i++)if(rules.BudgetChoices[i]==rules.DefaultBudget)budgetIndex=i;
        }
        public void Close(){if(root!=null){UnityEngine.Object.DestroyImmediate(root);root=null;}Recording=null;}
        public void Open(TowerBlueprint blueprint)
        {
            Close();source=blueprint;
            var shade=ui.Panel(parent,"Wizard trial overlay",new Rect(0,0,1600,1000),new Color(.1f,.2f,.34f,.3f),true);root=shade.gameObject;
            var sheet=ui.Panel(shade,"Wizard trial sheet",new Rect(120,70,1360,860),WorkshopUi.Paper,true);
            ui.Label(sheet,"Wizard trial heading",T("full_trial"),new Rect(30,20,1170,56),36,true);
            ui.IconButton(sheet,"Wizard close trial",new Rect(1268,24,62,48),WorkshopIcon.Close,T("close"),Close);
            ui.Label(sheet,"Wizard trial hint",T("trial_hint"),new Rect(30,88,1280,52),20,color:WorkshopUi.Muted);
            ui.Button(sheet,"Wizard trial scenario",new Rect(30,160,470,50),T("scenario"),CycleScenario,WorkshopIcon.Chevron);
            ui.Button(sheet,"Wizard trial budget",new Rect(520,160,410,50),T("budget"),()=>{budgetIndex=(budgetIndex+1)%run.Session.Balance.Trials.BudgetChoices.Count;Invalidate();},WorkshopIcon.Cost);
            var fire=ui.Button(sheet,"Wizard trial counterfire",new Rect(950,160,380,50),T("counterfire"),()=>{counterfire=!counterfire;Invalidate();},WorkshopIcon.Shield);
            fire.interactable=run.Session.Balance.Mechanics.RangedEnemies;
            conditions=ui.Label(sheet,"Wizard trial conditions","",new Rect(30,218,1300,34),19,color:WorkshopUi.Muted);
            ui.Label(sheet,"Trial first heading",T("your_tower"),new Rect(30,256,620,44),26,true);
            ui.Label(sheet,"Trial second heading",T("same_budget"),new Rect(710,256,620,44),26,true);
            first=Lane(sheet,"Wizard trial first",new Rect(30,306,620,290));second=Lane(sheet,"Wizard trial second",new Rect(710,306,620,290));
            firstResult=ui.Label(sheet,"Wizard trial first result","",new Rect(30,608,620,74),24);
            secondResult=ui.Label(sheet,"Wizard trial second result","",new Rect(710,608,620,74),24);
            ui.Button(sheet,"Wizard trial details",new Rect(30,704,360,44),T("details"),()=>{details=!details;detailRoot.SetActive(details);},WorkshopIcon.Chevron);
            var detail=ui.Panel(sheet,"Wizard trial expanded",new Rect(414,694,916,82),WorkshopUi.Surface);detailRoot=detail.gameObject;
            detailText=ui.Label(detail,"Wizard trial detailed result","",new Rect(16,6,882,70),17);detailRoot.SetActive(details);
            ui.Button(sheet,"Wizard trial run",new Rect(940,786,390,50),T("run_trial"),()=>Record(source),WorkshopIcon.Target,true);
            Invalidate();
            WorkshopModal.Open(root,Close);ui.HideTooltip();
        }
        TrialLaneSketch Lane(Transform parent,string name,Rect rect)
        {
            var view=ui.Panel(parent,name+" viewport",rect,Color.white,true);view.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            return WorkshopUi.Graphic<TrialLaneSketch>(view,name,new Rect(0,0,rect.width,rect.height));
        }
        void CycleScenario()
        {
            var available=run.Session.Balance.Trials.Scenarios.Where(s=>s.Scenario!=TrialScenario.Blots||run.Session.Balance.Mechanics.CorpseBlots).ToArray();
            int current=Array.FindIndex(available,s=>s.Scenario==scenario);scenario=available[(current+1)%available.Length].Scenario;Invalidate();
        }
        void Invalidate()
        {
            Recording=null;first.SetResult(null,0,1);second.SetResult(null,0,1);
            firstResult.text=secondResult.text=ui.Resolve(T("trial_empty"));detailText.text="";
            var rules=run.Session.Balance.Trials;budgetIndex=Mathf.Clamp(budgetIndex,0,rules.BudgetChoices.Count-1);
            conditions.text=ui.Resolve(T("trial_conditions",rules.For(scenario).Name,rules.BudgetChoices[budgetIndex],T(counterfire?"on":"off")));
        }
        public TrialComparison Record(TowerBlueprint blueprint)
        {
            if(root==null)Open(blueprint);
            var preview=run.PreviewBlueprint(blueprint);
            if(!preview.Valid){firstResult.text=ui.Resolve(preview.Reason);return null;}
            Recording=run.RunTrial(blueprint,scenario,counterfire,run.Session.Balance.Trials.BudgetChoices[budgetIndex]);
            if(Recording==null){firstResult.text=ui.Resolve(T("trial_unavailable"));return null;}
            var a=Recording.First;var b=Recording.Second;float started=Time.unscaledTime,duration=Mathf.Max(a.DurationSeconds,b.DurationSeconds);
            first.SetResult(a,started,duration);second.SetResult(b,started,duration);
            firstResult.text=ui.Resolve(T("trial_result",a.Damage,a.Leaks,a.SurvivingTowers,a.Copies));
            secondResult.text=ui.Resolve(T("trial_result",b.Damage,b.Leaks,b.SurvivingTowers,b.Copies));
            detailText.text=ui.Resolve(T("trial_details",a.Cost,b.Cost,a.DurationSeconds,b.DurationSeconds,a.UnresolvedEnemies,b.UnresolvedEnemies,a.BlotsCleared,b.BlotsCleared,a.IdleSeconds,b.IdleSeconds,a.UnresolvedBlots,b.UnresolvedBlots));
            return Recording;
        }
    }
}
