using System.Collections.Generic;

namespace PaperDefense.Core
{
    internal static class ContentInputCopy
    {
        internal static ContentCatalogInput Copy(ContentCatalogInput source)
        {
            var result=new ContentCatalogInput{RedEffectId=source.RedEffectId,BlueEffectId=source.BlueEffectId,GreenEffectId=source.GreenEffectId,
                Assessment=source.Assessment==null?null:new DrawingAssessmentSettingsInput{Revision=source.Assessment.Revision,Tolerance=source.Assessment.Tolerance,CompletionThreshold=source.Assessment.CompletionThreshold,PrecisionThreshold=source.Assessment.PrecisionThreshold}};
            if(source.Chassis==null)result.Chassis=null;else foreach(var d in source.Chassis)result.Chassis.Add(d==null?null:Meta(d,new ChassisDefinitionInput{Capacity=d.Capacity,BaseCost=d.BaseCost,MountCount=d.MountCount,UpgradeSlotCount=d.UpgradeSlotCount,ArmorSlotCount=d.ArmorSlotCount,FeedCapacity=d.FeedCapacity,BaseHealth=d.BaseHealth,HealthMultiplier=d.HealthMultiplier,BuildSeconds=d.BuildSeconds,SecondaryBuildSeconds=d.SecondaryBuildSeconds,PrimaryMountPivotX=d.PrimaryMountPivotX,PrimaryMountPivotY=d.PrimaryMountPivotY,PrimaryDualMountPivotX=d.PrimaryDualMountPivotX,PrimaryDualMountPivotY=d.PrimaryDualMountPivotY,SecondaryMountPivotX=d.SecondaryMountPivotX,SecondaryMountPivotY=d.SecondaryMountPivotY,LegacyKind=d.LegacyKind,MountPivots=d.MountPivots==null?null:d.MountPivots.ConvertAll(p=>p==null?null:new MountPivotInput{SlotIndex=p.SlotIndex,X=p.X,Y=p.Y})}));
            if(source.Weapons==null)result.Weapons=null;else foreach(var d in source.Weapons)result.Weapons.Add(d==null?null:Meta(d,new WeaponDefinitionInput{Attack=d.Attack,Stats=d.Stats==null?null:WeaponDefinition.CopyStats(d.Stats),Load=d.Load,AssemblyCredit=d.AssemblyCredit,MinimumLineRadius=d.MinimumLineRadius,MinimumRadialRange=d.MinimumRadialRange,MinimumRange=d.MinimumRange,MaximumFootprintAspectRatio=d.MaximumFootprintAspectRatio,EffectIds=d.EffectIds==null?null:new List<string>(d.EffectIds),LegacyKind=d.LegacyKind}));
            if(source.Modules==null)result.Modules=null;else foreach(var d in source.Modules)
            {
                if(d==null){result.Modules.Add(null);continue;}
                var clone=Meta(d,new ModuleDefinitionInput{SlotKind=d.SlotKind,Load=d.Load,Cost=d.Cost,BuildSeconds=d.BuildSeconds,Repeatable=d.Repeatable,ExclusiveGroup=d.ExclusiveGroup,AllowedChassisIds=d.AllowedChassisIds==null?null:new List<string>(d.AllowedChassisIds),AllowedAttacks=d.AllowedAttacks==null?null:new List<AttackKind>(d.AllowedAttacks),LegacyModule=d.LegacyModule});
                if(d.Modifiers==null)clone.Modifiers=null;else foreach(var m in d.Modifiers)clone.Modifiers.Add(m==null?null:new StatModifierInput{Stat=m.Stat,Operation=m.Operation,Scope=m.Scope,MountIndex=m.MountIndex,Value=m.Value});result.Modules.Add(clone);
            }
            if(source.Effects==null)result.Effects=null;else foreach(var d in source.Effects)result.Effects.Add(d==null?null:new EffectDefinitionInput{Id=d.Id,Kind=d.Kind,Duration=d.Duration,Strength=d.Strength,TickSeconds=d.TickSeconds,IgnoresDefense=d.IgnoresDefense});
            if(source.StartingPages==null)result.StartingPages=null;else foreach(var d in source.StartingPages)
            {
                if(d==null){result.StartingPages.Add(null);continue;}
                var clone=new StartingBlueprintDefinitionInput{Id=d.Id,Name=d.Name,ChassisId=d.ChassisId,FirstWeaponId=d.FirstWeaponId,SecondWeaponId=d.SecondWeaponId,Slot=d.Slot};
                clone.WeaponMounts=d.WeaponMounts==null?null:d.WeaponMounts.ConvertAll(m=>m==null?null:new WeaponMountSelectionData{SlotIndex=m.SlotIndex,WeaponId=m.WeaponId,FootprintAspect=m.FootprintAspect});
                if(d.Modules==null)clone.Modules=null;else foreach(var m in d.Modules)clone.Modules.Add(m==null?null:new StartingModuleDefinitionInput{InstanceId=m.InstanceId,DefinitionId=m.DefinitionId});result.StartingPages.Add(clone);
            }
            if(source.Templates==null)result.Templates=null;else foreach(var d in source.Templates)
            {
                if(d==null){result.Templates.Add(null);continue;}
                var clone=new DrawingTemplateDefinitionInput{Id=d.Id,Revision=d.Revision,DisplayName=d.DisplayName,PivotX=d.PivotX,PivotY=d.PivotY,Weight=d.Weight};
                if(d.Contours==null)clone.Contours=null;else foreach(var contour in d.Contours)
                {
                    if(contour==null){clone.Contours.Add(null);continue;}
                    var c=new DrawingContourInput();if(contour.Points==null)c.Points=null;else foreach(var p in contour.Points)c.Points.Add(p==null?null:new DrawingCoordinateInput(p.X,p.Y));clone.Contours.Add(c);
                }
                result.Templates.Add(clone);
            }
            return result;
        }
        static T Meta<T>(PartDefinitionInput source,T target) where T:PartDefinitionInput
        {target.Id=source.Id;target.Name=source.Name;target.Description=source.Description;target.ButtonLabel=source.ButtonLabel;target.UnlockLabel=source.UnlockLabel;target.TemplateId=source.TemplateId;target.Enabled=source.Enabled;target.InitiallyUnlocked=source.InitiallyUnlocked;target.Order=source.Order;target.UnlockGold=source.UnlockGold;target.VisualStyle=source.VisualStyle;return target;}
    }
}
