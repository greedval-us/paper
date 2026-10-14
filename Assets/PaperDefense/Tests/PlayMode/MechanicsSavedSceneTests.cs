#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using PaperDefense.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class MechanicsSavedSceneTests
    {
        [UnityTest] public IEnumerator IntegrationHonorsBattleHudPauseAndSpeedAndKeepsSingleStepExact()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/PaperDefense/Scenes/Tests/13_Integration.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var h = Object.FindFirstObjectByType<MechanicsTestScene>();
            Assert.That(h.Prepared, Is.True, h.PreparationError);
            h.enabled = false; // Drive exact frame durations without a concurrent Unity Update.
            var c = h.Controller;
            Assert.That(c.StartRun(out string reason), Is.True, reason);
            Assert.That(h.Paused, Is.EqualTo(c.Paused), "The stand and HUD must share one pause state after starting a run.");
            long tick = c.Session.TickIndex;
            c.TogglePause();
            Assert.That(h.AdvanceFrame(FixedStepClock.Step * 3), Is.Zero);
            Assert.That(c.Session.TickIndex, Is.EqualTo(tick));
            c.TogglePause(); c.ToggleSpeed();
            Assert.That(h.AdvanceFrame(FixedStepClock.Step), Is.EqualTo(2));
            Assert.That(c.Session.TickIndex, Is.EqualTo(tick + 2));
            h.TogglePause(); h.Step();
            Assert.That(c.Session.TickIndex, Is.EqualTo(tick + 3));
            Assert.That(h.AdvanceFrame(FixedStepClock.Step), Is.Zero);
            var last = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Integration controls verified"));
            yield return SceneManager.UnloadSceneAsync(last);
        }

        [UnityTest] public IEnumerator EverySavedScenePreparesOneIsolatedDriverExercisesItsFocusAndResets()
        {
            string[] allScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/PaperDefense/Scenes/Tests" });
            Assert.That(allScenes.Length, Is.EqualTo(26));
            // Independent visual galleries are exercised by NotebookArtStandSceneTests;
            // the original 23 scenes each still require their isolated simulation driver.
            string[] paths = Array.FindAll(allScenes, guid => {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                return !path.EndsWith("/24_TowerArt.unity", StringComparison.Ordinal)
                    && !path.EndsWith("/25_EnemyAnimation.unity", StringComparison.Ordinal)
                    && !path.EndsWith("/26_InkEffects.unity", StringComparison.Ordinal);
            });
            Assert.That(paths.Length, Is.EqualTo(23));
            Array.Sort(paths, (a, b) => StringComparer.Ordinal.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
            foreach (string guid in paths)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var harnesses = Object.FindObjectsByType<MechanicsTestScene>(FindObjectsSortMode.None);
                Assert.That(harnesses.Length, Is.EqualTo(1), path);
                var harness = harnesses[0];
                Assert.That(harness.Prepared, Is.True, path + " " + harness.PreparationError);
                Assert.That(Object.FindObjectsByType<RunController>(FindObjectsSortMode.None).Length, Is.EqualTo(1), path);
                Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1), path);
                var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                Assert.That(cameras.Length, Is.InRange(1, 2), path);
                Assert.That(harness.SimulationDriverCount, Is.EqualTo(1), path);
                Assert.That(harness.Controller.AutomaticSimulation, Is.False, path);
                if (harness.scenario.focus != MechanicsTestFocus.Profile)
                {
                    Assert.That(harness.Controller.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly), path);
                    Assert.That(harness.ProfileLoadCalls + harness.ProfileSaveCalls, Is.Zero, path);
                }
                // Single step must advance exactly one tick even while paused; no Update debt may leak into the next frame.
                if (!harness.Paused) harness.TogglePause();
                long tick = harness.Controller.Session.TickIndex;
                harness.Step();
                Assert.That(harness.Controller.Session.TickIndex, Is.EqualTo(tick + (harness.Controller.InWorkshop ? 0 : 1)), path);
                yield return null;
                Assert.That(harness.Controller.Session.TickIndex, Is.EqualTo(tick + (harness.Controller.InWorkshop ? 0 : 1)), path);
                ExerciseFocus(harness, path);
                int generation = harness.ResetGeneration;
                var oldSession = harness.Controller.Session;
                harness.ResetScenario();
                if (!harness.Paused) harness.TogglePause();
                yield return null;
                Assert.That(harness.Prepared, Is.True, path + " " + harness.PreparationError);
                Assert.That(harness.ResetGeneration, Is.EqualTo(generation + 1), path);
                Assert.That(harness.Controller.Session, Is.Not.SameAs(oldSession), path);
                Assert.That(harness.Controller.Session.TickIndex, Is.Zero, path);
                Assert.That(Object.FindObjectsByType<RunController>(FindObjectsSortMode.None).Length, Is.EqualTo(1), path);
            }
            var last = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Mechanics verification complete"));
            yield return SceneManager.UnloadSceneAsync(last);
        }

        static void ExerciseFocus(MechanicsTestScene h, string path)
        {
            var focus = h.scenario.focus; var c = h.Controller;
            if (focus == MechanicsTestFocus.Assembly || focus == MechanicsTestFocus.Integration)
            { Assert.That(c.InWorkshop, Is.True, path); Assert.That(c.Content.Chassis.Count, Is.GreaterThanOrEqualTo(4), path); return; }
            if (focus == MechanicsTestFocus.Drawing)
            {
                Assert.That(c.Workshop.Loadout[0].WeaponMounts.Count, Is.EqualTo(3), path);
                h.CompareDrawing(); Assert.That(h.QualityComparison, Is.Not.Null, path); return;
            }
            if (focus == MechanicsTestFocus.Profile)
            {
                Assert.That(h.FixtureDirectory, Does.Contain("PaperDefenseMechanics"), path);
                Assert.That(h.SaveProfileVariant(), Is.True, path);
                int count = c.Workshop.Album.Count;
                h.ReloadFixture(); Assert.That(h.Controller.Workshop.Album.Count, Is.EqualTo(count), path);
                h.SetProfileWriteFailure(true); Assert.That(h.SaveProfileVariant(), Is.False, path);
                Assert.That(h.Controller.Workshop.Album.Count, Is.EqualTo(count), path);
                h.SetProfileWriteFailure(false); Assert.That(h.SaveProfileVariant(), Is.True, path); return;
            }
            if (focus == MechanicsTestFocus.Pressure)
            {
                Assert.That(c.Session.Balance.StartingInk, Is.EqualTo(20000), path);
                Assert.That(c.Session.Structures.Count, Is.GreaterThan(100), path);
                int delivered = 0, launchEvents = 0;
                long lastSequence = 0;
                long stopTick = c.Session.TickIndex + 2000;
                bool ordered = true;
                Action<SimulationEvent> observe = e => {
                    delivered++; ordered &= e.Sequence > lastSequence; lastSequence = e.Sequence;
                    if (e.Kind == SimulationEventKind.Shot || e.Kind == SimulationEventKind.ProjectileLaunched) launchEvents++;
                };
                c.SimulationEventReceived += observe;
                try
                {
                    // Edge occupancy can delay admissions past the nominal spawn interval.
                    for (int i = 0; i < 1800; i++)
                    {
                        h.Step();
                        if (c.Session.WaveIndex > 0 && c.Session.RemainingToSpawn == 0) break;
                    }
                    Assert.That(c.Session.WaveIndex, Is.EqualTo(1), path);
                    Assert.That(c.Session.RemainingToSpawn, Is.Zero, path + " pressure spawning must finish within 1,800 ticks");
                    Assert.That(c.Session.Statistics.PeakAliveEnemies, Is.EqualTo(3000), path);
                    // The closed ring must breach naturally before its inner weapons can produce visual events.
                    while (c.Session.TickIndex < stopTick && launchEvents == 0) h.Step();
                }
                finally { c.SimulationEventReceived -= observe; }
                Assert.That(c.Session.Statistics.Launches(StructureKind.Mortar) + c.Session.Statistics.Launches(StructureKind.Piercer),
                    Is.GreaterThan(0), path + " pressure weapons must launch within 2,000 ticks");
                Assert.That(launchEvents, Is.GreaterThan(0), path + " a weapon launch must reach the visual event subscriber");
                Assert.That(delivered, Is.GreaterThan(0), path + " external pressure steps must deliver visual events");
                Assert.That(ordered, Is.True, path + " pressure events must be delivered once in sequence"); return;
            }
            if (focus == MechanicsTestFocus.Wells)
            {
                Assert.That(c.Session.Wells.Count, Is.EqualTo(3), path);
                int ink = c.Session.Ink; Assert.That(c.ActivateWell(0, out _), Is.True, path);
                Assert.That(c.Session.Ink, Is.LessThan(ink), path); Assert.That(c.Session.Wells[0].Active, Is.True, path); return;
            }
            if (focus == MechanicsTestFocus.Construction)
            {
                Assert.That(c.Session.Structures.Count, Is.GreaterThan(4), path);
                Assert.That(c.Erase(new Cell(21, 13), out int refund), Is.True, path); Assert.That(refund, Is.GreaterThanOrEqualTo(0)); return;
            }
            if (focus == MechanicsTestFocus.Economy)
            {
                int ink = c.Session.Ink; for (int i = 0; i < 60; i++) h.Step();
                Assert.That(c.Session.Ink, Is.EqualTo(ink), path); c.NextWave();
                Assert.That(c.Session.Phase, Is.EqualTo(RunPhase.Assault), path); return;
            }
            if (focus == MechanicsTestFocus.Armor) { ExerciseArmor(h, path); return; }
            if (focus == MechanicsTestFocus.Tracking) { ExerciseTracking(h, path); return; }
            if (focus == MechanicsTestFocus.Feed) { ExerciseFeed(h, path); return; }
            if (focus == MechanicsTestFocus.Color) { ExerciseColors(h, path); return; }
            int beforeBlots = c.Session.Blots.Count;
            float beforeHealth = c.Session.BaseHealth;
            for (int i = 0; i < 450; i++) h.Step();
            if (focus == MechanicsTestFocus.Blotter) Assert.That(c.Session.Blots.Count, Is.LessThan(beforeBlots), path);
            else if (focus == MechanicsTestFocus.Ranged)
                Assert.That(c.Session.BaseHealth < beforeHealth || c.Session.Statistics.DestroyedStructures > 0 ||
                    c.Session.Structures[0].Health < c.Session.Structures[0].MaxHealth, Is.True, path);
            else Assert.That(c.Session.Statistics.TotalDamage, Is.GreaterThan(0), path);
            if (focus == MechanicsTestFocus.Mortar)
                Assert.That(c.Session.Enemies[0].Health, Is.EqualTo(h.scenario.targets[0].health), path + " mortar dead zone");
            if (focus == MechanicsTestFocus.Rail)
                Assert.That(c.Session.Enemies[0].Health, Is.EqualTo(h.scenario.targets[0].health), path + " baseline out of range");
            if (focus == MechanicsTestFocus.EnemyTiers)
            {
                Assert.That(c.Session.Enemies[0].Health, Is.LessThan(c.Session.Enemies[1].Health), path);
                Assert.That(c.Session.Enemies[1].Health, Is.LessThan(c.Session.Enemies[2].Health), path);
            }
        }

        static void ExerciseArmor(MechanicsTestScene h, string path)
        {
            var c = h.Controller;
            var plainCell = PlacementCell(h, 0); var armoredCell = PlacementCell(h, 1);
            var plain = TowerAt(c, plainCell, path); var armored = TowerAt(c, armoredCell, path);
            Assert.That(armored.MaxHealth, Is.EqualTo(plain.MaxHealth), path + " armor must protect without extra HP");
            float defense = TowerCatalog.Defense(armored.Blueprint, h.scenario.CreateBalance());
            Assert.That(defense, Is.GreaterThan(0), path);
            // Compare the first paired impacts, before the two construction times or a later breach
            // can give the attackers a different number of successful shots.
            bool observedPairedImpact = false;
            for (int i = 0; i < 450; i++)
            {
                h.Step(); plain = TowerAt(c, plainCell, path); armored = TowerAt(c, armoredCell, path);
                float plainLoss = plain.MaxHealth - plain.Health, armoredLoss = armored.MaxHealth - armored.Health;
                if (plainLoss <= 0 || armoredLoss <= 0) continue;
                Assert.That(armoredLoss, Is.LessThan(plainLoss), path + " the shielded tower must take less damage");
                Assert.That(armoredLoss, Is.EqualTo(plainLoss * (1 - defense)).Within(.001f), path);
                observedPairedImpact = true; break;
            }
            Assert.That(observedPairedImpact, Is.True, path + " both towers must receive an actual enemy hit");
        }

        static void ExerciseTracking(MechanicsTestScene h, string path)
        {
            var c = h.Controller;
            var cells = new[] { PlacementCell(h, 0), PlacementCell(h, 1) };
            var ids = new[] { TowerAt(c, cells[0], path).Id, TowerAt(c, cells[1], path).Id };
            var firstShot = new long[2]; var firstTarget = new int[2];
            var start = new long[2]; var finish = new long[2]; var initialRemaining = new float[2];
            Action<SimulationEvent> observe = e => {
                if (e.Kind != SimulationEventKind.Shot) return;
                for (int i = 0; i < 2; i++)
                    if (e.EntityId == ids[i] && firstShot[i] == 0)
                    {
                        firstShot[i] = e.Tick;
                        firstTarget[i] = TowerAt(c, cells[i], path).GetMountTargetId(0);
                    }
            };
            c.SimulationEventReceived += observe;
            try
            {
                for (int tick = 0; tick < 240 && (finish[0] == 0 || finish[1] == 0); tick++)
                {
                    h.Step();
                    for (int i = 0; i < 2; i++)
                    {
                        var state = TowerAt(c, cells[i], path).GetMountState(0);
                        if (firstShot[i] == 0 || state.TargetId == 0 || state.TargetId == firstTarget[i]) continue;
                        if (start[i] == 0)
                        { start[i] = c.Session.TickIndex; initialRemaining[i] = state.AcquisitionRemaining; }
                        if (finish[i] == 0 && state.AcquisitionRemaining <= .000001f) finish[i] = c.Session.TickIndex;
                    }
                }
            }
            finally { c.SimulationEventReceived -= observe; }
            Assert.That(firstShot[0], Is.GreaterThan(0), path);
            Assert.That(firstShot[1], Is.GreaterThan(0), path);
            Assert.That(c.Session.Kills, Is.GreaterThanOrEqualTo(2), path + " first hits must kill the short-lived targets");
            Assert.That(finish[0], Is.GreaterThan(0), path + " ordinary tower must reacquire a second target");
            Assert.That(finish[1], Is.GreaterThan(0), path + " tracking tower must reacquire a second target");
            Assert.That(initialRemaining[1], Is.LessThan(initialRemaining[0]), path);
            Assert.That(finish[1] - start[1], Is.LessThan(finish[0] - start[0]),
                path + " tracking must reduce observed reacquisition time independently of construction and reload");
        }

        static void ExerciseFeed(MechanicsTestScene h, string path)
        {
            var totalLaunches = new int[4]; var barrelLaunches = new int[4][]; var reloadProgress = new float[4];
            for (int variant = 0; variant < 4; variant++)
            {
                h.variant = variant; h.ResetScenario();
                Assert.That(h.Prepared, Is.True, path + " " + h.PreparationError);
                var c = h.Controller; var cell = PlacementCell(h, 0);
                int guns = variant < 3 ? variant + 1 : 2;
                var tower = TowerAt(c, cell, path);
                Assert.That(tower.Blueprint.WeaponMounts.Count, Is.EqualTo(variant < 3 ? guns : 3), path);
                int warmup = 0;
                while (c.Session.Statistics.Launches(StructureKind.Gun) == 0 && warmup++ < 240) h.Step();
                Assert.That(c.Session.Statistics.Launches(StructureKind.Gun), Is.EqualTo(guns), path + " each active barrel must fire");
                tower = TowerAt(c, cell, path);
                float beforeReload = tower.GetMountState(0).ActionRemaining;
                Assert.That(beforeReload, Is.EqualTo(1).Within(.000001f), path);
                h.Step();
                reloadProgress[variant] = beforeReload - TowerAt(c, cell, path).GetMountState(0).ActionRemaining;
                Assert.That(reloadProgress[variant], Is.GreaterThan(0), path + " a fired barrel must reload");
                int beforeCount = c.Session.Statistics.Launches(StructureKind.Gun);
                barrelLaunches[variant] = new int[guns];
                var previous = new float[guns];
                for (int gun = 0; gun < guns; gun++) previous[gun] = TowerAt(c, cell, path).GetMountState(gun).ActionRemaining;
                for (int tick = 0; tick < 300; tick++)
                {
                    h.Step(); tower = TowerAt(c, cell, path);
                    for (int gun = 0; gun < guns; gun++)
                    {
                        var state = tower.GetMountState(gun);
                        Assert.That(state.TargetId, Is.GreaterThan(0), path + " active gun must keep a valid target");
                        if (state.ActionRemaining > previous[gun] + .1f) barrelLaunches[variant][gun]++;
                        previous[gun] = state.ActionRemaining;
                    }
                    if (variant == 3)
                    {
                        var idle = tower.GetMountState(2);
                        Assert.That(idle.TargetId, Is.Zero, path + " mortar targets are inside its dead zone");
                        Assert.That(idle.ActionRemaining, Is.Zero, path + " a ready idle mortar must not reserve feed");
                    }
                }
                totalLaunches[variant] = c.Session.Statistics.Launches(StructureKind.Gun) - beforeCount;
                int counted = 0;
                foreach (int launches in barrelLaunches[variant])
                { Assert.That(launches, Is.GreaterThan(0), path); counted += launches; }
                Assert.That(counted, Is.EqualTo(totalLaunches[variant]), path + " observed barrel cycles must match actual launches");
                Assert.That(c.Session.Statistics.Launches(StructureKind.Mortar), Is.Zero, path);
            }
            Assert.That(reloadProgress[0], Is.GreaterThan(reloadProgress[1]), path + " two guns share feed");
            Assert.That(reloadProgress[1], Is.GreaterThan(reloadProgress[2]), path + " three guns share feed");
            Assert.That(reloadProgress[3], Is.EqualTo(reloadProgress[1]).Within(.00001f), path + " idle mortar releases its feed share");
            Assert.That(totalLaunches[1], Is.GreaterThan(totalLaunches[0] * 1.6f), path);
            Assert.That(totalLaunches[2], Is.EqualTo(totalLaunches[1]).Within(3), path + " third gun shares total production");
            Assert.That(barrelLaunches[2][0], Is.LessThan(barrelLaunches[1][0]), path);
            Assert.That(totalLaunches[3], Is.EqualTo(totalLaunches[1]).Within(2), path + " two guns plus idle mortar retain two-gun production");
        }

        static void ExerciseColors(MechanicsTestScene h, string path)
        {
            for (int variant = 0; variant < 3; variant++)
            {
                h.variant = variant; h.ResetScenario();
                Assert.That(h.Prepared, Is.True, path + " " + h.PreparationError);
                var c = h.Controller; var color = (InkColor)(variant + 1);
                var tower = TowerAt(c, PlacementCell(h, 0), path);
                Assert.That(tower.Blueprint.WeaponMounts.Count, Is.EqualTo(3), path);
                Assert.That(tower.Color, Is.EqualTo(color), path);
                // Strongest chooses the next undamaged target after each mount hits, creating
                // more than three damage recipients inside one shared color window.
                Assert.That(c.SetPriority(tower.Cell, TargetPriority.Strongest), Is.True, path);
                int stock = c.Session.ColoredInk(color);
                Assert.That(stock, Is.EqualTo(h.scenario.startingColoredInk - TowerCatalog.ColorCost(tower.Blueprint, h.scenario.CreateBalance())), path);
                var applications = new HashSet<int>(); var damagedInFirstWindow = new HashSet<int>();
                var previousTimers = new Dictionary<int, float>(); var previousHealth = new Dictionary<int, float>();
                foreach (var enemy in c.Session.Enemies) { previousTimers[enemy.Id] = ColorTimer(enemy, color); previousHealth[enemy.Id] = enemy.Health; }
                double nextWindow = 0; int window = -1; bool checkedFirstWindow = false;
                float windowSeconds = c.Session.Balance.ColorWindowSeconds;
                for (int tick = 0; tick < 240; tick++)
                {
                    h.Step(); double now = c.Session.Elapsed;
                    foreach (var enemy in c.Session.Enemies)
                    {
                        float timer = ColorTimer(enemy, color);
                        bool applied = timer > Math.Max(0, previousTimers[enemy.Id] - FixedStepClock.Step) + .00001f;
                        if (applied)
                        {
                            if (window < 0 || now >= nextWindow - .00001)
                            {
                                if (window == 0)
                                {
                                    Assert.That(applications.Count, Is.EqualTo(3), path + " " + color);
                                    Assert.That(damagedInFirstWindow.Count, Is.GreaterThan(3), path + " ordinary damage must continue beyond color allowance");
                                    checkedFirstWindow = true;
                                }
                                applications.Clear(); window++; nextWindow = now + windowSeconds;
                            }
                            applications.Add(enemy.Id);
                            Assert.That(applications.Count, Is.LessThanOrEqualTo(3), path + " " + color + " shared source window");
                        }
                        if (window == 0 && enemy.Health < previousHealth[enemy.Id] - .00001f) damagedInFirstWindow.Add(enemy.Id);
                        previousTimers[enemy.Id] = timer; previousHealth[enemy.Id] = enemy.Health;
                        if (color != InkColor.Red) Assert.That(enemy.RedRemaining, Is.Zero, path);
                        if (color != InkColor.Blue) Assert.That(enemy.BlueRemaining, Is.Zero, path);
                        if (color != InkColor.Green) Assert.That(enemy.GreenRemaining, Is.Zero, path);
                    }
                    if (window >= 2) break;
                }
                Assert.That(checkedFirstWindow, Is.True, path + " " + color + " must exhaust and reopen its budget");
                Assert.That(window, Is.GreaterThanOrEqualTo(2), path + " " + color + " must apply in later windows");
                Assert.That(c.Session.ColoredInk(color), Is.EqualTo(stock), path + " finite cartridge cost is paid once on deployment");
                Assert.That(c.Session.Statistics.Launches(StructureKind.Gun), Is.GreaterThanOrEqualTo(6), path);
            }
        }

        static float ColorTimer(EnemyView enemy, InkColor color)
            => color == InkColor.Red ? enemy.RedRemaining : color == InkColor.Blue ? enemy.BlueRemaining : enemy.GreenRemaining;
        static Cell PlacementCell(MechanicsTestScene h, int index)
            => new Cell(h.scenario.placements[index].cell.x, h.scenario.placements[index].cell.y);
        static StructureView TowerAt(RunController controller, Cell cell, string path)
        {
            var tower = controller.Session.StructureAt(cell);
            Assert.That(tower.HasValue, Is.True, path + " expected tower at " + cell);
            return tower.Value;
        }
    }
}
#endif
