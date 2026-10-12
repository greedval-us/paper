using System;
using NUnit.Framework;
using PaperDefense.Core;
namespace PaperDefense.Tests
{
    public sealed class MortarFootprintBlueprintTests
    {
        [TestCase(1f)] [TestCase(3f)] public void EndpointsAreAccepted(float q)
            => Assert.That(new MortarFootprint(q).AspectRatio, Is.EqualTo(q));
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        [TestCase(0f)] [TestCase(3.01f)] public void InvalidRatiosAreRejected(float q)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new MortarFootprint(q));
        [Test] public void ShapeEditPreservesArtOriginAssessmentAndEconomy()
        {
            var page = new TowerBlueprint("m", "Mortar", ChassisKind.Heavy, StructureKind.Mortar, StructureKind.Mortar);
            var changed = page.WithMortarFootprint(0, new MortarFootprint(3));
            Assert.That(changed.FirstFootprint.AspectRatio, Is.EqualTo(3));
            Assert.That(page.FirstFootprint.AspectRatio, Is.EqualTo(1));
            Assert.That(changed.Drawing.GeometryHash, Is.EqualTo(page.Drawing.GeometryHash));
            Assert.That(changed.Origin, Is.EqualTo(page.Origin));
            Assert.That(changed.Assessment.Quality, Is.EqualTo(page.Assessment.Quality));
            var balance = new GameBalance();
            Assert.That(TowerCatalog.Cost(changed, balance, MaterialTier.Pencil), Is.EqualTo(TowerCatalog.Cost(page, balance, MaterialTier.Pencil)));
            Assert.That(TowerCatalog.FeedMultiplier(changed), Is.EqualTo(TowerCatalog.FeedMultiplier(page)));
        }
        [Test] public void CopiesRetainShapesAndOnlyReplacedWeaponResets()
        {
            var page = new TowerBlueprint("m", "Mortar", ChassisKind.Heavy, StructureKind.Mortar, StructureKind.Mortar)
                .WithMortarFootprint(0, new MortarFootprint(2)).WithMortarFootprint(1, new MortarFootprint(3));
            foreach (var copy in new[] { page.WithName("Rename"), page.WithDrawing(DrawingSnapshot.Empty), page.AsVariant("v", "Variant") })
            { Assert.That(copy.FirstFootprint.AspectRatio, Is.EqualTo(2)); Assert.That(copy.SecondFootprint.AspectRatio, Is.EqualTo(3)); }
            var changed = page.WithAssembly(ChassisKind.Standard, StructureKind.Gun, StructureKind.Mortar, TowerModules.Feed);
            Assert.That(changed.FirstFootprint.AspectRatio, Is.EqualTo(1));
            Assert.That(changed.SecondFootprint.AspectRatio, Is.EqualTo(3));
        }
        [Test] public void MountMustContainMortarAndFootprintMustExist()
        {
            var page = new TowerBlueprint("g", "Gun", ChassisKind.Standard, StructureKind.Gun);
            foreach (int mount in new[] { -1, 0, 1, 2 })
            { Assert.Throws<ArgumentOutOfRangeException>(() => page.GetMortarFootprint(mount)); Assert.Throws<ArgumentOutOfRangeException>(() => page.WithMortarFootprint(mount, MortarFootprint.Circle)); }
            var mortar = new TowerBlueprint("m", "Mortar", ChassisKind.Standard, StructureKind.Mortar);
            Assert.Throws<ArgumentNullException>(() => mortar.WithMortarFootprint(0, null));
        }
    }
}
