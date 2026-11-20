#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AssemblyTrialUiTests
    {
        AssemblyCommandFixture fixture;
        GameObject canvas, events;
        CompositeWorkshopPanel panel;
        [Serializable] sealed class FitEnvelope { public AssemblyFitCatalogInput fits; }

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(gold: 94, xp: 390);
            canvas = new GameObject("Isolated assembly trial canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
            events = new GameObject("Isolated assembly trial events", typeof(EventSystem));
            panel = new CompositeWorkshopPanel(canvas.transform, fixture.Run, null, null, sequentialFlow: true);
            panel.Reflow(1280, 720);
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(canvas); Object.DestroyImmediate(events); fixture.Dispose();
        }

        Button Button(string name) => panel.Root.GetComponentsInChildren<Button>(true).SingleOrDefault(button => button.name == name);
        void Click(string name)
        {
            var button = Button(name);
            Assert.That(button, Is.Not.Null, name + " must be reachable.");
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Canvas.ForceUpdateCanvases();
        }
        Text Text(string name) => panel.Root.GetComponentsInChildren<Text>(true).SingleOrDefault(text => text.name == name);
        Text[] MeasuredCells(bool comparison = false) => panel.Root.GetComponentsInChildren<Text>()
            .Where(text => text.name.StartsWith(comparison ? "Composite trial measured comparison row " : "Composite trial measured row ") && text.name.EndsWith(" value")).ToArray();
        TrialLaneSketch Lane => panel.Root.GetComponentInChildren<TrialLaneSketch>();
        object Report
        {
            get
            {
                var property = typeof(CompositeWorkshopPanel).GetProperty("LastAssemblyTrial", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(property, Is.Not.Null, "Shared reports must be available for towers and support.");
                return property.GetValue(panel);
            }
        }
        static object Value(object report, string name)
        {
            Assert.That(report, Is.Not.Null);
            var property = report.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(report);
        }
        AlbumEntry Support(AssemblyStructureFamily family) => fixture.Catalog.Starters.Single(entry =>
            entry.Kind == AlbumEntryKind.Structure && entry.Snapshot.Recipe.Structure.Family == family);
        void Open(AlbumEntry entry) { panel.OpenEntry(entry); Click("Composite trial"); }
        static string Json(AlbumEntry entry) => JsonUtility.ToJson(new AlbumEntryData(entry));

        [Test] public void ComparisonUsesAnotherOwnedPageWithTheSameScenarioBudgetAndSeedWithoutSaving()
        {
            string before = fixture.State(); int writes = fixture.Store.SaveCalls;
            Open(fixture.Gun());
            var property = typeof(CompositeWorkshopPanel).GetProperty("LastAssemblyTrialComparison");
            Assert.That(property, Is.Not.Null, "The comparison column needs a real independent shared report.");
            Assert.That(property.GetValue(panel), Is.Null, "Opening the comparison cannot invent measurements.");
            Click("Composite trial run");
            var comparison = property.GetValue(panel);
            Assert.That(comparison, Is.Not.Null);
            Assert.That(Value(comparison, "EntryId"), Is.Not.EqualTo(Value(Report, "EntryId")));
            foreach (string condition in new[] { "Scenario", "Budget", "Seed" })
                Assert.That(Value(comparison, condition), Is.EqualTo(Value(Report, condition)), condition);
            Assert.That(panel.Root.GetComponentsInChildren<TrialLaneSketch>().Count(), Is.EqualTo(2));
            Assert.That(Text("Composite trial measured row 0 label").text, Is.EqualTo("Урон"));
            Assert.That(MeasuredCells(true)[0].text, Is.EqualTo(panel.LastAssemblyTrialComparison.Damage.Value.ToString("0.##")));
            Assert.That(fixture.State(), Is.EqualTo(before));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [TestCase(true)] [TestCase(false)]
        public void BlotsReportKeepsAllSixMeasurementRowsVisibleAt720p(bool comparing)
        {
            Open(fixture.Gun()); Click("Composite trial scenario Blots");
            if (!comparing)
                for (int i = 0; i < 20 && Text("Composite trial comparison entry name") != null; i++)
                    Click("Composite trial compare");
            Assert.That(Text("Composite trial comparison entry name") != null, Is.EqualTo(comparing));
            Click("Composite trial run");
            var labels = panel.Root.GetComponentsInChildren<Text>().Where(value => value.name.StartsWith("Composite trial measured row ") && value.name.EndsWith(" label")).ToArray();
            Assert.That(labels.Length, Is.EqualTo(6));
            Assert.That(MeasuredCells().Length, Is.EqualTo(6));
            Assert.That(MeasuredCells(true).Length, Is.EqualTo(comparing ? 6 : 0));
            Assert.That(labels.Last().text, Does.Contain("Длительность"));
            Assert.That(MeasuredCells().Last().text, Does.Contain(panel.LastAssemblyTrial.DurationSeconds.ToString("0.##")));
            if (comparing) Assert.That(MeasuredCells(true).Last().text, Does.Contain(panel.LastAssemblyTrialComparison.DurationSeconds.ToString("0.##")));
            foreach (var label in labels.Concat(MeasuredCells()).Concat(MeasuredCells(true)))
            {
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.EqualTo(label.text.Length),
                    label.name + ": the report must render its final measurement, including duration.");
                var table = (RectTransform)label.transform.parent;
                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector2 local = table.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(table.rect.xMin - .01f, table.rect.xMax + .01f));
                    Assert.That(local.y, Is.InRange(table.rect.yMin - .01f, table.rect.yMax + .01f));
                }
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void CompactTrialReportShowsRealMeasurementsWithoutMovingItsActions(bool comparing)
        {
            panel.Reflow(800, 600); Open(fixture.Gun()); Click("Composite trial scenario Blots");
            if (!comparing)
                for (int i = 0; i < 20 && Text("Composite trial comparison entry name") != null; i++)
                    Click("Composite trial compare");
            Assert.That(Text("Composite trial comparison entry name") != null, Is.EqualTo(comparing));
            var scroll = panel.Root.GetComponentsInChildren<ScrollRect>()
                .SingleOrDefault(item => item.name == "Composite trial report scroll");
            Assert.That(scroll, Is.Not.Null, "Compact reports need a bounded scroll above the pinned actions.");
            Assert.That(scroll.vertical, Is.True); Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            var actions = new[] { "Composite trial compare", "Composite trial run", "Composite trial pause",
                "Composite trial restart", "Composite trial header return" }.Select(Button).ToArray();
            var actionBounds = actions.Select(button => BattleHudThemeEvidence.Bounds(panel.Root, (RectTransform)button.transform)).ToArray();
            var viewportBounds = BattleHudThemeEvidence.Bounds(panel.Root, scroll.viewport);
            foreach (var button in actions)
            {
                Assert.That(button.transform.IsChildOf(scroll.content), Is.False, button.name);
                Assert.That(viewportBounds.Overlaps(BattleHudThemeEvidence.Bounds(panel.Root, (RectTransform)button.transform)), Is.False, button.name);
            }
            Click("Composite trial run");
            scroll = panel.Root.GetComponentsInChildren<ScrollRect>().Single(item => item.name == "Composite trial report scroll");
            Assert.That(panel.LastAssemblyTrial, Is.Not.Null);
            var cells = MeasuredCells(); Assert.That(cells.Length, Is.EqualTo(6));
            foreach (var cell in cells.Concat(MeasuredCells(true)))
            {
                Assert.That(cell.transform.IsChildOf(scroll.content), Is.True);
                Assert.That(cell.rectTransform.rect.height, Is.GreaterThanOrEqualTo(28));
                Assert.That(cell.fontSize, Is.GreaterThanOrEqualTo(14));
                Contains(scroll.viewport.rect, BattleHudThemeEvidence.Bounds(scroll.viewport, cell.rectTransform), cell.name);
                Assert.That(cell.cachedTextGenerator.characterCountVisible, Is.EqualTo(cell.text.Length), cell.name);
            }
            Assert.That(cells.Last().text, Does.Contain(panel.LastAssemblyTrial.DurationSeconds.ToString("0.##")));
            Contains(scroll.viewport.rect, BattleHudThemeEvidence.Bounds(scroll.viewport,
                Text("Composite trial quote row 0 label").rectTransform), "The compact report keeps its cost and measurements visible together.");
            scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
            Contains(scroll.viewport.rect, BattleHudThemeEvidence.Bounds(scroll.viewport,
                Text("Composite trial quote row 0 label").rectTransform), "The quote remains available above the measurements.");
            scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            Contains(scroll.viewport.rect, BattleHudThemeEvidence.Bounds(scroll.viewport, cells.Last().rectTransform), "The last measurement remains reachable.");
            for (int i = 0; i < actions.Length; i++)
                Assert.That(BattleHudThemeEvidence.Bounds(panel.Root, (RectTransform)actions[i].transform), Is.EqualTo(actionBounds[i]), actions[i].name);
        }

        [Test] public void FocusedTrialOmitsWholeRemoteCopyInsteadOfShowingItsClippedEdge()
        {
            var entry = fixture.Catalog.Starters.First(value => value.Kind == AlbumEntryKind.Tower && value.Snapshot.Recipe.Tower.Mounts.Count == 2);
            Open(entry); Click("Composite trial run");
            var lane = Lane; lane.PlaybackPaused = true;
            var frame = lane.Playback.Report.Frames[lane.Playback.FrameIndex];
            var actualPen = new MeshPen(null);
            NotebookTrialRenderer.Draw(actualPen, lane.Playback, lane.ViewBounds);
            var actual = new System.Collections.Generic.List<UIVertex>();
            using (var mesh = new VertexHelper())
            { actualPen.CopyTo(mesh, Vector2.zero, 1); mesh.GetUIVertexStream(actual); }
            int partialCopies = 0;
            foreach (var tower in frame.Towers.Where(value => value.Health > 0 && value.ResolvedEntry?.Snapshot != null))
            {
                var bounds = NotebookAssemblyView.Bounds(Center(tower), tower.ResolvedEntry.Snapshot, NotebookMountLayout.BodyRadians(tower));
                var view = lane.ViewBounds;
                if (!view.Overlaps(bounds) || bounds.xMin >= view.xMin && bounds.xMax <= view.xMax && bounds.yMin >= view.yMin && bounds.yMax <= view.yMax) continue;
                partialCopies++;
                var body = new MeshPen(null);
                DoodleGlyphs.StructureBase(body, tower, NotebookVisualThemeAsset.Default);
                DoodleGlyphs.StructureDynamic(body, tower, 1, (float)lane.Playback.Seconds, lane.Playback.Animation, NotebookVisualThemeAsset.Default);
                var bodyVertices = new System.Collections.Generic.List<UIVertex>();
                using (var mesh = new VertexHelper())
                { body.CopyTo(mesh, Vector2.zero, 1); mesh.GetUIVertexStream(bodyVertices); }
                Assert.That(bodyVertices, Is.Not.Empty);
                var sample = bodyVertices[0];
                Assert.That(actual.Any(value => (value.position - sample.position).sqrMagnitude < .000001f && value.uv0 == sample.uv0 && value.color.Equals(sample.color)), Is.False,
                    "A remote copy outside the combat camera must not leave half a foundation on its edge; the overview retains every real copy.");
            }
            Assert.That(partialCopies, Is.GreaterThan(0), "This actual double-gun report must exercise a copy across the focus boundary.");
        }

        [Test] public void OpeningTrialDoesNotInventMeasuredResultsOrStartSimulation()
        {
            Open(fixture.Gun());
            Assert.That(panel.LastTrial, Is.Null, "Opening the field is distinct from running it.");
            Assert.That(Report, Is.Null);
            Assert.That(Lane, Is.Not.Null);
            Assert.That(Lane.HasRecording, Is.False);
            Assert.That(Button("Composite trial run").IsInteractable(), Is.True);
            Assert.That(Button("Composite trial pause").IsInteractable(), Is.False);
            Assert.That(Text("Composite trial measured hint").text, Does.Not.Match(@"[0-9]"));
            Assert.That(MeasuredCells(), Is.Empty, "Unstarted trials cannot publish measured cells.");
        }

        static Vector2 Center(StructureView structure) => new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f);
        static Vector2 Point(Point2 point) => new Vector2(point.X, point.Y);
        static Rect Include(Rect bounds, Vector2 point) => Rect.MinMaxRect(Mathf.Min(bounds.xMin, point.x), Mathf.Min(bounds.yMin, point.y),
            Mathf.Max(bounds.xMax, point.x), Mathf.Max(bounds.yMax, point.y));
        static void Contains(Rect outer, Rect inner, string reason)
        {
            Assert.That(outer.xMin, Is.LessThanOrEqualTo(inner.xMin + .001f), reason + " left");
            Assert.That(outer.yMin, Is.LessThanOrEqualTo(inner.yMin + .001f), reason + " bottom");
            Assert.That(outer.xMax, Is.GreaterThanOrEqualTo(inner.xMax - .001f), reason + " right");
            Assert.That(outer.yMax, Is.GreaterThanOrEqualTo(inner.yMax - .001f), reason + " top");
        }
        static void TightEngagement(Rect focus, Rect engagement)
        {
            Contains(focus, engagement, "The real engagement cannot be cropped or moved to fit the camera.");
            Assert.That(focus.width, Is.LessThanOrEqualTo(engagement.width + 3.01f), "Remote copies and unrelated later actions must not determine the close-up width.");
            Assert.That(focus.height, Is.LessThanOrEqualTo(engagement.height + 3.01f), "Remote copies and unrelated later actions must not determine the close-up height.");
        }
        static Rect ActualTargetVisualBounds(TrialReport recording, TrialFrame actionFrame, SimulationEvent action, StructureView source)
        {
            var samples = recording.Frames.Where(frame => frame.Seconds <= actionFrame.Seconds)
                .SelectMany(frame => frame.Enemies.Where(enemy => enemy.Health > 0).Select(enemy => new { frame, enemy })).ToArray();
            Assert.That(samples, Is.Not.Empty, "A real recorded victim must define this first engagement.");
            int targetId = source.GetMountTargetId(action.MountSlotIndex);
            var identified = samples.Where(value => value.enemy.Id == targetId).OrderByDescending(value => value.frame.Seconds).ToArray();
            var victim = identified.Length > 0 ? identified[0] : samples
                .OrderBy(value => Vector2.SqrMagnitude(Point(value.enemy.Position) - Point(action.To)))
                .ThenByDescending(value => value.frame.Seconds).ThenBy(value => value.enemy.Id).First();
            var pen = new MeshPen(null);
            DoodleGlyphs.Enemy(pen, victim.enemy, (float)victim.frame.Seconds, theme: NotebookVisualThemeAsset.Default,
                sampleSeconds: victim.frame.Seconds, uniformScale: NotebookEnemyView.ScaleFor(true));
            foreach (var death in actionFrame.Events.Where(value => value.Kind == SimulationEventKind.EnemyDied && value.EntityId == victim.enemy.Id))
                DoodleGlyphs.DrawResourceEnemy(pen, death.EnemyKind, death.EnemyDrawingTier, Point(death.To),
                    NotebookEnemyMotion.Death, 0, NotebookVisualThemeAsset.Default, death.EnemyVisualScale, NotebookEnemyView.ScaleFor(true));
            Assert.That(pen.TryGetBounds(out var visual), Is.True, "The real target body, marks and health bar must have measurable geometry.");
            return visual;
        }
        static bool SpatialEvent(SimulationEventKind kind)
        {
            switch (kind)
            {
                case SimulationEventKind.Shot: case SimulationEventKind.ProjectileLaunched: case SimulationEventKind.ProjectileImpact:
                case SimulationEventKind.WeaponActionStarted: case SimulationEventKind.EnemyAttack: case SimulationEventKind.EnemyHit: case SimulationEventKind.EnemyDied:
                case SimulationEventKind.EnemyGlobLaunched: case SimulationEventKind.EnemyGlobImpact:
                case SimulationEventKind.StructureCompleted: case SimulationEventKind.StructureDestroyed: case SimulationEventKind.StructureErased:
                case SimulationEventKind.BlotSpawned: case SimulationEventKind.BlotExpired: case SimulationEventKind.BlotCleared: case SimulationEventKind.BlotAbsorbed:
                case SimulationEventKind.BaseHit: case SimulationEventKind.InkWellHit: case SimulationEventKind.InkWellActivated:
                    return true;
                default: return false;
            }
        }
        static void CompleteOverview(TrialLaneSketch lane)
        {
            Assert.That(lane.ViewBounds, Is.EqualTo(lane.RecordingBounds));
            foreach (var frame in lane.Playback.Report.Frames)
            {
                foreach (var tower in frame.Towers)
                {
                    Assert.That(lane.ViewBounds.Contains(Center(tower)), Is.True, "Every actual copy stays in the overview.");
                    if (tower.ResolvedEntry?.Snapshot != null)
                        Contains(lane.ViewBounds, NotebookAssemblyView.Bounds(Center(tower), tower.ResolvedEntry.Snapshot, NotebookMountLayout.BodyRadians(tower)), "The overview retains the whole shared assembly envelope.");
                }
                foreach (var enemy in frame.Enemies.Where(enemy => enemy.Health > 0))
                    Assert.That(lane.ViewBounds.Contains(Point(enemy.Position)), Is.True, "Every actual living enemy stays in the overview.");
                foreach (var action in frame.Events.Where(action => SpatialEvent(action.Kind)))
                {
                    Assert.That(lane.ViewBounds.Contains(Point(action.From)), Is.True, "The real recorded event origin stays in the overview: " + action.Kind);
                    Assert.That(lane.ViewBounds.Contains(Point(action.To)), Is.True, "The real recorded event endpoint stays in the overview: " + action.Kind);
                }
            }
        }

        [TestCase("core.gun")] [TestCase("core.mortar")]
        public void TrialFocusFramesActualShotsAndStructuresWithAnOverviewControl(string mechanism)
        {
            string before = fixture.State(); int writes = fixture.Store.SaveCalls;
            var entry = fixture.Catalog.Starters.First(page => page.Kind == AlbumEntryKind.Tower && page.Snapshot.Recipe.Tower.Mounts.Count == 1 && page.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == mechanism);
            Open(entry); Click("Composite trial run");
            var report = panel.LastAssemblyTrial; var comparison = panel.LastAssemblyTrialComparison;
            foreach (var lane in panel.Root.GetComponentsInChildren<TrialLaneSketch>())
            {
                var focus = lane.ViewBounds;
                var recording = lane.Playback.Report;
                Assert.That(recording.Copies, Is.GreaterThan(1), "This regression needs real remote copies that the close-up may omit.");
                Assert.That(focus.width, Is.LessThan(lane.RecordingBounds.width), "Unused enemy approach cells must not make the combat silhouettes microscopic.");
                var first = recording.Frames.SelectMany(frame => frame.Events.Select(action => new { frame, action }))
                    .Where(value => (value.action.Kind == SimulationEventKind.Shot || value.action.Kind == SimulationEventKind.ProjectileLaunched) &&
                        value.frame.Towers.Any(tower => tower.Id == value.action.SourceStructureId && tower.ResolvedEntry?.Snapshot != null))
                    .OrderBy(value => value.action.SimulationSeconds).ThenBy(value => value.action.Sequence).First();
                var source = first.frame.Towers.Single(tower => tower.Id == first.action.SourceStructureId);
                var muzzle = DoodleGlyphs.SegmentOrigin(source, first.action.MountSlotIndex, first.action.To, Point(first.action.From),
                    first.action.SimulationSeconds, null, NotebookVisualThemeAsset.Default);
                var engagement = NotebookAssemblyView.Bounds(Center(source), source.ResolvedEntry.Snapshot, NotebookMountLayout.BodyRadians(source));
                engagement = Include(Include(Include(engagement, muzzle), Point(first.action.From)), Point(first.action.To));
                if (first.action.Kind == SimulationEventKind.ProjectileLaunched)
                {
                    float arc = Mathf.Min(3, Vector2.Distance(muzzle, Point(first.action.To)) * .22f);
                    engagement = Include(engagement, new Vector2((muzzle.x + first.action.To.X) * .5f, Mathf.Max(muzzle.y, first.action.To.Y) + arc));
                    if (first.action.Radius > 0)
                    {
                        engagement = Include(engagement, Point(first.action.To) - Vector2.one * first.action.Radius);
                        engagement = Include(engagement, Point(first.action.To) + Vector2.one * first.action.Radius);
                    }
                }
                Assert.That(focus.Contains(muzzle), Is.True, "Focus uses the actual shared-tree muzzle, not an invented shot source.");
                Assert.That(focus.Contains(Point(first.action.To)), Is.True, "Focus retains the actual first victim/impact endpoint.");
                var targetVisual = ActualTargetVisualBounds(recording, first.frame, first.action, source);
                Contains(focus, targetVisual, "The actual first victim's entire body, marks and health bar/death pose must remain visible.");
                engagement = Include(Include(engagement, targetVisual.min), targetVisual.max);
                TightEngagement(focus, engagement);
            }
            Click("Composite trial overview");
            foreach (var lane in panel.Root.GetComponentsInChildren<TrialLaneSketch>()) CompleteOverview(lane);
            Assert.That(panel.LastAssemblyTrial, Is.SameAs(report));
            Assert.That(panel.LastAssemblyTrialComparison, Is.SameAs(comparison));
            Assert.That(fixture.State(), Is.EqualTo(before));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void GeneratorTrialServiceDoesNotAwardOrWriteTheProfile()
        {
            var before = fixture.SavedData;
            string state = fixture.State(); byte[] bytes = fixture.SavedBytes;
            int writes = fixture.Store.SaveCalls, loads = fixture.Store.LoadCalls;
            var entry = Support(AssemblyStructureFamily.Generator);
            var report = fixture.Run.RunAssemblyTrial(entry, AssemblyTrialScenario.Generator);
            Assert.That(report, Is.Not.Null);
            Assert.That(report.Income, Is.GreaterThan(0));
            Assert.That(report.PaidWaves, Is.GreaterThan(0));
            Assert.That(report.Damage, Is.Null, "Generator income is not DPS.");
            Assert.That(fixture.Run.RunAssemblyTrial(entry, AssemblyTrialScenario.Generator), Is.Not.Null,
                "Repeating the service call cannot publish local trial income as a real reward.");
            var after = fixture.Run.Workshop;
            Assert.That(after.Gold, Is.EqualTo(before.Gold));
            Assert.That(after.TotalXp, Is.EqualTo(before.TotalXp));
            Assert.That(JsonUtility.ToJson(fixture.SavedData), Is.EqualTo(JsonUtility.ToJson(before)), "Reward and operation receipts must remain identical.");
            Assert.That(fixture.State(), Is.EqualTo(state));
            Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
            Assert.That(fixture.Store.LoadCalls, Is.EqualTo(loads));
            Assert.That(fixture.Run.InWorkshop, Is.True);
        }

        [UnityTest] public IEnumerator PauseAndRestartUseTheSameSeedAndKeepTheUnsavedDraft()
        {
            panel.OpenEntry(fixture.Gun()); panel.DraftSession.Rename("Имя без сохранения"); panel.RefreshStatus();
            var session = panel.DraftSession; var draft = session.Draft;
            string before = Json(draft), state = fixture.State(); byte[] saved = fixture.SavedBytes; int writes = fixture.Store.SaveCalls;
            Click("Composite trial"); Click("Composite trial run");
            var report = Report; var recording = panel.LastTrial; var comparison = panel.LastAssemblyTrialComparison;
            Assert.That(comparison, Is.Not.Null);
            Assert.That(Value(report, "Seed"), Is.EqualTo(1337));
            Click("Composite trial pause");
            var lanes = panel.Root.GetComponentsInChildren<TrialLaneSketch>();
            Assert.That(lanes.Length, Is.EqualTo(2));
            Assert.That(lanes.All(lane => lane.PlaybackPaused), Is.True);
            var frozen = lanes.Select(lane => lane.PlaybackSeconds).ToArray();
            yield return new WaitForSecondsRealtime(.035f);
            for (int i = 0; i < lanes.Length; i++) Assert.That(lanes[i].PlaybackSeconds, Is.EqualTo(frozen[i]).Within(.0001f));
            // The independent recordings can wrap at different durations. Restore distinct real-recording
            // positions directly so camera/resize persistence does not depend on a long wall-clock wait.
            float sharedWindow = lanes.Min(lane => lane.Playback.Report.DurationSeconds);
            for (int i = 0; i < lanes.Length; i++)
            {
                var laneRecording = lanes[i].Playback.Report;
                float seconds = (i == 0 ? .2f : .7f) * sharedWindow;
                lanes[i].SetResult(laneRecording, Time.unscaledTime - seconds / lanes[i].PlaybackSpeed, laneRecording.DurationSeconds);
                lanes[i].PlaybackPaused = true;
            }
            frozen = lanes.Select(lane => lane.PlaybackSeconds).ToArray();
            Assert.That(Mathf.Abs(frozen[0] - frozen[1]), Is.GreaterThan(.001f), "The fixture must expose a reset of the second cursor to the first.");
            Click("Composite trial overview"); panel.Reflow(800, 600); Canvas.ForceUpdateCanvases();
            lanes = panel.Root.GetComponentsInChildren<TrialLaneSketch>();
            for (int i = 0; i < lanes.Length; i++)
            {
                Assert.That(lanes[i].PlaybackPaused, Is.True);
                Assert.That(lanes[i].PlaybackSeconds, Is.EqualTo(frozen[i]).Within(.001f), lanes[i].name);
            }
            Assert.That(Report, Is.SameAs(report)); Assert.That(panel.LastAssemblyTrialComparison, Is.SameAs(comparison));
            Click("Composite trial pause");
            Assert.That(lanes.All(lane => !lane.PlaybackPaused), Is.True);
            Click("Composite trial restart");
            Assert.That(Report, Is.Not.SameAs(report));
            Assert.That(Value(Report, "Seed"), Is.EqualTo(1337));
            Assert.That(Value(Report, "Budget"), Is.EqualTo(400));
            Assert.That(panel.LastTrial.Damage, Is.EqualTo(recording.Damage));
            Assert.That(panel.LastTrial.Frames.SelectMany(frame => frame.Events).Select(item => item.Kind + ":" + item.SimulationSeconds + ":" + item.SourceStructureId),
                Is.EqualTo(recording.Frames.SelectMany(frame => frame.Events).Select(item => item.Kind + ":" + item.SimulationSeconds + ":" + item.SourceStructureId)));
            Assert.That(panel.LastAssemblyTrialComparison, Is.Not.SameAs(comparison));
            Assert.That(panel.LastAssemblyTrialComparison.Damage, Is.EqualTo(comparison.Damage));
            Assert.That(panel.LastAssemblyTrialComparison.Recording.Frames.SelectMany(frame => frame.Events).Select(item => item.Kind + ":" + item.SimulationSeconds + ":" + item.SourceStructureId),
                Is.EqualTo(comparison.Recording.Frames.SelectMany(frame => frame.Events).Select(item => item.Kind + ":" + item.SimulationSeconds + ":" + item.SourceStructureId)));
            Click("Composite trial header return");
            Assert.That(panel.DraftSession, Is.SameAs(session));
            Assert.That(panel.DraftSession.Draft, Is.SameAs(draft));
            Assert.That(Json(draft), Is.EqualTo(before));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Имя без сохранения"));
            Assert.That(panel.Step, Is.EqualTo(3));
            Assert.That(fixture.State(), Is.EqualTo(state));
            Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void ChangingScenarioBudgetOrComparisonClearsBothPreviousMeasurements()
        {
            Open(fixture.Gun()); Click("Composite trial run");
            Assert.That(Report, Is.Not.Null);
            Click("Composite trial scenario Armored");
            AssertTrialMeasurementsInvalidated();
            Click("Composite trial run");
            Assert.That(Value(Report, "Scenario").ToString(), Is.EqualTo("Armored"));
            Click("Composite trial budget");
            AssertTrialMeasurementsInvalidated();
            Click("Composite trial run");
            Assert.That(Value(Report, "Budget"), Is.EqualTo(800));
            Assert.That((int)Value(Report, "Cost"), Is.LessThanOrEqualTo(800));
            Click("Composite trial compare"); AssertTrialMeasurementsInvalidated();
        }

        void AssertTrialMeasurementsInvalidated()
        {
            Assert.That(panel.LastAssemblyTrial, Is.Null); Assert.That(panel.LastAssemblyTrialComparison, Is.Null);
            Assert.That(panel.LastTrial, Is.Null);
            Assert.That(panel.Root.GetComponentsInChildren<TrialLaneSketch>().All(lane => !lane.HasRecording), Is.True);
            Assert.That(MeasuredCells(), Is.Empty); Assert.That(MeasuredCells(true), Is.Empty);
            Assert.That(Button("Composite trial pause").IsInteractable(), Is.False);
            Assert.That(Button("Composite trial restart").IsInteractable(), Is.False);
        }

        [Test] public void TrialModalBlocksBackgroundSaveAndRestoresReviewFocusOnReturn()
        {
            panel.OpenEntry(fixture.Gun());
            var save = Button("Sequence save");
            Click("Composite trial");
            Assert.That(save.IsInteractable(), Is.False);
            var modal = Lane.GetComponentInParent<WorkshopModal>();
            Assert.That(modal, Is.Not.Null, "The field belongs to the actual modal scope regardless of its viewport nesting.");
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.transform.IsChildOf(modal.transform), Is.True);
            Click("Composite trial header return");
            Assert.That(Button("Sequence save").IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("Composite trial").gameObject));
        }

        [Test] public void CoveredBlotterDraftUsesTheSharedCleaningTrialWithoutRewards()
        {
            Assert.That(fixture.Commands.PurchasePart("core.blotter", Guid.NewGuid()).Success, Is.True);
            Assert.That(fixture.Commands.PurchasePart("head.blotter", Guid.NewGuid()).Success, Is.True);
            var gun = fixture.Gun(); var tower = gun.Snapshot.Recipe.Tower; var old = tower.Mounts[0].Weapon;
            var weapon = new WeaponRecipe(new PartRef("core.blotter", 1), new PartRef("head.blotter", 1), old.Sight, old.Magazine, old.Trigger);
            var blotter = new AlbumEntry("trial.blotter", "Промокашка", 1, new RecipeSnapshot(
                new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) })),
                fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
            Assert.That(blotter.Snapshot.Recipe.Parts.All(part => fixture.Run.Workshop.IsUnlocked(part.Id)), Is.True);
            string before = fixture.State(); int writes = fixture.Store.SaveCalls;
            panel.OpenEntry(blotter);
            Assert.That(panel.DraftSession.Resolution.Valid, Is.True, "The complete art catalog now certifies the owned blotter parts.");
            Assert.That(panel.DraftSession.CanTrial, Is.True);
            Click("Composite trial"); Click("Composite trial run");
            Assert.That(panel.LastAssemblyTrial, Is.Not.Null);
            Assert.That(panel.LastAssemblyTrial.Scenario, Is.EqualTo(AssemblyTrialScenario.Blots));
            Assert.That(panel.LastAssemblyTrial.Damage, Is.Null, "Cleaning never invents DPS or damage.");
            Assert.That(panel.LastAssemblyTrial.BlotsCleared, Is.GreaterThan(0));
            Assert.That(fixture.State(), Is.EqualTo(before));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void BlotScenarioShowsMeasuredCleaningRatherThanTheoreticalDps()
        {
            Open(fixture.Gun()); Click("Composite trial scenario Blots"); Click("Composite trial run");
            Assert.That(Value(Report, "Scenario").ToString(), Is.EqualTo("Blots"));
            Assert.That(Value(Report, "BlotsCleared"), Is.EqualTo(0), "A gun does not clean blots.");
            Assert.That(Value(Report, "UnresolvedBlots"), Is.GreaterThan(0));
            Assert.That(panel.Root.GetComponentsInChildren<Text>().Where(label => label.name.StartsWith("Composite trial measured row ")).Any(label => label.text.Contains("DPS")), Is.False);
        }

        [TestCase("invalid")] [TestCase("locked")] [TestCase("overload")] [TestCase("clearance")]
        public void InvalidLockedOverloadAndClearanceDraftsCannotLaunch(string kind)
        {
            var entry = fixture.Gun();
            if (kind == "locked")
            {
                var tower = entry.Snapshot.Recipe.Tower; var old = tower.Mounts[0].Weapon;
                var weapon = new WeaponRecipe(old.Mechanism, new PartRef("head.long", 1), old.Sight, old.Magazine, old.Trigger);
                entry = new AlbumEntry("trial.locked", "Закрытая деталь", 1, new RecipeSnapshot(new AssemblyRecipe(new TowerRecipe(tower.Foundation,
                    new[] { new WeaponMountRecipe(0, weapon) })), fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
            }
            if (kind == "overload")
            {
                var source = fixture.Catalog.Starters.First(page => page.Kind == AlbumEntryKind.Tower && page.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar");
                var tower = source.Snapshot.Recipe.Tower;
                entry = new AlbumEntry("trial.overload", "Перегруз", 1, new RecipeSnapshot(new AssemblyRecipe(new TowerRecipe(tower.Foundation,
                    new[] { new WeaponMountRecipe(0, tower.Mounts[0].Weapon), new WeaponMountRecipe(1, tower.Mounts[0].Weapon) })), fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
            }
            panel.OpenEntry(entry);
            if (kind == "invalid") panel.DraftSession.Rename("");
            if (kind == "clearance")
            {
                var input = AssemblyFitResources.ReadInput();
                var frame = input.Frames.Single(item => item.FrameId == entry.Snapshot.Recipe.Tower.Foundation.Frame.Id);
                frame.Keepouts = new[] { new AssemblyFitKeepoutInput { X = -.5f, Y = -.5f, Width = 1, Height = 1 } };
                var session = new WorkshopDraftSession(fixture.Run, fixture.Catalog, new AssemblyFitCatalog(input)); session.Begin(entry);
                Assert.That(session.Diagnostics.Select(issue => issue.Code), Does.Contain("assembly.clearance"));
                typeof(CompositeWorkshopPanel).GetProperty("DraftSession").SetValue(panel, session);
            }
            panel.Refresh();
            Assert.That(panel.DraftSession.CanTrial, Is.False);
            Assert.That(Button("Composite trial").IsInteractable(), Is.False);
            Assert.That(panel.RunTrial(), Is.Null);
            Assert.That(panel.Root.Find("Composite trial overlay"), Is.Null);
        }

        [TestCase(false)] [TestCase(true)]
        public void SupportTrialServiceRejectsLockedOrInvalidStatsWithoutWriting(bool locked)
        {
            var repair = Support(AssemblyStructureFamily.Repair).Snapshot.Recipe.Structure;
            var recipe = new AssemblyRecipe(new StructureRecipe(AssemblyStructureFamily.Repair, repair.Site,
                repair.Function,
                locked ? new PartRef("shell.reinforced", 1) : repair.Shell, repair.Utility));
            var definitions = fixture.Catalog.Definitions.Select(part => !locked && part.Id == "site.repair"
                ? new ComponentDefinition(part.Id, part.Revision, part.Name, part.Category, part.RequiredLevel,
                    part.UnlockGold, part.WeaponFamilies, part.StructureFamilies,
                    part.Modifiers.Select(modifier => modifier.Stat == AssemblyStat.Health && modifier.Operation == ModifierOperation.Set
                        ? new ComponentModifier(AssemblyStat.Health, ModifierOperation.Set, -1) : modifier), part.RequiredParts)
                : part);
            var entry = new AlbumEntry("trial.invalid.support", "Недопустимый ремонт", 1,
                new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, definitions));
            if (locked) Assert.That(fixture.Run.Workshop.IsUnlocked("shell.reinforced"), Is.False);
            else Assert.That(TowerCatalog.ResolveEntry(entry, fixture.Balance).Valid, Is.False);
            int writes = fixture.Store.SaveCalls;
            var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(entry);
            Assert.That(draft.CanTrial, Is.False);
            Assert.That(fixture.Run.RunAssemblyTrial(entry, AssemblyTrialScenario.Repair), Is.Null);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [TestCase(1024, 768)] [TestCase(1280, 720)] [TestCase(1920, 1080)]
        public void TrialControlsStayWithinTheSheetAtSupportedSizes(int width, int height)
        {
            panel.Reflow(width, height); Open(fixture.Gun());
            var sheet = panel.Root.Find("Composite trial overlay/Composite trial sheet") as RectTransform;
            Assert.That(sheet, Is.Not.Null);
            foreach (var control in sheet.GetComponentsInChildren<Selectable>())
            {
                var corners = new Vector3[4]; ((RectTransform)control.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 local = sheet.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(sheet.rect.xMin - .1f, sheet.rect.xMax + .1f), control.name);
                    Assert.That(local.y, Is.InRange(sheet.rect.yMin - .1f, sheet.rect.yMax + .1f), control.name);
                }
            }
            Assert.That(Button("Composite trial run"), Is.Not.Null);
            Assert.That(Button("Composite trial header return"), Is.Not.Null);
            Assert.That(Lane.rectTransform.rect.width, Is.GreaterThan(400));
            Assert.That(Lane.rectTransform.rect.height, Is.GreaterThan(250));
        }
    }
}
#endif
