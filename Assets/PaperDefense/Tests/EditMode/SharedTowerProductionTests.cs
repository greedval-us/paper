using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class SharedTowerProductionTests
    {
        static StructureState Tower(StructureKind second = StructureKind.Gun, InkColor color = InkColor.None)
            => new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Gun,
                Tier = MaterialTier.Pencil, IsComplete = true, Health = 100, MaxHealth = 100, Color = color, PrimaryTargetId = 1, SecondaryTargetId = 1,
                Blueprint = new TowerBlueprint("shared", "Shared production", ChassisKind.Heavy, StructureKind.Gun, second) };
        static EnemyState Enemy(int id = 1, float y = 10.5f) => new EnemyState
        { Id = id, Position = new Point2(11.5f, y), Health = 100000, MaxHealth = 100000 };
        static GameBalance DynamicBalance()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.DynamicFeed = true;
            balance.Construction.DynamicFeedCapacity = 1.6f;
            return balance;
        }
        static RunStatistics Run(GameBalance balance, StructureState tower, int ticks)
        {
            var weapons = new WeaponSystem(balance); var stats = new RunStatistics();
            var structures = new List<StructureState> { tower }; var enemies = new List<EnemyState> { Enemy() };
            var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer(); const float delta = 1f / 30;
            for (int i = 0; i < ticks; i++)
            {
                if (!balance.Construction.DynamicFeed)
                { tower.Cooldown = Math.Max(0, tower.Cooldown - delta); tower.SecondaryCooldown = Math.Max(0, tower.SecondaryCooldown - delta); }
                weapons.Fire(structures, enemies, shots, 1, 1, delta, i + 1, stats, events, (i + 1) * (double)delta);
            }
            return stats;
        }

        [Test] public void AnIdleMountDoesNotReduceTheActiveMountsSupply()
        {
            var balance = DynamicBalance(); balance.Structure(StructureKind.Mortar).Range = 0;
            var stats = Run(balance, Tower(StructureKind.Mortar), 900);
            Assert.That(stats.Launches(StructureKind.Gun), Is.EqualTo(70));
            Assert.That(stats.Launches(StructureKind.Mortar), Is.Zero);
        }

        [Test] public void TwoActiveGunsShareAboutOnePointSixTimesSingleGunProduction()
        {
            var balance = DynamicBalance(); var stats = Run(balance, Tower(), 900);
            Assert.That(stats.Launches(StructureKind.Gun), Is.EqualTo(114));
            Assert.That(TowerCatalog.FeedMultiplier(Tower().Blueprint, balance, 1), Is.EqualTo(1));
            Assert.That(TowerCatalog.FeedMultiplier(Tower().Blueprint, balance, 2), Is.EqualTo(.8f));
        }

        [Test] public void SharedProductionCannotStarveTheSlowerWeapon()
        {
            var stats = Run(DynamicBalance(), Tower(StructureKind.Mortar), 900);
            Assert.That(stats.Launches(StructureKind.Gun), Is.EqualTo(57));
            Assert.That(stats.Launches(StructureKind.Mortar), Is.EqualTo(14));
        }

        [Test] public void WaitingWithoutTargetsCannotAccumulateAnUnboundedVolley()
        {
            var balance = DynamicBalance(); var tower = Tower(); var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { tower }; var enemies = new List<EnemyState>();
            var stats = new RunStatistics(); var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer();
            for (int i = 0; i < 1000; i++) weapons.Fire(structures, enemies, shots, 1, 1, 1, i, stats, events, i);
            enemies.Add(Enemy()); tower.PrimaryTargetId = tower.SecondaryTargetId = 1;
            weapons.Fire(structures, enemies, shots, 1, 1, 1f / 30, 1001, stats, events, 1001);
            Assert.That(stats.Launches(StructureKind.Gun), Is.EqualTo(2));
            weapons.Fire(structures, enemies, shots, 1, 1, 1f / 30, 1002, stats, events, 1001 + 1d / 30);
            Assert.That(stats.Launches(StructureKind.Gun), Is.EqualTo(2));
        }

        [Test] public void HistoricalSupplyKeepsItsFixedPenaltyWhenAMountIsIdle()
        {
            var balance = GameBalance.CreateDefault(); balance.Structure(StructureKind.Mortar).Range = 0;
            Assert.That(Run(balance, Tower(StructureKind.Mortar), 900).Launches(StructureKind.Gun), Is.EqualTo(57));
        }

        [Test] public void TwoMountsShareThreeDistinctColorTargetsAndNeverLimitOrdinaryDamage()
        {
            var balance = GameBalance.CreateDefault(); balance.Effects.LimitTowerColor = true;
            balance.Effects.ColorTargetsPerWindow = 3; balance.Effects.ColorWindowSeconds = 1;
            balance.Structure(StructureKind.Gun).Damage = 1;
            var tower = Tower(color: InkColor.Blue); var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { tower }; var enemies = new List<EnemyState>();
            for (int i = 1; i <= 4; i++) { var enemy = Enemy(i); enemy.Position = new Point2(50, 30); enemies.Add(enemy); }
            var stats = new RunStatistics(); var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer();
            for (int i = 0; i < enemies.Count; i++)
            {
                if (i > 0) enemies[i - 1].Position = new Point2(50, 30);
                enemies[i].Position = new Point2(11.5f, 10.5f); tower.PrimaryTargetId = tower.SecondaryTargetId = enemies[i].Id;
                tower.Cooldown = tower.SecondaryCooldown = 0;
                weapons.Fire(structures, enemies, shots, 1, 1, .1f, i + 1, stats, events, i * .1);
                Assert.That(enemies[i].BlueRemaining, Is.EqualTo(i < 3 ? 3 : 0));
                Assert.That(enemies[i].Health, Is.EqualTo(99998));
            }
            Assert.That(stats.Damage(StructureKind.Gun), Is.EqualTo(8));
            tower.Cooldown = tower.SecondaryCooldown = 0;
            weapons.Fire(structures, enemies, shots, 1, 1, .1f, 5, stats, events, 1.01);
            Assert.That(enemies[3].BlueRemaining, Is.EqualTo(3), "A later impact starts the next shared window.");
        }

        [Test] public void ATargetReceivesAtMostOneColorRefreshInTheSharedWindow()
        {
            var balance = GameBalance.CreateDefault(); balance.Effects.LimitTowerColor = true;
            var tower = Tower(color: InkColor.Blue); var enemy = Enemy(); var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { tower }; var enemies = new List<EnemyState> { enemy };
            var stats = new RunStatistics(); var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer();
            weapons.Fire(structures, enemies, shots, 1, 1, .1f, 1, stats, events, 0);
            EnemyEffects.Advance(enemy, .2f, stats, balance.Effects);
            tower.Cooldown = tower.SecondaryCooldown = 0;
            weapons.Fire(structures, enemies, shots, 1, 1, .1f, 2, stats, events, .2);
            Assert.That(enemy.BlueRemaining, Is.EqualTo(2.8f).Within(.00001f));
            Assert.That(stats.Damage(StructureKind.Gun), Is.EqualTo(48));
        }

        [Test] public void FlyingShellsShareSourceBudgetAndKeepLaunchColorAfterRepaintAndSourceDeath()
        {
            var balance = GameBalance.CreateDefault(); balance.Effects.LimitTowerColor = true;
            var mortar = balance.Structure(StructureKind.Mortar); mortar.Damage = 5; mortar.FlightSeconds = .8f;
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Mortar,
                Tier = MaterialTier.Pencil, Health = 100, IsComplete = true, Color = InkColor.Blue, PrimaryTargetId = 1, SecondaryTargetId = 1,
                Blueprint = new TowerBlueprint("dual-mortar", "Two shells", ChassisKind.Heavy, StructureKind.Mortar, StructureKind.Mortar) };
            var weapons = new WeaponSystem(balance); var structures = new List<StructureState> { tower };
            var enemies = new List<EnemyState>(); for (int i = 0; i < 5; i++) enemies.Add(Enemy(i + 1, 10.5f + i * .1f));
            var stats = new RunStatistics(); var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer();
            weapons.Fire(structures, enemies, shots, 1, 1, .01f, 1, stats, events, 0);
            Assert.That(weapons.Projectiles.Count, Is.EqualTo(2));
            tower.Color = InkColor.Green; tower.Health = 0; structures.Clear();
            balance.Effects.BlueDuration = 9; balance.Effects.BlueSpeedMultiplier = .1f;
            weapons.Fire(structures, enemies, shots, 1, 1, .8f, 2, stats, events, .8);
            int colored = 0;
            foreach (var enemy in enemies)
            {
                Assert.That(enemy.Health, Is.EqualTo(99990)); Assert.That(enemy.GreenRemaining, Is.Zero);
                if (enemy.BlueRemaining <= 0) continue;
                colored++; Assert.That(enemy.BlueRemaining, Is.EqualTo(3));
                Assert.That(EnemyEffects.MovementMultiplier(enemy, balance.Effects), Is.EqualTo(.65f));
            }
            Assert.That(colored, Is.EqualTo(3)); Assert.That(stats.Damage(StructureKind.Mortar), Is.EqualTo(50));
        }

        [Test] public void HitscanAndDelayedImpactUseOneSharedSourceColorBudget()
        {
            var balance = GameBalance.CreateDefault(); balance.Effects.LimitTowerColor = true;
            balance.Structure(StructureKind.Gun).Damage = 1; balance.Structure(StructureKind.Mortar).Damage = 1;
            var tower = Tower(StructureKind.Mortar, InkColor.Blue); var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { tower }; var enemies = new List<EnemyState>();
            for (int i = 0; i < 5; i++) enemies.Add(Enemy(i + 1, 10.5f + i * .1f));
            var stats = new RunStatistics(); var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer();
            weapons.Fire(structures, enemies, shots, 1, 1, .01f, 1, stats, events, 0);
            structures.Clear();
            weapons.Fire(structures, enemies, shots, 1, 1, .8f, 2, stats, events, .8);
            int colored = 0; foreach (var enemy in enemies) if (enemy.BlueRemaining > 0) colored++;
            Assert.That(colored, Is.EqualTo(3)); Assert.That(stats.Damage(StructureKind.Gun), Is.EqualTo(1));
            Assert.That(stats.Damage(StructureKind.Mortar), Is.EqualTo(5));
        }
    }
}
