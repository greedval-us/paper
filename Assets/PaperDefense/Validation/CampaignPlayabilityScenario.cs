using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PaperDefense.Core;

namespace PaperDefense.Validation
{
    public enum CampaignDefensePolicy { Mixed, Expansion, Concentrated }
    [Serializable] public sealed class CampaignModernizationRecord
    {
        public string Direction, Color;
        public int StructureId, Wave, Rank, Cost, InkAfter;
        public bool IsColor;
        public float Seconds, WorkSeconds;
        public float CompletedAt = -1;
    }
    [Serializable] public sealed class CampaignFlaskRecord
    {
        public int Wave, Amount, BalanceAfter;
        public InkColor Color;
    }
    [Serializable] public sealed class CampaignBuildRecord
    {
        public string EntryId;
        public int Wave, X, Y, Cost, InkAfter;
        public float Seconds, FacingDegrees;
    }
    [Serializable] public sealed class CampaignWaveRecord
    {
        public int Wave, Kills, Ink, Builds, SurvivingStructures, PeakAlive;
        // Cumulative spawn counters at the same instant as Kills, indexed by the respective enums.
        public int[] SpawnedByKind, SpawnedByDrawingTier;
        public float BaseHealth, Seconds;
        public string Outcome;
    }
    [Serializable] public sealed class CampaignPlayabilityResult
    {
        public string MissionId, Policy, Outcome, Revision;
        public int Seed, StartingInk, Ink, CompletedWaves, PlannedWaves, Kills, ExpectedEnemies, Ticks, PaidInk, RefundedInk;
        public float StartingBaseHealth, BaseHealth, Seconds, RepairedHealth;
        public double WallMilliseconds;
        public bool Passed, FieldModernization;
        public int MaximumRank, GlobalUpgradeChoices, CompletedModernizations, PaidColoredInk;
        public List<CampaignModernizationRecord> Modernizations = new List<CampaignModernizationRecord>();
        public List<CampaignFlaskRecord> Flasks = new List<CampaignFlaskRecord>();
        public List<CampaignBuildRecord> Builds = new List<CampaignBuildRecord>();
        public List<CampaignWaveRecord> Waves = new List<CampaignWaveRecord>();
    }

    /// <summary>Paid assembly commands against authored missions, shared by Unity and the standalone harness.</summary>
    public static class CampaignPlayabilityScenario
    {
        public const float StepSeconds = 1f / 30f;
        public const int MaximumTicksPerWave = 18000;
        public const double MaximumWallSeconds = 120;
        public const string Policy = "field-mixed-v2";
        static readonly InkColor[] FlaskColors = { InkColor.Green, InkColor.Red, InkColor.Blue };

        public static CampaignPlayabilityResult Run(GameBalance baseline, ComponentCatalog catalog,
            CampaignMissionDefinition mission, bool defend = true, int? seed = null, bool gunOnly = false, CampaignDefensePolicy policy = CampaignDefensePolicy.Mixed)
        {
            if (!Enum.IsDefined(typeof(CampaignDefensePolicy), policy)) throw new ArgumentOutOfRangeException(nameof(policy));
            var configured = baseline.CreateSnapshot();
            configured.Mechanics.FieldModernization = true;
            var balance = MissionBalanceFactory.Create(configured, mission);
            // A first campaign traversal has only victories on preceding pages saved.
            balance.Mechanics.MaximumModernizationRank = mission.Index == 0 ? 1 : mission.Index < 3 ? 2 : 3;
            var profile = WorkshopProfile.CreateNew(catalog, balance.ResolveContent(), balance);
            var loadout = new RunLoadoutSnapshot(profile.GetLoadoutEntries(), balance);
            var resolved = new Dictionary<string, ResolvedEntry>(StringComparer.Ordinal);
            for (int i = 0; i < loadout.Entries.Count; i++)
                if (loadout.Entries[i] != null) resolved.Add(loadout.Entries[i].Id, loadout.Resolved[i]);
            var session = new GameSession(balance, seed ?? mission.Seed, false);
            var result = new CampaignPlayabilityResult {
                MissionId = mission.Id, Policy = defend ? gunOnly ? "field-gun-only-v2" : policy == CampaignDefensePolicy.Mixed ? Policy : policy == CampaignDefensePolicy.Expansion ? "field-expansion-v2" : "field-concentrated-v2" : "no-defense", Revision = catalog.BalanceRevision,
                FieldModernization = session.Mechanics.FieldModernization, MaximumRank = session.Mechanics.MaximumModernizationRank,
                Seed = seed ?? mission.Seed, StartingInk = session.Ink, StartingBaseHealth = session.BaseHealth,
                PlannedWaves = mission.WaveCount, ExpectedEnemies = balance.Waves.Sum(w => w.Count)
            };
            var timer = Stopwatch.StartNew();
            var planned = Orders(session.BaseCell, mission, balance);
            if (mission.Index == 3) planned = CreekOutposts(planned, session.BaseCell);
            if (mission.Index == 8) planned = ArchiveBatteries(planned, session.BaseCell);
            var orders = planned.ToArray();
            int purchase = 0;
            int reserve = mission.Index >= 3 ? resolved["album.guard"].Cost : 0;
            int openingCount = mission.Index == 0 ? 6 : mission.Index == 1 || mission.Index == 4 ? 8 : mission.Index == 2 ? 11 : mission.Index == 3 ? 8 : 14;
            bool Build(string id, Cell cell, float? chosenHeading = null)
            {
                var entry = resolved[id];
                int before = session.Ink;
                float heading = chosenHeading ?? PlacementBearing(session, cell, entry, mission.Index);
                if (!session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out _, id, facingDegrees: heading)) return false;
                result.Builds.Add(new CampaignBuildRecord {
                    EntryId = id, Wave = session.WaveIndex, X = cell.X, Y = cell.Y, FacingDegrees = session.StructureAt(cell).FacingDegrees,
                    Cost = before - session.Ink, InkAfter = session.Ink, Seconds = session.Elapsed
                });
                return true;
            }
            void Buy(int maximum = int.MaxValue)
            {
                int bought = 0;
                while (purchase < orders.Length && bought < maximum && (policy != CampaignDefensePolicy.Concentrated || purchase < openingCount))
                {
                    var order = orders[purchase];
                    string id = gunOnly ? "album.guard" : order.id;
                    if (session.Ink < resolved[id].Cost + reserve) break;
                    bool built = false;
                    foreach (var cell in Nearby(order.cell))
                        if (Build(id, cell)) { built = true; bought++; break; }
                    purchase++;
                    if (!built) continue;
                }
            }
            bool Modernize(ModernizationQuote quote)
            {
                if (!quote.Valid || (!quote.IsColor && session.Ink - quote.Cost < reserve) || !session.TryModernize(quote, out _)) return false;
                result.Modernizations.Add(new CampaignModernizationRecord {
                    StructureId = quote.StructureId, Wave = session.WaveIndex, Rank = quote.NextRank,
                    Direction = quote.Direction.ToString(), Color = quote.Color.ToString(), IsColor = quote.IsColor,
                    Cost = quote.Cost, InkAfter = quote.IsColor ? session.ColoredInk(quote.Color) : session.Ink,
                    Seconds = session.Elapsed, WorkSeconds = quote.WorkSeconds
                });
                return true;
            }
            bool UpgradeOne()
            {
                var candidates = session.Structures.Where(t => t.Health > 0 && t.IsComplete && !t.Modernization.IsWorking &&
                    t.ResolvedEntry?.Kind == AlbumEntryKind.Tower).OrderBy(t => t.Modernization.StepsUsed).ThenBy(t => t.Id).ToArray();
                foreach (var tower in candidates)
                {
                    var ranks = tower.Modernization.Ranks;
                    ModernizationDirection[] sequence = { ModernizationDirection.Power, ModernizationDirection.Resilience,
                        ModernizationDirection.Mechanics, ModernizationDirection.Power, ModernizationDirection.Resilience,
                        session.Mechanics.MaximumModernizationRank == 3 ? ModernizationDirection.Power : ModernizationDirection.Mechanics,
                        ModernizationDirection.Range };
                    foreach (var direction in sequence.Distinct().OrderBy(d => mission.Index == 7 ? ranks.Rank(d) : 0))
                    {
                        int desired = sequence.Take(6).Count(d => d == direction);
                        if (direction == ModernizationDirection.Range) desired = 1;
                        // Fill each first rank before deepening; subsequent passes buy the specified specialization.
                        if (ranks.Rank(direction) >= Math.Min(desired, session.Mechanics.MaximumModernizationRank)) continue;
                        var quote = session.QuoteModernization(tower.Id, direction);
                        if (Modernize(quote)) return true;
                    }
                }
                return false;
            }
            void ColorOne()
            {
                var richest = FlaskColors.OrderByDescending(session.ColoredInk).First();
                foreach (var tower in session.Structures.Where(t => t.Health > 0 && t.IsComplete && !t.Modernization.IsWorking &&
                    t.ResolvedEntry?.Kind == AlbumEntryKind.Tower).OrderByDescending(t => t.ResolvedEntry.Dps))
                {
                    InkColor color = tower.Color == InkColor.None ? richest : tower.Color;
                    if (Modernize(session.QuoteColorModernization(tower.Id, color))) return;
                }
            }
            void ClaimWaveFlask()
            {
                if (session.PendingInkFlasks == 0) return;
                var color = FlaskColors[result.Flasks.Count % FlaskColors.Length];
                int before = session.ColoredInk(color);
                if (!session.TryClaimInkFlask(session.RunId, session.NextInkFlaskId, color, out string reason))
                    throw new InvalidOperationException("Unable to claim earned ink: " + reason);
                result.Flasks.Add(new CampaignFlaskRecord { Wave = session.CompletedWaves, Color = color,
                    Amount = session.ColoredInk(color) - before, BalanceAfter = session.ColoredInk(color) });
            }
            int ticksInWave = 0, lastCompleted = 0, wavePeak = 0;
            var reinforcedSpitters = new HashSet<int>();
            int lastFireTick = 0;
            float lastDamage = 0;
            long lastEvent = 0;
            float phaseStarted = 0;
            var previousPhase = session.Phase;
            for (; result.Ticks < MaximumTicksPerWave * mission.WaveCount; result.Ticks++)
            {
                if (session.Phase == RunPhase.Victory || session.Phase == RunPhase.Defeat) break;
                if (timer.Elapsed.TotalSeconds >= MaximumWallSeconds || ticksInWave >= MaximumTicksPerWave) break;
                if (session.Phase != previousPhase)
                { previousPhase = session.Phase; phaseStarted = session.Elapsed; ticksInWave = 0; }
                if (session.PendingUpgrade) throw new InvalidOperationException("Field progression generated a free global upgrade.");
                if (defend && result.Ticks % 60 == 0)
                {
                    if (session.Statistics.TotalDamage > lastDamage)
                    {
                        lastDamage = session.Statistics.TotalDamage;
                        lastFireTick = result.Ticks;
                    }
                    if (mission.Index >= 4 && policy == CampaignDefensePolicy.Mixed)
                    {
                        // Replace missing sections of the inner perimeter from earned ink.
                        for (int x = -2; x <= 2; x++)
                            for (int y = -2; y <= 2; y++)
                            {
                                if (Math.Max(Math.Abs(x), Math.Abs(y)) != 2 || session.Ink < reserve + resolved["album.wall"].Cost) continue;
                                var cell = new Cell(session.BaseCell.X + x, session.BaseCell.Y + y);
                                if (session.StructureAt(cell) == null) Build("album.wall", cell);
                            }
                    }
                    if (reserve > 0 && policy != CampaignDefensePolicy.Concentrated && session.Phase == RunPhase.Assault &&
                        session.RemainingToSpawn == 0 && session.Enemies.Count > 0 && session.Enemies.Count <= 6)
                    {
                        bool stalled = result.Ticks - lastFireTick >= 20 * 30;
                        var target = session.Enemies[0].Position;
                        bool finalHoldout = session.WaveIndex == mission.WaveCount && session.Enemies.Count == 1 && !Covered(session, target);
                        if ((stalled || finalHoldout) && session.Ink < reserve)
                        {
                            var distant = session.Structures.Where(t => t.ResolvedEntry?.Kind == AlbumEntryKind.Tower && !t.Modernization.IsWorking)
                                .OrderByDescending(t => DistanceSquared(t.Cell, target)).FirstOrDefault();
                            if (distant != null && session.TryErase(distant.Cell, out int refund)) result.RefundedInk += refund;
                        }
                        if (session.Ink >= reserve && PlaceReserve(session, (cell, heading) => Build("album.guard", cell, heading), stalled))
                            lastFireTick = result.Ticks;
                    }
                    // With a single objective, ranged attackers can gather just outside the castle's defenses.
                    int firstGuardWave = mission.Index == 4 || mission.Index == 8 ? 1 : 3;
                    if (mission.Index >= 4 && policy == CampaignDefensePolicy.Mixed && session.Phase == RunPhase.Assault &&
                        session.WaveIndex >= firstGuardWave && session.Ink >= reserve)
                    {
                        foreach (var enemy in session.Enemies.Where(e => e.Health > 0 && e.Kind == EnemyKind.Spitter &&
                            DistanceSquared(session.BaseCell, e.Position) <= 16 * 16 && !reinforcedSpitters.Contains(e.Id))
                            .OrderBy(e => DistanceSquared(session.BaseCell, e.Position)))
                        {
                            if (!BuildToward(session, enemy.Position, (cell, heading) => Build("album.guard", cell, heading))) continue;
                            reinforcedSpitters.Add(enemy.Id);
                            break;
                        }
                    }
                    bool preparing = session.Phase == RunPhase.Planning || session.Phase == RunPhase.Intermission;
                    if (preparing) ClaimWaveFlask();
                    if (policy == CampaignDefensePolicy.Expansion || gunOnly) Buy();
                    else if (purchase < openingCount) Buy(openingCount - purchase);
                    else
                    {
                        if (preparing)
                        {
                            if (policy == CampaignDefensePolicy.Mixed) ColorOne();
                            UpgradeOne();
                        }
                        if (policy == CampaignDefensePolicy.Mixed) Buy(1);
                    }
                }
                if ((session.Phase == RunPhase.Planning || session.Phase == RunPhase.Intermission) &&
                    session.Elapsed - phaseStarted >= 10) session.StartNextWave();
                session.Tick(StepSeconds);
                wavePeak = Math.Max(wavePeak, session.Enemies.Count);
                for (int eventIndex = session.EventBuffer.Count - 1; eventIndex >= 0; eventIndex--)
                {
                    var item = session.EventBuffer[eventIndex];
                    if (item.Sequence <= lastEvent) break;
                    if (item.Kind != SimulationEventKind.ModernizationCompleted) continue;
                    var work = result.Modernizations.LastOrDefault(u => u.StructureId == item.SourceStructureId && u.CompletedAt < 0);
                    if (work != null) { work.CompletedAt = (float)item.SimulationSeconds; result.CompletedModernizations++; }
                }
                lastEvent = session.EventBuffer.LatestSequence;
                ticksInWave++;
                if (session.CompletedWaves != lastCompleted)
                {
                    lastCompleted = session.CompletedWaves;
                    result.Waves.Add(Record(session, result.Builds.Count, wavePeak));
                    wavePeak = 0;
                }
            }
            timer.Stop();
            if (result.Waves.Count == 0 || result.Waves[result.Waves.Count - 1].Wave != session.WaveIndex ||
                session.Phase == RunPhase.Defeat) result.Waves.Add(Record(session, result.Builds.Count, wavePeak));
            result.Outcome = session.Phase == RunPhase.Victory || session.Phase == RunPhase.Defeat ? session.Phase.ToString() : "Timeout";
            result.CompletedWaves = session.CompletedWaves; result.Kills = session.Kills;
            result.BaseHealth = session.BaseHealth; result.Ink = session.Ink; result.Seconds = session.Elapsed;
            result.PaidInk = session.Statistics.BuildInvestment + session.Statistics.UpgradeInvestment;
            result.GlobalUpgradeChoices = session.Statistics.Upgrades.Count;
            result.PaidColoredInk = result.Modernizations.Where(u => u.IsColor).Sum(u => u.Cost);
            result.RepairedHealth = session.Statistics.TotalRepairedHealth;
            result.WallMilliseconds = timer.Elapsed.TotalMilliseconds;
            result.Passed = defend
                ? result.Outcome == "Victory" && result.CompletedWaves == mission.WaveCount &&
                    result.Kills == result.ExpectedEnemies && session.BaseHealth > 0 && result.PaidInk > 0 && result.GlobalUpgradeChoices == 0
                : result.Outcome == "Defeat" && result.Builds.Count == 0 && result.PaidInk == 0;
            return result;
        }
        // A distant survivor can attack from outside every fixed sector. Spend the held construction ink on that lane.
        static bool PlaceReserve(GameSession session, Func<Cell, float, bool> build, bool stalled)
        {
            foreach (var enemy in session.Enemies)
            {
                if (enemy.Health <= 0 || (!stalled && Covered(session, enemy.Position))) continue;
                if (BuildToward(session, enemy.Position, build)) return true;
            }
            return false;
        }

        static bool BuildToward(GameSession session, Point2 target, Func<Cell, float, bool> build)
        {
            double dx = session.BaseCell.X + .5 - target.X, dy = session.BaseCell.Y + .5 - target.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double fraction = distance > 4 ? 4 / distance : 1;
            var position = new Cell((int)Math.Floor(target.X + dx * fraction), (int)Math.Floor(target.Y + dy * fraction));
            foreach (var cell in Nearby(position))
            {
                float heading = (float)(Math.Atan2(target.Y - cell.Y - .5, target.X - cell.X - .5) * 180 / Math.PI);
                if (build(cell, heading)) return true;
            }
            return false;
        }

        static bool Covered(GameSession session, Point2 target)
        {
            foreach (var tower in session.Structures)
            {
                if (tower.Health <= 0 || tower.ResolvedEntry == null) continue;
                double dx = target.X - tower.Cell.X - .5, dy = target.Y - tower.Cell.Y - .5;
                double distanceSquared = dx * dx + dy * dy;
                double bearing = Math.Atan2(dy, dx) * 180 / Math.PI;
                double angle = Math.Abs((bearing - tower.FacingDegrees + 540) % 360 - 180);
                foreach (var weapon in tower.ResolvedEntry.Weapons)
                    if (weapon.Damage > 0 && distanceSquared >= weapon.MinimumRange * weapon.MinimumRange &&
                        distanceSquared <= weapon.Range * weapon.Range && angle <= weapon.FiringArcDegrees * .5) return true;
            }
            return false;
        }

        static double DistanceSquared(Cell cell, Point2 point)
        {
            double dx = cell.X + .5 - point.X, dy = cell.Y + .5 - point.Y;
            return dx * dx + dy * dy;
        }

        static float PlacementBearing(GameSession session, Cell cell, ResolvedEntry entry, int mission)
        {
            int x = cell.X - session.BaseCell.X, y = cell.Y - session.BaseCell.Y;
            if (entry.Kind == AlbumEntryKind.Tower)
            {
                if (mission == 8 || mission == 5 && entry.Weapons[0].Family != AssemblyWeaponFamily.Mortar)
                    return (float)(Math.Atan2(-y, -x) * 180 / Math.PI);
                if (mission == 1) return 90;
                if (mission == 7) return 0;
            }
            return session.DefaultFacingDegrees(cell, StructureKind.Gun, entry);
        }

        static IEnumerable<(string id, Cell cell)> CreekOutposts(IEnumerable<(string id, Cell cell)> planned, Cell center)
        {
            // Catch the western bridge column and the southern bank before either reaches the central guns.
            Cell[] guardOffsets = { new Cell(-9, 2), new Cell(2, -9), new Cell(-9, 0), new Cell(0, -9), new Cell(-9,-2), new Cell(-2,-9) };
            Cell[] piercerOffsets = { new Cell(-9, -1), new Cell(2, -9), new Cell(-9, 1), new Cell(-1, -9) };
            int guards = 0, piercers = 0;
            foreach (var order in planned)
            {
                Cell offset;
                if (order.id == "album.guard") offset = guardOffsets[guards++ % guardOffsets.Length];
                else if (order.id == "album.piercer") offset = piercerOffsets[piercers++ % piercerOffsets.Length];
                else { yield return order; continue; }
                yield return (order.id, new Cell(center.X + offset.X, center.Y + offset.Y));
            }
        }

        static IEnumerable<(string id, Cell cell)> ArchiveBatteries(IEnumerable<(string id, Cell cell)> planned, Cell center)
        {
            // Short-range guns stay one cell outside the protected ring and cover the opposite flank.
            foreach (var order in planned)
            {
                int x = order.cell.X - center.X, y = order.cell.Y - center.Y;
                int radius = Math.Max(Math.Abs(x), Math.Abs(y));
                if (radius > 0 && (order.id == "album.guard" || order.id == "album.piercer"))
                    yield return (order.id, new Cell(center.X + (int)Math.Round(3d * x / radius),
                        center.Y + (int)Math.Round(3d * y / radius)));
                else yield return order;
            }
        }

        static CampaignWaveRecord Record(GameSession session, int builds, int peakAlive) => new CampaignWaveRecord {
            Wave = session.WaveIndex, Kills = session.Kills, Ink = session.Ink, Builds = builds,
            SpawnedByKind = session.Statistics.SpawnedByKind.ToArray(),
            SpawnedByDrawingTier = session.Statistics.SpawnedByDrawingTier.ToArray(),
            PeakAlive = peakAlive, BaseHealth = session.BaseHealth, Seconds = session.Elapsed, Outcome = session.Phase.ToString(),
            SurvivingStructures = session.Structures.Count(s => s.Health > 0)
        };
        static IEnumerable<(string id, Cell cell)> Orders(Cell center, CampaignMissionDefinition mission, GameBalance balance)
        {
            var directions = new List<Cell>();
            // Prepare for each physical front in the order that it first joins the assault.
            int admitted = 0;
            foreach (var wave in balance.Waves)
            {
                int next = wave.Sides & ~admitted;
                if ((next & 1) != 0) directions.Add(new Cell(-1, 0));
                if ((next & 2) != 0) directions.Add(new Cell(1, 0));
                if ((next & 4) != 0) directions.Add(new Cell(0, 1));
                if ((next & 8) != 0) directions.Add(new Cell(0, -1));
                admitted |= next;
                // Keep the opening-page reference strategy used by the four-front siege comparison.
                if (mission.Index == 0) break;
            }
            if (mission.Index == 0)
            {
                // From behind the castle the west-facing guns cover both the approach and the castle walls.
                yield return ("album.guard", new Cell(center.X + 1, center.Y));
                yield return ("album.guard", new Cell(center.X + 1, center.Y - 2));
                yield return ("album.piercer", new Cell(center.X + 1, center.Y + 2));
                yield return ("album.slow", new Cell(center.X - 7, center.Y));
                yield return ("album.mortar", new Cell(center.X - 2, center.Y - 2));
            }
            else
            {
                yield return ("album.mortar", new Cell(center.X - 2, center.Y - 2));
                if (mission.Index >= 5 && mission.Index <= 7)
                {
                    yield return ("album.wall", new Cell(center.X - 1, center.Y));
                    yield return ("album.wall", new Cell(center.X + 1, center.Y));
                    yield return ("album.wall", new Cell(center.X, center.Y - 1));
                    yield return ("album.wall", new Cell(center.X, center.Y + 1));
                }
                if (mission.Index == 4 || mission.Index == 7)
                {
                    int reserveDistance = mission.Index == 4 ? 2 : 1;
                    yield return ("album.guard", new Cell(center.X - reserveDistance, center.Y - reserveDistance));
                    yield return ("album.guard", new Cell(center.X + reserveDistance, center.Y + reserveDistance));
                }
                if (mission.Index == 1)
                {
                    // Put the two north-facing batteries behind the castle so both mountain passes meet their sectors.
                    yield return ("album.guard", new Cell(center.X - 2, center.Y - 3));
                    yield return ("album.guard", new Cell(center.X + 2, center.Y - 3));
                    yield return ("album.piercer", new Cell(center.X - 1, center.Y - 4));
                    yield return ("album.piercer", new Cell(center.X + 1, center.Y - 4));
                }
                else
                {
                    foreach (var d in directions)
                        yield return ("album.guard", mission.Index == 3
                            ? new Cell(center.X + 4 * d.Y, center.Y + 4 * d.Y)
                            : new Cell(center.X + d.X * 5, center.Y + d.Y * 5));
                    foreach (var d in directions) yield return ("album.piercer", new Cell(center.X + d.X * 4 - d.Y, center.Y + d.Y * 4 + d.X));
                    if (mission.Index == 3)
                    {
                        yield return ("album.guard", new Cell(center.X + 3, center.Y + 1));
                        yield return ("album.guard", new Cell(center.X - 3, center.Y - 1));
                        yield return ("album.guard", new Cell(center.X + 3, center.Y - 1));
                    }
                }
            }
            yield return ("album.repair", new Cell(center.X + 2, center.Y - 2));
            if (mission.Index != 0)
                foreach (var d in directions) yield return ("album.slow", new Cell(center.X + d.X * 7, center.Y + d.Y * 7));
            if (mission.Index == 2)
            {
                yield return ("album.guard", new Cell(center.X + 1, center.Y + 1));
                yield return ("album.guard", new Cell(center.X - 1, center.Y - 1));
            }
            for (int ring = 0; ring < 12; ring++)
                foreach (var d in directions)
                {
                    string id = ring % 3 == 0 ? "album.mortar" : ring % 3 == 1 ? "album.piercer" : "album.guard";
                    int radius = 3 + ring / 2;
                    yield return (id, new Cell(center.X + d.X * radius + d.Y * (ring % 3 - 1) * 2,
                        center.Y + d.Y * radius - d.X * (ring % 3 - 1) * 2));
                }
        }
        static IEnumerable<Cell> Nearby(Cell center)
        {
            yield return center;
            for (int radius = 1; radius <= 3; radius++)
                for (int y = -radius; y <= radius; y++)
                    for (int x = -radius; x <= radius; x++)
                        if (Math.Max(Math.Abs(x), Math.Abs(y)) == radius)
                            yield return new Cell(center.X + x, center.Y + y);
        }
    }
}
