using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PaperDefense.Core;
using PaperDefense.Presentation;
using PaperDefense.Validation;
using UnityEngine;
using UnityEngine.Rendering;

namespace PaperDefense.Editor
{
    /// <summary>Isolated current-version evidence. These methods neither read profiles nor replace historical reports.</summary>
    public static class FullLoopValidation
    {
        public static string RunPressureJson()
        {
            var timer = Stopwatch.StartNew();
            var normal = Replay(1); var accelerated = Replay(2); timer.Stop();
            bool identical = normal.Report.Tick == accelerated.Report.Tick && normal.Report.Fingerprint == accelerated.Report.Fingerprint;
            return JsonUtility.ToJson(new PressureReport
            {
                runAtUtc = DateTime.UtcNow.ToString("O"), unityVersion = UnityEngine.Application.unityVersion,
                scope = "Current core replay in Editor; historical 3000-enemy load fixture, not target campaign balance or rendered FPS. Groupings1/2 use identical fixed steps. No profile IO.",
                configuration = normal.Config, commands = normal.Commands.ToArray(), normal = normal.Report, accelerated = accelerated.Report,
                identicalState = identical, passed = identical && normal.Report.Passed && accelerated.Report.Passed,
                wallTimeMilliseconds = timer.Elapsed.TotalMilliseconds
            }, true);
        }

        static PressureScenario Replay(int group)
        {
            var scenario = new PressureScenario();
            while (!scenario.Finished) for (int i = 0; i < group && !scenario.Finished; i++) scenario.Step();
            return scenario;
        }

        public static string RunAuthoredCopiesJson()
        {
            const int copies = 120, iterations = 30;
            var parts = new List<DrawingPart>();
            foreach (var key in new[] { DrawingPartKey.Base, DrawingPartKey.Mount0, DrawingPartKey.Mount1, DrawingPartKey.Decoration })
            {
                var points = new DrawingPoint[128];
                for (int i = 0; i < points.Length; i++)
                    points[i] = new DrawingPoint(-.38f + i * .006f, (float)Math.Sin(i * .43 + (int)key) * .27f);
                parts.Add(new DrawingPart(key, new[] { new DrawingStroke(points) }));
            }
            var drawing = new DrawingSnapshot(parts);
            var blueprint = new TowerBlueprint("load-authored", "512-point crooked double", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun,
                drawing: drawing, origin: DrawingOrigin.PlayerDrawing);
            var balance = GameBalancePresets.CreateTargetWorkshop();
            balance.StartingInk = 100000; balance.PlanningSeconds = 10000; balance.EnableInkWells = false;
            balance.Structure(StructureKind.Gun).BuildSeconds = 0; balance.Construction.SecondaryBuildSeconds = 0;
            var game = new GameSession(balance, starterDefenses: false);
            var cells = new List<Cell>();
            for (int i = 0; i < copies; i++) cells.Add(new Cell(4 + i % 20 * 2, 4 + i / 20 * 2));
            string reason;
            if (!game.TryBuildBlueprint(cells, blueprint, MaterialTier.Pencil, InkColor.None, out reason)) throw new InvalidOperationException(reason);
            game.Tick(1f / 30);
            game.Structures[0].Health -= 7; game.Structures[0].Cooldown = .37f;
            bool independent = game.Structures[1].Health == game.Structures[1].MaxHealth && game.Structures[1].Cooldown == 0 &&
                game.Structures[0].Id != game.Structures[1].Id;
            AuthoredGeometryCache.Clear();
            var prepared = AuthoredGeometryCache.For(drawing);
            bool shared = true;
            foreach (var state in game.Structures)
                shared &= ReferenceEquals(prepared, AuthoredGeometryCache.For(state.Blueprint.Drawing));
            var root = new GameObject("Isolated authored-copy validation");
            try
            {
                var view = root.AddComponent<NotebookWorldView>(); view.Initialize(new RunReadModel(game));
                root.SendMessage("LateUpdate");
                int staticBefore = view.StaticStructureBuildCount;
                double total = 0, maximum = 0;
                for (int frame = 0; frame < iterations; frame++)
                {
                    foreach (var state in game.Structures)
                    {
                        state.PrimaryAim = new Point2(state.Cell.X + 3 + frame * .1f, state.Cell.Y + 4);
                        state.SecondaryAim = new Point2(state.Cell.X - 4, state.Cell.Y + 2 + frame * .1f);
                    }
                    root.SendMessage("LateUpdate");
                    total += view.LastMeshBuildMilliseconds; maximum = Math.Max(maximum, view.LastMeshBuildMilliseconds);
                }
                var meshes = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                bool uint32 = meshes.All(m => m.indexFormat == IndexFormat.UInt32);
                bool complete = game.Structures.All(s => s.IsComplete);
                bool immutable = drawing.GeometryHash == blueprint.Drawing.GeometryHash && drawing.PointCount == 512;
                return JsonUtility.ToJson(new CopiesReport
                {
                    runAtUtc = DateTime.UtcNow.ToString("O"), unityVersion = UnityEngine.Application.unityVersion,
                    processor = SystemInfo.processorType, graphicsDevice = SystemInfo.graphicsDeviceName,
                    scope = "Editor CPU mesh assembly only:120 authored double-gun copies,512 source points/page,30 synchronous dynamic-aim redraws after warmup. Enlarged isolated load-fixture budget; no enemies or profile IO. No rendered FPS or Player timing claim.",
                    copies = game.Structures.Count, pointsPerPage = drawing.PointCount, iterations = iterations,
                    cacheBuilds = AuthoredGeometryCache.BuildCount, meshBatches = meshes.Length,
                    maximumVerticesInOneMesh = meshes.Max(m => m.vertexCount), totalMeshVertices = meshes.Sum(m => m.vertexCount),
                    allMeshesUse32BitIndices = uint32, independentStates = independent, sharedPreparedGeometry = shared,
                    immutableSource = immutable, completedStructures = complete,
                    staticRebuildsDuringDynamicAim = view.StaticStructureBuildCount - staticBefore,
                    averageMeshBuildMilliseconds = total / iterations, maximumMeshBuildMilliseconds = maximum,
                    passed = game.Structures.Count == copies && complete && independent && shared && immutable && uint32 &&
                        meshes.Length == 5 && AuthoredGeometryCache.BuildCount == 1 && view.StaticStructureBuildCount == staticBefore
                }, true);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Serializable] sealed class PressureReport
        {
            public string runAtUtc, unityVersion, scope;
            public bool passed, identicalState;
            public double wallTimeMilliseconds;
            public PressureScenarioConfig configuration;
            public PressureScenarioCommand[] commands;
            public PressureScenarioReport normal, accelerated;
        }
        [Serializable] sealed class CopiesReport
        {
            public string runAtUtc, unityVersion, processor, graphicsDevice, scope;
            public int copies, pointsPerPage, iterations, cacheBuilds, meshBatches, maximumVerticesInOneMesh, totalMeshVertices, staticRebuildsDuringDynamicAim;
            public bool allMeshesUse32BitIndices, independentStates, sharedPreparedGeometry, immutableSource, completedStructures, passed;
            public double averageMeshBuildMilliseconds, maximumMeshBuildMilliseconds;
        }
    }
}
