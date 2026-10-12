using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class RailPersistenceTests
    {
        string folder, path;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "paper-rail-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); path = Path.Combine(folder, "profile.json");
        }
        [TearDown] public void TearDown()
        {
            string absolute = Path.GetFullPath(folder);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("paper-rail-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
        static DrawingStroke Line(int index = 0) => new DrawingStroke(new[] {
            new DrawingPoint(-.4f, -.3f + index * .05f), new DrawingPoint(.1f, .2f + index * .05f) });

        [Test] public void RailRightAndAuthoredLinesRoundTripAsFormatTwoWithoutChangingProfileVersion()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(100);
            Assert.That(profile.Unlock("module.rail", out string reason), Is.True, reason);
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Rail, new[] { Line() }) });
            var page = TowerBlueprint.NewDraft("rail", "Моя направляющая", ChassisKind.Standard, StructureKind.Gun,
                modules: TowerModules.Rail).WithDrawing(drawing);
            Assert.That(profile.SaveBlueprint(page, out reason), Is.True, reason); Assert.That(profile.AssignSlot(0, page.Id, out reason), Is.True, reason);
            Assert.That(new WorkshopProfileStore(path).Save(profile).Success, Is.True);
            var data = JsonUtility.FromJson<WorkshopProfileData>(File.ReadAllText(path));
            Assert.That(data.Version, Is.EqualTo(4)); Assert.That(data.Album[4].Drawing.FormatVersion, Is.EqualTo(2));
            Assert.That(data.Album[4].Drawing.BindingVersion, Is.EqualTo(1)); Assert.That(data.Album[4].Drawing.Parts.Length, Is.EqualTo(8));
            var restored = new WorkshopProfileStore(path).Load(); Assert.That(restored.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(restored.Profile.Gold, Is.EqualTo(40)); Assert.That(restored.Profile.IsUnlocked("module.rail"), Is.True);
            TowerBlueprint selected = restored.Profile.GetLoadoutBlueprints()[0];
            Assert.That(selected.Modules, Is.EqualTo(TowerModules.Rail)); Assert.That(selected.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
        }

        [TestCase(2, false)] [TestCase(2, true)] [TestCase(3, false)] [TestCase(3, true)]
        public void HistoricalSevenPartDrawingUpgradesWithoutChangingAnyOriginalStroke(int version, bool backup)
        {
            var profile = new WorkshopProfile();
            var parts = new DrawingPart[7]; for (int i = 0; i < parts.Length; i++) parts[i] = new DrawingPart((DrawingPartKey)i, new[] { Line(i) });
            var page = TowerBlueprint.NewDraft("historical", "Старые семь частей", ChassisKind.Standard, StructureKind.Gun).WithDrawing(new DrawingSnapshot(parts));
            Assert.That(profile.SaveBlueprint(page, out _), Is.True); Assert.That(profile.AssignSlot(0, page.Id, out _), Is.True);
            WorkshopProfileData historical = HistoricalProfileTestData.Create(profile, version);
            string[] originalParts = historical.Album[4].Drawing.Parts.Select(JsonUtility.ToJson).ToArray();
            string source = backup ? path + ".bak" : path; File.WriteAllText(source, JsonUtility.ToJson(historical, true), Encoding.Unicode);
            byte[] original = File.ReadAllBytes(source); if (backup) File.WriteAllText(path, "broken primary");
            var store = new WorkshopProfileStore(path); var loaded = store.Load(); Assert.That(loaded.CanWrite, Is.True);
            TowerBlueprint upgraded = loaded.Profile.GetLoadoutBlueprints()[0];
            Assert.That(upgraded.Drawing.FormatVersion, Is.EqualTo(2)); Assert.That(upgraded.Drawing.BindingVersion, Is.EqualTo(1));
            Assert.That(upgraded.Drawing.Parts.Count, Is.EqualTo(8)); Assert.That(upgraded.Drawing.GetPart(DrawingPartKey.Rail).Strokes, Is.Empty);
            DrawingData upgradedData = loaded.Profile.ToData().Album[4].Drawing;
            Assert.That(upgradedData.Parts.Take(7).Select(JsonUtility.ToJson), Is.EqualTo(originalParts));
            Assert.That(historical.Album[4].Drawing.Parts.Select(JsonUtility.ToJson), Is.EqualTo(originalParts));
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(File.ReadAllBytes(version == 2 ? path + ".v2-original" : path + ".bak"), Is.EqualTo(original));
            Assert.That(new WorkshopProfileStore(path).Load().Profile.GetLoadoutBlueprints()[0].Drawing.GeometryHash, Is.EqualTo(upgraded.Drawing.GeometryHash));
        }

        [Test] public void FormatOneCannotSmuggleARailKeyByOmittingAnOriginalPart()
        {
            var data = HistoricalProfileTestData.Create(new WorkshopProfile(), 3);
            data.Album[0].Drawing.Parts[6].Key = DrawingPartKey.Rail;
            File.WriteAllText(path, JsonUtility.ToJson(data, true)); byte[] original = File.ReadAllBytes(path);
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired)); Assert.That(loaded.CanWrite, Is.False);
            Assert.That(store.Save(loaded.Profile).Success, Is.False); Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

        [TestCase("missing")] [TestCase("duplicate")]
        public void FormatTwoRequiresAllEightDistinctPartKeys(string defect)
        {
            var data = new WorkshopProfile().ToData(); data.Album[0].Drawing.FormatVersion = 2;
            var original = data.Album[0].Drawing.Parts;
            if (defect == "missing") data.Album[0].Drawing.Parts = original.Take(7).ToArray();
            else
            {
                var parts = new DrawingPartData[8]; for (int i = 0; i < parts.Length; i++) parts[i] = original[0];
                data.Album[0].Drawing.Parts = parts;
            }
            File.WriteAllText(path, JsonUtility.ToJson(data));
            var loaded = new WorkshopProfileStore(path).Load(); Assert.That(loaded.CanWrite, Is.False);
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
        }

        [Test] public void AssetRailTuningIsOwnedByPreviewAndReadonlyRangeWithoutChangingFeed()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            var host = new GameObject("Rail preview test"); host.SetActive(false);
            try
            {
                asset.ResetToDefaults(); asset.construction.RailRangeMultiplier = 2; asset.construction.RailIntervalMultiplier = 1.5f;
                var run = host.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                var balance = asset.CreateWorkshopBalance(); asset.construction.RailRangeMultiplier = 4;
                var page = new TowerBlueprint("rail", "Направляющая", ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Rail);
                var preview = run.PreviewBlueprint(page);
                Assert.That(preview.Range, Is.EqualTo(22)); Assert.That(preview.FireRate, Is.EqualTo(1 / (.42f * 1.5f)).Within(.00001f));
                Assert.That(preview.FeedMultiplier, Is.EqualTo(1)); Assert.That(preview.Load, Is.EqualTo(6)); Assert.That(preview.Cost, Is.EqualTo(60));
                var read = new RunReadModel(new GameSession(balance, starterDefenses: false));
                Assert.That(read.Balance.WeaponRange(page, StructureKind.Gun), Is.EqualTo(22));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
