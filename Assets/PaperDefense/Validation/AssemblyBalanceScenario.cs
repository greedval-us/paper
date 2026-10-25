using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Validation
{
    [Serializable]
    public sealed class AssemblyBalancePlacement
    {
        public string EntryId;
        public int X, Y, Cost;
        public float Seconds;
    }

    [Serializable]
    public sealed class AssemblyBalanceTarget
    {
        public int Id;
        public string Kind, DrawingTier;
        public float X, Y, Health;
    }

    [Serializable]
    public sealed class AssemblyBalanceRecoveryAction
    {
        public string Command, Reason, EntryId;
        public int StructureId, X, Y, Refund, InkBefore, InkAfter, WaveIndex, Kills;
        public int TargetStructureId, TargetX, TargetY;
        public float Seconds, NoKillSeconds;
        public int[] LiveEnemyIds = Array.Empty<int>();
    }

    /// <summary>Applicability flags distinguish an unavailable measurement from a measured zero.</summary>
    [Serializable]
    public sealed class AssemblyBalanceRow
    {
        public string Fixture, RecipeId, RecipeName, Revision, Role, Scenario, Outcome, Notes;
        public int Seed, Budget, StartingInk, RemainingInk, UnitCost, Cost, Copies, FixtureCost;
        public int Leaks, UnresolvedEnemies, BlotsCleared, UnresolvedBlots, SurvivingStructures, DestroyedStructures;
        public int Income, NetResource, CompletedWaves, PaidWaves, Kills, SpawnedEnemies, BaseHits;
        public int Generators, SimulationTicks, RefundedInk;
        public int EnemyCount, BlotCount, MaximumTicks;
        public string EnemyKind, EnemyDrawingTier;
        public bool Repair, DamageApplicable, LeaksApplicable, CastleApplicable, ControlApplicable;
        public bool RepairApplicable, IncomeApplicable, CleaningApplicable;
        public float Damage, IdleSeconds, DurationSeconds, StartingBaseHealth, BaseHealth, BaseDamageTaken;
        public float PreventedMovement, RepairedHealth, SurvivingStructureHealth, BuildSeconds;
        public float EnemyHealth, EnemySpeed, EnemySpawnInterval, StepSeconds;
        public float SpawnX, SpawnY, SpawnYVariation, ExitX, RepairTargetInitialHealth, RepairTargetMaximumHealth;
        public int[] SpawnedByKind = Array.Empty<int>(), SpawnedByDrawingTier = Array.Empty<int>();
        public string[] Composition = Array.Empty<string>();
        public AssemblyBalancePlacement[] Placements = Array.Empty<AssemblyBalancePlacement>();
        public AssemblyBalanceTarget[] InitialTargets = Array.Empty<AssemblyBalanceTarget>();
        public AssemblyBalanceRecoveryAction[] RecoveryActions = Array.Empty<AssemblyBalanceRecoveryAction>();
    }

    [Serializable]
    public sealed class AssemblyBalanceEvidence
    {
        public int SchemaVersion = 1;
        public string Source = "PlayerBalance -> PlayerRules -> PlayerContent -> PlayerAssemblyCatalog";
        public string Revision, Scope;
        public AssemblyBalanceRow[] Rows;
        public string[] UniversalDominance;
        public WaveDefinition[] CampaignWaves;
    }

    /// <summary>Measured composite recipes, production firing lanes and paid eight-wave campaigns. No profile store.</summary>
    public static class AssemblyBalanceScenario
    {
        public const string PlayerBalancePath = "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset";
        public const string ResultsPath = "docs/validation/modular-album/assembly-balance-results.json";
        static readonly int[] Seeds = { 1337, 2026, 4242 }, Budgets = { 200, 400, 800 };
        static readonly TrialScenario[] Lanes = { TrialScenario.Fast, TrialScenario.Crowd, TrialScenario.Armored };
        const float CampaignDelta = 1f / 30f;
        const int MaximumCampaignTicks = 60000;

        public static void LoadRuntime(out GameBalance balance, out ComponentCatalog catalog)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(PlayerBalancePath);
            if (asset == null || asset.dataPreset == null || asset.dataPreset.database == null ||
                asset.dataPreset.database.assemblyCatalog == null)
                throw new InvalidOperationException("The live PlayerBalance / PlayerRules / PlayerContent / AssemblyCatalog chain is incomplete.");
            balance = asset.CreateWorkshopBalance();
            catalog = asset.dataPreset.database.assemblyCatalog.CreateSnapshot();
        }

        public static List<AssemblyBalanceRow> Run(int seed, int budget, TrialScenario scenario)
        {
            LoadRuntime(out var balance, out var catalog);
            return Run(balance, catalog, seed, budget, scenario);
        }

        public static List<AssemblyBalanceRow> Run(GameBalance balance, ComponentCatalog catalog,
            int seed, int budget, TrialScenario scenario)
        {
            BalanceValidation.Validate(balance);
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!Enum.IsDefined(typeof(TrialScenario), scenario)) throw new ArgumentOutOfRangeException(nameof(scenario));
            if (budget < 0 || budget > TrialRules.MaximumBudget) throw new ArgumentOutOfRangeException(nameof(budget));
            var owned = balance.CreateSnapshot();
            var rows = new List<AssemblyBalanceRow>();
            foreach (var entry in TowerEntries(catalog))
                rows.Add(Measure(entry, owned, (AssemblyTrialScenario)scenario, budget, seed));
            foreach (var entry in catalog.Starters.Where(e => e.Kind == AlbumEntryKind.Structure))
            {
                var family = entry.Snapshot.Recipe.Structure.Family;
                var fixture = family == AssemblyStructureFamily.Repair ? AssemblyTrialScenario.Repair :
                    family == AssemblyStructureFamily.Generator ? AssemblyTrialScenario.Generator : (AssemblyTrialScenario)scenario;
                if (AssemblyTrialRunner.ScenariosFor(entry).Contains(fixture))
                    rows.Add(Measure(entry, owned, fixture, budget, seed));
            }
            return rows;
        }

        public static IReadOnlyList<AlbumEntry> TowerEntries(ComponentCatalog catalog)
        {
            var result = catalog.Starters.Where(e => e.Kind == AlbumEntryKind.Tower).ToList();
            foreach (string core in new[] { "fan", "ricochet", "stapler", "blotter" })
                result.Add(CreateTower(catalog, "balance." + core, core));
            result.Add(CreateTower(catalog, "balance.gun-stapler", "gun", "stapler"));
            result.Add(CreateTower(catalog, "balance.mortar-stapler", "mortar", "stapler"));
            result.Add(CreateTower(catalog, "balance.gun-blotter", "gun", "blotter"));
            return result.AsReadOnly();
        }

        public static AlbumEntry CreateTower(ComponentCatalog catalog, string id, params string[] cores)
        {
            PartRef P(string part) => new PartRef(part, 1);
            var foundation = new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
            var mounts = cores.Select((core, slot) => new WeaponMountRecipe(slot, new WeaponRecipe(
                P("core." + core), P("head." + core), P("sight.iron"), P("magazine.standard"), P("trigger.standard"))));
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, mounts));
            var used = catalog.Definitions.Where(d => recipe.Parts.Any(p => p.Id == d.Id && p.Revision == d.Revision));
            return new AlbumEntry(id, string.Join(" + ", cores), 1, new RecipeSnapshot(recipe, catalog.BalanceRevision, used));
        }

        static AssemblyBalanceRow Measure(AlbumEntry entry, GameBalance balance, AssemblyTrialScenario scenario, int budget, int seed)
        {
            var measured = AssemblyTrialRunner.Run(entry, balance, scenario, budget, seed);
            var recording = measured.Recording;
            var initial = recording.Frames[0];
            var final = recording.Frames[recording.Frames.Count - 1];
            string role = entry.Kind == AlbumEntryKind.Tower ?
                measured.Resolved.Weapons.All(w => w.Family == AssemblyWeaponFamily.Blotter) ? "Cleaner" : "Tower" :
                measured.Resolved.Support.Kind.ToString();
            bool tower = entry.Kind == AlbumEntryKind.Tower;
            var row = new AssemblyBalanceRow {
                Fixture = tower ? "FiringLane" : "SupportFixture", RecipeId = entry.Id, RecipeName = entry.Name,
                Revision = entry.Snapshot.BalanceRevision, Role = role, Scenario = scenario.ToString(),
                Seed = seed, Budget = budget, StartingInk = budget, UnitCost = measured.UnitCost,
                Cost = measured.Cost, Copies = measured.Copies, BuildSeconds = measured.Resolved.BuildSeconds,
                DamageApplicable = measured.Damage.HasValue, Damage = measured.Damage ?? 0,
                LeaksApplicable = measured.Leaks.HasValue, Leaks = measured.Leaks ?? 0,
                UnresolvedEnemies = measured.UnresolvedEnemies ?? final.Enemies.Count,
                IdleSeconds = measured.IdleSeconds ?? 0, DurationSeconds = measured.DurationSeconds,
                SimulationTicks = (int)Math.Round(measured.DurationSeconds / balance.Trials.StepSeconds),
                ControlApplicable = measured.PreventedMovement.HasValue, PreventedMovement = measured.PreventedMovement ?? 0,
                RepairApplicable = measured.RepairedHealth.HasValue, RepairedHealth = measured.RepairedHealth ?? 0,
                IncomeApplicable = measured.Income.HasValue, Income = measured.Income ?? 0,
                CompletedWaves = measured.CompletedWaves ?? 0, PaidWaves = measured.PaidWaves ?? 0,
                CleaningApplicable = measured.BlotsCleared.HasValue, BlotsCleared = measured.BlotsCleared ?? 0,
                UnresolvedBlots = measured.UnresolvedBlots ?? 0,
                SurvivingStructures = recording.SurvivingTowers,
                SurvivingStructureHealth = final.Towers.Where(t => tower || t.SourceAlbumEntryId == entry.Id).Sum(t => t.Health),
                Composition = new[] { entry.Id },
                InitialTargets = initial.Enemies.Select(e => new AssemblyBalanceTarget {
                    Id = e.Id, Kind = e.Kind.ToString(), DrawingTier = e.DrawingTier.ToString(),
                    X = e.Position.X, Y = e.Position.Y, Health = e.Health
                }).ToArray(),
                Placements = initial.Towers.Select(t => new AssemblyBalancePlacement {
                    EntryId = t.SourceAlbumEntryId ?? (tower ? entry.Id : "fixture.wall"), X = t.Cell.X, Y = t.Cell.Y,
                    Cost = tower || t.SourceAlbumEntryId == entry.Id ? measured.UnitCost : 0
                }).ToArray(),
                Notes = tower ? "Production WeaponSystem; complete towers; fixed lane targets and no castle or campaign rewards. Mixed control utility is measured by changed leaks, not synthetic DPS." :
                    "Production GameSession support fixture. Repair has an independent damaged wall; generator uses eight empty eligible waves, separate from the real campaign. Slow control is paired movement prevented against a no-slow reference."
            };
            row.RemainingInk = budget - row.Cost + row.Income;
            row.NetResource = row.Income - row.Cost;
            row.StepSeconds = balance.Trials.StepSeconds;
            row.MaximumTicks = balance.Trials.MaximumTicks;
            if (tower || scenario == AssemblyTrialScenario.Crowd || scenario == AssemblyTrialScenario.Fast || scenario == AssemblyTrialScenario.Armored)
            {
                var target = balance.Trials.For(scenario == AssemblyTrialScenario.Fast ? TrialScenario.Fast :
                    scenario == AssemblyTrialScenario.Armored ? TrialScenario.Armored : scenario == AssemblyTrialScenario.Blots ? TrialScenario.Blots : TrialScenario.Crowd);
                row.EnemyCount = target.Count; row.EnemyHealth = target.Health; row.EnemySpeed = target.Speed;
                row.EnemySpawnInterval = target.SpawnInterval; row.EnemyKind = target.EnemyKind.ToString();
                row.EnemyDrawingTier = target.DrawingTier.ToString();
            }
            if (tower)
            {
                row.SpawnX = balance.Trials.SpawnX; row.SpawnY = balance.Trials.SpawnY;
                row.SpawnYVariation = balance.Trials.SpawnYVariation; row.ExitX = balance.Trials.ExitX;
            }
            if (scenario == AssemblyTrialScenario.Blots) row.BlotCount = balance.Trials.BlotCount;
            if (measured.RepairedHealth.HasValue)
            {
                row.FixtureCost = TowerCatalog.ToolCost(balance, StructureKind.Wall, MaterialTier.Pencil);
                row.RepairTargetInitialHealth = 20; row.RepairTargetMaximumHealth = 100000;
            }
            row.Outcome = row.UnresolvedEnemies > 0 || row.UnresolvedBlots > 0 ? "DurationCap" : "Finished";
            return row;
        }

        public static List<AssemblyBalanceRow> RunMatrix(GameBalance balance, ComponentCatalog catalog)
        {
            var rows = new List<AssemblyBalanceRow>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (int seed in Seeds) foreach (int budget in Budgets)
            {
                foreach (var lane in Lanes)
                    foreach (var row in Run(balance, catalog, seed, budget, lane))
                        if (seen.Add(row.RecipeId + "/" + row.Scenario + "/" + seed + "/" + budget)) rows.Add(row);
                foreach (var entry in TowerEntries(catalog))
                    rows.Add(Measure(entry, balance, AssemblyTrialScenario.Blots, budget, seed));
            }
            return rows;
        }

        public static AssemblyBalanceRow RunCampaign(GameBalance source, ComponentCatalog catalog,
            int seed, int generators, bool repair, Action<GameSession> observe = null, bool recoverStalemates = true)
        {
            if (generators < 0 || generators > 2) throw new ArgumentOutOfRangeException(nameof(generators));
            if (source.Waves.Count != 8) throw new ArgumentException("The campaign requires the live eight wave definitions.", nameof(source));
            var rules = source.CreateSnapshot(); var session = new GameSession(rules, seed, false);
            var orders = new List<AssemblyBalancePlacement>();
            var recoveries = new List<AssemblyBalanceRecoveryAction>();
            var resolved = catalog.Starters.ToDictionary(e => e.Id, e => TowerCatalog.ResolveEntry(e, rules).Value, StringComparer.Ordinal);

            bool Build(string id, Cell cell, bool required = false)
            {
                var entry = resolved[id];
                if (!session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out string reason, id))
                {
                    if (required) throw new InvalidOperationException(id + ": " + reason);
                    return false;
                }
                orders.Add(new AssemblyBalancePlacement { EntryId = id, X = cell.X, Y = cell.Y, Cost = entry.Cost, Seconds = session.Elapsed });
                return true;
            }

            var center = session.BaseCell;
            if (generators >= 1) Build("album.generator", new Cell(center.X - 2, center.Y), true);
            if (generators >= 2) Build("album.generator", new Cell(center.X + 2, center.Y), true);
            if (repair) Build("album.repair", new Cell(center.X, center.Y + 2), true);
            Build("album.wall", new Cell(center.X, center.Y + 3), true);
            var cells = new List<Cell>();
            var directions = new[] { new Cell(-1, 0), new Cell(1, 0), new Cell(0, -1), new Cell(0, 1),
                new Cell(-1, -1), new Cell(1, -1), new Cell(-1, 1), new Cell(1, 1) };
            foreach (int radius in new[] { 3, 4, 6, 7, 8, 9 }) foreach (var direction in directions)
                cells.Add(new Cell(center.X + radius * direction.X, center.Y + radius * direction.Y));
            string[] battery = { "album.guard", "album.double", "album.piercer", "album.mortar" };
            int purchaseIndex = 0;
            void BuyBattery()
            {
                foreach (var cell in cells)
                {
                    if (session.StructureAt(cell) != null) continue;
                    string id = battery[purchaseIndex % battery.Length];
                    if (session.Ink < resolved[id].Cost) break;
                    if (Build(id, cell)) purchaseIndex++;
                }
            }
            float phaseStarted = 0, lastKillSeconds = 0;
            var observedPhase = session.Phase; int ticks = 0, observedKills = 0;

            void RecoverUncoveredRepairStalemate()
            {
                if (!recoverStalemates || session.Phase != RunPhase.Assault || session.RemainingToSpawn != 0 ||
                    session.Enemies.Count == 0 || session.Elapsed - lastKillSeconds < 30) return;

                // A ranged attacker can keep shooting a repaired outer blocker beyond the
                // fixed battery's coverage. Leave combat rules intact and use a player command.
                foreach (var enemy in session.Enemies)
                {
                    if (enemy.Health <= 0) continue;
                    foreach (var tower in session.Structures)
                    {
                        if (tower.Health <= 0 || !tower.IsComplete || tower.BuildRemaining > 0 ||
                            tower.ResolvedEntry?.Kind != AlbumEntryKind.Tower) continue;
                        double dx = tower.Cell.X + .5d - enemy.Position.X;
                        double dy = tower.Cell.Y + .5d - enemy.Position.Y;
                        double distance = dx * dx + dy * dy;
                        foreach (var weapon in tower.ResolvedEntry.Weapons)
                            if (weapon.Attack != AttackKind.Cleanup && weapon.Range > 0 &&
                                distance >= weapon.MinimumRange * (double)weapon.MinimumRange &&
                                distance <= weapon.Range * (double)weapon.Range) return;
                    }
                }

                var repairRates = StructureSupportRules.RepairRates(session.Structures);
                foreach (var post in session.Structures)
                {
                    var support = post.ResolvedEntry?.Support;
                    if (post.Health <= 0 || !post.IsComplete || post.BuildRemaining > 0 ||
                        support?.Kind != AssemblyStructureFamily.Repair) continue;
                    var repairedTarget = repairRates.Keys.Where(target => {
                        double dx = post.Cell.X - target.Cell.X, dy = post.Cell.Y - target.Cell.Y;
                        return dx * dx + dy * dy <= support.Radius * (double)support.Radius;
                    }).OrderBy(target => target.Id).FirstOrDefault();
                    if (repairedTarget == null) continue;
                    var action = new AssemblyBalanceRecoveryAction {
                        Command = "Erase", Reason = "UncoveredRepairedBlocker", EntryId = post.SourceAlbumEntryId,
                        StructureId = post.Id, X = post.Cell.X, Y = post.Cell.Y, Seconds = session.Elapsed,
                        InkBefore = session.Ink, WaveIndex = session.WaveIndex, Kills = session.Kills,
                        NoKillSeconds = session.Elapsed - lastKillSeconds,
                        TargetStructureId = repairedTarget.Id, TargetX = repairedTarget.Cell.X, TargetY = repairedTarget.Cell.Y,
                        LiveEnemyIds = session.Enemies.Where(enemy => enemy.Health > 0).Select(enemy => enemy.Id).ToArray()
                    };
                    if (!session.TryErase(post.Cell, out int refund)) continue;
                    action.Refund = refund; action.InkAfter = session.Ink;
                    recoveries.Add(action);
                    lastKillSeconds = session.Elapsed;
                    break;
                }
            }

            for (; ticks < MaximumCampaignTicks; ticks++)
            {
                if (session.Phase == RunPhase.Victory || session.Phase == RunPhase.Defeat) break;
                if (session.Phase != observedPhase)
                { observedPhase = session.Phase; phaseStarted = session.Elapsed; lastKillSeconds = session.Elapsed; }
                if (session.Kills != observedKills || session.Phase != RunPhase.Assault || session.RemainingToSpawn > 0)
                { observedKills = session.Kills; lastKillSeconds = session.Elapsed; }
                if (session.Phase == RunPhase.Planning || session.Phase == RunPhase.Intermission)
                {
                    if (session.PendingUpgrade) session.ChooseUpgrade(session.WaveIndex == 4 ? RunUpgrade.RapidFire : RunUpgrade.DeepInk);
                    BuyBattery();
                    if (session.Elapsed - phaseStarted >= 10) session.StartNextWave();
                }
                if (ticks % 60 == 0) { BuyBattery(); RecoverUncoveredRepairStalemate(); }
                session.Tick(CampaignDelta);
                if (observe != null && (ticks + 1) % 300 == 0) observe(session);
            }
            var stats = session.Statistics;
            int totalPlanned = rules.Waves.Sum(w => w.Count);
            return new AssemblyBalanceRow {
                Fixture = "Campaign", RecipeId = "campaign.g" + generators + ".repair" + (repair ? "1" : "0"),
                RecipeName = "Mixed paid battery", Revision = catalog.BalanceRevision, Role = "Campaign", Scenario = "EightWaves",
                Seed = seed, Budget = rules.StartingInk, StartingInk = rules.StartingInk, RemainingInk = session.Ink,
                Cost = stats.BuildInvestment, Copies = stats.TotalStructuresBuilt, DamageApplicable = true,
                Damage = stats.TotalDamage, IdleSeconds = stats.TotalIdleSeconds, DurationSeconds = session.Elapsed,
                SimulationTicks = ticks, StartingBaseHealth = rules.BaseHealth, BaseHealth = session.BaseHealth,
                CastleApplicable = true, BaseHits = stats.BaseHits, BaseDamageTaken = stats.BaseDamageTaken,
                Repair = repair, RepairApplicable = repair, RepairedHealth = stats.TotalRepairedHealth,
                Generators = generators, IncomeApplicable = true, Income = stats.TotalGeneratorIncome,
                NetResource = stats.TotalGeneratorIncome - orders.Where(o => o.EntryId == "album.generator").Sum(o => o.Cost),
                PaidWaves = session.Structures.Sum(s => s.GeneratorPaidWaves), CompletedWaves = session.CompletedWaves,
                SurvivingStructures = session.Structures.Count(s => s.Health > 0), DestroyedStructures = stats.DestroyedStructures,
                SurvivingStructureHealth = session.Structures.Sum(s => s.Health), Kills = session.Kills,
                SpawnedEnemies = stats.TotalSpawnedEnemies, SpawnedByKind = stats.SpawnedByKind.ToArray(),
                EnemyCount = totalPlanned, StepSeconds = CampaignDelta, MaximumTicks = MaximumCampaignTicks,
                SpawnedByDrawingTier = stats.SpawnedByDrawingTier.ToArray(),
                UnresolvedEnemies = Math.Max(0, totalPlanned - session.Kills),
                Outcome = session.Phase == RunPhase.Victory ? "Victory" : session.Phase == RunPhase.Defeat ? "Defeat" : "Timeout",
                Composition = orders.Select(o => o.EntryId).Distinct().ToArray(), Placements = orders.ToArray(),
                RefundedInk = stats.RefundedInk, RecoveryActions = recoveries.ToArray(),
                Notes = (recoverStalemates ? "Campaign policy v2: after thirty seconds without kills, with no spawn backlog and every survivor outside all completed offensive mount ranges, erase an active repair post sustaining a damaged blocker using the ordinary refund command. RecoveryActions records each command; the repair treatment means an initial repair purchase and may include this erasure. " :
                    "Historical campaign policy v1: no stalemate recovery or erasure. ") +
                    "Original Player map, eight populated waves and castle HP; no starter structures or forced construction completion. Support buys reduce the same starting budget. Fixed paid mixed battery reinvests earned ink every 60 ticks; erased repair posts are not replaced. Each phase waits ten seconds; upgrades DeepInk except wave4 RapidFire. No well activation, free healing or generator replacement. Campaign Leaks is inapplicable: enemies repeatedly attack the castle. NetResource is actual generator income minus generator purchases; RemainingInk includes all production rewards and ordinary erasure refunds. Placements and Cost retain all paid purchases; RefundedInk records refunds separately. Same seed repeats a treatment; spawn admission may change the realized mixture across treatments."
            };
        }

        public static List<AssemblyBalanceRow> RunCampaignMatrix(GameBalance balance, ComponentCatalog catalog)
        {
            var rows = new List<AssemblyBalanceRow>();
            foreach (int seed in Seeds) foreach (int generators in new[] { 0, 1, 2 }) foreach (bool repair in new[] { false, true })
                rows.Add(RunCampaign(balance, catalog, seed, generators, repair));
            return rows;
        }

        /// <summary>Only equal-condition combat lanes are comparable; support and cleaner roles have independent usefulness tests.</summary>
        public static string[] FindUniversalDominance(IReadOnlyList<AssemblyBalanceRow> rows)
        {
            var groups = rows.Where(r => r.Fixture == "FiringLane" && r.Role == "Tower" && r.DamageApplicable && r.LeaksApplicable)
                .GroupBy(r => r.RecipeId).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            var result = new List<string>();
            string Key(AssemblyBalanceRow r) => r.Scenario + "/" + r.Seed + "/" + r.Budget;
            foreach (var first in groups) foreach (var second in groups)
            {
                if (first.Key == second.Key) continue;
                var reference = second.Value.ToDictionary(Key, StringComparer.Ordinal);
                bool dominates = first.Value.Length == second.Value.Length && first.Value.Length > 0;
                foreach (var a in first.Value)
                {
                    if (!reference.TryGetValue(Key(a), out var b)) { dominates = false; break; }
                    bool noWorse = a.Damage >= b.Damage && a.Leaks <= b.Leaks && a.UnresolvedEnemies <= b.UnresolvedEnemies &&
                        a.Cost <= b.Cost && a.Copies <= b.Copies && a.IdleSeconds <= b.IdleSeconds && a.BuildSeconds <= b.BuildSeconds &&
                        a.CleaningApplicable == b.CleaningApplicable && (!a.CleaningApplicable || a.BlotsCleared >= b.BlotsCleared && a.UnresolvedBlots <= b.UnresolvedBlots);
                    bool better = a.Damage > b.Damage || a.Leaks < b.Leaks || a.UnresolvedEnemies < b.UnresolvedEnemies ||
                        a.Cost < b.Cost || a.Copies < b.Copies || a.IdleSeconds < b.IdleSeconds || a.BuildSeconds < b.BuildSeconds ||
                        a.CleaningApplicable && (a.BlotsCleared > b.BlotsCleared || a.UnresolvedBlots < b.UnresolvedBlots);
                    if (!noWorse || !better) { dominates = false; break; }
                }
                if (dominates) result.Add(first.Key + " > " + second.Key);
            }
            return result.ToArray();
        }

        public static AssemblyBalanceEvidence CreateEvidence(GameBalance balance, ComponentCatalog catalog)
        {
            var rows = RunMatrix(balance, catalog); rows.AddRange(RunCampaignMatrix(balance, catalog));
            return new AssemblyBalanceEvidence {
                Revision = catalog.BalanceRevision, Rows = rows.ToArray(), CampaignWaves = balance.Waves.ToArray(),
                UniversalDominance = FindUniversalDominance(rows),
                Scope = "Measured production combat and support, budgets200/400/800, seeds1337/2026/4242, Fast/Crowd/Armored plus Blots and full eight-wave campaign. No player profile access. Applicability flags define missing role metrics. Damage is actual capped HP loss; no balance conclusion is inferred from theoretical DPS. NetResource is income minus purchases (campaign: generator purchases); RemainingInk records retained stock. Dominance compares damage, leaks, unresolved enemies, spending, object count, idle time and construction time for matching combat lanes."
            };
        }

        public static string Export(GameBalance balance, ComponentCatalog catalog, string outputPath)
        {
            var evidence = CreateEvidence(balance, catalog);
            string path = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(evidence, true) + Environment.NewLine);
            return path;
        }

        public static string Export()
        {
            LoadRuntime(out var balance, out var catalog);
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Export(balance, catalog, Path.Combine(root, ResultsPath));
        }
    }
}
