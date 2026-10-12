using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Compatibility for old standalone callers. Production loads an explicitly referenced content asset.</summary>
    public static class LegacyContentCatalog
    {
        public static ContentCatalogSnapshot Create(GameBalance balance) => new ContentCatalogSnapshot(CreateInput(balance),true);
        public static ContentCatalogInput CreateInput(GameBalance balance)
        {
            if(balance==null)throw new ArgumentNullException(nameof(balance));var rules=balance.Construction;
            var input=new ContentCatalogInput();int order=0;
            foreach(var part in PartCatalog.All)
            {
                if(part.Chassis.HasValue)
                {
                    bool heavy=part.Chassis.Value==ChassisKind.Heavy;
                    var d=new ChassisDefinitionInput{Capacity=heavy?rules.HeavyCapacity:rules.StandardCapacity,BaseCost=heavy?rules.HeavyBaseCost:rules.StandardBaseCost,BaseHealth=rules.FoundationHealth,HealthMultiplier=heavy?rules.HeavyHealthMultiplier:1,BuildSeconds=heavy?rules.HeavyBuildSeconds:0,SecondaryBuildSeconds=rules.SecondaryBuildSeconds,LegacyKind=part.Chassis.Value,PrimaryMountPivotY=.04f,PrimaryDualMountPivotX=-.17f,PrimaryDualMountPivotY=.04f,SecondaryMountPivotX=.17f,SecondaryMountPivotY=.04f};
                    Metadata(d,part,order++,rules);input.Chassis.Add(d);
                }
                else if(part.MountKind.HasValue)
                {
                    StructureKind kind=part.MountKind.Value;
                    var d=new WeaponDefinitionInput{Attack=Attack(kind),Stats=WeaponDefinition.CopyStats(balance.Structure(kind)),Load=rules.WeaponLoad(kind),AssemblyCredit=rules.WeaponAssemblyCredit,LegacyKind=kind,MinimumLineRadius=kind==StructureKind.Piercer?.2f:0,MinimumRadialRange=kind==StructureKind.Spikes?.5f:0};
                    if(kind==StructureKind.Stapler){d.EffectIds.Add("effect.stapler.slow");input.Effects.Add(new EffectDefinitionInput{Id="effect.stapler.slow",Kind=EffectKind.Slow,Duration=d.Stats.SlowDuration,Strength=d.Stats.SlowSpeedMultiplier});}
                    Metadata(d,part,order++,rules);input.Weapons.Add(d);
                }
                else
                {
                    var d=new ModuleDefinitionInput{Load=rules.ModuleLoad(part.Module),LegacyModule=part.Module};
                    switch(part.Module)
                    {
                        case TowerModules.Armor:d.Cost=rules.ArmorCost;d.BuildSeconds=rules.ArmorBuildSeconds;d.Modifiers.Add(Modifier(ModifierStat.FoundationHealth,ModifierOperation.Multiply,ModifierScope.Foundation,rules.ArmorHealthMultiplier));d.Modifiers.Add(Modifier(ModifierStat.FoundationDefense,ModifierOperation.Set,ModifierScope.Foundation,rules.ArmorDefense));break;
                        case TowerModules.Feed:d.Cost=rules.FeedCost;d.BuildSeconds=rules.FeedBuildSeconds;d.Modifiers.Add(Modifier(ModifierStat.FeedMultiplier,ModifierOperation.Multiply,ModifierScope.AllWeapons,rules.FeedModuleMultiplier));break;
                        case TowerModules.Tracking:d.Cost=rules.TrackingCost;d.BuildSeconds=rules.TrackingBuildSeconds;d.Modifiers.Add(Modifier(ModifierStat.AcquisitionSeconds,ModifierOperation.Set,ModifierScope.AllWeapons,rules.TrackingAcquisitionSeconds));break;
                        case TowerModules.Rail:d.Cost=rules.RailCost;d.BuildSeconds=rules.RailBuildSeconds;d.Modifiers.Add(Modifier(ModifierStat.WeaponRange,ModifierOperation.Multiply,ModifierScope.AllWeapons,rules.RailRangeMultiplier));d.Modifiers.Add(Modifier(ModifierStat.ActionInterval,ModifierOperation.Multiply,ModifierScope.AllWeapons,rules.RailIntervalMultiplier));break;
                    }
                    Metadata(d,part,order++,rules);input.Modules.Add(d);
                }
            }
            input.RedEffectId="effect.color.red";input.BlueEffectId="effect.color.blue";input.GreenEffectId="effect.color.green";
            input.Effects.Add(new EffectDefinitionInput{Id=input.RedEffectId,Kind=EffectKind.DamageOverTime,Duration=balance.Effects.RedDuration,Strength=balance.Effects.RedDamagePerTick,TickSeconds=balance.Effects.RedTickSeconds,IgnoresDefense=balance.Effects.RedIgnoresDefense});
            input.Effects.Add(new EffectDefinitionInput{Id=input.BlueEffectId,Kind=EffectKind.Slow,Duration=balance.Effects.BlueDuration,Strength=balance.Effects.BlueSpeedMultiplier});
            input.Effects.Add(new EffectDefinitionInput{Id=input.GreenEffectId,Kind=EffectKind.DefenseReduction,Duration=balance.Effects.GreenDuration,Strength=balance.Effects.GreenDefenseReduction});
            input.StartingPages.Add(new StartingBlueprintDefinitionInput{Id="starter.gun",Name="Пулемёт",ChassisId="chassis.standard",FirstWeaponId="weapon.gun",Slot=0});
            input.StartingPages.Add(new StartingBlueprintDefinitionInput{Id="starter.double",Name="Двойной пулемёт",ChassisId="chassis.standard",FirstWeaponId="weapon.gun",SecondWeaponId="weapon.gun",Slot=1});
            input.StartingPages.Add(new StartingBlueprintDefinitionInput{Id="starter.piercer",Name="Пробивающая пушка",ChassisId="chassis.standard",FirstWeaponId="weapon.piercer",Slot=2});
            var mortar=new StartingBlueprintDefinitionInput{Id="starter.mortar",Name="Защищённая мортира",ChassisId="chassis.standard",FirstWeaponId="weapon.mortar",Slot=3};
            mortar.Modules.Add(new StartingModuleDefinitionInput{InstanceId="armor",DefinitionId="module.armor"});input.StartingPages.Add(mortar);
            return input;
        }
        public static AttackKind Attack(StructureKind kind){switch(kind){case StructureKind.Piercer:return AttackKind.Line;case StructureKind.Mortar:return AttackKind.AreaProjectile;case StructureKind.Fan:return AttackKind.Cone;case StructureKind.Ricochet:return AttackKind.Ricochet;case StructureKind.Spikes:return AttackKind.Radial;case StructureKind.Blotter:return AttackKind.Cleanup;default:return AttackKind.Single;}}
        static StatModifierInput Modifier(ModifierStat stat,ModifierOperation op,ModifierScope scope,float value)=>new StatModifierInput{Stat=stat,Operation=op,Scope=scope,Value=value};
        static void Metadata(PartDefinitionInput d,WorkshopPartDefinition p,int order,TowerConstructionRules rules){d.Id=p.Id;d.Name=p.Name;d.Description=p.Description;d.ButtonLabel=p.ButtonLabel;d.UnlockLabel=p.UnlockLabel;d.InitiallyUnlocked=p.InitiallyUnlocked;d.UnlockGold=PartCatalog.GetUnlockGold(p,rules);d.Order=order;d.VisualStyle=DrawingVisualStyle.Legacy;}
    }
}
