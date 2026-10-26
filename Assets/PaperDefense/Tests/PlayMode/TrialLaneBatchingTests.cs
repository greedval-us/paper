using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class TrialLaneBatchingTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        // Raster bodies need far fewer vertices. Keep the fixture above the same Canvas limit.
        const int OverflowCrowdSize = 3300;
        GameObject root;
        TrialLaneSketch lane;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        void CreateLane()
        {
            root = new GameObject("Isolated trial batching canvas", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var page = new GameObject("Trial page", typeof(RectTransform));
            page.transform.SetParent(root.transform, false);
            lane = page.AddComponent<TrialLaneSketch>();
            lane.rectTransform.sizeDelta = new Vector2(640, 420);
            lane.raycastTarget = false;
        }

        static TrialReport Crowd(int count)
        {
            var enemies = new List<EnemyState>(count);
            for (int i = 0; i < count; i++)
                enemies.Add(new EnemyState { Id = i + 1, Kind = EnemyKind.Doodle,
                    Position = new Point2(10 + i % 35, 10 + i / 35), Health = 50, MaxHealth = 50,
                    DrawingTier = DrawingTier.Sketch });
            var frame = (TrialFrame)typeof(TrialFrame).GetConstructors(PrivateInstance)[0].Invoke(new object[] {
                0f, enemies, Array.Empty<StructureState>(), Array.Empty<MortarProjectile>(),
                Array.Empty<ShotEvent>(), null, null, null, 0L });
            var report = new TrialReport { DurationSeconds = 10 };
            typeof(TrialReport).GetProperty(nameof(TrialReport.Frames)).SetValue(report, new[] { frame });
            return report;
        }

        void Show(TrialReport report)
        {
            lane.SetResult(report, Time.unscaledTime, report?.DurationSeconds ?? 0);
            lane.PlaybackPaused = true;
            typeof(TrialLaneSketch).GetMethod("Update", PrivateInstance).Invoke(lane, null);
        }

        static void Populate(Graphic graphic, VertexHelper helper)
            => graphic.GetType().GetMethod("OnPopulateMesh", PrivateInstance, null,
                new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });

        List<UIVertex> UnsplitTriangles()
        {
            var reference = new MeshPen(null);
            Rect rect = lane.rectTransform.rect;
            float scale = Mathf.Max(.1f, Mathf.Min((rect.width - 16) / 42, (rect.height - 14) / 28));
            reference.MinimumLineWidth = 1f / (scale * lane.canvas.scaleFactor);
            NotebookTrialRenderer.Draw(reference, lane.Playback);
            using (var helper = new VertexHelper())
            {
                reference.CopyTo(helper, rect.center - new Vector2(29, 22) * scale, scale);
                Assert.That(helper.currentVertCount, Is.GreaterThan(65000),
                    "The fixture must exercise the real detailed renderer above the Unity UI mesh limit.");
                var result = new List<UIVertex>(); helper.GetUIVertexStream(result); return result;
            }
        }

        List<UIVertex> BoundedTriangles()
        {
            var result = new List<UIVertex>();
            var mesh = new Mesh();
            try
            {
                foreach (var graphic in lane.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.isActiveAndEnabled) continue;
                    using (var helper = new VertexHelper())
                    {
                        Populate(graphic, helper);
                        Assert.That(helper.currentVertCount, Is.LessThan(65000),
                            graphic.name + " must fit Unity's individual UI mesh limit.");
                        helper.FillMesh(mesh);
                        var batch = new List<UIVertex>(); helper.GetUIVertexStream(batch);
                        for (int i = 0; i < batch.Count; i++)
                        {
                            UIVertex vertex = batch[i];
                            vertex.position = lane.transform.InverseTransformPoint(graphic.transform.TransformPoint(vertex.position));
                            result.Add(vertex);
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(mesh); }
            return result;
        }

        static void AssertSameTriangles(List<UIVertex> expected, List<UIVertex> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), "No triangle may be dropped or repeated at batch boundaries.");
            for (int i = 0; i < expected.Count; i++)
                if ((actual[i].position - expected[i].position).sqrMagnitude > .000001f ||
                    !actual[i].color.Equals(expected[i].color) || actual[i].uv0 != expected[i].uv0)
                    Assert.Fail("Triangle corner {0} changed position, color, UV or draw order across batching.", i);
        }

        [Test]
        public void FramedRecordingFacesRightWithTheEntireRecordedSceneReflectedTogether()
        {
            CreateLane(); lane.FrameRecording = true; Show(Crowd(2));
            Rect rect = lane.rectTransform.rect, bounds = lane.ViewBounds;
            float scale = Mathf.Max(.1f, Mathf.Min((rect.width - 24) / bounds.width, (rect.height - 24) / bounds.height));
            var reference = new MeshPen(null) { MinimumLineWidth = 1f / (scale * lane.canvas.scaleFactor) };
            NotebookTrialRenderer.Draw(reference, lane.Playback, bounds);
            using (var helper = new VertexHelper())
            {
                reference.CopyTo(helper, rect.center - bounds.center * scale, scale);
                var expected = new List<UIVertex>(); helper.GetUIVertexStream(expected);
                for (int i = 0; i < expected.Count; i++)
                {
                    var vertex = expected[i];
                    vertex.position.x = rect.center.x * 2 - vertex.position.x;
                    expected[i] = vertex;
                }
                AssertSameTriangles(expected, BoundedTriangles());
            }
        }

        [Test]
        public void SmallTrialPreviewRetainsAtLeastOnePixelWideLines()
        {
            CreateLane(); lane.rectTransform.sizeDelta = new Vector2(570, 230);
            Show(Crowd(0));
            var triangles = BoundedTriangles();
            float thickness = Mathf.Abs(triangles[2].position.y - triangles[0].position.y);
            Assert.That(thickness * lane.canvas.scaleFactor, Is.GreaterThanOrEqualTo(.99f),
                "Small trial previews must not shrink authored lines below a screen pixel.");
        }

        [Test]
        public void DetailedCrowdAboveUiVertexLimitRetainsEveryTriangleInDrawOrder()
        {
            CreateLane(); Show(Crowd(OverflowCrowdSize));
            List<UIVertex> expected = UnsplitTriangles();
            AssertSameTriangles(expected, BoundedTriangles());
            Assert.That(lane.GetComponentsInChildren<Graphic>().Length, Is.GreaterThan(1));
        }

        [UnityTest]
        public IEnumerator BatchesAreReusedAfterSparseEmptyAndDisabledPlayback()
        {
            CreateLane(); TrialReport crowd = Crowd(OverflowCrowdSize); Show(crowd);
            yield return null; Canvas.ForceUpdateCanvases();
            var warmed = lane.GetComponentsInChildren<Graphic>(true);
            Assert.That(warmed.Length, Is.GreaterThan(1));
            Show(Crowd(0)); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(BoundedTriangles().Count, Is.LessThan(1000), "Old crowd batches must be cleared on a sparse frame.");
            Show(null); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(BoundedTriangles(), Is.Empty, "Removing the recording must clear all pooled geometry.");
            Show(crowd); lane.enabled = false; yield return null; Canvas.ForceUpdateCanvases();
            foreach (var graphic in lane.GetComponentsInChildren<Graphic>())
            {
                var mesh = graphic.canvasRenderer.GetMesh();
                Assert.That(mesh == null || mesh.vertexCount == 0 || !graphic.isActiveAndEnabled, Is.True,
                    "Disabling the parent Graphic must also hide its batches.");
            }
            lane.enabled = true; yield return null; Canvas.ForceUpdateCanvases();
            AssertSameTriangles(UnsplitTriangles(), BoundedTriangles());
            CollectionAssert.AreEqual(warmed, lane.GetComponentsInChildren<Graphic>(true),
                "The same pool must survive fewer, empty and re-enabled frames without child churn.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PausedBatchesFollowResizingWithoutCanvasRebuildErrors()
        {
            CreateLane(); Show(Crowd(OverflowCrowdSize));
            yield return null; Canvas.ForceUpdateCanvases();
            lane.rectTransform.sizeDelta = new Vector2(350, 610);
            lane.rectTransform.pivot = new Vector2(.2f, .75f);
            yield return null; Canvas.ForceUpdateCanvases();
            AssertSameTriangles(UnsplitTriangles(), BoundedTriangles());
            foreach (var graphic in lane.GetComponentsInChildren<Graphic>())
            {
                Assert.That(graphic.canvasRenderer.GetMesh().vertexCount, Is.LessThan(65000));
                Assert.That(graphic.raycastTarget, Is.False, "Render-only children cannot intercept the workshop controls.");
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
