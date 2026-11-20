using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class StandardMountCompatibilityTests
    {
        static DrawingStroke Stroke(params DrawingPoint[] points) => new DrawingStroke(points);

        static DrawingSnapshot Drawing(float length = .625f, bool crooked = true) => new DrawingSnapshot(new[] {
            new DrawingPart(DrawingBindings.Base, new[] { Stroke(new DrawingPoint(-.375f, -.375f),
                new DrawingPoint(.375f, -.375f), new DrawingPoint(.375f, .375f),
                new DrawingPoint(-.375f, .375f), new DrawingPoint(-.375f, -.375f)) }),
            new DrawingPart(DrawingBindings.Mount0, new[] { crooked
                ? Stroke(new DrawingPoint(-.0625f, 0), new DrawingPoint(.125f, .25f), new DrawingPoint(.0625f, length))
                : Stroke(new DrawingPoint(0, 0), new DrawingPoint(.0625f, length)) })
        });

        static TowerBlueprint Authored(DrawingOrigin origin, float length = .625f, bool crooked = true)
            => new TowerBlueprint("historical.mount", "Historical mount", ChassisKind.Standard, StructureKind.Gun,
                drawing: Drawing(length, crooked), origin: origin);

        static StructureView Build(TowerBlueprint blueprint, GameBalance balance = null)
        {
            balance = balance ?? GameBalance.CreateDefault();
            balance.StartingInk = 10000;
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, blueprint,
                MaterialTier.Pencil, InkColor.None, out string reason, facingDegrees: 90), Is.True, reason);
            game.Structures[0].IsComplete = true;
            game.Structures[0].BuildRemaining = 0;
            return new RunReadModel(game).Structures[0];
        }

        static string Saved(TowerBlueprint blueprint) => JsonUtility.ToJson(new BlueprintData(blueprint));

        static void AssertPoint(Vector2 actual, float x, float y)
        {
            Assert.That(actual.x, Is.EqualTo(x).Within(.00001f));
            Assert.That(actual.y, Is.EqualTo(y).Within(.00001f));
        }

        static void AssertStroke(Mesh mesh, int first, Vector2 from, Vector2 to)
        {
            var vertices = mesh.vertices;
            AssertPoint((vertices[first] + vertices[first + 3]) * .5f, from.x, from.y);
            AssertPoint((vertices[first + 1] + vertices[first + 2]) * .5f, to.x, to.y);
        }

        static void AssertNativeStrokes(Mesh mesh)
        {
            var atlas = new List<Vector2>();
            mesh.GetUVs(1, atlas);
            Assert.That(atlas.Count, Is.EqualTo(mesh.vertexCount));
            Assert.That(atlas, Is.All.EqualTo(NotebookSpriteCatalog.WhiteUV),
                "Historical strokes must not be replaced with standard sprite artwork.");
        }

        static void Populate(WeaponAssemblyGraphic graphic, VertexHelper target)
            => typeof(WeaponAssemblyGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null)
                .Invoke(graphic, new object[] { target });

        [TestCase(DrawingOrigin.PlayerDrawing)]
        [TestCase(DrawingOrigin.Freeform)]
        public void HistoricalBodyAndCrookedBarrelKeepTheirSavedLinesInWorldAndAlbum(DrawingOrigin origin)
        {
            var blueprint = Authored(origin);
            string saved = Saved(blueprint);
            var mesh = new Mesh();
            var root = new GameObject("Historical assembly compatibility", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(WeaponAssemblyGraphic));
            try
            {
                var pen = new MeshPen(mesh);
                NotebookTowerComposition.DrawStatic(pen, blueprint, new Vector2(10, 20), MaterialTier.Pencil, InkColor.None);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.EqualTo(16));
                AssertStroke(mesh, 0, new Vector2(9.625f, 19.625f), new Vector2(10.375f, 19.625f));
                AssertStroke(mesh, 4, new Vector2(10.375f, 19.625f), new Vector2(10.375f, 20.375f));
                AssertNativeStrokes(mesh);

                pen.Clear();
                NotebookTowerComposition.DrawMounts(pen, blueprint, new Vector2(10, 20), MaterialTier.Pencil,
                    InkColor.None, default, false, null, 0, 0, overrideAim: _ => new Point2(20, 20.04f));
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.EqualTo(8));
                AssertStroke(mesh, 0, new Vector2(10, 20.1025f), new Vector2(10.25f, 19.915f));
                AssertStroke(mesh, 4, new Vector2(10.25f, 19.915f), new Vector2(10.625f, 19.9775f));
                AssertNativeStrokes(mesh);

                var graphic = root.GetComponent<WeaponAssemblyGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(300, 200);
                graphic.SetBlueprint(blueprint);
                using (var target = new VertexHelper())
                {
                    Populate(graphic, target);
                    Assert.That(target.currentVertCount, Is.EqualTo(24), "All six saved line quads must reach the album preview.");
                    var vertex = new UIVertex();
                    for (int i = 0; i < target.currentVertCount; i++)
                    {
                        target.PopulateUIVertex(ref vertex, i);
                        Assert.That((Vector2)vertex.uv0, Is.EqualTo(NotebookSpriteCatalog.WhiteUV));
                    }
                }
                Assert.That(Saved(blueprint), Is.EqualTo(saved));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }

        static NotebookVisualThemeAsset RecoilTheme()
        {
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            theme.Data.Recoils.Add(new NotebookWeaponRecoil { WeaponId = "weapon.gun", Muzzle = new Vector2(.125f, .875f),
                Duration = .2f, Distance = .1f, Keys = new List<NotebookRecoilKey> {
                    new NotebookRecoilKey { Time = 0, Value = 1 }, new NotebookRecoilKey { Time = .2f, Value = 0 } } });
            return theme;
        }

        static NotebookAnimationState Recoiling(StructureView tower)
        {
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.WeaponActionStarted, default, new Point2(20, 10.54f), StructureKind.Gun,
                sourceStructureId: tower.Id, mountSlotIndex: 0, actionId: 1, simulationSeconds: 1);
            var animation = new NotebookAnimationState();
            animation.AddEvent(events[0]);
            return animation;
        }

        [Test]
        public void HistoricalPlayerDrawingMuzzleKeepsItsThemeOffsetAimAndRecoil()
        {
            var tower = Build(Authored(DrawingOrigin.PlayerDrawing));
            var theme = RecoilTheme();
            try
            {
                Vector2 muzzle = NotebookTowerComposition.Muzzle(tower, 0, new Point2(20, 10.54f), 1.1, Recoiling(tower), theme);
                // Mount (10.5, 10.54), aimed right: forward .875 - .05 recoil, side -.125.
                AssertPoint(muzzle, 11.325f, 10.415f);
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [TestCase(.25f)]
        [TestCase(.75f)]
        public void FreeformMuzzleKeepsTheDrawnTipOffsetWhenAimedAndRecoiling(float length)
        {
            var blueprint = Authored(DrawingOrigin.Freeform, length, crooked: false);
            var tower = Build(blueprint);
            string saved = Saved(blueprint);
            var theme = RecoilTheme();
            try
            {
                Vector2 muzzle = NotebookTowerComposition.Muzzle(tower, 0, new Point2(20, 10.54f), 1.1, Recoiling(tower), theme);
                // The authored tip (.0625, length) overrides the catalog muzzle (.125, .875).
                AssertPoint(muzzle, 10.45f + length, 10.4775f);
                Assert.That(Saved(blueprint), Is.EqualTo(saved));
            }
            finally { Object.DestroyImmediate(theme); }
        }

        static GameBalance SparseBalance()
        {
            var balance = GameBalance.CreateDefault();
            var input = LegacyContentCatalog.CreateInput(balance);
            input.Templates.Add(new DrawingTemplateDefinitionInput { Id = "template.compatibility", DisplayName = "Compatibility", Weight = 0 });
            foreach (var part in input.Chassis) part.TemplateId = "template.compatibility";
            foreach (var part in input.Weapons) part.TemplateId = "template.compatibility";
            foreach (var part in input.Modules) part.TemplateId = "template.compatibility";
            var chassis = input.Chassis.Find(part => part.Id == "chassis.heavy");
            chassis.MountCount = 3;
            chassis.Capacity = 100;
            input.Weapons.Find(part => part.Id == "weapon.gun").Stats.Range = 40;
            input.Weapons.Find(part => part.Id == "weapon.ricochet").Stats.Range = 40;
            balance.Content = input.CreateSnapshot();
            balance.Trials.MaximumTicks = 90;
            balance.Trials.For(TrialScenario.Crowd).Count = 4;
            balance.Trials.For(TrialScenario.Crowd).Health = 1000;
            return balance;
        }

        static float[] Numbers(TowerBlueprint blueprint, GameBalance balance)
        {
            float feed = TowerCatalog.FeedMultiplier(blueprint, balance);
            var numbers = new List<float> { TowerCatalog.Cost(blueprint, balance, MaterialTier.Pencil),
                TowerCatalog.Health(blueprint, balance, MaterialTier.Pencil), TowerCatalog.BuildSeconds(blueprint, balance, MaterialTier.Pencil),
                TowerCatalog.Load(blueprint, balance), TowerCatalog.Capacity(blueprint, balance), feed,
                TowerCatalog.AcquisitionSeconds(blueprint, balance) };
            foreach (var mount in blueprint.WeaponMounts)
            {
                numbers.Add(TowerCatalog.WeaponRange(blueprint, balance, mount.WeaponId, mount.SlotIndex));
                numbers.Add(TowerCatalog.WeaponDamage(blueprint, balance, mount.WeaponId, MaterialTier.Pencil, mount.SlotIndex));
                numbers.Add(TowerCatalog.ActionInterval(blueprint, balance, mount.WeaponId, feed, mount.SlotIndex));
            }
            return numbers.ToArray();
        }

        [Test]
        public void SparseStandardBatteryRoundTripAndEveryRendererPreserveRecipeAndCombatNumbers()
        {
            var balance = SparseBalance();
            var original = new TowerBlueprint("standard.sparse.saved", "Sparse saved battery", "chassis.heavy", new[] {
                new WeaponMountSelection(2, "weapon.ricochet"), new WeaponMountSelection(0, "weapon.gun") },
                new[] { new ModuleSelection("saved.tracking", "module.tracking"), new ModuleSelection("saved.feed", "module.feed") },
                content: balance.Content);
            string saved = Saved(original);
            float[] numbers = Numbers(original, balance);
            int budget = TowerCatalog.Cost(original, balance, MaterialTier.Pencil);
            var expected = TrialRunner.Run(original, balance, TrialScenario.Crowd, budget, seed: 7);
            Assert.That(expected.Damage, Is.GreaterThan(0), "The fixture must exercise actual combat.");
            var restored = JsonUtility.FromJson<BlueprintData>(saved).ToBlueprint().WithContent(balance.Content);
            Assert.That(Saved(restored), Is.EqualTo(saved));
            Assert.That(restored.Id, Is.EqualTo("standard.sparse.saved"));
            Assert.That(restored.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(restored.Drawing.IsEmpty, Is.True);
            Assert.That(restored.WeaponMounts.Select(mount => mount.SlotIndex), Is.EqualTo(new[] { 0, 2 }));
            Assert.That(restored.WeaponMounts.Select(mount => mount.WeaponId), Is.EqualTo(new[] { "weapon.gun", "weapon.ricochet" }));
            Assert.That(restored.ModuleSelections.Select(module => module.InstanceId), Is.EqualTo(new[] { "saved.tracking", "saved.feed" }));
            Assert.That(restored.GetWeaponMount(1), Is.Null);

            var root = new GameObject("Sparse standard compatibility", typeof(RectTransform), typeof(CanvasRenderer), typeof(WeaponAssemblyGraphic));
            var mesh = new Mesh();
            try
            {
                var graphic = root.GetComponent<WeaponAssemblyGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(300, 200);
                graphic.SetBlueprint(restored);
                using (var target = new VertexHelper())
                {
                    Populate(graphic, target);
                    Assert.That(target.currentVertCount, Is.GreaterThan(0));
                    target.Clear();
                    NotebookTowerComposition.DrawPreviewUI(target, restored, Vector2.zero, 50, new NotebookAnimationState(), 1, 0,
                        aim: slot => slot == 2 ? new Point2(-10, 10) : new Point2(10, 10));
                    Assert.That(target.currentVertCount, Is.GreaterThan(0));
                }
                Assert.That(Saved(restored), Is.EqualTo(saved));
                Assert.That(Numbers(restored, balance), Is.EqualTo(numbers));

                var pen = new MeshPen(mesh);
                var tower = Build(restored, balance);
                DoodleGlyphs.StructureBase(pen, tower);
                DoodleGlyphs.StructureDynamic(pen, tower, 1);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(Saved(tower.Blueprint), Is.EqualTo(saved));
                Assert.That(Numbers(restored, balance), Is.EqualTo(numbers));

                var actual = TrialRunner.Run(restored, balance, TrialScenario.Crowd, budget, seed: 7);
                var playback = new NotebookTrialPlayback();
                playback.Bind(actual);
                playback.Seek(.5);
                NotebookTrialRenderer.Draw(pen, playback);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(actual.Frames.SelectMany(frame => frame.Events)
                    .Where(item => item.Kind == SimulationEventKind.WeaponActionStarted)
                    .Select(item => item.MountSlotIndex).Distinct().OrderBy(slot => slot), Is.EqualTo(new[] { 0, 2 }));
                Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)));
                Assert.That(actual.DrawingQuality, Is.EqualTo(expected.DrawingQuality));
                Assert.That(actual.Frames.SelectMany(frame => frame.Events), Is.EqualTo(expected.Frames.SelectMany(frame => frame.Events)));
                Assert.That(actual.Frames.SelectMany(frame => frame.Shots), Is.EqualTo(expected.Frames.SelectMany(frame => frame.Shots)));
                foreach (var frameTower in actual.Frames.SelectMany(frame => frame.Towers))
                    Assert.That(Saved(frameTower.Blueprint), Is.EqualTo(saved));
                Assert.That(Saved(restored), Is.EqualTo(saved));
                Assert.That(Saved(original), Is.EqualTo(saved));
                Assert.That(Numbers(restored, balance), Is.EqualTo(numbers));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }

        [TestCase(DrawingOrigin.LegacyStandard)]
        [TestCase(DrawingOrigin.PlayerDrawing)]
        [TestCase(DrawingOrigin.Freeform)]
        public void RicochetContinuationKeepsItsRecordedOriginInsteadOfReturningToTheMuzzle(DrawingOrigin origin)
        {
            var blueprint = origin == DrawingOrigin.LegacyStandard
                ? new TowerBlueprint("standard.bounce", "Standard bounce", ChassisKind.Standard, StructureKind.Ricochet)
                : Authored(origin);
            var tower = Build(blueprint);
            var theme = RecoilTheme();
            try
            {
                var aim = new Point2(18, 14);
                var animation = Recoiling(tower);
                AssertPoint(NotebookTowerComposition.SegmentOrigin(tower, 0, aim, new Vector2(16.125f, 12.375f), 1.1, animation, theme),
                    16.125f, 12.375f);
                AssertPoint(NotebookTowerComposition.SegmentOrigin(tower, 0, aim, new Vector2(10.501f, 10.5f), 1.1, animation, theme),
                    10.501f, 10.5f);
                var source = new Vector2(10.5f, 10.5f);
                Vector2 first = NotebookTowerComposition.SegmentOrigin(tower, 0, aim, source, 1.1, animation, theme);
                Assert.That(Vector2.Distance(first, source), Is.GreaterThan(.1f),
                    "Only the tower-center sentinel should be replaced with a muzzle position.");
            }
            finally { Object.DestroyImmediate(theme); }
        }

        static GameBalance PlayerBalance()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(asset, Is.Not.Null);
            var balance = asset.CreateWorkshopBalance();
            Assert.That(balance.ResolveContent().GetChassis("chassis.battery").Enabled, Is.True,
                "The resolved PlayerRules overrides must enable the production battery.");
            return balance;
        }

        static TowerBlueprint PlayerBattery(GameBalance balance)
            => new TowerBlueprint("production.mount-battery", "Production mount battery", "chassis.battery", new[] {
                new WeaponMountSelection(0, "weapon.gun"), new WeaponMountSelection(1, "weapon.gun"),
                new WeaponMountSelection(2, "weapon.gun") }, content: balance.ResolveContent());

        static GameSession BuildPlayerSession(TowerBlueprint blueprint, GameBalance balance, float? facing = null)
        {
            var buildBalance = balance.CreateSnapshot();
            buildBalance.StartingInk = 10000;
            var game = new GameSession(buildBalance, starterDefenses: false);
            for (int y = 2; y < buildBalance.Height - 2; y++)
                for (int x = 2; x < buildBalance.Width - 2; x++)
                    if (game.TryBuildBlueprint(new[] { new Cell(x, y) }, blueprint, MaterialTier.Pencil, InkColor.None, out _, facing))
                    {
                        game.Structures[0].IsComplete = true;
                        game.Structures[0].BuildRemaining = 0;
                        return game;
                    }
            Assert.Fail("The production battlefield must contain a legal cell for this assembly.");
            return null;
        }

        static Vector2 NativeBarrelTip(Mesh mesh, Vector2 forward, int first = 0, int count = -1)
        {
            var vertices = mesh.vertices; var colors = mesh.colors32;
            Color32 ink = NightNotebookArt.Ink;
            var side = new Vector2(forward.y, -forward.x);
            float furthest = float.NegativeInfinity;
            int end = count < 0 ? vertices.Length : first + count;
            for (int i = first; i < end; i++)
                if (colors[i].Equals(ink)) furthest = Mathf.Max(furthest, Vector2.Dot(vertices[i], forward));
            Assert.That(float.IsNegativeInfinity(furthest), Is.False, "The standard barrel must have a visible structural outline.");
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int i = first; i < end; i++)
                if (colors[i].Equals(ink) && Mathf.Abs(Vector2.Dot(vertices[i], forward) - furthest) < .00001f)
                { float across = Vector2.Dot(vertices[i], side); low = Mathf.Min(low, across); high = Mathf.Max(high, across); }
            return forward * furthest + side * ((low + high) * .5f);
        }

        static NotebookVisualThemeAsset NeutralRecoilTheme()
        {
            var theme = RecoilTheme();
            theme.Data.Palette = NotebookVisualThemeAsset.Default.Data.Palette;
            theme.Data.Recoils[0].Muzzle = new Vector2(0, .35f);
            return theme;
        }

        [TestCase(5f, 0f, .04f, -.08f)]
        [TestCase(0f, -5f, .12f, -.04f)]
        public void ProductionGunMuzzleMatchesTheStandardNativeBarrelTipIncludingAimAndRecoil(float dx, float dy, float age, float recoil)
        {
            var balance = PlayerBalance();
            var blueprint = new TowerBlueprint("production.gun-tip", "Production gun tip", "chassis.standard", "weapon.gun",
                content: balance.ResolveContent());
            var game = BuildPlayerSession(blueprint, balance, facing: 90);
            var state = game.Structures[0];
            Vector2 origin = new Vector2(state.Cell.X + .5f, state.Cell.Y + .5f);
            var local = DrawingTemplateCatalog.Pivot(blueprint, DrawingBindings.Mount0);
            var aim = new Point2(origin.x + local.X + dx, origin.y + local.Y + dy);
            state.GetMountState(0).Aim = aim;
            var tower = new RunReadModel(game).Structures[0];
            var theme = NeutralRecoilTheme();
            var mesh = new Mesh();
            try
            {
                var forward = new Vector2(dx, dy).normalized;
                var pen = new MeshPen(mesh);
                DoodleGlyphs.StructureDynamic(pen, tower, 1, 1 + age, null, theme); pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                AssertNativeStrokes(mesh);
                Vector2 restingTip = NativeBarrelTip(mesh, forward);
                var animation = Recoiling(tower);
                pen.Clear();
                DoodleGlyphs.StructureDynamic(pen, tower, 1, 1 + age, animation, theme); pen.Upload();
                Vector2 renderedTip = NativeBarrelTip(mesh, forward);
                Vector2 muzzle = NotebookTowerComposition.Muzzle(tower, 0, aim, 1 + age, animation, theme);
                Assert.That(Vector2.Distance(muzzle, renderedTip), Is.LessThan(.00001f),
                    "Effects must begin at the actual native barrel endpoint after aiming and recoil.");
                Assert.That(Vector2.Distance(renderedTip - restingTip, forward * recoil), Is.LessThan(.00001f));
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void ProductionBatteryKeepsIndependentRecoilAttachedToAllThreeLogicalSlots()
        {
            var blueprint = PlayerBattery(PlayerBalance());
            var theme = NeutralRecoilTheme();
            var restMesh = new Mesh(); var recoilingMesh = new Mesh();
            try
            {
                var events = new SimulationEventBuffer();
                var animation = new NotebookAnimationState();
                for (int slot = 0; slot < 3; slot++)
                    events.Publish(1, SimulationEventKind.WeaponActionStarted, default, default, StructureKind.Gun,
                        sourceStructureId: 74, mountSlotIndex: slot, actionId: 100 + slot, simulationSeconds: 1 + slot * .05);
                foreach (var item in events) animation.AddEvent(item);
                var origin = new Vector2(5, 6);
                var restPen = new MeshPen(restMesh);
                var movedPen = new MeshPen(recoilingMesh);
                NotebookTowerComposition.DrawMounts(restPen, blueprint, origin, MaterialTier.Pencil, InkColor.None,
                    default, false, null, 74, 1.12, theme: theme); restPen.Upload();
                NotebookTowerComposition.DrawMounts(movedPen, blueprint, origin, MaterialTier.Pencil, InkColor.None,
                    default, false, animation, 74, 1.12, theme: theme); movedPen.Upload();
                Assert.That(blueprint.WeaponMounts.Select(mount => mount.SlotIndex), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(recoilingMesh.vertexCount, Is.EqualTo(restMesh.vertexCount));
                Assert.That(restMesh.vertexCount, Is.GreaterThan(0));
                Assert.That(restMesh.vertexCount % 3, Is.Zero);
                int perMount = restMesh.vertexCount / 3;
                var still = restMesh.vertices; var moved = recoilingMesh.vertices;
                float[] distances = { -.04f, -.065f, -.09f };
                for (int slot = 0; slot < 3; slot++)
                {
                    var pivot = DrawingTemplateCatalog.Pivot(blueprint, DrawingBindings.Mount(slot));
                    var restTip = NativeBarrelTip(restMesh, Vector2.up, slot * perMount, perMount);
                    Assert.That(Vector2.Distance(restTip, origin + new Vector2(pivot.X, pivot.Y + .35f)), Is.LessThan(.00001f));
                    for (int vertex = slot * perMount; vertex < (slot + 1) * perMount; vertex++)
                        Assert.That(Vector2.Distance(moved[vertex] - still[vertex], Vector2.up * distances[slot]), Is.LessThan(.00001f),
                            "Each emitted vertex must retain recoil from logical slot " + slot);
                }
                AssertNativeStrokes(recoilingMesh);
            }
            finally { Object.DestroyImmediate(restMesh); Object.DestroyImmediate(recoilingMesh); Object.DestroyImmediate(theme); }
        }
        [Test]
        public void ProductionBatterySaveLoadAndRenderingPreserveAllThreeCoreShotSlots()
        {
            var balance = PlayerBalance();
            var original = PlayerBattery(balance);
            Assert.That(NotebookTowerComposition.TryGetBounds(original, out _), Is.True);
            var profile = new WorkshopProfile(balance.ResolveContent(), balance);
            profile.GrantGold(1000);
            Assert.That(profile.Unlock("chassis.battery", out string reason, balance), Is.True, reason);
            Assert.That(profile.SaveBlueprint(original, out reason, balance), Is.True, reason);
            Assert.That(profile.AssignSlot(3, original.Id, out reason, balance), Is.True, reason);
            string savedProfile = JsonUtility.ToJson(profile.ToData());
            string savedPage = Saved(original);
            float[] numbers = Numbers(original, balance);
            int budget = TowerCatalog.Cost(original, balance, MaterialTier.Pencil);
            var expected = TrialRunner.Run(original, balance, TrialScenario.Crowd, budget, seed: 7);
            string path = Path.Combine(Path.GetTempPath(), "PaperMountCompatibility-" + Guid.NewGuid().ToString("N") + ".json");
            var root = new GameObject("Production battery compatibility", typeof(RectTransform), typeof(CanvasRenderer), typeof(WeaponAssemblyGraphic));
            var mesh = new Mesh();
            try
            {
                var store = new WorkshopProfileStore(path);
                Assert.That(store.Save(profile).Success, Is.True, store.LastDiagnostic);
                var loaded = new WorkshopProfileStore(path).Load();
                Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Primary));
                loaded.Profile.ConfigureContent(balance.ResolveContent());
                var restored = loaded.Profile.GetLoadoutBlueprints()[3].WithContent(balance.ResolveContent());
                Assert.That(Saved(restored), Is.EqualTo(savedPage));
                Assert.That(NotebookTowerComposition.TryGetBounds(restored, out _), Is.True);
                var graphic = root.GetComponent<WeaponAssemblyGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(300, 200);
                graphic.SetBlueprint(restored);
                using (var target = new VertexHelper())
                {
                    Populate(graphic, target);
                    Assert.That(target.currentVertCount, Is.GreaterThan(0));
                }
                var pen = new MeshPen(mesh);
                var tower = new RunReadModel(BuildPlayerSession(restored, balance)).Structures[0];
                DoodleGlyphs.StructureBase(pen, tower);
                DoodleGlyphs.StructureDynamic(pen, tower, 1);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));

                var actual = TrialRunner.Run(restored, balance, TrialScenario.Crowd, budget, seed: 7);
                var shots = actual.Frames.SelectMany(frame => frame.Events).Where(item => item.Kind == SimulationEventKind.Shot).ToArray();
                Assert.That(shots.Select(item => item.MountSlotIndex).Distinct().OrderBy(slot => slot), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(shots.All(item => item.SourceStructureId == 1 && item.WeaponId == "weapon.gun"), Is.True);
                var playback = new NotebookTrialPlayback();
                playback.Bind(actual);
                playback.Seek(actual.Frames.First(frame => frame.Events.Any(item => item.Kind == SimulationEventKind.Shot)).Seconds);
                NotebookTrialRenderer.Draw(pen, playback);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(actual.Frames.SelectMany(frame => frame.Events), Is.EqualTo(expected.Frames.SelectMany(frame => frame.Events)));
                Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)));
                Assert.That(Numbers(restored, balance), Is.EqualTo(numbers));
                Assert.That(Saved(restored), Is.EqualTo(savedPage));
                Assert.That(JsonUtility.ToJson(loaded.Profile.ToData()), Is.EqualTo(savedProfile));
                Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(savedProfile));
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(mesh);
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }
    }
}
