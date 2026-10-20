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
            bool observedAim = false, observedRecoil = false, observedRest = false;
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
                    var rest = AssemblyVisualComposer.Compose(tower.ResolvedEntry.Snapshot, new AssemblyVisualPose());
                    var idlePose = AssemblyPreviewPose.For(tower.ResolvedEntry.Snapshot);
                    var poses = rest.Anchors.Select(anchor => {
                        var target = tower.GetMountAim(anchor.SlotIndex);
                        Vector2 direction = new Vector2(target.X, target.Y) - NotebookAssemblyView.Point(origin, anchor.RotationCenter, tower.ResolvedEntry.Snapshot);
                        float radians = target.X == 0 && target.Y == 0
                            ? idlePose.Mounts.Single(pose => pose.SlotIndex == anchor.SlotIndex).Radians
                            : direction.sqrMagnitude < .000001f ? 0 : Mathf.Atan2(direction.y, direction.x) - Mathf.PI * .5f;
                        radians = Mathf.Atan2(Mathf.Sin(radians), Mathf.Cos(radians));
                        var mechanism = tower.ResolvedEntry.Snapshot.Recipe.Tower.Mounts.Single(mount => mount.SlotIndex == anchor.SlotIndex).Weapon.Mechanism;
                        Assert.That(AssemblyVisualComposer.Fits.TryGetWeapon(mechanism.Id, mechanism.Revision, out var fit), Is.True);
                        float recoil = playback.Animation.Recoil(tower.Id, anchor.SlotIndex, seconds);
                        return new AssemblyMountPose(anchor.SlotIndex, Mathf.Clamp(radians, fit.MinRadians, fit.MaxRadians),
                            Mathf.Clamp(recoil, -fit.RecoilLimit, 0));
                    }).ToArray();
                    observedAim |= poses.Any(pose => {
                        var target = tower.GetMountAim(pose.SlotIndex);
                        return (target.X != 0 || target.Y != 0) && Mathf.Abs(pose.Radians) > .001f;
                    });
                    observedRecoil |= poses.Any(pose => pose.Recoil < -.001f);
                    observedRest |= poses.All(pose => Mathf.Abs(pose.Recoil) < .000001f);
                    var expected = AssemblyVisualComposer.Compose(tower.ResolvedEntry.Snapshot, new AssemblyVisualPose(poses));
                    var trialPen = new MeshPen(trialMesh);
                    NotebookTrialRenderer.Draw(trialPen, playback); trialPen.Upload();
                    var battlePen = new MeshPen(battleMesh);
                    DoodleGlyphs.StructureBase(battlePen, tower);
                    DoodleGlyphs.StructureDynamic(battlePen, tower, 1, seconds, playback.Animation); battlePen.Upload();
                    AssertRecipeQuads(trialMesh, expected, origin, tower.ResolvedEntry.Snapshot, "trial");
                    AssertRecipeQuads(battleMesh, expected, origin, tower.ResolvedEntry.Snapshot, "battle");
                }
                Assert.That(observedAim, Is.True, "The real recording must exercise an aimed pose.");
                Assert.That(observedRecoil, Is.True, "The real firing event must exercise visible recoil.");
                Assert.That(observedRest, Is.True, "The initial recording must include a pose without recoil.");
            }
            finally { Object.DestroyImmediate(trialMesh); Object.DestroyImmediate(battleMesh); }
        }

        static void AssertRecipeQuads(Mesh mesh, AssemblyVisualComposition expected, Vector2 origin, RecipeSnapshot snapshot, string surface)
        {
            var uv = new List<Vector2>(); mesh.GetUVs(1, uv); var vertices = mesh.vertices;
            foreach (var group in expected.Fragments.GroupBy(fragment => (fragment.SpriteId, fragment.Trim, fragment.Region)))
            {
                var sample = group.First();
                Assert.That(NotebookSpriteCatalog.TryGet(sample.SpriteId, out var sprite), Is.True, sample.SpriteId);
                Rect draw = Rect.MinMaxRect(Mathf.Max(sample.Trim.xMin, sample.Region.xMin), Mathf.Max(sample.Trim.yMin, sample.Region.yMin),
                    Mathf.Min(sample.Trim.xMax, sample.Region.xMax), Mathf.Min(sample.Trim.yMax, sample.Region.yMax));
                Vector2[] corners = { draw.min, new Vector2(draw.xMax, draw.yMin), draw.max, new Vector2(draw.xMin, draw.yMax) };
                Vector2[] expectedUv = corners.Select(point => new Vector2(
                    (sprite.rect.xMin + (point.x - sample.Trim.xMin) / sample.Trim.width * sprite.rect.width) / sprite.texture.width,
                    (sprite.rect.yMin + (point.y - sample.Trim.yMin) / sample.Trim.height * sprite.rect.height) / sprite.texture.height)).ToArray();
                var actual = new List<Vector2[]>();
                for (int first = 0; first + 3 < uv.Count; first++)
                {
                    bool matches = true;
                    for (int corner = 0; corner < 4; corner++)
                        if (Vector2.Distance(uv[first + corner], expectedUv[corner]) > .000001f) matches = false;
                    if (matches) actual.Add(new[] { (Vector2)vertices[first], (Vector2)vertices[first + 1],
                        (Vector2)vertices[first + 2], (Vector2)vertices[first + 3] });
                }
                var fragments = group.ToArray();
                Assert.That(actual.Count, Is.EqualTo(fragments.Length), surface + ": " + sample.SpriteId + " fragment count");
                for (int index = 0; index < fragments.Length; index++)
                    for (int corner = 0; corner < 4; corner++)
                        Assert.That(Vector2.Distance(actual[index][corner], NotebookAssemblyView.Point(origin, fragments[index].Transform(corners[corner]), snapshot)),
                            Is.LessThan(.00001f), surface + ": " + sample.SpriteId + ", mount." + fragments[index].SlotIndex + ", corner " + corner);
            }
        }
    }
}
