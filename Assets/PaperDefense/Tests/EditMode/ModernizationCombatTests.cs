using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ModernizationCombatTests
    {
        [TestCase(InkColor.Red,1,1)] [TestCase(InkColor.Red,2,1.4f)] [TestCase(InkColor.Red,3,2)]
        [TestCase(InkColor.Blue,1,.65f)] [TestCase(InkColor.Blue,2,.56f)] [TestCase(InkColor.Blue,3,.48f)]
        [TestCase(InkColor.Green,1,.20f)] [TestCase(InkColor.Green,2,.28f)] [TestCase(InkColor.Green,3,.36f)]
        public void ColorRanksScaleCapturedDefinitions(InkColor color,int rank,float expected)
        {
            var baseline=ColorEffectPayload.Capture(color,new EffectsRules());var result=ModernizationRules.ResolveColor(baseline,rank);
            Assert.That(result.Strength,Is.EqualTo(expected).Within(.00001));Assert.That(result.Duration,Is.EqualTo(baseline.Duration));
            Assert.That(result.TickSeconds,Is.EqualTo(baseline.TickSeconds));Assert.That(result.IgnoresDefense,Is.EqualTo(baseline.IgnoresDefense));
        }
        [Test] public void CustomBlueAndGreenDefinitionsRespectEffectBounds()
        {
            var rules=new EffectsRules {BlueSpeedMultiplier=.45f,GreenDefenseReduction=.8f};
            Assert.That(ModernizationRules.ResolveColor(ColorEffectPayload.Capture(InkColor.Blue,rules),3).Strength,Is.EqualTo(.4f));
            var green=ModernizationRules.ResolveColor(ColorEffectPayload.Capture(InkColor.Green,rules),3);
            Assert.That(green.Strength,Is.LessThanOrEqualTo(1));
            Assert.DoesNotThrow(()=>EnemyEffects.ApplyHit(new EnemyState { Health=100 },0,green,new RunStatistics(),StructureKind.Gun,rules,0));
        }
        [Test] public void SharedBudgetIncreaseRetainsSpentTargetsAndCurrentWindow()
        {
            var budget=new TowerColorBudget(1,3);
            Assert.That(budget.TryApply(1,0),Is.True);Assert.That(budget.TryApply(2,0),Is.True);Assert.That(budget.TryApply(3,0),Is.True);
            budget.SetTargetLimit(4);Assert.That(budget.TryApply(1,.1),Is.False);Assert.That(budget.TryApply(4,.1),Is.True);Assert.That(budget.TryApply(5,.1),Is.False);
            Assert.That(budget.TryApply(1,1),Is.True);
        }
        static GameBalance Balance()
        {var b=GameBalance.CreateDefault();b.Mechanics.FieldModernization=true;b.Mechanics.MaximumModernizationRank=3;b.Construction.AcquisitionSeconds=0;return b;}
        static StructureState Tower(GameBalance balance,string first="gun",string second=null)
        {
            var recipe=AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(first),second==null?null:AssemblyResolverFixture.Weapon(second));
            var entry=AssemblyResolverFixture.Resolve(recipe,balance:balance);
            return new StructureState(entry) { Id=1,Cell=new Cell(10,10),IsComplete=true,BuildRemaining=0 };
        }
        static EnemyState Enemy(int id,float x,float y=10.5f)=>new EnemyState {Id=id,Position=new Point2(x,y),Health=1000,MaxHealth=1000};

        [TestCase(false,false)] [TestCase(true,false)] [TestCase(true,true)]
        public void PaidRanksReachTheOriginalWeaponAndPreserveItsDrawingAndBearing(bool blueprint,bool dynamicFeed)
        {
            var balance=Balance();
            balance.StartingInk=1000;
            balance.PlanningSeconds=1000;
            balance.Construction.DynamicFeed=dynamicFeed;
            balance.EnableInkWells=true;
            balance.WellInkPerWave=40;
            balance.Waves[0].Count=0;
            var session=new GameSession(balance,starterDefenses:false);
            var cell=new Cell(10,10);
            var cells=new[]{cell};
            var page=new TowerBlueprint("old-page","Дозор",ChassisKind.Standard,StructureKind.Gun);
            if(dynamicFeed)
            {
                page=page.AsFreeform();
                var drawingEditor=new DrawingEditSession(page,page.Drawing);
                drawingEditor.AddRectangle(DrawingBindings.Base,new DrawingPoint(-.35f,-.35f),new DrawingPoint(.35f,.35f),out _);
                drawingEditor.AddRectangle(DrawingBindings.Mount0,new DrawingPoint(-.1f,0),new DrawingPoint(.1f,.65f),out _);
                page=page.WithFreeformDrawing(drawingEditor.Snapshot);
            }
            string reason;
            bool built=blueprint
                ?session.TryBuildBlueprint(cells,page,MaterialTier.Pencil,InkColor.None,out reason,0,"old-page")
                :session.TryBuild(cells,StructureKind.Gun,MaterialTier.Pencil,out reason,0);
            Assert.That(built,Is.True,reason);
            var tower=session.StructureAt(cell);
            session.Tick(tower.BuildRemaining+.1f);
            var drawing=tower.Blueprint;
            var original=tower.ModernizationEntry;
            Assert.That(original.Kind,Is.EqualTo(AlbumEntryKind.Legacy));
            Assert.That(new RunReadModel(session).StructureAt(cell).Value.ModernizationEntry,Is.SameAs(original));
            if(blueprint)Assert.That(tower.SourceAlbumEntryId,Is.EqualTo("old-page"));
            var enemies=new List<EnemyState>{Enemy(1,10.5f+original.Weapons[0].Range*1.05f)};
            var weapons=new WeaponSystem(balance);
            var shots=new List<ShotEvent>();
            void Fire() => weapons.Fire(new List<StructureState>{tower},enemies,shots,1,1,.01f,1,new RunStatistics(),new SimulationEventBuffer());
            Fire();
            Assert.That(shots,Is.Empty,"The test target starts just beyond the original range.");
            foreach(ModernizationDirection direction in Enum.GetValues(typeof(ModernizationDirection)))
            {
                var quote=session.QuoteModernization(tower.Id,direction);
                Assert.That(quote.Valid,Is.True,quote.Reason);
                int ink=session.Ink;
                Assert.That(session.TryModernize(quote,out reason),Is.True,reason);
                Assert.That(session.Ink,Is.EqualTo(ink-quote.Cost));
                Assert.That(tower.ModernizationEntry,Is.SameAs(quote.Before));
                session.Tick(quote.WorkSeconds+.1f);
                Assert.That(tower.Modernization.Rank(direction),Is.EqualTo(quote.NextRank));
                Assert.That(tower.ModernizationEntry.Health,Is.EqualTo(quote.After.Health));
                Assert.That(tower.ModernizationEntry.Weapons[0].DpsAtFeed(1),Is.EqualTo(quote.After.Weapons[0].DpsAtFeed(1)));
            }
            int well=0;while(session.Wells[well].Color!=InkColor.Red)well++;
            Assert.That(session.TryActivateWell(well,out reason),Is.True,reason);
            session.StartNextWave();session.Tick(.1f);
            var red=session.QuoteColorModernization(tower.Id,InkColor.Red);
            Assert.That(session.TryModernize(red,out reason),Is.True,reason);
            session.Tick(red.WorkSeconds+.1f);
            Fire();
            Assert.That(enemies[0].Health,Is.EqualTo(1000-original.Weapons[0].Damage*1.3f).Within(.001f));
            Assert.That(enemies[0].RedEffect.Active.Strength,Is.EqualTo(red.AfterColor.Strength).Within(.001f));
            Assert.That(tower.GetMountState(0).Cooldown,Is.EqualTo(tower.ModernizationEntry.Weapons[0].Interval).Within(.001f));
            Assert.That(tower.MaxHealth,Is.EqualTo(original.Health*1.35f).Within(.001f));
            Assert.That(tower.FacingDegrees,Is.Zero);
            Assert.That(tower.Blueprint,Is.SameAs(drawing));
            Assert.That(tower.ResolvedEntry,Is.Null,"Legacy combat and authored drawing keep their original execution path.");
        }
        [Test] public void RealPowerShotLeavesRedPayloadAtColorRankStrength()
        {
            var b=Balance();var t=Tower(b);ModernizationRules.InitializeModernizationForTrial(t,new ModernizationRanks(power:3,color:InkColor.Red,colorRank:2),3,b);
            var enemy=Enemy(1,12.5f);var stats=new RunStatistics();
            new WeaponSystem(b).Fire(new List<StructureState>{t},new List<EnemyState>{enemy},new List<ShotEvent>(),1,1,.01f,1,stats,new SimulationEventBuffer());
            Assert.That(enemy.Health,Is.EqualTo(1000-12*2.1f).Within(.001));Assert.That(enemy.RedEffect.Active.Strength,Is.EqualTo(1.4f).Within(.001));
        }
        [TestCase(false)] [TestCase(true)]
        public void TrialInitializationRejectsDisabledOrInertColorBeforeChangingTheTower(bool inert)
        {
            var b=Balance();if(inert)b.Effects.RedDamagePerTick=0;else b.Mechanics.ColorCartridges=false;
            var tower=Tower(b);var baseline=tower.ResolvedEntry;
            Assert.Throws<ArgumentException>(()=>ModernizationRules.InitializeModernizationForTrial(tower,new ModernizationRanks(color:InkColor.Red,colorRank:1),3,b));
            Assert.That(tower.ResolvedEntry,Is.SameAs(baseline));Assert.That(tower.Color,Is.EqualTo(InkColor.None));Assert.That(tower.Modernization.ColorRank,Is.Zero);
        }
        [Test] public void MultipleMountsShareOneThreeTargetColorBudget()
        {
            var b=Balance();var t=Tower(b,"fan","fan");ModernizationRules.InitializeModernizationForTrial(t,new ModernizationRanks(color:InkColor.Green,colorRank:1),3,b);
            var enemies=Enumerable.Range(1,7).Select(i=>Enemy(i,11.5f+i*.2f)).ToList();var stats=new RunStatistics();
            new WeaponSystem(b).Fire(new List<StructureState>{t},enemies,new List<ShotEvent>(),1,1,.01f,1,stats,new SimulationEventBuffer());
            Assert.That(stats.Launches("weapon.fan"),Is.EqualTo(2));Assert.That(enemies.Count(e=>e.GreenRemaining>0),Is.EqualTo(3));
        }
        [Test] public void FlyingMortarRetainsOldDamagePayloadAndSharedBudgetAfterRankChange()
        {
            var b=Balance();var t=Tower(b,"mortar");ModernizationRules.InitializeModernizationForTrial(t,new ModernizationRanks(color:InkColor.Red,colorRank:1),3,b);
            var enemies=Enumerable.Range(1,6).Select(i=>Enemy(i,18.5f+i*.05f)).ToList();var weapons=new WeaponSystem(b);var shots=new List<ShotEvent>();var stats=new RunStatistics();var events=new SimulationEventBuffer();
            weapons.Fire(new List<StructureState>{t},enemies,shots,1,1,.01f,1,stats,events,.01);
            var shell=weapons.Projectiles.Single();var shared=t.ColorBudget;
            Assert.That(shared.TryApply(99,.01),Is.True);Assert.That(shared.TryApply(98,.01),Is.True);Assert.That(shared.TryApply(97,.01),Is.True);
            t.ModernizationState.Ranks=new ModernizationRanks(power:3,color:InkColor.Red,colorRank:2);
            t.ResolvedEntry=ModernizationRules.Resolve(t.ModernizationState.Baseline,t.ModernizationState.Ranks);shared.SetTargetLimit(4);
            weapons.Fire(new List<StructureState>(),enemies,shots,1,1,.79f,2,stats,events,.8);
            Assert.That(enemies.Count(e=>e.RedRemaining>0),Is.EqualTo(1),"Only one newly available budget target remains; rank change cannot reset three already spent targets.");
            Assert.That(enemies.Where(e=>e.RedRemaining>0).Single().RedEffect.Active.Strength,Is.EqualTo(1));
            Assert.That(enemies.All(e=>Math.Abs(e.Health-(1000-28))<.001),Is.True,"The launched shell retains its unmodified direct damage.");
            Assert.That(shell.ColorBudget,Is.SameAs(shared));
        }
        [Test] public void WorkingTowerCannotFireAndResumesTheSameUnfinishedMagazine()
        {
            var b=Balance();var tower=Tower(b);var enemy=Enemy(1,12.5f);var weapons=new WeaponSystem(b);var stats=new RunStatistics();var shots=new List<ShotEvent>();var events=new SimulationEventBuffer();
            var towers=new List<StructureState>{tower};var enemies=new List<EnemyState>{enemy};weapons.Fire(towers,enemies,shots,1,1,.1f,1,stats,events,.1);
            var cycle=tower.GetMountState(0).Cycle;int rounds=cycle.RoundsRemaining;double work=cycle.RemainingWork;float health=enemy.Health;
            var entry=tower.ResolvedEntry;var sessionRules=Balance();sessionRules.StartingInk=10000;sessionRules.PlanningSeconds=1000;
            var session=new GameSession(sessionRules,starterDefenses:false);Assert.That(session.TryBuildEntry(new[]{new Cell(5,5)},entry,MaterialTier.Pencil,InkColor.None,out _),Is.True);session.Tick(entry.BuildSeconds+.1f);
            var paid=session.Structures.Single();paid.GetMountState(0).Cycle=cycle;paid.GetMountState(0).AcquisitionRemaining=.3f;paid.GetMountState(0).TargetId=enemy.Id;
            Assert.That(session.TryModernize(session.QuoteModernization(paid.Id,ModernizationDirection.Mechanics),out _),Is.True);
            weapons.Fire(new List<StructureState>{paid},enemies,shots,1,1,1,2,stats,events,1.1);
            Assert.That(enemy.Health,Is.EqualTo(health));Assert.That(paid.GetMountState(0).Cycle,Is.SameAs(cycle));
            session.Tick(2);Assert.That(paid.Modernization.IsWorking,Is.False);Assert.That(paid.GetMountState(0).Cycle.RoundsRemaining,Is.EqualTo(rounds));Assert.That(paid.GetMountState(0).Cycle.RemainingWork,Is.EqualTo(work));Assert.That(paid.GetMountState(0).AcquisitionRemaining,Is.EqualTo(.3f));
        }
        [Test] public void MixedCleanupMountDoesNotAcquirePowerDamage()
        {
            var b=Balance();var t=Tower(b,"gun","blotter");var entry=ModernizationRules.Resolve(t.ResolvedEntry,new ModernizationRanks(power:3));
            Assert.That(entry.Weapons.Single(w=>w.Attack==AttackKind.Cleanup).Damage,Is.Zero);
            Assert.That(entry.Weapons.Single(w=>w.Attack!=AttackKind.Cleanup).Damage,Is.EqualTo(25.2f).Within(.001));
        }
        [Test] public void RealRicochetUsesUpgradedAcquisitionRangeButUnchangedJumpAndTargetLimits()
        {
            var b=Balance();var tower=Tower(b,"ricochet");var baseline=tower.ResolvedEntry.Weapons[0];
            ModernizationRules.InitializeModernizationForTrial(tower,new ModernizationRanks(range:3,color:InkColor.Green,colorRank:1),3,b);
            float firstX=10.5f+baseline.Range*1.2f;
            var enemies=Enumerable.Range(1,4).Select(i=>Enemy(i,firstX+(i-1)*baseline.JumpRange*.5f)).ToList();
            var stats=new RunStatistics();var shots=new List<ShotEvent>();
            new WeaponSystem(b).Fire(new List<StructureState>{tower},enemies,shots,1,1,.01f,1,stats,new SimulationEventBuffer());
            Assert.That(stats.Launches("weapon.ricochet"),Is.EqualTo(1));Assert.That(shots.Count,Is.EqualTo(baseline.TargetLimit));
            Assert.That(enemies.All(e=>Math.Abs(e.Health-(1000-baseline.Damage))<.001f),Is.True);
            Assert.That(enemies.Count(e=>e.GreenRemaining>0),Is.EqualTo(3));
            Assert.That(tower.ResolvedEntry.Weapons[0].JumpRange,Is.EqualTo(baseline.JumpRange));
            Assert.That(tower.ResolvedEntry.Weapons[0].TargetLimit,Is.EqualTo(baseline.TargetLimit));
        }
        [Test] public void RealMechanicsShotsRespectTheIntervalFloorAndSharedFeed()
        {
            var b=Balance();b.Combat.MinimumActionInterval=.5f;var tower=Tower(b,"gun","gun");var baseline=tower.ResolvedEntry;
            ModernizationRules.InitializeModernizationForTrial(tower,new ModernizationRanks(mechanics:3),3,b);
            Assert.That(tower.ResolvedEntry.FeedForActiveMounts(2),Is.EqualTo(baseline.FeedForActiveMounts(2)));
            var enemy=Enemy(1,12.5f);var stats=new RunStatistics();
            new WeaponSystem(b).Fire(new List<StructureState>{tower},new List<EnemyState>{enemy},new List<ShotEvent>(),1,1,.49f,1,stats,new SimulationEventBuffer());
            Assert.That(stats.Launches("weapon.gun"),Is.EqualTo(2),"Each mount gets its initial shot; mechanics cannot bypass the action interval floor.");
            Assert.That(tower.GetMountState(0).Cycle.RoundsRemaining,Is.EqualTo(baseline.Weapons[0].MagazineSize-1));
            Assert.That(tower.GetMountState(1).Cycle.RoundsRemaining,Is.EqualTo(baseline.Weapons[1].MagazineSize-1));
        }
    }
}
