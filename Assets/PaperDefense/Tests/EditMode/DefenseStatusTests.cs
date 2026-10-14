using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DefenseStatusTests
    {
        static EnemyState Enemy() => new EnemyState { Id = 1, Kind = EnemyKind.Doodle, Health = 1000, MaxHealth = 1000 };
        static TowerBlueprint Armor => new TowerBlueprint("armored", "Броня", ChassisKind.Standard,
            StructureKind.Gun, modules: TowerModules.Armor);

        static ColorEffectPayload Payload(InkColor color, float strength, float duration)
        {
            var rules = new EffectsRules();
            if (color == InkColor.Blue) { rules.BlueSpeedMultiplier = strength; rules.BlueDuration = duration; }
            if (color == InkColor.Green) { rules.GreenDefenseReduction = strength; rules.GreenDuration = duration; }
            if (color == InkColor.Red) { rules.RedDamagePerTick = strength; rules.RedDuration = duration; }
            return ColorEffectPayload.Capture(color, rules);
        }

        [TestCase(0f, 100f)]
        [TestCase(-.5f, 100f)]
        [TestCase(.25f, 75f)]
        [TestCase(.9f, 40f)]
        public void OrdinaryDamageUsesTheSameBoundedDefenseFormula(float defense, float expected)
            => Assert.That(DamageRules.Apply(100, defense, .6f), Is.EqualTo(expected).Within(.0001f));

        [Test]
        public void InvalidRawDamageCannotHealOrPoisonHealth()
        {
            Assert.That(DamageRules.Apply(-1, .25f, .6f), Is.Zero);
            Assert.That(DamageRules.Apply(float.NaN, .25f, .6f), Is.Zero);
            Assert.That(DamageRules.Apply(float.PositiveInfinity, .25f, .6f), Is.Zero);
        }

        [TestCase(.25f, 75f)]
        [TestCase(.9f, 40f)]
        public void AnInterceptedGlobUsesTheArmorAndCommonDefenseCap(float armor, float expectedDamage)
        {
            var balance = GameBalance.CreateDefault(); balance.EnableEnemyMechanics = true;
            balance.Construction.ArmorDefense = armor; balance.Construction.ArmorHealthMultiplier = 1;
            balance.Effects.MaximumDefense = .6f;
            balance.Structure(StructureKind.Gun).MaxHealth = 1000;
            balance.Construction.FoundationHealth = 1000;
            balance.Structure(StructureKind.Gun).BuildSeconds = balance.Construction.ArmorBuildSeconds = 0;
            var session = new GameSession(balance, starterDefenses: false);
            Assert.That(session.TryBuildBlueprint(new[] { new Cell(6, 5) }, Armor, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            StructureState tower = session.Structures[0];
            float before = tower.Health;
            ((List<EnemyGlob>)session.EnemyProjectiles).Add(new EnemyGlob { Id = 1,
                From = new Point2(4.5f, 5.5f), To = new Point2(8.5f, 5.5f), Damage = 100,
                Duration = 0, Remaining = 0, ObjectiveIndex = int.MinValue });
            session.Tick(.01f);
            Assert.That(before - tower.Health, Is.EqualTo(expectedDamage).Within(.0001f));
            Assert.That(session.EnemyProjectiles, Is.Empty);
        }

        [TestCase(.25f, 20.25f)]
        [TestCase(.9f, 10.8f)]
        public void ARealMeleeHitUsesTheSameArmorAndCap(float armor, float expectedDamage)
        {
            var balance = GameBalance.CreateDefault(); balance.Width = 20; balance.Height = 16;
            balance.Construction.ArmorDefense = armor; balance.Construction.ArmorHealthMultiplier = 1;
            balance.Effects.MaximumDefense = .6f;
            balance.Structure(StructureKind.Wall).MaxHealth = balance.Structure(StructureKind.Gun).MaxHealth = 1000;
            balance.Construction.FoundationHealth = 1000;
            balance.Structure(StructureKind.Wall).BuildSeconds = balance.Structure(StructureKind.Gun).BuildSeconds = 0;
            balance.Construction.ArmorBuildSeconds = 0;
            balance.Waves.Clear(); balance.Waves.Add(new WaveDefinition { Count = 1, GnawerFraction = 1 });
            var session = new GameSession(balance, starterDefenses: false);
            var target = new Cell(8, 8);
            var ring = new List<Cell>();
            for (int x = 8; x <= 12; x++) { ring.Add(new Cell(x, 6)); ring.Add(new Cell(x, 10)); }
            for (int y = 7; y <= 9; y++) { if (y != 8) ring.Add(new Cell(8, y)); ring.Add(new Cell(12, y)); }
            Assert.That(session.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out _), Is.True);
            Assert.That(session.TryBuildBlueprint(new[] { target }, Armor, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            StructureState tower = session.StructureAt(target);
            float before = tower.Health;
            session.StartNextWave(); session.Tick(.01f);
            session.Enemies[0].Position = new Point2(7.5f, 8.5f);
            session.Tick(.01f);
            Assert.That(before - tower.Health, Is.EqualTo(expectedDamage).Within(.0001f));
        }

        [Test]
        public void RedCapturesLaunchDurationPulseDamageAndItsExplicitDefenseBypass()
        {
            var rules = new EffectsRules { RedDuration = 1, RedTickSeconds = .25f, RedDamagePerTick = 2,
                RedIgnoresDefense = true, HatchedDefense = .9f };
            ColorEffectPayload launch = ColorEffectPayload.Capture(InkColor.Red, rules);
            rules.RedDuration = 9; rules.RedTickSeconds = .1f; rules.RedDamagePerTick = 99; rules.RedIgnoresDefense = false;
            EnemyState enemy = Enemy(); enemy.DrawingTier = DrawingTier.Hatched;
            EnemyEffects.ApplyHit(enemy, 0, launch, new RunStatistics(), StructureKind.Mortar, rules);
            Assert.That(enemy.RedRemaining, Is.EqualTo(1));
            EnemyEffects.Advance(enemy, 2, new RunStatistics(), rules);
            Assert.That(enemy.Health, Is.EqualTo(992));
            Assert.That(enemy.RedRemaining, Is.Zero);
        }

        [Test]
        public void BlueCapturesLaunchStrengthAndDuration()
        {
            var rules = new EffectsRules { BlueSpeedMultiplier = .4f, BlueDuration = 1 };
            ColorEffectPayload launch = ColorEffectPayload.Capture(InkColor.Blue, rules);
            rules.BlueSpeedMultiplier = .9f; rules.BlueDuration = 9;
            EnemyState enemy = Enemy();
            EnemyEffects.ApplyHit(enemy, 0, launch, new RunStatistics(), StructureKind.Gun, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.4f));
            EnemyEffects.Advance(enemy, 1.01f, new RunStatistics(), rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(1));
        }

        [Test]
        public void GreenCapturesItsReductionAndDurationAndAffectsItsOwnRawHit()
        {
            var rules = new EffectsRules { GreenDefenseReduction = .3f, GreenDuration = 1, HatchedDefense = .5f };
            ColorEffectPayload launch = ColorEffectPayload.Capture(InkColor.Green, rules);
            rules.GreenDefenseReduction = .1f; rules.GreenDuration = 9;
            EnemyState enemy = Enemy(); enemy.DrawingTier = DrawingTier.Hatched;
            Assert.That(EnemyEffects.ApplyHit(enemy, 10, launch, new RunStatistics(), StructureKind.Gun, rules), Is.EqualTo(8).Within(.0001f));
            EnemyEffects.Advance(enemy, 1.01f, new RunStatistics(), rules);
            Assert.That(EnemyEffects.Defense(enemy, rules), Is.EqualTo(.5f));
        }

        [Test]
        public void WeakerSlowHasItsOwnRemainingDurationAndCannotExtendTheStrongerOne()
        {
            EnemyState enemy = Enemy(); var rules = new EffectsRules();
            EnemyEffects.ApplySlow(enemy, .4f, 1);
            EnemyEffects.Advance(enemy, .25f, null, rules);
            EnemyEffects.ApplySlow(enemy, .8f, 3);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.4f));
            EnemyEffects.Advance(enemy, .8f, null, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.8f));
            EnemyEffects.Advance(enemy, 2.25f, null, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(1));
        }

        [Test]
        public void EqualSlowRefreshesOnceAndDoesNotMultiplyWithBlueOrBlot()
        {
            EnemyState enemy = Enemy(); var rules = new EffectsRules(); enemy.BlotRemaining = 10;
            EnemyEffects.ApplyHit(enemy, 0, Payload(InkColor.Blue, .65f, 3), null, StructureKind.Gun, rules);
            EnemyEffects.ApplySlow(enemy, .5f, 1);
            EnemyEffects.Advance(enemy, .75f, null, rules);
            EnemyEffects.ApplySlow(enemy, .5f, 1);
            EnemyEffects.Advance(enemy, .5f, null, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.5f));
            EnemyEffects.Advance(enemy, .51f, null, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.65f));
        }

        [Test]
        public void GreenUsesTheStrongestReductionThenOnlyTheWeakSourcesOwnRemainder()
        {
            EnemyState enemy = Enemy(); enemy.DrawingTier = DrawingTier.Hatched;
            var rules = new EffectsRules { HatchedDefense = .5f };
            EnemyEffects.ApplyHit(enemy, 0, Payload(InkColor.Green, .4f, 1), null, StructureKind.Gun, rules);
            EnemyEffects.ApplyHit(enemy, 0, Payload(InkColor.Green, .1f, 3), null, StructureKind.Gun, rules);
            Assert.That(EnemyEffects.Defense(enemy, rules), Is.EqualTo(.1f).Within(.0001f));
            EnemyEffects.Advance(enemy, 1.1f, null, rules);
            Assert.That(EnemyEffects.Defense(enemy, rules), Is.EqualTo(.4f).Within(.0001f));
            EnemyEffects.Advance(enemy, 2, null, rules);
            Assert.That(EnemyEffects.Defense(enemy, rules), Is.EqualTo(.5f));
        }

        [Test]
        public void RedDoesNotStackOrLetAWeakSourceProlongTheStrongDamage()
        {
            EnemyState enemy = Enemy(); var rules = new EffectsRules();
            EnemyEffects.ApplyHit(enemy, 0, Payload(InkColor.Red, 4, 1), null, StructureKind.Gun, rules);
            EnemyEffects.ApplyHit(enemy, 0, Payload(InkColor.Red, 1, 3), null, StructureKind.Gun, rules);
            EnemyEffects.Advance(enemy, 1, null, rules);
            Assert.That(enemy.Health, Is.EqualTo(984));
            EnemyEffects.Advance(enemy, 2, null, rules);
            Assert.That(enemy.Health, Is.EqualTo(976));
            EnemyEffects.Advance(enemy, 1, null, rules);
            Assert.That(enemy.Health, Is.EqualTo(976));
        }

        [Test]
        public void InvalidSlowParametersAreRejectedBeforeAnyStatusChanges()
        {
            EnemyState enemy = Enemy();
            Assert.Throws<ArgumentOutOfRangeException>(() => EnemyEffects.ApplySlow(enemy, -.1f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EnemyEffects.ApplySlow(enemy, .5f, float.NaN));
            Assert.That(enemy.BlueRemaining, Is.Zero);
        }

        [TestCase(false, .6f, 970f)]
        [TestCase(false, .75f, 965f)]
        [TestCase(true, .6f, 970f)]
        [TestCase(true, .75f, 965f)]
        public void RedPulsesUseDefenseAtThePulseTimeIncludingSimultaneousExpiry(bool historical, float greenDuration, float expected)
        {
            var rules = new EffectsRules { HatchedDefense = .5f, GreenDefenseReduction = .5f,
                GreenDuration = greenDuration, RedDuration = 1, RedDamagePerTick = 10,
                RedTickSeconds = .25f, RedIgnoresDefense = false };
            EnemyState Create()
            {
                var enemy = Enemy(); enemy.DrawingTier = DrawingTier.Hatched;
                if (historical) { enemy.RedRemaining = 1; enemy.GreenRemaining = greenDuration; }
                else
                {
                    EnemyEffects.ApplyHit(enemy, 0, InkColor.Green, null, StructureKind.Gun, rules);
                    EnemyEffects.ApplyHit(enemy, 0, InkColor.Red, null, StructureKind.Gun, rules);
                }
                return enemy;
            }
            var whole = Create(); var split = Create();
            EnemyEffects.Advance(whole, 1, null, rules);
            for (int i = 0; i < 20; i++) EnemyEffects.Advance(split, .05f, null, rules);
            Assert.That(whole.Health, Is.EqualTo(expected).Within(.001f), "Expired green cannot erase its earlier synergy with red.");
            Assert.That(split.Health, Is.EqualTo(whole.Health).Within(.001f));
            Assert.That(whole.GreenRemaining, Is.Zero);
            Assert.That(whole.RedRemaining, Is.Zero);
        }

        [Test]
        public void BlotDefenseProtectsOnlyPulsesBeforeItsExpiry()
        {
            var rules = new EffectsRules { RedDuration = 1, RedDamagePerTick = 10,
                RedTickSeconds = .25f, RedIgnoresDefense = false };
            var enemy = Enemy(); enemy.BlotRemaining = .6f;
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Red, null, StructureKind.Mortar, rules);
            EnemyEffects.Advance(enemy, 1, null, rules, .5f);
            Assert.That(enemy.Health, Is.EqualTo(970).Within(.001f), "Two protected pulses and two unprotected pulses.");
        }
    }
}
