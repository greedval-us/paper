using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
#if UNITY_EDITOR
using PaperDefense.Application;
using UnityEditor;
#endif

namespace PaperDefense.Tests
{
    public sealed class NotebookAnimationRenderingTests
    {
        NotebookVisualThemeAsset theme;
        [SetUp] public void Setup()
        {
            theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            theme.Data.Recoils.Add(new NotebookWeaponRecoil { WeaponId = "weapon.gun", Duration = .2f, Distance = .1f,
                Keys = new List<NotebookRecoilKey> { new NotebookRecoilKey { Time = 0, Value = 1 }, new NotebookRecoilKey { Time = .2f, Value = 0 } } });
        }
        [TearDown] public void Teardown() { Object.DestroyImmediate(theme); }
        static void Action(SimulationEventBuffer events, int structure, int slot, long action, double seconds)
            => events.Publish(1, SimulationEventKind.WeaponActionStarted, default, new Point2(20, 20), StructureKind.Gun,
                sourceStructureId: structure, mountSlotIndex: slot, actionId: action, simulationSeconds: seconds);

        [Test] public void ThreeIdenticalWeaponsRecoilIndependentlyAndSegmentsNeverRestartAnAction()
        {
            var events = new SimulationEventBuffer(); var state = new NotebookAnimationState();
            for (int slot = 0; slot < 3; slot++)
            {
                Action(events, 7, slot, slot + 1, 1 + slot * .05);
                events.Publish(1, SimulationEventKind.Shot, default, new Point2(20, 20), StructureKind.Gun,
                    sourceStructureId: 7, mountSlotIndex: slot, actionId: slot + 1, simulationSeconds: 1 + slot * .05);
                events.Publish(1, SimulationEventKind.Shot, default, new Point2(21, 21), StructureKind.Gun,
                    sourceStructureId: 7, mountSlotIndex: slot, actionId: slot + 1, simulationSeconds: 1 + slot * .05);
            }
            foreach (var item in events) state.AddEvent(item);
            Assert.That(state.RecoilStartCount, Is.EqualTo(3));
            Assert.That(state.Recoil(7, 0, 1.12, theme), Is.EqualTo(-.04f).Within(.00001));
            Assert.That(state.Recoil(7, 1, 1.12, theme), Is.EqualTo(-.065f).Within(.00001));
            Assert.That(state.Recoil(7, 2, 1.12, theme), Is.EqualTo(-.09f).Within(.00001));
            state.AddEvent(events[0]);
            Assert.That(state.RecoilStartCount, Is.EqualTo(3), "Repeated deliveries cannot launch a second recoil.");
            Assert.That(state.Recoil(8, 0, 1.12, theme), Is.Zero);
        }

        [Test] public void SimulationClockFreezesOnPauseAdvancesOnStepAndCapsTheTerminalTail()
        {
            var clock = new NotebookVisualClock(); clock.Reset();
            clock.Advance(2, RunPhase.Assault, false, .016, .6);
            clock.Advance(2, RunPhase.Assault, true, 20, .6);
            Assert.That(clock.Seconds, Is.EqualTo(2));
            clock.Advance(2.125, RunPhase.Assault, true, 20, .6);
            Assert.That(clock.Seconds, Is.EqualTo(2.125), "A manual step advances by its actual simulation delta.");
            clock.Advance(2.375, RunPhase.Assault, false, .016, .6);
            Assert.That(clock.Seconds, Is.EqualTo(2.375), "Speed is already represented by elapsed simulation time.");
            clock.Advance(2.375, RunPhase.Victory, false, .016, .6);
            clock.Advance(2.375, RunPhase.Victory, true, 20, .6);
            Assert.That(clock.Seconds, Is.EqualTo(2.375));
            clock.Advance(2.375, RunPhase.Victory, false, 20, .6);
            Assert.That(clock.Seconds, Is.EqualTo(2.975).Within(.000001));
            clock.Advance(2.375, RunPhase.Victory, false, 20, .6);
            Assert.That(clock.Seconds, Is.EqualTo(2.975).Within(.000001));
            clock.Reset(); Assert.That(clock.Seconds, Is.Zero); Assert.That(clock.Terminal, Is.False);
        }

        [Test] public void OverflowEvictsOnlyVisualRecordsAndResetClearsAllTransientState()
        {
            var events = new SimulationEventBuffer(4096); var state = new NotebookAnimationState();
            for (int i = 0; i < 1200; i++) Action(events, i + 1, i % 3, i + 1, 1);
            for (int i = 0; i < 200; i++)
                events.Publish(1, SimulationEventKind.EnemyDied, default, new Point2(i, 3), default, entityId: i + 1,
                    simulationSeconds: 1, enemyKind: EnemyKind.Gnawer, enemyDrawingTier: DrawingTier.Hatched);
            foreach (var item in events) state.AddEvent(item, 32);
            Assert.That(state.MountCount, Is.EqualTo(NotebookAnimationState.MaximumMounts));
            Assert.That(state.Deaths.Count, Is.EqualTo(32));
            Assert.That(state.Deaths.Last().Position.x, Is.EqualTo(199));
            Assert.That(state.DroppedRecords, Is.GreaterThan(0));
            state.Prune(5, theme); Assert.That(state.MountCount, Is.Zero); Assert.That(state.Deaths.Count, Is.Zero);
            state.Clear(); Assert.That(state.RecoilStartCount, Is.Zero); Assert.That(state.DroppedRecords, Is.Zero);
        }

        [Test] public void TrialSeekReplaysTimestampedActionsAndRepeatedSeeksPreserveTheSamePose()
        {
            var report = TrialRunner.Run(new TowerBlueprint("seek", "Seek fixture", ChassisKind.Standard, StructureKind.Gun), TrialScenario.Crowd, budget: 40);
            var action = report.Frames.SelectMany(frame => frame.Events).First(item => item.Kind == SimulationEventKind.WeaponActionStarted);
            var playback = new NotebookTrialPlayback(); playback.Bind(report);
            double seconds = action.SimulationSeconds + .02;
            playback.Seek(seconds, theme);
            float recoil = playback.Animation.Recoil(action.SourceStructureId, action.MountSlotIndex, seconds, theme);
            Assert.That(recoil, Is.LessThan(0));
            int starts = playback.Animation.RecoilStartCount;
            playback.Seek(seconds, theme);
            Assert.That(playback.Animation.RecoilStartCount, Is.EqualTo(starts));
            playback.Seek(0, theme); playback.Seek(seconds, theme);
            Assert.That(playback.Animation.Recoil(action.SourceStructureId, action.MountSlotIndex, seconds, theme), Is.EqualTo(recoil));
            Assert.That(playback.Effects.Count, Is.LessThanOrEqualTo(theme.MaximumEffects));
            playback.Bind(report); Assert.That(playback.Animation.MountCount, Is.Zero);
        }

        [Test] public void EnemyWalkUsesActualMovementAndAStoppedEnemyReturnsToIdle()
        {
            var report = TrialRunner.Run(new TowerBlueprint("movement", "Movement fixture", ChassisKind.Standard, StructureKind.Gun), TrialScenario.Crowd, budget: 0);
            var first = report.Frames.First(frame => frame.Enemies.Count > 0);
            var before = first.Enemies[0];
            var next = report.Frames.First(frame => frame.Seconds > first.Seconds && frame.Enemies.Any(enemy => enemy.Id == before.Id
                && (enemy.Position.X != before.Position.X || enemy.Position.Y != before.Position.Y)));
            var after = next.Enemies.First(enemy => enemy.Id == before.Id);
            var animation = new NotebookAnimationState();
            Assert.That(animation.Observe(before, first.Seconds, out _, theme), Is.EqualTo(NotebookEnemyMotion.Idle));
            Assert.That(animation.Observe(after, next.Seconds, out _, theme), Is.EqualTo(NotebookEnemyMotion.Walk));
            Assert.That(animation.Observe(after, next.Seconds + .1, out _, theme), Is.EqualTo(NotebookEnemyMotion.Idle));
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.EnemyHit, after.Position, after.Position, default, entityId: after.Id, simulationSeconds: next.Seconds + .1);
            animation.AddEvent(events[0]);
            Assert.That(animation.Observe(after, next.Seconds + .12, out float hitAge, theme), Is.EqualTo(NotebookEnemyMotion.Hit));
            Assert.That(hitAge, Is.EqualTo(.02f).Within(.00001));
        }

        static TrialFrame Frame(float seconds, EnemyState[] enemies = null, StructureState[] towers = null,
            MortarProjectile[] projectiles = null, SimulationEvent[] events = null)
            => (TrialFrame)typeof(TrialFrame).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single().Invoke(new object[] {
                seconds, enemies ?? System.Array.Empty<EnemyState>(), towers ?? System.Array.Empty<StructureState>(),
                projectiles ?? System.Array.Empty<MortarProjectile>(), System.Array.Empty<ShotEvent>(), null, null, events, 0L });
        static TrialReport Recording(params TrialFrame[] frames)
        {
            var report = new TrialReport();
            typeof(TrialReport).GetProperty(nameof(TrialReport.Frames)).SetValue(report, System.Array.AsReadOnly(frames));
            return report;
        }
        static NotebookArtShape Shape(string id, params Vector2[] points) => new NotebookArtShape { Id = id,
            Marks = new List<NotebookArtMark> { new NotebookArtMark { Primitive = NotebookArtPrimitive.Polyline, Width = .04f, Points = points } } };

        [TestCase(false)] [TestCase(true)]
        public void TrialImpactPreservesCapturedMortarFootprint(bool vertical)
        {
            var footprint = FootprintGeometry.Resolve(new MortarFootprint(3), 2, default,
                vertical ? new Point2(0, 1) : new Point2(1, 0));
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.ProjectileImpact, default, new Point2(10, 10),
                StructureKind.Mortar, 2, impactFootprint: footprint, simulationSeconds: .05);
            var playback = new NotebookTrialPlayback();
            playback.Bind(Recording(Frame(0), Frame(1, events: new[] { events[0] })));
            playback.Seek(.05, theme);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh); NotebookTrialRenderer.DrawEffects(pen, playback, theme); pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), "The captured footprint must remain visible without a decorative asset.");
                var size = mesh.bounds.size;
                Assert.That(vertical ? size.y : size.x, Is.EqualTo(2 * footprint.ForwardRadius).Within(.14f));
                Assert.That(vertical ? size.x : size.y, Is.EqualTo(2 * footprint.SideRadius).Within(.14f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void TrialEffectUsesTheAssignedThemePalette()
        {
            theme.Data.Palette = new[] { new Color(.91f, .07f, .83f, 1) };
            theme.Data.Shapes.Add(Shape("fx.castle.hit", Vector2.zero, Vector2.up));
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.BaseHit, default, new Point2(10, 10), default, simulationSeconds: .05);
            var playback = new NotebookTrialPlayback();
            playback.Bind(Recording(Frame(0), Frame(1, events: new[] { events[0] })));
            playback.Seek(.05, theme);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh); NotebookTrialRenderer.DrawEffects(pen, playback, theme); pen.Upload();
                var expected = (Color32)theme.Color(NotebookInkChannel.Navy);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(mesh.colors32.All(c => c.r == expected.r && c.g == expected.g && c.b == expected.b), Is.True);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(.2f)] [TestCase(2f)]
        public void CapturedCircularFootprintOwnsItsRadiusEvenWhenFallbackDiffers(float radius)
        {
            var footprint = FootprintGeometry.Resolve(new MortarFootprint(1), radius, default, new Point2(1, 0));
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                NotebookEffectRenderer.DrawFootprint(pen, Vector2.zero, footprint, 5, 1, .01f, Color.white, 32, 1);
                pen.Upload();
                Assert.That(mesh.bounds.size.x, Is.EqualTo(2 * radius).Within(.02f));
                Assert.That(mesh.bounds.size.y, Is.EqualTo(2 * radius).Within(.02f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(AttackKind.Cone, "fx.fan")] [TestCase(AttackKind.Line, "fx.piercer")]
        [TestCase(AttackKind.Radial, "fx.stapler")] [TestCase(AttackKind.Cleanup, "fx.cleanup")]
        public void EffectsUseCapturedAttackForWeaponsWithoutLegacyAliases(AttackKind attack, string effectId)
        {
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.Shot, default, default, (StructureKind)(-1),
                weaponId: "weapon.custom", attack: attack);
            Assert.That(NotebookEffectRenderer.EffectId(events[0]), Is.EqualTo(effectId));
        }

        [Test] public void TrialDeathSuppressesTheStaleLiveSnapshotEvenAfterTheDeathTailExpiresAndSeekRestoresIt()
        {
            theme.Data.Shapes.Add(Shape("enemy.doodle.sketch.idle", Vector2.zero, Vector2.up));
            theme.Data.Shapes.Add(Shape("enemy.doodle.sketch.death", Vector2.zero, Vector2.right));
            var enemy = new EnemyState { Id = 11, Position = new Point2(10, 10), Health = 10, MaxHealth = 10 };
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.EnemyDied, enemy.Position, enemy.Position, default, entityId: enemy.Id, simulationSeconds: .05);
            var report = Recording(Frame(0, new[] { enemy }), Frame(1, events: new[] { events[0] }));
            var playback = new NotebookTrialPlayback(); playback.Bind(report); playback.Seek(.06, theme);
            Assert.That(playback.FrameIndex, Is.Zero); Assert.That(playback.IsEnemyEnded(enemy.Id), Is.True);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh); NotebookTrialRenderer.DrawEnemies(pen, playback, theme); pen.Upload();
                var deathOnly = mesh.vertices;
                pen.Clear(); DoodleGlyphs.DrawResourceEnemy(pen, enemy.Kind, enemy.DrawingTier, new Vector2(10, 10), NotebookEnemyMotion.Death, .01f, theme); pen.Upload();
                Assert.That(deathOnly, Is.EqualTo(mesh.vertices), "The interval contains one death pose and no stale live pose.");
                playback.Seek(.8, theme); pen.Clear(); NotebookTrialRenderer.DrawEnemies(pen, playback, theme); pen.Upload();
                Assert.That(mesh.vertexCount, Is.Zero, "Expiry of the visual tail must not resurrect the previous snapshot.");
                playback.Seek(1, theme); Assert.That(playback.IsEnemyEnded(enemy.Id), Is.False, "The next snapshot reconciles removals.");
                playback.Seek(.01, theme); pen.Clear(); NotebookTrialRenderer.DrawEnemies(pen, playback, theme); pen.Upload();
                Assert.That(playback.IsEnemyEnded(enemy.Id), Is.False); Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test] public void TrialImpactSuppressesSnapshotFallbackAndMortarGlobIdsStayIndependent()
        {
            theme.Data.Shapes.Add(Shape("fx.mortar.projectile", Vector2.zero, Vector2.up));
            var shell = new MortarProjectile();
            void Set(string name, object value) => typeof(MortarProjectile).GetProperty(name).SetValue(shell, value);
            Set(nameof(MortarProjectile.Id), 1); Set(nameof(MortarProjectile.From), new Point2(10, 10));
            Set(nameof(MortarProjectile.To), new Point2(20, 20)); Set(nameof(MortarProjectile.Duration), 1f); Set(nameof(MortarProjectile.Remaining), 1f);
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.ProjectileLaunched, shell.From, shell.To, StructureKind.Mortar, entityId: 1, travelSeconds: 1, simulationSeconds: .01);
            events.Publish(1, SimulationEventKind.EnemyGlobLaunched, shell.From, shell.To, default, entityId: 1, travelSeconds: 1, simulationSeconds: .01);
            events.Publish(2, SimulationEventKind.ProjectileImpact, shell.From, shell.To, StructureKind.Mortar, entityId: 1, simulationSeconds: .05);
            var playback = new NotebookTrialPlayback();
            playback.Bind(Recording(Frame(0, projectiles: new[] { shell }), Frame(1, events: events.ToArray()))); playback.Seek(.06, theme);
            Assert.That(playback.IsProjectileEnded(1), Is.True); Assert.That(playback.IsProjectileEnded(1, true), Is.False);
            Assert.That(playback.Projectiles.Select(item => item.Kind), Is.EqualTo(new[] { SimulationEventKind.EnemyGlobLaunched }));
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh); NotebookTrialRenderer.DrawProjectiles(pen, playback, theme); pen.Upload();
                Assert.That(mesh.vertexCount, Is.EqualTo(24), "Only the eight-triangle glob fallback remains; the stale mortar cannot be redrawn.");
                playback.Seek(1, theme); Assert.That(playback.IsProjectileEnded(1), Is.False);
                playback.Seek(0, theme); Assert.That(playback.IsProjectileEnded(1), Is.False);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test] public void WorldAndTrialRicochetContinuationStartsAtThePreviousVictimWhileTheFirstSegmentUsesTheMuzzle()
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000;
            var blueprint = new TowerBlueprint("bounce", "Bounce fixture", ChassisKind.Standard, StructureKind.Ricochet);
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, blueprint, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            var tower = game.Structures[0]; Point2 source = new Point2(10.5f, 10.5f), victim = new Point2(16, 12), nextVictim = new Point2(18, 14);
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.Shot, source, victim, StructureKind.Ricochet, sourceStructureId: tower.Id, mountSlotIndex: 0, actionId: 1);
            events.Publish(1, SimulationEventKind.Shot, victim, nextVictim, StructureKind.Ricochet, sourceStructureId: tower.Id, mountSlotIndex: 0, actionId: 1);
            var root = new GameObject("Ricochet segment renderer"); var mesh = new Mesh();
            try
            {
                var view = root.AddComponent<NotebookWorldView>(); view.Initialize(new RunReadModel(game)); view.AddEvent(events[0]);
                var draw = typeof(NotebookWorldView).GetMethod("DrawEffects", BindingFlags.Instance | BindingFlags.NonPublic);
                draw.Invoke(view, null);
                var effectPen = (MeshPen)typeof(NotebookWorldView).GetField("effectsPen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var worldMesh = (Mesh)typeof(MeshPen).GetField("mesh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effectPen);
                Vector3 firstFrom = (worldMesh.vertices[0] + worldMesh.vertices[3]) * .5f;
                Assert.That(Vector2.Distance(firstFrom, new Vector2(source.X, source.Y)), Is.GreaterThan(.1f));
                view.Bind(new RunReadModel(game)); view.AddEvent(events[1]); draw.Invoke(view, null);
                Vector3 bounceFrom = (worldMesh.vertices[0] + worldMesh.vertices[3]) * .5f;
                Assert.That(bounceFrom.x, Is.EqualTo(victim.X).Within(.00001)); Assert.That(bounceFrom.y, Is.EqualTo(victim.Y).Within(.00001));
                var playback = new NotebookTrialPlayback();
                playback.Bind(Recording(Frame(0, towers: new[] { tower }, events: new[] { events[1] }))); playback.Seek(0, theme);
                var pen = new MeshPen(mesh); NotebookTrialRenderer.DrawEffects(pen, playback, theme); pen.Upload();
                Vector3 trialFrom = (mesh.vertices[0] + mesh.vertices[3]) * .5f;
                Assert.That(trialFrom.x, Is.EqualTo(victim.X).Within(.00001)); Assert.That(trialFrom.y, Is.EqualTo(victim.Y).Within(.00001));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }

        [TestCase("well")]
        [TestCase("blot")]
        [TestCase("glob")]
        [TestCase("mortar")]
        public void AssignedThemePaletteReachesWorldWellsBlotsAndProjectileMeshes(string path)
        {
            var sourceTheme = NotebookVisualThemeAsset.Default;
            Assert.That(sourceTheme, Is.Not.Null, "The regression must exercise the actual authored resource art.");
            var originalPalette = (Color[])sourceTheme.Data.Palette.Clone();
            var customTheme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            var root = new GameObject("Assigned theme renderer"); var mesh = new Mesh();
            try
            {
                customTheme.ReplaceData(JsonUtility.FromJson<NotebookArtSource>(JsonUtility.ToJson(sourceTheme.Data)));
                Assert.That(customTheme.Data.Palette, Is.Not.SameAs(sourceTheme.Data.Palette));
                Color overrideInk = new Color(.913f, .071f, .827f, 1);
                for (int channel = 0; channel < customTheme.Data.Palette.Length; channel++) customTheme.Data.Palette[channel] = overrideInk;
                var balance = GameBalance.CreateDefault(); balance.EnableInkWells = true;
                var game = new GameSession(balance, starterDefenses: false);
                const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                void Set(object item, string property, object value) => item.GetType().GetProperty(property).SetValue(item, value);
                if (path == "blot")
                {
                    var blots = typeof(GameSession).GetMethod("GetCorpseBlots", privateInstance).Invoke(game, null);
                    blots.GetType().GetMethod("Add", privateInstance).Invoke(blots, new object[] { new Cell(10, 10), 5f });
                }
                else if (path == "glob")
                {
                    var glob = new EnemyGlob(); Set(glob, "Id", 1); Set(glob, "From", new Point2(10, 10));
                    Set(glob, "To", new Point2(14, 10)); Set(glob, "Duration", 1f); Set(glob, "Remaining", .5f);
                    ((List<EnemyGlob>)typeof(GameSession).GetField("enemyGlobs", privateInstance).GetValue(game)).Add(glob);
                }
                else if (path == "mortar")
                {
                    var projectile = new MortarProjectile(); Set(projectile, "Id", 1); Set(projectile, "From", new Point2(10, 10));
                    Set(projectile, "To", new Point2(14, 10)); Set(projectile, "Duration", 1f); Set(projectile, "Remaining", .5f);
                    Set(projectile, "Radius", 1f);
                    var weapons = typeof(GameSession).GetField("weapons", privateInstance).GetValue(game);
                    ((List<MortarProjectile>)weapons.GetType().GetField("projectiles", privateInstance).GetValue(weapons)).Add(projectile);
                }
                var readModel = new RunReadModel(game);
                var view = root.AddComponent<NotebookWorldView>(); view.VisualTheme = customTheme; view.Initialize(readModel);
                Mesh rendered;
                if (path == "well")
                {
                    Assert.That(readModel.Wells, Is.Not.Empty);
                    var pen = new MeshPen(mesh);
                    typeof(NotebookWorldView).GetMethod("DrawWell", privateInstance).Invoke(view, new object[] { pen, readModel.Wells[0] });
                    pen.Upload(); rendered = mesh;
                }
                else
                {
                    typeof(NotebookWorldView).GetMethod("DrawEffects", privateInstance).Invoke(view, null);
                    var pen = (MeshPen)typeof(NotebookWorldView).GetField("effectsPen", privateInstance).GetValue(view);
                    rendered = (Mesh)typeof(MeshPen).GetField("mesh", privateInstance).GetValue(pen);
                }
                Color32 expected = overrideInk;
                Assert.That(rendered.colors32.Count(color => color.r == expected.r && color.g == expected.g && color.b == expected.b),
                    Is.GreaterThan(0), path + " resource vertices must use the assigned theme palette.");
                Assert.That(sourceTheme.Data.Palette, Is.EqualTo(originalPalette), "The resource theme must remain unchanged.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); Object.DestroyImmediate(customTheme); }
        }

#if UNITY_EDITOR
        [Test] public void AuthoredBatteryRecoilTransformsSavedLinesRigidlyAndNeverRebuildsOrReassessesThem()
        {
            var configured = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/TargetNotebookBalance.asset");
            Assert.That(configured, Is.Not.Null);
            var balance = configured.CreateBalance();
            var drawing = new DrawingSnapshot(new[] {
                new DrawingPart("mount.0", new[] { new DrawingStroke(new[] { new DrawingPoint(-.1f, .03f), new DrawingPoint(.04f, .31f) }) }),
                new DrawingPart("mount.1", new[] { new DrawingStroke(new[] { new DrawingPoint(.1f, -.02f), new DrawingPoint(-.02f, .26f) }) }),
                new DrawingPart("mount.2", new[] { new DrawingStroke(new[] { new DrawingPoint(0, 0), new DrawingPoint(.09f, .37f) }) }) });
            var blueprint = new TowerBlueprint("authored-battery", "Own lines", "chassis.battery", new[] {
                new WeaponMountSelection(0, "weapon.gun"), new WeaponMountSelection(1, "weapon.gun"), new WeaponMountSelection(2, "weapon.gun") },
                drawing: drawing, origin: DrawingOrigin.PlayerDrawing, content: balance.Content);
            string hash = drawing.GeometryHash; var stamp = blueprint.EvaluationStamp;
            var state = new NotebookAnimationState(); var events = new SimulationEventBuffer();
            for (int slot = 0; slot < 3; slot++) Action(events, 1, slot, slot + 1, 1 + slot * .05);
            foreach (var item in events) state.AddEvent(item);
            AuthoredGeometryCache.Clear(); var prepared = AuthoredGeometryCache.For(drawing);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                NotebookTowerComposition.DrawMounts(pen, blueprint, new Vector2(10, 20), MaterialTier.Ink, InkColor.None,
                    default, false, state, 1, 1.12, theme: theme); pen.Upload();
                var vertices = mesh.vertices; int index = 0;
                for (int slot = 0; slot < 3; slot++)
                {
                    Point2 pivot = DrawingTemplateCatalog.Pivot(blueprint, "mount." + slot);
                    foreach (var vertex in prepared.GetPart("mount." + slot).Vertices)
                    {
                        Assert.That(vertices[index].x, Is.EqualTo(10 + pivot.X + vertex.x).Within(.00001));
                        Assert.That(vertices[index].y, Is.EqualTo(20 + pivot.Y + vertex.y + state.Recoil(1, slot, 1.12, theme)).Within(.00001));
                        index++;
                    }
                }
                for (int frame = 0; frame < 20; frame++)
                {
                    pen.Clear(); NotebookTowerComposition.DrawMounts(pen, blueprint, Vector2.zero, MaterialTier.Ink, InkColor.None,
                        default, false, state, 1, 1.12 + frame * .01, theme: theme);
                }
                Assert.That(AuthoredGeometryCache.BuildCount, Is.EqualTo(1));
                Assert.That(drawing.GeometryHash, Is.EqualTo(hash)); Assert.That(blueprint.EvaluationStamp, Is.SameAs(stamp));
                Assert.That(vertices.Length, Is.EqualTo(12), "Empty base and decorative layers remain empty.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }
#endif
    }
}
