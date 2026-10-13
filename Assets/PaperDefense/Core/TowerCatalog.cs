using System;
using System.Runtime.CompilerServices;

namespace PaperDefense.Core
{
    /// <summary>Resolves catalog assemblies and their effective construction and combat statistics.</summary>
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
        {
            if(blueprint==null)return ActionInterval(balance.Structure(kind).ShotInterval,production,quality,balance);
            var assembly=Resolve(blueprint,balance);
            var weapon=assembly.Content.GetWeapon(LegacyContentIds.Weapon(kind));
            return ActionInterval(assembly,weapon,FindWeaponMount(blueprint,weapon.Id),balance,production,quality);
        }
        public static float ActionInterval(TowerBlueprint blueprint,GameBalance balance,string weaponId,float production,float quality,int mount=0){var a=Resolve(blueprint,balance);return ActionInterval(a,a.Content.GetWeapon(weaponId),mount,balance,production,quality);}
        public static int Capacity(ChassisKind chassis)=>Capacity(chassis,null); public static int Capacity(TowerBlueprint blueprint)=>Capacity(blueprint,null);
        public static int Capacity(ChassisKind chassis,GameBalance balance)=>Content(null,balance).GetChassis(LegacyContentIds.Chassis(chassis)).Capacity;
        public static int Capacity(TowerBlueprint blueprint,GameBalance balance)=>Resolve(blueprint,balance).Chassis.Capacity;
        public static int Load(TowerBlueprint blueprint)=>Load(blueprint,null);
        public static int Load(TowerBlueprint blueprint,GameBalance balance){var a=Resolve(blueprint,balance);long load=0;foreach(var weapon in a.Weapons)if(weapon!=null)load+=weapon.Load;foreach(var m in a.Modules)load+=m.Load;return (int)Math.Min(int.MaxValue,load);}
        public static bool IsMountPart(StructureKind kind)=>PartCatalog.TryGetMount(kind,out _);
        public static float WeaponRange(TowerBlueprint blueprint,GameBalance balance,StructureKind kind)
        {
            // Construction tools have no aiming range and are not weapon catalog entries.
            if(kind==StructureKind.Wall||kind==StructureKind.Spikes)return 0;
            string weaponId=LegacyContentIds.Weapon(kind);
            return WeaponRange(blueprint,balance,weaponId,FindWeaponMount(blueprint,weaponId));
        }
        static int FindWeaponMount(TowerBlueprint blueprint,string weaponId)
        {
            if(blueprint!=null)
                foreach(var selection in blueprint.WeaponMounts)
                    if(selection.WeaponId==weaponId)return selection.SlotIndex;
            return 0;
        }
        public static float WeaponRange(TowerBlueprint blueprint,GameBalance balance,string id,int mount=0)
        {
            var assembly=blueprint==null?null:Resolve(blueprint,balance);
            var weapon=(assembly?.Content??Content(null,balance)).GetWeapon(id);
            return WeaponRange(assembly,weapon,mount);
        }
        internal static float WeaponRange(ResolvedTowerAssembly assembly,WeaponDefinition weapon,int mount)
        {float initial=weapon.Attack==AttackKind.Radial?Math.Max(weapon.MinimumRadialRange,weapon.Stats.Radius):weapon.Stats.Range;return Math.Max(0,assembly?.Modify(ModifierStat.WeaponRange,initial,mount,weapon.Attack==AttackKind.Cleanup)??initial);}
        public static bool ValidateParts(TowerBlueprint blueprint,out string reason)
        {
            reason="";if(blueprint==null)reason=UserText.Message("copy.48635af5e97f1a11");
            else if(blueprint.Id.Length==0||blueprint.Id.Length>ProfileLimits.MaximumBlueprintIdLength||blueprint.Name.Length==0||blueprint.Name.Length>ProfileLimits.MaximumBlueprintNameLength)reason=UserText.Message("copy.756c5dc332b8c7ff", ProfileLimits.MaximumBlueprintNameLength);
            else if(blueprint.LegacyAssembly&&(!Enum.IsDefined(typeof(ChassisKind),blueprint.Chassis)||(blueprint.Modules&~(TowerModules.Armor|TowerModules.Feed|TowerModules.Tracking|TowerModules.Rail))!=0||!IsMountPart(blueprint.FirstWeapon)||(blueprint.SecondWeapon.HasValue&&!IsMountPart(blueprint.SecondWeapon.Value))))reason=UserText.Message("copy.e7785425d35b8c85");
            return reason.Length==0;
        }
        public static bool Validate(TowerBlueprint blueprint,out string reason)=>Validate(blueprint,null,out reason);
        public static bool Validate(TowerBlueprint blueprint,GameBalance balance,out string reason)
        {if(!ValidateParts(blueprint,out reason))return false;try{var a=Resolve(blueprint,balance);a.Validate(Balance(balance));int excess=Load(blueprint,balance)-a.Chassis.Capacity;if(excess>0)reason=UserText.Message("copy.b6c9a1b5ca87c2e3", excess);}catch(ArgumentException ex){reason=ex.Message;}return reason.Length==0;}
        public static int Cost(TowerBlueprint blueprint,GameBalance balance,MaterialTier tier)
        {var a=Resolve(blueprint,balance);long cost=a.Chassis.BaseCost;foreach(var weapon in a.Weapons)if(weapon!=null)cost+=Math.Max(0,weapon.Stats.Cost-weapon.AssemblyCredit);foreach(var m in a.Modules)cost+=m.Cost;return(int)Math.Min(int.MaxValue,Math.Ceiling(cost*(double)Math.Max(0,balance.Material(tier).CostMultiplier)));}
        public static float Health(TowerBlueprint blueprint,GameBalance balance,MaterialTier tier){var a=Resolve(blueprint,balance);float health=a.Chassis.BaseHealth*a.Chassis.HealthMultiplier;health=a.Modify(ModifierStat.FoundationHealth,health);return Math.Max(1,health*balance.Material(tier).HealthMultiplier);}
        public static float Defense(TowerBlueprint blueprint,GameBalance balance)=>blueprint==null?0:Resolve(blueprint,balance).Modify(ModifierStat.FoundationDefense,0);
        public static int ColorCost(TowerBlueprint blueprint,GameBalance balance)=>(int)Math.Max(balance.Construction.ColorMinimum,((long)Cost(blueprint,balance,MaterialTier.Pencil)+balance.Construction.ColorCostDivisor-1)/balance.Construction.ColorCostDivisor);
        public static float BuildSeconds(TowerBlueprint blueprint,GameBalance balance,MaterialTier tier){var a=Resolve(blueprint,balance);float seconds=0;foreach(var weapon in a.Weapons)if(weapon!=null)seconds=Math.Max(seconds,weapon.Stats.BuildSeconds);seconds+=(a.Blueprint.WeaponMounts.Count-1)*a.Chassis.SecondaryBuildSeconds;seconds+=a.Chassis.BuildSeconds;foreach(var m in a.Modules)seconds+=m.BuildSeconds;return Math.Max(0,seconds*balance.Material(tier).BuildTimeMultiplier);}
        public static float FeedMultiplier(TowerBlueprint blueprint)=>FeedMultiplier(blueprint,null); public static float AcquisitionSeconds(TowerBlueprint blueprint)=>AcquisitionSeconds(blueprint,null);
        public static float FeedMultiplier(TowerBlueprint blueprint,GameBalance balance,int activeMounts)=>FeedMultiplier(Resolve(blueprint,balance),Balance(balance),activeMounts);
        internal static float FeedMultiplier(ResolvedTowerAssembly a,GameBalance balance,int activeMounts)
        {var r=balance.Construction;float module=a.Modify(ModifierStat.FeedMultiplier,1);if(r.DynamicFeed){if(activeMounts<=0)return 0;return Math.Min(r.MaxFeedMultiplier,Math.Min(module,(a.Chassis.FeedCapacity>0?a.Chassis.FeedCapacity:r.DynamicFeedCapacity)*module/activeMounts));}return Math.Min(r.MaxFeedMultiplier,(a.Blueprint.WeaponMounts.Count>1?r.DualFeedMultiplier:1)*module);}
        public static float FeedMultiplier(TowerBlueprint blueprint,GameBalance balance)=>FeedMultiplier(blueprint,balance,blueprint.WeaponMounts.Count);
        public static float AcquisitionSeconds(TowerBlueprint blueprint,GameBalance balance)=>AcquisitionSeconds(Resolve(blueprint,balance),Balance(balance));
        internal static float AcquisitionSeconds(ResolvedTowerAssembly a,GameBalance balance,int mount=0)=>a.Modify(ModifierStat.AcquisitionSeconds,balance.Construction.AcquisitionSeconds,mount,a.Weapons[mount]?.Attack==AttackKind.Cleanup);
    }
}
