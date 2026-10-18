using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyPilotAdapterTests
    {
        static readonly string[] Sprites = { "part.gun.barrels", "part.pilot.sight", "part.pilot.magazine", "part.pilot.trigger" };

        [Test]
        public void RecordedDoubleGun_TrialAndBattleAdaptersMatchRecipeCompositionAtRestAimAndRecoil()
        {
            var snapshot = AssemblyVisualPilotTests.Pilot();
            var balance = GameBalance.CreateDefault();
            var catalog = new ComponentCatalog(snapshot.BalanceRevision, snapshot.Definitions);
            var before = AssemblyResolver.Resolve(snapshot.Recipe, catalog, balance, MaterialTier.Pencil, AssemblyVisualComposer.Fits);
            Assert.That(before.Valid, Is.True);
            Assert.That(before.Value.Weapons.Select(w => w.Range), Is.All.EqualTo(8));
            var blueprint = new TowerBlueprint("adapter-pilot", "Adapter pilot", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);
            // The compatibility adapter retains historical combat tuning; the new recipe resolves its own range above.
            var legacyRanges = blueprint.WeaponMounts.ToDictionary(m => m.SlotIndex,
                m => TowerCatalog.WeaponRange(blueprint, balance, m.WeaponId, m.SlotIndex));
            int cost = TowerCatalog.Cost(blueprint, balance, MaterialTier.Pencil);
            var report = TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, budget: cost, seed: 7);
            Assert.That(report.Copies, Is.EqualTo(1));
            var first = report.Frames.First(f => f.Towers.Count > 0);
            var firing = report.Frames.First(f => f.Events.Any(e => e.Kind == SimulationEventKind.WeaponActionStarted));
            var action = firing.Events.First(e => e.Kind == SimulationEventKind.WeaponActionStarted);
            var playback = new NotebookTrialPlayback(); playback.Bind(report);
            var trialMesh = new Mesh(); var battleMesh = new Mesh();
            bool observedAim = false, observedRecoil = false;
            try
            {
                foreach (double time in new[] { (double)first.Seconds, firing.Seconds, action.SimulationSeconds + .024 }.Distinct().OrderBy(t => t))
                {
                    playback.Seek(time);
                    var tower = report.Frames[playback.FrameIndex].Towers.Single();
                    var origin = new Vector2(tower.Cell.X + .5f, tower.Cell.Y + .5f);
                    // Trial rendering samples its playback clock as a float; use that same pose in both adapters.
                    float seconds = (float)playback.Seconds;
                    var poses = tower.Blueprint.WeaponMounts.Select(selected => {
                        var mount = NotebookMountLayout.Resolve(tower.Blueprint, selected.SlotIndex);
                        float angle = mount.AimAngle(tower.GetMountAim(selected.SlotIndex), origin);
                        angle = Mathf.Atan2(Mathf.Sin(angle), Mathf.Cos(angle));
                        float recoil = playback.Animation.Recoil(tower.Id, selected.SlotIndex, seconds);
                        return new AssemblyMountPose(selected.SlotIndex, angle, recoil);
                    }).ToArray();
                    observedAim |= poses.Any(p => Mathf.Abs(p.Radians) > .001f);
                    observedRecoil |= poses.Any(p => p.Recoil < -.001f);
                    var expected = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose(poses));
                    Assert.That(expected.Anchors.Select(a => a.SlotIndex), Is.EqualTo(new[] { 0, 1 }));
                    foreach (var selected in tower.Blueprint.WeaponMounts)
                        Assert.That(TowerCatalog.WeaponRange(tower.Blueprint, balance, selected.WeaponId, selected.SlotIndex),
                            Is.EqualTo(legacyRanges[selected.SlotIndex]));

                    var trialPen = new MeshPen(trialMesh);
                    NotebookTrialRenderer.Draw(trialPen, playback); trialPen.Upload();
                    var battlePen = new MeshPen(battleMesh);
                    DoodleGlyphs.StructureBase(battlePen, tower);
                    DoodleGlyphs.StructureDynamic(battlePen, tower, 1, seconds, playback.Animation);
                    battlePen.Upload();
                    AssertQuads(trialMesh, expected, origin, "trial");
                    AssertQuads(battleMesh, expected, origin, "battle");
                    foreach (var selected in tower.Blueprint.WeaponMounts)
                        Assert.That(TowerCatalog.WeaponRange(tower.Blueprint, balance, selected.WeaponId, selected.SlotIndex),
                            Is.EqualTo(legacyRanges[selected.SlotIndex]));
                }
                Assert.That(observedAim, Is.True, "The real trial must exercise an aimed pose as well as its initial pose.");
                Assert.That(observedRecoil, Is.True, "The recoil-peak sample must exercise visible barrel displacement.");
                var after = AssemblyResolver.Resolve(snapshot.Recipe, catalog, balance, MaterialTier.Pencil, AssemblyVisualComposer.Fits);
                Assert.That(after.Valid, Is.True);
                Assert.That(after.Value.Weapons.Select(w => w.Range), Is.EqualTo(before.Value.Weapons.Select(w => w.Range)));
                Assert.That(after.Value.Weapons.Select(w => w.Range), Is.All.EqualTo(8));
            }
            finally { Object.DestroyImmediate(trialMesh); Object.DestroyImmediate(battleMesh); }
        }

        static void AssertQuads(Mesh mesh, AssemblyVisualComposition expected, Vector2 origin, string surface)
        {
            foreach (string spriteId in Sprites)
            {
                var actual = AtlasQuads(mesh, spriteId);
                var fragments = expected.Fragments.Where(f => f.SpriteId == spriteId).OrderBy(f => f.SlotIndex).ToArray();
                Assert.That(fragments.Select(f => f.SlotIndex), Is.EqualTo(new[] { 0, 1 }), spriteId);
                Assert.That(actual.Count, Is.EqualTo(2), surface + " must draw one " + spriteId + " for each stable mount.");
                for (int slot = 0; slot < 2; slot++)
                {
                    var f = fragments[slot];
                    var corners = new[] { f.Trim.min, new Vector2(f.Trim.xMax, f.Trim.yMin), f.Trim.max, new Vector2(f.Trim.xMin, f.Trim.yMax) };
                    for (int corner = 0; corner < 4; corner++)
                        Assert.That(Vector2.Distance(actual[slot][corner], origin + f.Transform(corners[corner])), Is.LessThan(.00001f),
                            surface + ": " + spriteId + ", mount." + slot + ", corner " + corner);
                }
            }
        }

        static List<Vector2[]> AtlasQuads(Mesh mesh, string spriteId)
        {
            Assert.That(NotebookSpriteCatalog.TryGet(spriteId, out var sprite), Is.True, spriteId);
            var uv = new List<Vector2>(); mesh.GetUVs(1, uv);
            var vertices = mesh.vertices;
            var corners = new[] {
                new Vector2(sprite.rect.xMin / sprite.texture.width, sprite.rect.yMin / sprite.texture.height),
                new Vector2(sprite.rect.xMax / sprite.texture.width, sprite.rect.yMin / sprite.texture.height),
                new Vector2(sprite.rect.xMax / sprite.texture.width, sprite.rect.yMax / sprite.texture.height),
                new Vector2(sprite.rect.xMin / sprite.texture.width, sprite.rect.yMax / sprite.texture.height)
            };
            var quads = new List<Vector2[]>();
            // Trial grid, effects and enemy meshes need not contain a multiple of four vertices.
            for (int first = 0; first + 3 < uv.Count; first++)
            {
                bool match = true;
                for (int corner = 0; corner < 4; corner++)
                    if (Vector2.Distance(uv[first + corner], corners[corner]) > .000001f) match = false;
                if (match) quads.Add(new[] { (Vector2)vertices[first], (Vector2)vertices[first + 1],
                    (Vector2)vertices[first + 2], (Vector2)vertices[first + 3] });
            }
            return quads;
        }
    }
}
