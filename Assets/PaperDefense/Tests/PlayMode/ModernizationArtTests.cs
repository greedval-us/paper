#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class ModernizationArtTests
    {
        static AlbumEntry Entry => AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
            "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot().Starters[0];
        static GameBalance Rules()
        {
            var rules = GameBalance.CreateDefault(); rules.Mechanics.FieldModernization = true;
            rules.Mechanics.MaximumModernizationRank = 3; rules.StartingInk = 1000; rules.PlanningSeconds = 3600;
            rules.Trials.MaximumTicks = 12; return rules;
        }
        static UIVertex[] Marks(StructureView tower, NotebookVisualThemeAsset theme = null)
        {
            var pen = new MeshPen(null); DoodleGlyphs.DrawModernization(pen, tower, theme);
            return Vertices(pen);
        }
        static UIVertex[] Vertices(MeshPen pen)
        {
            using (var helper = new VertexHelper())
            {
                pen.CopyTo(helper, Vector2.zero, 1); var values = new UIVertex[helper.currentVertCount];
                for (int i = 0; i < values.Length; i++) helper.PopulateUIVertex(ref values[i], i);
                return values;
            }
        }
        static UIVertex[] Effect(SimulationEvent item, float age, NotebookVisualThemeAsset theme = null, bool dark = true, float unitsPerPixel = 0)
        {
            var pen = new MeshPen(null) { WorldUnitsPerPixel = unitsPerPixel };
            NotebookEffectRenderer.Draw(pen, item, new Vector2(100, 100), age, theme, dark);
            return Vertices(pen);
        }
        static SimulationEvent CompletionEvent()
        {
            var events = new SimulationEventBuffer();
            events.Publish(10, SimulationEventKind.ModernizationCompleted, new Point2(4.5f, 4.5f), new Point2(4.5f, 4.5f),
                StructureKind.Gun, entityId: 7, sourceStructureId: 7, simulationSeconds: 2);
            return events.Single();
        }
        static StructureView Frame(ModernizationRanks ranks)
            => TrialRunner.Run(Entry, Rules(), TrialScenario.Crowd, new TrialModernizationConfiguration(ranks, 3), 200).Frames[0].Towers[0];
        static object[] Signature(UIVertex[] values) => values.Select(v => (object)(v.position, v.color)).ToArray();
        static UIVertex[] Assembly(AssemblyVisualComposition composition)
        {
            var pen = new MeshPen(null); composition.Draw(pen, Vector2.zero); return Vertices(pen);
        }
        static UIVertex[] Equipment(AssemblyVisualComposition composition, ComponentCategory category)
        {
            var pen = new MeshPen(null);
            foreach (var fragment in composition.Fragments.Where(f => f.Layer == (f.SlotIndex < 0 ? (int)category : 6 + (int)category)))
                NightNotebookArt.DrawDevelopment(fragment, composition.Development, pen, Vector2.zero, 1, Color.white, 1);
            return Vertices(pen);
        }

        [Test] public void CompletedDirectionsEquipTheActualAssemblyAtEveryRankWithoutMovingItsMechanicalAnchors()
        {
            var baseline = Frame(default); var rest = DoodleGlyphs.ComposeAssembly(baseline);
            var plain = Assembly(rest).Select(v => v.position).ToArray();
            var directionShapes = new System.Collections.Generic.List<Vector3[]>();
            foreach (ModernizationDirection direction in System.Enum.GetValues(typeof(ModernizationDirection)))
            {
                Vector3[] previous = plain; float previousSpan = 0;
                for (int rank = 1; rank <= 3; rank++)
                {
                    var ranks = new ModernizationRanks(power: direction == ModernizationDirection.Power ? rank : 0,
                        mechanics: direction == ModernizationDirection.Mechanics ? rank : 0,
                        range: direction == ModernizationDirection.Range ? rank : 0,
                        resilience: direction == ModernizationDirection.Resilience ? rank : 0);
                    var state = Frame(ranks); var composition = DoodleGlyphs.ComposeAssembly(state);
                    var vertices = Assembly(composition); var shape = vertices.Select(v => v.position).ToArray();
                    Assert.That(shape, Is.Not.EqualTo(previous), direction + " must change the machine itself at each rank, without drawing rank markers.");
                    foreach (var other in directionShapes) Assert.That(shape, Is.Not.EqualTo(other), "Different directions need different equipment.");
                    directionShapes.Add(shape); previous = shape;
                    Assert.That(composition.Fragments.Select(f => (f.PartId, f.Region, f.Origin, f.Radians)),
                        Is.EqualTo(rest.Fragments.Select(f => (f.PartId, f.Region, f.Origin, f.Radians))));
                    Assert.That(composition.Anchors.Select(a => (a.Attach, a.RotationCenter, a.Muzzle)),
                        Is.EqualTo(rest.Anchors.Select(a => (a.Attach, a.RotationCenter, a.Muzzle))));
                    Assert.That(state.ResolvedEntry.Snapshot.Recipe.Parts.Select(p => p.Id),
                        Is.EqualTo(baseline.ResolvedEntry.Snapshot.Recipe.Parts.Select(p => p.Id)));
                    Assert.That(NotebookAssemblyView.ScaleFor(state.ResolvedEntry.Snapshot),
                        Is.EqualTo(NotebookAssemblyView.ScaleFor(baseline.ResolvedEntry.Snapshot)));
                    var category = direction == ModernizationDirection.Power ? ComponentCategory.Head :
                        direction == ModernizationDirection.Resilience ? ComponentCategory.Hull : ComponentCategory.Mechanism;
                    var hardware = Equipment(composition, category);
                    Assert.That(hardware, Is.Not.Empty);
                    float span = direction == ModernizationDirection.Range ? hardware.Max(v => v.position.y) - hardware.Min(v => v.position.y) :
                        hardware.Max(v => v.position.x) - hardware.Min(v => v.position.x);
                    Assert.That(span, Is.GreaterThan(previousSpan), "Higher ranks enlarge physical equipment, not just a counter.");
                    previousSpan = span;
                    foreach (var vertex in vertices)
                    {
                        Assert.That(vertex.position.x, Is.InRange(composition.Bounds.xMin - .001f, composition.Bounds.xMax + .001f));
                        Assert.That(vertex.position.y, Is.InRange(composition.Bounds.yMin - .001f, composition.Bounds.yMax + .001f));
                    }
                }
            }
            var weapon = baseline.ResolvedEntry.Snapshot.Recipe.Tower.Mounts[0].Weapon;
            AssemblyVisualComposer.Fits.TryGetWeapon(weapon.Mechanism.Id, weapon.Mechanism.Revision, out var fit);
            float angle = Mathf.Clamp(.43f, fit.MinRadians, fit.MaxRadians), recoil = -fit.RecoilLimit * .5f;
            var motion = new[] { new AssemblyMountPose(0, angle, recoil) };
            var development = new AssemblyDevelopmentPose(power: 2, mechanics: 1, range: 2);
            var aimed = AssemblyVisualComposer.ComposeWeapon(weapon, new AssemblyVisualPose(motion, development: development));
            var unaimed = AssemblyVisualComposer.ComposeWeapon(weapon, new AssemblyVisualPose(
                new[] { new AssemblyMountPose(0, 0, recoil) }, development: development));
            var aimedEquipment = Equipment(aimed, ComponentCategory.Head);
            var unaimedEquipment = Equipment(unaimed, ComponentCategory.Head);
            Assert.That(aimedEquipment.Length, Is.EqualTo(unaimedEquipment.Length));
            for (int i = 0; i < aimedEquipment.Length; i++)
                Assert.That(Vector2.Distance(aimedEquipment[i].position, NotebookMountLayout.Rotate(unaimedEquipment[i].position, angle)), Is.LessThan(.00001f));
            Assert.That(aimed.Anchors.Select(a => a.Muzzle), Is.EqualTo(AssemblyVisualComposer.ComposeWeapon(weapon,
                new AssemblyVisualPose(motion)).Anchors.Select(a => a.Muzzle)), "Equipment follows aim and recoil but never moves the shot origin.");
        }

        [Test] public void ActiveColorIsPaintedOnWeaponChamberAndBarrelAndFrozenEquipmentDoesNotAnimate()
        {
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                theme.Data.ReducedMotion = true; theme.Data.NightInkOnDarkSurfaces = true;
                foreach (var color in new[] { InkColor.Red, InkColor.Blue, InkColor.Green })
                {
                    var state = Frame(new ModernizationRanks(power: 1, color: color, colorRank: 2));
                    var composition = DoodleGlyphs.ComposeAssembly(state);
                    Assert.That(composition.Development.Color, Is.EqualTo(color));
                    Assert.That(composition.Development.ColorRank, Is.EqualTo(2));
                    Color32 ink = NightNotebookEffectPalette.ColorInk(color);
                    foreach (var category in new[] { ComponentCategory.Mechanism, ComponentCategory.Head })
                    {
                        var painted = Equipment(composition, category).Where(v => v.color.Equals(ink)).ToArray();
                        Assert.That(painted.Length, Is.GreaterThanOrEqualTo(12), category + " needs a filled colour panel, not a point marker.");
                        Assert.That(painted.Max(v => v.position.x) - painted.Min(v => v.position.x), Is.GreaterThan(.02f));
                    }
                    var reduced = DoodleGlyphs.ComposeAssembly(state, 500, theme: theme);
                    foreach (var category in new[] { ComponentCategory.Mechanism, ComponentCategory.Head })
                        Assert.That(Signature(Equipment(reduced, category)), Is.EqualTo(Signature(Equipment(composition, category))),
                            "Frozen equipment must stay unchanged while time and reduced-motion settings change.");
                }
                var bare = Frame(default).ResolvedEntry.Snapshot;
                var selectedButInactive = AssemblyVisualComposer.Compose(bare, new AssemblyVisualPose(
                    development: new AssemblyDevelopmentPose(color: InkColor.Blue, colorRank: 0)));
                Assert.That(Signature(Assembly(selectedButInactive)), Is.EqualTo(Signature(Assembly(
                    AssemblyVisualComposer.Compose(bare, new AssemblyVisualPose())))), "A colour choice without a completed rank must not paint active weapon parts.");
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test] public void IdenticalRecipesWithDifferentDirectionsHaveBoundedDistinctStaticMarkersAndIdenticalMuzzles()
        {
            var power = Frame(new ModernizationRanks(power: 3, mechanics: 2, range: 1));
            var resilience = Frame(new ModernizationRanks(resilience: 3, range: 2, mechanics: 1));
            var a = Marks(power); var b = Marks(resilience);
            Assert.That(a, Is.Not.Empty); Assert.That(Signature(b), Is.Not.EqualTo(Signature(a)));
            foreach (var vertex in a.Concat(b))
            {
                Assert.That(vertex.position.x, Is.InRange(power.Cell.X, power.Cell.X + 1));
                Assert.That(vertex.position.y, Is.InRange(power.Cell.Y, power.Cell.Y + 1));
            }
            Assert.That(DoodleGlyphs.ComposeAssembly(power).Anchors.Select(v => v.Muzzle),
                Is.EqualTo(DoodleGlyphs.ComposeAssembly(resilience).Anchors.Select(v => v.Muzzle)));
            Assert.That(power.ResolvedEntry.Snapshot.Recipe.Parts.Select(p => p.Id),
                Is.EqualTo(resilience.ResolvedEntry.Snapshot.Recipe.Parts.Select(p => p.Id)));
        }

        [Test] public void ActualBattleWorkProgressAndCompletionReadTheSameStateAsTrialWithoutAdvancingTime()
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            var entry = TowerCatalog.ResolveEntry(Entry, rules).Value; var cell = new Cell(4, 4);
            Assert.That(session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            session.Tick(10); var tower = session.StructureAt(cell);
            var unmodifiedAssembly = Signature(Assembly(DoodleGlyphs.ComposeAssembly(new RunReadModel(session).Structures.Single(s => s.Id == tower.Id))));
            var quote = session.QuoteModernization(tower.Id, ModernizationDirection.Power);
            Assert.That(session.TryModernize(quote, out reason), Is.True, reason);
            var started = new RunReadModel(session).Structures.Single(s => s.Id == tower.Id);
            var before = Signature(Marks(started)); session.Tick(1);
            var middle = new RunReadModel(session).Structures.Single(s => s.Id == tower.Id);
            Assert.That(Signature(Assembly(DoodleGlyphs.ComposeAssembly(middle))), Is.EqualTo(unmodifiedAssembly), "Pending work must not install finished hardware early.");
            Assert.That(middle.Modernization.WorkRemaining, Is.LessThan(started.Modernization.WorkRemaining));
            Assert.That(Signature(Marks(middle)), Is.Not.EqualTo(before));
            Assert.That(Signature(Marks(middle)), Is.EqualTo(Signature(Marks(middle))), "Sampling frozen state cannot advance progress.");
            session.Tick(2); var completed = new RunReadModel(session).Structures.Single(s => s.Id == tower.Id);
            Assert.That(completed.Modernization.IsWorking, Is.False);
            Assert.That(Signature(Assembly(DoodleGlyphs.ComposeAssembly(completed))), Is.Not.EqualTo(unmodifiedAssembly), "The completed real battle purchase changes the actual assembly.");
            Assert.That(completed.Modernization.Rank(ModernizationDirection.Power), Is.EqualTo(Frame(new ModernizationRanks(power: 1)).Modernization.Rank(ModernizationDirection.Power)));
            Assert.That(Marks(completed), Is.Not.Empty);
            var complete = session.EventBuffer.Last(e => e.Kind == SimulationEventKind.ModernizationCompleted);
            Assert.That(session.EventBuffer.Count(e => e.Kind == SimulationEventKind.ModernizationCompleted), Is.EqualTo(1));
            Assert.That(complete.EntityId, Is.EqualTo(tower.Id)); Assert.That(complete.SourceStructureId, Is.EqualTo(tower.Id));
            Assert.That(complete.To.X, Is.EqualTo(cell.X + .5f)); Assert.That(complete.To.Y, Is.EqualTo(cell.Y + .5f));
            Assert.That(complete.Sequence, Is.GreaterThan(session.EventBuffer.Single(e => e.Kind == SimulationEventKind.ModernizationStarted).Sequence));
            long tick = session.TickIndex, sequence = session.EventBuffer.LatestSequence; int ink = session.Ink;
            var accent = Effect(complete, .1f);
            AssertStampBounds(accent, complete);
            Assert.That(accent.Max(v => v.position.y), Is.GreaterThan(cell.Y + 1), "The transient stamp must clear the tower silhouette.");
            Assert.That(accent.Max(v => v.position.x) - accent.Min(v => v.position.x), Is.GreaterThan(.7f), "Completion must be larger than the old tiny check.");
            Assert.That(Effect(complete, NotebookEffectRenderer.Duration(complete, NotebookVisualThemeAsset.Default)), Is.Empty);
            Assert.That(session.TickIndex, Is.EqualTo(tick)); Assert.That(session.EventBuffer.LatestSequence, Is.EqualTo(sequence));
            Assert.That(session.Ink, Is.EqualTo(ink)); Assert.That(tower.Modernization.Rank(ModernizationDirection.Power), Is.EqualTo(1));
        }

        [Test] public void CompletionStampSettlesOnceHoldsThenFadesAndReducedMotionKeepsItsGeometryFixed()
        {
            var item = CompletionEvent();
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                theme.Data.NightInkOnDarkSurfaces = true;
                Assert.That(NotebookEffectRenderer.Duration(item, theme), Is.EqualTo(.9f));
                var early = Effect(item, .02f, theme); var held = Effect(item, .25f, theme);
                var heldLater = Effect(item, .4f, theme); var fading = Effect(item, .8f, theme);
                Assert.That(early.Select(v => v.position), Is.Not.EqualTo(held.Select(v => v.position)), "The normal stamp settles once.");
                Assert.That(Signature(heldLater), Is.EqualTo(Signature(held)), "The legible hold must not pulse or blink.");
                Assert.That(fading.Select(v => v.position), Is.EqualTo(held.Select(v => v.position)), "Fade must not expand into an explosion ring.");
                Assert.That(fading.Max(v => v.color.a), Is.InRange(1, held.Max(v => v.color.a) - 1));
                foreach (var frame in new[] { early, held, fading }) AssertStampBounds(frame, item);
                theme.Data.ReducedMotion = true;
                var reducedStart = Effect(item, .02f, theme); var reducedEnd = Effect(item, .8f, theme);
                Assert.That(reducedStart.Select(v => v.position), Is.EqualTo(held.Select(v => v.position)));
                Assert.That(reducedEnd.Select(v => v.position), Is.EqualTo(reducedStart.Select(v => v.position)));
                Assert.That(reducedEnd.Max(v => v.color.a), Is.LessThan(reducedStart.Max(v => v.color.a)));
                Assert.That(Effect(item, -.01f, theme), Is.Empty);
                Assert.That(Effect(item, .9f, theme), Is.Empty);
                Assert.That(Effect(item, 2, theme), Is.Empty, "Completion never loops, including in reduced motion.");
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test] public void CompletionUsesContrastingGoldAndSharesItsMinimumLifetimeWithThemeOverrides()
        {
            var item = CompletionEvent(); var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                var clip = new NotebookArtClip { Id = "fx.modernization.complete", Duration = .12f, Loop = true };
                theme.Data.Clips.Add(clip); theme.Data.NightInkOnDarkSurfaces = true;
                Assert.That(NotebookEffectRenderer.EffectId(item), Is.EqualTo(clip.Id));
                Assert.That(NotebookEffectRenderer.Duration(item, theme), Is.EqualTo(.9f), "A short authored clip cannot erase the confirmation before it is readable.");
                var night = Effect(item, .25f, theme); var paper = Effect(item, .25f, theme, false);
                Color32 nightInk = night.OrderByDescending(v => v.color.a).First().color;
                Color32 paperInk = paper.OrderByDescending(v => v.color.a).First().color;
                Assert.That(nightInk, Is.EqualTo((Color32)NightNotebookArt.Accent), "The event has no cartridge color: completion uses ordinary gold.");
                Assert.That(Contrast(nightInk, NightNotebookArt.Paper), Is.GreaterThanOrEqualTo(4.5f));
                Assert.That(Contrast(paperInk, NotebookPalette.Paper), Is.GreaterThanOrEqualTo(4.5f));
                Assert.That(paperInk, Is.Not.EqualTo(nightInk), "Light paper needs darker gold.");
                theme.Data.NightInkOnDarkSurfaces = false;
                Assert.That(Signature(Effect(item, .25f, theme)), Is.EqualTo(Signature(paper)), "Caller-owned themes opt in to the dark surface palette.");
                clip.Duration = 1.2f;
                Assert.That(NotebookEffectRenderer.Duration(item, theme), Is.EqualTo(1.2f));
                Assert.That(Effect(item, 1.05f, theme), Is.Not.Empty);
                Assert.That(Effect(item, 1.2f, theme), Is.Empty, "Even a looping clip cannot make completion immortal.");
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test] public void OverviewCompletionKeepsAReadableStampWithoutGrowingUnboundedOrChangingCloseZoomArtwork()
        {
            var item = CompletionEvent(); const float overview = 1f / 6;
            var natural = Effect(item, .25f); var close = Effect(item, .25f, unitsPerPixel: .01f);
            var distant = Effect(item, .25f, unitsPerPixel: overview);
            var extreme = Effect(item, .25f, unitsPerPixel: .5f);
            var naturalPaper = StampPaper(natural); var closePaper = StampPaper(close);
            Assert.That(closePaper, Is.EqualTo(naturalPaper), "Close zoom keeps the authored panel geometry; pixel stroke antialiasing remains MeshPen's responsibility.");
            var distantPaper = StampPaper(distant); var extremePaper = StampPaper(extreme);
            float Width(Vector3[] points) => points.Max(v => v.x) - points.Min(v => v.x);
            Assert.That(Width(naturalPaper), Is.EqualTo(.78f).Within(.0001f));
            Assert.That(Width(distantPaper) / overview, Is.GreaterThanOrEqualTo(14 - .001f), "A six-pixel cell must not reduce completion to a five-pixel check.");
            Assert.That(Width(extremePaper), Is.LessThanOrEqualTo(2.4f + .0001f), "The authored stamp remains bounded even beyond the supported overview zoom.");
            foreach (var vertex in distant)
            {
                Assert.That(vertex.position.x, Is.InRange(item.To.X - 1.5f, item.To.X + 1.5f));
                Assert.That(vertex.position.y, Is.InRange(item.To.Y + .1f, item.To.Y + 2.3f));
            }
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                theme.Data.NightInkOnDarkSurfaces = true; theme.Data.ReducedMotion = true;
                var early = Effect(item, .02f, theme, unitsPerPixel: overview);
                var late = Effect(item, .8f, theme, unitsPerPixel: overview);
                Assert.That(early.Select(v => v.position), Is.EqualTo(late.Select(v => v.position)));
                Assert.That(Width(StampPaper(early)) / overview, Is.GreaterThanOrEqualTo(14 - .001f));
                Assert.That(Effect(item, .9f, theme, unitsPerPixel: overview), Is.Empty);
            }
            finally { Object.DestroyImmediate(theme); }
        }

        static Vector3[] StampPaper(UIVertex[] vertices)
        {
            Color32 paper = NightNotebookArt.Paper;
            var points = vertices.Where(vertex => vertex.color.r == paper.r && vertex.color.g == paper.g && vertex.color.b == paper.b)
                .Select(vertex => vertex.position).ToArray();
            Assert.That(points, Is.Not.Empty, "The stamp needs its contrasting paper backing.");
            return points;
        }

        static void AssertStampBounds(UIVertex[] vertices, SimulationEvent item)
        {
            Assert.That(vertices, Is.Not.Empty);
            foreach (var vertex in vertices)
            {
                Assert.That(vertex.position.x, Is.InRange(item.To.X - .55f, item.To.X + .55f));
                Assert.That(vertex.position.y, Is.InRange(item.To.Y + .3f, item.To.Y + 1.05f));
            }
        }

        static float Contrast(Color first, Color second)
        {
            float Linear(float value) => value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
            float Luminance(Color value) => .2126f * Linear(value.r) + .7152f * Linear(value.g) + .0722f * Linear(value.b);
            float a = Luminance(first), b = Luminance(second);
            return (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
        }

        [Test] public void FixedColorRankAndReducedMotionRemainReadableAndRecordedRanksSurviveSeeking()
        {
            var setup = new TrialModernizationConfiguration(new ModernizationRanks(power: 1, color: InkColor.Blue, colorRank: 2), 2);
            var report = TrialRunner.Run(Entry, Rules(), TrialScenario.Crowd, setup, 100);
            var playback = new NotebookTrialPlayback(); playback.Bind(report); playback.Seek(report.DurationSeconds); playback.Seek(0);
            var tower = report.Frames[playback.FrameIndex].Towers[0];
            Assert.That(tower.Modernization.ColorRank, Is.EqualTo(2)); Assert.That(tower.Color, Is.EqualTo(InkColor.Blue));
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                theme.Data.ReducedMotion = true;
                Assert.That(Signature(Marks(tower, theme)), Is.EqualTo(Signature(Marks(tower))), "Rank/work markers stay static with reduced motion.");
                Assert.That(Signature(Marks(tower)), Is.Not.EqualTo(Signature(Marks(Frame(new ModernizationRanks(power: 1, color: InkColor.Blue, colorRank: 1))))));
            }
            finally { Object.DestroyImmediate(theme); }
        }
    }
}
#endif

