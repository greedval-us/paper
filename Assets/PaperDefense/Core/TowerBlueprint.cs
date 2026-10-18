using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum ChassisKind { Standard = 0, Heavy = 1 }
    public enum DrawingOrigin { LegacyStandard = 1, PlayerDrawing = 2, Freeform = 3 }
    [Flags] public enum TowerModules { None = 0, Armor = 1, Feed = 2, Tracking = 4, Rail = 8 }

    public sealed class WeaponMountSelection
    {
        public int SlotIndex { get; }
        public string WeaponId { get; }
        public MortarFootprint Footprint { get; }
        public FanVolley Volley { get; }
        public WeaponMountSelection(int slotIndex,string weaponId,MortarFootprint footprint=null,FanVolley volley=null)
        {
            if(slotIndex<0||slotIndex>=TowerBlueprint.MaximumWeaponMounts)throw new ArgumentOutOfRangeException(nameof(slotIndex));
            SlotIndex=slotIndex;WeaponId=ContentValidation.Id(weaponId,"blueprint.weaponMounts.weaponId");Footprint=footprint??MortarFootprint.Circle;Volley=volley;
        }
    }

    public sealed class TowerBlueprint
    {
        public const int MaximumWeaponMounts=3;
        public ArmorDistribution Reinforcement { get; }
        public bool HasArmor
        {
            get
            {
                foreach(var selection in ModuleSelections)
                    if(Content!=null&&Content.TryGetModule(selection.DefinitionId,out var module)
                        ? module.SlotKind==ModuleSlotKind.Armor : LegacyContentIds.Module(selection.DefinitionId)==TowerModules.Armor) return true;
                return false;
            }
        }
        public TowerBlueprint WithReinforcement(ArmorDistribution value)
        {
            if(value!=null&&!HasArmor)throw new ArgumentException(UserText.Message("reinforce.need_armor"));
            return new TowerBlueprint(Id,Name,ChassisId,WeaponMounts,ModuleSelections,Drawing,Origin,Content,EvaluationStamp,FreeformRuleVersion,value);
        }
        public IReadOnlyList<WeaponMountSelection> WeaponMounts { get; }
        public string Id { get; } public string Name { get; }
        public string ChassisId { get; } public string FirstWeaponId { get; } public string SecondWeaponId { get; }
        public IReadOnlyList<ModuleSelection> ModuleSelections { get; }
        public ContentCatalogSnapshot Content { get; }
        public ChassisKind Chassis { get; }
        public StructureKind FirstWeapon { get; }
        public StructureKind? SecondWeapon { get; }
        public TowerModules Modules { get; }
        internal bool LegacyAssembly { get; }
        public DrawingSnapshot Drawing { get; } public DrawingOrigin Origin { get; }
        public bool IsFreeform => Origin == DrawingOrigin.Freeform;
        public int FreeformRuleVersion { get; }
        public bool HasPlayerDrawing => Origin != DrawingOrigin.LegacyStandard;
        public FreeformTowerDesign FreeformDesign { get; }
        public DrawingAssessment Assessment { get; } public DrawingEvaluationStamp EvaluationStamp { get; }
        public MortarFootprint FirstFootprint { get; } public MortarFootprint SecondFootprint { get; }
        public TowerBlueprint(string id,string name,string chassisId,string firstWeaponId,string secondWeaponId=null,
            IEnumerable<ModuleSelection> modules=null,DrawingSnapshot drawing=null,DrawingOrigin origin=DrawingOrigin.LegacyStandard,
            MortarFootprint firstFootprint=null,MortarFootprint secondFootprint=null,ContentCatalogSnapshot content=null,DrawingEvaluationStamp evaluationStamp=null,int freeformRuleVersion=FreeformTowerDesign.RuleVersion,ArmorDistribution reinforcement=null)
            :this(id,name,chassisId,LegacyMounts(firstWeaponId,secondWeaponId,firstFootprint,secondFootprint),modules,drawing,origin,content,evaluationStamp,freeformRuleVersion,reinforcement)
        { }
        static IEnumerable<WeaponMountSelection> LegacyMounts(string first,string second,MortarFootprint firstFootprint,MortarFootprint secondFootprint)
        {
            yield return new WeaponMountSelection(0,first,firstFootprint);
            if(second!=null)yield return new WeaponMountSelection(1,second,secondFootprint);
            else if(secondFootprint!=null&&!secondFootprint.IsCircle)throw new ArgumentException("Only occupied mounts may have a noncircular footprint.");
        }
        public TowerBlueprint(string id,string name,string chassisId,IEnumerable<WeaponMountSelection> weaponMounts,
            IEnumerable<ModuleSelection> modules=null,DrawingSnapshot drawing=null,DrawingOrigin origin=DrawingOrigin.LegacyStandard,
            ContentCatalogSnapshot content=null,DrawingEvaluationStamp evaluationStamp=null,int freeformRuleVersion=FreeformTowerDesign.RuleVersion,ArmorDistribution reinforcement=null)
        {
            if(!Enum.IsDefined(typeof(DrawingOrigin),origin))throw new ArgumentOutOfRangeException(nameof(origin));
            Id=id?.Trim()??"";Name=name?.Trim()??"";ChassisId=ContentValidation.Id(chassisId,"blueprint.chassisId");
            var mounts=new List<WeaponMountSelection>();var slots=new HashSet<int>();
            if(weaponMounts==null)throw new ArgumentNullException(nameof(weaponMounts));
            foreach(var mount in weaponMounts){if(mount==null||!slots.Add(mount.SlotIndex))throw new ArgumentException("blueprint.weaponMounts: empty or duplicate mount");mounts.Add(new WeaponMountSelection(mount.SlotIndex,mount.WeaponId,mount.Footprint,mount.Volley));}
            if(mounts.Count==0||mounts.Count>MaximumWeaponMounts||!slots.Contains(0))throw new ArgumentException("blueprint.weaponMounts: slot 0 is required; at most three mounts");
            mounts.Sort((a,b)=>a.SlotIndex.CompareTo(b.SlotIndex));WeaponMounts=mounts.AsReadOnly();
            FirstWeaponId=GetWeaponId(0);SecondWeaponId=GetWeaponId(1);
            var owned=new List<ModuleSelection>();var instances=new HashSet<string>(StringComparer.Ordinal);
            if(modules!=null)foreach(var module in modules){if(module==null||!instances.Add(module.InstanceId))throw new ArgumentException(UserText.Message("copy.14b33da5d4fa66a2"));if(owned.Count>=DrawingSnapshot.MaximumParts)throw new ArgumentException(UserText.Message("copy.64b9875afd6f3520"));owned.Add(new ModuleSelection(module.InstanceId,module.DefinitionId));}
            ModuleSelections=owned.AsReadOnly();Content=content;Chassis=LegacyContentIds.Chassis(chassisId);FirstWeapon=LegacyContentIds.Weapon(FirstWeaponId);SecondWeapon=SecondWeaponId==null?(StructureKind?)null:LegacyContentIds.Weapon(SecondWeaponId);
            foreach(var module in owned)Modules|=LegacyContentIds.Module(module.DefinitionId);
            Drawing=drawing??DrawingSnapshot.Empty;Origin=origin;FirstFootprint=GetWeaponMount(0).Footprint;SecondFootprint=GetWeaponMount(1)?.Footprint??MortarFootprint.Circle;
            if (IsFreeform && (freeformRuleVersion < 1 || freeformRuleVersion > FreeformTowerDesign.RuleVersion)) throw new ArgumentOutOfRangeException(nameof(freeformRuleVersion));
            FreeformRuleVersion = IsFreeform ? freeformRuleVersion : 0;
            Reinforcement=reinforcement;
            if(origin==DrawingOrigin.LegacyStandard&&!Drawing.IsEmpty)throw new ArgumentException(UserText.Message("copy.b343fa86244c62b6"),nameof(origin));
            FreeformDesign=IsFreeform?new FreeformTowerDesign(this):null;
            Assessment=DrawingEvaluator.Evaluate(this,evaluationStamp);EvaluationStamp=Assessment.ToStamp();
        }
        public TowerBlueprint(string id,string name,ChassisKind chassis,StructureKind firstWeapon,StructureKind? secondWeapon=null,TowerModules modules=TowerModules.None,
            DrawingSnapshot drawing=null,DrawingOrigin origin=DrawingOrigin.LegacyStandard,MortarFootprint firstFootprint=null,MortarFootprint secondFootprint=null,DrawingEvaluationStamp evaluationStamp=null,int freeformRuleVersion=FreeformTowerDesign.RuleVersion,ArmorDistribution reinforcement=null)
            :this(id,name,LegacyContentIds.Chassis(chassis)??"legacy.invalid.chassis."+(int)chassis,LegacyContentIds.Weapon(firstWeapon)??"legacy.invalid.weapon."+(int)firstWeapon,
                secondWeapon.HasValue?(LegacyContentIds.Weapon(secondWeapon.Value)??"legacy.invalid.weapon."+(int)secondWeapon.Value):null,LegacyContentIds.Modules(modules),drawing,origin,firstFootprint,secondFootprint,null,evaluationStamp,freeformRuleVersion,reinforcement)
        {
            Chassis=chassis;FirstWeapon=firstWeapon;SecondWeapon=secondWeapon;Modules=modules;LegacyAssembly=true;
            if((firstWeapon!=StructureKind.Mortar&&!FirstFootprint.IsCircle)||(secondWeapon!=StructureKind.Mortar&&!SecondFootprint.IsCircle))throw new ArgumentException("Only mortar mounts may have a noncircular footprint.");
        }
        public bool SupportsFootprint(int mount)
        {string id=mount<0||mount>=MaximumWeaponMounts?null:GetWeaponId(mount);return id!=null&&(Content!=null&&Content.TryGetWeapon(id,out var weapon)?weapon.SupportsFootprint:LegacyContentIds.Weapon(id)==StructureKind.Mortar);}
        public WeaponMountSelection GetWeaponMount(int mount)
        {if(mount<0||mount>=MaximumWeaponMounts)throw new ArgumentOutOfRangeException(nameof(mount));foreach(var selection in WeaponMounts)if(selection.SlotIndex==mount)return selection;return null;}
        public string GetWeaponId(int mount)=>GetWeaponMount(mount)?.WeaponId;
        public MortarFootprint GetMortarFootprint(int mount){if(!SupportsFootprint(mount))throw new ArgumentOutOfRangeException(nameof(mount),"Mount must support an area projectile footprint.");return GetWeaponMount(mount).Footprint;}
        public TowerBlueprint WithMortarFootprint(int mount,MortarFootprint footprint)
        {GetMortarFootprint(mount);if(footprint==null)throw new ArgumentNullException(nameof(footprint));var selected=new List<WeaponMountSelection>();foreach(var item in WeaponMounts)selected.Add(item.SlotIndex==mount?new WeaponMountSelection(mount,item.WeaponId,footprint,item.Volley):item);return new TowerBlueprint(Id,Name,ChassisId,selected,ModuleSelections,Drawing,Origin,Content,EvaluationStamp,FreeformRuleVersion,Reinforcement);}
        public bool SupportsVolley(int mount)
        {string id=GetWeaponId(mount);return id!=null&&(Content!=null&&Content.TryGetWeapon(id,out var weapon)?weapon.SupportsVolley:LegacyContentIds.Weapon(id)==StructureKind.Fan);}
        public FanVolley GetFanVolley(int mount)
        {if(!SupportsVolley(mount))throw new ArgumentOutOfRangeException(nameof(mount));return GetWeaponMount(mount).Volley;}
        public TowerBlueprint WithFanVolley(int mount,FanVolley volley)
        {
            GetFanVolley(mount);var selected=new List<WeaponMountSelection>();
            foreach(var item in WeaponMounts)selected.Add(item.SlotIndex==mount?new WeaponMountSelection(mount,item.WeaponId,item.Footprint,volley):item);
            return new TowerBlueprint(Id,Name,ChassisId,selected,ModuleSelections,Drawing,Origin,Content,EvaluationStamp,FreeformRuleVersion,Reinforcement);
        }
        TowerBlueprint Copy(string id,string name,DrawingSnapshot drawing,DrawingOrigin origin,ContentCatalogSnapshot content,DrawingEvaluationStamp stamp)
            =>new TowerBlueprint(id,name,ChassisId,WeaponMounts,ModuleSelections,drawing,origin,content,stamp,IsFreeform?FreeformRuleVersion:FreeformTowerDesign.RuleVersion,Reinforcement);
        public TowerBlueprint WithContent(ContentCatalogSnapshot content)=>ReferenceEquals(Content,content)?this:Copy(Id,Name,Drawing,Origin,content,EvaluationStamp);
        public TowerBlueprint WithName(string name)=>Copy(Id,name,Drawing,Origin,Content,EvaluationStamp);
        public TowerBlueprint WithDrawing(DrawingSnapshot drawing)=>Copy(Id,Name,drawing??throw new ArgumentNullException(nameof(drawing)),IsFreeform?DrawingOrigin.Freeform:DrawingOrigin.PlayerDrawing,Content,HasPlayerDrawing&&drawing.GeometryHash==Drawing.GeometryHash?EvaluationStamp:null);
        /// <summary>Explicitly applies current construction rules. Ordinary copies retain historical rules.</summary>
        public TowerBlueprint WithFreeformDrawing(DrawingSnapshot drawing)=>new TowerBlueprint(Id,Name,ChassisId,WeaponMounts,ModuleSelections,drawing??throw new ArgumentNullException(nameof(drawing)),DrawingOrigin.Freeform,Content,reinforcement:Reinforcement);
        public TowerBlueprint AsFreeform()=>IsFreeform?this:WithFreeformDrawing(Drawing);
        public TowerBlueprint AsVariant(string id,string name)=>Copy(id,name,Drawing.DeepCopy(),Origin,Content,EvaluationStamp);
        public TowerBlueprint AsNew(string id,string name)=>Copy(id,name,HasPlayerDrawing?Drawing.DeepCopy():DrawingSnapshot.Empty,IsFreeform?DrawingOrigin.Freeform:DrawingOrigin.PlayerDrawing,Content,HasPlayerDrawing?EvaluationStamp:null);
        /// <summary>Creates a ready-to-assemble standard page without authored geometry.</summary>
        public static TowerBlueprint NewStandardDraft(string id,string name,string chassis,string firstWeapon,string secondWeapon=null,IEnumerable<ModuleSelection> modules=null,ContentCatalogSnapshot content=null)
            =>new TowerBlueprint(id,name,chassis,firstWeapon,secondWeapon,modules,DrawingSnapshot.Empty,DrawingOrigin.LegacyStandard,content:content);
        /// <summary>Changes a standard recipe without converting it to the historical drawing editor format.</summary>
        public TowerBlueprint WithStandardAssembly(string chassis,IEnumerable<WeaponMountSelection> weaponMounts,IEnumerable<ModuleSelection> modules)
        {
            if(Origin!=DrawingOrigin.LegacyStandard)throw new InvalidOperationException("Historical drawings cannot be changed through the standard assembly path.");
            var candidate=WithAssembly(chassis,weaponMounts,modules);
            return new TowerBlueprint(Id,Name,candidate.ChassisId,candidate.WeaponMounts,candidate.ModuleSelections,
                DrawingSnapshot.Empty,DrawingOrigin.LegacyStandard,Content,reinforcement:candidate.Reinforcement);
        }
        public static TowerBlueprint NewDraft(string id,string name,ChassisKind chassis,StructureKind firstWeapon,StructureKind? secondWeapon=null,TowerModules modules=TowerModules.None)=>new TowerBlueprint(id,name,chassis,firstWeapon,secondWeapon,modules,DrawingSnapshot.Empty,DrawingOrigin.PlayerDrawing);
        public static TowerBlueprint NewDraft(string id,string name,string chassis,string firstWeapon,string secondWeapon=null,IEnumerable<ModuleSelection> modules=null,ContentCatalogSnapshot content=null)=>new TowerBlueprint(id,name,chassis,firstWeapon,secondWeapon,modules,DrawingSnapshot.Empty,DrawingOrigin.PlayerDrawing,content:content);
        public TowerBlueprint WithAssembly(ChassisKind chassis,StructureKind first,StructureKind? second,TowerModules modules)=>WithAssembly(LegacyContentIds.Chassis(chassis),LegacyContentIds.Weapon(first),second.HasValue?LegacyContentIds.Weapon(second.Value):null,LegacyContentIds.Modules(modules));
        public TowerBlueprint WithAssembly(string chassis,string first,string second,IEnumerable<ModuleSelection> modules)
        {
            var mounts=new List<WeaponMountSelection>{new WeaponMountSelection(0,first,first==FirstWeaponId?FirstFootprint:null,first==FirstWeaponId?GetWeaponMount(0)?.Volley:null)};
            if(second!=null)mounts.Add(new WeaponMountSelection(1,second,second==SecondWeaponId?SecondFootprint:null,second==SecondWeaponId?GetWeaponMount(1)?.Volley:null));
            if(GetWeaponMount(2)!=null)mounts.Add(GetWeaponMount(2));
            return WithAssembly(chassis,mounts,modules);
        }
        public TowerBlueprint WithWeaponMount(int mount,string weaponId)
        {
            GetWeaponMount(mount);if(mount==0&&weaponId==null)throw new ArgumentException("The first mount is required.",nameof(weaponId));
            var selected=new List<WeaponMountSelection>();foreach(var item in WeaponMounts)if(item.SlotIndex!=mount)selected.Add(item);
            if(weaponId!=null)selected.Add(new WeaponMountSelection(mount,weaponId,weaponId==GetWeaponId(mount)?GetWeaponMount(mount).Footprint:null,weaponId==GetWeaponId(mount)?GetWeaponMount(mount).Volley:null));
            return WithAssembly(ChassisId,selected,ModuleSelections);
        }
        public TowerBlueprint WithAssembly(string chassis,IEnumerable<WeaponMountSelection> weaponMounts,IEnumerable<ModuleSelection> modules)
        {
            var mounts=new List<WeaponMountSelection>(weaponMounts??throw new ArgumentNullException(nameof(weaponMounts)));mounts.Sort((a,b)=>a.SlotIndex.CompareTo(b.SlotIndex));
            var selected=new List<ModuleSelection>(modules??Array.Empty<ModuleSelection>());
            bool sameModules=selected.Count==ModuleSelections.Count;
            for(int i=0;sameModules&&i<selected.Count;i++)sameModules=selected[i].InstanceId==ModuleSelections[i].InstanceId&&selected[i].DefinitionId==ModuleSelections[i].DefinitionId;
            bool same=chassis==ChassisId&&mounts.Count==WeaponMounts.Count&&sameModules;
            for(int i=0;same&&i<mounts.Count;i++)same=mounts[i].SlotIndex==WeaponMounts[i].SlotIndex&&mounts[i].WeaponId==WeaponMounts[i].WeaponId&&mounts[i].Footprint.SameShape(WeaponMounts[i].Footprint);
            if(same)
            {
                for(int i=0;i<mounts.Count;i++)if(!ReferenceEquals(mounts[i].Volley,WeaponMounts[i].Volley))
                    return new TowerBlueprint(Id,Name,ChassisId,mounts,ModuleSelections,Drawing,Origin,Content,EvaluationStamp,FreeformRuleVersion,Reinforcement);
                return this;
            }
            var candidate=new TowerBlueprint(Id,Name,chassis,mounts,selected,DrawingSnapshot.Empty,IsFreeform?DrawingOrigin.Freeform:DrawingOrigin.PlayerDrawing,Content,freeformRuleVersion:IsFreeform?FreeformRuleVersion:FreeformTowerDesign.RuleVersion);
            // A deserialized custom module has no catalog yet. Unchanged module selections retain the layer.
            if(Reinforcement!=null&&(candidate.HasArmor||sameModules))
                candidate=new TowerBlueprint(Id,Name,chassis,mounts,selected,candidate.Drawing,candidate.Origin,Content,freeformRuleVersion:candidate.FreeformRuleVersion,reinforcement:Reinforcement);
            if(Origin==DrawingOrigin.LegacyStandard)return candidate;
            var retained=new List<DrawingPart>();foreach(var part in Drawing.Parts)if(DrawingPartPolicy.ShouldRetain(part.BindingId,this,candidate))retained.Add(part);
            return candidate.WithDrawing(new DrawingSnapshot(retained));
        }
    }
}
