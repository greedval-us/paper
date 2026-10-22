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
            panel = new CompositeWorkshopPanel(canvas.transform, fixture.Run, null, null);
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
            Assert.That(Text("Composite trial comparison measured result").text, Does.Contain("Измеренный"));
            Assert.That(fixture.State(), Is.EqualTo(before));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [TestCase(true)] [TestCase(false)]
        public void BlotsReportKeepsAllSixMeasurementRowsVisibleAt720p(bool comparing)
        {
            Open(fixture.Gun()); Click("Composite trial scenario Blots");
            if (!comparing)
                for (int i = 0; i < 20 && Text("Composite trial comparison measured result") != null; i++)
                    Click("Composite trial compare");
            Assert.That(Text("Composite trial comparison measured result") != null, Is.EqualTo(comparing));
            Click("Composite trial run");
            foreach (var label in panel.Root.GetComponentsInChildren<Text>().Where(value =>
                value.name == "Composite trial measured result" || value.name == "Composite trial comparison measured result"))
            {
                Assert.That(label.text.Split('\n').Length, Is.EqualTo(6));
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.EqualTo(label.text.Length),
                    label.name + ": the report must render its final measurement, including duration.");
            }
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
                var bounds = NotebookAssemblyView.Bounds(Center(tower), tower.ResolvedEntry.Snapshot);
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
            Assert.That(Text("Composite trial measured result").text, Does.Not.Match(@"[0-9]"));
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
                        Contains(lane.ViewBounds, NotebookAssemblyView.Bounds(Center(tower), tower.ResolvedEntry.Snapshot), "The overview retains the whole shared assembly envelope.");
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
                var engagement = NotebookAssemblyView.Bounds(Center(source), source.ResolvedEntry.Snapshot);
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

        [TestCase(AssemblyStructureFamily.Wall)] [TestCase(AssemblyStructureFamily.Slow)]
        [TestCase(AssemblyStructureFamily.Repair)] [TestCase(AssemblyStructureFamily.Generator)]
        public void SupportFocusUsesARealEffectAndItsBeneficiaryWithoutDiscardingTheOverview(AssemblyStructureFamily family)
        {
            string before = fixture.State(); int writes = fixture.Store.SaveCalls;
            Open(Support(family)); Click("Composite trial run");
            var report = panel.LastAssemblyTrial; var comparison = panel.LastAssemblyTrialComparison;
            var lane = panel.Root.GetComponentsInChildren<TrialLaneSketch>().First(field => ReferenceEquals(field.Playback.Report, report.Recording));
            var recording = lane.Playback.Report;
            StructureView source = default; Vector2 beneficiary = default; TrialFrame effectFrame = null;
            if (family == AssemblyStructureFamily.Wall)
            {
                // A walkable fixture can route around the walls without an EnemyAttack.
                // Frame the actual closest recorded approach to the first real wall instead.
                source = recording.Frames[0].Towers.First(tower => tower.ResolvedEntry?.Support?.Kind == family);
                Vector2 origin = Center(source);
                var closest = recording.Frames.SelectMany(frame => frame.Enemies.Where(enemy => enemy.Health > 0).Select(enemy => new { frame, enemy }))
                    .OrderBy(value => Vector2.SqrMagnitude(origin - Point(value.enemy.Position)))
                    .ThenBy(value => value.frame.Seconds).ThenBy(value => value.enemy.Id).First();
                beneficiary = Point(closest.enemy.Position); effectFrame = closest.frame;
            }
            else if (family == AssemblyStructureFamily.Repair)
            {
                var initial = recording.Frames[0].Towers.First(tower => tower.Health < tower.MaxHealth && tower.ResolvedEntry?.Support?.Kind != family);
                effectFrame = recording.Frames.First(frame => frame.Towers.Any(tower => tower.Id == initial.Id && tower.Health > initial.Health));
                var repaired = effectFrame.Towers.Single(tower => tower.Id == initial.Id);
                source = effectFrame.Towers.Where(tower => tower.ResolvedEntry?.Support?.Kind == family &&
                    Vector2.Distance(Center(tower), Center(repaired)) <= tower.ResolvedEntry.Support.Radius).OrderBy(tower => tower.Id).First();
                beneficiary = Center(repaired);
                Assert.That(report.RepairedHealth, Is.GreaterThan(0), "The beneficiary actually gained health in this recording.");
            }
            else if (family == AssemblyStructureFamily.Generator)
            {
                var initial = recording.Frames[0].Towers.First(tower => tower.ResolvedEntry?.Support?.Kind == family);
                effectFrame = recording.Frames.First(frame => frame.Towers.Any(tower => tower.Id == initial.Id && tower.GeneratorPaidWaves > initial.GeneratorPaidWaves));
                source = effectFrame.Towers.Single(tower => tower.Id == initial.Id); beneficiary = Center(source);
                Assert.That(report.Income, Is.GreaterThan(0), "The selected generator really produced income.");
            }
            else
            {
                foreach (var frame in recording.Frames)
                {
                    foreach (var support in frame.Towers.Where(tower => tower.ResolvedEntry?.Support?.Kind == family).OrderBy(tower => tower.Id))
                    {
                        var affected = frame.Enemies.Where(enemy => enemy.Health > 0 && Vector2.Distance(Center(support), Point(enemy.Position)) <= support.ResolvedEntry.Support.Radius).ToArray();
                        if (affected.Length == 0) continue;
                        source = support; beneficiary = Point(affected[0].Position); effectFrame = frame; break;
                    }
                    if (effectFrame != null) break;
                }
                Assert.That(effectFrame, Is.Not.Null, "A real enemy must enter the support's effect radius.");
                Assert.That(report.PreventedMovement, Is.GreaterThan(0), "The recorded slow actually prevented movement.");
            }
            var focus = lane.ViewBounds;
            var engagement = Include(NotebookAssemblyView.Bounds(Center(source), source.ResolvedEntry.Snapshot), beneficiary);
            if (family == AssemblyStructureFamily.Wall || family == AssemblyStructureFamily.Slow)
            {
                var target = effectFrame.Enemies.Where(enemy => enemy.Health > 0)
                    .OrderBy(enemy => Vector2.SqrMagnitude(Point(enemy.Position) - beneficiary)).ThenBy(enemy => enemy.Id).First();
                var pen = new MeshPen(null);
                DoodleGlyphs.Enemy(pen, target, (float)effectFrame.Seconds, sampleSeconds: effectFrame.Seconds,
                    uniformScale: NotebookEnemyView.ScaleFor(true));
                Assert.That(pen.TryGetBounds(out var targetVisual), Is.True);
                Contains(focus, targetVisual, "The selected real support beneficiary's entire enemy drawing remains visible.");
                engagement = Include(Include(engagement, targetVisual.min), targetVisual.max);
            }
            TightEngagement(focus, engagement);
            Assert.That(focus.width * focus.height, Is.LessThan(lane.RecordingBounds.width * lane.RecordingBounds.height), "A support close-up cannot silently reuse the entire recording.");
            Click("Composite trial overview");
            foreach (var field in panel.Root.GetComponentsInChildren<TrialLaneSketch>()) CompleteOverview(field);
            Assert.That(panel.LastAssemblyTrial, Is.SameAs(report)); Assert.That(panel.LastAssemblyTrialComparison, Is.SameAs(comparison));
            Assert.That(fixture.State(), Is.EqualTo(before)); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void GeneratorTrial_DoesNotAwardProfile()
        {
            var before = fixture.SavedData;
            string state = fixture.State(); byte[] bytes = fixture.SavedBytes;
            int writes = fixture.Store.SaveCalls, loads = fixture.Store.LoadCalls;
            Open(Support(AssemblyStructureFamily.Generator)); Click("Composite trial run");
            Assert.That(Report, Is.Not.Null);
            Assert.That(Value(Report, "Income"), Is.GreaterThan(0));
            Assert.That(Value(Report, "PaidWaves"), Is.GreaterThan(0));
            Assert.That(Value(Report, "Damage"), Is.Null, "Generator income is not DPS.");
            Click("Composite trial pause"); Click("Composite trial restart"); Click("Composite close trial");
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
            string before = Json(draft), state = fixture.State(); int writes = fixture.Store.SaveCalls;
            Click("Composite trial"); Click("Composite trial run");
            var report = Report; var recording = panel.LastTrial;
            Assert.That(Value(report, "Seed"), Is.EqualTo(1337));
            Click("Composite trial pause");
            Assert.That(Lane.PlaybackPaused, Is.True);
            float frozen = Lane.PlaybackSeconds;
            yield return new WaitForSecondsRealtime(.035f);
            Assert.That(Lane.PlaybackSeconds, Is.EqualTo(frozen).Within(.0001f));
            Click("Composite trial pause");
            Assert.That(Lane.PlaybackPaused, Is.False);
            Click("Composite trial restart");
            Assert.That(Report, Is.Not.SameAs(report));
            Assert.That(Value(Report, "Seed"), Is.EqualTo(1337));
            Assert.That(Value(Report, "Budget"), Is.EqualTo(400));
            Assert.That(panel.LastTrial.Damage, Is.EqualTo(recording.Damage));
            Assert.That(panel.LastTrial.Frames.SelectMany(frame => frame.Events).Select(item => item.Kind + ":" + item.SimulationSeconds + ":" + item.SourceStructureId),
                Is.EqualTo(recording.Frames.SelectMany(frame => frame.Events).Select(item => item.Kind + ":" + item.SimulationSeconds + ":" + item.SourceStructureId)));
            Click("Composite close trial");
            Assert.That(panel.DraftSession, Is.SameAs(session));
            Assert.That(panel.DraftSession.Draft, Is.SameAs(draft));
            Assert.That(Json(draft), Is.EqualTo(before));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Имя без сохранения"));
            Assert.That(panel.Step, Is.EqualTo(2));
            Assert.That(fixture.State(), Is.EqualTo(state));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void ChangingScenarioOrBudgetClearsThePreviousMeasurement()
        {
            Open(fixture.Gun()); Click("Composite trial run");
            Assert.That(Report, Is.Not.Null);
            Click("Composite trial scenario Armored");
            Assert.That(Report, Is.Null); Assert.That(Lane.HasRecording, Is.False);
            Click("Composite trial run");
            Assert.That(Value(Report, "Scenario").ToString(), Is.EqualTo("Armored"));
            Click("Composite trial budget");
            Assert.That(Report, Is.Null); Assert.That(panel.LastTrial, Is.Null);
            Click("Composite trial run");
            Assert.That(Value(Report, "Budget"), Is.EqualTo(800));
            Assert.That((int)Value(Report, "Cost"), Is.LessThanOrEqualTo(800));
        }

        [TestCase(AssemblyStructureFamily.Repair, "RepairedHealth")]
        [TestCase(AssemblyStructureFamily.Generator, "Income")]
        public void SupportResultsShowTheirApplicableProductionMetric(AssemblyStructureFamily family, string metric)
        {
            Open(Support(family)); Click("Composite trial run");
            Assert.That(Value(Report, metric), Is.GreaterThan(0));
            Assert.That(Value(Report, "Damage"), Is.Null);
            Assert.That(Text("Composite trial measured result").text, Does.Not.Contain("DPS"));
            Assert.That(Text("Composite trial conditions").text, Does.Contain("400").And.Contain("1337"));
        }

        [Test] public void TrialModalBlocksBackgroundSaveAndRestoresReviewFocusOnReturn()
        {
            panel.OpenEntry(fixture.Gun());
            var save = Button("Composite save");
            Click("Composite trial");
            Assert.That(save.IsInteractable(), Is.False);
            var modal = Lane.GetComponentInParent<WorkshopModal>();
            Assert.That(modal, Is.Not.Null, "The field belongs to the actual modal scope regardless of its viewport nesting.");
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.transform.IsChildOf(modal.transform), Is.True);
            Click("Composite close trial");
            Assert.That(Button("Composite save").IsInteractable(), Is.True);
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
            Assert.That(Text("Composite trial measured result").text, Does.Not.Contain("DPS"));
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
                var input = JsonUtility.FromJson<FitEnvelope>(Resources.Load<TextAsset>("Art/NotebookMounts").text).fits;
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
        public void SupportTrialStillRejectsLockedOrInvalidStats(bool locked)
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
            panel.OpenEntry(entry);
            Assert.That(panel.DraftSession.CanTrial, Is.False);
            Assert.That(Button("Composite trial").IsInteractable(), Is.False);
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
            Assert.That(Button("Composite close trial"), Is.Not.Null);
            Assert.That(Lane.rectTransform.rect.width, Is.GreaterThan(400));
            Assert.That(Lane.rectTransform.rect.height, Is.GreaterThan(250));
        }
    }
}
#endif
