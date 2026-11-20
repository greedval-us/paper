using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public sealed partial class AlbumReleaseProbe
    {
        const string AtlasFlag = "-paper-release-atlas";

        [Serializable] public sealed class AtlasReport : RunReport
        {
            public AtlasPage[] pages;
        }

        [Serializable] public sealed class AtlasPage
        {
            public string mission, title, terrain;
            public int castleX, castleY, waveCount, planned, entranceSides, entranceBands, spawned, peakVisible;
            public int[] spawnedByKind, spawnedByDrawingTier;
            public Metric denseFrameInterval;
        }

        // The atlas needs access to every page without touching the player's saves.
        sealed class AtlasProfile : IWorkshopProfileStore
        {
            WorkshopProfile profile;
            public AtlasProfile(WorkshopProfile initial) { profile = initial; }
            public ProfileLoadResult Load() => new ProfileLoadResult(profile, ProfileLoadSource.Primary, "", true);
            public ProfileSaveResult Save(WorkshopProfile next)
            { profile = next; return new ProfileSaveResult(true, ""); }
        }

        IEnumerable CaptureCampaignMaps()
        {
            var run = game.Run;
            var missions = CampaignCatalog.Default.Missions;
            var balance = run.CreateMissionPreview(missions[0].Id).Balance;
            var profile = WorkshopProfile.CreateNew(run.AssemblyCatalog, balance.ResolveContent(), balance);
            for (int page = 0; page < missions.Count - 1; page++)
            {
                var prior = missions[page];
                Require(profile.TryApplyMissionReward(prior, Guid.NewGuid(), prior.WaveCount, true, 1, out var reason), reason);
            }
            run.InitializeWorkshop(new AtlasProfile(profile));
            var atlas = (AtlasReport)report;
            atlas.pages = new AtlasPage[missions.Count - 1];
            var camera = game.World.WorldCamera;
            var shell = game.GetComponentInChildren<PlayerShell>();
            var endOfFrame = new WaitForEndOfFrame();
            for (int page = 1; page < missions.Count; page++)
            {
                var mission = missions[page];
                game.OpenCampaign();
                Require(game.OpenMission(mission.Id), "Unable to open " + mission.Id);
                Click("Prepare mission"); Click("Start battle");
                var session = run.Session;
                Require(run.ActiveMission?.Id == mission.Id, "Wrong mission in battle.");
                var map = session.Battlefield;
                var wave = session.Balance.Waves[0];
                var row = atlas.pages[page - 1] = new AtlasPage {
                    mission = mission.Id, title = mission.Title, terrain = map.Style.ToString(),
                    castleX = session.BaseCell.X, castleY = session.BaseCell.Y,
                    waveCount = session.Balance.Waves.Count, planned = wave.Count,
                    entranceSides = wave.Sides, entranceBands = wave.EntranceBands
                };
                Require(session.Balance.Waves.All(w => w.IsHorde && w.Count >= 200), mission.Id + " still contains a small wave.");
                int gate = Enumerable.Range(0, map.Entrances.Count).First(i => map.IsWaveEntrance(i, wave.Sides, wave.EntranceBands));
                PlaceAtlasBattery(session.BaseCell, map.Entrances[gate]);
                game.Drawing.SetBattleView(true);
                Click("Launch wave");
                float start = session.Elapsed;
                double previous = Time.realtimeSinceStartupAsDouble;
                var intervals = new List<double>();
                while (session.Phase == RunPhase.Assault && session.Elapsed - start < 75)
                {
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    int visible = CountVisibleEnemyCenters(session, camera);
                    row.peakVisible = Math.Max(row.peakVisible, visible);
                    if (visible >= 200) intervals.Add((now - previous) * 1000);
                    previous = now;
                    row.spawned = session.Statistics.TotalSpawnedEnemies;
                    if (row.spawned == row.planned && intervals.Count >= 60) break;
                }
                Require(row.spawned == row.planned && row.peakVisible >= 200 && intervals.Count >= 60,
                    mission.Id + " did not admit and render its opening horde.");
                row.spawnedByKind = session.Statistics.SpawnedByKind.ToArray();
                row.spawnedByDrawingTier = session.Statistics.SpawnedByDrawingTier.ToArray();
                row.denseFrameInterval = Summarize(intervals, "Frame interval with 200+ visible in the opening assault at 1x", "ms");
                yield return endOfFrame;
                shell.RefreshBattleHud();
                Screenshot(mission.NotebookIndex + "-" + mission.PageIndex);
                yield return null;
            }
            atlas.performanceComplete = true;
        }

        void PlaceAtlasBattery(Cell home, Cell gate)
        {
            var run = game.Run;
            float heading = Mathf.Atan2(gate.Y - home.Y, gate.X - home.X) * Mathf.Rad2Deg;
            foreach (string id in new[] { "album.mortar", "album.guard" })
            {
                int slot = Enumerable.Range(0, run.RunLoadout.Entries.Count).First(i => run.RunLoadout.Entries[i]?.Id == id);
                bool bought = false;
                for (int radius = 2; radius <= 4 && !bought; radius++)
                {
                    for (int y = -radius; y <= radius && !bought; y++)
                    for (int x = -radius; x <= radius && !bought; x++)
                    {
                        if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
                        var cell = new Cell(home.X + x, home.Y + y);
                        bought = run.BuildEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None, out _, heading);
                    }
                }
                Require(bought, "Unable to purchase " + id + " near " + home);
            }
        }
    }
}
