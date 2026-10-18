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
            camera = root.AddComponent<Camera>(); camera.orthographic = true;
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
