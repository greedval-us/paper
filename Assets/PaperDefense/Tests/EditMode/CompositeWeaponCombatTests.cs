using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class CompositeWeaponCombatTests
    {
        static GameBalance Balance()
        {
            var balance = GameBalance.CreateDefault();
            balance.Construction.AcquisitionSeconds = 0;
            balance.Mechanics.CorpseBlots = true;
            return balance;
        }
        static ResolvedEntry Resolve(string family, GameBalance balance, ComponentCatalog catalog = null, string magazine = "standard")
            => Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(family, magazine: magazine)), balance, catalog);
        static ResolvedEntry Resolve(AssemblyRecipe recipe, GameBalance balance, ComponentCatalog catalog = null)
        {
            var result = AssemblyResolver.Resolve(recipe, catalog ?? AssemblyResolverFixture.Catalog(), balance, MaterialTier.Pencil);
            Assert.That(result.Valid, Is.True, string.Join(",", result.Diagnostics.Select(d => d.Code)));
            return result.Value;
        }
        static EnemyState Enemy(int id, float x, float y = 10.5f, EnemyKind kind = EnemyKind.Doodle)
            => new EnemyState { Id = id, Position = new Point2(x, y), Kind = kind, Health = 1000, MaxHealth = 1000 };

        sealed class Combat
        {
            internal readonly WeaponSystem Weapons;
            internal readonly List<StructureState> Towers = new List<StructureState>();
            internal readonly List<EnemyState> Enemies = new List<EnemyState>();
            internal readonly List<ShotEvent> Shots = new List<ShotEvent>(), AllShots = new List<ShotEvent>();
            internal readonly RunStatistics Statistics = new RunStatistics();
            internal readonly SimulationEventBuffer Events = new SimulationEventBuffer(4096);
            internal StructureState Tower => Towers[0];
            long tick; double seconds;
            internal Combat(ResolvedEntry entry, GameBalance balance, IBlotContext blots = null)
            {
                Weapons = new WeaponSystem(balance, blots);
                Towers.Add(new StructureState(entry) { Id = 1, Cell = new Cell(10, 10), IsComplete = true, BuildRemaining = 0 });
            }
            internal void Step(float delta)
            {
                seconds += delta; Shots.Clear();
                Weapons.Fire(Towers, Enemies, Shots, 1, 1, delta, ++tick, Statistics, Events, seconds);
                AllShots.AddRange(Shots);
            }
            internal void Steps(int count, float delta = .01f) { for (int i = 0; i < count; i++) Step(delta); }
        }

        [Test] public void StructureConstructorOwnsResolvedStatsAndInitializesConstructionState()
        {
            var balance = Balance(); var entry = Resolve("gun", balance);
            var tower = new StructureState(entry);
            Assert.That(tower.ResolvedEntry.Cost, Is.EqualTo(40));
            Assert.That(tower.ResolvedEntry.Weapons[0].Damage, Is.EqualTo(12));
            Assert.That(tower.Tier, Is.EqualTo(MaterialTier.Pencil));
            Assert.That(tower.MaxHealth, Is.EqualTo(55)); Assert.That(tower.Health, Is.EqualTo(55));
            Assert.That(tower.BuildRemaining, Is.EqualTo(1.4f).Within(.00001f));
        }

        [TestCase("gun", 12)] [TestCase("piercer", 20)] [TestCase("mortar", 28)]
        [TestCase("fan", 14)] [TestCase("ricochet", 12)] [TestCase("stapler", 5)] [TestCase("blotter", 0)]
        public void EveryFamilyUsesOwnedDamageAndPublishesItsExistingEventAlias(string family, float damage)
        {
            var balance = Balance(); var entry = Resolve(family, balance);
            var combat = new Combat(entry, balance, new Blots(new Cell(12, 10)));
            var enemy = Enemy(1, family == "mortar" ? 18.5f : 12.5f); combat.Enemies.Add(enemy);
            combat.Step(.01f);
            if (family == "mortar") { combat.Towers.Clear(); combat.Step(.79f); }
            Assert.That(enemy.Health, Is.EqualTo(1000 - damage).Within(.0001f));
            Assert.That(entry.Weapons[0].SourceId, Is.EqualTo("core." + family));
            var started = combat.Events.Single(e => e.Kind == SimulationEventKind.WeaponActionStarted);
            Assert.That(started.WeaponId, Is.EqualTo("weapon." + family));
            Assert.That(started.SourceStructureId, Is.EqualTo(1)); Assert.That(started.MountSlotIndex, Is.Zero);
            Assert.That(started.ActionId, Is.GreaterThan(0));
            Assert.That(combat.Statistics.Launches("weapon." + family), Is.EqualTo(1));
        }

        [Test] public void PiercerHitsAtMostSevenDistinctCollinearVictims()
        {
            var balance = Balance(); var combat = new Combat(Resolve("piercer", balance), balance);
            for (int i = 0; i < 9; i++) combat.Enemies.Add(Enemy(i + 1, 11.5f + i));
            var outsideLine = Enemy(20, 12.5f, 11.5f); combat.Enemies.Add(outsideLine);
            combat.Step(.01f);
            Assert.That(combat.Enemies.Take(7).Select(e => e.Health), Is.All.EqualTo(980));
            Assert.That(combat.Enemies.Skip(7).Select(e => e.Health), Is.All.EqualTo(1000));
            Assert.That(combat.Statistics.Damage("weapon.piercer"), Is.EqualTo(140));
        }

        [Test] public void FanUsesNinetyDegreeConeAndItsRangeBoundaries()
        {
            var balance = Balance(); var combat = new Combat(Resolve("fan", balance), balance); combat.Tower.Priority = TargetPriority.Siege;
            combat.Enemies.AddRange(new[] { Enemy(1, 12.5f, 10.5f, EnemyKind.Gnawer), Enemy(2, 12.5f, 12.5f),
                Enemy(3, 12.5f, 12.51f), Enemy(4, 9.5f), Enemy(5, 16.5f), Enemy(6, 16.51f) });
            combat.Step(.01f);
            Assert.That(combat.Enemies.Select(e => e.Health), Is.EqualTo(new[] { 986, 986, 1000, 1000, 986, 1000 }));
            Assert.That(combat.Shots.Count, Is.EqualTo(3));
        }

        [Test] public void FanHitsAtMostEightVictimsRegardlessOfListOrder()
        {
            var balance = Balance(); var combat = new Combat(Resolve("fan", balance), balance);
            for (int i = 8; i >= 0; i--) combat.Enemies.Add(Enemy(i + 1, 11.5f + i * .2f));
            combat.Step(.01f);
            Assert.That(combat.Enemies.Count(e => e.Health == 986), Is.EqualTo(8));
            Assert.That(combat.Enemies.Single(e => e.Id == 9).Health, Is.EqualTo(1000));
        }

        [Test] public void RicochetMakesAtMostFourDistinctHitsWithThreeUnitJumps()
        {
            var balance = Balance(); var combat = new Combat(Resolve("ricochet", balance), balance);
            for (int i = 0; i < 5; i++) combat.Enemies.Add(Enemy(i + 1, 11.5f + 3 * i));
            combat.Step(.01f);
            Assert.That(combat.Enemies.Select(e => e.Health), Is.EqualTo(new[] { 988, 988, 988, 988, 1000 }));
            Assert.That(combat.Shots.Count, Is.EqualTo(4));
            Assert.That(combat.Shots.Select(s => s.To.X).Distinct().Count(), Is.EqualTo(4));
            Assert.That(combat.Shots.Skip(1).Select(s => s.To.X - s.From.X), Is.All.EqualTo(3));
        }

        [Test] public void RicochetCannotCrossAGapLargerThanThreeEvenInsideInitialRange()
        {
            var balance = Balance(); var combat = new Combat(Resolve("ricochet", balance), balance);
            combat.Enemies.Add(Enemy(1, 11.5f)); combat.Enemies.Add(Enemy(2, 14.501f));
            combat.Step(.01f);
            Assert.That(combat.Enemies[0].Health, Is.EqualTo(988)); Assert.That(combat.Enemies[1].Health, Is.EqualTo(1000));
        }

        [Test] public void MortarRejectsTargetsInsideSixAndLaunchesAtTheExactBoundary()
        {
            var balance = Balance(); var combat = new Combat(Resolve("mortar", balance), balance);
            combat.Enemies.Add(Enemy(1, 16.49f)); combat.Step(.01f);
            Assert.That(combat.Weapons.Projectiles, Is.Empty);
            combat.Enemies.Add(Enemy(2, 16.5f)); combat.Step(.01f);
            Assert.That(combat.Weapons.Projectiles.Count, Is.EqualTo(1));
            Assert.That(combat.Weapons.Projectiles[0].To.X, Is.EqualTo(16.5f));
            Assert.That(combat.Weapons.Projectiles[0].Duration, Is.EqualTo(.8f));
            Assert.That(combat.Weapons.Projectiles[0].Remaining, Is.EqualTo(.79f).Within(.00001f));
            Assert.That(combat.Enemies.Select(e => e.Health), Is.All.EqualTo(1000));
        }

        [Test] public void MortarHitsCurrentOccupantsOfItsFixedPointAfterFlightWithoutEightTargetCap()
        {
            var balance = Balance(); var combat = new Combat(Resolve("mortar", balance), balance);
            var original = Enemy(1, 18.5f); combat.Enemies.Add(original); combat.Step(.01f);
            original.Position = new Point2(23.5f, 10.5f);
            var arrivals = Enumerable.Range(0, 10).Select(i => Enemy(100 + i, 16.7f + .4f * i)).ToArray();
            combat.Enemies.AddRange(arrivals); combat.Towers.Clear(); combat.Step(.78f);
            Assert.That(arrivals.Select(e => e.Health), Is.All.EqualTo(1000));
            combat.Step(.01f);
            Assert.That(original.Health, Is.EqualTo(1000)); Assert.That(arrivals.Select(e => e.Health), Is.All.EqualTo(972));
            Assert.That(combat.Statistics.Damage("weapon.mortar"), Is.EqualTo(280));
            var impact = combat.Events.Single(e => e.Kind == SimulationEventKind.ProjectileImpact);
            Assert.That(impact.To.X, Is.EqualTo(18.5f)); Assert.That(impact.Radius, Is.EqualTo(2.4f));
        }

        [Test] public void MortarLaunchedInsideACoarseStepImpactsAtItsTrueFlightTime()
        {
            var balance = Balance(); var combat = new Combat(Resolve("mortar", balance), balance);
            var enemy = Enemy(1, 18.5f); combat.Enemies.Add(enemy); combat.Step(1);
            Assert.That(enemy.Health, Is.EqualTo(972));
            Assert.That(combat.Weapons.Projectiles, Is.Empty);
            var launch = combat.Events.Single(e => e.Kind == SimulationEventKind.ProjectileLaunched);
            var impact = combat.Events.Single(e => e.Kind == SimulationEventKind.ProjectileImpact);
            Assert.That(launch.SimulationSeconds, Is.EqualTo(0).Within(.00001));
            Assert.That(impact.SimulationSeconds, Is.EqualTo(.8).Within(.00001));
            Assert.That(impact.SimulationSeconds - launch.SimulationSeconds, Is.EqualTo(.8).Within(.00001));
        }

        [Test] public void MortarLaunchedNearACoarseStepEndRetainsOnlyItsUnelapsedFlight()
        {
            var balance = Balance(); var combat = new Combat(Resolve("mortar", balance), balance);
            combat.Enemies.Add(Enemy(1, 18.5f)); combat.Step(2);
            var launches = combat.Events.Where(e => e.Kind == SimulationEventKind.ProjectileLaunched).ToArray();
            Assert.That(launches.Length, Is.EqualTo(2));
            Assert.That(launches[1].SimulationSeconds, Is.EqualTo(1.9).Within(.00001));
            Assert.That(combat.Weapons.Projectiles.Count, Is.EqualTo(1));
            Assert.That(combat.Weapons.Projectiles[0].Duration, Is.EqualTo(.8f));
            Assert.That(combat.Weapons.Projectiles[0].Remaining, Is.EqualTo(.7f).Within(.00001f));
            Assert.That(combat.Events.Count(e => e.Kind == SimulationEventKind.ProjectileImpact), Is.EqualTo(1));
        }

        [Test] public void IsolatedCompositeMortarFinishesItsFlightAfterTheSourceIsRemoved()
        {
            var balance = Balance(); var combat = new Combat(Resolve("mortar", balance), balance);
            var enemy = Enemy(1, 18.5f); combat.Enemies.Add(enemy);
            combat.Weapons.FireIsolatedMount(combat.Tower, 0, combat.Enemies, combat.Shots,
                .01f, 1, combat.Statistics, combat.Events, .01);
            Assert.That(combat.Weapons.Projectiles.Count, Is.EqualTo(1));
            Assert.That(combat.Weapons.Projectiles[0].Remaining, Is.EqualTo(.79f).Within(.00001f));

            combat.Weapons.FireIsolatedMount(null, 0, combat.Enemies, combat.Shots,
                .8f, 2, combat.Statistics, combat.Events, .81);
            Assert.That(enemy.Health, Is.EqualTo(972));
            Assert.That(combat.Weapons.Projectiles, Is.Empty);
            Assert.That(combat.Events.Single(e => e.Kind == SimulationEventKind.ProjectileImpact).SimulationSeconds,
                Is.EqualTo(.8).Within(.00001));
        }

        [Test] public void AModernGunFiresAtTheStepStartBeforeAnOlderLegacyShellImpacts()
        {
            var balance = Balance(); var combat = new Combat(Resolve("gun", balance), balance);
            var modern = combat.Tower; combat.Towers.Clear();
            combat.Towers.Add(new StructureState { Id = 7, Kind = StructureKind.Mortar, Cell = new Cell(10, 10),
                Health = 55, MaxHealth = 55, IsComplete = true, BuildRemaining = 0 });
            var enemy = Enemy(1, 16.5f); enemy.Health = 12; combat.Enemies.Add(enemy);
            combat.Step(.01f);
            Assert.That(combat.Weapons.Projectiles.Count, Is.EqualTo(1));
            Assert.That(combat.Weapons.Projectiles[0].Remaining, Is.EqualTo(.8f));

            combat.Towers.Clear(); combat.Towers.Add(modern); combat.Step(1);
            Assert.That(combat.Statistics.Launches("weapon.gun"), Is.EqualTo(1));
            Assert.That(combat.Statistics.Damage("weapon.gun"), Is.EqualTo(12));
            Assert.That(combat.Statistics.Damage("weapon.mortar"), Is.Zero);
            var gun = combat.Events.Single(e => e.Kind == SimulationEventKind.Shot && e.WeaponId == "weapon.gun");
            var impact = combat.Events.Single(e => e.Kind == SimulationEventKind.ProjectileImpact);
            Assert.That(gun.SimulationSeconds, Is.EqualTo(.01).Within(.00001));
            Assert.That(gun.SimulationSeconds, Is.LessThan(impact.SimulationSeconds));
            Assert.That(gun.Sequence, Is.LessThan(impact.Sequence));
            Assert.That(modern.GetMountState(0).Cycle.RoundsRemaining, Is.EqualTo(5));
        }

        [Test] public void BlotterCleansWithoutDamagingEnemiesOrRequiringEnemyTargets()
        {
            var balance = Balance(); var marks = new Blots(new Cell(12, 10), new Cell(13, 10), new Cell(20, 10));
            var combat = new Combat(Resolve("blotter", balance), balance, marks);
            var enemy = Enemy(1, 11.5f); combat.Enemies.Add(enemy); combat.Step(.01f);
            Assert.That(enemy.Health, Is.EqualTo(1000)); Assert.That(combat.Statistics.Damage("weapon.blotter"), Is.Zero);
            Assert.That(combat.Statistics.BlotsCleared, Is.EqualTo(2)); Assert.That(marks.Cells, Is.EqualTo(new[] { new Cell(20, 10) }));
            Assert.That(combat.Shots.Single().Radius, Is.EqualTo(2));
        }

        [TestCase(.6f, .6f)] [TestCase(.1f, .4f)]
        public void StaplerSlowUsesTheStrongestRateAndNeverMultipliesBelowPointFour(float sourceSlow, float expected)
        {
            var balance = Balance(); var data = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(data, "core.stapler", AssemblyStat.Slow, ModifierOperation.Set, sourceSlow);
            var entry = Resolve("stapler", balance, AssemblyResolverFixture.Modified(data));
            var combat = new Combat(entry, balance);
            combat.Towers.Add(new StructureState(entry) { Id = 2, Cell = new Cell(10, 11), IsComplete = true, BuildRemaining = 0 });
            var enemy = Enemy(1, 12.5f); combat.Enemies.Add(enemy); combat.Step(.01f);
            Assert.That(enemy.Health, Is.EqualTo(990));
            Assert.That(EnemyEffects.MovementMultiplier(enemy), Is.EqualTo(expected).Within(.00001f));
        }

        [TestCase(2)] [TestCase(3)]
        public void TwoAndThreeMountsKeepIndependentMagazineStatesAndSharedActiveFeed(int count)
        {
            var balance = Balance();
            var mounts = new List<WeaponMountRecipe> {
                new WeaponMountRecipe(0, AssemblyResolverFixture.Weapon(trigger: "burst")),
                new WeaponMountRecipe(1, AssemblyResolverFixture.Weapon()) };
            if (count == 3) mounts.Add(new WeaponMountRecipe(2, AssemblyResolverFixture.Weapon("piercer")));
            var foundation = AssemblyResolverFixture.Foundation(frame: count == 3 ? "battery" : "standard");
            var combat = new Combat(Resolve(new AssemblyRecipe(new TowerRecipe(foundation, mounts)), balance), balance);
            combat.Enemies.Add(Enemy(1, 12.5f)); combat.Steps(100);
            Assert.That(combat.AllShots.Count(s => s.MountSlotIndex == 0), Is.EqualTo(3));
            Assert.That(combat.AllShots.Count(s => s.MountSlotIndex == 1), Is.EqualTo(2));
            if (count == 3) Assert.That(combat.AllShots.Count(s => s.MountSlotIndex == 2), Is.EqualTo(1));
            Assert.That(combat.Tower.GetMountState(0).Cycle, Is.Not.Null);
            Assert.That(combat.Tower.GetMountState(1).Cycle, Is.Not.SameAs(combat.Tower.GetMountState(0).Cycle));
        }

        [Test] public void CoarseStepPreservesSimultaneousShotsFromDifferentMounts()
        {
            var balance = Balance();
            var entry = Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(), AssemblyResolverFixture.Weapon()), balance);
            var coarse = new Combat(entry, balance); var fine = new Combat(entry, balance);
            foreach (var combat in new[] { coarse, fine })
            { var enemy = Enemy(1, 12.5f); enemy.Health = 24; combat.Enemies.Add(enemy); }
            coarse.Step(1); fine.Steps(100);
            foreach (var combat in new[] { coarse, fine })
            {
                Assert.That(combat.AllShots.Select(s => s.MountSlotIndex), Is.EqualTo(new[] { 0, 1 }));
                Assert.That(combat.AllShots.Select(s => s.SimulationSeconds), Is.All.EqualTo(0).Within(.00001));
                Assert.That(combat.Tower.GetMountState(0).Cycle.RoundsRemaining, Is.EqualTo(5));
                Assert.That(combat.Tower.GetMountState(1).Cycle.RoundsRemaining, Is.EqualTo(5));
            }
        }

        [Test] public void CoarseStepPreservesSimultaneousShotsFromDifferentTowers()
        {
            var balance = Balance(); var entry = Resolve("gun", balance);
            var coarse = new Combat(entry, balance); var fine = new Combat(entry, balance);
            foreach (var combat in new[] { coarse, fine })
            {
                combat.Towers.Add(new StructureState(entry) { Id = 2, Cell = new Cell(10, 11), IsComplete = true, BuildRemaining = 0 });
                var enemy = Enemy(1, 12.5f); enemy.Health = 24; combat.Enemies.Add(enemy);
            }
            coarse.Step(1); fine.Steps(100);
            foreach (var combat in new[] { coarse, fine })
            {
                Assert.That(combat.AllShots.Select(s => s.SourceStructureId), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(combat.AllShots.Select(s => s.SimulationSeconds), Is.All.EqualTo(0).Within(.00001));
                Assert.That(combat.Towers.Select(t => t.GetMountState(0).Cycle.RoundsRemaining), Is.All.EqualTo(5));
            }
        }

        [TestCase(false, 3)] [TestCase(true, 2)]
        public void IdleBlotterReleasesFeedButReloadingBlotterKeepsItsShare(bool cleaning, int expectedGunShots)
        {
            var balance = Balance();
            var recipe = AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(), AssemblyResolverFixture.Weapon("blotter"));
            var combat = new Combat(Resolve(recipe, balance), balance, cleaning ? new Blots(new Cell(12, 10)) : new Blots());
            combat.Enemies.Add(Enemy(1, 12.5f)); combat.Steps(100);
            Assert.That(combat.AllShots.Count(s => s.WeaponId == "weapon.gun"), Is.EqualTo(expectedGunShots));
            Assert.That(combat.Statistics.Launches("weapon.blotter"), Is.EqualTo(cleaning ? 1 : 0));
        }

        [Test] public void TargetLossDoesNotRefillAnUnfinishedDrumMagazine()
        {
            var balance = Balance(); var combat = new Combat(Resolve("gun", balance, magazine: "drum"), balance);
            var enemy = Enemy(1, 12.5f); combat.Enemies.Add(enemy); combat.Steps(100);
            Assert.That(combat.AllShots.Count, Is.EqualTo(3));
            combat.Enemies.Clear(); combat.Steps(500); Assert.That(combat.AllShots.Count, Is.EqualTo(3));
            combat.Enemies.Add(enemy); combat.Steps(800);
            Assert.That(combat.AllShots.Count, Is.GreaterThan(12));
            Assert.That(combat.AllShots[12].SimulationSeconds - combat.AllShots[11].SimulationSeconds,
                Is.EqualTo(1.26).Within(.02), "The twelfth overall shot exhausts the original drum despite temporary target loss.");
        }

        [Test] public void CoarseStepDoesNotSpendPredictedRoundsAfterTheOnlyTargetDiesOrRefillForItsReplacement()
        {
            var balance = Balance(); var combat = new Combat(Resolve("gun", balance, magazine: "drum"), balance);
            var first = Enemy(1, 12.5f); first.Health = 1; combat.Enemies.Add(first);
            combat.Step(5);
            Assert.That(first.Health, Is.LessThanOrEqualTo(0));
            Assert.That(combat.AllShots.Count, Is.EqualTo(1), "Predicted later shots have no living target.");
            Assert.That(combat.Tower.GetMountState(0).Cycle.RoundsRemaining, Is.EqualTo(11));
            Assert.That(combat.Tower.GetMountState(0).Cycle.IsReloading, Is.False);

            combat.Enemies.Add(Enemy(2, 12.5f)); combat.Step(.01f);
            Assert.That(combat.AllShots.Count, Is.EqualTo(2));
            Assert.That(combat.Tower.GetMountState(0).Cycle.RoundsRemaining, Is.EqualTo(10),
                "Acquiring a replacement must continue the same drum.");
        }

        [Test] public void AcquisitionCompletedAtTheStepEndFiresAtThatEndpointWithoutWaitingForAnotherFrame()
        {
            var balance = Balance(); balance.Construction.AcquisitionSeconds = .1f;
            var combat = new Combat(Resolve("gun", balance), balance); combat.Enemies.Add(Enemy(1, 12.5f));
            combat.Step(.1f);
            Assert.That(combat.AllShots.Count, Is.EqualTo(1));
            Assert.That(combat.AllShots[0].SimulationSeconds, Is.EqualTo(.1).Within(.00001));
            Assert.That(combat.Tower.GetMountState(0).Cycle.RoundsRemaining, Is.EqualTo(5));
            combat.Step(.01f);
            Assert.That(combat.AllShots.Count, Is.EqualTo(1));
        }

        [TestCase(MaterialTier.Pencil, 12f)] [TestCase(MaterialTier.Ink, 15f)] [TestCase(MaterialTier.Marker, 19.2f)]
        public void MaterialDamageIsAppliedOnceByResolutionAndUsedUnchangedByCombat(MaterialTier material, float expectedDamage)
        {
            var balance = Balance();
            var entry = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(), balance: balance, material: material);
            var combat = new Combat(entry, balance); var enemy = Enemy(1, 12.5f); combat.Enemies.Add(enemy); combat.Step(.01f);
            Assert.That(entry.Weapons[0].Damage, Is.EqualTo(expectedDamage).Within(.0001f));
            Assert.That(1000 - enemy.Health, Is.EqualTo(entry.Weapons[0].Damage).Within(.0001f));
            Assert.That(combat.Statistics.Damage("weapon.gun"), Is.EqualTo(entry.Weapons[0].Damage).Within(.0001f));
            Assert.That(combat.AllShots.Count, Is.EqualTo(1));
        }

        [Test] public void ZeroDeltaDoesNotStartOrAdvanceCompositeAttacks()
        {
            var balance = Balance(); var combat = new Combat(Resolve("gun", balance), balance); combat.Enemies.Add(Enemy(1, 12.5f));
            combat.Step(0); Assert.That(combat.AllShots, Is.Empty);
            combat.Step(.01f); Assert.That(combat.AllShots.Count, Is.EqualTo(1));
            combat.Step(0); Assert.That(combat.AllShots.Count, Is.EqualTo(1));
        }

        sealed class Blots : IBlotContext
        {
            internal readonly List<Cell> Cells;
            internal Blots(params Cell[] cells) { Cells = new List<Cell>(cells); }
            public bool TryFindNearest(Point2 origin, float range, out Cell cell)
            {
                cell = default; bool found = false; double nearest = range * range;
                foreach (var candidate in Cells)
                {
                    double dx = candidate.X + .5 - origin.X, dy = candidate.Y + .5 - origin.Y, square = dx * dx + dy * dy;
                    if (square > nearest) continue;
                    if (!found || square < nearest || candidate.Y < cell.Y || (candidate.Y == cell.Y && candidate.X < cell.X))
                    { cell = candidate; nearest = square; found = true; }
                }
                return found;
            }
            public int Clear(Cell center, float radius)
            {
                int removed = 0;
                for (int i = Cells.Count - 1; i >= 0; i--)
                {
                    float dx = Cells[i].X - center.X, dy = Cells[i].Y - center.Y;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    Cells.RemoveAt(i); removed++;
                }
                return removed;
            }
        }
    }
}
