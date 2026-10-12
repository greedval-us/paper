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
        internal readonly IReadOnlyList<ModuleDefinition> Modules;
        internal ResolvedTowerAssembly(TowerBlueprint blueprint,ContentCatalogSnapshot content)
        {Blueprint=blueprint??throw new ArgumentNullException(nameof(blueprint));Content=content;Chassis=content.GetChassis(blueprint.ChassisId);First=content.GetWeapon(blueprint.FirstWeaponId);Second=blueprint.SecondWeaponId==null?null:content.GetWeapon(blueprint.SecondWeaponId);var list=new List<ModuleDefinition>();foreach(var item in blueprint.ModuleSelections)list.Add(content.GetModule(item.DefinitionId));Modules=list.AsReadOnly();}
        internal void Validate(GameBalance balance)
        {
            int genericBindings=0;foreach(var selection in Blueprint.ModuleSelections)if(!DrawingBindings.TryLegacy(DrawingBindings.Module(selection.InstanceId),out _))genericBindings++;
            if(genericBindings>TowerCatalog.MaximumGenericModuleBindings)throw new ArgumentException("blueprint.modules: превышен технический предел "+TowerCatalog.MaximumGenericModuleBindings+" дополнительных привязок рисунка");
            if(!Chassis.Enabled||!First.Enabled||(Second!=null&&!Second.Enabled))throw new ArgumentException("Сборка содержит отключённую запись каталога.");
            if(Second!=null&&Chassis.MountCount<2)throw new ArgumentException(Chassis.Id + ": второе крепление недоступно");
            if((!First.SupportsFootprint&&Blueprint.FirstFootprint.AspectRatio!=1)||((Second==null||!Second.SupportsFootprint)&&Blueprint.SecondFootprint.AspectRatio!=1))throw new ArgumentException("Форма области доступна только площадному снаряду.");
            if(Blueprint.FirstFootprint.AspectRatio>First.MaximumFootprintAspectRatio||(Second!=null&&Blueprint.SecondFootprint.AspectRatio>Second.MaximumFootprintAspectRatio))throw new ArgumentException("Геометрия области превышает предел оружия.");
            var selected=new HashSet<string>(StringComparer.Ordinal);var groups=new HashSet<string>(StringComparer.Ordinal);
            foreach(var module in Modules)
            {
                if(!module.Enabled)throw new ArgumentException(module.Id + ": запись отключена");
                if(!selected.Add(module.Id)&&!module.Repeatable)throw new ArgumentException(module.Id + ": повторы запрещены");
                if(module.ExclusiveGroup.Length>0&&!groups.Add(module.ExclusiveGroup))throw new ArgumentException(module.Id + ": конфликт оборудования " + module.ExclusiveGroup);
                if(module.AllowedChassisIds.Count>0&&!Contains(module.AllowedChassisIds,Chassis.Id))throw new ArgumentException(module.Id + ": несовместимое основание");
                if(module.AllowedAttacks.Count>0&&(!Contains(module.AllowedAttacks,First.Attack)||(Second!=null&&!Contains(module.AllowedAttacks,Second.Attack))))throw new ArgumentException(module.Id + ": несовместимое оружие");
            }
            ValidateValue(ModifierStat.FoundationHealth,Chassis.BaseHealth*Chassis.HealthMultiplier);
            ValidateValue(ModifierStat.FoundationDefense,0);
            ValidateValue(ModifierStat.FeedMultiplier,1);
            ValidateWeapon(First,0,balance);if(Second!=null)ValidateWeapon(Second,1,balance);
        }
        void ValidateWeapon(WeaponDefinition weapon,int mount,GameBalance balance)
        {
            bool support=weapon.Attack==AttackKind.Cleanup;
            ValidateValue(ModifierStat.WeaponRange,weapon.Attack==AttackKind.Radial?Math.Max(weapon.MinimumRadialRange,weapon.Stats.Radius):weapon.Stats.Range,mount,support);
            ValidateValue(ModifierStat.ActionInterval,Math.Max(balance.Combat.MinimumActionInterval,weapon.Stats.ShotInterval),mount,support);
            ValidateValue(ModifierStat.AcquisitionSeconds,balance.Construction.AcquisitionSeconds,mount,support);
        }
        void ValidateValue(ModifierStat stat,float initial,int mount=0,bool support=false)
        {float value=Modify(stat,initial,mount,support);if(float.IsNaN(value)||float.IsInfinity(value)||value<0||(stat==ModifierStat.ActionInterval&&value==0))throw new ArgumentException("modules." + stat + ": невалидный итог модификаторов");}
        static bool Contains<T>(IReadOnlyList<T> list,T item){foreach(var value in list)if(EqualityComparer<T>.Default.Equals(value,item))return true;return false;}
        internal float Modify(ModifierStat stat,float value,int mount=0,bool support=false)
        {foreach(var module in Modules)foreach(var modifier in module.Modifiers){if(modifier.Stat!=stat)continue;if(modifier.Scope==ModifierScope.Mount&&modifier.MountIndex!=mount)continue;if(modifier.Scope==ModifierScope.Support&&!support)continue;value=modifier.Apply(value);}return value;}
    }
}
