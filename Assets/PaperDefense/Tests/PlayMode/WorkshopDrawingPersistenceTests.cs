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
    public sealed class WorkshopDrawingPersistenceTests
    {
        string directory, path;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PaperDrawingStoreTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "profile.json");
        }
        [TearDown] public void TearDown()
        {
            string absolute = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("PaperDrawingStoreTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }

        static DrawingStroke Stroke(float offset = 0) => new DrawingStroke(new[]
        { new DrawingPoint(-.6f, -.4f + offset), new DrawingPoint(.2f, .3f + offset), new DrawingPoint(.7f, -.1f + offset) });
        static DrawingSnapshot Drawing() => new DrawingSnapshot(new[]
        {
            new DrawingPart(DrawingPartKey.Base, new[] { Stroke() }),
            new DrawingPart(DrawingPartKey.Mount0, new[] { Stroke(.1f) }),
            new DrawingPart(DrawingPartKey.Mount1, new[] { Stroke(.2f) }),
            new DrawingPart(DrawingPartKey.Decoration, new[] { Stroke(-.1f) })
        });
        static TowerBlueprint Page(string id = "authored") => TowerBlueprint.NewDraft(id, "Кривые два ствола",
            ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun).WithDrawing(Drawing());

        [Test] public void EmptyAuthoredPageIsDraftButAnyNonemptyDrawingCanBeSaved()
        {
            var profile = new WorkshopProfile();
            var draft = TowerBlueprint.NewDraft("draft", "Черновик", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(draft.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(profile.SaveBlueprint(draft, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            var incomplete = draft.WithDrawing(new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, new[] { Stroke() }) }));
            Assert.That(profile.SaveBlueprint(incomplete, out reason), Is.True, reason);
        }

        [Test] public void LegacyRenameKeepsOriginButNewVariantAndChangedAssemblyRequireDrawing()
        {
            var original = new WorkshopProfile().Album[0];
            Assert.That(original.WithName("Новое имя").Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(original.WithAssembly(original.Chassis, original.FirstWeapon, original.SecondWeapon, original.Modules).Origin,
                Is.EqualTo(DrawingOrigin.LegacyStandard));
            var changed = original.WithAssembly(ChassisKind.Standard, StructureKind.Piercer, null, TowerModules.None);
            Assert.That(changed.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(changed.Drawing.IsEmpty, Is.True);
            var variant = original.AsNew("new.page", "Новая страница");
            Assert.That(variant.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(new WorkshopProfile().SaveBlueprint(variant, out _), Is.False);
        }

        [Test] public void ChangingOneWeaponKeepsOtherAuthorLinesAndDecoration()
        {
            TowerBlueprint page = Page();
            var changed = page.WithAssembly(ChassisKind.Standard, StructureKind.Piercer, StructureKind.Gun, TowerModules.None);
            Assert.That(changed.Drawing.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.Zero);
            Assert.That(changed.Drawing.GetPart(DrawingPartKey.Mount1).Strokes.Count, Is.EqualTo(1));
            Assert.That(changed.Drawing.GetPart(DrawingPartKey.Base).Strokes.Count, Is.EqualTo(1));
            Assert.That(changed.Drawing.GetPart(DrawingPartKey.Decoration).Strokes.Count, Is.EqualTo(1));
            Assert.That(page.Drawing.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(1));
        }

        [Test] public void RestartPreservesBothDifferentBarrelsAndExactAuthoredGeometry()
        {
            var profile = new WorkshopProfile(); var page = Page();
            Assert.That(profile.SaveBlueprint(page, out _), Is.True); Assert.That(profile.AssignSlot(0, page.Id, out _), Is.True);
            var store = new WorkshopProfileStore(path); Assert.That(store.Save(profile).Success, Is.True);
            Assert.That(JsonUtility.FromJson<WorkshopProfileData>(File.ReadAllText(path)).Version, Is.EqualTo(WorkshopProfile.CurrentVersion));
            var restored = new WorkshopProfileStore(path).Load();
            Assert.That(restored.Source, Is.EqualTo(ProfileLoadSource.Primary));
            var saved = restored.Profile.GetLoadoutBlueprints()[0];
            Assert.That(saved.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(saved.Drawing.GeometryHash, Is.EqualTo(page.Drawing.GeometryHash));
            Assert.That(saved.Drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[0].Y,
                Is.Not.EqualTo(saved.Drawing.GetPart(DrawingPartKey.Mount1).Strokes[0].Points[0].Y));
        }

        [Test] public void VersionTwoReloadDoesNotResurrectDeletedStarterPages()
        {
            var profile = new WorkshopProfile();
            for (int i = 0; i < 4; i++)
            {
                string old = profile.Loadout[i]; var page = Page("own." + i);
                Assert.That(profile.SaveBlueprint(page, out _), Is.True);
                Assert.That(profile.AssignSlot(i, page.Id, out _), Is.True);
                Assert.That(profile.RemoveBlueprint(old, out _), Is.True);
            }
            var historical = HistoricalProfileTestData.Create(profile, 2);
            File.WriteAllText(path, JsonUtility.ToJson(historical, true));
            var restored = new WorkshopProfileStore(path).Load().Profile;
            Assert.That(restored.Album.Count, Is.EqualTo(4));
            Assert.That(restored.Album.Select(page => page.Id), Is.EqualTo(new[] { "own.0", "own.1", "own.2", "own.3" }));
        }

        [Test] public void FullAlbumWithMaximumDrawingDataRoundTripsBeyondOldOneMegabyteLimit()
        {
            var strokes = new DrawingStroke[128];
            for (int s = 0; s < strokes.Length; s++)
            {
                var points = new DrawingPoint[16];
                for (int p = 0; p < points.Length; p++) points[p] = new DrawingPoint(-.9f + p * .12f, -.9f + s * .014f);
                strokes[s] = new DrawingStroke(points);
            }
            var maximum = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, strokes) });
            var profile = new WorkshopProfile();
            for (int i = 0; i < 64; i++)
            {
                var page = TowerBlueprint.NewDraft("max." + i, new string('Я', 48), ChassisKind.Standard, StructureKind.Gun).WithDrawing(maximum);
                if (i < 4)
                { string old = profile.Loadout[i]; Assert.That(profile.SaveBlueprint(page, out _), Is.True);
                    Assert.That(profile.AssignSlot(i, page.Id, out _), Is.True); Assert.That(profile.RemoveBlueprint(old, out _), Is.True); }
                else Assert.That(profile.SaveBlueprint(page, out _), Is.True);
            }
            var store = new WorkshopProfileStore(path); Assert.That(store.Save(profile).Success, Is.True);
            Assert.That(Encoding.UTF8.GetByteCount(File.ReadAllText(path)), Is.GreaterThan(1024 * 1024));
            var restored = new WorkshopProfileStore(path).Load();
            Assert.That(restored.CanWrite, Is.True, restored.Message); Assert.That(restored.Profile.Album.Count, Is.EqualTo(64));
            Assert.That(restored.Profile.Album[63].Drawing.PointCount, Is.EqualTo(2048));
            Assert.That(restored.Profile.Album[63].Drawing.GeometryHash, Is.EqualTo(maximum.GeometryHash));
        }

        static string LegacyJson() => "{\"Version\":1,\"Gold\":321,\"Unlocked\":[\"chassis.heavy\",\"module.tracking\"],\"Loadout\":[\"custom\",\"starter.double\",\"starter.piercer\",\"starter.mortar\"],\"Album\":[{\"Id\":\"custom\",\"Name\":\"Старая башня\",\"Chassis\":1,\"FirstWeapon\":2,\"Modules\":4}]}";

        [TestCase(false)] [TestCase(true)]
        public void VersionOneMigrationPreservesExactOriginalBytesAndProgressBeforeFirstCommit(bool fromBackup)
        {
            string source = fromBackup ? path + ".bak" : path;
            File.WriteAllText(source, LegacyJson(), new UnicodeEncoding(false, true));
            if (fromBackup) File.WriteAllText(path, "corrupted primary");
            byte[] original = File.ReadAllBytes(source);
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.True, loaded.Message); Assert.That(loaded.Profile.Gold, Is.EqualTo(321));
            Assert.That(loaded.Profile.GetLoadoutBlueprints()[0].Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(File.ReadAllBytes(path + ".v1-original"), Is.EqualTo(original));
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(File.ReadAllBytes(path + ".v1-original"), Is.EqualTo(original));
            Assert.That(new WorkshopProfileStore(path).Load().Profile.Gold, Is.EqualTo(321));
        }

        [Test] public void FailureToPreserveLegacyOriginalDoesNotCommitOrChangeSourceBytes()
        {
            File.WriteAllText(path, LegacyJson()); byte[] original = File.ReadAllBytes(path);
            var store = new WorkshopProfileStore(path, new MigrationFaultIO()); var loaded = store.Load();
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(File.Exists(path + ".bak"), Is.False);
        }

        [TestCase("metadata")] [TestCase("future-metadata")] [TestCase("origin")]
        [TestCase("format")] [TestCase("binding")]
        [TestCase("bounds")] [TestCase("nan")] [TestCase("truncated")]
        public void InvalidVersionTwoDrawingNeverSilentlyDropsPagesOrOverwritesSource(string defect)
        {
            var profile = new WorkshopProfile(); Assert.That(profile.SaveBlueprint(Page(), out _), Is.True);
            var data = HistoricalProfileTestData.Create(profile, 2); var page = data.Album[4];
            if (defect == "metadata") page.DrawingMetadataVersion = 0;
            if (defect == "future-metadata") page.DrawingMetadataVersion = 99;
            if (defect == "origin") page.Origin = (DrawingOrigin)(-1);
            if (defect == "format") page.Drawing.FormatVersion = 99;
            if (defect == "binding") page.Drawing.BindingVersion = 99;
            if (defect == "bounds") page.Drawing.Parts[0].Strokes[0].Points[0].X = 1.1f;
            if (defect == "nan") page.Drawing.Parts[0].Strokes[0].Points[0].X = float.NaN;
            string json = JsonUtility.ToJson(data, true); if (defect == "truncated") json = json.Substring(0, json.Length / 2);
            File.WriteAllText(path, json);
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(loaded.CanWrite, Is.False); Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(json));
        }

        [Test] public void FutureDrawingMetadataCannotRollBackToOlderCompatibleBackup()
        {
            var profile = new WorkshopProfile(); Assert.That(profile.SaveBlueprint(Page(), out _), Is.True);
            var data = profile.ToData(); data.Album[4].Drawing.BindingVersion = 99;
            string primary = JsonUtility.ToJson(data, true), backup = LegacyJson();
            File.WriteAllText(path, primary); File.WriteAllText(path + ".bak", backup);
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
        }

        sealed class MigrationFaultIO : IWorkshopProfileIO
        {
            public string ReadAllText(string file) => File.ReadAllText(file);
            public void CreateDirectory(string folder) => Directory.CreateDirectory(folder);
            public void WriteAllText(string file, string contents) => File.WriteAllText(file, contents);
            public void Move(string source, string target) => File.Move(source, target);
            public void Copy(string source, string target) => throw new IOException("Injected original-preservation failure");
            public void Replace(string source, string target, string backup) => File.Replace(source, target, backup);
            public void Delete(string file) => File.Delete(file);
        }
    }
}
