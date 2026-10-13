using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ProgressionRulesTests
    {
        static GameBalance RulesBalance()
        {
            var balance = GameBalance.CreateDefault();
            balance.PassiveInkPerSecond = 0;
            balance.EarlyLaunchInkPerSecond = 0;
            balance.WaveInkGrant = 0;
            balance.PlanningSeconds = balance.IntermissionSeconds = 100;
            balance.StartingInk = 1000;
            balance.Waves.Clear();
            for (int i = 0; i < 8; i++) balance.Waves.Add(new WaveDefinition { Count = 0 });
            balance.Structure(StructureKind.Gun).BuildSeconds = 0;
            return balance;
        }

        static void CompleteEmptyWave(GameSession session)
        {
            session.StartNextWave();
            session.Tick(.01f);
        }

        static void ReachUpgrade(GameSession session)
        {
            CompleteEmptyWave(session);
            CompleteEmptyWave(session);
            Assert.That(session.PendingUpgrade, Is.True);
        }

        static StructureState BuildGun(GameSession session)
        {
            var cell = new Cell(session.BaseCell.X + 4, session.BaseCell.Y + 4);
            Assert.That(session.TryBuild(new[] { cell }, StructureKind.Gun, MaterialTier.Pencil, out string reason), Is.True, reason);
            return session.StructureAt(cell);
        }

        [Test] public void UpgradeChoicePreservesTheWholePreparationCountdown()
        {
            var balance = RulesBalance();
            balance.IntermissionSeconds = 2;
            var session = new GameSession(balance, starterDefenses: false);
            ReachUpgrade(session);
            session.Tick(5);
            Assert.That(session.Countdown, Is.EqualTo(2), "Reading upgrade choices must not consume preparation time.");
            session.ChooseUpgrade(RunUpgrade.RapidFire);
            session.Tick(.5f);
            Assert.That(session.WaveIndex, Is.EqualTo(2));
            Assert.That(session.Countdown, Is.EqualTo(1.5f).Within(.001f));
            session.Tick(1.6f);
            Assert.That(session.WaveIndex, Is.EqualTo(3));
        }

        static EnemyState AddTarget(GameSession session, StructureState tower)
        {
            var enemy = new EnemyState
            {
                Id = 1001, Kind = EnemyKind.Doodle, Health = 1000, MaxHealth = 1000,
                Position = new Point2(tower.Cell.X + 2.5f, tower.Cell.Y + .5f)
            };
            ((List<EnemyState>)session.Enemies).Add(enemy);
            return enemy;
        }

        [Test]
        public void ProgressionDefaultsAndOwnedSnapshotPreserveHistoricalRules()
        {
            var rules = new ProgressionRules();
            Assert.That(rules.RapidFireMultiplier, Is.EqualTo(1.22f));
            Assert.That(rules.DeepInkDamageMultiplier, Is.EqualTo(1.18f));
            Assert.That(rules.DeepInkHealthMultiplier, Is.EqualTo(1.25f));
            Assert.That(rules.SalvageRefundBonus, Is.EqualTo(.12f));
            Assert.That(rules.SalvageSupplyMultiplier, Is.EqualTo(1.25f));
            Assert.That(rules.SalvageImmediateInk, Is.EqualTo(60));
            Assert.That(rules.MaximumRefundFraction, Is.EqualTo(.95f));
            Assert.That(rules.LegacyHealthGrowthPerWave, Is.EqualTo(.06f));
            Assert.That(rules.LegacyHealthGrowthStartsAfterWave, Is.EqualTo(3));
            Assert.That(rules.EnemySpeedScaleMinimum, Is.EqualTo(.92f));
            Assert.That(rules.EnemySpeedScaleVariation, Is.EqualTo(.16f));
            Assert.That(rules.UpgradeEveryWaves, Is.EqualTo(2));
            Assert.That(rules.LastUpgradeWave, Is.EqualTo(6));
            Assert.That(rules.WellThreatFraction, Is.EqualTo(.3f));
            Assert.That(rules.RequireAssignedEnemyRewards, Is.False);
            Assert.That(rules.SalvageWaveRewardMultiplier, Is.EqualTo(1));

            int value = 1;
            foreach (FieldInfo field in typeof(ProgressionRules).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(int)) field.SetValue(rules, value++ * 3);
                else if (field.FieldType == typeof(float)) field.SetValue(rules, value++ * .17f);
                else if (field.FieldType == typeof(bool)) field.SetValue(rules, true);
            }
            ProgressionRules snapshot = rules.CreateSnapshot();
            Assert.That(snapshot, Is.Not.SameAs(rules));
            foreach (FieldInfo field in typeof(ProgressionRules).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(field.GetValue(snapshot), Is.EqualTo(field.GetValue(rules)), field.Name);
            rules.RapidFireMultiplier = 999;
            Assert.That(snapshot.RapidFireMultiplier, Is.Not.EqualTo(999));
        }

        [Test]
        public void ConfiguredUpgradeCadenceChangesOnlyTheRequestedWaveGate()
        {
            var balance = RulesBalance();
            balance.Progression.UpgradeEveryWaves = 3;
            balance.Progression.LastUpgradeWave = 3;
            var session = new GameSession(balance, starterDefenses: false);
            for (int wave = 1; wave < 8; wave++)
            {
                CompleteEmptyWave(session);
                Assert.That(session.PendingUpgrade, Is.EqualTo(wave == 3), "Wave " + wave);
                if (session.PendingUpgrade) session.ChooseUpgrade(RunUpgrade.RapidFire);
            }
        }

        [Test]
        public void ConfiguredRapidFireChangesTheActualWeaponCooldown()
        {
            var balance = RulesBalance();
            balance.Structure(StructureKind.Gun).ShotInterval = .5f;
            balance.Progression.RapidFireMultiplier = 2;
            var session = new GameSession(balance, starterDefenses: false);
            ReachUpgrade(session);
            session.ChooseUpgrade(RunUpgrade.RapidFire);
            StructureState tower = BuildGun(session);
            EnemyState target = AddTarget(session, tower);
            session.StartNextWave();
            session.Tick(.01f);
            Assert.That(target.Health, Is.LessThan(1000));
            Assert.That(tower.Cooldown, Is.EqualTo(.25f).Within(.00001f));
        }

        [Test]
        public void ConfiguredDeepInkChangesExistingAndNewHealthAndActualDamage()
        {
            var balance = RulesBalance();
            balance.Progression.DeepInkDamageMultiplier = 1.5f;
            balance.Progression.DeepInkHealthMultiplier = 1.7f;
            var session = new GameSession(balance, starterDefenses: false);
            StructureState existing = BuildGun(session);
            existing.Health = existing.MaxHealth / 2;
            ReachUpgrade(session);
            session.ChooseUpgrade(RunUpgrade.DeepInk);
            Assert.That(existing.MaxHealth, Is.EqualTo(55 * 1.7f).Within(.0001f));
            Assert.That(existing.Health / existing.MaxHealth, Is.EqualTo(.5f));
            Assert.That(session.TryBuild(new[] { new Cell(existing.Cell.X + 1, existing.Cell.Y) }, StructureKind.Gun,
                MaterialTier.Pencil, out string reason), Is.True, reason);
            Assert.That(session.Structures[1].MaxHealth, Is.EqualTo(55 * 1.7f).Within(.0001f));
            // Disable the second tower so damage isolates the upgraded weapon.
            session.Structures[1].Health = 0;
            EnemyState target = AddTarget(session, existing);
            session.StartNextWave();
            session.Tick(.01f);
            Assert.That(1000 - target.Health, Is.EqualTo(18).Within(.0001f));
        }

        [Test]
        public void ConfiguredSalvageChangesImmediateInkSupplyAndPaidRefund()
        {
            var balance = RulesBalance();
            balance.Progression.SalvageImmediateInk = 17;
            balance.Progression.SalvageSupplyMultiplier = 2;
            balance.Progression.SalvageRefundBonus = .03f;
            balance.Material(MaterialTier.Pencil).RefundFraction = .2f;
            balance.PassiveInkPerSecond = 4;
            var session = new GameSession(balance, starterDefenses: false);
            ReachUpgrade(session);
            int before = session.Ink;
            session.ChooseUpgrade(RunUpgrade.Salvage);
            Assert.That(session.Ink, Is.EqualTo(before + 17));
            session.Tick(1);
            Assert.That(session.Ink, Is.EqualTo(before + 25));
            StructureState tower = BuildGun(session);
            Assert.That(session.TryErase(tower.Cell, out int refund), Is.True);
            Assert.That(refund, Is.EqualTo(9));
        }

        [Test]
        public void ConfiguredMaximumRefundStillUsesActualPaidCostAndHealth()
        {
            var balance = RulesBalance();
            balance.Progression.MaximumRefundFraction = .6f;
            balance.Material(MaterialTier.Pencil).RefundFraction = 1;
            var session = new GameSession(balance, starterDefenses: false);
            StructureState tower = BuildGun(session);
            tower.Health *= .5f;
            Assert.That(session.TryErase(tower.Cell, out int refund), Is.True);
            Assert.That(refund, Is.EqualTo(12));
            Assert.That(session.Ink, Is.EqualTo(972));
        }

        [Test]
        public void ConfiguredLegacyWaveGrowthChangesSpawnedEnemyHealth()
        {
            var balance = RulesBalance();
            balance.Progression.LegacyHealthGrowthStartsAfterWave = 0;
            balance.Progression.LegacyHealthGrowthPerWave = .5f;
            balance.Waves[0].Count = 1;
            var session = new GameSession(balance, starterDefenses: false);
            session.StartNextWave();
            session.Tick(.01f);
            Assert.That(session.Enemies[0].MaxHealth, Is.EqualTo(33));
        }

        [Test]
        public void ConfiguredSpeedJitterChangesRealMovementWithoutChangingTheEnemyRole()
        {
            var balance = RulesBalance();
            balance.Progression.EnemySpeedScaleMinimum = .25f;
            balance.Progression.EnemySpeedScaleVariation = 0;
            balance.Waves[0].Count = 1;
            balance.Enemy(EnemyKind.Doodle).Speed = 1;
            var session = new GameSession(balance, starterDefenses: false);
            session.StartNextWave();
            session.Tick(.01f);
            Point2 before = session.Enemies[0].Position;
            session.Tick(.02f);
            Point2 after = session.Enemies[0].Position;
            float dx = after.X - before.X, dy = after.Y - before.Y;
            Assert.That((float)Math.Sqrt(dx * dx + dy * dy), Is.EqualTo(.005f).Within(.00001f));
            Assert.That(session.Enemies[0].Kind, Is.EqualTo(EnemyKind.Doodle));
        }

        [TestCase(false, 27f)]
        [TestCase(true, 54f)]
        public void GnawerUsesItsConfiguredStructureDamageMultiplierInAnActualWallHit(bool custom, float expectedDamage)
        {
            var balance = RulesBalance();
            balance.Structure(StructureKind.Wall).BuildSeconds = 0;
            balance.Structure(StructureKind.Wall).MaxHealth = 1000;
            balance.Waves[0].Count = 1;
            balance.Waves[0].GnawerFraction = 1;
            if (custom) balance.Enemy(EnemyKind.Gnawer).StructureDamageMultiplier = 3;
            var session = new GameSession(balance, starterDefenses: false);
            var ring = new List<Cell>();
            for (int x = session.BaseCell.X - 2; x <= session.BaseCell.X + 2; x++)
            {
                ring.Add(new Cell(x, session.BaseCell.Y - 2));
                ring.Add(new Cell(x, session.BaseCell.Y + 2));
            }
            for (int y = session.BaseCell.Y - 1; y <= session.BaseCell.Y + 1; y++)
            {
                ring.Add(new Cell(session.BaseCell.X - 2, y));
                ring.Add(new Cell(session.BaseCell.X + 2, y));
            }
            Assert.That(session.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out string reason), Is.True, reason);
            session.StartNextWave();
            session.Tick(.01f);
            Assert.That(session.Enemies[0].Kind, Is.EqualTo(EnemyKind.Gnawer));
            session.Enemies[0].Position = new Point2(session.BaseCell.X - 2.5f, session.BaseCell.Y + .5f);
            session.Tick(.01f);
            StructureState wall = session.StructureAt(new Cell(session.BaseCell.X - 2, session.BaseCell.Y));
            Assert.That(1000 - wall.Health, Is.EqualTo(expectedDamage).Within(.0001f));
        }
    }
}
