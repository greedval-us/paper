using System;
using System.Collections;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public sealed partial class AlbumReleaseProbe
    {
        const string SiegeFlag = "-paper-release-siege";

        [Serializable] public sealed class SiegeReport : RunReport
        {
            public SiegeWave[] waves;
            public string outcome;
        }

        [Serializable] public sealed class SiegeWave
        {
            public int wave, count, sides, startingInk, spentInk, spawned, killed, peakAlive, peakVisible, speed;
            public int west, east, north, south;
            public int engagementShotVisible;
            public float healthAfter, firstFullyAdmittedSeconds = -1;
            public Metric denseFrameInterval;
        }

        IEnumerable MeasureCampaignHordes()
        {
            OpenBattleFromHome();
            var run = game.Run;
            var session = run.Session;
            var camera = game.World.WorldCamera;
            var shell = game.GetComponentInChildren<PlayerShell>();
            var siege = (SiegeReport)report;
            var endOfFrame = new WaitForEndOfFrame();
            int[] directions = { 1, 2, 12, 15 };
            Require(run.ActiveMission?.Id == "notebook-1-page-1" && session.Balance.Waves.Count == directions.Length,
                "Expected the four production waves of the opening mission.");
            siege.waves = new SiegeWave[directions.Length];
            int lastEnemyId = 0;
            for (int index = 0; index < directions.Length; index++)
            {
                var configured = session.Balance.Waves[index];
                var w = siege.waves[index] = new SiegeWave {
                    wave = index + 1, count = configured.Count, sides = configured.Sides, startingInk = session.Ink
                };
                Require(configured.IsHorde && w.sides == directions[index] && (index == 0 || w.count > siege.waves[index - 1].count),
                    "Expected increasingly large hordes on alternating fronts.");
                if (session.PendingInkFlasks > 0)
                {
                    var color = index == 1 ? InkColor.Red : index == 2 ? InkColor.Blue : InkColor.Green;
                    Require(run.ClaimInkFlask(session.RunId, session.NextInkFlaskId, color, out string reason), GameText.Resolve(reason));
                    actions.Add("Claimed earned " + color + " ink flask before wave " + w.wave);
                }
                PlaceSiegeDefense(w.wave);
                w.spentInk = w.startingInk - session.Ink;
                Require(w.spentInk > 0, "Each intermission should fund a new response to the next front.");
                if (index == 0 || index == 3) run.ToggleSpeed();
                w.speed = run.Speed;
                shell.RefreshBattleHud();
                if (index == 2)
                {
                    // Let the normal intermission UI select the next fronts before LateUpdate positions the camera.
                    yield return null;
                    yield return endOfFrame;
                    shell.RefreshBattleHud();
                    Screenshot("north-south-forecast");
                }
                Click("Launch wave");
                Require(session.Phase == RunPhase.Assault && session.WaveIndex == w.wave, "The next wave did not start.");
                int startingKills = session.Kills;
                int priorSpawns = session.Statistics.TotalSpawnedEnemies;
                float start = session.Elapsed;
                double lastFrame = Time.realtimeSinceStartupAsDouble;
                var denseFrames = new List<double>();
                int omitThrough = Time.frameCount + 2;
                bool captured = false;
                while (session.Phase == RunPhase.Assault && session.WaveIndex == w.wave)
                {
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    double frameMs = (now - lastFrame) * 1000;
                    lastFrame = now;
                    ObserveEntranceCounts(session, w, ref lastEnemyId);
                    int visible = CountVisibleEnemyCenters(session, camera);
                    w.peakAlive = Math.Max(w.peakAlive, session.Enemies.Count);
                    w.peakVisible = Math.Max(w.peakVisible, visible);
                    w.spawned = session.Statistics.TotalSpawnedEnemies - priorSpawns;
                    if (w.spawned == w.count && w.firstFullyAdmittedSeconds < 0)
                        w.firstFullyAdmittedSeconds = session.Elapsed - start;
                    if (visible >= VisibleThreshold && frameMs > 0 && Time.frameCount > omitThrough)
                        denseFrames.Add(frameMs);
                    if (index == 3 && !captured && w.spawned == w.count)
                    {
                        yield return endOfFrame;
                        shell.RefreshBattleHud();
                        Screenshot("four-fronts");
                        omitThrough = Time.frameCount + 3;
                        captured = true;
                    }
                    if (captured && w.engagementShotVisible == 0 && visible > 0 && visible < VisibleThreshold &&
                        session.Phase == RunPhase.Assault)
                    {
                        // The four-front overview and dense timing are complete. Use the ordinary castle focus
                        // and wait for the next actual shell to land inside the close-up.
                        game.Drawing.SetBattleView(false);
                        bool impact = false;
                        void CheckImpact(SimulationEvent item)
                        {
                            if (item.Kind != SimulationEventKind.ProjectileImpact || item.Weapon != StructureKind.Mortar) return;
                            var point = camera.WorldToViewportPoint(new Vector3(item.To.X, item.To.Y, 0));
                            if (point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1) impact = true;
                        }
                        run.SimulationEventReceived += CheckImpact;
                        try
                        {
                            while (!impact && session.Phase == RunPhase.Assault) yield return null;
                            if (impact && session.Phase == RunPhase.Assault)
                            {
                                yield return endOfFrame;
                                shell.RefreshBattleHud();
                                Screenshot("castle-engagement");
                                w.engagementShotVisible = CountVisibleEnemyCenters(session, camera);
                            }
                        }
                        finally { run.SimulationEventReceived -= CheckImpact; }
                        // This close-up uses a different zoom and must not enter the overview measurements.
                        while (session.Phase == RunPhase.Assault) yield return null;
                    }
                }
                w.killed = session.Kills - startingKills;
                w.healthAfter = session.BaseHealth;
                w.denseFrameInterval = Summarize(denseFrames, "Rendered frame interval with 200+ visible, capture excluded", "ms");
                int observed = (w.west > 0 ? 1 : 0) | (w.east > 0 ? 2 : 0) | (w.north > 0 ? 4 : 0) | (w.south > 0 ? 8 : 0);
                Require(w.spawned == w.count && w.killed == w.count && w.east + w.west + w.north + w.south == w.count &&
                    observed == w.sides, "Unexpected admitted bodies, kills or physical entry fronts in wave " + w.wave + ".");
                Require(w.healthAfter > 0 && w.peakVisible >= VisibleThreshold, "Paid defenses failed to handle a visible horde.");
                if (index == 3) Require(w.peakVisible >= 600 && captured && w.speed == 1, "Missing a 600+ visible final siege at normal speed.");
            }
            siege.outcome = session.Phase.ToString();
            Require(session.Phase == RunPhase.Victory && session.CompletedWaves == siege.waves.Length, "The paid defense must win the mission.");
            report.performanceComplete = true;
        }

        // Surviving bodies retain admission order in the read model. A newly observed ID
        // is still at its physical map edge within one rendered frame of the fixed-step run.
        static void ObserveEntranceCounts(IRunReadModel session, SiegeWave totals, ref int lastId)
        {
            int newest = lastId;
            for (int i = session.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = session.Enemies[i];
                if (enemy.Id <= lastId) break;
                newest = Math.Max(newest, enemy.Id);
                var xy = enemy.Position;
                if (xy.X < 2) totals.west++;
                else if (xy.X > session.Balance.Width - 2) totals.east++;
                else if (xy.Y > session.Balance.Height - 2) totals.north++;
                else if (xy.Y < 2) totals.south++;
                else throw new InvalidOperationException("New enemy first observed away from every gate.");
            }
            lastId = newest;
        }

        void PlaceSiegeDefense(int wave)
        {
            var run = game.Run;
            var home = run.Session.BaseCell;
            void Buy(string id, int x, int y, float heading)
            {
                var cell = new Cell(home.X + x, home.Y + y);
                var entries = run.RunLoadout.Entries;
                int slot = 0;
                while (slot < entries.Count && entries[slot]?.Id != id) slot++;
                Require(slot < entries.Count, "Missing starter page " + id);
                bool ok = run.BuildEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None, out string reason, heading);
                Require(ok, id + " at " + cell + ": " + GameText.Resolve(reason));
                actions.Add("Wave " + wave + " built " + id + " at " + cell + " facing " + heading);
            }
            VisitSiegePlacements(wave, Buy);
        }

        internal static void VisitSiegePlacements(int wave, Action<string, int, int, float> buy)
        {
            switch (wave)
            {
                case 1:
                    buy("album.mortar", 2, 0, 180);
                    buy("album.guard", 2, -2, 180);
                    for (int y = -2; y <= 2; y++) buy("album.wall", -2, y, 180);
                    break;
                case 2:
                    buy("album.piercer", -2, 0, 0);
                    buy("album.guard", -2, 2, 0);
                    break;
                case 3:
                    buy("album.piercer", 0, -2, 90);
                    buy("album.piercer", 0, 2, 270);
                    break;
                case 4:
                    buy("album.mortar", -3, 2, 0);
                    buy("album.guard", 2, 2, 180);
                    break;
            }
        }
    }
}
