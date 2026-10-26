using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PaperDefense.Tests
{
    public sealed class NotebookCameraReadabilityTests
    {
        GameObject root;
        DrawingInput drawing;
        NotebookWorldView view;
        Camera camera;
        Mouse mouse;
        InputSettings originalSettings, settings;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            originalSettings = InputSystem.settings;
            settings = Object.Instantiate(originalSettings);
            settings.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // Replacing the Input System's transient default can destroy it unless its flags are retained.
            HideFlags flags = originalSettings.hideFlags;
            try { originalSettings.hideFlags = HideFlags.None; InputSystem.settings = settings; }
            finally { originalSettings.hideFlags = flags; }
            root = new GameObject("Camera readability fixture"); root.SetActive(false);
            var run = root.AddComponent<RunController>();
            run.AutomaticSimulation = false;
            run.Initialize(allowLegacyFixtureDefaults: true);
            camera = new GameObject("Readability camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform, false); camera.orthographic = true;
            camera.orthographicSize = 10;
            camera.transform.position = new Vector3(run.Session.Balance.Width * .5f, run.Session.Balance.Height * .5f, -10);
            view = root.AddComponent<NotebookWorldView>(); view.WorldCamera = camera; view.Initialize(run.Session);
            drawing = root.AddComponent<DrawingInput>(); drawing.Initialize(run, view, camera);
            typeof(DrawingInput).GetProperty("Zoom").SetValue(drawing, 2f);
            mouse = InputSystem.AddDevice<Mouse>();
        }

        [TearDown] public void TearDown()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            Object.DestroyImmediate(root);
            InputSystem.settings = originalSettings;
            Object.DestroyImmediate(settings);
        }

        void Scroll(float delta, Vector2 screen)
        {
            Assert.That(mouse.enabled, Is.True, "Synthetic camera input must remain enabled when the editor loses focus.");
            InputState.Change(mouse, new MouseState { scroll = new Vector2(0, delta) });
            typeof(DrawingInput).GetMethod("Navigate", Private).Invoke(drawing, new object[] { mouse, screen });
        }

        [Test] public void NormalizedWheelNotchZoomsNoticeablyAndKeepsTheCursorAnchor()
        {
            var screen = camera.pixelRect.center + new Vector2(40, 20);
            var before = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            Scroll(1, screen);
            Assert.That(drawing.Zoom, Is.GreaterThan(2.3f), "One normalized notch must be useful, not a 0.15% change.");
            var after = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            Assert.That(Vector3.Distance(before, after), Is.LessThan(.001f));
            Scroll(-1, screen);
            Assert.That(drawing.Zoom, Is.EqualTo(2).Within(.001f));
        }

        [Test] public void CameraScaleRefreshesStaticGeometryButPanningDoesNot()
        {
            typeof(NotebookWorldView).GetMethod("LateUpdate", Private).Invoke(view, null);
            int before = view.StaticGeometryRefreshCount;
            camera.orthographicSize *= .5f;
            typeof(NotebookWorldView).GetMethod("LateUpdate", Private).Invoke(view, null);
            Assert.That(view.StaticGeometryRefreshCount, Is.GreaterThan(before));
            int after = view.StaticGeometryRefreshCount;
            camera.transform.position += Vector3.right;
            typeof(NotebookWorldView).GetMethod("LateUpdate", Private).Invoke(view, null);
            Assert.That(view.StaticGeometryRefreshCount, Is.EqualTo(after));
        }

        [TestCase("pan")]
        [TestCase("zoom")]
        [TestCase("aspect")]
        public void ProductionMapSceneryRemainsAnchoredDuringCameraNavigation(string navigation)
        {
            // This is the actual terrain/placement map. The old open-field fixture
            // never exercised the camera-framed scenery branch.
            var session = new GameSession(NotebookTowerDefensePreset.Create(), starterDefenses: false);
            var read = new RunReadModel(session);
            var map = read.Battlefield;
            Assert.That(map, Is.Not.Null);
            Assert.That(NotebookSpriteCatalog.Available, Is.True, "Use the real imported terrain illustrations.");
            camera.aspect = 16f / 9f; camera.orthographicSize = 20;
            camera.transform.position = new Vector3(48.5f, 32.5f, -10);
            view.ModernEnemyPresentation = true; view.Bind(read);
            typeof(NotebookWorldView).GetMethod("LateUpdate", Private).Invoke(view, null);

            var before = PaperAtlas();
            var pineBefore = ProjectMapPine(before.Vertices);
            var runId = read.RunId;
            long tick = read.TickIndex;
            int ink = read.Ink, gold = read.GoldEarned;
            float elapsed = read.Elapsed, health = read.BaseHealth;
            int topology = read.TopologyVersion, structures = read.Structures.Count;
            var props = new List<TerrainPropView>(map.Props);
            var placement = new bool[read.Balance.Width * read.Balance.Height];
            for (int y = 0; y < read.Balance.Height; y++)
                for (int x = 0; x < read.Balance.Width; x++)
                    placement[y * read.Balance.Width + x] = map.CanPlace(new Cell(x, y), StructureKind.Wall);
            var freeCell = new[] { new Cell(40, 32) };
            var terrainCell = new[] { new Cell(7, 32) };
            var freeQuote = read.QuoteBuild(freeCell, StructureKind.Wall, MaterialTier.Pencil);
            var terrainQuote = read.QuoteBuild(terrainCell, StructureKind.Wall, MaterialTier.Pencil);
            Assert.That(freeQuote.Valid, Is.True, "A camera move must retain this usable construction cell.");
            Assert.That(terrainQuote.Valid, Is.False, "The authored pine occupies reserved terrain.");

            if (navigation == "pan") camera.transform.position += new Vector3(3, -2, 0);
            // The production map is 64 units high, so 20 -> 24 crosses the old
            // 22.4-unit scenery threshold and must not add/remove illustrations.
            else if (navigation == "zoom") camera.orthographicSize = 24;
            else camera.aspect = 4f / 3f;
            typeof(NotebookWorldView).GetMethod("LateUpdate", Private).Invoke(view, null);
            var after = PaperAtlas();

            Assert.That(read.RunId, Is.EqualTo(runId));
            Assert.That(read.TickIndex, Is.EqualTo(tick)); Assert.That(read.Elapsed, Is.EqualTo(elapsed));
            Assert.That(read.Ink, Is.EqualTo(ink)); Assert.That(read.GoldEarned, Is.EqualTo(gold));
            Assert.That(read.BaseHealth, Is.EqualTo(health)); Assert.That(read.Structures.Count, Is.EqualTo(structures));
            Assert.That(read.TopologyVersion, Is.EqualTo(topology)); Assert.That(read.Battlefield, Is.SameAs(map));
            CollectionAssert.AreEqual(props, map.Props, "Camera framing cannot rewrite terrain footprints.");
            for (int y = 0; y < read.Balance.Height; y++)
                for (int x = 0; x < read.Balance.Width; x++)
                    Assert.That(map.CanPlace(new Cell(x, y), StructureKind.Wall),
                        Is.EqualTo(placement[y * read.Balance.Width + x]), $"Placement at {x},{y} changed with the camera.");
            var afterFree = read.QuoteBuild(freeCell, StructureKind.Wall, MaterialTier.Pencil);
            var afterTerrain = read.QuoteBuild(terrainCell, StructureKind.Wall, MaterialTier.Pencil);
            Assert.That(afterFree.Valid, Is.EqualTo(freeQuote.Valid)); Assert.That(afterFree.Cost, Is.EqualTo(freeQuote.Cost));
            Assert.That(afterTerrain.Valid, Is.EqualTo(terrainQuote.Valid)); Assert.That(afterTerrain.Reason, Is.EqualTo(terrainQuote.Reason));

            AssertPaperAtlasUnchanged(before, after, navigation);
            var pineAfter = ProjectMapPine(after.Vertices);
            if (navigation == "pan")
            {
                var screenDelta = new Vector2(3 * camera.pixelWidth / (40f * camera.aspect),
                    -2 * camera.pixelHeight / 40f);
                var expectedCenter = pineBefore.center - screenDelta;
                Assert.That(Vector2.Distance(pineAfter.center, expectedCenter), Is.LessThan(.01f),
                    "The terrain illustration must move across the screen with its map cell.");
                Assert.That(Vector2.Distance(pineAfter.size, pineBefore.size), Is.LessThan(.01f));
            }
            else if (navigation == "zoom")
            {
                Assert.That(pineAfter.height, Is.EqualTo(pineBefore.height * 20f / 24f).Within(.01f));
                camera.orthographicSize = 10;
                typeof(NotebookWorldView).GetMethod("LateUpdate", Private).Invoke(view, null);
                var close = PaperAtlas();
                AssertPaperAtlasUnchanged(before, close, "close zoom");
                Assert.That(ProjectMapPine(close.Vertices).height, Is.EqualTo(pineBefore.height * 2).Within(.01f),
                    "Zoom enlarges the same tree instead of resizing it to a fixed screen height.");
            }
        }

        (Vector3[] Vertices, Vector2[] Uvs) PaperAtlas()
        {
            var filter = root.transform.Find("Graph paper").GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;
            var source = mesh.vertices;
            var sourceUv = new List<Vector2>(); mesh.GetUVs(1, sourceUv);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>();
            for (int index = 0; index < source.Length; index++)
            {
                // Grid strokes have a white atlas coordinate and may legitimately
                // change their widths or disappear at overview scale.
                if (sourceUv[index] == NotebookSpriteCatalog.WhiteUV) continue;
                vertices.Add(filter.transform.TransformPoint(source[index])); uv.Add(sourceUv[index]);
            }
            Assert.That(vertices.Count, Is.GreaterThan(4), "Actual terrain artwork must remain present.");
            Assert.That(vertices.Count % 4, Is.Zero, "Terrain atlas illustrations are complete quads.");
            return (vertices.ToArray(), uv.ToArray());
        }

        static void AssertPaperAtlasUnchanged((Vector3[] Vertices, Vector2[] Uvs) before,
            (Vector3[] Vertices, Vector2[] Uvs) after, string navigation)
        {
            Assert.That(after.Vertices.Length, Is.EqualTo(before.Vertices.Length),
                $"Camera {navigation} must not make terrain illustrations appear or disappear.");
            CollectionAssert.AreEqual(before.Uvs, after.Uvs, $"Camera {navigation} must retain the same terrain illustrations.");
            for (int index = 0; index < before.Vertices.Length; index++)
                Assert.That(Vector3.Distance(after.Vertices[index], before.Vertices[index]), Is.LessThan(.0001f),
                    $"Terrain vertex {index} moved in world coordinates during camera {navigation}.");
        }

        Rect ProjectMapPine(Vector3[] vertices)
        {
            // This authored two-by-three-cell pine occupies (7,32), with its
            // illustration centred at (8,33.5), independently of the camera.
            for (int index = 0; index < vertices.Length; index += 4)
            {
                Vector3 center = (vertices[index] + vertices[index + 2]) * .5f;
                if (Vector3.Distance(center, new Vector3(8, 33.5f, 0)) > .001f) continue;
                Vector2 low = camera.WorldToScreenPoint(vertices[index]);
                Vector2 high = low;
                for (int corner = 1; corner < 4; corner++)
                {
                    Vector2 point = camera.WorldToScreenPoint(vertices[index + corner]);
                    low = Vector2.Min(low, point); high = Vector2.Max(high, point);
                }
                return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            }
            Assert.Fail("The actual authored pine must remain rendered at its reserved map footprint.");
            return default;
        }

        [Test] public void FractionalScrollAccumulatesAndZoomStopsAtBothLimits()
        {
            var screen = camera.pixelRect.center;
            for (int i = 0; i < 4; i++) Scroll(.25f, screen);
            Assert.That(drawing.Zoom, Is.EqualTo(2 * NotebookViewSettingsAsset.Default.ZoomPerNotch).Within(.001f));
            Scroll(100, screen);
            Assert.That(drawing.Zoom, Is.EqualTo(NotebookViewSettingsAsset.Default.MaximumZoom));
            Scroll(-100, screen);
            Assert.That(drawing.Zoom, Is.EqualTo(1));
            Assert.That(float.IsNaN(camera.orthographicSize), Is.False);
        }

        [Test] public void PlatformRangesNormalizeOnlyLegacyWindowsInput()
        {
            Assert.That(DrawingInput.ScrollNotches(120, InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange, RuntimePlatform.WindowsPlayer), Is.EqualTo(1));
            Assert.That(DrawingInput.ScrollNotches(.25f, InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms, RuntimePlatform.WindowsPlayer), Is.EqualTo(.25f));
            Assert.That(DrawingInput.ScrollNotches(1, InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange, RuntimePlatform.OSXPlayer), Is.EqualTo(1));
        }

        [Test] public void ScrollOutsideTheMapDoesNotZoomAndFocusClampsToMapEdges()
        {
            Scroll(1, new Vector2(-100, -100));
            Assert.That(drawing.Zoom, Is.EqualTo(2));
            drawing.Focus(new Cell(0, 0));
            Assert.That(drawing.CameraCenter.x, Is.GreaterThanOrEqualTo(camera.orthographicSize * camera.aspect));
            Assert.That(drawing.CameraCenter.y, Is.GreaterThanOrEqualTo(camera.orthographicSize));
        }

        [Test] public void OverviewHidesFineGridAndCloseZoomRestoresIt()
        {
            var mesh = root.transform.Find("Graph paper").GetComponent<MeshFilter>().sharedMesh;
            camera.orthographicSize = camera.pixelHeight / 14f; // Seven pixels per cell.
            typeof(NotebookWorldView).GetMethod("LateUpdate", Private).Invoke(view, null);
            int overviewVertices = mesh.vertexCount;
            camera.orthographicSize = camera.pixelHeight / 60f;
            typeof(NotebookWorldView).GetMethod("LateUpdate", Private).Invoke(view, null);
            Assert.That(mesh.vertexCount, Is.GreaterThan(overviewVertices + 2000), "Microtexture must only exist at readable scales.");
        }

        [Test] public void ResourceAndAuthoredStrokesKeepScreenWidthWithoutChangingSourceGeometry()
        {
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh) { WorldUnitsPerPixel = .2f, MinimumStrokePixels = 1.25f };
                var shape = new NotebookArtShape();
                shape.Marks.Add(new NotebookArtMark { Points = new[] { Vector2.zero, Vector2.right }, Width = .02f });
                NotebookArtRenderer.Draw(pen, shape, Vector2.zero);
                pen.Upload();
                var uv = new System.Collections.Generic.List<Vector3>(); mesh.GetUVs(0, uv);
                Assert.That(uv[0].x * 2 / pen.WorldUnitsPerPixel, Is.EqualTo(1.25f).Within(.001f));
                Assert.That(uv[0].z, Is.EqualTo(1), "Stroke must opt in to analytic coverage.");
                int cachedBuilds = NotebookArtRenderer.GeometryBuildCount;
                NotebookArtRenderer.Draw(pen, shape, Vector2.one);
                Assert.That(NotebookArtRenderer.GeometryBuildCount, Is.EqualTo(cachedBuilds));

                var drawingData = new DrawingSnapshot(new[] { new DrawingPart(DrawingBindings.Base,
                    new[] { new DrawingStroke(new[] { new DrawingPoint(0, 0), new DrawingPoint(.5f, 0) }) }) });
                string hash = drawingData.GeometryHash;
                var part = AuthoredGeometryCache.For(drawingData).GetPart(DrawingBindings.Base);
                Vector2 source = part.Vertices[0];
                pen.Clear(); pen.Append(part, new Vector2(3, 4), Mathf.PI * .5f, Color.blue); pen.Upload();
                Assert.That((mesh.vertices[0] + mesh.vertices[3]) * .5f, Is.EqualTo(new Vector3(3, 4, 0)));
                Assert.That(Vector3.Distance((mesh.vertices[1] + mesh.vertices[2]) * .5f, new Vector3(3, 4.5f, 0)), Is.LessThan(.001f));
                Assert.That(part.Vertices[0], Is.EqualTo(source));
                Assert.That(drawingData.GeometryHash, Is.EqualTo(hash));
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
