using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class MortarFootprintHudTests
    {
        [Test] public void HistoricalAuthoredArtSurvivesVariantAndTemporaryStore()
        {
            string folder = Path.Combine(Path.GetTempPath(), "PaperFootprintUI-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); string path = Path.Combine(folder, "profile.json");
            try
            {
                var store = new WorkshopProfileStore(path); var initial = new WorkshopProfile(); initial.GrantGold(1000);
                Assert.That(initial.Unlock("chassis.heavy", out var unlockReason), Is.True, unlockReason);
                var source = new TowerBlueprint("historical-mortar-art", "Imported mortar", ChassisKind.Heavy,
                    StructureKind.Mortar, StructureKind.Mortar).AsFreeform();
                var drawing = new DrawingEditSession(source, source.Drawing);
                Assert.That(drawing.AddStroke(DrawingPartKey.Mount0,
                    new[] { new DrawingPoint(0,0), new DrawingPoint(0,.4f) }, out _), Is.True);
                FreeformUiTestDrawing.Complete(drawing); source = source.WithFreeformDrawing(drawing.Snapshot);
                Assert.That(initial.SaveBlueprint(source, out var reason), Is.True, reason);
                Assert.That(store.Save(initial).Success, Is.True);
                Assert.That(source.Drawing.IsEmpty, Is.False);
                var variant = source.WithMortarFootprint(0, new MortarFootprint(3))
                    .WithMortarFootprint(1, new MortarFootprint(3)).AsVariant("historical-mortar-variant", "Variant");
                Assert.That(variant.Id, Is.Not.EqualTo(source.Id));
                Assert.That(variant.Drawing.GeometryHash, Is.EqualTo(source.Drawing.GeometryHash));
                Assert.That(variant.Origin, Is.EqualTo(source.Origin));
                Assert.That(variant.Assessment.Quality, Is.EqualTo(source.Assessment.Quality));
                Assert.That(initial.SaveBlueprint(variant, out reason), Is.True, reason);
                Assert.That(store.Save(initial).Success, Is.True);
                var loaded = new WorkshopProfileStore(path).Load().Profile;
                var stored = loaded.Album.Single(p => p.Id == variant.Id);
                Assert.That(stored.FirstFootprint.AspectRatio, Is.EqualTo(3));
                Assert.That(stored.SecondFootprint.AspectRatio, Is.EqualTo(3));
                Assert.That(stored.Drawing.GeometryHash, Is.EqualTo(source.Drawing.GeometryHash));
                Assert.That(stored.Origin, Is.EqualTo(source.Origin));
                Assert.That(stored.Assessment.Quality, Is.EqualTo(source.Assessment.Quality));
                Assert.That(loaded.Album.Single(p => p.Id == source.Id).FirstFootprint.AspectRatio, Is.EqualTo(1));
            }
            finally
            {
                string absolute = Path.GetFullPath(folder);
                string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.That(absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(absolute).StartsWith("PaperFootprintUI-", StringComparison.Ordinal), Is.True);
                Directory.Delete(folder, true);
            }
        }
    }
}
