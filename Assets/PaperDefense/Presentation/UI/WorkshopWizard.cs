using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;
namespace PaperDefense.Presentation
{
    /// <summary>Player-facing sequence. Draft changes stay local until an explicit successful save.</summary>
    public sealed class WorkshopWizard
    {
        readonly RunController run;
        readonly WorkshopUi ui;
        readonly Transform root;
        readonly Action<TowerBlueprint> changed;
        readonly Action prepare;
        readonly UnityEngine.UI.Button[] steps=new UnityEngine.UI.Button[4];
        readonly UnityEngine.UI.Text[] stepNumbers=new UnityEngine.UI.Text[4];
        readonly UnityEngine.UI.Button next,back;
        readonly UnityEngine.UI.Text notice,gold,summaryTitle,details;
        readonly UnityEngine.UI.Text[] summaryValues=new UnityEngine.UI.Text[3];
        readonly WorkshopIconGraphic[] stepChecks=new WorkshopIconGraphic[4];
        readonly UnityEngine.UI.ScrollRect detailScroll;
        readonly BlueprintSketch preview,smallPreview;
        readonly GameObject summaryRoot;
        readonly WizardAssemblyPanel assembly;
        readonly WizardAlbumPanel album;
        readonly WizardTrialPanel trial;
        GameObject reviewRoot;UnityEngine.UI.InputField name;BlueprintSketch reviewDrawing;
        int reached;bool detailsOpen,saved,dirty;
        public int Step { get; private set; }
        public TowerBlueprint Draft { get; private set; }
        public GuidedDrawingPanel Drawing { get; }
        public MortarFootprintPanel Mortar { get; }
        public FanVolleyPanel Volley { get; }
        public ArmorReinforcementPanel Armor { get; }
        public bool AlbumOpen=>album.IsOpen;
        public string Status=>notice.text;
        internal static string Copy(string key,params object[] values)=>UserText.Message("wizard."+key,values);
        public WorkshopWizard(Transform parent,RunController run,UserTextResolver resolver,Action<TowerBlueprint> changed,Action prepare)
        {
            this.run=run;root=parent;this.changed=changed;this.prepare=prepare;ui=new WorkshopUi(resolver);
            ui.Panel(parent,"Guided workshop",new Rect(0,0,1600,1000),WorkshopUi.Paper,true);
            ui.Label(parent,"Wizard title",Copy("title"),new Rect(35,18,800,62),40,true);
            gold=ui.Label(parent,"Wizard gold","",new Rect(1050,26,230,40),20,color:WorkshopUi.Muted);
            ui.Button(parent,"Wizard album",new Rect(1300,24,260,48),Copy("album"),()=>ShowAlbum(true),WorkshopIcon.Book);
            ui.Panel(parent,"Wizard navigation",new Rect(24,110,250,782),WorkshopUi.Surface);
            string[] labels={"foundation","weapon","drawing","review"};
            WorkshopIcon[] icons={WorkshopIcon.Tower,WorkshopIcon.Weapon,WorkshopIcon.Pencil,WorkshopIcon.Target};
            for(int i=0;i<4;i++)
            {
                int slot=i;
                stepNumbers[i]=ui.Label(parent,"Wizard progress "+i,(i+1).ToString(),new Rect(43,140+i*126,36,36),22,color:WorkshopUi.Muted);
                stepChecks[i]=WorkshopUi.Graphic<WorkshopIconGraphic>(parent,"Wizard completed "+i,new Rect(43,145+i*126,24,24));stepChecks[i].Icon=WorkshopIcon.Check;stepChecks[i].color=WorkshopUi.Success;
                steps[i]=ui.Button(parent,"Wizard step "+i,new Rect(38,181+i*126,222,62),Copy(labels[i]),()=>GoToStep(slot),icons[i]);
                if(i<3)ui.Panel(parent,"Wizard connector "+i,new Rect(60,252+i*126,2,17),WorkshopUi.Line);
            }
            smallPreview=WorkshopUi.Graphic<BlueprintSketch>(parent,"Wizard assembled thumbnail",new Rect(70,680,155,165));smallPreview.ShowReference=true;
            ui.Label(parent,"Wizard thumbnail caption",Copy("your_tower"),new Rect(46,845,206,32),17,color:WorkshopUi.Muted);
            notice=ui.Label(parent,"Wizard notice","",new Rect(300,882,840,38),17,color:WorkshopUi.Warning);
            back=ui.Button(parent,"Wizard back",new Rect(24,930,210,48),Copy("back"),Back,WorkshopIcon.Back);
            next=ui.Button(parent,"Wizard next",new Rect(1230,930,340,48),Copy("to_weapon"),NextFromUi,WorkshopIcon.Next,true);
            ui.Button(parent,"Start run",new Rect(264,930,270,48),Copy("preparation"),()=>{Suspend();prepare?.Invoke();},WorkshopIcon.Book);
            var side=ui.Panel(parent,"Wizard summary",new Rect(1170,110,400,782),WorkshopUi.Surface);summaryRoot=side.gameObject;
            summaryTitle=ui.Label(side,"Wizard summary title",Copy("your_tower"),new Rect(24,18,352,50),30,true);
            preview=WorkshopUi.Graphic<BlueprintSketch>(side,"Wizard tower preview",new Rect(64,88,270,275));preview.ShowReference=true;
            string[] metrics={"damage","range","cost"};WorkshopIcon[] symbols={WorkshopIcon.Damage,WorkshopIcon.Range,WorkshopIcon.Cost};
            for(int i=0;i<3;i++)
            {
                var symbol=WorkshopUi.Graphic<WorkshopIconGraphic>(side,"Summary icon "+i,new Rect(26,392+i*42,24,24));symbol.Icon=symbols[i];symbol.color=WorkshopUi.Blue;
                ui.Label(side,"Summary metric "+i,Copy(metrics[i]),new Rect(62,387+i*42,230,34),20);
                summaryValues[i]=ui.Label(side,"Summary value "+i,"",new Rect(285,387+i*42,86,34),23);summaryValues[i].alignment=TextAnchor.MiddleRight;
            }
            ui.Button(side,"Wizard details",new Rect(24,526,352,46),Copy("details"),()=>{detailsOpen=!detailsOpen;Refresh();},WorkshopIcon.Chevron);
            var detailList=new CatalogScrollList(side,"Wizard detail scroll",new Rect(26,589,346,167),1,1,167,0);detailScroll=detailList.Scroll;
            details=ui.Label(detailList.Content,"Wizard detailed values","",new Rect(0,0,328,167),18,color:WorkshopUi.Muted);
            assembly=new WizardAssemblyPanel(parent,ui,run,this);
            Drawing=new GuidedDrawingPanel(parent,ui,new DrawingEditorContext{Preview=p=>run.PreviewBlueprint(p),Workshop=()=>run.Workshop,Purchase=run.UnlockDrawingAid,Probe=run.ProbeDrawing},p=>SetDraft(p,false));
            Drawing.BackRequested+=Back;
            album=new WizardAlbumPanel(parent,ui,run,this);
            trial=new WizardTrialPanel(parent,ui,run);
            Mortar=new MortarFootprintPanel(parent,ui.BodyFont,p=>SetDraft(p,false),run.CompareMortarFootprints,p=>run.PreviewBlueprint(p).Reason,resolver,run.CompareMortarForms);
            Volley=new FanVolleyPanel(parent,ui.BodyFont,p=>SetDraft(p,false),run.ProbeFanVolley,resolver);
            Armor=new ArmorReinforcementPanel(parent,ui.BodyFont,p=>SetDraft(p,false),run.ProbeArmor,run.ArmorDefense,run.MaximumStructureDefense,resolver);
            foreach(string sheet in new[]{"Mortar footprint sheet","Footprint study sheet","Fan volley sheet","Reinforcement sheet"})WorkshopSpecialistStyle.Apply(parent.Find(sheet),ui);
            ui.EnsureTooltip(parent);
            NewDraft();
        }
        public void NewDraft()
        {
            CloseOverlays();
            string chassis=run.Content.AllParts.First(p=>p.Category==PartCategory.Chassis&&p.Enabled&&Owned(p)).Id;
            string weapon=run.Content.AllParts.First(p=>p.Category==PartCategory.Weapon&&p.Enabled&&p.IsCombatMount&&Owned(p)).Id;
            Draft=new TowerBlueprint("drawing-"+Guid.NewGuid().ToString("N"),ui.Resolve(Copy("new_name")),chassis,weapon,origin:DrawingOrigin.Freeform,content:run.Content);
            Step=reached=0;saved=false;dirty=false;notice.text="";changed?.Invoke(Draft);BuildStep();
        }
        internal bool Owned(WorkshopPartDefinition p)=>p.InitiallyUnlocked||run.Workshop.IsUnlocked(p.Id);
        internal bool Rights(out string reason)
        {
            foreach(string id in new[]{Draft.ChassisId}.Concat(Draft.WeaponMounts.Select(m=>m.WeaponId)).Concat(Draft.ModuleSelections.Select(m=>m.DefinitionId)))
                if(!run.Content.TryGet(id,out var p)||!p.Enabled||!Owned(p)){reason=Copy("locked_selected");return false;}
            reason="";return true;
        }
        internal bool AssemblyValid(out string reason)=>Rights(out reason)&&run.ValidateAssembly(Draft,out reason);
        internal void Feedback(string message){notice.text=ui.Resolve(message);}
        internal void SetDraft(TowerBlueprint value,bool rebuild=true)
        {
            Draft=value.WithContent(run.Content);saved=false;dirty=true;changed?.Invoke(Draft);notice.text="";
            if(rebuild)BuildStep();else Refresh();
        }
        public bool SelectChassis(string id)
        {
            if(!run.Content.TryGetChassis(id,out var p)||!p.Enabled||!Owned(p))return false;
            reached=Math.Min(reached,1);SetDraft(Draft.WithAssembly(id,Draft.WeaponMounts,Draft.ModuleSelections));return true;
        }
        public bool SelectWeapon(int mount,string id)
        {
            if(mount<0||mount>=run.Content.GetChassis(Draft.ChassisId).MountCount)return false;
            if(id!=null&&(!run.Content.TryGetWeapon(id,out var p)||!p.Enabled||!Owned(p)))return false;
            if(mount==0&&id==null)return false;
            reached=Math.Min(reached,2);SetDraft(Draft.WithWeaponMount(mount,id));return true;
        }
        public bool ChangeModule(string id,bool remove=false)
        {
            if(!run.Content.TryGetModule(id,out var definition)||!definition.Enabled||!Owned(definition))return false;
            var modules=Draft.ModuleSelections.ToList();int index=modules.FindLastIndex(m=>m.DefinitionId==id);
            if(remove||index>=0&&!definition.Repeatable){if(index>=0)modules.RemoveAt(index);else return false;}
            else modules.Add(new ModuleSelection("module-"+Guid.NewGuid().ToString("N"),id));
            reached=Math.Min(reached,2);SetDraft(Draft.WithAssembly(Draft.ChassisId,Draft.WeaponMounts,modules));return true;
        }
        public bool Next()
        {
            if(Step==3)return Save();
            if(Step==0)
            {if(!run.Content.TryGetChassis(Draft.ChassisId,out var part)||!Owned(part)){Feedback(Copy("locked_selected"));return false;}}
            if(Step==1&&!AssemblyValid(out string reason)){Feedback(reason);return false;}
            if(Step==2)
            {
                if(!Drawing.TryFinish(out var candidate,out string error)){Feedback(error);return false;}
                Draft=candidate;changed?.Invoke(Draft);
                if(!Rights(out error)){Feedback(error);return false;}
            }
            Step++;reached=Math.Max(reached,Step);notice.text="";BuildStep();return true;
        }
        void NextFromUi()
        {
            if(Next()||Step!=3)return;
            if(run.Workshop.SaveState==ProfileSaveState.WriteFailed||run.Workshop.SaveState==ProfileSaveState.RecoveryRequired)
                WorkshopConfirm.Show(root,ui,"Не удалось сохранить",notice.text+"\n\nЧерновик остаётся в мастерской. Повтори запись или вернись к работе.","Повторить",NextFromUi);
        }
        public bool GoToStep(int step)
        {
            if(step<0||step>3||step>reached)return false;
            if(step>Step){while(Step<step)if(!Next())return false;return true;}
            if(Step==2){Drawing.Hide();Draft=Drawing.Candidate;changed?.Invoke(Draft);}
            Step=step;notice.text="";BuildStep();return true;
        }
        public void Back(){if(Step>0)GoToStep(Step-1);else ShowAlbum(true);}
        public bool Save()
        {
            if(Step!=3)return false;
            if(name!=null)Rename(name.text);
            if(!run.SaveBlueprint(Draft,out string reason)){Feedback(reason);return false;}
            saved=true;changed?.Invoke(Draft);Feedback(Copy("saved"));Refresh();return true;
        }
        public bool SaveVariant()
        {
            var before=Draft;Draft=Draft.AsVariant("drawing-"+Guid.NewGuid().ToString("N"),ValidName(name!=null?name.text:Draft.Name));
            if(Save())return true;Draft=before;return false;
        }
        public void Load(TowerBlueprint value)
        {
            CloseOverlays();Draft=value.WithContent(run.Content);Step=reached=3;saved=true;changed?.Invoke(Draft);notice.text="";BuildStep();
        }
        internal void RequestNewDraft()=>ReplaceDraft(NewDraft);
        internal void RequestLoad(TowerBlueprint value)=>ReplaceDraft(()=>Load(value));
        void ReplaceDraft(Action action)
        {
            if(!saved&&(dirty||Step>0||!Draft.Drawing.IsEmpty))
                WorkshopConfirm.Show(root,ui,"Заменить текущий черновик?","Несохранённые изменения этого чертежа будут потеряны. Можно вернуться и сохранить их на шаге «Проверка».","Заменить",action);
            else action();
        }
        public void ShowAlbum(bool visible)
        {
            ui.HideTooltip();if(visible&&Step==2){Drawing.Hide();Draft=Drawing.Candidate;changed?.Invoke(Draft);}
            if(visible)album.Show();else{album.Hide();BuildStep();}
        }
        internal void OpenGeometry(int mount)
        {
            CloseSpecialEditors();ui.HideTooltip();
            var focus=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            var weapon=run.Content.GetWeapon(Draft.GetWeaponId(mount));
            if(weapon.SupportsFootprint){Mortar.Open(Draft,mount);WorkshopModal.Open(root.Find("Mortar footprint sheet").gameObject,Mortar.Close,focus);}
            else if(weapon.SupportsVolley){Volley.Open(Draft,mount);WorkshopModal.Open(root.Find("Fan volley sheet").gameObject,Volley.Close,focus);}
        }
        internal void OpenArmor()
        {CloseSpecialEditors();ui.HideTooltip();var focus=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;Armor.Open(Draft);WorkshopModal.Open(root.Find("Reinforcement sheet").gameObject,Armor.Close,focus);}
        internal void OpenTrial(){ui.HideTooltip();trial.Open(Draft);}
        public TrialComparison RunTrial()=>trial.Record(Draft);
        void CloseSpecialEditors(){Mortar?.Close();Volley?.Close();Armor?.Close();}
        void CloseOverlays(){CloseSpecialEditors();Drawing?.Hide();assembly?.Hide();album?.Hide();trial?.Close();ui.HideTooltip();}
        public void Suspend(){CloseOverlays();}
        public void Resume(){BuildStep();}
        internal void ReflowFoundation(float width,float height)=>assembly.ReflowFoundation(width,height);
        void BuildStep()
        {
            ui.HideTooltip();Drawing.Hide();assembly.Hide();summaryRoot.SetActive(Step!=0&&Step!=2);
            if(reviewRoot!=null){UnityEngine.Object.DestroyImmediate(reviewRoot);reviewRoot=null;name=null;reviewDrawing=null;}
            if(Step<2)assembly.Show(Step,Draft);
            else if(Step==2)Drawing.Open(Draft);
            else BuildReview();
            Refresh();
        }
        void BuildReview()
        {
            var page=ui.Panel(root,"Wizard review",new Rect(300,110,840,760),WorkshopUi.Surface);reviewRoot=page.gameObject;
            ui.Label(page,"Review heading",Copy("review_heading"),new Rect(28,18,784,50),32,true);
            ui.Label(page,"Review hint",Copy("review_hint"),new Rect(28,75,784,32),18,color:WorkshopUi.Muted);
            reviewDrawing=WorkshopUi.Graphic<BlueprintSketch>(page,"Review drawing",new Rect(38,125,370,350));reviewDrawing.SetBlueprint(Draft);
            ui.Label(page,"Review name heading",Copy("name"),new Rect(442,128,365,30),18);
            var field=ui.Panel(page,"Wizard name",new Rect(440,166,370,52),Color.white,true);
            name=field.gameObject.AddComponent<UnityEngine.UI.InputField>();name.textComponent=ui.Label(field,"Wizard name value","",new Rect(14,4,342,44),22);
            name.characterLimit=ProfileLimits.MaximumBlueprintNameLength;name.SetTextWithoutNotify(ui.Resolve(Draft.Name));
            name.onEndEdit.AddListener(value=>{Rename(value);Refresh();});
            ui.Button(page,"Wizard full trial",new Rect(440,244,370,58),Copy("full_trial"),OpenTrial,WorkshopIcon.Target);
            ui.Label(page,"Review check",Copy("review_ready"),new Rect(440,320,368,150),20,color:WorkshopUi.Muted);
            ui.Label(page,"Review adjustments",Copy("adjustments"),new Rect(28,492,784,34),22);
            BuildSpecialButtons(page,Draft,new Rect(28,538,784,125));
            ui.Button(page,"Wizard save variant",new Rect(28,681,350,48),Copy("save_variant"),()=>SaveVariant(),WorkshopIcon.Copy);
            ui.Button(page,"Wizard open saved",new Rect(420,681,390,48),Copy("album"),()=>ShowAlbum(true),WorkshopIcon.Book);
        }
        string ValidName(string value)=>string.IsNullOrWhiteSpace(value)?ui.Resolve(Copy("new_name")):value.Trim();
        void Rename(string value)
        {
            string valid=ValidName(value);if(name!=null)name.SetTextWithoutNotify(valid);
            if(Draft.Name==valid)return;Draft=Draft.WithName(valid);saved=false;changed?.Invoke(Draft);
        }
        internal void BuildSpecialButtons(Transform parent,TowerBlueprint source,Rect bounds,Action beforeOpen=null)
        {
            var actions=new List<(string label,Action action,WorkshopIcon icon)>();
            foreach(var mount in source.WeaponMounts)
            {
                int slot=mount.SlotIndex;var weapon=run.Content.GetWeapon(mount.WeaponId);
                if(weapon.SupportsFootprint)actions.Add((Copy("imprint",slot+1),()=>OpenGeometry(slot),WorkshopIcon.Target));
                if(weapon.SupportsVolley)actions.Add((Copy("volley",slot+1),()=>OpenGeometry(slot),WorkshopIcon.Weapon));
            }
            if(source.HasArmor)actions.Add((Copy("armor"),OpenArmor,WorkshopIcon.Shield));
            if(actions.Count==0){ui.Label(parent,"Wizard no geometry options",Copy("no_special"),bounds,18,color:WorkshopUi.Muted);return;}
            var scroll=new CatalogScrollList(parent,"Wizard geometry options",bounds,actions.Count,2,52,8);
            for(int i=0;i<actions.Count;i++){var action=actions[i];ui.Button(scroll.Content,"Wizard geometry "+i,new Rect(i%2*(bounds.width/2),i/2*60,bounds.width/2-12,52),action.label,()=>{beforeOpen?.Invoke();action.action();},action.icon);}
        }
        public void Refresh()
        {
            if(Draft==null)return;
            gold.text=ui.Resolve(Copy("gold",run.Workshop.Gold));smallPreview.ShowReference=preview.ShowReference=Draft.Drawing.IsEmpty;smallPreview.SetBlueprint(Draft);preview.SetBlueprint(Draft);if(reviewDrawing!=null)reviewDrawing.SetBlueprint(Draft);
            summaryTitle.text=ui.Resolve(Copy(Draft.Drawing.IsEmpty?"assembly_preview":"your_tower"));
            for(int i=0;i<4;i++){ui.SetSelected(steps[i],i==Step);steps[i].interactable=i<=reached;stepNumbers[i].gameObject.SetActive(i>=Step);stepChecks[i].gameObject.SetActive(i<Step);}
            var nextLabel=next.GetComponentInChildren<UnityEngine.UI.Text>();nextLabel.text=ui.Resolve(Copy(Step==0?"to_weapon":Step==1?"to_drawing":Step==2?"to_review":"save"));
            var stats=run.PreviewBlueprint(Draft);
            var first=stats.Weapons!=null&&stats.Weapons.Count>0?stats.Weapons[0]:(WeaponPreview?)null;
            summaryValues[0].text=(first?.Damage??0).ToString("0.#");summaryValues[1].text=stats.Range.ToString("0.#");summaryValues[2].text=stats.Cost.ToString();
            detailScroll.gameObject.SetActive(detailsOpen);
            var text=new System.Text.StringBuilder(ui.Resolve(Copy("detail_values",stats.Health,stats.Load,stats.Capacity,stats.BuildSeconds,stats.FireRate,stats.Defense*100)));
            if(stats.Weapons!=null)foreach(var weapon in stats.Weapons)text.Append("\n\n").Append(ui.Resolve(Copy("mount_detail",weapon.SlotIndex+1,weapon.Damage,weapon.Range,weapon.Interval)));
            details.text=text.ToString();float height=Mathf.Max(167,details.preferredHeight+8);details.rectTransform.sizeDelta=new Vector2(328,height);detailScroll.content.sizeDelta=new Vector2(0,height);
            if(Step==3&&!stats.Valid)Feedback(stats.Reason);
            if(Step==3&&saved)nextLabel.text=ui.Resolve(Copy("saved_button"));
        }
    }
}
