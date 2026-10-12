using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Application;
using UnityEngine;
namespace PaperDefense.Tests
{
    public sealed class MortarFootprintPersistenceTests
    {
        string directory, path;
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "PaperFootprintTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); path = Path.Combine(directory, "profile.json"); }
        [TearDown] public void TearDown()
        {
            string full = Path.GetFullPath(directory);
            Assert.That(full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(full).StartsWith("PaperFootprintTests-"), Is.True);
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }
        [Test] public void LegacyVariantSavesWithoutChangingSourceAndRoundTripsShape()
        {
            var profile = new WorkshopProfile(); var original = profile.Album.Single(p => p.FirstWeapon == StructureKind.Mortar);
            var variant = original.AsVariant("oval", "Oval").WithMortarFootprint(0, new MortarFootprint(3));
            Assert.That(profile.SaveBlueprint(variant, out string reason), Is.True, reason);
            var store = new WorkshopProfileStore(path); Assert.That(store.Save(profile).Success, Is.True);
            var loaded = new WorkshopProfileStore(path).Load().Profile;
            Assert.That(loaded.Album.Single(p => p.Id == "oval").FirstFootprint.AspectRatio, Is.EqualTo(3));
            Assert.That(loaded.Album.Single(p => p.Id == original.Id).FirstFootprint.AspectRatio, Is.EqualTo(1));
            Assert.That(variant.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
        }
        [Test] public void VersionFourRoundTripsTwoDifferentShapes()
        {
            var page = new TowerBlueprint("two", "Two", ChassisKind.Heavy, StructureKind.Mortar, StructureKind.Mortar)
                .WithMortarFootprint(0, new MortarFootprint(2)).WithMortarFootprint(1, new MortarFootprint(3));
            var restored = new BlueprintData(page).ToBlueprint(4);
            Assert.That(restored.FirstFootprint.AspectRatio, Is.EqualTo(2)); Assert.That(restored.SecondFootprint.AspectRatio, Is.EqualTo(3));
            Assert.That(WorkshopProfile.CurrentVersion, Is.EqualTo(5));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] public void HistoricalVersionsRestoreCircle(int version)
        {
            var data = new WorkshopProfile().ToData(); data.Version = version;
            foreach (var page in data.Album) { page.FootprintMetadataVersion = 0; page.FirstFootprintAspect = 0; page.SecondFootprintAspect = 0; if (version == 2) { page.Drawing.FormatVersion = 1; page.Drawing.Parts = page.Drawing.Parts.Where(p => (int)p.Key < 7).ToArray(); } }
            foreach (var page in WorkshopProfile.FromData(data).Album) Assert.That(page.FirstFootprint.AspectRatio, Is.EqualTo(1));
        }
        [TestCase(0f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(3.01f)]
        public void InvalidVersionFourAspectIsRejected(float aspect)
        {
            var page = new BlueprintData(new TowerBlueprint("m", "M", ChassisKind.Standard, StructureKind.Mortar)); page.FirstFootprintAspect = aspect;
            Assert.Throws<ArgumentOutOfRangeException>(() => page.ToBlueprint(4));
        }
        [Test] public void MissingAndFutureMetadataAreRejectedAndNonMortarCannotHaveOval()
        {
            var page = new BlueprintData(new TowerBlueprint("g", "G", ChassisKind.Standard, StructureKind.Gun));
            page.FootprintMetadataVersion = 0; Assert.Throws<ArgumentException>(() => page.ToBlueprint(4));
            page.FootprintMetadataVersion = 2; Assert.Throws<NotSupportedException>(() => page.ToBlueprint(4));
            page.FootprintMetadataVersion = 1; page.FirstFootprintAspect = 2; Assert.Throws<ArgumentException>(() => page.ToBlueprint(4));
        }
        [Test] public void VersionThreeMigrationPreservesExactBytes()
        {
            var data = new WorkshopProfile().ToData(); data.Version = 3;
            File.WriteAllText(path, JsonUtility.ToJson(data, true), new UnicodeEncoding(false, true)); byte[] original = File.ReadAllBytes(path);
            var store = new WorkshopProfileStore(path); var loaded = store.Load(); Assert.That(loaded.CanWrite, Is.True);
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(File.ReadAllBytes(path + ".v3-original"), Is.EqualTo(original));
        }
        [TestCase(false)] [TestCase(true)] public void FutureRootOrFootprintMetadataProtectsPrimaryAndBackup(bool root)
        {
            var data = new WorkshopProfile().ToData();
            if (root) data.Version = 99; else data.Album[0].FootprintMetadataVersion = 99;
            File.WriteAllText(path, JsonUtility.ToJson(data)); File.WriteAllText(path + ".bak", JsonUtility.ToJson(new WorkshopProfile().ToData()));
            byte[] primary = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.False); Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(primary)); Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
        }
        [Test] public void FailedVersionThreeOriginalCopyDoesNotCommit()
        {
            var data = new WorkshopProfile().ToData(); data.Version = 3;
            File.WriteAllText(path, JsonUtility.ToJson(data)); byte[] original = File.ReadAllBytes(path);
            var store = new WorkshopProfileStore(path, new CopyFaultIO()); var loaded = store.Load();
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original)); Assert.That(File.Exists(path + ".bak"), Is.False);
        }
        sealed class CopyFaultIO : IWorkshopProfileIO
        {
            public string ReadAllText(string file) => File.ReadAllText(file);
            public void CreateDirectory(string folder) => Directory.CreateDirectory(folder);
            public void WriteAllText(string file, string contents) => File.WriteAllText(file, contents);
            public void Move(string source, string target) => File.Move(source, target);
            public void Copy(string source, string target) => throw new IOException("Injected v3 original copy failure");
            public void Replace(string source, string target, string backup) => File.Replace(source, target, backup);
            public void Delete(string file) => File.Delete(file);
        }
    }
}
