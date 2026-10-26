using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class WeaponCycleTests
    {
        static ResolvedWeapon Weapon(int rounds = 6, float interval = .42f, float reload = .42f, float floor = .05f)
            => new ResolvedWeapon(0, "core.gun", AssemblyWeaponFamily.Gun, AttackKind.Single, 12, 8, 0,
                interval, rounds, reload, 0, 1, 0, 0, 0, 0, 1, 0, floor);

        static ResolvedWeapon CatalogWeapon(string magazine = "standard", string trigger = "standard")
            => AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(
                AssemblyResolverFixture.Weapon(magazine: magazine, trigger: trigger))).Weapons[0];

        static void SameState(WeaponCycleState actual, WeaponCycleState expected)
        {
            Assert.That(actual.Initialized, Is.EqualTo(expected.Initialized));
            Assert.That(actual.RoundsRemaining, Is.EqualTo(expected.RoundsRemaining));
            Assert.That(actual.IsReloading, Is.EqualTo(expected.IsReloading));
            Assert.That(actual.RemainingWork, Is.EqualTo(expected.RemainingWork).Within(.000001));
        }

        static List<float> Collect(ResolvedWeapon weapon, int steps, float delta, out WeaponCycleState state, float feed = 1)
        {
            state = new WeaponCycleState(); var times = new List<float>(); double elapsed = 0;
            for (int step = 0; step < steps; step++)
            {
                var result = WeaponCycleRules.Step(state, weapon, delta, true, feed);
                foreach (float offset in result.ShotOffsets) times.Add((float)(elapsed + offset));
                state = result.State; elapsed += delta;
            }
            return times;
        }

        [Test] public void CreatedMountStartsWithAFullReadyMagazineWithoutFiring()
        {
            var weapon = Weapon(3, 1, 2);
            var state = WeaponCycleRules.CreateState(weapon);
            Assert.That(state.Initialized, Is.True);
            Assert.That(state.RoundsRemaining, Is.EqualTo(3));
            Assert.That(state.IsReloading, Is.False);
            Assert.That(state.RemainingWork, Is.Zero);
            var paused = WeaponCycleRules.Step(state, weapon, 0, true, 1);
            Assert.That(paused.ShotOffsets, Is.Empty);
            SameState(paused.State, state);
        }

        [Test] public void StandardMagazine_RepeatsAt252Seconds()
        {
            var weapon = CatalogWeapon();
            var result = WeaponCycleRules.Step(new WeaponCycleState(), weapon, 2.52f, true, 1);
            Assert.That(result.ShotOffsets.Count, Is.EqualTo(7));
            Assert.That(result.ShotOffsets[0], Is.Zero);
            Assert.That(result.ShotOffsets[6], Is.EqualTo(2.52f).Within(.0001f));
            Assert.That(result.State.RoundsRemaining, Is.EqualTo(5));
        }

        [Test] public void BurstMagazine_RepeatsAt126Seconds()
        {
            var result = WeaponCycleRules.Step(new WeaponCycleState(), CatalogWeapon(trigger: "burst"), 1.26f, true, 1);
            Assert.That(result.ShotOffsets.Count, Is.EqualTo(4));
            Assert.That(result.ShotOffsets.Take(3), Is.EqualTo(new[] { 0f, .189f, .378f }).Within(.0001f));
            Assert.That(result.ShotOffsets[3], Is.EqualTo(1.26f).Within(.0001f));
        }

        [Test] public void DrumMagazine_RepeatsAt5187Seconds()
        {
            var result = WeaponCycleRules.Step(new WeaponCycleState(), CatalogWeapon(magazine: "drum"), 5.187f, true, 1);
            Assert.That(result.ShotOffsets.Count, Is.EqualTo(13));
            Assert.That(result.ShotOffsets[1], Is.EqualTo(.357f).Within(.0001f));
            Assert.That(result.ShotOffsets[12], Is.EqualTo(5.187f).Within(.0001f));
        }

        [Test] public void SingleRoundMagazineWaitsOnlyForReload()
        {
            var result = WeaponCycleRules.Step(new WeaponCycleState(), Weapon(1, 9, 2), 4, true, 1);
            Assert.That(result.ShotOffsets, Is.EqualTo(new[] { 0f, 2f, 4f }));
            Assert.That(result.State.IsReloading, Is.True);
            Assert.That(result.State.RoundsRemaining, Is.Zero);
            Assert.That(result.State.RemainingWork, Is.EqualTo(1));
        }

        [Test] public void EndpointShotsAreIncludedOnceAndTheFollowingStepContinuesTheNextPhase()
        {
            var weapon = Weapon(2, 1, 2);
            var first = WeaponCycleRules.Step(new WeaponCycleState(), weapon, 1, true, 1);
            Assert.That(first.ShotOffsets, Is.EqualTo(new[] { 0f, 1f }));
            Assert.That(first.State.IsReloading, Is.True);
            Assert.That(first.State.RemainingWork, Is.EqualTo(1));
            var second = WeaponCycleRules.Step(first.State, weapon, 2, true, 1);
            Assert.That(second.ShotOffsets, Is.EqualTo(new[] { 2f }));
            Assert.That(second.State.IsReloading, Is.False);
            Assert.That(second.State.RoundsRemaining, Is.EqualTo(1));
        }

        [Test] public void ZeroDeltaNeitherInitializesNorFiresNorAdvancesAnExistingPhase()
        {
            var weapon = Weapon(); var fresh = new WeaponCycleState();
            var initial = WeaponCycleRules.Step(fresh, weapon, 0, true, 1);
            Assert.That(initial.ShotOffsets, Is.Empty); SameState(initial.State, fresh);
            var active = WeaponCycleRules.Step(fresh, weapon, .1f, true, 1);
            var paused = WeaponCycleRules.Step(active.State, weapon, 0, true, 1);
            Assert.That(paused.ShotOffsets, Is.Empty); SameState(paused.State, active.State);
            Assert.That(fresh.Initialized, Is.False);
            Assert.That(fresh.RoundsRemaining, Is.Zero);
        }

        [Test] public void ZeroFeedPausesWorkAndCannotFireEvenWhenFreshAndTargeted()
        {
            var weapon = Weapon(); var fresh = new WeaponCycleState();
            var stopped = WeaponCycleRules.Step(fresh, weapon, 100, true, 0);
            Assert.That(stopped.ShotOffsets, Is.Empty); SameState(stopped.State, fresh);
            var active = WeaponCycleRules.Step(fresh, weapon, .1f, true, 1);
            var paused = WeaponCycleRules.Step(active.State, weapon, 100, true, 0);
            Assert.That(paused.ShotOffsets, Is.Empty); SameState(paused.State, active.State);
        }

        [Test] public void LosingTheTargetAfterTheLastRoundFinishesReloadWithoutSavingAQueueOfShots()
        {
            var weapon = Weapon(2, 1, 2);
            var exhausted = WeaponCycleRules.Step(new WeaponCycleState(), weapon, 1, true, 1);
            Assert.That(exhausted.State.IsReloading, Is.True);
            var idle = WeaponCycleRules.Step(exhausted.State, weapon, 100, false, 1);
            Assert.That(idle.ShotOffsets, Is.Empty);
            Assert.That(idle.State.IsReloading, Is.False);
            Assert.That(idle.State.RoundsRemaining, Is.EqualTo(2));
            Assert.That(idle.State.RemainingWork, Is.Zero);
            var reacquired = WeaponCycleRules.Step(idle.State, weapon, .1f, true, 1);
            Assert.That(reacquired.ShotOffsets, Is.EqualTo(new[] { 0f }));
            Assert.That(reacquired.State.RoundsRemaining, Is.EqualTo(1));
        }

        [Test] public void AnEmptyFieldBeforeAcquisitionCreatesNoPostponedShots()
        {
            var weapon = Weapon();
            var idle = WeaponCycleRules.Step(new WeaponCycleState(), weapon, 100, false, 1);
            Assert.That(idle.ShotOffsets, Is.Empty);
            var acquired = WeaponCycleRules.Step(idle.State, weapon, .1f, true, 1);
            Assert.That(acquired.ShotOffsets, Is.EqualTo(new[] { 0f }));
        }

        [Test] public void TargetChangesDoNotDiscardRoundsOrRestartTheCurrentInterval()
        {
            var weapon = Weapon(3, 1, 2);
            var first = WeaponCycleRules.Step(new WeaponCycleState(), weapon, .25f, true, 1);
            var absent = WeaponCycleRules.Step(first.State, weapon, .25f, false, 1);
            Assert.That(absent.State.RoundsRemaining, Is.EqualTo(2));
            Assert.That(absent.State.IsReloading, Is.False);
            Assert.That(absent.State.RemainingWork, Is.EqualTo(.5));
            var newTarget = WeaponCycleRules.Step(absent.State, weapon, .5f, true, 1);
            Assert.That(newTarget.ShotOffsets, Is.EqualTo(new[] { .5f }));
            Assert.That(newTarget.State.RoundsRemaining, Is.EqualTo(1));
        }

        [Test] public void FeedChangesApplyOnceToTheRemainingNormalizedIntervalWork()
        {
            var weapon = Weapon(2, 1, 2);
            var first = WeaponCycleRules.Step(new WeaponCycleState(), weapon, .25f, true, .5f);
            Assert.That(first.State.RemainingWork, Is.EqualTo(.875));
            var faster = WeaponCycleRules.Step(first.State, weapon, .4375f, true, 2);
            Assert.That(faster.ShotOffsets, Is.EqualTo(new[] { .4375f }));
            Assert.That(faster.State.IsReloading, Is.True);
            Assert.That(faster.State.RemainingWork, Is.EqualTo(1));
            Assert.That(first.State.RemainingWork, Is.EqualTo(.875), "An input state is immutable.");
        }

        [Test] public void FeedChangesApplyOnceDuringReloadAndDoNotAddAnInterval()
        {
            var weapon = Weapon(1, 9, 2);
            var first = WeaponCycleRules.Step(new WeaponCycleState(), weapon, .5f, true, 1);
            Assert.That(first.State.IsReloading, Is.True);
            Assert.That(first.State.RemainingWork, Is.EqualTo(.75));
            var faster = WeaponCycleRules.Step(first.State, weapon, .75f, true, 2);
            Assert.That(faster.ShotOffsets, Is.EqualTo(new[] { .75f }));
            Assert.That(faster.State.RemainingWork, Is.EqualTo(1));
        }

        [Test] public void IntervalFloorStillAppliesWhenFeedIncreases()
        {
            var result = WeaponCycleRules.Step(new WeaponCycleState(), Weapon(3, .01f, 1, .05f), .1f, true, 100);
            Assert.That(result.ShotOffsets, Is.EqualTo(new[] { 0f, .05f, .1f }).Within(.000001f));
        }

        [TestCase("standard", 2.52f, 252)]
        [TestCase("burst", 1.26f, 126)]
        [TestCase("drum", 5.187f, 5187)]
        public void CoarseAndFineStepsProduceTheSameShotsAndFinalState(string kind, float duration, int steps)
        {
            var weapon = kind == "burst" ? CatalogWeapon(trigger: "burst") : kind == "drum" ? CatalogWeapon(magazine: "drum") : CatalogWeapon();
            var coarse = Collect(weapon, 1, duration, out var coarseState);
            var fine = Collect(weapon, steps, duration / steps, out var fineState);
            Assert.That(fine, Is.EqualTo(coarse).Within(.0001f));
            SameState(fineState, coarseState);
        }

        [TestCase(2)] [TestCase(3)]
        public void EachMountOwnsIndependentRoundsAndPhaseProgress(int mounts)
        {
            var weapon = Weapon(2, 1, 2);
            var states = Enumerable.Range(0, mounts).Select(_ => new WeaponCycleState()).ToArray();
            var first = WeaponCycleRules.Step(states[0], weapon, 1, true, 1);
            states[0] = first.State;
            for (int index = 1; index < mounts; index++)
            {
                Assert.That(states[index].Initialized, Is.False);
                var own = WeaponCycleRules.Step(states[index], weapon, .25f, true, .5f);
                Assert.That(own.ShotOffsets, Is.EqualTo(new[] { 0f }));
                Assert.That(own.State.RoundsRemaining, Is.EqualTo(1));
                Assert.That(own.State.RemainingWork, Is.EqualTo(.875));
                states[index] = own.State;
            }
            Assert.That(states[0].RoundsRemaining, Is.Zero);
            Assert.That(states[0].IsReloading, Is.True);
            Assert.That(states[0].RemainingWork, Is.EqualTo(1));
        }

        [Test] public void InputStateAndReturnedShotOffsetsCannotBeMutatedThroughTheResult()
        {
            var fresh = new WeaponCycleState(); var weapon = Weapon(2, 1, 2);
            var result = WeaponCycleRules.Step(fresh, weapon, .5f, true, 1);
            Assert.That(fresh.Initialized, Is.False);
            Assert.That(result.State.Initialized, Is.True);
            Assert.That(result.State.RoundsRemaining, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<float>)result.ShotOffsets)[0] = 99);
            var branch = WeaponCycleRules.Step(result.State, weapon, .25f, true, 1);
            Assert.That(result.State.RemainingWork, Is.EqualTo(.5));
            Assert.That(branch.State.RemainingWork, Is.EqualTo(.25));
        }

        [Test] public void EachShotExposesItsOwnImmutableStateBeforeTheRestOfTheStepAdvances()
        {
            var result = WeaponCycleRules.Step(new WeaponCycleState(), Weapon(2, 1, 2), 1.5f, true, 1);
            Assert.That(result.StatesAfterShots.Count, Is.EqualTo(result.ShotOffsets.Count));
            Assert.That(result.StatesAfterShots[0].RoundsRemaining, Is.EqualTo(1));
            Assert.That(result.StatesAfterShots[0].IsReloading, Is.False);
            Assert.That(result.StatesAfterShots[0].RemainingWork, Is.EqualTo(1));
            Assert.That(result.StatesAfterShots[1].RoundsRemaining, Is.Zero);
            Assert.That(result.StatesAfterShots[1].IsReloading, Is.True);
            Assert.That(result.StatesAfterShots[1].RemainingWork, Is.EqualTo(1));
            Assert.That(result.State.RemainingWork, Is.EqualTo(.75));
            Assert.Throws<NotSupportedException>(() => ((IList<WeaponCycleState>)result.StatesAfterShots)[0] = new WeaponCycleState());
        }

        [Test] public void MissingStateOrWeaponIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => WeaponCycleRules.Step(null, Weapon(), 1, true, 1));
            Assert.Throws<ArgumentNullException>(() => WeaponCycleRules.Step(new WeaponCycleState(), null, 1, true, 1));
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaIsRejected(float delta)
            => Assert.Catch<ArgumentException>(() => WeaponCycleRules.Step(new WeaponCycleState(), Weapon(), delta, true, 1));

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidFeedIsRejected(float feed)
            => Assert.Catch<ArgumentException>(() => WeaponCycleRules.Step(new WeaponCycleState(), Weapon(), 1, true, feed));

        [TestCase("interval", 0f)] [TestCase("interval", -1f)] [TestCase("interval", float.NaN)] [TestCase("interval", float.PositiveInfinity)]
        [TestCase("reload", 0f)] [TestCase("reload", -1f)] [TestCase("reload", float.NaN)] [TestCase("reload", float.PositiveInfinity)]
        [TestCase("floor", 0f)] [TestCase("floor", -1f)] [TestCase("floor", float.NaN)] [TestCase("floor", float.PositiveInfinity)]
        public void InvalidWeaponTimingsAreRejectedBeforeAnyAdvance(string field, float value)
        {
            var weapon = field == "interval" ? Weapon(interval: value) : field == "reload" ? Weapon(reload: value) : Weapon(floor: value);
            Assert.Catch<ArgumentException>(() => WeaponCycleRules.Step(new WeaponCycleState(), weapon, 0, false, 0));
        }

        [TestCase(0)] [TestCase(-1)]
        public void InvalidMagazineSizeIsRejected(int rounds)
            => Assert.Catch<ArgumentException>(() => WeaponCycleRules.Step(new WeaponCycleState(), Weapon(rounds), 1, true, 1));
    }
}
