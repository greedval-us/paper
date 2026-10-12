using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Validation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Independent before/after evidence. Never reads the player's profile; no production rules live here.</summary>
    public static class RefactorValidation
    {
        public static string RulesJson()
        {
            var result = new RulesEvidence();
            foreach (string name in new[] { "NotebookBalance", "TargetNotebookBalance" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/" + name + ".asset");
                var balance = asset.CreateWorkshopBalance();
                var entry = new RulesEntry { asset = name, effectiveSettings = Canonical(balance) };
                foreach (var first in balance.StructureDefinitions)
                {
                    if (!TowerCatalog.IsMountPart(first.Kind)) continue;
                    foreach (var tier in new[] { MaterialTier.Pencil, MaterialTier.Ink, MaterialTier.Marker })
                        foreach (var modules in new[] { TowerModules.None, TowerModules.Armor, TowerModules.Feed | TowerModules.Tracking, TowerModules.Rail })
                        {
                            var blueprint = new TowerBlueprint("fixture", "fixture", ChassisKind.Heavy, first.Kind, StructureKind.Gun, modules);
                            entry.assemblies.Add(new AssemblyEntry
                            {
                                weapon = first.Kind.ToString(), material = tier.ToString(), modules = (int)modules,
                                cost = TowerCatalog.Cost(blueprint, balance, tier), load = TowerCatalog.Load(blueprint, balance),
                                capacity = TowerCatalog.Capacity(blueprint, balance), color = TowerCatalog.ColorCost(blueprint, balance),
                                health = TowerCatalog.Health(blueprint, balance, tier), defense = TowerCatalog.Defense(blueprint, balance),
                                build = TowerCatalog.BuildSeconds(blueprint, balance, tier), range = TowerCatalog.WeaponRange(blueprint, balance, first.Kind),
                                interval = TowerCatalog.ActionInterval(blueprint, balance, first.Kind, TowerCatalog.FeedMultiplier(blueprint, balance), 1)
                            });
                        }
                }
                result.presets.Add(entry);
            }
            return JsonUtility.ToJson(result, true);
        }

        public static string TrialsJson(bool legacyFixture = false)
        {
            var result = new List<string>();
            var balance = legacyFixture ? GameBalancePresets.CreateTargetWorkshop() :
                AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/TargetNotebookBalance.asset").CreateWorkshopBalance();
            foreach (var kind in new[] { StructureKind.Gun, StructureKind.Piercer, StructureKind.Mortar, StructureKind.Fan, StructureKind.Ricochet, StructureKind.Stapler, StructureKind.Blotter })
                foreach (TrialScenario scenario in Enum.GetValues(typeof(TrialScenario)))
                {
                    var blueprint = new TowerBlueprint("trial-contract", "trial-contract", ChassisKind.Standard, kind);
                    var report = TrialRunner.Run(blueprint, balance, scenario, 200, 1337, true, InkColor.Blue);
                    var hash = new StableHash(); hash.Value(report);
                    result.Add(kind + "/" + scenario + ":" + hash.Hex);
                }
            return string.Join("\n", result);
        }

        // Keep expensive evidence off the editor tool's request timeout. Capture Unity assets on its thread first.
        public static void StartReplay(bool target, string outputPath, bool legacyFixture = false, bool campaignReplay = false)
        {
            bool campaignMode = target || campaignReplay;
            var pressure = campaignMode ? null : new PressureScenario(null, legacyFixture ? null : AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/NotebookBalance.asset").CreateWorkshopBalance());
            var campaign = campaignMode ? new InkLoopScenario(AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/" + (target ? "TargetNotebookBalance" : "NotebookBalance") + ".asset").CreateWorkshopBalance()) : null;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var trace = new TraceEvidence { scenario = target ? "target campaign seed1337" : campaignMode ? "historical campaign seed1337" : "historical pressure seed1337" };
                    var hash = new StableHash(); long lastEvent = 0;
                    if (campaignMode)
                    {
                        while (!campaign.Finished) { campaign.Step(); Observe(campaign.Session, hash, trace, ref lastEvent); }
                        trace.finalReport = JsonUtility.ToJson(campaign.Report);
                    }
                    else
                    {
                        while (!pressure.Finished) { pressure.Step(); Observe(pressure.Session, hash, trace, ref lastEvent); }
                        trace.finalReport = JsonUtility.ToJson(pressure.Report);
                    }
                    trace.digest = hash.Hex;
                    File.WriteAllText(outputPath, JsonUtility.ToJson(trace, true));
                }
                catch (Exception error) { File.WriteAllText(outputPath + ".error", error.ToString()); }
            });
        }

        static void Observe(GameSession game, StableHash hash, TraceEvidence trace, ref long lastEvent)
        {
            hash.Value(game.TickIndex); hash.Value(game.Phase); hash.Value(game.Ink); hash.Value(game.GoldEarned);
            hash.Value(game.BaseHealth); hash.Value(game.Countdown); hash.Value(game.Elapsed);
            hash.Value(game.Kills); hash.Value(game.RemainingToSpawn); hash.Value(game.PendingUpgrade);
            hash.Value(game.Structures); hash.Value(game.Enemies); hash.Value(game.Projectiles); hash.Value(game.Shots);
            var read = new RunReadModel(game);
            hash.Value(read.Blots); hash.Value(read.EnemyProjectiles); hash.Value(read.Wells);
            foreach (InkColor color in Enum.GetValues(typeof(InkColor))) hash.Value(game.ColoredInk(color));
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
            { hash.Value(game.Statistics.Launches(kind)); hash.Value(game.Statistics.Damage(kind)); hash.Value(game.Statistics.IdleSeconds(kind)); }
            foreach (var item in game.EventBuffer)
                if (item.Sequence > lastEvent) { hash.Value(item); lastEvent = item.Sequence; trace.events++; }
            trace.ticks++;
            if (game.TickIndex % 300 == 0) trace.checkpoints.Add(game.TickIndex + ":" + hash.Hex);
        }

        public static string PerformanceJson()
        {
            // Measurements exclude replay hashing and report generation. Identical fixtures before/after.
            var result = new PerformanceEvidence { unityVersion = UnityEngine.Application.unityVersion, processor = SystemInfo.processorType };
            long allocationProbe = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(new byte[65536]);
            result.allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() > allocationProbe;
            Measure(result, "pressure combat3000", () => { var r = new PressureScenario(); while (!r.Finished) r.Step(); }, 3);
            var points = new DrawingPoint[2048];
            for (int i = 0; i < points.Length; i++) points[i] = new DrawingPoint(-.45f + i / 2048f * .9f, (float)Math.Sin(i * .13) * .3f);
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, new[] { new DrawingStroke(points) }) });
            var blueprint = new TowerBlueprint("perf", "perf", ChassisKind.Standard, StructureKind.Gun, drawing: drawing, origin: DrawingOrigin.PlayerDrawing);
            Measure(result, "editor undo/redo2048points", () => { var e = new DrawingEditSession(blueprint); e.ClearPart(DrawingPartKey.Base); e.Undo(); e.Redo(); }, 30);
            Measure(result, "assessment256uncachedpages", () =>
            {
                for (int i = 0; i < 256; i++)
                {
                    var p = new[] { new DrawingPoint(-.4f, i / 4096f), new DrawingPoint(.4f, .2f) };
                    var d = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, new[] { new DrawingStroke(p) }) });
                    new TowerBlueprint("assessment", "assessment", ChassisKind.Standard, StructureKind.Gun, drawing: d, origin: DrawingOrigin.PlayerDrawing);
                }
            }, 1);
            var profile = new WorkshopProfile(); string reason;
            for (int i = 0; i < 60; i++)
                if (!profile.SaveBlueprint(blueprint.AsNew("perf-" + i, "perf"), out reason)) throw new InvalidOperationException(reason);
            string folder = Path.Combine("Temp", "PaperDefenseRefactor", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            var store = new WorkshopProfileStore(Path.GetFullPath(Path.Combine(folder, "isolated-profile.json")));
            Measure(result, "fullalbum64pages save+load", () =>
            {
                var save = store.Save(profile); var loaded = store.Load();
                if (!save.Success || loaded.Profile.Album.Count != 64) throw new InvalidOperationException("Isolated full-album IO failed.");
            }, 3);
            return JsonUtility.ToJson(result, true);
        }

        static void Measure(PerformanceEvidence result, string name, Action action, int count)
        {
            var sample = new Measurement { name = name };
            for (int i = 0; i < count; i++)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread(); var clock = Stopwatch.StartNew(); action(); clock.Stop();
                sample.milliseconds.Add(clock.Elapsed.TotalMilliseconds); sample.allocatedBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocated);
            }
            result.measurements.Add(sample);
        }

        static string Canonical(object value)
        {
            if (value == null) return "null";
            if (value is string text) return text;
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) return Convert.ToString(value, CultureInfo.InvariantCulture);
            var result = new StringBuilder();
            if (value is IEnumerable list) { foreach (var item in list) result.Append('[').Append(Canonical(item)).Append(']'); }
            else foreach (var field in Fields(type)) result.Append(field.Name).Append('=').Append(Canonical(field.GetValue(value))).Append(';');
            return result.ToString();
        }

        static readonly Dictionary<Type, FieldInfo[]> fields = new Dictionary<Type, FieldInfo[]>();
        static FieldInfo[] Fields(Type type)
        {
            lock (fields)
            {
                if (fields.TryGetValue(type, out var found)) return found;
                found = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                // Freeze the pre-refactor observable schema (source archived under docs/validation/content/before).
                // These newly added fields only reference content/IDs; all old fields, including private effects, remain hashed.
                found = Array.FindAll(found, field => !AddedMetadata(type, field.Name));
                Array.Sort(found, (a, b) => string.CompareOrdinal(a.Name, b.Name)); fields.Add(type, found); return found;
            }
        }

        static bool AddedMetadata(Type type, string name)
        {
            if (type==typeof(GameBalance)) return name=="Content" || name=="Combat" || name=="Trials" || name=="WellPlacements";
            if (type==typeof(StructureState)) return name=="Assembly" || name=="ContentId";
            if (type==typeof(StructureView)) return name=="ContentId";
            if (type==typeof(ProjectileView)) return name=="WeaponId";
            if (type==typeof(EnemyState)) return name=="RedSourceWeaponId";
            if (type==typeof(ShotEvent) || type==typeof(SimulationEvent)) return name=="WeaponId";
            if (type==typeof(MortarProjectile)) return name=="Definition" || name=="<WeaponId>k__BackingField";
            return type.FullName=="PaperDefense.Core.TimedEffectState+Entry" && name=="WeaponId";
        }

        sealed class StableHash
        {
            ulong hash = 14695981039346656037UL;
            public string Hex => hash.ToString("x16", CultureInfo.InvariantCulture);
            void Number(ulong number) { unchecked { for (int i = 0; i < 8; i++) { hash ^= number & 255; hash *= 1099511628211UL; number >>= 8; } } }
            public void Value(object value)
            {
                if (value == null) { Number(ulong.MaxValue); return; }
                if (value is TowerBlueprint b) { Value(b.Id); Value(b.Chassis); Value(b.FirstWeapon); Value(b.SecondWeapon); Value(b.Modules); Value(b.Drawing.GeometryHash); Value(b.Assessment.Quality); return; }
                if (value is string text) { Number((ulong)text.Length); foreach (char c in text) Number(c); return; }
                if (value is float f) { Number(unchecked((ulong)BitConverter.SingleToInt32Bits(f))); return; }
                if (value is double d) { Number(unchecked((ulong)BitConverter.DoubleToInt64Bits(d))); return; }
                Type type = value.GetType();
                if (type.IsPrimitive || type.IsEnum) { Number(unchecked((ulong)Convert.ToInt64(value))); return; }
                if (value is IEnumerable list) { int count = 0; foreach (var item in list) { Value(item); count++; } Number((ulong)count); return; }
                foreach (var field in Fields(type)) Value(field.GetValue(value));
            }
        }

        [Serializable] sealed class RulesEvidence { public List<RulesEntry> presets = new List<RulesEntry>(); }
        [Serializable] sealed class RulesEntry { public string asset, effectiveSettings; public List<AssemblyEntry> assemblies = new List<AssemblyEntry>(); }
        [Serializable] sealed class AssemblyEntry { public string weapon, material; public int modules, cost, load, capacity, color; public float health, defense, build, range, interval; }
        [Serializable] sealed class TraceEvidence { public string scenario, digest, finalReport; public int ticks, events; public List<string> checkpoints = new List<string>(); }
        [Serializable] sealed class PerformanceEvidence { public string unityVersion, processor; public bool allocationCounterAvailable; public List<Measurement> measurements = new List<Measurement>(); }
        [Serializable] sealed class Measurement { public string name; public List<double> milliseconds = new List<double>(); public List<long> allocatedBytes = new List<long>(); }
    }
}
