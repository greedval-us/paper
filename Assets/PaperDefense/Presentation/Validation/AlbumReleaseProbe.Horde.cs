using System;
using System.Collections;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public sealed partial class AlbumReleaseProbe
    {
        const string HordeFlag = "-paper-release-horde";
        const int VisibleThreshold = 200;

        [Serializable] public sealed class HordeReport : RunReport
        {
            public HordeMeasurement horde;
        }

        [Serializable] public sealed class HordeMeasurement
        {
            public string mission, phaseAfterWave;
            public int planned, spawned, peakAlive, peakVisible, kills, spentInk, mortarImpacts, hitsOnWalls, destroyedWalls;
            public float healthAfterWave, firstFullyAdmittedSeconds = -1, longestDenseSeconds;
            public int massShotVisible, impactShotVisible, breachShotVisible;
            public float massShotSeconds, impactShotSeconds, breachShotSeconds;
            public Metric denseFrameInterval, denseMeshBuild, bombardmentFrameInterval;
        }

        IEnumerable MeasureOpeningHorde()
        {
            OpenBattleFromHome();
            var run = game.Run;
            var session = run.Session;
            var m = ((HordeReport)report).horde = new HordeMeasurement {
                mission = run.ActiveMission?.Id, planned = session.Balance.Waves[0].Count
            };
            Require(m.mission == "notebook-1-page-1" && m.planned == 360, "Expected the production 360-enemy opening wave.");
            var home = session.BaseCell;
            int startingInk = session.Ink;
            void Buy(string id, int dx, int dy)
            {
                var entries = run.RunLoadout.Entries;
                int slot = 0;
                while (slot < entries.Count && entries[slot]?.Id != id) slot++;
                var cell = new Cell(home.X + dx, home.Y + dy);
                Require(slot < entries.Count, "Missing starter page " + id);
                bool built = run.BuildEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None, out string reason,
                    facingDegrees: 180);
                Require(built, id + " at " + cell + ": " + GameText.Resolve(reason));
                actions.Add("Built " + id + " at " + cell);
            }
            Buy("album.mortar", 2, 0);
            Buy("album.guard", 2, -2);
            for (int dy = -2; dy <= 2; dy++) Buy("album.wall", -2, dy);
            m.spentInk = startingInk - session.Ink;
            Require(m.spentInk > 0, "Placement must debit the actual opening budget.");

            // The production camera is orthographic and aligned to the map axes.
            var camera = game.World.WorldCamera;
            var shell = game.GetComponentInChildren<PlayerShell>();
            var endOfFrame = new WaitForEndOfFrame();
            void CaptureCurrentTick(string name, long tick)
            {
                shell.RefreshBattleHud();
                Screenshot(name);
                Require(tick == session.TickIndex, "Capture no longer corresponds to the measured simulation tick.");
            }
            var frameTimes = new List<double>();
            var buildTimes = new List<double>();
            var combatFrames = new List<double>();
            float lastImpactTime = float.NegativeInfinity;
            void Observe(SimulationEvent action)
            {
                if (action.Kind == SimulationEventKind.ProjectileImpact && action.Weapon == StructureKind.Mortar)
                {
                    m.mortarImpacts++;
                    lastImpactTime = (float)action.SimulationSeconds;
                }
                if (action.Weapon != StructureKind.Wall) return;
                if (action.Kind == SimulationEventKind.StructureHit) m.hitsOnWalls++;
                if (action.Kind == SimulationEventKind.StructureDestroyed) m.destroyedWalls++;
            }

            run.SimulationEventReceived += Observe;
            try
            {
                Click("Launch wave");
                Require(session.Phase == RunPhase.Assault, "The campaign wave did not start.");
                float start = session.Elapsed, denseSince = -1;
                double lastFrame = Time.realtimeSinceStartupAsDouble;
                int omitThrough = Time.frameCount;
                bool massCaptured = false, impactCaptured = false, detailCaptured = false, focused = false;
                int focusFrame = -1;
                while (session.Phase == RunPhase.Assault && session.WaveIndex == 1)
                {
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    double interval = (now - lastFrame) * 1000;
                    lastFrame = now;
                    float age = session.Elapsed - start;
                    int visible = CountVisibleEnemyCenters(session, camera);
                    m.spawned = session.Statistics.TotalSpawnedEnemies;
                    m.peakAlive = Math.Max(m.peakAlive, session.Enemies.Count);
                    if (!focused) m.peakVisible = Math.Max(m.peakVisible, visible);
                    if (m.spawned == m.planned && m.firstFullyAdmittedSeconds < 0) m.firstFullyAdmittedSeconds = age;
                    if (!focused && visible >= VisibleThreshold)
                    {
                        if (denseSince < 0) denseSince = age;
                        m.longestDenseSeconds = Math.Max(m.longestDenseSeconds, age - denseSince);
                        if (Time.frameCount > omitThrough && interval > 0)
                        {
                            frameTimes.Add(interval);
                            buildTimes.Add(game.World.LastMeshBuildMilliseconds);
                        }
                    }
                    else denseSince = -1;
                    if (!focused && session.Elapsed - lastImpactTime <= 1 && Time.frameCount > omitThrough && interval > 0)
                        combatFrames.Add(interval);

                    // Wait for LateUpdate to build this tick's world mesh before rendering evidence.
                    long measuredTick = session.TickIndex;
                    bool tookPicture = false;
                    if (!massCaptured && m.spawned == m.planned)
                    {
                        yield return endOfFrame;
                        CaptureCurrentTick("horde-mass", measuredTick);
                        m.massShotVisible = visible; m.massShotSeconds = age; massCaptured = tookPicture = true;
                    }
                    if (!impactCaptured && m.mortarImpacts > 0)
                    {
                        if (!tookPicture) yield return endOfFrame;
                        CaptureCurrentTick("horde-impact", measuredTick);
                        m.impactShotVisible = visible; m.impactShotSeconds = age; impactCaptured = tookPicture = true;
                    }
                    if (!focused && impactCaptured && m.longestDenseSeconds >= 15 && visible < VisibleThreshold &&
                        session.Phase == RunPhase.Assault)
                    {
                        game.Drawing.SetBattleView(false);
                        game.Drawing.Focus(new Cell(home.X - 2, home.Y));
                        focused = true; focusFrame = Time.frameCount;
                    }
                    if (focused && !detailCaptured && m.destroyedWalls > 0 && Time.frameCount >= focusFrame + 2 && session.Phase == RunPhase.Assault)
                    {
                        if (!tookPicture) yield return endOfFrame;
                        CaptureCurrentTick("horde-wall", measuredTick);
                        m.breachShotVisible = visible; m.breachShotSeconds = age; detailCaptured = tookPicture = true;
                    }
                    if (tookPicture) omitThrough = Time.frameCount + 3;
                }
                m.denseFrameInterval = Summarize(frameTimes, "Rendered frame interval during 200+ visible at opening zoom; capture frames excluded", "ms");
                m.denseMeshBuild = Summarize(buildTimes, "NotebookWorldView world mesh build during 200+ visible; capture frames excluded", "ms");
                m.bombardmentFrameInterval = Summarize(combatFrames, "Rendered frame interval within one simulated second of mortar impact at opening zoom; capture frames excluded", "ms");
                m.kills = session.Kills; m.healthAfterWave = session.BaseHealth; m.phaseAfterWave = session.Phase.ToString();
                m.peakAlive = Math.Max(m.peakAlive, session.Statistics.PeakAliveEnemies);
                Require(m.spawned == 360 && m.peakAlive >= 250, "The chosen paid defense did not encounter a 250+ living horde.");
                Require(m.firstFullyAdmittedSeconds >= 0 && m.firstFullyAdmittedSeconds <= 15f + FixedStepClock.Step,
                    "The full wave must enter within the first 15 seconds (one simulation step tolerance).");
                Require(m.peakVisible >= VisibleThreshold && m.longestDenseSeconds >= 15,
                    "The actual opening camera did not show 200 living enemies continuously for 15 simulated seconds.");
                Require(massCaptured && impactCaptured && m.mortarImpacts > 0, "Missing horde or artillery evidence.");
                Require(detailCaptured && m.hitsOnWalls > 0 && m.destroyedWalls > 0,
                    "Missing enemy damage to a paid wall or a screenshot of the breach.");
                Require(session.Phase == RunPhase.Intermission && session.BaseHealth > 0, "Paid defense failed the first wave.");
                Require(frameTimes.Count >= 30 && combatFrames.Count >= 15, "Insufficient standalone frame samples.");
                report.performanceComplete = true;
            }
            finally { run.SimulationEventReceived -= Observe; }
        }

        static int CountVisibleEnemyCenters(IRunReadModel session, Camera camera)
        {
            float depth = -camera.transform.position.z;
            var low = camera.ViewportToWorldPoint(new Vector3(0, 0, depth));
            var high = camera.ViewportToWorldPoint(new Vector3(1, 1, depth));
            int visible = 0;
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                var enemy = session.Enemies[i];
                if (enemy.Health > 0 && enemy.Position.X >= low.x && enemy.Position.X <= high.x &&
                    enemy.Position.Y >= low.y && enemy.Position.Y <= high.y) visible++;
            }
            return visible;
        }
    }
}
