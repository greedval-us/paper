using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class MechanicsConfigurationTests
    {
        static GameBalance Balance(int count = 1)
        {
            var balance = GameBalance.CreateDefault();
            balance.Width = 40; balance.Height = 24; balance.StartingInk = 1000;
            balance.BaseHealth = 500; balance.PlanningSeconds = balance.IntermissionSeconds = 1000;
            balance.EnableEnemyMechanics = true; balance.EnableInkWells = true;
            balance.PassiveInkPerSecond = balance.EarlyLaunchInkPerSecond = 0;
            balance.WaveInkGrant = 0;
            balance.Waves = new List<WaveDefinition> { new WaveDefinition { Count = count, SpawnInterval = .005f, Sides = 1 } };
            foreach (var definition in balance.StructureDefinitions) definition.BuildSeconds = 0;
            foreach (var definition in balance.EnemyDefinitions)
            {
                definition.Speed = definition.Damage = 0;
                definition.MaxHealth = 100; definition.Reward = 0;
            }
            return balance;
        }

        static void SetConfiguration(MechanicsConfiguration mechanics, bool enabled)
        {
            mechanics.EnemyDrawingTiers = mechanics.CorpseBlots = mechanics.RangedEnemies = enabled;
            mechanics.InkWells = mechanics.ColorCartridges = mechanics.RunUpgrades = enabled;
        }

        static void AssertAll(MechanicsAvailability mechanics, bool enabled)
        {
            Assert.That(mechanics.EnemyDrawingTiers, Is.EqualTo(enabled), nameof(mechanics.EnemyDrawingTiers));
            Assert.That(mechanics.CorpseBlots, Is.EqualTo(enabled), nameof(mechanics.CorpseBlots));
            Assert.That(mechanics.RangedEnemies, Is.EqualTo(enabled), nameof(mechanics.RangedEnemies));
            Assert.That(mechanics.InkWells, Is.EqualTo(enabled), nameof(mechanics.InkWells));
            Assert.That(mechanics.ColorCartridges, Is.EqualTo(enabled), nameof(mechanics.ColorCartridges));
            Assert.That(mechanics.RunUpgrades, Is.EqualTo(enabled), nameof(mechanics.RunUpgrades));
        }

        static TowerBlueprint AuthoredGun() => new TowerBlueprint("mechanics.authored", "Только декор", ChassisKind.Standard,
            StructureKind.Gun, drawing: new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Decoration, new[] {
                new DrawingStroke(new[] { new DrawingPoint(-.8f, -.8f), new DrawingPoint(-.7f, -.7f) }) }) }),
            origin: DrawingOrigin.PlayerDrawing);

        static GameSession Spawn(GameBalance balance)
        {
            var session = new GameSession(balance, starterDefenses: false);
            session.StartNextWave(); session.Tick(.02f);
            Assert.That(session.Enemies, Is.Not.Empty);
            return session;
        }

        static EnemyState FireAt(GameBalance balance, StructureKind weapon, InkColor color, DrawingTier tier = DrawingTier.Sketch)
        {
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = weapon, Tier = MaterialTier.Pencil,
                Health = 55, MaxHealth = 55, IsComplete = true, Color = color };
            var enemy = new EnemyState { Id = 1, Kind = EnemyKind.Doodle, Position = new Point2(11.5f, 10.5f),
                Health = 100, MaxHealth = 100, DrawingTier = tier };
            new WeaponSystem(balance).Fire(new List<StructureState> { tower }, new List<EnemyState> { enemy }, new List<ShotEvent>(),
                1, 1, 1f / 30, 1, new RunStatistics(), new SimulationEventBuffer());
            return enemy;
        }

        [Test]
        public void NewConfigurationKeepsEveryExistingMechanicEnabledWhenLegacyGatesAllowIt()
        {
            AssertAll(MechanicsAvailability.Resolve(Balance()), true);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void LegacyGatesAffectOnlyTheirHistoricalMechanics(bool enemyMechanics, bool inkWells)
        {
            var balance = Balance(); balance.EnableEnemyMechanics = enemyMechanics; balance.EnableInkWells = inkWells;
            var mechanics = MechanicsAvailability.Resolve(balance);
            Assert.That(mechanics.EnemyDrawingTiers, Is.EqualTo(enemyMechanics));
            Assert.That(mechanics.CorpseBlots, Is.EqualTo(enemyMechanics));
            Assert.That(mechanics.RangedEnemies, Is.EqualTo(enemyMechanics));
            Assert.That(mechanics.InkWells, Is.EqualTo(inkWells));
            Assert.That(mechanics.ColorCartridges, Is.True);
            Assert.That(mechanics.RunUpgrades, Is.True);
        }

        [Test]
        public void BalanceSnapshotOwnsItsMechanicsConfiguration()
        {
            var source = Balance(); var copy = source.CreateSnapshot();
            Assert.That(copy.Mechanics, Is.Not.SameAs(source.Mechanics));
            SetConfiguration(source.Mechanics, false);
            AssertAll(MechanicsAvailability.Resolve(copy), true);
            AssertAll(MechanicsAvailability.Resolve(source), false);
        }

        [Test]
        public void SessionAndReadModelOwnFrozenRulesAndAvailability()
        {
            var balance = Balance(); var session = new GameSession(balance, starterDefenses: false);
            var readModel = new RunReadModel(session);
            SetConfiguration(balance.Mechanics, false);
            balance.EnableEnemyMechanics = balance.EnableInkWells = false;
            Assert.That(session.Balance, Is.Not.SameAs(balance), "Preparation owns its complete balance snapshot.");
            AssertAll(session.Mechanics, true);
            AssertAll(readModel.Balance.Mechanics, true);
            AssertAll(new GameSession(balance, starterDefenses: false).Mechanics, false);
        }

        [Test]
        public void SpawnUsesTheAvailabilityCapturedAtSessionCreation()
        {
            var balance = Balance(); balance.Waves[0].HatchedFraction = 1;
            var session = new GameSession(balance, starterDefenses: false);
            balance.Mechanics.EnemyDrawingTiers = false;
            session.StartNextWave(); session.Tick(.02f);
            Assert.That(session.Enemies[0].DrawingTier, Is.EqualTo(DrawingTier.Hatched));
            Assert.That(session.Enemies[0].MaxHealth, Is.EqualTo(170).Within(.001f));
        }

        [Test]
        public void DisablingDrawingTiersKeepsRangedEnemiesAndCorpseBlots()
        {
            var balance = Balance(); balance.Mechanics.EnemyDrawingTiers = false;
            balance.Waves[0].SpitterFraction = balance.Waves[0].HatchedFraction = 1;
            var session = Spawn(balance); var enemy = session.Enemies[0];
            Assert.That(enemy.Kind, Is.EqualTo(EnemyKind.Spitter));
            Assert.That(enemy.DrawingTier, Is.EqualTo(DrawingTier.Sketch));
            Assert.That(enemy.MaxHealth, Is.EqualTo(100));
            enemy.Position = new Point2(5.5f, 5.5f); enemy.Health = 0; session.Tick(.02f);
            Assert.That(session.Blots.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisablingDrawingTiersDoesNotIntroduceLegacyHealthGrowth()
        {
            var balance = Balance(); balance.Mechanics.EnemyDrawingTiers = false;
            balance.Progression.LegacyHealthGrowthPerWave = .5f; balance.Progression.LegacyHealthGrowthStartsAfterWave = 0;
            balance.Waves[0].HatchedFraction = 1;
            var session = Spawn(balance);
            Assert.That(session.Enemies[0].MaxHealth, Is.EqualTo(100));
        }

        [Test]
        public void DisablingLegacyEnemyMechanicsPreservesHistoricalHealthGrowth()
        {
            var balance = Balance(); balance.EnableEnemyMechanics = false;
            balance.Progression.LegacyHealthGrowthPerWave = .5f; balance.Progression.LegacyHealthGrowthStartsAfterWave = 0;
            var session = Spawn(balance);
            Assert.That(session.Enemies[0].DrawingTier, Is.EqualTo(DrawingTier.Sketch));
            Assert.That(session.Enemies[0].MaxHealth, Is.EqualTo(150));
        }

        [Test]
        public void DisablingCorpseBlotsPreventsResidueAndRejectsPaidCleanup()
        {
            var balance = Balance(2); balance.Mechanics.CorpseBlots = false;
            var session = Spawn(balance);
            session.Enemies[0].Position = new Point2(5.5f, 5.5f); session.Enemies[0].Health = 0; session.Tick(.02f);
            Assert.That(session.Blots, Is.Empty);
            int ink = session.Ink;
            Assert.That(session.TryBlotter(new Cell(5, 5), out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(session.Ink, Is.EqualTo(ink));
            Assert.That(session.BlotterCooldownRemaining, Is.Zero);
            Assert.That(session.Mechanics.RangedEnemies, Is.True);
            Assert.That(session.Mechanics.EnemyDrawingTiers, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AnExplicitBlotContextUsesTheCapturedDeveloperFlagIndependentlyOfLegacyGeneration(bool enabled, bool legacyGeneration)
        {
            var balance = Balance(); balance.EnableEnemyMechanics = legacyGeneration; balance.Mechanics.CorpseBlots = enabled;
            var context = new ExplicitBlotContext();
            var weapons = new WeaponSystem(balance, blotContext: context);
            balance.Mechanics.CorpseBlots = !enabled;
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Blotter,
                Health = 55, MaxHealth = 55, IsComplete = true, Tier = MaterialTier.Pencil };
            var statistics = new RunStatistics(); var shots = new List<ShotEvent>();
            weapons.Fire(new List<StructureState> { tower }, new List<EnemyState>(), shots,
                1, 1, 1f / 30, 1, statistics, new SimulationEventBuffer());
            Assert.That(statistics.BlotsCleared, Is.EqualTo(enabled ? 1 : 0));
            Assert.That(statistics.Launches(StructureKind.Blotter), Is.EqualTo(enabled ? 1 : 0));
            Assert.That(context.Remaining, Is.EqualTo(enabled ? 0 : 1));
            Assert.That(context.FindCalls, Is.EqualTo(enabled ? 1 : 0));
            Assert.That(shots.Count, Is.EqualTo(enabled ? 1 : 0));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AnExplicitBlotTrialUsesTheDeveloperFlagIndependentlyOfLegacyCorpseGeneration(bool enabled, bool legacyGeneration)
        {
            var balance = GameBalance.CreateDefault(); balance.EnableEnemyMechanics = legacyGeneration;
            balance.Mechanics.CorpseBlots = enabled;
            balance.Structure(StructureKind.Blotter).Range = balance.Structure(StructureKind.Blotter).Radius = 100;
            var blueprint = new TowerBlueprint("explicit.blotter", "Промокашка", ChassisKind.Standard, StructureKind.Blotter);
            int cost = TowerCatalog.Cost(blueprint, balance, MaterialTier.Pencil);
            var report = TrialRunner.Run(blueprint, balance, TrialScenario.Blots, cost, 79);
            Assert.That(report.Frames[0].Blots.Count, Is.EqualTo(enabled ? 48 : 0));
            Assert.That(report.BlotsCleared, Is.EqualTo(enabled ? 48 : 0));
            Assert.That(report.UnresolvedBlots, Is.Zero); Assert.That(report.UnresolvedEnemies, Is.Zero);
            Assert.That(report.Damage, Is.Zero); Assert.That(report.Leaks, Is.Zero);
            Assert.That(balance.Mechanics.CorpseBlots, Is.EqualTo(enabled), "The trial must preserve its caller's configuration.");
        }

        sealed class ExplicitBlotContext : IBlotContext
        {
            internal int Remaining { get; private set; } = 1;
            internal int FindCalls { get; private set; }
            public bool TryFindNearest(Point2 origin, float range, out Cell cell)
            {
                FindCalls++; cell = new Cell(12, 10); return Remaining > 0;
            }
            public int Clear(Cell center, float radius)
            {
                int cleared = Remaining; Remaining = 0; return cleared;
            }
        }

        [Test]
        public void DisablingRangedEnemiesUsesOrdinarySpawnsAndKeepsDrawingTiersAndBlots()
        {
            var balance = Balance(); balance.Mechanics.RangedEnemies = false;
            balance.Waves[0].SpitterFraction = balance.Waves[0].HatchedFraction = 1;
            var session = Spawn(balance); var enemy = session.Enemies[0];
            Assert.That(enemy.Kind, Is.EqualTo(EnemyKind.Doodle));
            Assert.That(enemy.DrawingTier, Is.EqualTo(DrawingTier.Hatched));
            Assert.That(enemy.MaxHealth, Is.EqualTo(170).Within(.001f));
            Assert.That(session.EnemyProjectiles, Is.Empty);
            enemy.Position = new Point2(5.5f, 5.5f); enemy.Health = 0; session.Tick(.02f);
            Assert.That(session.Blots.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisablingWellsReleasesObjectiveCellsAndRejectsActivationWithoutPayment()
        {
            var balance = Balance(); balance.Mechanics.InkWells = false;
            var session = new GameSession(balance, starterDefenses: false);
            Assert.That(session.Wells, Is.Empty);
            int ink = session.Ink;
            Assert.That(session.TryActivateWell(0, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty); Assert.That(session.Ink, Is.EqualTo(ink));
            Assert.That(session.TryBuild(new[] { new Cell(balance.Width / 5, balance.Height / 4) },
                StructureKind.Wall, MaterialTier.Pencil, out reason), Is.True, reason);
            Assert.That(session.Mechanics.ColorCartridges, Is.True, "A trial may supply color without campaign well economics.");
        }

        [Test]
        public void DisablingColorsRejectsPaintAndColoredConstructionAtomically()
        {
            var balance = Balance(); balance.Mechanics.ColorCartridges = false;
            var session = new GameSession(balance, starterDefenses: false);
            var blueprint = new TowerBlueprint("neutral", "Без цвета", ChassisKind.Standard, StructureKind.Gun);
            var existing = new Cell(10, 10); var rejected = new Cell(11, 10);
            Assert.That(session.TryBuildBlueprint(new[] { existing }, blueprint, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            int ink = session.Ink, color = session.ColoredInk(InkColor.Red);
            Assert.That(session.TryPaint(existing, InkColor.Red, out reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(session.QuoteBlueprint(new[] { rejected }, blueprint, MaterialTier.Pencil, InkColor.Red).Valid, Is.False);
            Assert.That(session.TryBuildBlueprint(new[] { rejected }, blueprint, MaterialTier.Pencil, InkColor.Red, out reason), Is.False);
            Assert.That(session.Ink, Is.EqualTo(ink)); Assert.That(session.ColoredInk(InkColor.Red), Is.EqualTo(color));
            Assert.That(session.StructureAt(existing).Color, Is.EqualTo(InkColor.None));
            Assert.That(session.StructureAt(rejected), Is.Null);
        }

        [TestCase(InkColor.Red)]
        [TestCase(InkColor.Blue)]
        [TestCase(InkColor.Green)]
        public void DisablingColorsKeepsDirectWeaponDamageWithoutApplyingCartridgeEffects(InkColor color)
        {
            var balance = Balance(); balance.Mechanics.ColorCartridges = false;
            var enemy = FireAt(balance, StructureKind.Gun, color);
            Assert.That(enemy.Health, Is.EqualTo(88));
            Assert.That(enemy.RedRemaining, Is.Zero); Assert.That(enemy.BlueRemaining, Is.Zero); Assert.That(enemy.GreenRemaining, Is.Zero);
        }

        [Test]
        public void DisablingColorsDoesNotDisableTheStaplersIndependentSlow()
        {
            var balance = Balance(); balance.Mechanics.ColorCartridges = false;
            var enemy = FireAt(balance, StructureKind.Stapler, InkColor.Red);
            Assert.That(enemy.Health, Is.EqualTo(96));
            Assert.That(enemy.RedRemaining, Is.Zero);
            Assert.That(enemy.BlueRemaining, Is.EqualTo(2));
            Assert.That(EnemyEffects.MovementMultiplier(enemy, balance.Effects), Is.EqualTo(.8f).Within(.0001f));
        }

        [Test]
        public void DisablingDrawingTiersKeepsColorEffectsWithoutTierDefenseInTheTrial()
        {
            var balance = Balance(); balance.Mechanics.EnemyDrawingTiers = false;
            balance.Structure(StructureKind.Gun).Damage = 10; balance.Structure(StructureKind.Gun).ShotInterval = 1000;
            var blueprint = new TowerBlueprint("colored.trial", "Цветная проба", ChassisKind.Standard, StructureKind.Gun);
            var report = TrialRunner.Run(blueprint, balance, TrialScenario.Armored, 40, color: InkColor.Blue);
            Assert.That(report.Damage, Is.EqualTo(10).Within(.0001f));
            bool foundSlow = false;
            foreach (var frame in report.Frames)
                foreach (var enemy in frame.Enemies) if (enemy.BlueRemaining > 0) foundSlow = true;
            Assert.That(foundSlow, Is.True);
        }

        [Test]
        public void DisablingRunUpgradesCannotLeaveAnIntermissionWaitingForAChoice()
        {
            var balance = Balance(0); balance.Mechanics.RunUpgrades = false;
            balance.Progression.UpgradeEveryWaves = 1; balance.Progression.LastUpgradeWave = 99;
            balance.Waves.Add(new WaveDefinition { Count = 0 }); balance.Waves.Add(new WaveDefinition { Count = 0 });
            var session = new GameSession(balance, starterDefenses: false);
            for (int wave = 1; wave <= 3; wave++)
            {
                session.StartNextWave(); session.Tick(.02f);
                Assert.That(session.WaveIndex, Is.EqualTo(wave));
                Assert.That(session.PendingUpgrade, Is.False);
            }
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Victory));
        }

        static IEnumerable<TestCaseData> MechanicCombinations()
        {
            for (int mask = 0; mask < 64; mask++) yield return new TestCaseData(mask).SetName("TinyCampaignCompletesWithMechanicsMask_" + mask);
        }

        [TestCaseSource(nameof(MechanicCombinations))]
        public void EveryMechanicCombinationPreservesConstructionCombatAndWaveCompletion(int mask)
        {
            var balance = Balance();
            balance.Mechanics.EnemyDrawingTiers = (mask & 1) != 0;
            balance.Mechanics.CorpseBlots = (mask & 2) != 0;
            balance.Mechanics.RangedEnemies = (mask & 4) != 0;
            balance.Mechanics.InkWells = (mask & 8) != 0;
            balance.Mechanics.ColorCartridges = (mask & 16) != 0;
            balance.Mechanics.RunUpgrades = (mask & 32) != 0;
            balance.Progression.UpgradeEveryWaves = 1; balance.Progression.LastUpgradeWave = 99;
            balance.Construction.AcquisitionSeconds = 0;
            balance.Structure(StructureKind.Gun).Range = 100;
            balance.Structure(StructureKind.Gun).Damage = 1000; balance.Structure(StructureKind.Gun).ShotInterval = .05f;
            balance.Waves.Clear();
            for (int wave = 0; wave < 3; wave++)
                balance.Waves.Add(new WaveDefinition { Count = 1, SpawnInterval = .005f, Sides = 1,
                    SpitterFraction = 1, HatchedFraction = 1 });
            var session = new GameSession(balance, 42, false);
            Assert.That(session.TryBuildBlueprint(new[] { new Cell(6, 6) }, AuthoredGun(), MaterialTier.Pencil,
                InkColor.None, out string reason), Is.True, reason);
            for (int wave = 1; wave <= 3; wave++)
            {
                session.StartNextWave(); session.Tick(.2f);
                Assert.That(session.WaveIndex, Is.EqualTo(wave));
                Assert.That(session.Kills, Is.EqualTo(wave));
                Assert.That(session.Enemies, Is.Empty);
                if (session.PendingUpgrade) session.ChooseUpgrade(RunUpgrade.RapidFire);
                Assert.That(session.PendingUpgrade, Is.False);
            }
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Victory));
            Assert.That(session.BaseHealth, Is.EqualTo(500));
            Assert.That(session.Structures.Count, Is.EqualTo(1));
        }

        [Test]
        public void CoreAssemblyDoesNotReferenceUnityOrTheApplicationAndPresentationLayers()
        {
            foreach (var reference in typeof(GameSession).Assembly.GetReferencedAssemblies())
            {
                Assert.That(reference.Name.StartsWith("Unity", StringComparison.Ordinal), Is.False, reference.Name);
                Assert.That(reference.Name, Is.Not.EqualTo("PaperDefense.Application"));
                Assert.That(reference.Name, Is.Not.EqualTo("PaperDefense.Presentation"));
            }
        }
    }
}
