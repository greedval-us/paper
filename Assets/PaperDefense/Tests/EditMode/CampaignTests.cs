using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class CampaignTests
    {
        [Test]
        public void ReceiptGuidLetterCaseCannotRepeatAPaymentOrCorruptTheNextProfileRoundTrip()
        {
            var profile = new WorkshopProfile();
            var mission = CampaignCatalog.Default.Missions[0];
            var runId = Guid.ParseExact("abcdef0123456789abcdef0123456789", "N");
            Assert.That(profile.TryApplyMissionReward(mission, runId, mission.WaveCount, true, 3, out _), Is.True);
            var data = profile.ToData();
            data.CampaignRewards[0].RunId = data.CampaignRewards[0].RunId.ToUpperInvariant();
            profile = WorkshopProfile.FromData(data);
            int gold = profile.Gold, xp = profile.TotalXp;
            Assert.That(profile.FindCampaignRunReward(runId), Is.Not.Null);
            Assert.That(profile.TryApplyMissionReward(mission, runId, mission.WaveCount, true, 3, out _), Is.True);
            Assert.That(profile.Gold, Is.EqualTo(gold)); Assert.That(profile.TotalXp, Is.EqualTo(xp));
            Assert.That(profile.ToData().CampaignRewards, Has.Length.EqualTo(1));
            Assert.That(profile.MissionWins(mission.Id), Is.EqualTo(1));
            Assert.DoesNotThrow(() => WorkshopProfile.FromData(profile.ToData()));
        }

        [TestCase(true)] [TestCase(false)]
        public void ClassicAndCampaignReceiptsCannotShareARunIdentityInEitherPaymentOrder(bool missionFirst)
        {
            var profile = new WorkshopProfile();
            var mission = CampaignCatalog.Default.Missions[0];
            var runId = Guid.NewGuid();
            if (missionFirst) Assert.That(profile.TryApplyMissionReward(mission, runId, mission.WaveCount, true, 3, out _), Is.True);
            else Assert.That(profile.TryApplyRunReward(runId, 8, true, out _), Is.True);
            int gold = profile.Gold, xp = profile.TotalXp;
            bool accepted = missionFirst ? profile.TryApplyRunReward(runId, 8, true, out var code)
                : profile.TryApplyMissionReward(mission, runId, mission.WaveCount, true, 3, out code);
            Assert.That(accepted, Is.False); Assert.That(code, Is.EqualTo("profile.reward.mission_mismatch"));
            Assert.That(profile.Gold, Is.EqualTo(gold)); Assert.That(profile.TotalXp, Is.EqualTo(xp));
            Assert.DoesNotThrow(() => WorkshopProfile.FromData(profile.ToData()));
        }

        [TestCase("retired-page")]
        [TestCase("notebook-3-page-3")]
        public void UnavailableSavedSelectionIsPreservedWhileReadModelOffersAnUnlockedPage(string savedId)
        {
            var data = new WorkshopProfile().ToData();
            data.Campaign.SelectedMissionId = savedId;
            var profile = WorkshopProfile.FromData(data);
            Assert.That(new CampaignReadModel(profile).SelectedMissionId, Is.EqualTo("notebook-1-page-1"));
            Assert.That(profile.ToData().Campaign.SelectedMissionId, Is.EqualTo(savedId));
        }

        [Test]
        public void EachNotebookKeepsItsEnemyFamilyThroughPreviewBattleAndContinuation()
        {
            var baseline = NotebookTowerDefensePreset.Create();
            var expected = new[] { EnemyFamily.Ink, EnemyFamily.Undead, EnemyFamily.Clockwork };
            foreach (var mission in CampaignCatalog.Default.Missions)
            {
                var family = expected[mission.NotebookIndex - 1];
                var balance = MissionBalanceFactory.Create(baseline, mission);
                Assert.That(mission.EnemyFamily, Is.EqualTo(family), mission.Id);
                // Skip the first assault to reach a genuine intermission at minimal test cost.
                balance.Waves[0].Count = 0;
                var session = new GameSession(balance, mission.Seed, false);
                Assert.That(new RunReadModel(session).Balance.EnemyFamily, Is.EqualTo(family), mission.Id);
                session.StartNextWave();
                session.Tick(balance.SimulationStep);
                Assert.That(session.TryCaptureIntermissionCheckpoint(out var saved, out var reason), Is.True, reason);
                var restoredBalance = MissionBalanceFactory.Create(baseline, mission);
                restoredBalance.Waves[0].Count = 0;
                Assert.That(GameSession.TryRestoreIntermissionCheckpoint(restoredBalance, saved, out var restored, out reason), Is.True, reason);
                Assert.That(new RunReadModel(restored).Balance.EnemyFamily, Is.EqualTo(family), mission.Id);
            }
            Assert.That(baseline.EnemyFamily, Is.EqualTo(EnemyFamily.Ink));
        }

        [Test]
        public void EachPageFieldsOneBossAtTheMidpointAndOneAtTheFinale()
        {
            var baseline = NotebookTowerDefensePreset.Create();
            Assert.That(baseline.Waves.All(wave => wave.Boss == BossRank.None), Is.True);
            foreach (var mission in CampaignCatalog.Default.Missions)
            {
                var balance = MissionBalanceFactory.Create(baseline, mission);
                // Keep every scheduled wave but use a small escort so we can inspect every admission.
                foreach (var wave in balance.Waves) wave.Count = 3;
                var game = new GameSession(balance, mission.Seed, false);
                var model = new RunReadModel(game);
                for (int number = 1; number <= mission.WaveCount; number++)
                {
                    var expected = number == mission.WaveCount ? BossRank.Finale :
                        number == (mission.WaveCount + 1) / 2 ? BossRank.Midpoint : BossRank.None;
                    Assert.That(model.Balance.Waves[number - 1].Boss, Is.EqualTo(expected), mission.Id);
                    if (game.PendingUpgrade) game.ChooseUpgrade(RunUpgrade.RapidFire);
                    game.StartNextWave();
                    for (int i = 0; i < 240 && game.RemainingToSpawn > 0; i++) game.Tick(.1f);
                    Assert.That(model.Enemies.Count, Is.EqualTo(3), mission.Id);
                    Assert.That(model.Enemies[0].Boss, Is.EqualTo(expected), mission.Id);
                    Assert.That(model.Enemies.Skip(1).All(e => e.Boss == BossRank.None), Is.True, mission.Id);
                    if (expected != BossRank.None)
                    {
                        var leader = game.Enemies[0];
                        var ordinary = balance.Enemy(leader.Kind);
                        Assert.That(leader.BossStats.MaxHealth, Is.GreaterThan(ordinary.MaxHealth));
                        Assert.That(leader.VisualScale, Is.GreaterThan(ordinary.VisualScale * balance.Waves[number - 1].Horde.BodyScale));
                        Assert.That(leader.Kind, Is.EqualTo(expected == BossRank.Finale ? EnemyKind.Spitter : EnemyKind.Gnawer));
                        EnemyEffects.ApplyHit(leader, leader.MaxHealth * 10, default, game.Statistics,
                            StructureKind.Gun, balance.Effects, events: game.EventBuffer);
                        Assert.That(game.EventBuffer.Last(e => e.Kind == SimulationEventKind.EnemyHit).Boss, Is.EqualTo(expected));
                        Assert.That(game.EventBuffer.Last(e => e.Kind == SimulationEventKind.EnemyDied).Boss, Is.EqualTo(expected));
                    }
                    foreach (var enemy in game.Enemies) enemy.Health = 0;
                    game.Tick(balance.SimulationStep);
                    if (number == 1)
                    {
                        Assert.That(game.TryCaptureIntermissionCheckpoint(out var saved, out var reason), Is.True, reason);
                        Assert.That(GameSession.TryRestoreIntermissionCheckpoint(balance, saved, out game, out reason), Is.True, reason);
                        model = new RunReadModel(game);
                    }
                }
                Assert.That(game.Phase, Is.EqualTo(RunPhase.Victory), mission.Id);
            }
        }

        [TestCase("notebook-1-page-1")]
        [TestCase("notebook-2-page-1")]
        [TestCase("notebook-3-page-1")]
        public void FinaleBossLandsItsRangedAttackWhileStillOutsideContactRange(string missionId)
        {
            var mission = CampaignCatalog.Default.Find(missionId);
            var balance = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), mission);
            balance.Waves.RemoveRange(0, balance.Waves.Count - 1);
            balance.Waves[0].Count = 1;
            var game = new GameSession(balance, mission.Seed, false);
            game.StartNextWave();
            game.Tick(balance.SimulationStep);
            var boss = game.Enemies.Single();
            boss.ObjectiveIndex = -1;
            boss.Position = new Point2(game.BaseCell.X - 4.5f, game.BaseCell.Y + .5f);
            var start = boss.Position;
            for (int i = 0; i < 180 && game.BaseHealth == balance.BaseHealth; i++) game.Tick(balance.SimulationStep);
            Assert.That(boss.Position, Is.EqualTo(start), missionId);
            Assert.That(game.BaseHealth, Is.LessThan(balance.BaseHealth), missionId);
            foreach (var kind in new[] { SimulationEventKind.EnemyAttack, SimulationEventKind.EnemyGlobLaunched, SimulationEventKind.EnemyGlobImpact })
                Assert.That(game.EventBuffer.Any(item => item.Kind == kind && item.Boss == BossRank.Finale), Is.True, kind.ToString());
        }

        [Test]
        public void NineAuthoredPagesOwnDifferentConnectedMapsAndTheMixedWaveSchedule()
        {
            var missions = CampaignCatalog.Default.Missions;
            Assert.That(missions.Select(m => m.WaveCount), Is.EqualTo(new[] { 4, 5, 8, 5, 6, 9, 6, 6, 10 }));
            Assert.That(missions.Select(m => m.Id).Distinct().Count(), Is.EqualTo(9));
            var baseline = NotebookTowerDefensePreset.Create();
            int originalWaves = baseline.Waves.Count;
            var signatures = missions.Select(m =>
            {
                var balance = MissionBalanceFactory.Create(baseline, m);
                var session = new GameSession(balance, m.Seed, false);
                Assert.That(session.Battlefield, Is.Not.Null);
                Assert.That(session.Balance.Waves.Count, Is.EqualTo(m.WaveCount));
                Assert.That(session.Battlefield.Entrances.All(c => session.Battlefield.DistanceToCastle(c) < int.MaxValue), Is.True, m.Id);
                Assert.That(balance.Waves.All(w => w.IsHorde && w.Count >= 300), Is.True, m.Id);
                Assert.That(balance.Waves.Select(w => w.Count), Is.Ordered.Ascending, m.Id);
                foreach (var wave in balance.Waves)
                    for (int side = 1; side <= 8; side <<= 1)
                        if ((wave.Sides & side) != 0)
                            Assert.That(session.Battlefield.HasEntrance(side), Is.True, m.Id);
                return string.Join(";", session.Battlefield.Props.Select(p => p.Cell.X + "," + p.Cell.Y + "," + p.Width + "," + p.Height));
            }).ToArray();
            Assert.That(signatures.Distinct().Count(), Is.EqualTo(9));
            Assert.That(baseline.Waves.Count, Is.EqualTo(originalWaves));
        }

        [Test]
        public void TheRiverFieldsAndGateActuallyAdmitDifferentOpeningHordes()
        {
            GameSession OpenPage(int index)
            {
                var mission = CampaignCatalog.Default.Missions[index];
                var balance = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), mission);
                var session = new GameSession(balance, mission.Seed, false);
                session.StartNextWave();
                for (int halfSeconds = 0; halfSeconds < 80 && session.RemainingToSpawn > 0; halfSeconds++)
                    session.Tick(.5f);
                Assert.That(session.Statistics.TotalSpawnedEnemies, Is.EqualTo(balance.Waves[0].Count), mission.Id);
                return session;
            }

            var river = OpenPage(3);
            var fields = OpenPage(5);
            var gate = OpenPage(7);
            Assert.That(river.Statistics.SpawnedByKind[(int)EnemyKind.Doodle],
                Is.GreaterThan(river.Statistics.TotalSpawnedEnemies * .85f));
            Assert.That(fields.Statistics.SpawnedByKind[(int)EnemyKind.Runner],
                Is.GreaterThan(fields.Statistics.TotalSpawnedEnemies * .50f));
            var tiers = gate.Statistics.SpawnedByDrawingTier;
            Assert.That(tiers[(int)DrawingTier.Outlined] + tiers[(int)DrawingTier.Hatched],
                Is.GreaterThan(gate.Statistics.TotalSpawnedEnemies * .40f));
            var defenses = gate.Balance.Effects;
            Assert.That(gate.Enemies.Any(e => EnemyEffects.Defense(e, defenses) > 0), Is.True);
        }

        [Test]
        public void FinalMapChangesFromPackedNorthernApproachToRunnersThenProtectedColumn()
        {
            var final = CampaignCatalog.Default.Missions[8];
            var waves = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), final).Waves;
            Assert.That(waves.Take(3).Select(w => w.Sides), Is.EqualTo(new[] { 4, 2, 8 }));
            Assert.That(waves[0].Count * waves[0].SpawnInterval,
                Is.LessThan(waves[1].Count * waves[1].SpawnInterval));
            Assert.That(waves[0].RunnerFraction, Is.LessThan(.1f));
            Assert.That(waves[1].RunnerFraction, Is.GreaterThan(.5f));
            Assert.That(waves[2].OutlinedFraction + waves[2].HatchedFraction, Is.GreaterThanOrEqualTo(.5f));
            Assert.That(waves.Last().Sides, Is.EqualTo(15));
        }

        [Test]
        public void NorthernPassUsesItsSouthernCastleForPathsAndBuildClearance()
        {
            var mission = CampaignCatalog.Default.Missions[1];
            var balance = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), mission);
            var session = new GameSession(balance, mission.Seed, false);
            var map = session.Battlefield;
            var home = new Cell(48, 13);
            var middle = new Cell(48, 32);
            Assert.That(map.Style, Is.EqualTo(CampaignTerrainStyle.NorthPass));
            Assert.That(session.BaseCell, Is.EqualTo(home));
            Assert.That(map.Castle, Is.EqualTo(home));
            CollectionAssert.AreEqual(new[] { new Cell(28, 63), new Cell(68, 63) }, map.Entrances);
            Assert.That(map.DistanceToCastle(home), Is.Zero);
            Assert.That(map.DistanceToCastle(middle), Is.GreaterThan(0).And.LessThan(int.MaxValue));
            Assert.That(session.QuoteBuild(new[] { home }, StructureKind.Wall, MaterialTier.Pencil).Valid, Is.False);
            Assert.That(session.QuoteBuild(new[] { middle }, StructureKind.Wall, MaterialTier.Pencil).Valid, Is.True);
            // The active session owns its map, even if the authoring data is edited later.
            balance.Battlefield.CastleY = 1;
            Assert.That(session.BaseCell, Is.EqualTo(home));
        }

        [Test]
        public void CrossroadsAndRiverRestrictMovementAndPlacementToTheirActualOpenings()
        {
            var baseline = NotebookTowerDefensePreset.Create();
            var cross = MissionBalanceFactory.Create(baseline, CampaignCatalog.Default.Missions[2]).ResolveBattlefield();
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 96; x++)
            {
                bool passage = (x >= 39 && x <= 57) || (y >= 23 && y <= 41);
                var cell = new Cell(x, y);
                Assert.That(cross.IsWalkable(cell), Is.EqualTo(passage));
                Assert.That(cross.CanPlace(cell, StructureKind.Wall), Is.EqualTo(passage));
            }
            var river = MissionBalanceFactory.Create(baseline, CampaignCatalog.Default.Missions[3]).ResolveBattlefield();
            Assert.That(river.Entrances.All(c => c.X < 45), Is.True);
            Assert.That(river.Castle.X, Is.GreaterThan(52));
            for (int y = 0; y < 64; y++)
            {
                bool bridge = (y >= 12 && y <= 22) || (y >= 41 && y <= 51);
                var cell = new Cell(48, y);
                Assert.That(river.IsWalkable(cell), Is.EqualTo(bridge));
                Assert.That(river.CanPlace(cell, StructureKind.TarPit), Is.EqualTo(bridge));
            }
        }

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(8)]
        public void SessionCompletesTheActualFourNineOrTenWavePage(int index)
        {
            var mission = CampaignCatalog.Default.Missions[index];
            var balance = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), mission);
            foreach (var wave in balance.Waves)
                wave.Count = 0;
            var session = new GameSession(balance, mission.Seed, false);
            for (int wave = 1; wave <= mission.WaveCount; wave++)
            {
                if (session.PendingUpgrade)
                    session.ChooseUpgrade(RunUpgrade.RapidFire);
                session.StartNextWave();
                session.Tick(1f / 30);
                Assert.That(session.CompletedWaves, Is.EqualTo(wave));
                Assert.That(session.Phase == RunPhase.Victory, Is.EqualTo(wave == mission.WaveCount));
            }
        }

        [TestCase(1, 1, true)]
        [TestCase(1, 2, true)]
        [TestCase(7, 1, false)]
        [TestCase(7, 2, false)]
        public void SingleBandWavesOnlyAdmitBodiesAtTheAnnouncedGate(int page, int band, bool naturalCrowd)
        {
            var mission = CampaignCatalog.Default.Missions[page];
            var rules = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), mission);
            rules.Crowd.Enabled = naturalCrowd;
            var wave = rules.Waves[0];
            wave.EntranceBands = band;
            var session = new GameSession(rules, mission.Seed, false);
            int gate = Enumerable.Range(0, session.Battlefield.Entrances.Count)
                .Single(i => session.Battlefield.IsWaveEntrance(i, wave.Sides, band));
            var selected = session.Battlefield.Entrances[gate];
            Assert.That(new RunReadModel(session).Balance.Waves[0].EntranceBands, Is.EqualTo(band));
            session.StartNextWave();
            session.Tick(.4f);
            Assert.That(session.Enemies.Count, Is.Positive);
            foreach (var enemy in session.Enemies)
            {
                float lateral = wave.Sides == 4 ? enemy.Position.X - selected.X : enemy.Position.Y - selected.Y;
                Assert.That(Math.Abs(lateral), Is.LessThan(rules.Crowd.EntranceHalfWidth + 2));
            }
        }

        [Test]
        public void OpeningHordeFillsBothWestApproachesAndHoldsOverTwoHundredLiveEnemies()
        {
            var mission = CampaignCatalog.Default.Missions[0];
            var balance = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), mission);
            var session = new GameSession(balance, mission.Seed, false);
            Assert.That(new RunReadModel(session).Balance.Waves[0].IsHorde, Is.True);
            session.StartNextWave();

            // At one second new arrivals are still at the two separated entrance bands.
            session.Tick(1);
            Assert.That(session.Enemies.Any(e => e.Position.Y < 30), Is.True);
            Assert.That(session.Enemies.Any(e => e.Position.Y > 35), Is.True);
            Assert.That(session.Enemies.All(e => e.Position.X < 4), Is.True);
            session.Tick(14.1f);
            Assert.That(session.RemainingToSpawn, Is.Zero);
            Assert.That(session.Enemies.Count, Is.EqualTo(360));
            Assert.That(session.Enemies.All(e => e.Kind == EnemyKind.Doodle && e.Health < 5 && e.VisualScale < 1.1f), Is.True);
            for (int second = 0; second < 15; second++)
            {
                session.Tick(1);
                Assert.That(session.Enemies.Count, Is.GreaterThanOrEqualTo(200));
                Assert.That(session.Phase, Is.EqualTo(RunPhase.Assault));
            }
        }

        [Test]
        public void FourHordesEnterOnlyTheirAnnouncedEdgesAndPayTheirOwnInkPools()
        {
            var baseline = NotebookTowerDefensePreset.Create();
            var missions = CampaignCatalog.Default.Missions;
            var balance = MissionBalanceFactory.Create(baseline, missions[0]);
            Assert.That(baseline.Waves.Any(w => w.IsHorde), Is.False);
            var session = new GameSession(balance, missions[0].Seed, false);
            int[] counts = { 360, 480, 600, 720 }, sides = { 1, 2, 12, 15 }, rewards = { 14, 20, 26, 40 };
            for (int w = 0; w < counts.Length; w++)
            {
                var wave = balance.Waves[w];
                Assert.That(wave.IsHorde, Is.True);
                Assert.That(wave.Sides, Is.EqualTo(sides[w]));
                if (session.PendingUpgrade) session.ChooseUpgrade(RunUpgrade.RapidFire);
                session.StartNextWave();
                int observed = 0, lastId = -1;
                for (int sample = 0; sample < 62; sample++)
                {
                    session.Tick(.25f);
                    foreach (var enemy in session.Enemies)
                    {
                        if (enemy.Id <= lastId) continue;
                        var p = enemy.Position;
                        int side = p.X < 3 ? 1 : p.X > balance.Width - 3 ? 2 :
                            p.Y > balance.Height - 3 ? 4 : p.Y < 3 ? 8 : 0;
                        Assert.That(side & sides[w], Is.Not.Zero, "An arrival must be on an announced edge.");
                        observed |= side;
                        lastId = enemy.Id;
                    }
                }
                Assert.That(observed, Is.EqualTo(sides[w]));
                Assert.That(session.RemainingToSpawn, Is.Zero);
                Assert.That(session.Enemies.Count, Is.EqualTo(counts[w]));
                Assert.That(session.Enemies.Sum(e => e.RewardBudget), Is.EqualTo(rewards[w]));
                int before = session.Ink;
                foreach (var enemy in session.Enemies) enemy.Health = 0;
                session.Tick(balance.SimulationStep);
                Assert.That(session.Ink - before, Is.EqualTo(rewards[w] + wave.InkGrant));
            }
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Victory));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ContactFightersBreachAWallWhileOtherEnemiesCanFlankIt(bool horde)
        {
            var mission = CampaignCatalog.Default.Missions[0];
            var balance = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), mission);
            if (!horde)
            {
                balance.Waves[0].Horde = default;
                balance.Waves[0].Count = 14;
            }
            var session = new GameSession(balance, mission.Seed, false);
            int x = session.BaseCell.X - 2;
            var row = Enumerable.Range(session.BaseCell.Y - 2, 5).Select(y => new Cell(x, y)).ToArray();
            Assert.That(session.TryBuild(row, StructureKind.Wall, MaterialTier.Pencil, out string reason), Is.True, reason);
            session.StartNextWave();
            bool flanked = false, hit = false, crossedBreach = false;
            var openings = new System.Collections.Generic.HashSet<Cell>();
            long lastEvent = 0;
            for (int step = 0; step < 300 && session.Phase == RunPhase.Assault && !crossedBreach; step++)
            {
                session.Tick(.25f);
                foreach (var action in session.EventBuffer)
                {
                    if (action.Sequence <= lastEvent) continue;
                    lastEvent = action.Sequence;
                    if (action.Weapon != StructureKind.Wall) continue;
                    if (action.Kind == SimulationEventKind.StructureHit && action.Magnitude > 0) hit = true;
                    if (action.Kind == SimulationEventKind.StructureDestroyed)
                        openings.Add(new Cell((int)action.To.X, (int)action.To.Y));
                }
                if (openings.Count == 0 && session.Enemies.Any(e => e.Position.X >= x + 1)) flanked = true;
                if (openings.Count != 0)
                    crossedBreach = session.Enemies.Any(e => openings.Contains(new Cell((int)e.Position.X, (int)e.Position.Y)));
            }
            Assert.That(flanked, Is.True, "The open flanks must still let enemies get past the intact row.");
            Assert.That(hit, Is.EqualTo(horde));
            Assert.That(crossedBreach, Is.EqualTo(horde));
        }

        [Test]
        public void RepeatedDeliveryAndReplayKeepFirstClearCurrencyAndStarsIndependent()
        {
            var profile = new WorkshopProfile();
            var mission = CampaignCatalog.Default.Missions[0];
            var run = Guid.NewGuid();
            int initial = profile.Gold;
            Assert.That(profile.TryApplyMissionReward(mission, run, 2, false, 0, out _), Is.True);
            Assert.That(profile.TryApplyMissionReward(mission, run, 1, false, 0, out _), Is.True);
            Assert.That(profile.Gold - initial, Is.EqualTo(2 * mission.GoldPerWave));
            Assert.That(profile.IsMissionUnlocked(CampaignCatalog.Default.Missions[1].Id), Is.False);
            Assert.That(profile.TryApplyMissionReward(mission, run, 4, true, 2, out _), Is.True);
            var first = profile.FindCampaignRunReward(run);
            Assert.That(first.FirstClear, Is.True);
            int gold = profile.Gold, xp = profile.TotalXp;
            Assert.That(profile.TryApplyMissionReward(mission, run, 4, true, 3, out _), Is.True);
            Assert.That(profile.Gold, Is.EqualTo(gold));
            Assert.That(profile.TotalXp, Is.EqualTo(xp));
            Assert.That(profile.BestMissionStars(mission.Id), Is.EqualTo(3));
            Assert.That(profile.MissionWins(mission.Id), Is.EqualTo(1));
            Assert.That(profile.IsMissionUnlocked(CampaignCatalog.Default.Missions[1].Id), Is.True);
            Assert.That(mission.UnlockPartIds.All(profile.IsUnlocked), Is.True);
            var replay = Guid.NewGuid();
            Assert.That(profile.TryApplyMissionReward(mission, replay, 4, true, 1, out _), Is.True);
            Assert.That(profile.FindCampaignRunReward(replay).FirstClear, Is.False);
            Assert.That(profile.Gold - gold, Is.EqualTo(4 * mission.GoldPerWave + mission.CompletionGold));
            Assert.That(profile.BestMissionStars(mission.Id), Is.EqualTo(3));
            Assert.That(profile.MissionWins(mission.Id), Is.EqualTo(2));
            Assert.That(profile.TryApplyMissionReward(CampaignCatalog.Default.Missions[1], run, 4, false, 0, out _), Is.False,
                "A paid run ID cannot be rebound to another page.");
        }

        [Test]
        public void AllNineClearsAndSelectionSurviveProfileRoundTrip()
        {
            var profile = new WorkshopProfile();
            foreach (var mission in CampaignCatalog.Default.Missions)
            {
                Assert.That(profile.TrySelectMission(mission.Id, out _), Is.True);
                Assert.That(profile.TryApplyMissionReward(mission, Guid.NewGuid(), mission.WaveCount, true, 3, out _), Is.True);
            }
            var restored = WorkshopProfile.FromData(profile.ToData());
            Assert.That(restored.SelectedMissionId, Is.EqualTo("notebook-3-page-3"));
            Assert.That(CampaignCatalog.Default.Missions.Sum(m => restored.BestMissionStars(m.Id)), Is.EqualTo(27));
            Assert.That(restored.Gold, Is.EqualTo(profile.Gold));
            Assert.That(restored.TotalXp, Is.EqualTo(profile.TotalXp));
            Assert.That(CampaignCatalog.Default.Next(restored.SelectedMissionId), Is.Null);
        }

        [Test]
        public void VersionEightRetainsItsMoneyReceiptsAndRightsWithoutInventingCampaignClears()
        {
            var source = new WorkshopProfile();
            var oldRun = Guid.NewGuid();
            Assert.That(source.TryApplyRunReward(oldRun, 8, true, out _), Is.True);
            var data = source.ToData();
            data.Version = 8;
            data.Campaign = null;
            data.CampaignRewards = null;
            var restored = WorkshopProfile.FromData(data);
            Assert.That(restored.ToData().Version, Is.EqualTo(WorkshopProfile.CurrentVersion));
            Assert.That(restored.Gold, Is.EqualTo(source.Gold));
            Assert.That(restored.TotalXp, Is.EqualTo(source.TotalXp));
            Assert.That(restored.FindRunReward(oldRun).VictoryBonus, Is.True);
            Assert.That(restored.IsMissionUnlocked("notebook-1-page-1"), Is.True);
            Assert.That(restored.IsMissionUnlocked("notebook-1-page-2"), Is.False);
            Assert.That(restored.ToData().Unlocked, Is.EqualTo(source.ToData().Unlocked));
        }

        [TestCase("state")]
        [TestCase("mission")]
        [TestCase("receipt")]
        public void FutureCampaignMetadataProtectsTheSourceBeforeMalformedSiblingValidation(string field)
        {
            var data = new WorkshopProfile().ToData();
            data.Entries[0] = null;
            if (field == "state")
                data.Campaign.Version = 99;
            if (field == "mission")
                data.Campaign.Missions = new[] { new CampaignMissionStateData { Version = 99 } };
            if (field == "receipt")
                data.CampaignRewards = new[] { new CampaignRunRewardData { Version = 99 } };
            Assert.Throws<NotSupportedException>(() => WorkshopProfile.FromData(data));
        }
    }
}
