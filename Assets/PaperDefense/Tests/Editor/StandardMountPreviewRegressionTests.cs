using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class StandardMountPreviewRegressionTests
    {
        static readonly Vector2[] NativeTriangle = {
            new Vector2(.5f, .25f), new Vector2(.75f, .25f), new Vector2(.5f, .625f)
        };

        static TowerBlueprint Standard(bool armored = false)
            => new TowerBlueprint("standard-preview-regression", "Standard preview", ChassisKind.Standard,
                StructureKind.Gun, modules: armored ? TowerModules.Armor : TowerModules.None);

        static BlueprintSketch Sketch(out GameObject root, Vector2 size)
        {
            root = new GameObject("Standard mount preview regression", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(BlueprintSketch));
            var sketch = root.GetComponent<BlueprintSketch>();
            sketch.rectTransform.sizeDelta = size;
            sketch.ShowReference = false;
            return sketch;
        }

        static List<UIVertex> Populate(BlueprintSketch sketch)
        {
            using (var helper = new VertexHelper())
            {
                // Graphic also has an OnPopulateMesh overload taking Mesh.
                var populate = typeof(BlueprintSketch).GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(VertexHelper) }, null);
                Assert.That(populate, Is.Not.Null);
                populate.Invoke(sketch, new object[] { helper });
                var vertices = new List<UIVertex>();
                helper.GetUIVertexStream(vertices);
                Assert.That(vertices, Is.Not.Empty);
                return vertices;
            }
        }

        static Rect Bounds(List<Vector2> points)
        {
            Assert.That(points, Is.Not.Empty);
            Vector2 low = points[0], high = low;
            foreach (var point in points) { low = Vector2.Min(low, point); high = Vector2.Max(high, point); }
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        static Vector2 FoundationOrigin(List<UIVertex> vertices, string chassis)
        {
            Assert.That(NotebookSpriteCatalog.TryGet(chassis, out var sprite), Is.True);
            Assert.That(NotebookSpriteCatalog.TryGetBounds(chassis, out var local), Is.True);
            var points = new List<Vector2>();
            foreach (var vertex in vertices)
            {
                var pixel = new Vector2(vertex.uv0.x * sprite.texture.width, vertex.uv0.y * sprite.texture.height);
                if (pixel.x >= sprite.rect.xMin - .001f && pixel.x <= sprite.rect.xMax + .001f
                    && pixel.y >= sprite.rect.yMin - .001f && pixel.y <= sprite.rect.yMax + .001f)
                    points.Add(vertex.position);
            }
            // Recover the actual foundation transform from its atlas quad, rather than assuming
            // that either the viewport center or the assembly's bounding-box center is its origin.
            var displayed = Bounds(points);
            float scale = displayed.width / local.width;
            Assert.That(scale, Is.GreaterThan(0));
            Assert.That(displayed.height / local.height, Is.EqualTo(scale).Within(.001f));
            return displayed.min - local.min * scale;
        }

        [Test]
        public void ReinforcementSectorsStayCenteredOnTheRenderedFoundationAfterFit()
        {
            var sketch = Sketch(out var root, new Vector2(155, 165));
            try
            {
                sketch.SetBlueprint(Standard(true).WithReinforcement(ArmorDistribution.Uniform()));
                var vertices = Populate(sketch);
                Vector2 foundation = FoundationOrigin(vertices, "chassis.standard");
                var sectors = new List<Vector2>();
                Color32 sectorColor = NotebookPalette.Navy;
                foreach (var vertex in vertices)
                    if (vertex.color.Equals(sectorColor)
                        && ((Vector2)vertex.uv0 == Vector2.zero || (Vector2)vertex.uv0 == NotebookSpriteCatalog.WhiteUV))
                        sectors.Add(vertex.position);
                Vector2 sectorCenter = Bounds(sectors).center;
                Assert.That(Vector2.Distance(sectorCenter, foundation), Is.LessThan(.01f),
                    "Armor sectors and the facing arrow must follow the foundation origin after the whole assembly is fitted.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static NotebookAnimationState Recoiling(int structureId)
        {
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.WeaponActionStarted, default, new Point2(-10, 0), StructureKind.Gun,
                sourceStructureId: structureId, mountSlotIndex: 0, actionId: 1, simulationSeconds: 1, weaponId: "weapon.gun");
            var animation = new NotebookAnimationState();
            animation.AddEvent(events[0]);
            return animation;
        }

        [TestCase(155f, 165f, false)]
        [TestCase(155f, 165f, true)]
        [TestCase(100f, 240f, false)]
        [TestCase(100f, 240f, true)]
        public void AimedPreviewFitsAfterResizeIncludingItsRecoil(float width, float height, bool recoiling)
        {
            var sketch = Sketch(out var root, new Vector2(300, 300));
            try
            {
                sketch.ShowReinforcement = false;
                sketch.SetBlueprint(Standard());
                var animation = recoiling ? Recoiling(1) : new NotebookAnimationState();
                // Sample a nonzero late recoil: fitting cannot rely on the backward motion hiding
                // the excess width of the left-facing barrel at the recoil peak.
                const double seconds = 1.14;
                if (recoiling)
                    Assert.That(animation.Recoil(1, 0, seconds), Is.LessThan(0));
                sketch.SetPreviewPose(animation, seconds, _ => new Point2(-10, 0));
                Populate(sketch);
                sketch.rectTransform.sizeDelta = new Vector2(width, height);
                var vertices = Populate(sketch);
                Rect available = sketch.rectTransform.rect;
                foreach (var vertex in vertices)
                {
                    Assert.That(vertex.position.x, Is.InRange(available.xMin - .01f, available.xMax + .01f),
                        "Aiming and resizing must fit the rendered pose, including the weapon's entire atlas frame.");
                    Assert.That(vertex.position.y, Is.InRange(available.yMin - .01f, available.yMax + .01f),
                        "Aiming and resizing must fit the rendered pose, including its recoil.");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        static NotebookVisualThemeAsset NativeTheme()
        {
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            theme.Data.Shapes.Add(new NotebookArtShape {
                Id = "weapon.gun", Marks = new List<NotebookArtMark> {
                    new NotebookArtMark { Name = "Explicit native weapon", Primitive = NotebookArtPrimitive.Polygon,
                        Channel = NotebookInkChannel.Navy, Points = (Vector2[])NativeTriangle.Clone() }
                }
            });
            theme.Data.Recoils.Add(new NotebookWeaponRecoil {
                WeaponId = "weapon.gun", Muzzle = new Vector2(.125f, .875f), Duration = .2f, Distance = .1f,
                Keys = new List<NotebookRecoilKey> {
                    new NotebookRecoilKey { Time = 0, Value = 1 },
                    new NotebookRecoilKey { Time = .2f, Value = 0 }
                }
            });
            return theme;
        }

        [Test]
        public void ExplicitNativeWeaponKeepsItsGeometryAimAndRecoilInsteadOfTheAtlasFrame()
        {
            var theme = NativeTheme();
            var mesh = new Mesh();
            try
            {
                var blueprint = Standard();
                var pen = new MeshPen(mesh);
                var origin = new Vector2(10, 20);
                var pivot = DrawingTemplateCatalog.Pivot(blueprint, DrawingBindings.Mount0);
                var aim = new Point2(20, origin.y + pivot.Y);
                NotebookTowerComposition.DrawMounts(pen, blueprint, origin, MaterialTier.Ink, InkColor.None,
                    default, false, Recoiling(1), 1, 1.1, theme: theme, overrideAim: _ => aim);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.EqualTo(3),
                    "An explicit native triangle in the assigned theme must not become a four-vertex atlas weapon.");
                var vertices = mesh.vertices;
                var uv = new List<Vector2>(); mesh.GetUVs(1, uv);
                for (int i = 0; i < NativeTriangle.Length; i++)
                {
                    // The native theme uses its existing local units and +Y forward, not pixel-canvas units.
                    Vector2 expected = origin + new Vector2(pivot.X - .05f, pivot.Y)
                        + new Vector2(NativeTriangle[i].y, -NativeTriangle[i].x);
                    Assert.That(Vector2.Distance(vertices[i], expected), Is.LessThan(.00001f), "native vertex " + i);
                    Assert.That(uv[i], Is.EqualTo(NotebookSpriteCatalog.WhiteUV));
                }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void ExplicitNativeWeaponKeepsItsThemeMuzzleAimAndRecoil()
        {
            var theme = NativeTheme();
            try
            {
                var balance = GameBalance.CreateDefault(); balance.StartingInk = 10000;
                var game = new GameSession(balance, starterDefenses: false);
                Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, Standard(), MaterialTier.Pencil,
                    InkColor.None, out string reason), Is.True, reason);
                var tower = new RunReadModel(game).Structures[0];
                Vector2 muzzle = NotebookTowerComposition.Muzzle(tower, 0, new Point2(20, 10.54f), 1.1,
                    Recoiling(tower.Id), theme);
                // Existing native contract: mount (10.5,10.54), forward .875-.05, side -.125.
                Assert.That(muzzle.x, Is.EqualTo(11.325f).Within(.00001f));
                Assert.That(muzzle.y, Is.EqualTo(10.415f).Within(.00001f));
            }
            finally { Object.DestroyImmediate(theme); }
        }
    }
}
