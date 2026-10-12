using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PaperDefense.Core
{
    public enum ChassisKind { Standard = 0, Heavy = 1 }
    public enum DrawingOrigin { LegacyStandard = 1, PlayerDrawing = 2 }
    [Flags] public enum TowerModules { None = 0, Armor = 1, Feed = 2, Tracking = 4, Rail = 8 }

    public sealed class TowerBlueprint
    {
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
        public DrawingAssessment Assessment { get; } public DrawingEvaluationStamp EvaluationStamp { get; }
        public MortarFootprint FirstFootprint { get; } public MortarFootprint SecondFootprint { get; }
        public TowerBlueprint(string id,string name,string chassisId,string firstWeaponId,string secondWeaponId=null,
            IEnumerable<ModuleSelection> modules=null,DrawingSnapshot drawing=null,DrawingOrigin origin=DrawingOrigin.LegacyStandard,
            MortarFootprint firstFootprint=null,MortarFootprint secondFootprint=null,ContentCatalogSnapshot content=null,DrawingEvaluationStamp evaluationStamp=null)
        {
            if(!Enum.IsDefined(typeof(DrawingOrigin),origin))throw new ArgumentOutOfRangeException(nameof(origin));
            Id=id?.Trim()??"";Name=name?.Trim()??"";ChassisId=ContentValidation.Id(chassisId,"blueprint.chassisId");FirstWeaponId=ContentValidation.Id(firstWeaponId,"blueprint.firstWeaponId");
            SecondWeaponId=secondWeaponId==null?null:ContentValidation.Id(secondWeaponId,"blueprint.secondWeaponId");
            var owned=new List<ModuleSelection>();var instances=new HashSet<string>(StringComparer.Ordinal);
            if(modules!=null)foreach(var module in modules){if(module==null||!instances.Add(module.InstanceId))throw new ArgumentException("blueprint.modules: пустой или повторяющийся экземпляр");if(owned.Count>=DrawingSnapshot.MaximumParts)throw new ArgumentException("blueprint.modules: слишком много экземпляров");owned.Add(new ModuleSelection(module.InstanceId,module.DefinitionId));}
            ModuleSelections=owned.AsReadOnly();Content=content;Chassis=LegacyContentIds.Chassis(chassisId);FirstWeapon=LegacyContentIds.Weapon(firstWeaponId);SecondWeapon=secondWeaponId==null?(StructureKind?)null:LegacyContentIds.Weapon(secondWeaponId);
            foreach(var module in owned)Modules|=LegacyContentIds.Module(module.DefinitionId);
            Drawing=drawing??DrawingSnapshot.Empty;Origin=origin;FirstFootprint=firstFootprint??MortarFootprint.Circle;SecondFootprint=secondFootprint??MortarFootprint.Circle;
            if(origin==DrawingOrigin.LegacyStandard&&!Drawing.IsEmpty)throw new ArgumentException("Авторские линии требуют пометки PlayerDrawing.",nameof(origin));
            Assessment=DrawingEvaluator.Evaluate(this,evaluationStamp);EvaluationStamp=Assessment.ToStamp();
        }
        public TowerBlueprint(string id,string name,ChassisKind chassis,StructureKind firstWeapon,StructureKind? secondWeapon=null,TowerModules modules=TowerModules.None,
            DrawingSnapshot drawing=null,DrawingOrigin origin=DrawingOrigin.LegacyStandard,MortarFootprint firstFootprint=null,MortarFootprint secondFootprint=null,DrawingEvaluationStamp evaluationStamp=null)
            :this(id,name,LegacyContentIds.Chassis(chassis)??"legacy.invalid.chassis."+(int)chassis,LegacyContentIds.Weapon(firstWeapon)??"legacy.invalid.weapon."+(int)firstWeapon,
                secondWeapon.HasValue?(LegacyContentIds.Weapon(secondWeapon.Value)??"legacy.invalid.weapon."+(int)secondWeapon.Value):null,LegacyContentIds.Modules(modules),drawing,origin,firstFootprint,secondFootprint,null,evaluationStamp)
        {
            Chassis=chassis;FirstWeapon=firstWeapon;SecondWeapon=secondWeapon;Modules=modules;LegacyAssembly=true;
            if((firstWeapon!=StructureKind.Mortar&&FirstFootprint.AspectRatio!=1)||(secondWeapon!=StructureKind.Mortar&&SecondFootprint.AspectRatio!=1))throw new ArgumentException("Only mortar mounts may have a noncircular footprint.");
        }
        public bool SupportsFootprint(int mount)
        {string id=mount==0?FirstWeaponId:mount==1?SecondWeaponId:null;return id!=null&&(Content!=null&&Content.TryGetWeapon(id,out var weapon)?weapon.SupportsFootprint:LegacyContentIds.Weapon(id)==StructureKind.Mortar);}
        public MortarFootprint GetMortarFootprint(int mount){if(!SupportsFootprint(mount))throw new ArgumentOutOfRangeException(nameof(mount),"Mount must support an area projectile footprint.");return mount==0?FirstFootprint:SecondFootprint;}
        public TowerBlueprint WithMortarFootprint(int mount,MortarFootprint footprint){GetMortarFootprint(mount);if(footprint==null)throw new ArgumentNullException(nameof(footprint));return Copy(Id,Name,Drawing,Origin,mount==0?footprint:FirstFootprint,mount==1?footprint:SecondFootprint,Content,EvaluationStamp);}
        TowerBlueprint Copy(string id,string name,DrawingSnapshot drawing,DrawingOrigin origin,MortarFootprint first,MortarFootprint second,ContentCatalogSnapshot content,DrawingEvaluationStamp stamp)
            =>new TowerBlueprint(id,name,ChassisId,FirstWeaponId,SecondWeaponId,ModuleSelections,drawing,origin,first,second,content,stamp);
        public TowerBlueprint WithContent(ContentCatalogSnapshot content)=>ReferenceEquals(Content,content)?this:Copy(Id,Name,Drawing,Origin,FirstFootprint,SecondFootprint,content,EvaluationStamp);
        public TowerBlueprint WithName(string name)=>Copy(Id,name,Drawing,Origin,FirstFootprint,SecondFootprint,Content,EvaluationStamp);
        public TowerBlueprint WithDrawing(DrawingSnapshot drawing)=>Copy(Id,Name,drawing??throw new ArgumentNullException(nameof(drawing)),DrawingOrigin.PlayerDrawing,FirstFootprint,SecondFootprint,Content,Origin==DrawingOrigin.PlayerDrawing&&drawing.GeometryHash==Drawing.GeometryHash?EvaluationStamp:null);
        public TowerBlueprint AsVariant(string id,string name)=>Copy(id,name,Drawing.DeepCopy(),Origin,FirstFootprint,SecondFootprint,Content,EvaluationStamp);
        public TowerBlueprint AsNew(string id,string name)=>Copy(id,name,Origin==DrawingOrigin.PlayerDrawing?Drawing.DeepCopy():DrawingSnapshot.Empty,DrawingOrigin.PlayerDrawing,FirstFootprint,SecondFootprint,Content,Origin==DrawingOrigin.PlayerDrawing?EvaluationStamp:null);
        public static TowerBlueprint NewDraft(string id,string name,ChassisKind chassis,StructureKind firstWeapon,StructureKind? secondWeapon=null,TowerModules modules=TowerModules.None)=>new TowerBlueprint(id,name,chassis,firstWeapon,secondWeapon,modules,DrawingSnapshot.Empty,DrawingOrigin.PlayerDrawing);
        public static TowerBlueprint NewDraft(string id,string name,string chassis,string firstWeapon,string secondWeapon=null,IEnumerable<ModuleSelection> modules=null,ContentCatalogSnapshot content=null)=>new TowerBlueprint(id,name,chassis,firstWeapon,secondWeapon,modules,DrawingSnapshot.Empty,DrawingOrigin.PlayerDrawing,content:content);
        public TowerBlueprint WithAssembly(ChassisKind chassis,StructureKind first,StructureKind? second,TowerModules modules)=>WithAssembly(LegacyContentIds.Chassis(chassis),LegacyContentIds.Weapon(first),second.HasValue?LegacyContentIds.Weapon(second.Value):null,LegacyContentIds.Modules(modules));
        public TowerBlueprint WithAssembly(string chassis,string first,string second,IEnumerable<ModuleSelection> modules)
        {
            var selected=new List<ModuleSelection>(modules??Array.Empty<ModuleSelection>());
            bool same=chassis==ChassisId&&first==FirstWeaponId&&second==SecondWeaponId&&selected.Count==ModuleSelections.Count;
            for(int i=0;same&&i<selected.Count;i++)same=selected[i].InstanceId==ModuleSelections[i].InstanceId&&selected[i].DefinitionId==ModuleSelections[i].DefinitionId;
            if(same)return this;
            var candidate=new TowerBlueprint(Id,Name,chassis,first,second,selected,DrawingSnapshot.Empty,DrawingOrigin.PlayerDrawing,first==FirstWeaponId?FirstFootprint:MortarFootprint.Circle,second==SecondWeaponId?SecondFootprint:MortarFootprint.Circle,Content);
            if(Origin==DrawingOrigin.LegacyStandard)return candidate;
            var retained=new List<DrawingPart>();foreach(var part in Drawing.Parts)if(DrawingPartPolicy.ShouldRetain(part.BindingId,this,candidate))retained.Add(part);
            return candidate.WithDrawing(new DrawingSnapshot(retained));
        }
    }
    public static class TowerCatalog
    {
        public const int MaximumGenericModuleBindings=DrawingSnapshot.MaximumParts-DrawingSnapshot.LegacyPartCount;
        sealed class AssemblyCache
        {
            readonly ConditionalWeakTable<ContentCatalogSnapshot,ResolvedTowerAssembly> assemblies=new ConditionalWeakTable<ContentCatalogSnapshot,ResolvedTowerAssembly>();
            readonly ConditionalWeakTable<ContentCatalogSnapshot,ResolvedTowerAssembly>.CreateValueCallback createAssembly;
            internal AssemblyCache(TowerBlueprint blueprint){createAssembly=content=>new ResolvedTowerAssembly(blueprint,content);}
            internal ResolvedTowerAssembly Resolve(ContentCatalogSnapshot content)=>assemblies.GetValue(content,createAssembly);
        }
        static readonly ConditionalWeakTable<TowerBlueprint,AssemblyCache> assemblyCaches=new ConditionalWeakTable<TowerBlueprint,AssemblyCache>();
        static readonly ConditionalWeakTable<TowerBlueprint,AssemblyCache>.CreateValueCallback createCache=blueprint=>new AssemblyCache(blueprint);
        static readonly GameBalance historicalBalance=GameBalance.CreateDefault();
        static GameBalance Balance(GameBalance balance)=>balance??historicalBalance;
        static ContentCatalogSnapshot Content(TowerBlueprint blueprint,GameBalance balance)=>balance?.Content??blueprint?.Content??LegacyContentCatalog.Create(Balance(balance));
        internal static ResolvedTowerAssembly Resolve(TowerBlueprint blueprint,GameBalance balance)=>Resolve(blueprint,Content(blueprint,balance));
        internal static ResolvedTowerAssembly Resolve(TowerBlueprint blueprint,ContentCatalogSnapshot content)
            =>content.IsLegacyAdapter?new ResolvedTowerAssembly(blueprint,content):assemblyCaches.GetValue(blueprint,createCache).Resolve(content);
        public static int ToolCost(GameBalance balance,StructureKind kind,MaterialTier tier)=>(int)Math.Ceiling(Math.Max(0,balance.Structure(kind).Cost)*Math.Max(0,balance.Material(tier).CostMultiplier));
        public static float QualityMultiplier(TowerBlueprint blueprint,GameBalance balance)=>QualityMultiplier(blueprint,balance,balance.Mechanics.DrawingQuality);
        public static float QualityMultiplier(TowerBlueprint blueprint,GameBalance balance,bool enabled)=>!enabled||blueprint==null||blueprint.Origin==DrawingOrigin.LegacyStandard?1:DrawingEvaluator.FireMultiplier(blueprint.Assessment.Quality,balance.DrawingMinimumRate,balance.DrawingMaximumRate);
        internal static float QualityMultiplier(float quality,GameBalance balance)=>balance.Mechanics.DrawingQuality?DrawingEvaluator.FireMultiplier(quality,balance.DrawingMinimumRate,balance.DrawingMaximumRate):1;
        public static float ActionInterval(StructureDefinition definition,float productionMultiplier,float qualityMultiplier)=>ActionInterval(definition.ShotInterval,productionMultiplier,qualityMultiplier,historicalBalance);
        static float ActionInterval(float seconds,float production,float quality,GameBalance balance)
        {if(quality<=0||quality>1||float.IsNaN(quality)||float.IsInfinity(quality))throw new ArgumentOutOfRangeException(nameof(quality));return Math.Max(balance.Combat.MinimumActionInterval,seconds/Math.Max(balance.Combat.MinimumProductionMultiplier,production))/quality;}
        internal static float ActionInterval(ResolvedTowerAssembly assembly,WeaponDefinition weapon,int mount,GameBalance balance,float production,float quality)
        // New additive modifiers obey the configured floor after supply and quality have been applied.
        {float interval=ActionInterval(weapon.Stats.ShotInterval,production,quality,balance);return assembly==null?interval:Math.Max(balance.Combat.MinimumActionInterval,assembly.Modify(ModifierStat.ActionInterval,interval,mount,weapon.Attack==AttackKind.Cleanup));}
        public static float ActionInterval(TowerBlueprint blueprint,GameBalance balance,StructureKind kind,float production,float quality)
        {if(blueprint==null)return ActionInterval(balance.Structure(kind).ShotInterval,production,quality,balance);var a=Resolve(blueprint,balance);var weapon=a.Content.GetWeapon(LegacyContentIds.Weapon(kind));return ActionInterval(a,weapon,a.First.Id!=weapon.Id&&a.Second?.Id==weapon.Id?1:0,balance,production,quality);}
        public static float ActionInterval(TowerBlueprint blueprint,GameBalance balance,string weaponId,float production,float quality,int mount=0){var a=Resolve(blueprint,balance);return ActionInterval(a,a.Content.GetWeapon(weaponId),mount,balance,production,quality);}
        public static int Capacity(ChassisKind chassis)=>Capacity(chassis,null); public static int Capacity(TowerBlueprint blueprint)=>Capacity(blueprint,null);
        public static int Capacity(ChassisKind chassis,GameBalance balance)=>Content(null,balance).GetChassis(LegacyContentIds.Chassis(chassis)).Capacity;
        public static int Capacity(TowerBlueprint blueprint,GameBalance balance)=>Resolve(blueprint,balance).Chassis.Capacity;
        public static int Load(TowerBlueprint blueprint)=>Load(blueprint,null);
        public static int Load(TowerBlueprint blueprint,GameBalance balance){var a=Resolve(blueprint,balance);long load=(long)a.First.Load+(a.Second?.Load??0);foreach(var m in a.Modules)load+=m.Load;return (int)Math.Min(int.MaxValue,load);}
        public static bool IsMountPart(StructureKind kind)=>PartCatalog.TryGetMount(kind,out _);
        public static float WeaponRange(TowerBlueprint blueprint,GameBalance balance,StructureKind kind)=>WeaponRange(blueprint,balance,LegacyContentIds.Weapon(kind));
        public static float WeaponRange(TowerBlueprint blueprint,GameBalance balance,string id,int mount=0)
        {var a=blueprint==null?null:Resolve(blueprint,balance);var w=(a?.Content??Content(null,balance)).GetWeapon(id);return WeaponRange(a,w,mount);}
        internal static float WeaponRange(ResolvedTowerAssembly assembly,WeaponDefinition weapon,int mount)
        {float initial=weapon.Attack==AttackKind.Radial?Math.Max(weapon.MinimumRadialRange,weapon.Stats.Radius):weapon.Stats.Range;return Math.Max(0,assembly?.Modify(ModifierStat.WeaponRange,initial,mount,weapon.Attack==AttackKind.Cleanup)??initial);}
        public static bool ValidateParts(TowerBlueprint blueprint,out string reason)
        {
            reason="";if(blueprint==null)reason="Выберите чертёж.";
            else if(blueprint.Id.Length==0||blueprint.Id.Length>ProfileLimits.MaximumBlueprintIdLength||blueprint.Name.Length==0||blueprint.Name.Length>ProfileLimits.MaximumBlueprintNameLength)reason="Нужны идентификатор и название до "+ProfileLimits.MaximumBlueprintNameLength+" знаков.";
            else if(blueprint.LegacyAssembly&&(!Enum.IsDefined(typeof(ChassisKind),blueprint.Chassis)||(blueprint.Modules&~(TowerModules.Armor|TowerModules.Feed|TowerModules.Tracking|TowerModules.Rail))!=0||!IsMountPart(blueprint.FirstWeapon)||(blueprint.SecondWeapon.HasValue&&!IsMountPart(blueprint.SecondWeapon.Value))))reason="Неизвестная деталь сборки.";
            return reason.Length==0;
        }
        public static bool Validate(TowerBlueprint blueprint,out string reason)=>Validate(blueprint,null,out reason);
        public static bool Validate(TowerBlueprint blueprint,GameBalance balance,out string reason)
        {if(!ValidateParts(blueprint,out reason))return false;try{var a=Resolve(blueprint,balance);a.Validate(Balance(balance));int excess=Load(blueprint,balance)-a.Chassis.Capacity;if(excess>0)reason="Не хватает "+excess+" ед. нагрузки. Снимите оружие или выберите другое основание.";}catch(ArgumentException ex){reason=ex.Message;}return reason.Length==0;}
        public static int Cost(TowerBlueprint blueprint,GameBalance balance,MaterialTier tier)
        {var a=Resolve(blueprint,balance);long cost=a.Chassis.BaseCost;cost+=Math.Max(0,a.First.Stats.Cost-a.First.AssemblyCredit);if(a.Second!=null)cost+=Math.Max(0,a.Second.Stats.Cost-a.Second.AssemblyCredit);foreach(var m in a.Modules)cost+=m.Cost;return(int)Math.Min(int.MaxValue,Math.Ceiling(cost*(double)Math.Max(0,balance.Material(tier).CostMultiplier)));}
        public static float Health(TowerBlueprint blueprint,GameBalance balance,MaterialTier tier){var a=Resolve(blueprint,balance);float health=a.Chassis.BaseHealth*a.Chassis.HealthMultiplier;health=a.Modify(ModifierStat.FoundationHealth,health);return Math.Max(1,health*balance.Material(tier).HealthMultiplier);}
        public static float Defense(TowerBlueprint blueprint,GameBalance balance)=>blueprint==null?0:Resolve(blueprint,balance).Modify(ModifierStat.FoundationDefense,0);
        public static int ColorCost(TowerBlueprint blueprint,GameBalance balance)=>(int)Math.Max(balance.Construction.ColorMinimum,((long)Cost(blueprint,balance,MaterialTier.Pencil)+balance.Construction.ColorCostDivisor-1)/balance.Construction.ColorCostDivisor);
        public static float BuildSeconds(TowerBlueprint blueprint,GameBalance balance,MaterialTier tier){var a=Resolve(blueprint,balance);float seconds=a.First.Stats.BuildSeconds;if(a.Second!=null)seconds=Math.Max(seconds,a.Second.Stats.BuildSeconds)+a.Chassis.SecondaryBuildSeconds;seconds+=a.Chassis.BuildSeconds;foreach(var m in a.Modules)seconds+=m.BuildSeconds;return Math.Max(0,seconds*balance.Material(tier).BuildTimeMultiplier);}
        public static float FeedMultiplier(TowerBlueprint blueprint)=>FeedMultiplier(blueprint,null); public static float AcquisitionSeconds(TowerBlueprint blueprint)=>AcquisitionSeconds(blueprint,null);
        public static float FeedMultiplier(TowerBlueprint blueprint,GameBalance balance,int activeMounts)=>FeedMultiplier(Resolve(blueprint,balance),Balance(balance),activeMounts);
        internal static float FeedMultiplier(ResolvedTowerAssembly a,GameBalance balance,int activeMounts)
        {var r=balance.Construction;float module=a.Modify(ModifierStat.FeedMultiplier,1);if(r.DynamicFeed){if(activeMounts<=0)return 0;return Math.Min(r.MaxFeedMultiplier,Math.Min(module,r.DynamicFeedCapacity*module/activeMounts));}return Math.Min(r.MaxFeedMultiplier,(a.Second!=null?r.DualFeedMultiplier:1)*module);}
        public static float FeedMultiplier(TowerBlueprint blueprint,GameBalance balance)=>FeedMultiplier(blueprint,balance,blueprint.SecondWeaponId!=null?2:1);
        public static float AcquisitionSeconds(TowerBlueprint blueprint,GameBalance balance)=>AcquisitionSeconds(Resolve(blueprint,balance),Balance(balance));
        internal static float AcquisitionSeconds(ResolvedTowerAssembly a,GameBalance balance,int mount=0)=>a.Modify(ModifierStat.AcquisitionSeconds,balance.Construction.AcquisitionSeconds,mount,(mount==0?a.First:a.Second)?.Attack==AttackKind.Cleanup);
    }
}
