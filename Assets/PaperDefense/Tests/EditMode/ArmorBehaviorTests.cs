using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ArmorBehaviorTests
    {
        static TowerBlueprint Page()=>new TowerBlueprint("armor","Armor",ChassisKind.Standard,StructureKind.Gun,modules:TowerModules.Armor);
        [Test] public void FrontProtectionUsesIncomingPositionAndCapsWithoutFreeRearDefense()
        {
            var rules=GameBalance.CreateDefault();rules.Construction.ArmorDefense=.25f;rules.Effects.MaximumDefense=.6f;
            var page=Page().WithReinforcement(new ArmorDistribution(new[]{12,0,0,0}));
            var tower=new StructureState{Cell=new Cell(10,10),Blueprint=page,PrimaryAim=new Point2(0,-100)};
            Assert.That(DamageRules.StructureDamage(100,tower,rules,new Point2(10.5f,20)),Is.EqualTo(40).Within(.001));
            Assert.That(DamageRules.StructureDamage(100,tower,rules,new Point2(10.5f,0)),Is.EqualTo(100));
            Assert.That(DamageRules.StructureDamage(100,tower,rules,new Point2(10.5f,10.5f)),Is.EqualTo(100));
            var uniform=new StructureState{Cell=tower.Cell,Blueprint=Page().WithReinforcement(ArmorDistribution.Uniform())};
            Assert.That(DamageRules.StructureDamage(100,uniform,rules,new Point2(0,10.5f)),Is.EqualTo(75));
            Assert.That(TowerCatalog.Cost(page,rules,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.Cost(Page(),rules,MaterialTier.Pencil)));
            Assert.That(TowerCatalog.Health(page,rules,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.Health(Page(),rules,MaterialTier.Pencil)));
        }
        [Test] public void CopiesDrawingWeaponAndChassisChangesKeepDistributionUntilArmorIsRemoved()
        {
            var page=Page().WithReinforcement(new ArmorDistribution(new[]{6,3,0,3},2));
            Assert.That(page.WithName("Copy").AsVariant("variant","Variant").WithContent(GameBalance.CreateDefault().ResolveContent()).Reinforcement,Is.SameAs(page.Reinforcement));
            Assert.That(page.WithWeaponMount(0,"weapon.fan").Reinforcement,Is.SameAs(page.Reinforcement));
            Assert.That(page.WithFreeformDrawing(DrawingSnapshot.Empty).Reinforcement,Is.SameAs(page.Reinforcement));
            Assert.That(page.WithAssembly(ChassisKind.Heavy,StructureKind.Gun,null,TowerModules.Armor|TowerModules.Feed).Reinforcement,Is.SameAs(page.Reinforcement));
            Assert.That(page.WithAssembly(ChassisKind.Standard,StructureKind.Gun,null,TowerModules.None).Reinforcement,Is.Null);
        }
        [Test] public void BlueprintRoundtripPreservesDistributionAndLegacyPagesRemainUnchanged()
        {
            var page=Page().WithReinforcement(new ArmorDistribution(new[]{6,3,0,3},2));
            var restored=new BlueprintData(page).ToBlueprint();
            Assert.That(restored.Reinforcement,Is.Not.Null);
            Assert.That(restored.Reinforcement.Points,Is.EqualTo(page.Reinforcement.Points));
            Assert.That(restored.Reinforcement.FacingQuarterTurns,Is.EqualTo(2));
            Assert.That(new BlueprintData(Page()).ToBlueprint().Reinforcement,Is.Null);
        }
        [Test] public void CatalogCopiesOwnGenericArmorLayersAndFutureDataIsNotDiscarded()
        {
            var input=LegacyContentCatalog.CreateInput(GameBalance.CreateDefault());
            var module=input.Modules.Find(m=>m.Id=="module.armor");module.Id="module.custom-armor";module.LegacyModule=TowerModules.None;
            foreach(var existing in input.StartingPages)foreach(var part in existing.Modules)if(part.DefinitionId=="module.armor")part.DefinitionId=module.Id;
            var page=input.StartingPages[0];page.Modules.Add(new StartingModuleDefinitionInput{InstanceId="custom",DefinitionId=module.Id});
            page.Reinforcement=new ArmorDistributionData(ArmorDistribution.Uniform(2));
            var copied=input.CreateCopy();page.Reinforcement.Points[0]=0;
            var catalog=new ContentCatalogSnapshot(copied,true);var restored=catalog.StartupBlueprints[0];
            Assert.That(restored.HasArmor,Is.True);Assert.That(restored.Reinforcement.Points[0],Is.EqualTo(3));
            Assert.That(restored.WithName("Copy").Reinforcement.FacingQuarterTurns,Is.EqualTo(2));
            var detached=new BlueprintData(restored).ToBlueprint();
            Assert.That(detached.WithWeaponMount(0,"weapon.fan").Reinforcement,Is.Not.Null,"A weapon edit before attaching content must not discard custom armor.");
            var serialized=new BlueprintData(restored);serialized.Reinforcement.Version=99;
            Assert.Throws<NotSupportedException>(()=>serialized.ToBlueprint());
            Assert.That(serialized.Reinforcement.CreateCopy().Version,Is.EqualTo(99));
            Assert.Throws<ArgumentException>(()=>new ArmorDistributionData{Version=0,Points=new[]{3,3,3,3}}.ToDistribution());
        }
    }
}
