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
        GameObject root,extras;int mount;bool expanded;string purchaseError;
        static string T(string key,params object[] args)=>WorkshopWizard.Copy(key,args);
        public WizardAssemblyPanel(Transform parent,WorkshopUi ui,RunController run,WorkshopWizard wizard)
        {this.parent=parent;this.ui=ui;this.run=run;this.wizard=wizard;}
        public void Hide(){if(root!=null){UnityEngine.Object.DestroyImmediate(root);root=null;}CloseExtras();expanded=false;}
        public void Show(int step,TowerBlueprint draft)
        {
            Hide();var page=ui.Panel(parent,"Wizard assembly",new Rect(300,110,840,760),WorkshopUi.Surface);root=page.gameObject;
            bool foundation=step==0;
            ui.Label(page,"Wizard assembly heading",T(foundation?"choose_foundation":"choose_weapon"),new Rect(28,18,784,54),32,true);
            ui.Label(page,"Wizard assembly hint",T(foundation?"foundation_hint":"weapon_hint"),new Rect(28,79,784,52),18,color:WorkshopUi.Muted);
            var parts=run.Content.AllParts.Where(p=>p.Enabled&&p.Category==(foundation?PartCategory.Chassis:PartCategory.Weapon)
                &&(foundation||p.Id!=LegacyContentIds.Weapon(StructureKind.Wall)&&p.Id!=LegacyContentIds.Weapon(StructureKind.Spikes))).ToArray();
            float top=150;
            if(!foundation)
            {
                int count=run.Content.GetChassis(draft.ChassisId).MountCount;
                // A previous larger foundation may still have an extra mount: keep it selectable for removal.
                count=Math.Max(count,draft.WeaponMounts.Max(m=>m.SlotIndex)+1);mount=Math.Min(mount,count-1);
                for(int i=0;i<count;i++)
                {
                    int slot=i;var button=ui.Button(page,"Wizard mount "+i,new Rect(28+i*258,145,246,48),T("mount",i+1),()=>{mount=slot;Show(step,wizard.Draft);},WorkshopIcon.Weapon);
                    ui.SetSelected(button,i==mount);
                }
                top=213;
            }
            var list=new CatalogScrollList(page,"Wizard part catalog",new Rect(28,top,784,foundation?520:455),parts.Length+(!foundation&&mount>0?1:0),2,142,14);
            for(int i=0;i<parts.Length;i++)BuildCard(list.Content,new Rect(i%2*394,i/2*156,378,142),parts[i],draft,foundation);
            if(!foundation&&mount>0)
            {
                int i=parts.Length;var button=ui.Button(list.Content,"Wizard remove mount",new Rect(i%2*394,i/2*156,378,142),T("empty_mount"),()=>
                {
                    // Removing an excess mount must remain possible after switching to a smaller chassis.
                    wizard.SetDraft(wizard.Draft.WithWeaponMount(mount,null));
                },WorkshopIcon.Trash);ui.SetSelected(button,draft.GetWeaponId(mount)==null);
            }
            if(!foundation)ui.Button(page,"Wizard additions",new Rect(28,695,784,44),T("additions"),()=>{expanded=true;ShowExtras();},WorkshopIcon.Settings);
            else ui.Label(page,"Wizard foundation note",T("foundation_note"),new Rect(28,695,784,46),17,color:WorkshopUi.Muted);
            if(expanded&&!foundation)ShowExtras();
        }
        void BuildCard(Transform parent,Rect bounds,WorkshopPartDefinition part,TowerBlueprint draft,bool foundation)
        {
            bool owned=wizard.Owned(part),selected=foundation?draft.ChassisId==part.Id:draft.GetWeaponId(mount)==part.Id;
            var card=ui.Panel(parent,"Wizard part "+part.Id,bounds,selected?WorkshopUi.Selected:Color.white,true);
            var art=WorkshopUi.Graphic<NotebookArtGraphic>(card,"Part illustration",new Rect(14,16,64,64));art.Bind(part.Id);
            ui.Label(card,"Part name",part.ButtonLabel,new Rect(92,12,272,32),21);
            string values;
            if(foundation){var p=run.Content.GetChassis(part.Id);values=T("foundation_values",p.MountCount,p.Capacity);}
            else{var p=run.Content.GetWeapon(part.Id);values=T("weapon_values",p.Stats.Damage,p.Stats.Range);}
            ui.Label(card,"Part values",values,new Rect(92,50,270,40),16,color:WorkshopUi.Muted);ui.Tip(card.gameObject,part.Description);
            int price=Price(part.Id);
            var choose=ui.Button(card,(owned?"Wizard choose ":"Wizard unlock ")+part.Id,new Rect(14,98,350,34),owned?T(selected?"selected":"choose"):T("unlock",price),()=>
            {
                ui.HideTooltip();
                if(!owned){if(!run.UnlockPart(part.Id,out string reason)){wizard.Feedback(reason);return;}Show(wizard.Step,wizard.Draft);return;}
                if(foundation)wizard.SelectChassis(part.Id);else wizard.SelectWeapon(mount,part.Id);
            },owned?WorkshopIcon.Check:WorkshopIcon.Cost);
            choose.interactable=(foundation||mount<run.Content.GetChassis(draft.ChassisId).MountCount)&&(owned||run.Workshop.Gold>=price);
        }
        int Price(string id)=>run.Workshop.Parts.FirstOrDefault(p=>p.Id==id)?.GoldCost??run.Content.AllParts.First(p=>p.Id==id).UnlockGold;
        void CloseExtras(){if(extras!=null){UnityEngine.Object.DestroyImmediate(extras);extras=null;}}
        void ShowExtras()
        {
            CloseExtras();ui.HideTooltip();
            var shade=ui.Panel(parent,"Wizard additions overlay",new Rect(0,0,1600,1000),new Color(.1f,.2f,.34f,.3f),true);extras=shade.gameObject;
            var panel=ui.Panel(shade,"Wizard additions sheet",new Rect(325,110,950,780),WorkshopUi.Paper,true);
            ui.Label(panel,"Additions heading",T("additions"),new Rect(28,20,805,52),32,true);
            ui.IconButton(panel,"Close additions",new Rect(866,22,56,48),WorkshopIcon.Close,T("close"),()=>{expanded=false;CloseExtras();});
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
                    if(!owned){purchaseError=run.UnlockPart(id,out var reason)?null:reason;ShowExtras();return;}
                    wizard.ChangeModule(id);ShowExtras();
                },owned?WorkshopIcon.Plus:WorkshopIcon.Cost);add.interactable=owned||run.Workshop.Gold>=price;
                if(count>0)ui.IconButton(card,"Wizard module remove "+id,new Rect(338,100,80,36),WorkshopIcon.Trash,T("remove"),()=>{wizard.ChangeModule(id,true);ShowExtras();});
            }
            ui.Label(panel,"Additions shape heading",T("adjustments"),new Rect(28,538,894,36),24,true);
            wizard.BuildSpecialButtons(panel,draft,new Rect(28,586,894,126),()=>{expanded=false;CloseExtras();});
            if(!string.IsNullOrEmpty(purchaseError)||!wizard.AssemblyValid(out _))
            {wizard.AssemblyValid(out var error);ui.Label(panel,"Additions error",purchaseError??error,new Rect(28,724,894,44),17,color:WorkshopUi.Warning);}
            WorkshopModal.Open(extras,()=>{expanded=false;CloseExtras();});ui.HideTooltip();
        }
    }
}
