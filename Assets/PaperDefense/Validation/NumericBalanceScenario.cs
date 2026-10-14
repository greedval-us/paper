using System;
using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Validation
{
    /// <summary>Equal-budget role comparisons. Uses production combat; never modifies the supplied rules or a profile.</summary>
    public static class NumericBalanceScenario
    {
        public static TowerBlueprint Build(GameBalance balance, string id)
        {
            string chassis = "chassis.standard";
            string[] weapons = { id };
            string[] modules = Array.Empty<string>();
            switch (id)
            {
                case "double-gun": weapons = new[] { "gun", "gun" }; break;
                case "gun-stapler": weapons = new[] { "gun", "stapler" }; break;
                case "mortar-stapler": weapons = new[] { "mortar", "stapler" }; break;
                case "gun-blotter": weapons = new[] { "gun", "blotter" }; break;
                case "gun-feed": weapons = new[] { "gun" }; modules = new[] { "feed" }; break;
                case "gun-tracking": weapons = new[] { "gun" }; modules = new[] { "tracking" }; break;
                case "gun-armor": weapons = new[] { "gun" }; modules = new[] { "armor" }; break;
                case "piercer-rail": weapons = new[] { "piercer" }; modules = new[] { "rail" }; break;
                case "mortar-feed": weapons = new[] { "mortar" }; modules = new[] { "feed" }; break;
                case "precision": chassis = "chassis.precision"; weapons = new[] { "gun" }; modules = new[] { "feed", "tracking", "rail" }; break;
                case "battery": chassis = "chassis.battery"; weapons = new[] { "gun", "gun", "gun" }; break;
                case "heavy": chassis = "chassis.heavy"; weapons = new[] { "gun", "piercer" }; modules = new[] { "armor" }; break;
            }
            var mounts = new List<WeaponMountSelection>();
            for (int i = 0; i < weapons.Length; i++) mounts.Add(new WeaponMountSelection(i, "weapon." + weapons[i]));
            var selections = new List<ModuleSelection>();
            foreach (var module in modules) selections.Add(new ModuleSelection(module, "module." + module));
            return new TowerBlueprint("balance." + id, id, chassis, mounts, selections, content: balance.Content);
        }

        // Campaign-like health/speed, plus an isolated target and a narrow approach. No automatic wave/tier HP scaling in TrialRunner.
        public static GameBalance Fixture(GameBalance source, string lane)
        {
            var b = source.CreateSnapshot();
            var f = b.Trials.For(TrialScenario.Crowd);
            f.Count = 120; f.Health = 60; f.Speed = 2.1f; f.SpawnInterval = .18f;
            f.EnemyKind = EnemyKind.Doodle; f.DrawingTier = DrawingTier.Sketch;
            if (lane == "fast") { f.Count = 90; f.Health = 42; f.Speed = 3.6f; f.SpawnInterval = .24f; f.EnemyKind = EnemyKind.Runner; }
            if (lane == "armored") { f.Count = 48; f.Health = 260; f.Speed = 1.6f; f.SpawnInterval = .5f; f.DrawingTier = DrawingTier.Hatched; f.EnemyKind = EnemyKind.Bulwark; }
            if (lane == "isolated") { f.Count = 12; f.Health = 180; f.Speed = 1.25f; f.SpawnInterval = 3; f.EnemyKind = EnemyKind.Gnawer; }
            if (lane == "line") b.Trials.SpawnYVariation = .5f;
            if (lane == "grazing")
            {
                f.Count = 24; f.Health = 24; f.Speed = 5; f.SpawnInterval = .8f;
                b.Trials.SpawnY = 18.35f; b.Trials.SpawnYVariation = 0;
            }
            if (lane == "standoff" || lane == "edge")
            {
                b.Trials.SpawnY = lane == "standoff" ? 24.5f : 20;
                b.Trials.SpawnYVariation = 0; b.Trials.CloseTowerColumns = 64;
            }
            return b;
        }

        public static NumericBalanceRow Measure(GameBalance balance, string build, string lane, int budget, int seed, InkColor color = InkColor.None, bool counterfire = false)
        {
            var b = Fixture(balance, lane);
            var bp = Build(b, build);
            var r = TrialRunner.Run(bp, b, lane == "blots" ? TrialScenario.Blots : TrialScenario.Crowd, budget, seed, counterfire, color);
            return new NumericBalanceRow { Build = build, Lane = lane, Budget = budget, Seed = seed, Color = color.ToString(), Counterfire = counterfire,
                Cost = r.Cost, Copies = r.Copies, Leaks = r.Leaks, Unresolved = r.UnresolvedEnemies, Damage = r.Damage,
                BlotsCleared = r.BlotsCleared, Survivors = r.SurvivingTowers };
        }

        public static List<NumericBalanceRow> Run(GameBalance balance, int? onlySeed = null)
        {
            var rows = new List<NumericBalanceRow>();
            foreach (var build in new[] { "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "double-gun", "gun-stapler", "mortar-stapler", "gun-feed", "gun-tracking", "gun-armor", "piercer-rail", "mortar-feed", "precision", "battery", "heavy" })
                foreach (var lane in new[] { "crowd", "fast", "armored", "isolated", "line" })
                    foreach (int budget in new[] { 200, 400, 800 })
                        foreach (int seed in new[] { 17, 41, 1337 })
                            if (!onlySeed.HasValue || onlySeed == seed) rows.Add(Measure(balance, build, lane, budget, seed));
            if (onlySeed.HasValue && onlySeed != 1337) return rows;
            foreach (var build in new[] { "gun", "mortar", "ricochet" })
                foreach (InkColor color in Enum.GetValues(typeof(InkColor)))
                    foreach (var lane in new[] { "crowd", "fast", "armored" })
                        rows.Add(Measure(balance, build, lane, 400, 1337, color));
            foreach (var build in new[] { "gun", "gun-armor", "heavy" })
                rows.Add(Measure(balance, build, "isolated", 400, 1337, counterfire: true));
            foreach (var build in new[] { "blotter", "gun-blotter" })
                rows.Add(Measure(balance, build, "blots", 400, 1337));
            return rows;
        }

        /// <summary>Fixed placement constraints isolate acquisition and range benefits; not general DPS rankings.</summary>
        public static List<NumericBalanceRow> RunSituational(GameBalance balance)
        {
            var rows = new List<NumericBalanceRow>();
            foreach (int seed in new[] { 17, 41, 1337 })
            {
                foreach (string build in new[] { "gun", "gun-tracking" }) rows.Add(Measure(balance, build, "grazing", 55, seed));
                foreach (string build in new[] { "piercer", "piercer-rail" }) rows.Add(Measure(balance, build, "standoff", 400, seed));
                foreach (string build in new[] { "gun", "precision" }) rows.Add(Measure(balance, build, "edge", 400, seed));
            }
            return rows;
        }
    }

    [Serializable]
    public sealed class NumericBalanceRow
    {
        public string Build, Lane, Color;
        public int Budget, Seed, Cost, Copies, Leaks, Unresolved, BlotsCleared, Survivors;
        public bool Counterfire;
        public float Damage;
    }
}
