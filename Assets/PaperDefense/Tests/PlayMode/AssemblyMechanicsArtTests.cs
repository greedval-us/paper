#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class AssemblyMechanicsArtTests
    {
        static PartRef P(string id) => new PartRef(id, 1);
        static WeaponRecipe Weapon(string family, string magazine = "magazine.standard")
            => new WeaponRecipe(P("core." + family), P("head." + family), P("sight.iron"), P(magazine), P("trigger.standard"), P("mount.standard"));

        static UIVertex[] Draw(AssemblyVisualComposition composition, float construction = 1, AssemblyMechanicsPose? overlay = null)
        {
            var pen = new MeshPen(null);
            composition.Draw(pen, Vector2.zero, mechanics: overlay, construction: construction);
            using (var target = new VertexHelper())
            {
                pen.CopyTo(target, Vector2.zero, 1);
                var vertices = new UIVertex[target.currentVertCount];
                for (int i = 0; i < vertices.Length; i++) target.PopulateUIVertex(ref vertices[i], i);
                return vertices;
            }
        }

        static AssemblyVisualComposition Posed(string family, AssemblyMechanicsPose mechanics, string magazine = "magazine.standard")
            => AssemblyVisualComposer.ComposeWeapon(Weapon(family, magazine), new AssemblyVisualPose(new[] {
                new AssemblyMountPose(0, -.6f, mechanics: mechanics) }));

        [TestCase("gun")] [TestCase("piercer")] [TestCase("mortar")] [TestCase("fan")]
        [TestCase("ricochet")] [TestCase("stapler")] [TestCase("blotter")]
        public void EveryFamilyMovesAnActualInternalMechanismWithoutMovingItsMuzzleOrChangingItsParts(string family)
        {
            var rest = Posed(family, default);
            var working = Posed(family, new AssemblyMechanicsPose(fire: .8f));
            Assert.That(working.Anchors.Select(anchor => (anchor.Attach, anchor.RotationCenter, anchor.Muzzle)),
                Is.EqualTo(rest.Anchors.Select(anchor => (anchor.Attach, anchor.RotationCenter, anchor.Muzzle))));
            Assert.That(working.Fragments.Select(fragment => (fragment.PartId, fragment.Layer, fragment.Origin, fragment.Radians)),
                Is.EqualTo(rest.Fragments.Select(fragment => (fragment.PartId, fragment.Layer, fragment.Origin, fragment.Radians))));
            var a = Draw(rest); var b = Draw(working);
            Assert.That(b.Select(vertex => vertex.position).SequenceEqual(a.Select(vertex => vertex.position)), Is.False,
                "Changing color or adding a flash is insufficient: the mechanism itself must move.");
            foreach (var vertex in b)
            {
                Assert.That(float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.x), Is.False);
                Assert.That(vertex.position.x, Is.InRange(working.Bounds.xMin - .00001f, working.Bounds.xMax + .00001f));
                Assert.That(vertex.position.y, Is.InRange(working.Bounds.yMin - .00001f, working.Bounds.yMax + .00001f));
            }
        }

        [TestCase("magazine.standard")] [TestCase("magazine.drum")] [TestCase("magazine.belt")]
        public void ReloadMovesTheSelectedMagazineInsideItsExistingEnvelopeAndReturnsExactlyToRest(string magazine)
        {
            var rest = Posed("gun", default, magazine);
            var reload = Posed("gun", new AssemblyMechanicsPose(true, .5f), magazine);
            var a = Draw(rest); var b = Draw(reload);
            Assert.That(b.Select(vertex => vertex.position).SequenceEqual(a.Select(vertex => vertex.position)), Is.False);
            Assert.That(reload.Bounds, Is.EqualTo(rest.Bounds));
            Assert.That(reload.Anchors.Select(anchor => anchor.Muzzle), Is.EqualTo(rest.Anchors.Select(anchor => anchor.Muzzle)));
            Assert.That(Draw(Posed("gun", default, magazine)).Select(vertex => vertex.position), Is.EqualTo(a.Select(vertex => vertex.position)));
        }

        static WeaponMountView Mount(WeaponCycleState cycle)
        {
            var state = new WeaponMountState();
            typeof(WeaponMountState).GetProperty(nameof(WeaponMountState.Cycle)).SetValue(state, cycle);
            return (WeaponMountView)typeof(WeaponMountView).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(WeaponMountState), typeof(Point2) }, null).Invoke(new object[] { state, default(Point2) });
        }

        [Test] public void ReloadPoseObservesNormalizedRealWorkAndStopsWhenTheFeedStopsOrMotionIsReduced()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var snapshot = catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower).Snapshot;
            var resolved = AssemblyResolver.Resolve(snapshot, GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(resolved.Valid, Is.True);
            var weapon = resolved.Value.Weapons[0];
            var cycle = new WeaponCycleState();
            for (int i = 0; i < 10000 && !cycle.IsReloading; i++)
                cycle = WeaponCycleRules.Step(cycle, weapon, .03f, true, 1).State;
            Assert.That(cycle.IsReloading, Is.True);
            var progressed = WeaponCycleRules.Step(cycle, weapon, weapon.ReloadSeconds * .25f, false, 1).State;
            var pose = DoodleGlyphs.MountMechanics(Mount(progressed), 0, false);
            Assert.That(pose.Reloading, Is.True);
            Assert.That(pose.Reload, Is.EqualTo(1 - progressed.RemainingWork).Within(.00001));
            var stopped = WeaponCycleRules.Step(progressed, weapon, 5, false, 0).State;
            Assert.That(DoodleGlyphs.MountMechanics(Mount(stopped), 0, false).Reload, Is.EqualTo(pose.Reload));
            var reduced = DoodleGlyphs.MountMechanics(Mount(progressed), 1, true);
            Assert.That(reduced.Reloading, Is.False); Assert.That(reduced.Fire, Is.Zero);
            Assert.That(progressed.RemainingWork, Is.EqualTo(stopped.RemainingWork), "Visual sampling must not advance the real cycle.");
        }

        [Test] public void ConstructionRevealsTheFoundationBeforeTheWeaponAndFinishesAtTheExactNormalDrawing()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var snapshot = catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower).Snapshot;
            var composition = AssemblyVisualComposer.Compose(snapshot, AssemblyPreviewPose.For(snapshot));
            Assert.That(Draw(composition, 0), Is.Empty);
            var early = Draw(composition, .25f); var complete = Draw(composition);
            Assert.That(early, Is.Not.Empty);
            Assert.That(early.Length, Is.LessThan(complete.Length));
            Assert.That(early.Max(vertex => vertex.position.y), Is.LessThan(complete.Max(vertex => vertex.position.y)),
                "The completed barrel cannot float above an unfinished foundation.");
            Assert.That(Draw(composition, 1).Select(vertex => (vertex.position, vertex.color)),
                Is.EqualTo(complete.Select(vertex => (vertex.position, vertex.color))));
        }

        [Test] public void DamageChangesMaterialWithoutDisplacingAnyAnchorsOrGeometry()
        {
            var composition = Posed("gun", default);
            var normal = Draw(composition); var hit = Draw(composition, overlay: new AssemblyMechanicsPose(hit: 1));
            Assert.That(hit.Select(vertex => vertex.position), Is.EqualTo(normal.Select(vertex => vertex.position)));
            Assert.That(hit.Select(vertex => vertex.color).SequenceEqual(normal.Select(vertex => vertex.color)), Is.False);
        }

        [Test] public void StructureWorkAndHitRecordsStayBoundedExpireAndDoNotAdvanceWhileSimulationTimeIsFrozen()
        {
            var animation = new NotebookAnimationState();
            animation.ObserveStructureWork(1, 10); animation.ObserveStructureHit(1, 10);
            var first = animation.StructureMechanics(1, 10.05, false);
            var paused = animation.StructureMechanics(1, 10.05, false);
            Assert.That(paused.Activity, Is.EqualTo(first.Activity)); Assert.That(paused.Hit, Is.EqualTo(first.Hit));
            Assert.That(first.Activity, Is.GreaterThan(0)); Assert.That(first.Hit, Is.GreaterThan(0));
            var reduced = animation.StructureMechanics(1, 10.05, true);
            Assert.That(reduced.Activity, Is.Zero); Assert.That(reduced.Hit, Is.EqualTo(first.Hit));
            for (int id = 2; id < NotebookAnimationState.MaximumMounts + 100; id++) animation.ObserveStructureWork(id, 10);
            Assert.That(animation.StructurePulseCount, Is.LessThanOrEqualTo(NotebookAnimationState.MaximumMounts));
            animation.Prune(11);
            Assert.That(animation.StructurePulseCount, Is.Zero);
            Assert.That(animation.StructureMechanics(1, 11, false).Activity, Is.Zero);
        }

        [TestCase("frame.outpost", "frame.precision")] [TestCase("frame.siege", "frame.precision")]
        [TestCase("hull.ribbed", "hull.reinforced")] [TestCase("turn.deliberate", "turn.standard")]
        [TestCase("head.fan.focus", "head.fan")] [TestCase("head.ricochet.long", "head.ricochet")]
        [TestCase("head.absorb", "head.blotter")] [TestCase("sight.snap", "sight.tracking")]
        [TestCase("sight.survey", "sight.scope")] [TestCase("magazine.quick", "magazine.standard")]
        [TestCase("magazine.reserve", "magazine.drum")] [TestCase("trigger.economy", "trigger.standard")]
        [TestCase("function.spikes.barbed", "function.spikes")] [TestCase("function.beacon.focused", "function.beacon")]
        public void CampaignVariantsHaveTheirOwnDrawnPartsWithinTheSameCategoryEnvelope(string variant, string original)
        {
            var a = AssemblyVisualComposer.ComposePart(P(original));
            var b = AssemblyVisualComposer.ComposePart(P(variant));
            Assert.That(b.Fragments.Single().PartId, Is.EqualTo(variant));
            Assert.That(b.Bounds, Is.EqualTo(a.Bounds));
            Assert.That(Draw(b).Select(vertex => (vertex.position, vertex.color))
                .SequenceEqual(Draw(a).Select(vertex => (vertex.position, vertex.color))), Is.False,
                "A new catalogue name must not reuse an identical fallback drawing.");
            Assert.That(Draw(b).Length, Is.GreaterThan(0));
        }

        [TestCase(SimulationEventKind.StructureHit)] [TestCase(SimulationEventKind.RepairApplied)]
        [TestCase(SimulationEventKind.GeneratorPaid)] [TestCase(SimulationEventKind.SlowApplied)]
        [TestCase(SimulationEventKind.SpikesTriggered)] [TestCase(SimulationEventKind.BeaconMarked)]
        public void ActualStructuralEventsDriveTheCorrectActorAndBoundedVisibleEffects(SimulationEventKind kind)
        {
            var events = new SimulationEventBuffer();
            events.Publish(1, kind, new Point2(10,10), new Point2(14,12), StructureKind.RepairPost,
                entityId: 7, sourceStructureId: kind == SimulationEventKind.StructureHit ? 0 : 3,
                simulationSeconds: 2, magnitude: kind == SimulationEventKind.SlowApplied ? .5f : 4,
                radius: kind == SimulationEventKind.SpikesTriggered ? 2 : 0);
            var item = events[0]; var animation = new NotebookAnimationState();
            Assert.That(animation.AddEvent(item), Is.True);
            var actor = animation.StructureMechanics(kind == SimulationEventKind.StructureHit ? 7 : 3, 2.03, false);
            Assert.That(kind == SimulationEventKind.StructureHit ? actor.Hit : actor.Activity, Is.GreaterThan(0));
            var untouched = animation.StructureMechanics(99, 2.03, false);
            Assert.That(untouched.Activity, Is.Zero); Assert.That(untouched.Hit, Is.Zero);
            var theme = NotebookVisualThemeAsset.Default; var pen = new MeshPen(null);
            NotebookEffectRenderer.Draw(pen, item, new Vector2(100,100), .03f, theme, true);
            Assert.That(pen.VertexCount, Is.GreaterThan(0));
            Assert.That(pen.TryGetBounds(out var bounds), Is.True);
            Assert.That(bounds.xMax, Is.LessThan(20), "Support and hit effects must use actual source/recipient positions, not an unrelated muzzle.");
            pen.Clear(); NotebookEffectRenderer.Draw(pen, item, Vector2.zero, NotebookEffectRenderer.Duration(item, theme) + .01f, theme, true);
            Assert.That(pen.VertexCount, Is.Zero, "Effects must leave no immortal geometry after their bounded lifetime.");
        }
    }
}
#endif
