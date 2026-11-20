using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DefeatTelemetryTests
    {
        static GameBalance Rules()
        {
            var rules = GameBalance.CreateDefault();
            rules.Width = 40; rules.Height = 24; rules.StartingInk = 100000;
            rules.BaseHealth = 1000; rules.EnableEnemyMechanics = true; rules.EnableInkWells = false;
            rules.PlanningSeconds = rules.IntermissionSeconds = 3600;
            rules.PassiveInkPerSecond = rules.EarlyLaunchInkPerSecond = 0;
            rules.WaveInkGrant = 0; rules.Progression.UpgradeEveryWaves = 0;
            rules.Waves = new List<WaveDefinition> { new WaveDefinition { Count = 1, Sides = 1 }, new WaveDefinition { Count = 1, Sides = 1 } };
            foreach (var definition in rules.StructureDefinitions) definition.BuildSeconds = 0;
            foreach (var definition in rules.EnemyDefinitions) { definition.Speed = 0; definition.Damage = 0; definition.MaxHealth = 100000; }
            return rules;
        }

        static object Value(object source, string name)
        {
            var property = source.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, "Missing telemetry property: " + name);
            return property.GetValue(source);
        }
        static object[] Records(object source, string name) => ((IEnumerable)Value(source, name)).Cast<object>().ToArray();
        static T Field<T>(object source, string name) => (T)Value(source, name);
        static float Exposure(GameSession session) => Records(session.Statistics, "PositionActivities").Sum(item => Field<float>(item, "ActiveSeconds"));
        static StructureState Build(GameSession session, Cell cell, StructureKind kind = StructureKind.Gun, float facing = 0)
        {
            Assert.That(session.TryBuild(new[] { cell }, kind, MaterialTier.Pencil, out var reason, facingDegrees: facing), Is.True, reason);
            return session.StructureAt(cell);
        }

        [Test]
        public void RealContactDamageIsClampedAttributedAndRetainedAfterVisualEventsOverflow()
        {
            var rules = Rules(); rules.BaseHealth = 7; rules.Enemy(EnemyKind.Doodle).Damage = 20;
            rules.Combat.ObjectiveContactDistance = 1;
            var source = new Point2(19.8f, 12.5f);
            var game = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(source, 1000) });
            game.Tick(.01f);
            Assert.That(game.Phase, Is.EqualTo(RunPhase.Defeat));
            for (int i = 0; i < game.EventBuffer.Capacity + 1; i++)
                game.EventBuffer.Publish(game.TickIndex, SimulationEventKind.Shot, source, source, StructureKind.Gun);
            var wave = Records(game.Statistics, "WaveLosses").Single();
            Assert.That(Field<int>(wave, "WaveIndex"), Is.EqualTo(1));
            Assert.That(Field<float>(wave, "BaseDamage"), Is.EqualTo(7));
            var approach = Records(game.Statistics, "ApproachLosses").Single();
            Assert.That(Field<Cell>(approach, "Cell"), Is.EqualTo(new Cell(19, 12)));
            Assert.That(Field<float>(approach, "BaseDamage"), Is.EqualTo(7));
            Assert.That(Field<int>(approach, "Hits"), Is.EqualTo(1));
            Assert.That(game.Statistics.BaseDamageTaken, Is.EqualTo(7));
        }

        [Test]
        public void RealGlobKeepsItsLaunchPositionAfterItsEnemyDies()
        {
            var rules = Rules(); var definition = rules.Enemy(EnemyKind.Spitter);
            definition.Damage = 9; definition.Range = 14; definition.WindupSeconds = .02f;
            definition.ProjectileSeconds = .3f; definition.AttackInterval = 20;
            var source = new Point2(12.5f, 12.5f);
            var game = GameSession.CreatePrepared(rules, 9, new[] { new PreparedEnemy(source, 1000, EnemyKind.Spitter) });
            for (int i = 0; i < 20 && game.EnemyProjectiles.Count == 0; i++) game.Tick(.01f);
            Assert.That(game.EnemyProjectiles.Count, Is.EqualTo(1), "The real enemy must launch a projectile.");
            game.Enemies[0].Health = 0;
            game.Tick(.4f);
            var approach = Records(game.Statistics, "ApproachLosses").Single();
            Assert.That(Field<Cell>(approach, "Cell"), Is.EqualTo(new Cell(12, 12)));
            Assert.That(Field<float>(approach, "BaseDamage"), Is.EqualTo(9).Within(.001));
            Assert.That(Field<float>(Records(game.Statistics, "WaveLosses").Single(), "BaseDamage"), Is.EqualTo(9).Within(.001));
        }

        [Test]
        public void ActivityExcludesPlanningPauseConstructionAndSupportButCountsActualLaunches()
        {
            var rules = Rules(); rules.Structure(StructureKind.Gun).BuildSeconds = 5;
            var game = new GameSession(rules, starterDefenses: false);
            var tower = Build(game, new Cell(12, 4)); Build(game, new Cell(3, 3), StructureKind.Wall);
            game.Tick(2);
            Assert.That(Exposure(game), Is.Zero, "Planning is not combat exposure.");
            game.StartNextWave(); game.Tick(.02f);
            game.Enemies[0].Position = new Point2(17.5f, 4.5f);
            game.Tick(2);
            Assert.That(Exposure(game), Is.Zero, "An unfinished tower has no combat exposure.");
            game.Tick(3);
            var record = Records(game.Statistics, "PositionActivities").Single();
            Assert.That(Field<int>(record, "StructureId"), Is.EqualTo(tower.Id));
            Assert.That(Field<float>(record, "ActiveSeconds"), Is.InRange(1.95f, 2.1f));
            Assert.That(Field<int>(record, "Launches"), Is.GreaterThan(0));
            float exposure = Exposure(game); int launches = Field<int>(record, "Launches");
            game.Tick(0);
            Assert.That(Exposure(game), Is.EqualTo(exposure));
            Assert.That(Field<int>(Records(game.Statistics, "PositionActivities").Single(), "Launches"), Is.EqualTo(launches));
            game.Tick(.1f);
            Assert.That(Field<float>(record, "ActiveSeconds"), Is.EqualTo(exposure), "Returned records are immutable snapshots.");
        }

        [Test]
        public void TwoMountsCountOneTowerExposureAndModernizationSuspendsIt()
        {
            var rules = Rules(); rules.Mechanics.FieldModernization = true; rules.Mechanics.MaximumModernizationRank = 3;
            var game = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(new Point2(35.5f, 20.5f), 100000) });
            var entry = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(), AssemblyResolverFixture.Weapon()), balance: rules);
            var cell = new Cell(5, 5);
            Assert.That(game.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            game.Tick(entry.BuildSeconds + .1f);
            var tower = game.StructureAt(cell);
            float before = Exposure(game); game.Tick(2);
            Assert.That(Exposure(game) - before, Is.EqualTo(2).Within(.01), "Exposure is not counted once per mount.");
            var quote = game.QuoteModernization(tower.Id, ModernizationDirection.Power);
            Assert.That(game.TryModernize(quote, out reason), Is.True, reason);
            before = Exposure(game); game.Tick(1);
            Assert.That(Exposure(game), Is.EqualTo(before), "A tower stopped for work cannot be accused of missing shots.");
            game.Tick(1.2f);
            Assert.That(Exposure(game) - before, Is.InRange(.15f, .25f));
        }

        [Test]
        public void RealDestroyedWallIsAttributedToItsWaveAndSaleDoesNotAddAnotherLoss()
        {
            var rules = Rules(); rules.Enemy(EnemyKind.Doodle).Damage = 10;
            rules.Enemy(EnemyKind.Doodle).StructureDamageMultiplier = 1;
            var game = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(new Point2(17.7f, 12.5f), 1000) });
            var ring = new List<Cell>();
            for (int x = 18; x <= 22; x++) { ring.Add(new Cell(x, 10)); ring.Add(new Cell(x, 14)); }
            for (int y = 11; y <= 13; y++) { ring.Add(new Cell(18, y)); ring.Add(new Cell(22, y)); }
            Assert.That(game.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
            var wall = game.StructureAt(new Cell(18, 12)); int investment = wall.PaidInk; wall.Health = 3;
            game.Tick(.01f);
            Assert.That(game.StructureAt(wall.Cell), Is.Null, "A real enemy must destroy the wall.");
            Assert.That(game.TryErase(new Cell(18, 10), out _), Is.True);
            var wave = Records(game.Statistics, "WaveLosses").Single();
            Assert.That(Field<int>(wave, "WaveIndex"), Is.EqualTo(1));
            Assert.That(Field<int>(wave, "DestroyedStructures"), Is.EqualTo(1));
            Assert.That(Field<int>(wave, "LostInvestment"), Is.EqualTo(investment));
        }

        [Test]
        public void DamageInLaterAssaultKeepsSeparateWaveTotals()
        {
            var rules = Rules(); rules.Enemy(EnemyKind.Doodle).Damage = 4; rules.Enemy(EnemyKind.Doodle).AttackInterval = 20;
            var game = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(new Point2(19.8f, 12.5f), 1000) });
            game.Tick(.01f); game.Enemies[0].Health = 0; game.Tick(.01f);
            Assert.That(game.CompletedWaves, Is.EqualTo(1));
            game.StartNextWave(); game.Tick(.01f); game.Enemies[0].Position = new Point2(21.2f, 12.5f); game.Tick(.01f);
            var waves = Records(game.Statistics, "WaveLosses");
            Assert.That(waves.Select(item => Field<int>(item, "WaveIndex")), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(waves.Select(item => Field<float>(item, "BaseDamage")), Is.EqualTo(new[] { 4f, 4f }));
        }

        [Test]
        public void RebuildingTheSameCellDoesNotInheritTheRemovedTowersActivity()
        {
            var rules = Rules(); var cell = new Cell(5, 5);
            var game = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(new Point2(35.5f, 20.5f), 100000) });
            var first = Build(game, cell); game.Tick(16);
            Assert.That(game.TryErase(cell, out _), Is.True);
            var second = Build(game, cell); game.Tick(1);
            var records = Records(game.Statistics, "PositionActivities");
            var removed = records.Single(item => Field<int>(item, "StructureId") == first.Id);
            var current = records.Single(item => Field<int>(item, "StructureId") == second.Id);
            Assert.That(Field<bool>(removed, "Removed"), Is.True);
            Assert.That(Field<bool>(removed, "Destroyed"), Is.False);
            Assert.That(Field<float>(removed, "ActiveSeconds"), Is.GreaterThan(15));
            Assert.That(Field<float>(current, "ActiveSeconds"), Is.InRange(.95f, 1.05f));
            Assert.That(Field<int>(current, "Launches"), Is.Zero);
        }

        [Test]
        public void UnknownDamageHasNoInventedApproachAndTelemetryHasExplicitBounds()
        {
            var statistics = new RunStatistics();
            var record = typeof(RunStatistics).GetMethod("RecordBaseDamage", new[] { typeof(float), typeof(int), typeof(Point2?) });
            Assert.That(record, Is.Not.Null, "The contextual damage overload is missing.");
            record.Invoke(statistics, new object[] { 3f, 0, null });
            record.Invoke(statistics, new object[] { 4f, 1, new Point2(float.NaN, 1) });
            Assert.That(Records(statistics, "ApproachLosses"), Is.Empty);
            Assert.That(Field<float>(statistics, "UnattributedBaseDamage"), Is.EqualTo(7));
            Assert.That(Records(statistics, "WaveLosses").All(item => Field<int>(item, "WaveIndex") > 0), Is.True);
            for (int i = 1; i <= 3000; i++) record.Invoke(statistics, new object[] { 1f, i, new Point2(i, i) });
            Assert.That(Records(statistics, "WaveLosses").Length, Is.LessThanOrEqualTo(2048));
            Assert.That(Records(statistics, "ApproachLosses").Length, Is.LessThanOrEqualTo(512));
            Assert.That(Field<bool>(statistics, "DefeatTelemetryTruncated"), Is.True);
            Assert.That(statistics.BaseDamageTaken, Is.EqualTo(3007));
            var activity = typeof(RunStatistics).GetMethod("RecordPositionActive");
            Assert.That(activity, Is.Not.Null);
            for (int i = 1; i <= 1000; i++) activity.Invoke(statistics, new object[] { i, new Cell(i, 1), "weapon.gun", 20f });
            Assert.That(Records(statistics, "PositionActivities").Length, Is.LessThanOrEqualTo(512));
        }

        [TestCase(false)] [TestCase(true)]
        public void StandaloneSpikeTrapIsNeverAReportedTowerPosition(bool trigger)
        {
            var rules = Rules();
            var game = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(new Point2(35.5f, 20.5f), 100000) });
            var trap = Build(game, new Cell(5, 5), StructureKind.Spikes);
            game.Tick(16);
            if (trigger)
            {
                // Radial attacks use the trap's .65 radius, not its .7 acquisition range.
                game.Enemies[0].Position = new Point2(5.8f, 5.5f);
                float health = game.Enemies[0].Health;
                game.Tick(1);
                Assert.That(game.Enemies[0].Health, Is.LessThan(health), "The fix must preserve an actual trap attack.");
                Assert.That(game.Statistics.Launches(StructureKind.Spikes), Is.GreaterThan(0));
            }
            Assert.That(Records(game.Statistics, "PositionActivities").Any(item => Field<int>(item, "StructureId") == trap.Id), Is.False,
                "Standalone traps are support; neither time without a victim nor a real trigger is tower firing telemetry.");
        }

        [Test]
        public void TowerMountedRadialWeaponRetainsExposureAndActualLaunches()
        {
            var rules = Rules(); var input = LegacyContentCatalog.CreateInput(rules);
            input.Templates.Add(new DrawingTemplateDefinitionInput { Id = "template.telemetry.radial", DisplayName = "Radial", Weight = 0 });
            foreach (var definition in input.Chassis) definition.TemplateId = "template.telemetry.radial";
            foreach (var definition in input.Weapons) definition.TemplateId = "template.telemetry.radial";
            foreach (var definition in input.Modules) definition.TemplateId = "template.telemetry.radial";
            var stats = WeaponDefinition.CopyStats(rules.Structure(StructureKind.Spikes));
            stats.Range = 6; stats.Radius = 4;
            input.Weapons.Add(new WeaponDefinitionInput { Id = "weapon.telemetry.radial", Name = "Radial tower", TemplateId = "template.telemetry.radial",
                Attack = AttackKind.Radial, LegacyKind = StructureKind.Spikes, Load = 1, AssemblyCredit = 0, Stats = stats });
            rules.Content = input.CreateSnapshot();
            var blueprint = new TowerBlueprint("telemetry.radial", "Radial tower", "chassis.standard", "weapon.telemetry.radial", content: rules.Content);
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = blueprint.FirstWeapon, ContentId = blueprint.FirstWeaponId,
                Blueprint = blueprint, IsComplete = true, Health = 100, MaxHealth = 100 };
            var enemy = new EnemyState { Id = 1, Position = new Point2(35.5f, 20.5f), Health = 100000, MaxHealth = 100000 };
            var system = new WeaponSystem(rules); var statistics = new RunStatistics();
            var events = new SimulationEventBuffer(); var shots = new List<ShotEvent>();
            var structures = new List<StructureState> { tower }; var enemies = new List<EnemyState> { enemy };
            for (int i = 0; i < 64; i++) system.Fire(structures, enemies, shots, 1, 1, .25f, i + 1, statistics, events);
            Assert.That(Field<float>(Records(statistics, "PositionActivities").Single(), "ActiveSeconds"), Is.EqualTo(16).Within(.001));
            enemy.Position = new Point2(12, 10.5f);
            for (int i = 0; i < 8; i++) system.Fire(structures, enemies, shots, 1, 1, .25f, i + 65, statistics, events);
            Assert.That(enemy.Health, Is.LessThan(100000));
            Assert.That(Field<int>(Records(statistics, "PositionActivities").Single(), "Launches"), Is.GreaterThan(0));
        }
    }
}
