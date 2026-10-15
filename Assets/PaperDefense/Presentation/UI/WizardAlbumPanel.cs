using System;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
namespace PaperDefense.Presentation
{
    internal sealed class WizardAlbumPanel
    {
        readonly Transform parent;readonly WorkshopUi ui;readonly RunController run;readonly WorkshopWizard wizard;
        GameObject root;int page;string selected,message="";
        public bool IsOpen=>root!=null&&root.activeSelf;
        static string T(string key,params object[] args)=>WorkshopWizard.Copy(key,args);
        public WizardAlbumPanel(Transform parent,WorkshopUi ui,RunController run,WorkshopWizard wizard)
        {this.parent=parent;this.ui=ui;this.run=run;this.wizard=wizard;}
        public void Hide(){if(root!=null){UnityEngine.Object.DestroyImmediate(root);root=null;}}
        public void Show()
        {
            Hide();ui.HideTooltip();
            var shade=ui.Panel(parent,"Wizard album overlay",new Rect(0,0,1600,1000),new Color(.1f,.2f,.34f,.3f),true);root=shade.gameObject;
            var sheet=ui.Panel(shade,"Wizard album sheet",new Rect(140,55,1320,880),WorkshopUi.Paper,true);
            ui.Label(sheet,"Album title",T("album"),new Rect(32,20,680,56),36,true);
            ui.Button(sheet,"Wizard new blueprint",new Rect(770,25,430,48),T("new_tower"),wizard.NewDraft,WorkshopIcon.Plus,true);
            ui.IconButton(sheet,"Wizard close album",new Rect(1222,25,64,48),WorkshopIcon.Close,T("close"),()=>wizard.ShowAlbum(false));
            int count=run.Workshop.Album.Count;page=Mathf.Clamp(page,0,Math.Max(0,(count-1)/4));
            for(int i=0;i<4;i++)
            {
                int index=page*4+i;if(index>=count)break;var bp=run.Workshop.Album[index];
                var card=ui.Panel(sheet,"Wizard album card "+index,new Rect(32+i%2*637,100+i/2*214,615,195),selected==bp.Id?WorkshopUi.Selected:Color.white,true);
                var sketch=WorkshopUi.Graphic<BlueprintSketch>(card,"Album thumbnail",new Rect(16,12,150,160));sketch.SetBlueprint(bp);
                ui.Label(card,"Album page name",bp.Name,new Rect(186,18,410,40),23);
                var stats=run.PreviewBlueprint(bp);
                ui.Label(card,"Album page price",T("album_price",stats.Cost),new Rect(186,65,404,36),18,color:WorkshopUi.Muted);
                ui.Button(card,"Wizard album edit "+index,new Rect(186,118,170,48),T("edit"),()=>wizard.Load(bp),WorkshopIcon.Pencil);
                ui.Button(card,"Wizard album select "+index,new Rect(368,118,160,48),T("loadout"),()=>{selected=bp.Id;message=T("choose_slot");Show();},WorkshopIcon.Book);
                var delete=ui.IconButton(card,"Wizard album delete "+index,new Rect(540,118,58,48),WorkshopIcon.Trash,T("delete"),()=>
                {bool deleted=run.DeleteBlueprint(bp.Id,out var reason);message=deleted?T("deleted"):reason;if(deleted&&selected==bp.Id)selected=null;Show();});
                delete.interactable=!run.Workshop.Loadout.Any(p=>p?.Id==bp.Id);
            }
            if(count==0)ui.Label(sheet,"Album empty",T("album_empty"),new Rect(50,140,1220,200),26,true);
            ui.Button(sheet,"Wizard album previous",new Rect(32,536,190,44),T("previous"),()=>{page--;Show();},WorkshopIcon.Back).interactable=page>0;
            ui.Label(sheet,"Wizard album page",T("page",page+1,Math.Max(1,(count+3)/4)),new Rect(536,536,250,44),20);
            ui.Button(sheet,"Wizard album next",new Rect(1076,536,210,44),T("next_page"),()=>{page++;Show();},WorkshopIcon.Next).interactable=(page+1)*4<count;
            ui.Label(sheet,"Album slots heading",T("battle_set"),new Rect(32,602,1238,36),25,true);
            for(int i=0;i<ProfileLimits.LoadoutSlotCount;i++)
            {
                int slot=i;var value=run.Workshop.Loadout[i];
                var button=ui.Button(sheet,"Wizard assign "+i,new Rect(32+i*318,651,304,66),T("slot_value",i+1,value?.Name??ui.Resolve(T("empty"))),()=>
                {
                    if(selected==null)return;
                    message=run.AssignBlueprint(slot,selected,out string reason)?T("assigned",slot+1):reason;Show();
                },WorkshopIcon.Book);
                button.interactable=selected!=null;ui.Tip(button.gameObject,T("slot_hint"));
            }
            string profileIssue=run.Workshop.SaveState==ProfileSaveState.WriteFailed||run.Workshop.SaveState==ProfileSaveState.RecoveryRequired?run.Workshop.SaveMessage:"";
            ui.Label(sheet,"Wizard album status",string.IsNullOrEmpty(message)?profileIssue:message,new Rect(32,744,1238,78),18,color:WorkshopUi.Warning);
            if(run.Workshop.HasUnsavedChanges)ui.Button(sheet,"Wizard retry profile",new Rect(928,824,358,38),T("retry_save"),()=>{run.RetrySave(out var reason);message=reason;Show();},WorkshopIcon.Save);
            WorkshopModal.Open(root,()=>wizard.ShowAlbum(false));ui.HideTooltip();
        }
    }
}
