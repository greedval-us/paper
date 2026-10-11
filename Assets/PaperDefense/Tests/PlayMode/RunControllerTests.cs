using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class RunControllerTests
    {
        readonly List<GameObject> roots = new List<GameObject>();
        GameBalanceAsset config;

        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<GameBalanceAsset>();
            config.ResetToDefaults();
            config.planningSeconds = 1000;
            config.startingInk = 2000;
        }

        [TearDown] public void TearDown()
        {
            foreach (var root in roots) Object.DestroyImmediate(root);
            roots.Clear();
            Object.DestroyImmediate(config);
        }

        RunController Controller()
        {
            var root = new GameObject("Manual frame controller");
            root.SetActive(false);
            roots.Add(root);
            var controller = root.AddComponent<RunController>();
            controller.balanceAsset = config;
            controller.Initialize();
            return controller;
        }

        [Test] public void RenderCadencePreservesStateForCommandsAtTheSameSimulationTicks()
        {
            var frequent = Controller();
            var rare = Controller();
            for (int segment = 0; segment < 8; segment++)
            {
                ApplyCommand(frequent, segment);
                ApplyCommand(rare, segment);
                for (int frame = 0; frame < 120; frame++) frequent.AdvanceFrame(1f / 60, false, 1);
                for (int frame = 0; frame < 20; frame++) rare.AdvanceFrame(FixedStepClock.Step * 3, false, 1);
                Assert.That(rare.Session.TickIndex, Is.EqualTo(frequent.Session.TickIndex));
                Assert.That(Fingerprint(rare.Session), Is.EqualTo(Fingerprint(frequent.Session)), "Segment " + segment);
            }
            Assert.That(frequent.DiscardedSimulationSeconds, Is.Zero);
            Assert.That(rare.DiscardedSimulationSeconds, Is.Zero);
        }

        static void ApplyCommand(RunController run, int segment)
        {
            if (segment == 0) run.Build(new[] { new Cell(12,12), new Cell(13,12) }, StructureKind.Wall, MaterialTier.Pencil, out _);
            if (segment == 1) run.NextWave();
            if (segment == 2) run.Upgrade(new Cell(12,12), out _);
            if (segment == 3) run.Build(new[] { new Cell(10,10) }, StructureKind.Mortar, MaterialTier.Pencil, out _);
            if (segment == 4) run.Erase(new Cell(13,12), out _);
            if (segment == 5) run.SetPriority(new Cell(10,10), TargetPriority.Strongest);
        }

        [Test] public void CatchUpFrameForwardsEventsFromEveryStepExactlyOnce()
        {
            config.weapons[1].range = 100;
            config.weapons[1].damage = .1f;
            config.weapons[1].shotInterval = .05f;
            config.enemies[0].health = 100000;
            config.enemies[0].speed = .1f;
            config.waves = new[] { new GameBalanceAsset.WaveSettings { count = 1, sides = 1, spawnInterval = .005f } };
            var run = Controller();
            var reference = new GameSession(config.CreateBalance());
            var received = new List<SimulationEvent>();
            run.SimulationEventReceived += received.Add;
            run.NextWave(); reference.StartNextWave();
            Assert.That(run.AdvanceFrame(FixedStepClock.Step * 6, false, 1), Is.EqualTo(6));
            for (int tick = 0; tick < 6; tick++) reference.Tick(FixedStepClock.Step);
            Assert.That(received.Count, Is.GreaterThan(1));
            Assert.That(received.Count, Is.EqualTo(reference.EventBuffer.Count));
            var ticks = new HashSet<long>();
            for (int i = 0; i < received.Count; i++)
            {
                Assert.That(received[i].Sequence, Is.EqualTo(reference.EventBuffer[i].Sequence));
                Assert.That(received[i].Tick, Is.EqualTo(reference.EventBuffer[i].Tick));
                Assert.That(received[i].Kind, Is.EqualTo(reference.EventBuffer[i].Kind));
                ticks.Add(received[i].Tick);
            }
            Assert.That(ticks.Count, Is.GreaterThan(1));
            int count = received.Count;
            run.AdvanceFrame(0, false, 1);
            Assert.That(received.Count, Is.EqualTo(count));
        }

        [Test] public void PauseAcceptsBlueprintAndNeverAdvancesItsConstruction()
        {
            var run = Controller();
            run.TogglePause();
            Assert.That(run.Build(new[] { new Cell(12,12) }, StructureKind.Wall, MaterialTier.Pencil, out _), Is.True);
            var before = run.Session.StructureAt(new Cell(12,12)).Value;
            for (int frame = 0; frame < 100; frame++) Assert.That(run.AdvanceFrame(1, true, 2), Is.Zero);
            Assert.That(run.Session.TickIndex, Is.Zero);
            Assert.That(run.Session.StructureAt(before.Cell).Value.BuildRemaining, Is.EqualTo(before.BuildRemaining));
            Assert.That(run.Session.StructureAt(before.Cell).Value.IsComplete, Is.False);
        }

        [Test] public void RestartClearsSelectedUpgradeBonusesAndStatistics()
        {
            config.weapons[1].range = 100;
            config.weapons[1].damage = 1000;
            config.weapons[1].shotInterval = .05f;
            config.enemies[0].health = 1;
            config.waves = new[]
            {
                new GameBalanceAsset.WaveSettings { count = 1, sides = 1, spawnInterval = .005f },
                new GameBalanceAsset.WaveSettings { count = 1, sides = 1, spawnInterval = .005f },
                new GameBalanceAsset.WaveSettings { count = 1, sides = 1, spawnInterval = .005f }
            };
            var run = Controller();
            float initial = run.Session.Structures[0].MaxHealth;
            run.NextWave();
            for (int i = 0; i < 30 && run.Session.Phase == RunPhase.Assault; i++) run.AdvanceFrame(FixedStepClock.Step, false, 1);
            Assert.That(run.Session.Phase, Is.EqualTo(RunPhase.Intermission));
            run.NextWave();
            for (int i = 0; i < 30 && !run.Session.PendingUpgrade; i++) run.AdvanceFrame(FixedStepClock.Step, false, 1);
            Assert.That(run.Session.PendingUpgrade, Is.True);
            run.ChooseUpgrade(RunUpgrade.DeepInk);
            Assert.That(run.Session.Structures[0].MaxHealth, Is.GreaterThan(initial));
            Assert.That(run.Session.Statistics.Upgrades.Count, Is.EqualTo(1));
            run.Restart();
            Assert.That(run.Session.Structures[0].MaxHealth, Is.EqualTo(initial));
            Assert.That(run.Session.Statistics.Upgrades.Count, Is.Zero);
            Assert.That(run.Session.TickIndex, Is.Zero);
            Assert.That(run.Session.Kills, Is.Zero);
            Assert.That(run.SkippedVisualEvents, Is.Zero);
        }

        [Test] public void PresentationContractContainsOnlyReadAccessAndValueViews()
        {
            var run = Controller();
            Assert.That(run.Session, Is.Not.InstanceOf<GameSession>());
            Assert.That(typeof(IRunReadModel).GetMethod("Tick"), Is.Null);
            foreach (var property in typeof(IRunReadModel).GetProperties())
                Assert.That(property.SetMethod, Is.Null, property.Name);
            foreach (var type in new[] { typeof(StructureView), typeof(EnemyView), typeof(ProjectileView),
                typeof(StructureSpec), typeof(MaterialSpec), typeof(EnemySpec), typeof(WaveView), typeof(BreachRecord) })
            {
                Assert.That(type.IsValueType, Is.True, type.Name);
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    Assert.That(field.IsInitOnly, Is.True, type.Name + "." + field.Name);
            }
            Assert.That(run.Session.Enemies, Is.Not.InstanceOf<IList<EnemyView>>());
            foreach (var type in new[] { typeof(GameBalanceView), typeof(RunStatisticsView), typeof(RunReadModel) })
            {
                Assert.That(type.GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty, type.Name);
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Assert.That(property.SetMethod == null || !property.SetMethod.IsPublic, Is.True, type.Name + "." + property.Name);
            }
            foreach (var property in run.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(GameSession)), property.Name);
            foreach (var field in run.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(GameSession)), field.Name);
        }

        static string Fingerprint(IRunReadModel run)
        {
            var text = new StringBuilder();
            text.Append(run.TickIndex).Append('|').Append(run.Phase).Append('|').Append(run.Ink).Append('|').Append(run.Kills)
                .Append('|').Append(run.WaveIndex).Append('|').Append(run.RemainingToSpawn);
            Number(text, run.BaseHealth); Number(text, run.Elapsed); Number(text, run.Countdown);
            for (int i = 0; i < run.Structures.Count; i++)
            {
                var st = run.Structures[i];
                text.Append('|').Append(st.Id).Append(st.Cell).Append(st.Kind).Append(st.Tier).Append(st.IsComplete).Append(st.Priority);
                Number(text, st.Health); Number(text, st.BuildRemaining); Number(text, st.Cooldown);
            }
            for (int i = 0; i < run.Enemies.Count; i++)
            {
                var enemy = run.Enemies[i];
                text.Append('|').Append(enemy.Id).Append(enemy.Kind);
                Number(text, enemy.Health); Number(text, enemy.Position.X); Number(text, enemy.Position.Y);
            }
            for (int i = 0; i < run.Projectiles.Count; i++)
            {
                var shot = run.Projectiles[i];
                text.Append('|').Append(shot.Id); Number(text, shot.Remaining); Number(text, shot.To.X); Number(text, shot.To.Y);
            }
            return text.ToString();
        }

        static void Number(StringBuilder text, float value) => text.Append('|').Append(value.ToString("R", CultureInfo.InvariantCulture));
    }
}
