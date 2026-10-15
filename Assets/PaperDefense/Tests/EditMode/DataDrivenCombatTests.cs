using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DataDrivenCombatTests
    {
        static ContentCatalogInput Catalog(GameBalance balance)
        {
            var input=LegacyContentCatalog.CreateInput(balance);
            input.Templates.Add(new DrawingTemplateDefinitionInput{Id="template.test",DisplayName="Test",Weight=0});
            foreach(var d in input.Chassis)d.TemplateId="template.test";
            foreach(var d in input.Weapons)d.TemplateId="template.test";
            foreach(var d in input.Modules)d.TemplateId="template.test";
            return input;
        }
        static WeaponDefinitionInput AddWeapon(ContentCatalogInput input,string id,AttackKind attack)
        {
            var weapon=new WeaponDefinitionInput{Id=id,Name=id,TemplateId="template.test",Attack=attack,Load=1,AssemblyCredit=0,
                Stats=new StructureDefinition{Cost=17,MaxHealth=55,BuildSeconds=0,Range=10,Damage=11,ShotInterval=.5f,Radius=.3f,MaxPierce=3,FlightSeconds=.4f,ConeAngleDegrees=90,MaxTargets=3,JumpRange=3,SlowSpeedMultiplier=1}};
            input.Weapons.Add(weapon);return weapon;
        }
        static TowerBlueprint Blueprint(GameBalance balance,string weaponId,string second=null,IEnumerable<ModuleSelection> modules=null,string chassis="chassis.standard")
            =>new TowerBlueprint("test","Test",chassis,weaponId,second,modules,content:balance.Content);
        static StructureState Tower(TowerBlueprint blueprint)=>new StructureState{Id=1,Cell=new Cell(10,10),Blueprint=blueprint,Kind=blueprint.FirstWeapon,IsComplete=true,Health=55,MaxHealth=55};
        static EnemyState Enemy(int id,float x,float y)=>new EnemyState{Id=id,Position=new Point2(x,y),Health=100,MaxHealth=100};
        static void Fire(WeaponSystem system,StructureState tower,List<EnemyState> enemies,RunStatistics statistics,SimulationEventBuffer events,float delta=.25f,long tick=1)
            =>system.Fire(new List<StructureState>{tower},enemies,new List<ShotEvent>(),1,1,delta,tick,statistics,events);

        [TestCase(AttackKind.Line)] [TestCase(AttackKind.Cone)] [TestCase(AttackKind.Ricochet)]
        [TestCase(AttackKind.Radial)] [TestCase(AttackKind.AreaProjectile)]
        public void VisualEventsCaptureTheRealAttackWithoutALegacyAlias(AttackKind attack)
        {
            var balance = GameBalance.CreateDefault(); var input = Catalog(balance);
            var weapon = AddWeapon(input, "weapon.test.visual", attack);
            if (attack == AttackKind.Radial) weapon.Stats.Radius = 4;
            balance.Content = input.CreateSnapshot();
            var events = new SimulationEventBuffer(); var system = new WeaponSystem(balance);
            var enemies = new List<EnemyState> { Enemy(1, 12, 10.5f) };
            Fire(system, Tower(Blueprint(balance, weapon.Id)), enemies, new RunStatistics(), events);
            system.Fire(new List<StructureState>(), enemies, new List<ShotEvent>(), 1, 1, 1, 2, new RunStatistics(), events);
            int actions = 0, results = 0;
            foreach (var item in events)
            {
                if (item.Kind != SimulationEventKind.WeaponActionStarted && item.Kind != SimulationEventKind.Shot &&
                    item.Kind != SimulationEventKind.ProjectileLaunched && item.Kind != SimulationEventKind.ProjectileImpact) continue;
                Assert.That(item.Attack, Is.EqualTo(attack));
                Assert.That(item.WeaponId, Is.EqualTo(weapon.Id));
                if (item.Kind == SimulationEventKind.WeaponActionStarted) actions++; else results++;
            }
            Assert.That(actions, Is.EqualTo(1)); Assert.That(results, Is.GreaterThan(0));
        }

        [Test]
        public void NewSingleWeaponUsesItsIdForRealDamageAndIndependentSlow()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.precision",AttackKind.Single);
            input.Effects.Add(new EffectDefinitionInput{Id="effect.test.slow",Kind=EffectKind.Slow,Duration=7,Strength=.4f});weapon.EffectIds.Add("effect.test.slow");
            balance.Content=input.CreateSnapshot();var bp=Blueprint(balance,weapon.Id);var tower=Tower(bp);var enemy=Enemy(1,12,10.5f);var stats=new RunStatistics();var events=new SimulationEventBuffer();
            var system=new WeaponSystem(balance);balance.Construction.AcquisitionSeconds=99;
            Fire(system,tower,new List<EnemyState>{enemy},stats,events);
            Assert.That(enemy.Health,Is.EqualTo(89));Assert.That(enemy.BlueRemaining,Is.EqualTo(7));Assert.That(EnemyEffects.MovementMultiplier(enemy,balance.Effects),Is.EqualTo(.4f));
            Assert.That(stats.Damage(weapon.Id),Is.EqualTo(11));Assert.That(stats.Damage(StructureKind.Gun),Is.Zero);Assert.That(stats.Launches(weapon.Id),Is.EqualTo(1));
            Assert.That(events[0].WeaponId,Is.EqualTo(weapon.Id));Assert.That(bp.FirstWeapon,Is.EqualTo((StructureKind)(-1)));
        }
        [Test]
        public void NewLineDefinitionControlsActualWidthAndTargetLimit()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.line",AttackKind.Line);weapon.Stats.Radius=.6f;weapon.Stats.MaxPierce=2;
            balance.Content=input.CreateSnapshot();var targets=new List<EnemyState>{Enemy(1,12,10.5f),Enemy(2,14,11),Enemy(3,15,10.5f),Enemy(4,13,11.2f)};
            var stats=new RunStatistics();Fire(new WeaponSystem(balance),Tower(Blueprint(balance,weapon.Id)),targets,stats,new SimulationEventBuffer());
            Assert.That(targets[0].Health,Is.EqualTo(89));Assert.That(targets[1].Health,Is.EqualTo(89));Assert.That(targets[2].Health,Is.EqualTo(100));Assert.That(targets[3].Health,Is.EqualTo(100));
            Assert.That(stats.TotalDamage,Is.EqualTo(22));
        }
        [Test]
        public void ZeroDamageSingleAttackStillAppliesItsIntrinsicSlow()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.support",AttackKind.Single);weapon.Stats.Damage=0;
            input.Effects.Add(new EffectDefinitionInput{Id="effect.test.support",Kind=EffectKind.Slow,Duration=6,Strength=.5f});weapon.EffectIds.Add("effect.test.support");balance.Content=input.CreateSnapshot();
            var enemy=Enemy(1,12,10.5f);var stats=new RunStatistics();Fire(new WeaponSystem(balance),Tower(Blueprint(balance,weapon.Id)),new List<EnemyState>{enemy},stats,new SimulationEventBuffer());
            Assert.That(enemy.Health,Is.EqualTo(100));Assert.That(enemy.BlueRemaining,Is.EqualTo(6));Assert.That(stats.TotalDamage,Is.Zero);Assert.That(stats.Launches(weapon.Id),Is.EqualTo(1));
        }
        [Test]
        public void NewAreaProjectileKeepsDefinitionAndIdUntilDelayedImpact()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.area",AttackKind.AreaProjectile);weapon.Stats.Radius=2;
            balance.Content=input.CreateSnapshot();var tower=Tower(Blueprint(balance,weapon.Id));var enemy=Enemy(1,13,10.5f);var enemies=new List<EnemyState>{enemy};var stats=new RunStatistics();var events=new SimulationEventBuffer();var system=new WeaponSystem(balance);
            Fire(system,tower,enemies,stats,events);Assert.That(enemy.Health,Is.EqualTo(100));Assert.That(system.Projectiles[0].WeaponId,Is.EqualTo(weapon.Id));
            tower.Health=0;Fire(system,tower,enemies,stats,events,.4f,2);
            Assert.That(enemy.Health,Is.EqualTo(89));Assert.That(stats.Damage(weapon.Id),Is.EqualTo(11));Assert.That(events[events.Count-1].WeaponId,Is.EqualTo(weapon.Id));
        }
        [Test]
        public void NewModuleAndChassisAffectQuoteAndConstructedTowerWithoutLegacyBits()
        {
            var balance=GameBalance.CreateDefault();balance.StartingInk=500;var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.single",AttackKind.Single);
            input.Chassis.Add(new ChassisDefinitionInput{Id="chassis.test",Name="Test",TemplateId="template.test",Capacity=20,BaseCost=9,BaseHealth=80,HealthMultiplier=1,SecondaryBuildSeconds=.4f});
            input.Modules.Add(new ModuleDefinitionInput{Id="module.test.tradeoff",Name="Tradeoff",TemplateId="template.test",Load=2,Cost=7,Modifiers=new List<StatModifierInput>{
                new StatModifierInput{Stat=ModifierStat.WeaponRange,Operation=ModifierOperation.Multiply,Scope=ModifierScope.AllWeapons,Value=1.5f},
                new StatModifierInput{Stat=ModifierStat.ActionInterval,Operation=ModifierOperation.Multiply,Scope=ModifierScope.AllWeapons,Value=1.2f}}});
            balance.Content=input.CreateSnapshot();var bp=Blueprint(balance,weapon.Id,modules:new[]{new ModuleSelection("tradeoff","module.test.tradeoff")},chassis:"chassis.test");
            Assert.That(bp.Modules,Is.EqualTo(TowerModules.None));Assert.That(TowerCatalog.Cost(bp,balance,MaterialTier.Pencil),Is.EqualTo(33));Assert.That(TowerCatalog.Health(bp,balance,MaterialTier.Pencil),Is.EqualTo(80));
            Assert.That(TowerCatalog.WeaponRange(bp,balance,weapon.Id),Is.EqualTo(15));Assert.That(TowerCatalog.ActionInterval(bp,balance,weapon.Id,1),Is.EqualTo(.6f).Within(.00001));
            var session=new GameSession(balance,starterDefenses:false);Assert.That(session.TryBuildBlueprint(new[]{new Cell(12,12)},bp,MaterialTier.Pencil,InkColor.None,out var reason),Is.True,reason);
            Assert.That(session.Ink,Is.EqualTo(467));Assert.That(session.Structures[0].MaxHealth,Is.EqualTo(80));Assert.That(session.Structures[0].ContentId,Is.EqualTo(weapon.Id));Assert.That(session.Statistics.BuiltCount(weapon.Id,MaterialTier.Pencil),Is.EqualTo(1));
        }
        [Test]
        public void DataMutationAfterPreparationCannotChangeCombatOrCatalog()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.snapshot",AttackKind.Single);balance.Content=input.CreateSnapshot();
            var session=new GameSession(balance,starterDefenses:false);weapon.Stats.Damage=999;weapon.Name="Changed";balance.Effects.RedDuration=999;balance.StartingInk=0;balance.Content=null;
            Assert.That(session.Balance.StartingInk,Is.EqualTo(360));Assert.That(session.Balance.Effects.RedDuration,Is.EqualTo(3));Assert.That(session.Balance.Content.GetWeapon(weapon.Id).Stats.Damage,Is.EqualTo(11));Assert.That(session.Balance.Content.GetWeapon(weapon.Id).Name,Is.EqualTo(weapon.Id));
        }
        [Test]
        public void ReturnedBalanceCopyCannotChangeLiveDamageOrRules()
        {
            var balance=GameBalance.CreateDefault();balance.StartingInk=1000;balance.PassiveInkPerSecond=0;
            foreach(var definition in balance.StructureDefinitions)definition.BuildSeconds=0;
            foreach(var definition in balance.EnemyDefinitions){definition.MaxHealth=100;definition.Speed=definition.Damage=0;}
            balance.Waves=new List<WaveDefinition>{new WaveDefinition{Count=1}};
            var session=new GameSession(balance,starterDefenses:false);session.StartNextWave();session.Tick(.01f);session.Enemies[0].Position=new Point2(12,10.5f);
            Assert.That(session.TryBuild(new[]{new Cell(10,10)},StructureKind.Gun,MaterialTier.Pencil,out _),Is.True);
            var exposed=session.Balance;exposed.Structure(StructureKind.Gun).Damage=999;exposed.Progression.DeepInkDamageMultiplier=999;exposed.Content=null;
            session.Tick(.01f);
            Assert.That(session.Enemies[0].Health,Is.EqualTo(88));Assert.That(session.Balance.Structure(StructureKind.Gun).Damage,Is.EqualTo(12));Assert.That(session.Balance.Content,Is.Not.Null);
        }
        [Test]
        public void DamageOverTimeRetainsNewWeaponIdentity()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.dot",AttackKind.Single);weapon.EffectIds.Add(input.RedEffectId);balance.Content=input.CreateSnapshot();
            var enemy=Enemy(1,12,10.5f);var stats=new RunStatistics();Fire(new WeaponSystem(balance),Tower(Blueprint(balance,weapon.Id)),new List<EnemyState>{enemy},stats,new SimulationEventBuffer());
            EnemyEffects.Advance(enemy,.25f,stats,balance.Effects);
            Assert.That(stats.Damage(weapon.Id),Is.EqualTo(12));Assert.That(stats.Damage(StructureKind.Gun),Is.Zero);
        }
        [Test]
        public void DuplicateAndInvalidReferencesRejectWholeCatalogWithFieldPath()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.invalid",AttackKind.Single);weapon.EffectIds.Add("effect.missing");
            Assert.That(TestText.ExceptionMessage<ArgumentException>(() => input.CreateSnapshot()),Does.Contain("effect.missing"));weapon.EffectIds.Clear();weapon.Stats.Damage=float.NaN;
            Assert.That(TestText.ExceptionMessage<ArgumentException>(() => input.CreateSnapshot()),Does.Contain("weapon.test.invalid.attack.damage"));weapon.Stats.Damage=0;input.Weapons.Add(weapon);
            Assert.That(TestText.ExceptionMessage<ArgumentException>(() => input.CreateSnapshot()),Does.Contain("повторяющийся ID"));
        }
        [Test]
        public void UnknownSavedSelectionsArePreservedAndCannotSpendResources()
        {
            var balance=GameBalance.CreateDefault();balance.Content=Catalog(balance).CreateSnapshot();var blueprint=Blueprint(balance,"weapon.future");
            Assert.That(TowerCatalog.ValidateParts(blueprint,out _),Is.True);Assert.That(blueprint.WithName("Retained").FirstWeaponId,Is.EqualTo("weapon.future"));
            var session=new GameSession(balance,starterDefenses:false);int ink=session.Ink;Assert.That(session.TryBuildBlueprint(new[]{new Cell(12,12)},blueprint,MaterialTier.Pencil,InkColor.None,out _),Is.False);Assert.That(session.Ink,Is.EqualTo(ink));Assert.That(session.Structures,Is.Empty);
        }
        [Test]
        public void DeepInputCopyHasNoSharedMutableListsOrStats()
        {
            var input=Catalog(GameBalance.CreateDefault());var copy=input.CreateCopy();copy.Weapons[0].Stats.Damage=500;copy.Modules[0].Modifiers[0].Value=500;copy.Effects[0].Duration=500;copy.Templates[0].DisplayName="Changed";
            Assert.That(input.Weapons[0].Stats.Damage,Is.EqualTo(12));Assert.That(input.Modules[0].Modifiers[0].Value,Is.EqualTo(1.5f));Assert.That(input.Effects[0].Duration,Is.EqualTo(2));Assert.That(input.Templates[0].DisplayName,Is.EqualTo("Test"));
        }
        [Test]
        public void ResolvedAssembliesAreReusedOnlyForTheSameImmutableBlueprintAndCatalog()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.cache",AttackKind.Single);balance.Content=input.CreateSnapshot();
            var blueprint=Blueprint(balance,weapon.Id);var first=TowerCatalog.Resolve(blueprint,balance);
            Assert.That(TowerCatalog.Resolve(blueprint,balance),Is.SameAs(first));
            var copy=balance.CreateSnapshot();Assert.That(TowerCatalog.Resolve(blueprint,copy),Is.SameAs(first));
            weapon.Stats.Range=4;copy.Content=input.CreateSnapshot();var changed=TowerCatalog.Resolve(blueprint,copy);
            Assert.That(changed,Is.Not.SameAs(first));Assert.That(changed.First.Stats.Range,Is.EqualTo(4));Assert.That(first.First.Stats.Range,Is.EqualTo(10));
            Assert.That(TowerCatalog.Resolve(blueprint.WithName("Other"),balance),Is.Not.SameAs(first));
            var mutable=GameBalance.CreateDefault();var old=new TowerBlueprint("legacy","Legacy",ChassisKind.Standard,StructureKind.Gun);
            var before=TowerCatalog.Resolve(old,mutable);mutable.Structure(StructureKind.Gun).Range=2;
            Assert.That(TowerCatalog.Resolve(old,mutable).First.Stats.Range,Is.EqualTo(2));Assert.That(before.First.Stats.Range,Is.Not.EqualTo(2));
        }
        [Test]
        public void AdditiveTradeoffsValidateAgainstRealStatsAndSharedFeedRejectsLocalScope()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.tradeoff",AttackKind.Single);
            var modifier=new StatModifierInput{Stat=ModifierStat.WeaponRange,Operation=ModifierOperation.Add,Scope=ModifierScope.Mount,MountIndex=0,Value=-2};
            input.Modules.Add(new ModuleDefinitionInput{Id="module.test.add",Name="Add",TemplateId="template.test",Modifiers=new List<StatModifierInput>{modifier}});balance.Content=input.CreateSnapshot();
            var blueprint=Blueprint(balance,weapon.Id,modules:new[]{new ModuleSelection("tradeoff","module.test.add")});
            Assert.That(TowerCatalog.Validate(blueprint,balance,out var reason),Is.True,reason);Assert.That(TowerCatalog.WeaponRange(blueprint,balance,weapon.Id),Is.EqualTo(8));
            modifier.Stat=ModifierStat.FeedMultiplier;modifier.Value=2;
            Assert.That(TestText.ExceptionMessage<ArgumentException>(() => input.CreateSnapshot()),Does.Contain("module.test.add.modifiers[0].scope"));
            modifier.Stat=ModifierStat.WeaponRange;modifier.Value=-20;balance.Content=input.CreateSnapshot();
            Assert.That(TowerCatalog.Validate(blueprint,balance,out reason),Is.False);Assert.That(TestText.Resolve(reason),Does.Contain("WeaponRange"));
        }
        [Test]
        public void AreaProbeRetainsNewSecondaryWeaponAndMountScopedRange()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var first=AddWeapon(input,"weapon.test.probe.first",AttackKind.Single);first.Stats.Damage=999;
            var area=AddWeapon(input,"weapon.test.probe.area",AttackKind.AreaProjectile);area.Stats.Range=1;area.Stats.Radius=2;area.Stats.Damage=7;
            input.Chassis.Add(new ChassisDefinitionInput{Id="chassis.test.probe",Name="Probe",TemplateId="template.test",Capacity=20,BaseHealth=80});
            input.Modules.Add(new ModuleDefinitionInput{Id="module.test.probe",Name="Probe range",TemplateId="template.test",Modifiers=new List<StatModifierInput>{
                new StatModifierInput{Stat=ModifierStat.WeaponRange,Operation=ModifierOperation.Add,Scope=ModifierScope.Mount,MountIndex=1,Value=9}}});balance.Content=input.CreateSnapshot();
            var blueprint=Blueprint(balance,first.Id,area.Id,new[]{new ModuleSelection("probe","module.test.probe")},"chassis.test.probe");
            var report=MortarFootprintProbe.Compare(blueprint,1,MortarFootprint.Circle,new MortarFootprint(2),balance,FootprintFormation.Group).First;
            Assert.That(report.Completed,Is.True);Assert.That(report.HitCount,Is.GreaterThan(0));Assert.That(report.Cost,Is.EqualTo(TowerCatalog.Cost(blueprint,balance,MaterialTier.Pencil)));
            var impact=report.Frames[report.Frames.Count-1];bool foundProjectile=false;
            foreach(var frame in report.Frames)foreach(var projectile in frame.Projectiles){Assert.That(projectile.WeaponId,Is.EqualTo(area.Id));foundProjectile=true;}
            Assert.That(impact.Impacts[0].WeaponId,Is.EqualTo(area.Id));
            foreach(var enemy in impact.Enemies)Assert.That(enemy.Health,Is.GreaterThanOrEqualTo(93));
            Assert.That(foundProjectile,Is.True);
        }
        [Test]
        public void LegacyStarterConvenienceSkipsMissingAndDisabledCatalogWeapons()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);input.StartingPages.Clear();input.Weapons.RemoveAll(x=>x.Id=="weapon.gun");input.Weapons.Find(x=>x.Id=="weapon.piercer").Enabled=false;
            balance.StructureDefinitions.RemoveAll(x=>x.Kind==StructureKind.Gun);balance.Content=input.CreateSnapshot();
            var session=new GameSession(balance);
            Assert.That(session.Structures.Count,Is.EqualTo(10));foreach(var structure in session.Structures)Assert.That(structure.Kind,Is.EqualTo(StructureKind.Wall));
        }
        [Test]
        public void IntervalFloorRemainsPositiveAfterFeedAndAdditiveModifiers()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.interval",AttackKind.Single);weapon.Stats.ShotInterval=1;
            var modifier=new StatModifierInput{Stat=ModifierStat.ActionInterval,Operation=ModifierOperation.Add,Scope=ModifierScope.AllWeapons,Value=-.5f};
            input.Modules.Add(new ModuleDefinitionInput{Id="module.test.interval",Name="Interval",TemplateId="template.test",Modifiers=new List<StatModifierInput>{modifier}});balance.Content=input.CreateSnapshot();
            var blueprint=Blueprint(balance,weapon.Id,modules:new[]{new ModuleSelection("interval","module.test.interval")});
            Assert.That(TowerCatalog.Validate(blueprint,balance,out var reason),Is.True,reason);
            Assert.That(TowerCatalog.ActionInterval(blueprint,balance,weapon.Id,2),Is.EqualTo(balance.Combat.MinimumActionInterval));
            Assert.That(TowerCatalog.ActionInterval(blueprint,balance,weapon.Id,10),Is.EqualTo(balance.Combat.MinimumActionInterval));
            modifier.Operation=ModifierOperation.Set;modifier.Value=0;balance.Content=input.CreateSnapshot();
            Assert.That(TowerCatalog.Validate(blueprint,balance,out reason),Is.False);Assert.That(TestText.Resolve(reason),Does.Contain("ActionInterval"));
        }
        [Test]
        public void RadialPreviewAndActualReachUseRadiusWithTheSameRangeModifiers()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);var weapon=AddWeapon(input,"weapon.test.radial",AttackKind.Radial);weapon.Stats.Range=20;weapon.Stats.Radius=1;
            input.Modules.Add(new ModuleDefinitionInput{Id="module.test.radial",Name="Radial",TemplateId="template.test",Modifiers=new List<StatModifierInput>{
                new StatModifierInput{Stat=ModifierStat.WeaponRange,Operation=ModifierOperation.Add,Scope=ModifierScope.AllWeapons,Value=2}}});balance.Content=input.CreateSnapshot();
            var blueprint=Blueprint(balance,weapon.Id,modules:new[]{new ModuleSelection("radial","module.test.radial")});
            Assert.That(TowerCatalog.WeaponRange(blueprint,balance,weapon.Id),Is.EqualTo(3));
            var enemies=new List<EnemyState>{Enemy(1,13,10.5f),Enemy(2,14,10.5f)};
            Fire(new WeaponSystem(balance),Tower(blueprint),enemies,new RunStatistics(),new SimulationEventBuffer());
            Assert.That(enemies[0].Health,Is.EqualTo(89));Assert.That(enemies[1].Health,Is.EqualTo(100));
        }
        [Test]
        public void TooManyGenericModuleBindingsBlockAssemblyWithoutDeletingTheStoredPage()
        {
            var balance=GameBalance.CreateDefault();var input=Catalog(balance);
            input.Modules.Add(new ModuleDefinitionInput{Id="module.test.binding-limit",Name="Binding",TemplateId="template.test",Repeatable=true,InitiallyUnlocked=true});balance.Content=input.CreateSnapshot();
            var selections=new List<ModuleSelection>();for(int i=0;i<TowerCatalog.MaximumGenericModuleBindings;i++)selections.Add(new ModuleSelection("instance."+i,"module.test.binding-limit"));
            var allowed=Blueprint(balance,"weapon.gun",modules:selections);
            Assert.That(TowerCatalog.Validate(allowed,balance,out var reason),Is.True,reason);
            selections.Add(new ModuleSelection("instance.extra","module.test.binding-limit"));var oversized=Blueprint(balance,"weapon.gun",modules:selections);
            Assert.That(TowerCatalog.ValidateParts(oversized,out _),Is.True);Assert.That(TowerCatalog.Validate(oversized,balance,out reason),Is.False);Assert.That(TestText.Resolve(reason),Does.Contain("blueprint.modules"));
            var data=new WorkshopProfile(balance.Content).ToData();var pages=new List<BlueprintData>(data.Album);pages.Add(new BlueprintData(oversized));data.Album=pages.ToArray();
            var loaded=WorkshopProfile.FromData(data);var retained=loaded.Album[loaded.Album.Count-1];
            Assert.That(retained.Id,Is.EqualTo(oversized.Id));Assert.That(retained.ModuleSelections.Count,Is.EqualTo(TowerCatalog.MaximumGenericModuleBindings+1));
            Assert.That(new BlueprintData(retained).ToBlueprint().ModuleSelections.Count,Is.EqualTo(retained.ModuleSelections.Count));
            var session=new GameSession(balance,starterDefenses:false);int ink=session.Ink;
            Assert.That(session.TryBuildBlueprint(new[]{new Cell(12,12)},retained,MaterialTier.Pencil,InkColor.None,out _),Is.False);Assert.That(session.Ink,Is.EqualTo(ink));Assert.That(session.Structures,Is.Empty);
        }
    }
}
