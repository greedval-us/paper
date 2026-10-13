using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ModularTowerTests
    {
        [Test] public void ArmorAndUpgradeSlotsAreIndependentAndSparseThirdMountCannotBypassCapacity()
        {
            var upgrades=typeof(ChassisDefinitionInput).GetField("UpgradeSlotCount");
            var armor=typeof(ChassisDefinitionInput).GetField("ArmorSlotCount");
            Assert.That(upgrades,Is.Not.Null);Assert.That(armor,Is.Not.Null);
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);
            var chassis=input.Chassis.Find(c=>c.Id=="chassis.heavy");chassis.Capacity=100;
            upgrades.SetValue(chassis,1);armor.SetValue(chassis,1);
            balance.Content=input.CreateSnapshot();
            var page=new TowerBlueprint("slots","Slots","chassis.heavy","weapon.gun",modules:new[]{new ModuleSelection("armor","module.armor"),new ModuleSelection("feed","module.feed")},content:balance.Content);
            Assert.That(TowerCatalog.Validate(page,balance,out var reason),Is.True,reason);
            var extra=page.WithAssembly(page.ChassisId,page.FirstWeaponId,null,new[]{new ModuleSelection("armor","module.armor"),new ModuleSelection("feed","module.feed"),new ModuleSelection("tracking","module.tracking")});
            Assert.That(TowerCatalog.Validate(extra,balance,out _),Is.False);
            var setter=typeof(TowerBlueprint).GetMethod("WithWeaponMount",new[]{typeof(int),typeof(string)});
            Assert.That(setter,Is.Not.Null);
            var sparse=(TowerBlueprint)setter.Invoke(page,new object[]{2,"weapon.gun"});
            Assert.That(TowerCatalog.Validate(sparse,balance,out _),Is.False);
        }

        static ContentCatalogInput Catalog(GameBalance balance)
        {
            var input=LegacyContentCatalog.CreateInput(balance);
            input.Templates.Add(new DrawingTemplateDefinitionInput{Id="template.test",DisplayName="Test",Weight=0});
            foreach(var part in input.Chassis)part.TemplateId="template.test";
            foreach(var part in input.Weapons)part.TemplateId="template.test";
            foreach(var part in input.Modules)part.TemplateId="template.test";
            return input;
        }
        [Test] public void ThirdMountIsStoredAndSurvivesProfileRoundTrip()
        {
            var set=typeof(TowerBlueprint).GetMethod("WithWeaponMount",new[]{typeof(int),typeof(string)});
            Assert.That(set,Is.Not.Null,"Indexed mount editing must support slot 2.");
            var page=new TowerBlueprint("three","Three",ChassisKind.Heavy,StructureKind.Mortar,StructureKind.Gun);
            page=(TowerBlueprint)set.Invoke(page,new object[]{2,"weapon.mortar"});
            page=page.WithMortarFootprint(2,new MortarFootprint(2));
            var restored=new BlueprintData(page).ToBlueprint();
            Assert.That(restored.GetMortarFootprint(2).AspectRatio,Is.EqualTo(2));
            Assert.That(WorkshopProfile.CurrentVersion,Is.EqualTo(7));
        }

        [TestCase(TargetPriority.Nearest)] [TestCase(TargetPriority.Strongest)] [TestCase(TargetPriority.Siege)]
        public void DataDrivenMinimumRangeRejectsNearCandidateBeforePriorityChoice(TargetPriority priority)
        {
            var minimum=typeof(WeaponDefinitionInput).GetField("MinimumRange");
            Assert.That(minimum,Is.Not.Null,"Weapon range must include an inner boundary.");
            var balance=GameBalance.CreateDefault();
            var input=Catalog(balance);
            var mortar=input.Weapons.Find(w=>w.Id=="weapon.mortar");
            minimum.SetValue(mortar,6f);mortar.Stats.Range=18;mortar.Stats.FlightSeconds=.5f;
            balance.Content=input.CreateSnapshot();
            var page=new TowerBlueprint("m","Mortar","chassis.standard","weapon.mortar",content:balance.Content);
            var tower=new StructureState{Id=1,Cell=new Cell(10,10),Kind=StructureKind.Mortar,Blueprint=page,Health=100,IsComplete=true,Priority=priority};
            var close=new EnemyState{Id=1,Kind=EnemyKind.Gnawer,Position=new Point2(11.5f,10.5f),Health=1000,MaxHealth=1000};
            var far=new EnemyState{Id=2,Position=new Point2(17.5f,10.5f),Health=100,MaxHealth=100};
            var system=new WeaponSystem(balance);
            system.Fire(new List<StructureState>{tower},new List<EnemyState>{close,far},new List<ShotEvent>(),1,1,1,1,new RunStatistics(),new SimulationEventBuffer());
            Assert.That(system.Projectiles.Count,Is.EqualTo(1));
            Assert.That(system.Projectiles[0].To,Is.EqualTo(far.Position));
        }

        [TestCase(5.999f,false)] [TestCase(6f,true)] [TestCase(18f,true)] [TestCase(18.001f,false)]
        public void MortarRangeIncludesBothBoundaries(float distance,bool expected)
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);
            var weapon=input.Weapons.Find(w=>w.Id=="weapon.mortar");weapon.MinimumRange=6;weapon.Stats.Range=18;
            balance.Content=input.CreateSnapshot();
            var page=new TowerBlueprint("range","Range","chassis.standard","weapon.mortar",content:balance.Content);
            var tower=Tower(page);var enemy=Enemy(1,distance);
            var system=new WeaponSystem(balance);Fire(system,tower,new List<EnemyState>{enemy});
            Assert.That(system.Projectiles.Count,Is.EqualTo(expected?1:0));
            Assert.That(tower.PrimaryTargetId,Is.EqualTo(expected?1:0));
        }

        [Test] public void ThreeMountsHaveIndependentAimReloadAndFootprints()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);
            var chassis=input.Chassis.Find(c=>c.Id=="chassis.heavy");chassis.MountCount=3;chassis.Capacity=100;
            var gun=input.Weapons.Find(w=>w.Id=="weapon.gun");gun.Stats.Range=3;
            var line=input.Weapons.Find(w=>w.Id=="weapon.piercer");line.MinimumRange=3;line.Stats.Range=5;
            var mortar=input.Weapons.Find(w=>w.Id=="weapon.mortar");mortar.MinimumRange=6;mortar.Stats.Range=18;
            balance.Content=input.CreateSnapshot();
            var page=new TowerBlueprint("three","Three","chassis.heavy",new[]{new WeaponMountSelection(0,gun.Id),new WeaponMountSelection(1,line.Id),new WeaponMountSelection(2,mortar.Id,new MortarFootprint(3))},content:balance.Content);
            Assert.That(TowerCatalog.Validate(page,balance,out var reason),Is.True,reason);
            var tower=Tower(page);var enemies=new List<EnemyState>{Enemy(1,2),Enemy(2,4),Enemy(3,7)};
            var system=new WeaponSystem(balance);Fire(system,tower,enemies);
            for(int i=0;i<3;i++)
            {Assert.That(tower.GetMountState(i).TargetId,Is.EqualTo(i+1));Assert.That(tower.GetMountState(i).Aim,Is.EqualTo(enemies[i].Position));Assert.That(tower.GetMountState(i).Cooldown,Is.GreaterThan(0));}
            Assert.That(system.Projectiles.Count,Is.EqualTo(1));
            Assert.That(system.Projectiles[0].ImpactFootprint.Value.ForwardRadius,Is.GreaterThan(system.Projectiles[0].ImpactFootprint.Value.SideRadius));
            var view=new StructureView(tower);var frozen=view.GetMountAim(2);tower.GetMountState(2).Aim=new Point2(1,2);
            Assert.That(view.GetMountAim(2),Is.EqualTo(frozen));
        }

        [Test] public void IdleDeadZoneMountDoesNotReserveSharedFeedAndLosesTracking()
        {
            var balance=GameBalance.CreateDefault();balance.Construction.DynamicFeed=true;
            var input=Catalog(balance);var chassis=input.Chassis.Find(c=>c.Id=="chassis.heavy");chassis.MountCount=3;chassis.Capacity=100;chassis.FeedCapacity=1.8f;
            var mortar=input.Weapons.Find(w=>w.Id=="weapon.mortar");mortar.MinimumRange=6;mortar.Stats.Range=18;
            balance.Content=input.CreateSnapshot();
            var page=new TowerBlueprint("feed","Feed","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(1,"weapon.gun"),new WeaponMountSelection(2,"weapon.mortar")},content:balance.Content);
            var tower=Tower(page);tower.GetMountState(2).TargetId=99;tower.GetMountState(2).AcquisitionRemaining=.1f;
            Fire(new WeaponSystem(balance),tower,new List<EnemyState>{Enemy(1,2)});
            float expected=TowerCatalog.ActionInterval(page,balance,"weapon.gun",.9f,1);
            Assert.That(tower.GetMountState(0).Cooldown,Is.EqualTo(expected).Within(.00001));
            Assert.That(tower.GetMountState(2).TargetId,Is.Zero);Assert.That(tower.GetMountState(2).AcquisitionRemaining,Is.Zero);
            Assert.That(tower.GetMountState(2).ActionRemaining,Is.Zero);
        }

        [Test] public void ThirdDrawingSurvivesCopiesAndReplacingAnotherMount()
        {
            var stroke=new DrawingStroke(new[]{new DrawingPoint(-.3f,0),new DrawingPoint(.3f,.2f)});
            var drawing=new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Mount2,new[]{stroke}),new DrawingPart(DrawingBindings.Mount0,new[]{stroke})});
            var page=new TowerBlueprint("art","Art","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.mortar",new MortarFootprint(2)),new WeaponMountSelection(2,"weapon.mortar",new MortarFootprint(3))},drawing:drawing,origin:DrawingOrigin.PlayerDrawing);
            var restored=new BlueprintData(page).ToBlueprint();
            Assert.That(restored.Drawing.GeometryHash,Is.EqualTo(page.Drawing.GeometryHash));Assert.That(restored.Assessment.ConstructionHash,Is.EqualTo(page.Assessment.ConstructionHash));
            var changed=restored.WithWeaponMount(0,"weapon.gun");
            Assert.That(changed.Drawing.GetPart(DrawingBindings.Mount0).Strokes,Is.Empty);
            Assert.That(changed.Drawing.GetPart(DrawingBindings.Mount2).Strokes.Count,Is.EqualTo(1));
            Assert.That(changed.GetMortarFootprint(2).AspectRatio,Is.EqualTo(3));
            foreach(var copy in new[]{page.WithName("Rename"),page.AsVariant("variant","Variant"),page.WithAssembly("chassis.standard",page.FirstWeaponId,null,page.ModuleSelections)})
                Assert.That(copy.GetMortarFootprint(2).AspectRatio,Is.EqualTo(3));
        }

        [Test] public void DrawingEditorTracksThirdMountChangesAndRetainsUnchangedThirdLayer()
        {
            var page=new TowerBlueprint("edit","Edit","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(2,"weapon.mortar")},origin:DrawingOrigin.PlayerDrawing);
            var editor=new DrawingEditSession(page);
            Assert.That(editor.AddStroke(DrawingBindings.Mount2,new[]{new DrawingPoint(-.2f,0),new DrawingPoint(.2f,.2f)},out var reason),Is.True,reason);
            Assert.That(editor.ReplaceBlueprint(page.WithWeaponMount(0,"weapon.piercer")),Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingBindings.Mount2).Strokes.Count,Is.EqualTo(1));
            Assert.That(editor.ReplaceBlueprint(editor.Blueprint.WithWeaponMount(2,"weapon.gun")),Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingBindings.Mount2).Strokes,Is.Empty);
        }

        [Test] public void InvalidRangeAndPostModuleDeadZoneAreRejected()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=input.Weapons.Find(w=>w.Id=="weapon.mortar");
            weapon.MinimumRange=6;weapon.Stats.Range=6;Assert.That(()=>input.CreateSnapshot(),Throws.ArgumentException);
            weapon.Stats.Range=18;weapon.MinimumRange=float.NaN;Assert.That(()=>input.CreateSnapshot(),Throws.ArgumentException);
            weapon.MinimumRange=6;
            input.Modules.Add(new ModuleDefinitionInput{Id="module.reduce",Name="Reduce",TemplateId="template.test",Modifiers=new List<StatModifierInput>{new StatModifierInput{Stat=ModifierStat.WeaponRange,Scope=ModifierScope.Mount,MountIndex=2,Operation=ModifierOperation.Set,Value=6}}});
            var chassis=input.Chassis.Find(c=>c.Id=="chassis.heavy");chassis.MountCount=3;chassis.Capacity=100;balance.Content=input.CreateSnapshot();
            var page=new TowerBlueprint("invalid","Invalid","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(2,"weapon.mortar")},new[]{new ModuleSelection("reduce","module.reduce")},content:balance.Content);
            Assert.That(TowerCatalog.Validate(page,balance,out var reason),Is.False);Assert.That(reason,Is.Not.Empty);
            Assert.That(()=>new WeaponMountSelection(3,"weapon.gun"),Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        static GameBalance ModularBalance()
        {
            var balance=GameBalance.CreateDefault();balance.StartingInk=1000;balance.Construction.DynamicFeed=true;
            var input=Catalog(balance);
            foreach(var chassis in input.Chassis){chassis.UpgradeSlotCount=2;chassis.ArmorSlotCount=1;chassis.FeedCapacity=1.6f;}
            input.Chassis.Add(new ChassisDefinitionInput{Id="chassis.precision",Name="Precision",TemplateId="template.test",InitiallyUnlocked=true,MountCount=1,UpgradeSlotCount=3,ArmorSlotCount=1,Capacity=16,BaseCost=20,BaseHealth=55,BuildSeconds=.25f,SecondaryBuildSeconds=.4f,FeedCapacity=1});
            input.Chassis.Add(new ChassisDefinitionInput{Id="chassis.battery",Name="Battery",TemplateId="template.test",InitiallyUnlocked=true,MountCount=3,UpgradeSlotCount=1,ArmorSlotCount=1,Capacity=22,BaseCost=34,BaseHealth=55,BuildSeconds=.65f,SecondaryBuildSeconds=.4f,FeedCapacity=1.8f});
            balance.Content=input.CreateSnapshot();return balance;
        }

        [TestCase(0,10,89)] [TestCase(1,14,144)] [TestCase(2,16,162)] [TestCase(3,22,213)] [TestCase(4,21,256)]
        public void ApprovedCompositionsUseAllMountLoadsAndSingleMaterialRounding(int example,int load,int cost)
        {
            var balance=ModularBalance();TowerBlueprint page;
            if(example==0)page=new TowerBlueprint("pair","Pair","chassis.standard","weapon.gun","weapon.piercer",content:balance.Content);
            else if(example==1)page=new TowerBlueprint("heavy","Heavy","chassis.heavy","weapon.mortar","weapon.gun",new[]{new ModuleSelection("armor","module.armor")},content:balance.Content);
            else if(example==2)page=new TowerBlueprint("precision","Precision","chassis.precision","weapon.mortar",modules:new[]{new ModuleSelection("armor","module.armor"),new ModuleSelection("feed","module.feed"),new ModuleSelection("tracking","module.tracking"),new ModuleSelection("rail","module.rail")},content:balance.Content);
            else if(example==3)page=new TowerBlueprint("mixed","Mixed","chassis.battery",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(1,"weapon.piercer"),new WeaponMountSelection(2,"weapon.mortar")},new[]{new ModuleSelection("armor","module.armor"),new ModuleSelection("tracking","module.tracking")},content:balance.Content);
            else page=new TowerBlueprint("mortars","Mortars","chassis.battery",new[]{new WeaponMountSelection(0,"weapon.mortar"),new WeaponMountSelection(1,"weapon.mortar"),new WeaponMountSelection(2,"weapon.mortar")},content:balance.Content);
            Assert.That(TowerCatalog.Validate(page,balance,out var reason),Is.True,reason);
            Assert.That(TowerCatalog.Load(page,balance),Is.EqualTo(load));Assert.That(TowerCatalog.Cost(page,balance,MaterialTier.Pencil),Is.EqualTo(cost));
            Assert.That(TowerCatalog.Cost(page,balance,MaterialTier.Ink),Is.EqualTo((int)Math.Ceiling(cost*(double)balance.Material(MaterialTier.Ink).CostMultiplier)));
            float longest=0;foreach(var mount in page.WeaponMounts)longest=Math.Max(longest,balance.Content.GetWeapon(mount.WeaponId).Stats.BuildSeconds);
            float extras=balance.Content.GetChassis(page.ChassisId).BuildSeconds+(page.WeaponMounts.Count-1)*.4f;
            foreach(var module in page.ModuleSelections)extras+=balance.Content.GetModule(module.DefinitionId).BuildSeconds;
            Assert.That(TowerCatalog.BuildSeconds(page,balance,MaterialTier.Pencil),Is.EqualTo(longest+extras).Within(.0001));
        }

        [Test] public void RejectedSlotAndLoadAssembliesSpendNothingAndLeaveSavedPageIntact()
        {
            var balance=ModularBalance();var session=new GameSession(balance,starterDefenses:false);
            var secondUpgrade=new TowerBlueprint("bad.slots","Bad slots","chassis.battery",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(2,"weapon.gun")},new[]{new ModuleSelection("feed","module.feed"),new ModuleSelection("tracking","module.tracking")},content:balance.Content);
            var overload=new TowerBlueprint("bad.load","Bad load","chassis.battery",new[]{new WeaponMountSelection(0,"weapon.mortar"),new WeaponMountSelection(1,"weapon.mortar"),new WeaponMountSelection(2,"weapon.mortar")},new[]{new ModuleSelection("armor","module.armor")},content:balance.Content);
            int ink=session.Ink;
            foreach(var page in new[]{secondUpgrade,overload})
            {
                Assert.That(session.TryBuildBlueprint(new[]{new Cell(12,12)},page,MaterialTier.Pencil,InkColor.None,out var reason),Is.False,reason);
                Assert.That(session.Ink,Is.EqualTo(ink));Assert.That(session.Structures,Is.Empty);
                Assert.That(new BlueprintData(page).ToBlueprint().WeaponMounts.Count,Is.EqualTo(page.WeaponMounts.Count));
            }
        }

        [Test] public void ThreeMortarPayloadsCaptureIndependentShapesAndOutliveTheSource()
        {
            var balance=ModularBalance();var page=new TowerBlueprint("shapes","Shapes","chassis.battery",new[]{new WeaponMountSelection(0,"weapon.mortar"),new WeaponMountSelection(1,"weapon.mortar",new MortarFootprint(2)),new WeaponMountSelection(2,"weapon.mortar",new MortarFootprint(3))},content:balance.Content);
            var system=new WeaponSystem(balance);var tower=Tower(page);var enemies=new List<EnemyState>{Enemy(1,7)};
            Fire(system,tower,enemies);Assert.That(system.Projectiles.Count,Is.EqualTo(3));
            for(int i=0;i<3;i++)
            {var shape=system.Projectiles[i].ImpactFootprint.Value;Assert.That(shape.ForwardRadius/shape.SideRadius,Is.EqualTo(i+1).Within(.0001));}
            var stats=new RunStatistics();
            system.Fire(new List<StructureState>(),enemies,new List<ShotEvent>(),1,1,10,2,stats,new SimulationEventBuffer());
            Assert.That(system.Projectiles,Is.Empty);Assert.That(stats.Damage("weapon.mortar"),Is.GreaterThan(0));
        }

        [Test] public void OldVersionFiveAssessmentAndDrawingHashesStayExactAfterCurrentMigration()
        {
            var profile=new WorkshopProfile();var old=profile.ToData();old.Version=5;
            foreach(var page in old.Album){page.WeaponMountMetadataVersion=0;page.WeaponMounts=null;}
            var restored=WorkshopProfile.FromData(old).ToData();
            Assert.That(restored.Version,Is.EqualTo(WorkshopProfile.CurrentVersion));
            for(int i=0;i<old.Album.Length;i++)
            {
                Assert.That(restored.Album[i].Evaluation.ConstructionHash,Is.EqualTo(old.Album[i].Evaluation.ConstructionHash));
                Assert.That(restored.Album[i].Evaluation.DrawingHash,Is.EqualTo(old.Album[i].Evaluation.DrawingHash));
                Assert.That(restored.Album[i].Evaluation.Quality,Is.EqualTo(old.Album[i].Evaluation.Quality));
                Assert.That(restored.Album[i].WeaponMountMetadataVersion,Is.EqualTo(1));
            }
        }

        [Test] public void VersionSixRequiresUniqueMountMetadataAndProtectsFutureMountSchemas()
        {
            var page=new BlueprintData(new TowerBlueprint("page","Page",ChassisKind.Standard,StructureKind.Gun));
            page.WeaponMounts=null;Assert.That(()=>page.ToBlueprint(6),Throws.ArgumentException);
            page.WeaponMounts=new[]{new WeaponMountSelectionData{SlotIndex=0,WeaponId="weapon.gun"},new WeaponMountSelectionData{SlotIndex=0,WeaponId="weapon.gun"}};
            Assert.That(()=>page.ToBlueprint(6),Throws.ArgumentException);
            page.WeaponMountMetadataVersion=2;Assert.That(()=>page.ToBlueprint(6),Throws.TypeOf<NotSupportedException>());
        }

        [Test] public void LegacyIntervalAdapterFindsUniqueWeaponOnThirdMount()
        {
            var balance=ModularBalance();var input=Catalog(balance);
            input.Chassis.Add(new ChassisDefinitionInput{Id="chassis.adapter",Name="Adapter",TemplateId="template.test",Capacity=100,BaseHealth=55,MountCount=3});
            input.Modules.Add(new ModuleDefinitionInput{Id="module.adapter",Name="Adapter",TemplateId="template.test",Modifiers=new List<StatModifierInput>{new StatModifierInput{Stat=ModifierStat.ActionInterval,Operation=ModifierOperation.Multiply,Scope=ModifierScope.Mount,MountIndex=2,Value=2}}});
            balance.Content=input.CreateSnapshot();
            var page=new TowerBlueprint("adapter","Adapter","chassis.adapter",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(2,"weapon.piercer")},new[]{new ModuleSelection("adapter","module.adapter")},content:balance.Content);
            Assert.That(TowerCatalog.ActionInterval(page,balance,StructureKind.Piercer,1,1),Is.EqualTo(TowerCatalog.ActionInterval(page,balance,"weapon.piercer",1,1,2)));
        }

        [Test] public void NondynamicFeedRecognizesSparseThirdMountAsASecondWorkingBarrel()
        {
            var balance=ModularBalance();balance.Construction.DynamicFeed=false;
            var page=new TowerBlueprint("sparse.feed","Sparse feed","chassis.battery",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(2,"weapon.gun")},content:balance.Content);
            Assert.That(TowerCatalog.FeedMultiplier(page,balance),Is.EqualTo(balance.Construction.DualFeedMultiplier));
        }

        [Test] public void LegacyThirdDrawingMountUsesTheSameAssessmentWeightAsOtherWeapons()
        {
            var page=new TowerBlueprint("weights","Weights","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(1,"weapon.gun"),new WeaponMountSelection(2,"weapon.mortar")});
            var templates=DrawingTemplateCatalog.Create(page);float first=0,third=0;
            foreach(var template in templates){if(template.BindingId==DrawingBindings.Mount0)first=template.Weight;if(template.BindingId==DrawingBindings.Mount2)third=template.Weight;}
            Assert.That(third,Is.EqualTo(first));
        }

        static StructureState Tower(TowerBlueprint page)=>new StructureState{Id=1,Cell=new Cell(10,10),Kind=page.FirstWeapon,Blueprint=page,Health=100,MaxHealth=100,IsComplete=true};
        static EnemyState Enemy(int id,float distance)=>new EnemyState{Id=id,Position=new Point2(10.5f+distance,10.5f),Health=1000,MaxHealth=1000};
        static void Fire(WeaponSystem system,StructureState tower,List<EnemyState> enemies)
            =>system.Fire(new List<StructureState>{tower},enemies,new List<ShotEvent>(),1,1,1,1,new RunStatistics(),new SimulationEventBuffer());
    }
}
