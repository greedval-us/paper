using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class SplitGunMountTests
    {
        static readonly string[] Parts = {
            "part.gun.support", "part.gun.cradle-back", "part.gun.barrels", "part.gun.collar-front"
        };
        const double PoseSeconds = 1.15;
        const float Tolerance = .00001f;

        static TowerBlueprint Standard() => new TowerBlueprint("split-gun-regression", "Split gun regression",
            ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);

        static GameSession Build(TowerBlueprint blueprint)
        {
            var balance = GameBalance.CreateDefault();
            balance.StartingInk = 10000;
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, blueprint, MaterialTier.Pencil,
                InkColor.None, out string reason), Is.True, reason);
            game.Structures[0].IsComplete = true;
            game.Structures[0].BuildRemaining = 0;
            return game;
        }

        static Vector2 Origin(GameSession game) => new Vector2(game.Structures[0].Cell.X + .5f,
            game.Structures[0].Cell.Y + .5f);

        static void Aim(GameSession game, int slot, Vector2 direction)
        {
            Vector2 target = Origin(game) + NotebookMountLayout.Resolve(game.Structures[0].Blueprint, slot).RotationCenter
                + direction * 5;
            game.Structures[0].GetMountState(slot).Aim = new Point2(target.x, target.y);
        }

        static NotebookVisualThemeAsset RecoilTheme()
        {
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            theme.Data.Recoils.Add(new NotebookWeaponRecoil {
                WeaponId = "weapon.gun", Duration = .2f, Distance = .05f,
                Keys = new List<NotebookRecoilKey> {
                    new NotebookRecoilKey { Time = 0, Value = 1 },
                    new NotebookRecoilKey { Time = .2f, Value = 0 }
                }
            });
            return theme;
        }

        static NotebookAnimationState Recoiling(int structureId)
        {
            var events = new SimulationEventBuffer();
            // Unequal action ages make a swapped or shared slot record observable.
            events.Publish(1, SimulationEventKind.WeaponActionStarted, default, default, StructureKind.Gun,
                sourceStructureId: structureId, mountSlotIndex: 0, actionId: 100, simulationSeconds: 1.10,
                weaponId: "weapon.gun");
            events.Publish(1, SimulationEventKind.WeaponActionStarted, default, default, StructureKind.Gun,
                sourceStructureId: structureId, mountSlotIndex: 1, actionId: 101, simulationSeconds: 1.00,
                weaponId: "weapon.gun");
            var animation = new NotebookAnimationState();
            foreach (var item in events) animation.AddEvent(item);
            return animation;
        }

        static Mesh Draw(GameSession game, NotebookVisualThemeAsset theme, NotebookAnimationState animation = null)
        {
            var tower = new RunReadModel(game).Structures[0];
            var mesh = new Mesh();
            var pen = new MeshPen(mesh);
            NotebookTowerComposition.DrawMounts(pen, tower.Blueprint, Origin(game), MaterialTier.Pencil,
                InkColor.None, tower, true, animation, tower.Id, PoseSeconds, theme: theme);
            pen.Upload();
            return mesh;
        }

        static List<Vector2[]> Quads(Mesh mesh, string spriteId)
        {
            Assert.That(NotebookSpriteCatalog.TryGet(spriteId, out var sprite), Is.True,
                "Missing semantic sprite " + spriteId);
            var uv = new List<Vector2>();
            mesh.GetUVs(1, uv);
            var vertices = mesh.vertices;
            var expected = new[] {
                new Vector2(sprite.rect.xMin / sprite.texture.width, sprite.rect.yMin / sprite.texture.height),
                new Vector2(sprite.rect.xMax / sprite.texture.width, sprite.rect.yMin / sprite.texture.height),
                new Vector2(sprite.rect.xMax / sprite.texture.width, sprite.rect.yMax / sprite.texture.height),
                new Vector2(sprite.rect.xMin / sprite.texture.width, sprite.rect.yMax / sprite.texture.height)
            };
            var result = new List<Vector2[]>();
            // Mount quads are emitted consecutively, including the very first four vertices.
            for (int first = 0; first + 3 < uv.Count; first += 4)
            {
                bool matches = true;
                for (int corner = 0; corner < 4; corner++)
                    if (Vector2.Distance(uv[first + corner], expected[corner]) > .000001f) matches = false;
                if (!matches) continue;
                result.Add(new[] { (Vector2)vertices[first], (Vector2)vertices[first + 1],
                    (Vector2)vertices[first + 2], (Vector2)vertices[first + 3] });
            }
            return result;
        }

        static Vector2[] Part(Mesh mesh, string spriteId, int slot)
        {
            var quads = Quads(mesh, spriteId);
            Assert.That(quads.Count, Is.EqualTo(2),
                "Each occupied standard socket must draw its own " + spriteId + " quad.");
            return quads[slot];
        }

        static void SameQuad(Vector2[] actual, Vector2[] expected, string message)
        {
            for (int corner = 0; corner < 4; corner++)
                Assert.That(Vector2.Distance(actual[corner], expected[corner]), Is.LessThan(Tolerance),
                    message + ", corner " + corner);
        }

        static Vector2 Center(Vector2[] quad) => (quad[0] + quad[2]) * .5f;

        [Test]
        public void EachStandardGunDrawsItsOwnCoreFragmentsInsideTheCompletePilot()
        {
            var game = Build(Standard());
            var theme = RecoilTheme();
            var mesh = Draw(game, theme);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(80), "Two eight-part guns and four foundation layers share one ordered batch.");
                foreach (string id in Parts)
                    for (int slot = 0; slot < 2; slot++)
                    {
                        var quad = Part(mesh, id, slot);
                        Assert.That((quad[1] - quad[0]).magnitude, Is.GreaterThan(.001f));
                        Assert.That((quad[3] - quad[0]).magnitude, Is.GreaterThan(.001f));
                    }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void SupportStaysOnItsFoundationSocketWhenBothGunsAimAndRecoil()
        {
            var game = Build(Standard());
            var theme = RecoilTheme();
            Aim(game, 0, Vector2.right); Aim(game, 1, Vector2.up);
            var resting = Draw(game, theme);
            Aim(game, 0, Vector2.down); Aim(game, 1, Vector2.left);
            var animation = Recoiling(game.Structures[0].Id);
            var moving = Draw(game, theme, animation);
            try
            {
                Assert.That(animation.Recoil(game.Structures[0].Id, 0, PoseSeconds, theme),
                    Is.EqualTo(-.0375f).Within(Tolerance));
                Assert.That(animation.Recoil(game.Structures[0].Id, 1, PoseSeconds, theme),
                    Is.EqualTo(-.0125f).Within(Tolerance));
                for (int slot = 0; slot < 2; slot++)
                    SameQuad(Part(moving, Parts[0], slot), Part(resting, Parts[0], slot),
                        "The fixed support must ignore aim and barrel recoil for slot " + slot);
                Vector2 spacing = NotebookMountLayout.Resolve(game.Structures[0].Blueprint, 1).Socket
                    - NotebookMountLayout.Resolve(game.Structures[0].Blueprint, 0).Socket;
                Assert.That(Vector2.Distance(Center(Part(resting, Parts[0], 1))
                    - Center(Part(resting, Parts[0], 0)), spacing), Is.LessThan(Tolerance),
                    "The supports must retain the spacing of their two distinct foundation sockets.");
            }
            finally
            {
                Object.DestroyImmediate(resting); Object.DestroyImmediate(moving); Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void CradleAndFrontCollarRotateWithTheirOwnGunWithoutRecoiling()
        {
            var game = Build(Standard());
            var theme = RecoilTheme();
            Aim(game, 0, Vector2.right); Aim(game, 1, Vector2.up);
            var resting = Draw(game, theme);
            var recoiling = Draw(game, theme, Recoiling(game.Structures[0].Id));
            Aim(game, 0, Vector2.down); Aim(game, 1, Vector2.left);
            var turned = Draw(game, theme);
            try
            {
                foreach (string id in new[] { Parts[1], Parts[3] })
                    for (int slot = 0; slot < 2; slot++)
                    {
                        var before = Part(resting, id, slot);
                        var after = Part(turned, id, slot);
                        SameQuad(Part(recoiling, id, slot), before,
                            id + " must not slide with the recoiling barrels in slot " + slot);
                        // Slot zero turns clockwise; slot one turns counter-clockwise.
                        foreach (int corner in new[] { 1, 3 })
                        {
                            Vector2 edge = before[corner] - before[0];
                            Vector2 expected = slot == 0 ? new Vector2(edge.y, -edge.x) : new Vector2(-edge.y, edge.x);
                            Assert.That(Vector2.Distance(after[corner] - after[0], expected), Is.LessThan(Tolerance),
                                id + " must rotate with its own slot's aim.");
                        }
                    }
            }
            finally
            {
                Object.DestroyImmediate(resting); Object.DestroyImmediate(recoiling);
                Object.DestroyImmediate(turned); Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void CradleBaseRegistrationStaysOverTheFixedSupportWhenBothGunsTurn()
        {
            var game = Build(Standard());
            var theme = RecoilTheme();
            Aim(game, 0, Vector2.right); Aim(game, 1, Vector2.right);
            var right = Draw(game, theme);
            Aim(game, 0, Vector2.left); Aim(game, 1, Vector2.left);
            var left = Draw(game, theme);
            var recoiling = Draw(game, theme, Recoiling(game.Structures[0].Id));
            try
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    var before = Part(right, Parts[1], slot);
                    // Independently measured base joint (211,34) in the 427 x 542 cradle canvas.
                    Vector2 joint = before[0] + (before[1] - before[0]) * (211f / 427)
                        + (before[3] - before[0]) * (34f / 542);
                    foreach (var pose in new[] { left, recoiling })
                    {
                        var after = Part(pose, Parts[1], slot);
                        Vector2 actual = after[0] + (after[1] - after[0]) * (211f / 427)
                            + (after[3] - after[0]) * (34f / 542);
                        Assert.That(Vector2.Distance(actual, joint), Is.LessThan(Tolerance),
                            "The cradle joint must stay seated on its fixed support when slot " + slot
                            + " turns, including while its barrels recoil.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(right); Object.DestroyImmediate(left);
                Object.DestroyImmediate(recoiling); Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void OnlyBarrelsSlideAlongEachIndependentAimAndMuzzleSharesThatDisplacement()
        {
            var game = Build(Standard());
            var theme = RecoilTheme();
            Aim(game, 0, Vector2.right); Aim(game, 1, Vector2.up);
            var animation = Recoiling(game.Structures[0].Id);
            var resting = Draw(game, theme);
            var recoiling = Draw(game, theme, animation);
            try
            {
                var tower = new RunReadModel(game).Structures[0];
                var offsets = new[] { new Vector2(-.0375f, 0), new Vector2(0, -.0125f) };
                for (int slot = 0; slot < 2; slot++)
                {
                    var before = Part(resting, Parts[2], slot);
                    var after = Part(recoiling, Parts[2], slot);
                    for (int corner = 0; corner < 4; corner++)
                        Assert.That(Vector2.Distance(after[corner] - before[corner], offsets[slot]),
                            Is.LessThan(Tolerance), "Barrel recoil must use the action and direction of slot " + slot);
                    foreach (string id in new[] { Parts[0], Parts[1], Parts[3] })
                        SameQuad(Part(recoiling, id, slot), Part(resting, id, slot), id + " must remain seated.");
                    Vector2 muzzleBefore = NotebookTowerComposition.Muzzle(tower, slot, tower.GetMountAim(slot),
                        PoseSeconds, null, theme);
                    Vector2 muzzleAfter = NotebookTowerComposition.Muzzle(tower, slot, tower.GetMountAim(slot),
                        PoseSeconds, animation, theme);
                    Assert.That(Vector2.Distance(muzzleAfter - muzzleBefore, offsets[slot]), Is.LessThan(Tolerance),
                        "Effects must follow the visible barrel's recoil in slot " + slot);
                }
            }
            finally
            {
                Object.DestroyImmediate(resting); Object.DestroyImmediate(recoiling); Object.DestroyImmediate(theme);
            }
        }

        [TestCase("chassis.heavy")]
        [TestCase("chassis.precision")]
        [TestCase("chassis.battery")]
        public void OtherFoundationsKeepTheWholeGunSprite(string chassis)
        {
            var blueprint = new TowerBlueprint("whole-gun-regression", "Whole gun regression", chassis, "weapon.gun");
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                NotebookTowerComposition.DrawMounts(pen, blueprint, Vector2.zero, MaterialTier.Pencil,
                    InkColor.None, default, false, null, 0, 0);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                Assert.That(Quads(mesh, "weapon.gun").Count, Is.EqualTo(1),
                    "The ordinary foundation and gun pilot must not change " + chassis + " artwork.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void RenderingSplitGunsPreservesTheSavedProfileAndBlueprint()
        {
            var profile = new WorkshopProfile();
            var blueprint = Standard();
            Assert.That(profile.SaveBlueprint(blueprint, out string reason), Is.True, reason);
            Assert.That(profile.AssignSlot(0, blueprint.Id, out reason), Is.True, reason);
            string savedProfile = JsonUtility.ToJson(profile.ToData());
            string savedBlueprint = JsonUtility.ToJson(new BlueprintData(blueprint));
            var game = Build(profile.GetLoadoutBlueprints()[0]);
            Aim(game, 0, Vector2.right); Aim(game, 1, Vector2.down);
            var theme = RecoilTheme();
            var mesh = Draw(game, theme, Recoiling(game.Structures[0].Id));
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(80));
                foreach (string id in Parts) Assert.That(Quads(mesh, id).Count, Is.EqualTo(2));
                Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(savedProfile),
                    "Presentation must not persist the split fragments, aim, or recoil into the profile.");
                Assert.That(JsonUtility.ToJson(new BlueprintData(blueprint)), Is.EqualTo(savedBlueprint));
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(theme); }
        }
    }
}
