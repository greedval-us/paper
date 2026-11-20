using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyModernAdapterTests
    {
        [Test]
        public void RecordedModernDoubleGun_TrialAndBattleDrawEveryFrozenRecipeFragmentAtInitialAimAndRecoil()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var entry = catalog.Starters.Single(item => item.Id == "album.double");
            var balance = GameBalance.CreateDefault();
            var resolved = AssemblyResolver.Resolve(entry.Snapshot, balance, MaterialTier.Pencil);
            Assert.That(resolved.Valid, Is.True);
            var report = TrialRunner.Run(entry, balance, TrialScenario.Crowd, budget: resolved.Value.Cost, seed: 7);
            Assert.That(report.Copies, Is.EqualTo(1));
            var first = report.Frames.First(frame => frame.Towers.Count > 0);
            var firing = report.Frames.First(frame => frame.Events.Any(item => item.Kind == SimulationEventKind.WeaponActionStarted));
            var action = firing.Events.First(item => item.Kind == SimulationEventKind.WeaponActionStarted);
            var playback = new NotebookTrialPlayback(); playback.Bind(report);
            var trialMesh = new Mesh(); var battleMesh = new Mesh();
            bool observedAim = false, observedRecoil = false, observedRest = false, observedMechanismWork = false;
            try
            {
                foreach (double time in new[] { (double)first.Seconds, firing.Seconds, action.SimulationSeconds + .024 }.Distinct().OrderBy(value => value))
                {
                    playback.Seek(time);
                    var tower = report.Frames[playback.FrameIndex].Towers.Single();
                    Assert.That(tower.ResolvedEntry?.Kind, Is.EqualTo(AlbumEntryKind.Tower));
                    Assert.That(tower.ResolvedEntry.Snapshot.Recipe, Is.EqualTo(entry.Snapshot.Recipe));
                    Vector2 origin = new Vector2(tower.Cell.X + .5f, tower.Cell.Y + .5f);
                    float seconds = (float)playback.Seconds;
                    float body = (tower.FacingDegrees - 90) * Mathf.Deg2Rad;
                    var rest = AssemblyVisualComposer.ComposePlaced(tower.ResolvedEntry.Snapshot, null, body);
                    var poses = rest.Anchors.Select(anchor => {
                        var recordedMount = tower.GetMountState(anchor.SlotIndex);
                        var target = recordedMount.Aim;
                        Vector2 direction = new Vector2(target.X, target.Y) - NotebookAssemblyView.Point(origin, anchor.RotationCenter, tower.ResolvedEntry.Snapshot);
                        float radians = recordedMount.TargetId == 0
                            ? 0
                            : direction.sqrMagnitude < .000001f ? 0 : Mathf.Atan2(direction.y, direction.x) - Mathf.PI * .5f - body;
                        radians = Mathf.Atan2(Mathf.Sin(radians), Mathf.Cos(radians));
                        var mechanism = tower.ResolvedEntry.Snapshot.Recipe.Tower.Mounts.Single(mount => mount.SlotIndex == anchor.SlotIndex).Weapon.Mechanism;
                        Assert.That(AssemblyVisualComposer.Fits.TryGetWeapon(mechanism.Id, mechanism.Revision, out var fit), Is.True);
                        float recoil = playback.Animation.Recoil(tower.Id, anchor.SlotIndex, seconds);
                        // The v4 receiver and magazine move inside their fixed fragment.
                        // Reconstruct the expected pose from the recorded real cycle and
                        // recoil, without calling the renderer's pose adapter under test.
                        var mechanics = new AssemblyMechanicsPose(
                            recordedMount.Cycle != null && recordedMount.IsReloading,
                            recordedMount.Cycle != null ? (float)(1 - recordedMount.CycleRemainingWork) : 0,
                            fit.RecoilLimit > 0 ? -recoil / fit.RecoilLimit : 0);
                        return new AssemblyMountPose(anchor.SlotIndex, Mathf.Clamp(radians, fit.MinRadians, fit.MaxRadians),
                            Mathf.Clamp(recoil, -fit.RecoilLimit, 0), mechanics);
                    }).ToArray();
                    observedAim |= poses.Any(pose => {
                        return tower.GetMountTargetId(pose.SlotIndex) != 0 && Mathf.Abs(pose.Radians) > .001f;
                    });
                    observedRecoil |= poses.Any(pose => pose.Recoil < -.001f);
                    observedRest |= poses.All(pose => Mathf.Abs(pose.Recoil) < .000001f);
                    observedMechanismWork |= poses.Any(pose => pose.Mechanics.Fire > .001f);
                    var expected = AssemblyVisualComposer.ComposePlaced(tower.ResolvedEntry.Snapshot, new AssemblyVisualPose(poses), body);
                    var trialPen = new MeshPen(trialMesh);
                    NotebookTrialRenderer.Draw(trialPen, playback); trialPen.Upload();
                    var battlePen = new MeshPen(battleMesh);
                    DoodleGlyphs.StructureBase(battlePen, tower);
                    DoodleGlyphs.StructureDynamic(battlePen, tower, 1, seconds, playback.Animation); battlePen.Upload();
                    AssertRecipeGeometry(trialMesh, expected, origin, tower.ResolvedEntry.Snapshot, "trial");
                    AssertRecipeGeometry(battleMesh, expected, origin, tower.ResolvedEntry.Snapshot, "battle");
                    foreach (var anchor in expected.Anchors)
                        Assert.That(Vector2.Distance(DoodleGlyphs.SegmentOrigin(tower, anchor.SlotIndex,
                            tower.GetMountTargetId(anchor.SlotIndex) == 0 ? default : tower.GetMountAim(anchor.SlotIndex),
                            origin, seconds, playback.Animation, null),
                            NotebookAssemblyView.Point(origin, anchor.Muzzle, tower.ResolvedEntry.Snapshot)),
                            Is.LessThan(.00001f), "The recorded mount must keep its own transformed muzzle: " + anchor.SlotIndex);
                }
                Assert.That(observedAim, Is.True, "The real recording must exercise an aimed pose.");
                Assert.That(observedRecoil, Is.True, "The real firing event must exercise visible recoil.");
                Assert.That(observedRest, Is.True, "The initial recording must include a pose without recoil.");
                Assert.That(observedMechanismWork, Is.True, "The real firing record must exercise internal mechanism movement.");
            }
            finally { Object.DestroyImmediate(trialMesh); Object.DestroyImmediate(battleMesh); }
        }

        static void AssertRecipeGeometry(Mesh mesh, AssemblyVisualComposition expected, Vector2 origin, RecipeSnapshot snapshot, string surface)
        {
            var uv = new List<Vector2>(); mesh.GetUVs(1, uv); var vertices = mesh.vertices;
            Assert.That(uv, Is.All.EqualTo(new Vector2(.5f, .5f)), surface + ": native fragments must not sample deleted art.");
            var consumed = new bool[vertices.Length];
            int consumedFragments = 0;
            float scale = NotebookAssemblyView.ScaleFor(snapshot);
            foreach (var fragment in expected.Fragments)
            {
                // Draw under the same world transform: the bounded stroke inset is authored
                // in final units, so scaling a unit-scale pen stream afterwards is not equivalent.
                var drawn = DrawFragment(fragment, origin, scale);
                Assert.That(drawn, Is.Not.Empty, fragment.PartId + " must contribute actual pen geometry.");
                var corners = new[] { fragment.Region.min, new Vector2(fragment.Region.xMax, fragment.Region.yMin),
                    fragment.Region.max, new Vector2(fragment.Region.xMin, fragment.Region.yMax) }
                    .Select(point => NotebookAssemblyView.Point(origin, fragment.Transform(point), snapshot)).ToArray();
                foreach (var point in drawn)
                {
                    Assert.That(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y), Is.False);
                    Assert.That(point.x, Is.InRange(corners.Min(corner => corner.x) - .00001f, corners.Max(corner => corner.x) + .00001f));
                    Assert.That(point.y, Is.InRange(corners.Min(corner => corner.y) - .00001f, corners.Max(corner => corner.y) + .00001f));
                }
                int found = -1;
                for (int first = 0; first + drawn.Length <= vertices.Length; first++)
                {
                    bool matches = true;
                    for (int vertex = 0; vertex < drawn.Length && matches; vertex++)
                        matches = !consumed[first + vertex] && Vector2.Distance(vertices[first + vertex], drawn[vertex]) <= .00001f;
                    if (matches) { found = first; break; }
                }
                Assert.That(found, Is.GreaterThanOrEqualTo(0), surface + ": missing transformed part " + fragment.PartId + ", mount." + fragment.SlotIndex);
                for (int vertex = 0; vertex < drawn.Length; vertex++) consumed[found + vertex] = true;
                consumedFragments++;
            }
            Assert.That(consumedFragments, Is.EqualTo(expected.Fragments.Count), "Each frozen recipe fragment must reach the actual renderer exactly once.");
        }

        static Vector2[] DrawFragment(AssemblyVisualFragment fragment, Vector2 origin, float scale)
        {
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                fragment.Draw(pen, origin, 1, Color.white, scale); pen.Upload();
                return mesh.vertices.Select(point => (Vector2)point).ToArray();
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
