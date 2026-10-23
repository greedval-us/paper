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
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class NotebookTowerDefensePresentationTests
    {
        GameObject root;
        GameBalanceAsset asset;
        BalancePresetAsset transientPreset;
        RunController run;
        DrawingInput input;
        NotebookHud hud;

        void Open(bool main)
        {
            root = new GameObject("Memory-only tower defense presentation"); root.SetActive(false);
            asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/TargetNotebookBalance.asset"));
            if (!main)
            {
                Assert.That(asset.dataPreset, Is.Not.Null);
                transientPreset = Object.Instantiate(asset.dataPreset);
                transientPreset.rules = GlobalBalanceSettings.From(transientPreset.CreateBalance());
                transientPreset.rules.StructureDefinitions.RemoveAll(definition =>
                    definition.Kind == StructureKind.TarPit || definition.Kind == StructureKind.RepairPost);
                asset.dataPreset = transientPreset;
                asset.legacyAuthoringAdapter = false;
            }
            run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
            Assert.That(run.StartRun(out string reason), Is.True, reason);
            input = root.AddComponent<DrawingInput>(); input.Initialize(run, null, null);
            hud = root.AddComponent<NotebookHud>(); hud.Initialize(run, input);
        }
        [TearDown] public void Close()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (asset != null) Object.DestroyImmediate(asset);
            if (transientPreset != null) Object.DestroyImmediate(transientPreset);
        }
        Button Card(int index) => root.GetComponentsInChildren<Button>(true).Single(button => button.name == "Tool " + index);

        [Test] public void MainPresetShowsEightLoadoutAndEightToolCardsWithoutOverlapAndAllSixFamilies()
        {
            Open(true);
            Assert.That(run.Session.Battlefield, Is.Not.Null);
            Assert.That(run.Session.Battlefield.IsOpenTerrain, Is.True);
            foreach (int side in new[] { 1, 2, 4, 8 })
                Assert.That(run.Session.Battlefield.Entrances.Count(cell => run.Session.Battlefield.Side(cell) == side), Is.GreaterThanOrEqualTo(3));
            var theme = NotebookVisualThemeAsset.Default;
            string[] icons = { "wall", "spikes", "erase", "upgrade", "blotter", "paint", "tar", "repair" };
            foreach (string icon in icons) Assert.That(theme.FindShape("ui.tool." + icon), Is.Not.Null, icon);
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                foreach (DrawingTier tier in Enum.GetValues(typeof(DrawingTier)))
                    foreach (NotebookEnemyMotion motion in Enum.GetValues(typeof(NotebookEnemyMotion)))
                        Assert.That(theme.FindClip(NotebookAnimationState.EnemyId(kind, tier, motion)), Is.Not.Null, kind + "/" + tier + "/" + motion);
            var cards = Enumerable.Range(0, ProfileLimits.LoadoutSlotCount + icons.Length).Select(Card).ToArray();
            Assert.That(cards.All(card => card.gameObject.activeSelf), Is.True);
            var ordered = cards.Select(card => card.GetComponent<RectTransform>()).OrderBy(rect => rect.anchoredPosition.x).ToArray();
            for (int i = 0; i < ordered.Length; i++)
            {
                Assert.That(ordered[i].anchoredPosition.x, Is.GreaterThanOrEqualTo(0));
                Assert.That(ordered[i].anchoredPosition.x + ordered[i].rect.width, Is.LessThanOrEqualTo(1552));
                if (i > 0) Assert.That(ordered[i].anchoredPosition.x, Is.GreaterThan(ordered[i - 1].anchoredPosition.x + ordered[i - 1].rect.width));
            }
            Assert.That(cards.Take(ProfileLimits.LoadoutSlotCount).All(card => card.GetComponentInChildren<BlueprintSketch>(true) != null), Is.True);
            Assert.That(cards.Skip(ProfileLimits.LoadoutSlotCount).All(card => card.GetComponentInChildren<NotebookArtGraphic>(true) != null), Is.True);
            foreach (var card in cards)
            {
                var bounds = card.GetComponent<RectTransform>();
                foreach (var child in card.GetComponentsInChildren<RectTransform>(true).Where(child => child.parent == card.transform))
                {
                    Assert.That(child.anchoredPosition.x, Is.GreaterThanOrEqualTo(0), card.name + "/" + child.name);
                    Assert.That(child.anchoredPosition.x + child.rect.width, Is.LessThanOrEqualTo(bounds.rect.width), card.name + "/" + child.name);
                }
            }
            var navigation = (Text)typeof(NotebookHud).GetField("navigationHint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            Assert.That(navigation.text, Does.Contain("дёготь").And.Contain("ремонт"));
        }

        [TestCase(ProfileLimits.LoadoutSlotCount + 6, StructureKind.TarPit)]
        [TestCase(ProfileLimits.LoadoutSlotCount + 7, StructureKind.RepairPost)]
        [TestCase(ProfileLimits.LoadoutSlotCount, StructureKind.Wall)]
        [TestCase(ProfileLimits.LoadoutSlotCount + 1, StructureKind.Spikes)]
        public void NewToolCardSelectionPriceAndLegalPlacementAgreeWithCore(int index, StructureKind kind)
        {
            Open(true); Card(index).onClick.Invoke();
            Assert.That(input.Mode, Is.EqualTo(DrawingMode.Build)); Assert.That(input.Tool, Is.EqualTo(kind));
            Assert.That(input.BlueprintSlot, Is.EqualTo(-1));
            string description = (string)typeof(NotebookHud).GetMethod("BuildDescription", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            description = GameText.Resolve(hud.TextResolver, description);
            Assert.That(description, Does.Contain("Свободная клетка").And.Not.Contain("дорог"));
            var map = run.Session.Battlefield;
            var cell = Enumerable.Range(0, run.Session.Balance.Width * run.Session.Balance.Height)
                .Select(value => new Cell(value % run.Session.Balance.Width, value / run.Session.Balance.Width))
                .First(value => map.CanPlace(value, kind) && run.QuoteBuild(new[] { value }, kind, input.Tier).Valid);
            var quote = run.QuoteBuild(new[] { cell }, input.Tool, input.Tier);
            Assert.That(Card(index).GetComponentsInChildren<Text>(true).Any(text => text.text == quote.Cost + " черн."), Is.True);
            int before = run.Session.Ink;
            Assert.That(run.Build(new[] { cell }, input.Tool, input.Tier, out string reason), Is.True, reason);
            Assert.That(before - run.Session.Ink, Is.EqualTo(quote.Cost));
            Assert.That(run.Session.StructureAt(cell).Value.Kind, Is.EqualTo(kind));
            var invalid = map.Props[0].Cell;
            quote = run.QuoteBuild(new[] { invalid }, input.Tool, input.Tier);
            Assert.That(quote.Valid, Is.False);
            Assert.That(run.Build(new[] { invalid }, input.Tool, input.Tier, out reason), Is.False);
            Assert.That(reason, Is.EqualTo(quote.Reason));
        }

        [Test] public void PresetWithoutOptionalToolsHidesTheirCardsAndRejectsTheirSelection()
        {
            Open(false);
            Assert.That(run.Session.Balance.HasStructure(StructureKind.TarPit), Is.False);
            Assert.That(run.Session.Balance.HasStructure(StructureKind.RepairPost), Is.False);
            var navigation = (Text)typeof(NotebookHud).GetField("navigationHint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            Assert.That(navigation.text, Does.Not.Contain("дёготь").And.Not.Contain("ремонт"));
            foreach (int index in new[] { ProfileLimits.LoadoutSlotCount + 6, ProfileLimits.LoadoutSlotCount + 7 })
                Assert.That(Card(index).gameObject.activeSelf, Is.False);
            foreach (bool constructionOnly in new[] { false, true })
            {
                input.ConstructionOnly = constructionOnly;
                input.SelectTool(StructureKind.TarPit); input.SelectTool(StructureKind.RepairPost);
                Assert.That(input.Tool, Is.EqualTo(StructureKind.Wall));
            }
        }

        [TestCase(-2, 10, 0)] [TestCase(5, 10, .5f)] [TestCase(20, 10, 1)] [TestCase(5, 0, 0)]
        public void EnemyHpRatioIsClamped(float hp, float maximum, float expected)
            => Assert.That(DoodleGlyphs.EnemyHealthRatio(hp, maximum), Is.EqualTo(expected));

        static EnemyView Enemy(float scale) => (EnemyView)typeof(EnemyView).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
            .Invoke(new object[] { new EnemyState { Id = 1, Kind = EnemyKind.Brute, Health = 100, MaxHealth = 100, VisualScale = scale, Position = new Point2(10, 10) } });

        [TestCase(false)] [TestCase(true)]
        public void LiveAndCrowdEnemiesUseAtlasArtworkPreserveScaleAndRenderFullHealthBars(bool compact)
        {
            var mesh = new Mesh();
            try
            {
                Assert.That(NotebookSpriteCatalog.Available, Is.True);
                var pen = new MeshPen(mesh); DoodleGlyphs.Enemy(pen, Enemy(1), 0, compact); pen.Upload();
                var first = mesh.bounds;
                var atlas = new System.Collections.Generic.List<Vector2>(); mesh.GetUVs(1, atlas);
                Assert.That(atlas.Count(value => value != NotebookSpriteCatalog.WhiteUV), Is.GreaterThanOrEqualTo(4),
                    "Large crowds must retain the same new enemy artwork as individual enemies.");
                Assert.That(mesh.colors32.Any(color => color.Equals((Color32)NotebookPalette.Green)), Is.True, "A full HP bar is visible before damage.");
                pen.Clear(); DoodleGlyphs.Enemy(pen, Enemy(2.4f), 0, compact); pen.Upload();
                Assert.That(mesh.bounds.size.x, Is.GreaterThan(first.size.x * 2));
                Assert.That(mesh.bounds.max.y, Is.GreaterThan(first.max.y), "A scaled enemy and its attached bar grow together.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(EnemyKind.Doodle)] [TestCase(EnemyKind.Runner)] [TestCase(EnemyKind.Gnawer)]
        [TestCase(EnemyKind.Spitter)] [TestCase(EnemyKind.Brute)] [TestCase(EnemyKind.Bulwark)]
        public void EnemyHealthBarFollowsAnimatedSilhouetteAtEveryPresentationScale(EnemyKind kind)
        {
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                foreach (float scale in new[] { 1f, 2.4f })
                foreach (float presentation in new[] { 1f, NotebookEnemyView.ModernScale })
                foreach (float windup in new[] { 0f, .3f })
                {
                    var enemy = (EnemyView)typeof(EnemyView).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                        .Invoke(new object[] { new EnemyState { Id = 7, Kind = kind, Health = 100, MaxHealth = 100,
                            VisualScale = scale, Position = new Point2(10, 10), WindupRemaining = windup } });
                    pen.Clear(); DoodleGlyphs.Enemy(pen, enemy, .21f, showHealth: false, uniformScale: presentation); pen.Upload();
                    var body = mesh.bounds;
                    pen.Clear();
                    // An earlier actor in the shared batch must not affect this enemy's anchor.
                    pen.Rect(-100, -100, 200, 200, Color.black);
                    DoodleGlyphs.Enemy(pen, enemy, .21f, uniformScale: presentation); pen.Upload();
                    var points = mesh.vertices; var colors = mesh.colors32;
                    var bar = points.Where((point, index) => colors[index].Equals((Color32)NotebookPalette.Green)).ToArray();
                    Assert.That(bar.Length, Is.EqualTo(4), kind + " retains its full health indicator.");
                    float gap = bar.Min(point => point.y) - body.max.y;
                    Assert.That(gap, Is.GreaterThan(.06f * presentation), kind + " must clear its visible head and border.");
                    Assert.That(gap, Is.LessThanOrEqualTo(.18f * scale * presentation), kind + " must stay attached to its current silhouette.");
                    Assert.That((bar.Min(point => point.x) + bar.Max(point => point.x)) * .5f,
                        Is.EqualTo(body.center.x).Within(.001f), kind + " follows the animated body's center.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test] public void DeathEventsRetainFamilyAndScaleForWorldAndTrialTails()
        {
            var events = new SimulationEventBuffer();
            events.Publish(1, SimulationEventKind.EnemyDied, default, new Point2(10, 10), default,
                entityId: 1, enemyKind: EnemyKind.Brute, enemyVisualScale: 2.4f);
            var animation = new NotebookAnimationState(); animation.AddEvent(events[0]);
            var death = animation.Deaths.Single(); Assert.That(death.VisualScale, Is.EqualTo(2.4f));
            Assert.That(NotebookAnimationState.EnemyId(death.Kind, death.Tier, NotebookEnemyMotion.Death), Does.StartWith("enemy.brute."));
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                DoodleGlyphs.DrawResourceEnemy(pen, death.Kind, death.Tier, death.Position, NotebookEnemyMotion.Death, 0); pen.Upload();
                float normal = mesh.bounds.size.x;
                pen.Clear(); DoodleGlyphs.DrawResourceEnemy(pen, death.Kind, death.Tier, death.Position, NotebookEnemyMotion.Death, 0, visualScale: death.VisualScale); pen.Upload();
                Assert.That(mesh.bounds.size.x, Is.EqualTo(normal * death.VisualScale).Within(.0001));
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
#endif
