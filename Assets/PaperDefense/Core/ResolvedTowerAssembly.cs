using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    internal sealed class ResolvedTowerAssembly
    {
        internal readonly ContentCatalogSnapshot Content;
        internal readonly TowerBlueprint Blueprint;
        internal readonly ChassisDefinition Chassis;
        internal readonly WeaponDefinition First,Second;
        internal readonly WeaponDefinition[] Weapons=new WeaponDefinition[TowerBlueprint.MaximumWeaponMounts];
        internal readonly IReadOnlyList<ModuleDefinition> Modules;
        internal ResolvedTowerAssembly(TowerBlueprint blueprint,ContentCatalogSnapshot content)
        {Blueprint=blueprint??throw new ArgumentNullException(nameof(blueprint));Content=content;Chassis=content.GetChassis(blueprint.ChassisId);foreach(var mount in blueprint.WeaponMounts)Weapons[mount.SlotIndex]=content.GetWeapon(mount.WeaponId);First=Weapons[0];Second=Weapons[1];var list=new List<ModuleDefinition>();foreach(var item in blueprint.ModuleSelections)list.Add(content.GetModule(item.DefinitionId));Modules=list.AsReadOnly();}
        internal void Validate(GameBalance balance)
        {
            int genericBindings=0;foreach(var selection in Blueprint.ModuleSelections)if(!DrawingBindings.TryLegacy(DrawingBindings.Module(selection.InstanceId),out _))genericBindings++;
            if(Blueprint.GetWeaponId(2)!=null)genericBindings++;
            if(genericBindings>TowerCatalog.MaximumGenericModuleBindings)throw new ArgumentException(UserText.Message("copy.e6cf169bb9c337b8", TowerCatalog.MaximumGenericModuleBindings));
            if(!Chassis.Enabled)throw new ArgumentException(UserText.Message("copy.0dc61ba711cc3b92"));
            foreach(var mount in Blueprint.WeaponMounts)
            {
                var weapon=Weapons[mount.SlotIndex];
                if(!weapon.Enabled)throw new ArgumentException(UserText.Message("copy.0dc61ba711cc3b92"));
                if(mount.SlotIndex>=Chassis.MountCount)throw new ArgumentException(UserText.Message("copy.de42044c131147ed", Chassis.Id, (mount.SlotIndex+1)));
                if(!weapon.SupportsFootprint&&mount.Footprint.AspectRatio!=1)throw new ArgumentException(UserText.Message("copy.ec26e9810b8fe03e"));
                if(mount.Footprint.AspectRatio>weapon.MaximumFootprintAspectRatio)throw new ArgumentException(UserText.Message("copy.a630b0b26793f9fd"));
            }
            var selected=new HashSet<string>(StringComparer.Ordinal);var groups=new HashSet<string>(StringComparer.Ordinal);
            int armor=0,upgrades=0;
            foreach(var module in Modules)
            {
                if(!module.Enabled)throw new ArgumentException(UserText.Message("copy.2d4668b344df24fc", module.Id));
                if(!selected.Add(module.Id)&&!module.Repeatable)throw new ArgumentException(UserText.Message("copy.c8e14675d52b9212", module.Id));
                if(module.ExclusiveGroup.Length>0&&!groups.Add(module.ExclusiveGroup))throw new ArgumentException(UserText.Message("copy.7b1c9d9825c95430", module.Id, module.ExclusiveGroup));
                if(module.AllowedChassisIds.Count>0&&!Contains(module.AllowedChassisIds,Chassis.Id))throw new ArgumentException(UserText.Message("copy.42e05910beb498f2", module.Id));
                if(module.AllowedAttacks.Count>0)foreach(var weapon in Weapons)if(weapon!=null&&!Contains(module.AllowedAttacks,weapon.Attack))throw new ArgumentException(UserText.Message("copy.eef9674c9ff3fe8c", module.Id));
                if(module.SlotKind==ModuleSlotKind.Armor)armor++;else upgrades++;
            }
            if(armor>Chassis.ArmorSlotCount)throw new ArgumentException(UserText.Message("copy.5f9d9c8203147ff4", Chassis.Id, armor, Chassis.ArmorSlotCount));
            if(upgrades>Chassis.UpgradeSlotCount)throw new ArgumentException(UserText.Message("copy.1c0cb4cdec20fb4d", Chassis.Id, upgrades, Chassis.UpgradeSlotCount));
            ValidateValue(ModifierStat.FoundationHealth,Chassis.BaseHealth*Chassis.HealthMultiplier);
            ValidateValue(ModifierStat.FoundationDefense,0);
            ValidateValue(ModifierStat.FeedMultiplier,1);
            for(int mount=0;mount<Weapons.Length;mount++)if(Weapons[mount]!=null)ValidateWeapon(Weapons[mount],mount,balance);
        }
        void ValidateWeapon(WeaponDefinition weapon,int mount,GameBalance balance)
        {
            bool support=weapon.Attack==AttackKind.Cleanup;
            ValidateValue(ModifierStat.WeaponRange,weapon.Attack==AttackKind.Radial?Math.Max(weapon.MinimumRadialRange,weapon.Stats.Radius):weapon.Stats.Range,mount,support);
            if(weapon.MinimumRange>0&&TowerCatalog.WeaponRange(this,weapon,mount)<=weapon.MinimumRange)throw new ArgumentException(UserText.Message("copy.c51135965fbecb05", weapon.Id));
            ValidateValue(ModifierStat.ActionInterval,Math.Max(balance.Combat.MinimumActionInterval,weapon.Stats.ShotInterval),mount,support);
            ValidateValue(ModifierStat.AcquisitionSeconds,balance.Construction.AcquisitionSeconds,mount,support);
        }
        void ValidateValue(ModifierStat stat,float initial,int mount=0,bool support=false)
        {float value=Modify(stat,initial,mount,support);if(float.IsNaN(value)||float.IsInfinity(value)||value<0||(stat==ModifierStat.ActionInterval&&value==0))throw new ArgumentException(UserText.Message("copy.d099e231ee9f7ecd", stat));}
        static bool Contains<T>(IReadOnlyList<T> list,T item){foreach(var value in list)if(EqualityComparer<T>.Default.Equals(value,item))return true;return false;}
        internal float Modify(ModifierStat stat,float value,int mount=0,bool support=false)
        {foreach(var module in Modules)foreach(var modifier in module.Modifiers){if(modifier.Stat!=stat)continue;if(modifier.Scope==ModifierScope.Mount&&modifier.MountIndex!=mount)continue;if(modifier.Scope==ModifierScope.Support&&!support)continue;value=modifier.Apply(value);}return value;}
    }
}
