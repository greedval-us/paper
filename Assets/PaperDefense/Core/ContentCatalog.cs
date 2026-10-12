using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    [Serializable] public sealed class ContentCatalogInput
    {
        public List<ChassisDefinitionInput> Chassis = new List<ChassisDefinitionInput>();
        public List<WeaponDefinitionInput> Weapons = new List<WeaponDefinitionInput>();
        public List<ModuleDefinitionInput> Modules = new List<ModuleDefinitionInput>();
        public List<EffectDefinitionInput> Effects = new List<EffectDefinitionInput>();
        public List<DrawingTemplateDefinitionInput> Templates = new List<DrawingTemplateDefinitionInput>();
        public List<StartingBlueprintDefinitionInput> StartingPages = new List<StartingBlueprintDefinitionInput>();
        public DrawingAssessmentSettingsInput Assessment = new DrawingAssessmentSettingsInput();
        public string RedEffectId, BlueEffectId, GreenEffectId;
        public ContentCatalogSnapshot CreateSnapshot() => new ContentCatalogSnapshot(this,false);
        public ContentCatalogInput CreateCopy() => ContentInputCopy.Copy(this);
    }
    public sealed class ContentCatalogSnapshot
    {
        readonly Dictionary<string,WorkshopPartDefinition> parts = new Dictionary<string,WorkshopPartDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string,ChassisDefinition> chassis = new Dictionary<string,ChassisDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string,WeaponDefinition> weapons = new Dictionary<string,WeaponDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string,ModuleDefinition> modules = new Dictionary<string,ModuleDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string,EffectDefinition> effects = new Dictionary<string,EffectDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string,DrawingTemplateDefinition> templates = new Dictionary<string,DrawingTemplateDefinition>(StringComparer.Ordinal);
        public IReadOnlyList<WorkshopPartDefinition> AllParts { get; }
        public IReadOnlyList<ChassisDefinition> Chassis { get; }
        public IReadOnlyList<WeaponDefinition> Weapons { get; }
        public IReadOnlyList<ModuleDefinition> Modules { get; }
        public IReadOnlyList<EffectDefinition> Effects { get; }
        public IReadOnlyList<DrawingTemplateDefinition> Templates { get; }
        public IReadOnlyList<TowerBlueprint> StartupBlueprints { get; }
        public IReadOnlyList<string> StartupSlotIds { get; }
        public DrawingAssessmentSettings Assessment { get; }
        public bool IsLegacyAdapter { get; }
        public string RedEffectId { get; } public string BlueEffectId { get; } public string GreenEffectId { get; }
        internal ContentCatalogSnapshot(ContentCatalogInput input,bool legacy)
        {
            if(input==null)throw new ArgumentNullException(nameof(input)); IsLegacyAdapter=legacy;
            if(input.Chassis==null||input.Weapons==null||input.Modules==null||input.Effects==null||input.Templates==null||input.StartingPages==null)throw new ArgumentException(UserText.Message("copy.9c703cb4ad7a7aa1"));
            var effectList=new List<EffectDefinition>();foreach(var item in input.Effects){var def=new EffectDefinition(item);AddUnique(effects,def.Id,def,"effects");effectList.Add(def);}
            var chassisList=new List<ChassisDefinition>();foreach(var item in input.Chassis){if(item==null)throw new ArgumentException(UserText.Message("copy.8c79015d8bd07839"));var def=new ChassisDefinition(item);AddPart(def);chassis.Add(def.Id,def);chassisList.Add(def);}
            var weaponList=new List<WeaponDefinition>();foreach(var item in input.Weapons){if(item==null)throw new ArgumentException(UserText.Message("copy.1d46788adcf6dcce"));var def=new WeaponDefinition(item,effects);AddPart(def);weapons.Add(def.Id,def);weaponList.Add(def);}
            var moduleList=new List<ModuleDefinition>();foreach(var item in input.Modules){if(item==null)throw new ArgumentException(UserText.Message("copy.cf3126da6db78921"));var def=new ModuleDefinition(item);AddPart(def);modules.Add(def.Id,def);moduleList.Add(def);}
            var templateList=new List<DrawingTemplateDefinition>();foreach(var item in input.Templates){if(item==null)throw new ArgumentException(UserText.Message("copy.1ea11335b3662a33"));var def=item.CreateSnapshot();AddUnique(templates,def.Id,def,"templates");templateList.Add(def);}
            if(chassisList.Count==0||weaponList.Count==0)throw new ArgumentException(UserText.Message("copy.a3cf41cbfcc72b12"));
            foreach(var def in moduleList)foreach(string id in def.AllowedChassisIds)if(!chassis.ContainsKey(id))throw new ArgumentException(UserText.Message("copy.ec5a4d91cd4a39f9", def.Id, id));
            if(!legacy)foreach(var def in parts.Values)if(string.IsNullOrWhiteSpace(def.TemplateId)||!templates.ContainsKey(def.TemplateId))throw new ArgumentException(UserText.Message("copy.1704413c67567e8d", def.Id, def.TemplateId));
            RedEffectId=ValidateColorBinding(input.RedEffectId,EffectKind.DamageOverTime,"redEffectId");BlueEffectId=ValidateColorBinding(input.BlueEffectId,EffectKind.Slow,"blueEffectId");GreenEffectId=ValidateColorBinding(input.GreenEffectId,EffectKind.DefenseReduction,"greenEffectId");
            Assessment=(input.Assessment ?? throw new ArgumentException(UserText.Message("copy.325ec8802f5b6699"))).CreateSnapshot();
            chassisList.Sort(Compare);weaponList.Sort(Compare);moduleList.Sort(Compare);
            var all=new List<WorkshopPartDefinition>();all.AddRange(chassisList);all.AddRange(weaponList);all.AddRange(moduleList);all.Sort(Compare);
            AllParts=all.AsReadOnly();Chassis=chassisList.AsReadOnly();Weapons=weaponList.AsReadOnly();Modules=moduleList.AsReadOnly();Effects=effectList.AsReadOnly();Templates=templateList.AsReadOnly();
            var startup=new List<TowerBlueprint>();var pageIds=new HashSet<string>(StringComparer.Ordinal);var slots=new string[ProfileLimits.LoadoutSlotCount];
            for(int i=0;i<input.StartingPages.Count;i++)
            {
                var page=input.StartingPages[i];string path="catalog.startingPages["+i+"]";
                if(page==null)throw new ArgumentException(UserText.Message("copy.cca18f49ef6ec104", path));
                string id=ContentValidation.Id(page.Id,path+".id");if(id.Length>ProfileLimits.MaximumBlueprintIdLength||!pageIds.Add(id))throw new ArgumentException(UserText.Message("copy.244b185b17f53660", path));
                if(string.IsNullOrWhiteSpace(page.Name)||page.Name.Length>ProfileLimits.MaximumBlueprintNameLength)throw new ArgumentException(UserText.Message("copy.871d5b192661b7e1", path));
                if(page.Slot< -1||page.Slot>=slots.Length||(page.Slot>=0&&slots[page.Slot]!=null))throw new ArgumentException(UserText.Message("copy.5cc37e5308594a55", path));
                if(!TryGetChassis(page.ChassisId,out _))throw new ArgumentException(UserText.Message("copy.9b72b01f5c1a9475", path, page.ChassisId));
                if(page.WeaponMounts==null)throw new ArgumentException(path+".weaponMounts: missing list");
                if(page.WeaponMounts.Count==0&&!TryGetWeapon(page.FirstWeaponId,out _))throw new ArgumentException(UserText.Message("copy.d662c11187a4a287", path, page.FirstWeaponId));
                string secondId=string.IsNullOrEmpty(page.SecondWeaponId)?null:page.SecondWeaponId;
                if(secondId!=null&&!TryGetWeapon(secondId,out _))throw new ArgumentException(UserText.Message("copy.123ca857e76d04a1", path, secondId));
                if(page.Modules==null)throw new ArgumentException(UserText.Message("copy.cd6a81f973678ffa", path));
                var selections=new List<ModuleSelection>();foreach(var selection in page.Modules){if(selection==null)throw new ArgumentException(UserText.Message("copy.51746a9092cc1942", path));GetModule(selection.DefinitionId);selections.Add(new ModuleSelection(selection.InstanceId,selection.DefinitionId));}
                TowerBlueprint blueprint;
                if(page.WeaponMounts.Count==0)blueprint=new TowerBlueprint(id,page.Name,page.ChassisId,page.FirstWeaponId,secondId,selections,content:this);
                else
                {
                    var mounts=new List<WeaponMountSelection>();
                    foreach(var mount in page.WeaponMounts){if(mount==null)throw new ArgumentException(path+".weaponMounts: empty mount");GetWeapon(mount.WeaponId);mounts.Add(new WeaponMountSelection(mount.SlotIndex,mount.WeaponId,MortarFootprint.CreateStored(mount.FootprintAspect)));}
                    blueprint=new TowerBlueprint(id,page.Name,page.ChassisId,mounts,selections,content:this);
                }
                startup.Add(blueprint);
                if(page.Slot>=0)slots[page.Slot]=id;
            }
            if(startup.Count>ProfileLimits.MaximumAlbumPages)throw new ArgumentException(UserText.Message("copy.cf56e003e6151510"));
            StartupBlueprints=startup.AsReadOnly();StartupSlotIds=Array.AsReadOnly(slots);
        }
        static int Compare(WorkshopPartDefinition a,WorkshopPartDefinition b){int order=a.Order.CompareTo(b.Order);return order!=0?order:StringComparer.Ordinal.Compare(a.Id,b.Id);}
        void AddPart(WorkshopPartDefinition def)=>AddUnique(parts,def.Id,def,"catalog");
        static void AddUnique<T>(Dictionary<string,T> map,string id,T value,string path){if(map.ContainsKey(id))throw new ArgumentException(UserText.Message("copy.b7cfd726e2b16d69", path, id));map.Add(id,value);}
        string ValidateColorBinding(string id,EffectKind kind,string field){var def=GetEffect(id);if(def.Kind!=kind)throw new ArgumentException(UserText.Message("copy.525c4e463aed54cb", field));return id;}
        public ContentCatalogSnapshot CreateSnapshot()=>this;
        public bool TryGet(string id,out WorkshopPartDefinition value){if(id==null){value=null;return false;}return parts.TryGetValue(id,out value);}
        public bool TryGetChassis(string id,out ChassisDefinition value){if(id==null){value=null;return false;}return chassis.TryGetValue(id,out value);}
        public bool TryGetWeapon(string id,out WeaponDefinition value){if(id==null){value=null;return false;}return weapons.TryGetValue(id,out value);}
        public bool TryGetModule(string id,out ModuleDefinition value){if(id==null){value=null;return false;}return modules.TryGetValue(id,out value);}
        public bool TryGetTemplate(string id,out DrawingTemplateDefinition value){if(id==null){value=null;return false;}return templates.TryGetValue(id,out value);}
        public ChassisDefinition GetChassis(string id)=>TryGetChassis(id,out var value)?value:throw new ArgumentException(UserText.Message("copy.c12b7f46a553fdd9", id));
        public WeaponDefinition GetWeapon(string id)=>TryGetWeapon(id,out var value)?value:throw new ArgumentException(UserText.Message("copy.0853f64b9754c827", id));
        public ModuleDefinition GetModule(string id)=>TryGetModule(id,out var value)?value:throw new ArgumentException(UserText.Message("copy.37e1a6467f128485", id));
        public DrawingTemplateDefinition GetTemplate(string id)=>TryGetTemplate(id,out var value)?value:throw new ArgumentException(UserText.Message("copy.8e472dce99cbd82e", id));
        public EffectDefinition GetEffect(string id)=>id!=null&&effects.TryGetValue(id,out var value)?value:throw new ArgumentException(UserText.Message("copy.eba03185092b0ee3", id));
    }
    public static class LegacyContentIds
    {
        public static string Chassis(ChassisKind kind)=>kind==ChassisKind.Standard?"chassis.standard":kind==ChassisKind.Heavy?"chassis.heavy":null;
        public static string Weapon(StructureKind kind)
        {
            switch(kind){case StructureKind.Wall:return "tool.wall";case StructureKind.Gun:return "weapon.gun";case StructureKind.Piercer:return "weapon.piercer";case StructureKind.Mortar:return "weapon.mortar";case StructureKind.Spikes:return "tool.spikes";case StructureKind.Fan:return "weapon.fan";case StructureKind.Ricochet:return "weapon.ricochet";case StructureKind.Stapler:return "weapon.stapler";case StructureKind.Blotter:return "weapon.blotter";default:return null;}
        }
        public static StructureKind Weapon(string id){foreach(StructureKind kind in Enum.GetValues(typeof(StructureKind)))if(Weapon(kind)==id)return kind;return (StructureKind)(-1);}
        public static ChassisKind Chassis(string id)=>id=="chassis.standard"?ChassisKind.Standard:id=="chassis.heavy"?ChassisKind.Heavy:(ChassisKind)(-1);
        public static string Module(TowerModules module)=>module==TowerModules.Armor?"module.armor":module==TowerModules.Feed?"module.feed":module==TowerModules.Tracking?"module.tracking":module==TowerModules.Rail?"module.rail":null;
        public static TowerModules Module(string id)=>id=="module.armor"?TowerModules.Armor:id=="module.feed"?TowerModules.Feed:id=="module.tracking"?TowerModules.Tracking:id=="module.rail"?TowerModules.Rail:TowerModules.None;
        public static IReadOnlyList<ModuleSelection> Modules(TowerModules mask){var list=new List<ModuleSelection>();foreach(var module in new[]{TowerModules.Armor,TowerModules.Feed,TowerModules.Tracking,TowerModules.Rail})if((mask&module)!=0){string id=Module(module);list.Add(new ModuleSelection(id.Substring("module.".Length),id));}return list.AsReadOnly();}
    }
}
