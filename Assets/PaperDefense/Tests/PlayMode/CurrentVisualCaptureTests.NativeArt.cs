#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed partial class CurrentVisualCaptureTests
    {
        [UnityTest, Explicit("Shows the workshop families and a paid mixed defense in the production camera.")]
        public IEnumerator CaptureFieldDefenses()
        {
            var size = Sizes[0];
            Size(size);
            using var buyer = new AssemblyCommandFixture(gold: 245, xp: 230);
            game.Run.InitializeWorkshop(buyer.Store);
            var templates = game.Run.AssemblyCatalog.Templates.Where(entry => entry.Kind == AlbumEntryKind.Structure).ToArray();
            Assert.That(templates.Length, Is.EqualTo(7));
            var gallery = Gallery("Сооружения полевой мастерской", size);
            try
            {
                for (int i = 0; i < templates.Length; i++)
                {
                    var entry = templates[i];
                    var tile = GalleryTile(gallery.transform.Find("Diagnostic content"), i, 4, 2, size);
                    StandardAssemblyIllustration.Draw(tile, entry, new Rect(16, 12, tile.rect.width - 32, tile.rect.height - 72), entry.Id);
                    GalleryCaption(tile, GameText.Resolve(entry.Name));
                }
                Capture("defense-families", size, diagnostic: true);
            }
            finally { Object.DestroyImmediate(gallery); }

            void Equip(string templateId, int slot)
            {
                var template = templates.Single(entry => entry.Id == templateId);
                var purchase = game.Run.AssemblyCommands.PurchaseStructureFamily(template.Snapshot.Recipe.Structure.Family, Guid.NewGuid());
                Assert.That(purchase.Success, Is.True, purchase.Code);
                var saved = game.Run.AssemblyCommands.SaveEntryAsVariant(template, Guid.NewGuid());
                Assert.That(saved.Success, Is.True, saved.Code);
                Assert.That(game.Run.AssemblyCommands.AssignSlot(slot, saved.ResultId, Guid.NewGuid()).Success, Is.True);
            }
            Equip("album.spikes", 2);
            Equip("album.beacon", 3);
            Equip("album.prism", 1);
            StartBattle();
            var home = game.Run.Session.BaseCell;
            void Build(int slot, int dx, int dy)
            {
                var cell = new Cell(home.X + dx, home.Y + dy);
                Assert.That(game.Run.BuildEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None, out var reason), Is.True,
                    cell + " · " + GameText.Resolve(reason));
            }
            Build(5, -5, -1); Build(2, -4, -1);
            Build(3, -3, 0); Build(0, -2, 2); Build(1, -4, 2);
            for (int step = 0; step < 200 && game.Run.Session.Structures.Any(item => !item.IsComplete); step++)
                game.Run.StepSimulation();
            Assert.That(game.Run.Session.Structures.All(item => item.IsComplete), Is.True);
            var focus = new Cell(home.X - 2, home.Y);
            Capture("defense-line", size, focus: focus);
            bool triggered = false;
            var pulseColors = new List<InkColor>();
            int shots = 0;
            void Observe(SimulationEvent action)
            {
                if (action.Kind == SimulationEventKind.SpikesTriggered) triggered = true;
                if (action.Kind == SimulationEventKind.PrismPulsed) pulseColors.Add(action.ColorEffect.Color);
                if (action.Kind == SimulationEventKind.Shot) shots++;
            }
            game.Run.SimulationEventReceived += Observe;
            try
            {
                game.Run.NextWave();
                int captured = 0;
                for (int step = 0; step < 10000 && !(triggered && captured == 3) && game.Run.Session.Phase == RunPhase.Assault; step++)
                {
                    game.Run.StepSimulation();
                    if (pulseColors.Count <= captured || captured == 3) continue;
                    // Capture the expanding line a few simulation ticks after the real pulse.
                    for (int tick = 0; tick < 5; tick++) game.Run.StepSimulation();
                    Capture("prism-pulse-" + pulseColors[captured].ToString().ToLowerInvariant(), size, actualShots: shots, focus: focus);
                    captured++;
                }
                Assert.That(triggered, Is.True, "The paid spike trap must intercept a real first-wave enemy.");
                CollectionAssert.AreEqual(new[] { InkColor.Red, InkColor.Blue, InkColor.Green }, pulseColors.Take(3));
                Capture("defense-in-action", size, actualShots: shots, focus: focus);
            }
            finally { game.Run.SimulationEventReceived -= Observe; }
            yield return null;
        }

        static readonly string[] Scenery = {
            "object.castle", "object.castle.damaged", "object.castle.ruined", "terrain.pine", "terrain.oak",
            "terrain.mountain", "terrain.ridge", "terrain.rock", "terrain.shrub", "object.entrance",
            "object.well.red", "object.well.red.active", "object.well.blue", "object.well.blue.active",
            "object.well.green", "object.well.green.active", "object.barricade-site"
        };
        static readonly string[] Effects = {
            "fx.muzzle", "fx.tracer", "fx.piercer", "fx.fan", "fx.ricochet", "fx.stapler",
            "fx.mortar.projectile", "fx.mortar.explosion", "fx.spitter.projectile", "fx.spitter.impact",
            "fx.structure.destroy", "object.blot", "fx.blot.birth", "fx.blot.expire", "fx.blot.cleanse",
            "fx.blot.absorb", "fx.castle.hit", "fx.well.hit", "fx.well.activate", "fx.cleanup",
            "fx.status.red", "fx.status.blue", "fx.status.green"
        };

        void CaptureNativeArt(Vector2Int size)
        {
            foreach (bool compact in new[] { false, true }) CaptureEnemyPixels(size, compact);
            foreach (DrawingTier tier in Enum.GetValues(typeof(DrawingTier)))
            {
                var poses = from EnemyKind role in Enum.GetValues(typeof(EnemyKind))
                    from NotebookEnemyMotion motion in Enum.GetValues(typeof(NotebookEnemyMotion))
                    select NotebookAnimationState.EnemyId(role, tier, motion);
                CaptureResources("enemies-" + tier.ToString().ToLowerInvariant(), poses, size);
            }
            CaptureResources("scenery", Scenery, size);
            CaptureResources("effects", Effects, size);
            CaptureResources("weapon-families", new[] { "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "blotter" }
                .Select(family => "ui.weapon." + family), size);
        }

        void CaptureEnemyPixels(Vector2Int size, bool compact)
        {
            var gallery = Gallery(compact ? "Силуэты толпы · 24 / 40 / 64 px" : "Силуэты врагов · 24 / 40 / 64 px", size);
            try
            {
                var roles = (EnemyKind[])Enum.GetValues(typeof(EnemyKind));
                var spans = new[] { 24, 40, 64 };
                var drawings = new List<(NotebookArtGraphic graphic, int pixels)>();
                for (int row = 0; row < spans.Length; row++)
                for (int column = 0; column < roles.Length; column++)
                {
                    var tile = GalleryTile(gallery.transform.Find("Diagnostic content"), row * roles.Length + column, roles.Length, 3, size);
                    float span = spans[row] / .92f;
                    var art = PlayerUi.Graphic<NotebookArtGraphic>(tile, "Enemy pixel sample", new Rect((tile.rect.width - span) * .5f, 8, span, span));
                    art.Bind(NotebookAnimationState.EnemyId(roles[column], DrawingTier.Outlined, NotebookEnemyMotion.Idle) + (compact ? ".compact" : ""));
                    GalleryCaption(tile, roles[column] + "\n" + spans[row] + " px");
                    drawings.Add((art, spans[row]));
                }
                Capture(compact ? "diagnostic-compact-pixels" : "diagnostic-native-pixels", size, diagnostic: true);
                foreach (var item in drawings)
                {
                    var mesh = item.graphic.canvasRenderer.GetMesh();
                    Assert.That(mesh, Is.Not.Null);
                    Assert.That(Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y) * item.graphic.canvas.scaleFactor,
                        Is.EqualTo(item.pixels).Within(.03f), "Pixel labels must describe the actual rendered ink span.");
                }
            }
            finally { Object.DestroyImmediate(gallery); }
        }

        void CaptureResources(string name, IEnumerable<string> resources, Vector2Int size)
        {
            string[] ids = resources.ToArray();
            const int columns = 4, rows = 3, capacity = columns * rows;
            for (int first = 0; first < ids.Length; first += capacity)
            {
                var gallery = Gallery("Боевой альбом · " + name, size);
                try
                {
                    for (int i = first; i < Mathf.Min(first + capacity, ids.Length); i++)
                    {
                        string id = ids[i];
                        var tile = GalleryTile(gallery.transform.Find("Diagnostic content"), i - first, columns, rows, size);
                        var art = PlayerUi.Graphic<NotebookArtGraphic>(tile, id, new Rect(8, 4, tile.rect.width - 16, tile.rect.height - 64));
                        var clip = NotebookVisualThemeAsset.Default.FindClip(id);
                        art.Bind(id, clip == null ? null : id); art.SetTime((clip?.Duration ?? 0) * .28f);
                        GalleryCaption(tile, id);
                    }
                    Capture("diagnostic-" + name + "-" + (first / capacity + 1), size, diagnostic: true);
                }
                finally { Object.DestroyImmediate(gallery); }
            }
        }
    }
}
#endif
