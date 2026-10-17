using System;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
namespace PaperDefense.Presentation
{
    internal sealed class WizardAssemblyPanel
    {
        readonly Transform parent;readonly WorkshopUi ui;readonly RunController run;readonly WorkshopWizard wizard;
        readonly WizardFoundationPanel foundation;readonly WizardWeaponPanel weapons;
        GameObject extras;string purchaseError,additionsFocus="Wizard additions";
        static string T(string key,params object[] args)=>WorkshopWizard.Copy(key,args);
        public WizardAssemblyPanel(Transform parent,WorkshopUi ui,RunController run,WorkshopWizard wizard)
        {this.parent=parent;this.ui=ui;this.run=run;this.wizard=wizard;foundation=new WizardFoundationPanel(parent,ui,run,wizard,ConfirmPurchase);weapons=new WizardWeaponPanel(parent,ui,run,wizard,ConfirmPurchase,OpenExtras);}
        public void Hide(){foundation.Hide();weapons.Hide();CloseExtras();}
        internal void ReflowFoundation(float width,float height)=>foundation.Reflow(width,height);
        internal void ReflowWeapons(float width,float height)=>weapons.Reflow(width,height);
        internal void RefreshWeapons(TowerBlueprint draft)=>weapons.Refresh(draft);
        public void Show(int step,TowerBlueprint draft)
        {
            Hide();
            if(step==0)foundation.Show(draft);
            else if(step==1)weapons.Show(draft);
        }
        int Price(string id)=>run.Workshop.Parts.FirstOrDefault(p=>p.Id==id)?.GoldCost??run.Content.AllParts.First(p=>p.Id==id).UnlockGold;
        void ConfirmPurchase(string id,Action refresh)
        {
            purchaseError=null;int price=Price(id),gold=run.Workshop.Gold;
            var definition=run.Content.AllParts.First(p=>p.Id==id);
            WorkshopConfirm.Show(parent,ui,"Открыть деталь?",ui.Resolve(definition.ButtonLabel)+"\n\nЦена: "+price+" золота\nНа счету: "+gold+"\n"+
                (gold>=price?"После открытия деталь останется доступной в мастерской.":"Не хватает: "+(price-gold)+" золота"),"Открыть за "+price,()=>
                { if(!run.UnlockPart(id,out string reason)){purchaseError=reason;wizard.Feedback(reason);}else wizard.Feedback("");refresh();wizard.Refresh(); },gold>=price);
        }
        void CloseExtras(){if(extras!=null){UnityEngine.Object.DestroyImmediate(extras);extras=null;}}
        void OpenExtras()
        {
            string focused=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name;
            additionsFocus=focused=="Wizard weapon armor"?focused:"Wizard additions";
            ShowExtras();
        }
        void FinishExtras()
        {
            CloseExtras();
            var focus=parent.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(button=>button.name==additionsFocus&&button.IsInteractable());
            if(focus!=null)UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(focus.gameObject);
        }
        void ShowExtras()
        {
            CloseExtras();ui.HideTooltip();
            var shade=ui.Panel(parent,"Wizard additions overlay",new Rect(0,0,1600,1000),new Color(.1f,.2f,.34f,.3f),true);extras=shade.gameObject;
            var panel=ui.Panel(shade,"Wizard additions sheet",new Rect(325,110,950,780),WorkshopUi.Paper,true);
            ui.Label(panel,"Additions heading",T("additions"),new Rect(28,20,805,52),32,true);
            ui.IconButton(panel,"Close additions",new Rect(866,22,56,48),WorkshopIcon.Close,T("close"),FinishExtras);
            var draft=wizard.Draft;var chassis=run.Content.GetChassis(draft.ChassisId);
            int armor=draft.ModuleSelections.Count(m=>run.Content.GetModule(m.DefinitionId).SlotKind==ModuleSlotKind.Armor);
            ui.Label(panel,"Additions capacity",T("module_slots",armor,chassis.ArmorSlotCount,draft.ModuleSelections.Count-armor,chassis.UpgradeSlotCount),new Rect(28,80,894,46),18,color:WorkshopUi.Muted);
            var modules=run.Content.AllParts.Where(p=>p.Enabled&&p.Category==PartCategory.Module).ToArray();
            var list=new CatalogScrollList(panel,"Wizard module catalog",new Rect(28,142,894,370),modules.Length,2,150,12);
            for(int i=0;i<modules.Length;i++)
            {
                var definition=run.Content.GetModule(modules[i].Id);string id=definition.Id;int count=draft.ModuleSelections.Count(m=>m.DefinitionId==id);bool owned=wizard.Owned(definition);
                var card=ui.Panel(list.Content,"Wizard module "+id,new Rect(i%2*450,i/2*162,432,150),count>0?WorkshopUi.Selected:Color.white,true);
                var art=WorkshopUi.Graphic<NotebookArtGraphic>(card,"Module illustration",new Rect(12,16,55,55));art.Bind(id);
                ui.Label(card,"Module name",definition.ButtonLabel,new Rect(82,12,330,32),21);
                ui.Label(card,"Module info",T("module_value",definition.Load,count),new Rect(82,52,330,32),17,color:WorkshopUi.Muted);ui.Tip(card.gameObject,definition.Description);
                int price=Price(id);
                var add=ui.Button(card,"Wizard module add "+id,new Rect(14,100,count>0?310:404,36),owned?T(count>0&&!definition.Repeatable?"remove":"add"):T("unlock",price),()=>
                {
                    if(!owned){ConfirmPurchase(id,ShowExtras);return;}
                    wizard.ChangeModule(id);ShowExtras();
                },owned?WorkshopIcon.Plus:WorkshopIcon.Cost);add.interactable=owned||run.Workshop.Gold>=price;
                if(count>0)ui.IconButton(card,"Wizard module remove "+id,new Rect(338,100,80,36),WorkshopIcon.Trash,T("remove"),()=>{wizard.ChangeModule(id,true);ShowExtras();});
            }
            ui.Label(panel,"Additions shape heading",T("adjustments"),new Rect(28,538,894,36),24,true);
            wizard.BuildSpecialButtons(panel,draft,new Rect(28,586,894,126),FinishExtras);
            if(!string.IsNullOrEmpty(purchaseError)||!wizard.AssemblyValid(out _))
            {wizard.AssemblyValid(out var error);ui.Label(panel,"Additions error",purchaseError??error,new Rect(28,724,894,44),17,color:WorkshopUi.Warning);}
            WorkshopModal.Open(extras,FinishExtras);ui.HideTooltip();
        }
    }
}
