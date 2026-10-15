using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class VisualEventMetadataTests
    {
        [Test] public void ErasingPublishesADistinctVisualWithoutRecordingACombatLoss()
        {
            var session = new GameSession(GameBalance.CreateDefault(), starterDefenses: false);
            var cell = new Cell(3, 3);
            Assert.That(session.TryBuild(new[] { cell }, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
            Assert.That(session.TryErase(cell, out _), Is.True);
            Assert.That(session.EventBuffer.Count(e => e.Kind.ToString() == "StructureErased"), Is.EqualTo(1));
            Assert.That(session.EventBuffer.Any(e => e.Kind == SimulationEventKind.StructureDestroyed), Is.False);
        }

        [Test] public void CompletionPublishesOnceWhenConstructionActuallyFinishes()
        {
            var session = new GameSession(GameBalance.CreateDefault(), starterDefenses: false);
            Assert.That(session.TryBuild(new[] { new Cell(3, 3) }, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
            Assert.That(session.EventBuffer.Any(e => e.Kind.ToString() == "StructureCompleted"), Is.False);
            for (int i = 0; i < 100; i++) session.Tick(.05f);
            Assert.That(session.EventBuffer.Count(e => e.Kind.ToString() == "StructureCompleted"), Is.EqualTo(1));
        }

        static StructureState Tower(StructureKind kind=StructureKind.Gun)=>new StructureState
        {Id=7,Cell=new Cell(10,10),Kind=kind,Tier=MaterialTier.Pencil,Health=100,MaxHealth=100,IsComplete=true};
        static EnemyState Enemy(int id=1,float health=1000,float y=12.5f)=>new EnemyState
        {Id=id,Kind=EnemyKind.Runner,DrawingTier=DrawingTier.Hatched,Position=new Point2(10.5f,y),Health=health,MaxHealth=health};
        static void Fire(WeaponSystem weapons,StructureState tower,List<EnemyState> enemies,SimulationEventBuffer events,List<ShotEvent> shots=null,
            double time=7.25,float delta=.01f,long tick=42)
            =>weapons.Fire(new List<StructureState>{tower},enemies,shots??new List<ShotEvent>(),1,1,delta,tick,new RunStatistics(),events,time);

        [Test] public void ExistingEnumValuesAndOptionalPublishDefaultsRemainCompatible()
        {
            Assert.That((int)SimulationEventKind.Shot,Is.Zero);Assert.That((int)SimulationEventKind.ProjectileLaunched,Is.EqualTo(1));
            Assert.That((int)SimulationEventKind.ProjectileImpact,Is.EqualTo(2));Assert.That((int)SimulationEventKind.StructureDestroyed,Is.EqualTo(3));
            var buffer=new SimulationEventBuffer();buffer.Publish(1,SimulationEventKind.Shot,default,default,StructureKind.Gun,entityId:12);
            Assert.That(buffer[0].EntityId,Is.EqualTo(12));Assert.That(buffer[0].MountSlotIndex,Is.EqualTo(-1));
            Assert.That(buffer[0].SourceStructureId,Is.Zero);Assert.That(buffer[0].ActionId,Is.Zero);
            Assert.That(new ShotEvent(default,default,StructureKind.Gun,0).MountSlotIndex,Is.EqualTo(-1));
        }

        [TestCase(StructureKind.Fan)] [TestCase(StructureKind.Ricochet)]
        public void MultipleSegmentsShareOneActionAndExactSimulationTime(StructureKind kind)
        {
            var buffer=new SimulationEventBuffer();var shots=new List<ShotEvent>();
            Fire(new WeaponSystem(GameBalance.CreateDefault()),Tower(kind),new List<EnemyState>{Enemy(),Enemy(2,y:13.5f),Enemy(3,y:14.5f)},buffer,shots);
            var action=buffer.Single(e=>e.Kind==SimulationEventKind.WeaponActionStarted);
            Assert.That(shots.Count,Is.GreaterThan(1));Assert.That(action.SourceStructureId,Is.EqualTo(7));
            Assert.That(action.MountSlotIndex,Is.Zero);Assert.That(action.SimulationSeconds,Is.EqualTo(7.25));
            foreach(var shot in shots)
            {Assert.That(shot.ActionId,Is.EqualTo(action.ActionId));Assert.That(shot.SourceStructureId,Is.EqualTo(7));Assert.That(shot.MountSlotIndex,Is.Zero);Assert.That(shot.SimulationSeconds,Is.EqualTo(7.25));}
            foreach(var shot in buffer.Where(e=>e.Kind==SimulationEventKind.Shot))
            {Assert.That(shot.ActionId,Is.EqualTo(action.ActionId));Assert.That(shot.EntityId,Is.EqualTo(7));}
        }

        [Test] public void ThreeIdenticalMountsPublishIndependentActionsWithoutConsumingEntityIds()
        {
            var balance=GameBalance.CreateDefault();balance.Construction.AcquisitionSeconds=0;
            var tower=Tower();tower.Blueprint=new TowerBlueprint("triple","Triple","chassis.heavy",new[]{
                new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(1,"weapon.gun"),new WeaponMountSelection(2,"weapon.gun")});
            var events=new SimulationEventBuffer();Fire(new WeaponSystem(balance),tower,new List<EnemyState>{Enemy()},events);
            var actions=events.Where(e=>e.Kind==SimulationEventKind.WeaponActionStarted).ToArray();
            Assert.That(actions.Length,Is.EqualTo(3));Assert.That(actions.Select(e=>e.ActionId).Distinct().Count(),Is.EqualTo(3));
            Assert.That(actions.Select(e=>e.MountSlotIndex),Is.EqualTo(new[]{0,1,2}));
            Assert.That(actions.All(e=>e.EntityId==7&&e.SourceStructureId==7),Is.True);
        }

        [Test] public void MortarImpactRetainsActionAndSourceAfterTowerRemoval()
        {
            var balance=GameBalance.CreateDefault();balance.Structure(StructureKind.Mortar).FlightSeconds=.2f;
            var weapons=new WeaponSystem(balance);var events=new SimulationEventBuffer();var enemies=new List<EnemyState>{Enemy()};
            Fire(weapons,Tower(StructureKind.Mortar),enemies,events,time:3);
            var action=events.Single(e=>e.Kind==SimulationEventKind.WeaponActionStarted);
            var launch=events.Single(e=>e.Kind==SimulationEventKind.ProjectileLaunched);
            var view=new ProjectileView(weapons.Projectiles[0]);
            weapons.Fire(new List<StructureState>(),enemies,new List<ShotEvent>(),1,1,.2f,43,new RunStatistics(),events,3.2);
            var impact=events.Single(e=>e.Kind==SimulationEventKind.ProjectileImpact);
            Assert.That(events.Count(e=>e.Kind==SimulationEventKind.WeaponActionStarted),Is.EqualTo(1));
            Assert.That(impact.EntityId,Is.EqualTo(launch.EntityId));Assert.That(impact.SourceStructureId,Is.EqualTo(7));
            Assert.That(impact.ActionId,Is.EqualTo(action.ActionId));Assert.That(impact.MountSlotIndex,Is.Zero);
            Assert.That(impact.SimulationSeconds,Is.EqualTo(3.2));Assert.That(view.ActionId,Is.EqualTo(action.ActionId));
        }

        [Test] public void DeathSnapshotSurvivesRemovalAndCannotRepeatWithSeveralHitsOrWithoutBlots()
        {
            var enemy=Enemy(health:1);var position=enemy.Position;var events=new SimulationEventBuffer();
            Fire(new WeaponSystem(GameBalance.CreateDefault()),Tower(),new List<EnemyState>{enemy},events);
            EnemyEffects.PublishDeath(enemy,events,43,8);EnemyEffects.PublishDeath(enemy,events,44,9);
            var death=events.Single(e=>e.Kind==SimulationEventKind.EnemyDied);enemy.Position=new Point2(99,99);enemy.Kind=EnemyKind.Doodle;
            Assert.That(death.To,Is.EqualTo(position));Assert.That(death.EnemyKind,Is.EqualTo(EnemyKind.Runner));
            Assert.That(death.EnemyDrawingTier,Is.EqualTo(DrawingTier.Hatched));Assert.That(death.EntityId,Is.EqualTo(1));
        }

        [Test] public void DamageOverTimePublishesOneDeathAtActualAdvancementTime()
        {
            var enemy=Enemy(health:2);var events=new SimulationEventBuffer();var rules=new EffectsRules();
            EnemyEffects.ApplyHit(enemy,0,InkColor.Red,new RunStatistics(),StructureKind.Gun);
            EnemyEffects.Advance(enemy,2,new RunStatistics(),rules,events:events,tick:12,simulationSeconds:1.125);
            EnemyEffects.Advance(enemy,2,new RunStatistics(),rules,events:events,tick:13,simulationSeconds:3.125);
            var death=events.Single(e=>e.Kind==SimulationEventKind.EnemyDied);
            Assert.That(death.Tick,Is.EqualTo(12));Assert.That(death.SimulationSeconds,Is.EqualTo(1.125));
            Assert.That(events.Count(e=>e.Kind==SimulationEventKind.EnemyHit),Is.EqualTo(1));
        }

        [Test]
        public void DamageOverTimeHitNamesTheSourceThatActuallyPulsedAfterAStrongerEffectExpired()
        {
            var enemy = Enemy(); var events = new SimulationEventBuffer();
            var rules = new EffectsRules { RedDuration = .1f, RedDamagePerTick = 4, RedTickSeconds = .25f };
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Red, null, StructureKind.Mortar, rules);
            rules.RedDuration = 1; rules.RedDamagePerTick = 1;
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Red, null, StructureKind.Gun, rules);
            EnemyEffects.Advance(enemy, .3f, null, rules, events: events);
            Assert.That(events.Single(e => e.Kind == SimulationEventKind.EnemyHit).WeaponId, Is.EqualTo("weapon.gun"));
            Assert.That(enemy.Health, Is.EqualTo(999));
        }

        [Test] public void FullSmallBufferReportsDropsAndPreservesCombatResult()
        {
            float Resolve(int capacity,out SimulationEventBuffer events)
            {events=new SimulationEventBuffer(capacity);var enemy=Enemy(health:1);Fire(new WeaponSystem(GameBalance.CreateDefault()),Tower(),new List<EnemyState>{enemy},events);return enemy.Health;}
            float tiny=Resolve(1,out var small),normal=Resolve(512,out var large);
            Assert.That(tiny,Is.EqualTo(normal));Assert.That(tiny,Is.Zero);Assert.That(small.Count,Is.EqualTo(1));
            Assert.That(small.LatestSequence,Is.EqualTo(large.LatestSequence));Assert.That(small.DroppedEventCount,Is.EqualTo(large.Count-1));
        }

        [Test] public void BlotLifecyclePublishesActualTransitionsOnce()
        {
            var transitions=new List<SimulationEventKind>();
            var system=new CorpseBlotSystem(20,20,(kind,cell)=>transitions.Add(kind));
            system.Add(new Cell(4,4),1);system.Add(new Cell(4,4),2);
            Assert.That(transitions,Is.EqualTo(new[]{SimulationEventKind.BlotSpawned}),"Refreshing a live merged blot does not create another birth.");
            system.Advance(2);system.Advance(2);system.FinishStep();
            Assert.That(transitions.Count(k=>k==SimulationEventKind.BlotExpired),Is.EqualTo(1));
            system.Add(new Cell(5,5),1);system.Clear(new Cell(5,5),0);system.Clear(new Cell(5,5),0);
            Assert.That(transitions.Count(k=>k==SimulationEventKind.BlotCleared),Is.EqualTo(1));
        }

        [Test] public void PausePublishesNothingAndAbsorptionCarriesTheActualEnemySnapshot()
        {
            var balance=GameBalance.CreateDefault();balance.EnableEnemyMechanics=true;
            var game=GameSession.CreatePrepared(balance,1,new[]{new PreparedEnemy(new Point2(4.5f,4.5f),100,EnemyKind.Runner,DrawingTier.Hatched)},
                new[]{new PreparedBlot(new Cell(4,4),2)});
            long before=game.EventBuffer.LatestSequence;game.Tick(0);
            Assert.That(game.EventBuffer.LatestSequence,Is.EqualTo(before));Assert.That(game.Elapsed,Is.Zero);
            game.Tick(.01f);
            var absorption=game.EventBuffer.Single(e=>e.Kind==SimulationEventKind.BlotAbsorbed);
            Assert.That(absorption.EntityId,Is.EqualTo(game.Enemies[0].Id));Assert.That(absorption.EnemyKind,Is.EqualTo(EnemyKind.Runner));
            Assert.That(absorption.EnemyDrawingTier,Is.EqualTo(DrawingTier.Hatched));Assert.That(absorption.SimulationSeconds,Is.EqualTo(game.Elapsed));
        }

        [Test] public void TrialRetainsShortEventsBetweenSnapshotsWithOwnedBoundedCollections()
        {
            var report=TrialRunner.Run(new TowerBlueprint("gun","Gun",ChassisKind.Standard,StructureKind.Gun),TrialScenario.Crowd,40);
            long sequence=0;bool between=false,action=false;
            for(int i=1;i<report.Frames.Count;i++)
            {
                var frame=report.Frames[i];Assert.That(frame.Events.Count,Is.LessThanOrEqualTo(TrialRunner.MaximumFrameEvents));
                foreach(var e in frame.Events)
                {
                    Assert.That(e.Sequence,Is.GreaterThan(sequence));sequence=e.Sequence;
                    Assert.That(e.SimulationSeconds,Is.GreaterThan(report.Frames[i-1].Seconds));
                    Assert.That(e.SimulationSeconds,Is.LessThanOrEqualTo(frame.Seconds));
                    if(e.SimulationSeconds<frame.Seconds)between=true;
                    if(e.Kind==SimulationEventKind.WeaponActionStarted)action=true;
                }
                if(frame.Events.Count>0)Assert.Throws<NotSupportedException>(()=>((IList<SimulationEvent>)frame.Events)[0]=default);
            }
            Assert.That(between,Is.True);Assert.That(action,Is.True);
        }
    }
}
