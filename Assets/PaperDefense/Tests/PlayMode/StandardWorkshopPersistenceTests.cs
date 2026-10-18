using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class StandardWorkshopPersistenceTests
    {
        string directory, path;
        readonly List<GameObject> roots = new List<GameObject>();
        GameBalanceAsset balance;

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PaperStandardWorkshopTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "profile.json");
        }

        [TearDown] public void TearDown()
        {
            foreach (var root in roots) Object.DestroyImmediate(root);
            roots.Clear();
            if (balance != null) Object.DestroyImmediate(balance);
            string target = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(target.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(target).StartsWith("PaperStandardWorkshopTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }

        static TowerBlueprint StandardPage(string id = "standard.saved")
            => TowerBlueprint.NewStandardDraft(id, "Standard without strokes", "chassis.standard", "weapon.gun",
                modules: new[] { new ModuleSelection("tracking", "module.tracking") });

        static TowerBlueprint HistoricalFreeformPage()
        {
            var body = new DrawingStroke(new[] {
                new DrawingPoint(-.4f, -.35f), new DrawingPoint(.4f, -.35f), new DrawingPoint(.4f, .35f),
                new DrawingPoint(-.4f, .35f), new DrawingPoint(-.4f, -.35f)
            });
            var barrel = new DrawingStroke(new[] {
                new DrawingPoint(-.1f, 0), new DrawingPoint(.1f, 0), new DrawingPoint(.1f, .8f),
                new DrawingPoint(-.1f, .8f), new DrawingPoint(-.1f, 0)
            });
            return new TowerBlueprint("historical.freeform", "Historical freeform", ChassisKind.Standard, StructureKind.Gun,
                drawing: new DrawingSnapshot(new[] {
                    new DrawingPart(DrawingBindings.Base, new[] { body }),
                    new DrawingPart(DrawingBindings.Mount0, new[] { barrel })
                }), origin: DrawingOrigin.Freeform, freeformRuleVersion: 1);
        }

        static WorkshopProfile ProfileWithProgress()
        {
            var profile = new WorkshopProfile();
            profile.GrantGold(500);
            Assert.That(profile.Unlock("chassis.heavy", out var reason), Is.True, reason);
            Assert.That(profile.Unlock("module.tracking", out reason), Is.True, reason);
            return profile;
        }

        static string PageState(TowerBlueprint page) => JsonUtility.ToJson(new BlueprintData(page));

        static void AssertStandard(TowerBlueprint page)
        {
            Assert.That(page, Is.Not.Null);
            Assert.That(page.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(page.Drawing.IsEmpty, Is.True);
            Assert.That(page.Drawing.StrokeCount, Is.Zero);
            Assert.That(page.Drawing.PointCount, Is.Zero);
            Assert.That(page.FreeformDesign, Is.Null);
            Assert.That(page.FreeformRuleVersion, Is.Zero);
        }

        RunController Controller(IWorkshopProfileStore store)
        {
            var root = new GameObject("Isolated standard workshop persistence");
            root.SetActive(false); roots.Add(root);
            var run = root.AddComponent<RunController>();
            run.AutomaticSimulation = false;
            run.balanceAsset = balance;
            run.InitializeWorkshop(store, allowLegacyFixtureDefaults: balance == null);
            return run;
        }

        [Test] public void VersionSevenRoundTripKeepsEmptyStandardDtosProgressAndHistoricalFreeformData()
        {
            var profile = ProfileWithProgress();
            var historical = HistoricalFreeformPage();
            var standard = StandardPage();
            Assert.That(profile.SaveBlueprint(historical, out var reason), Is.True, reason);
            Assert.That(profile.SaveBlueprint(standard, out reason), Is.True, reason);
            Assert.That(profile.AssignSlot(0, standard.Id, out reason), Is.True, reason);
            Assert.That(profile.AssignSlot(3, historical.Id, out reason), Is.True, reason);
            var expected = profile.ToData();
            string historicalState = PageState(historical);
            var store = new WorkshopProfileStore(path);
            Assert.That(store.Save(profile).Success, Is.True, store.LastDiagnostic);

            var written = JsonUtility.FromJson<WorkshopProfileData>(File.ReadAllText(path));
            Assert.That(written.Version, Is.EqualTo(7));
            var dto = written.Album.Single(p => p.Id == standard.Id);
            Assert.That(dto.Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(dto.DrawingMetadataVersion, Is.EqualTo(1));
            Assert.That(dto.Drawing, Is.Not.Null, "An empty drawing still has the required v7 DTO.");
            Assert.That(dto.Drawing.Parts, Has.Length.EqualTo(DrawingSnapshot.LegacyPartCount));
            foreach (var part in dto.Drawing.Parts) Assert.That(part.Strokes, Is.Empty);
            Assert.That(dto.EvaluationMetadataVersion, Is.EqualTo(1));
            Assert.That(dto.Evaluation, Is.Not.Null, "The strict v7 reader also requires evaluation metadata.");
            Assert.That(dto.Evaluation.Feedback, Is.Not.Null);
            Assert.That(dto.FreeformRuleVersion, Is.Zero);

            byte[] committed = File.ReadAllBytes(path);
            var restartedStore = new WorkshopProfileStore(path);
            var loaded = restartedStore.Load();
            Assert.That(loaded.CanWrite, Is.True, loaded.Message);
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(committed), "Reading v7 must not rewrite the source.");
            Assert.That(loaded.Profile.Gold, Is.EqualTo(expected.Gold));
            Assert.That(loaded.Profile.ToData().Unlocked, Is.EqualTo(expected.Unlocked));
            Assert.That(loaded.Profile.Loadout, Is.EqualTo(expected.Loadout));
            Assert.That(loaded.Profile.Album.Select(p => p.Id), Is.EqualTo(expected.Album.Select(p => p.Id)));
            var restoredStandard = loaded.Profile.GetLoadoutBlueprints()[0];
            AssertStandard(restoredStandard);
            Assert.That(PageState(restoredStandard), Is.EqualTo(PageState(standard)));
            var restoredHistorical = loaded.Profile.GetLoadoutBlueprints()[3];
            Assert.That(restoredHistorical.Origin, Is.EqualTo(DrawingOrigin.Freeform));
            Assert.That(restoredHistorical.FreeformRuleVersion, Is.EqualTo(1));
            Assert.That(restoredHistorical.Drawing.GeometryHash, Is.EqualTo(historical.Drawing.GeometryHash));
            Assert.That(PageState(restoredHistorical), Is.EqualTo(historicalState));
            var rules = GameBalance.CreateDefault();
            Assert.That(TowerCatalog.WeaponRange(restoredHistorical, rules, restoredHistorical.FirstWeaponId),
                Is.EqualTo(TowerCatalog.WeaponRange(historical, rules, historical.FirstWeaponId)));
            Assert.That(TowerCatalog.FeedMultiplier(restoredHistorical, rules), Is.EqualTo(TowerCatalog.FeedMultiplier(historical, rules)));

            loaded.Profile.GrantGold(9);
            Assert.That(restartedStore.Save(loaded.Profile).Success, Is.True, restartedStore.LastDiagnostic);
            var final = new WorkshopProfileStore(path).Load().Profile;
            Assert.That(final.Gold, Is.EqualTo(expected.Gold + 9));
            Assert.That(final.ToData().Unlocked, Is.EqualTo(expected.Unlocked));
            Assert.That(final.Loadout, Is.EqualTo(expected.Loadout));
            AssertStandard(final.GetLoadoutBlueprints()[0]);
            Assert.That(PageState(final.GetLoadoutBlueprints()[3]), Is.EqualTo(historicalState));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void AddingStandardPageToEveryHistoricalVersionPreservesProgressAndOriginalBytes(int version)
        {
            var profile = ProfileWithProgress();
            if (version == 7)
                Assert.That(profile.SaveBlueprint(HistoricalFreeformPage(), out var reason), Is.True, reason);
            var data = version <= 3 ? HistoricalProfileTestData.Create(profile, version) : profile.ToData();
            data.Version = version;
            File.WriteAllText(path, JsonUtility.ToJson(data, true), new UnicodeEncoding(false, true));
            byte[] original = File.ReadAllBytes(path);
            var store = new WorkshopProfileStore(path);
            var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.True, loaded.Message);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "Load alone is never a migration commit.");
            var before = loaded.Profile.ToData();
            string[] historicalPages = loaded.Profile.Album.Select(PageState).ToArray();
            var standard = StandardPage("new.standard.v" + version);
            Assert.That(loaded.Profile.SaveBlueprint(standard, out var saveReason), Is.True, saveReason);
            Assert.That(store.Save(loaded.Profile).Success, Is.True, store.LastDiagnostic);
            string preservedPath = version < WorkshopProfile.CurrentVersion ? path + ".v" + version + "-original" : path + ".bak";
            Assert.That(File.ReadAllBytes(preservedPath), Is.EqualTo(original));

            var restored = new WorkshopProfileStore(path).Load();
            Assert.That(restored.CanWrite, Is.True, restored.Message);
            Assert.That(restored.Profile.ToData().Version, Is.EqualTo(7));
            Assert.That(restored.Profile.Gold, Is.EqualTo(before.Gold));
            Assert.That(restored.Profile.ToData().Unlocked, Is.EqualTo(before.Unlocked));
            Assert.That(restored.Profile.Loadout, Is.EqualTo(before.Loadout));
            Assert.That(restored.Profile.Album.Take(historicalPages.Length).Select(PageState), Is.EqualTo(historicalPages));
            AssertStandard(restored.Profile.Album.Single(p => p.Id == standard.Id));
        }

        static string WorkshopState(RunController run)
            => run.Workshop.Gold + "|" + string.Join("|", run.Workshop.Album.Select(PageState)) + "|" +
                string.Join(",", run.Workshop.Loadout.Select(p => p?.Id ?? "")) + "|" +
                string.Join(",", run.Workshop.Parts.Where(p => run.Workshop.IsUnlocked(p.Id)).Select(p => p.Id));

        [TestCase("save")] [TestCase("assign")] [TestCase("unlock")]
        public void RefusedStandardTransactionKeepsDraftGoldRightsAlbumSlotsAndDiskUnchanged(string command)
        {
            var profile = ProfileWithProgress();
            profile.ConfigureContent(LegacyContentCatalog.Create(GameBalancePresets.CreateTargetWorkshop()));
            var existing = StandardPage("existing.standard");
            Assert.That(profile.SaveBlueprint(existing, out var reason), Is.True, reason);
            var initialStore = new WorkshopProfileStore(path);
            Assert.That(initialStore.Save(profile).Success, Is.True, initialStore.LastDiagnostic);
            var io = new RefusingWriteIO();
            var run = Controller(new WorkshopProfileStore(path, io));
            var draft = StandardPage("unsaved.standard");
            string draftBefore = PageState(draft), before = WorkshopState(run);
            byte[] bytesBefore = File.ReadAllBytes(path);
            int published = 0;
            run.WorkshopChanged += () => published++;
            io.FailWrites = true;

            bool saved;
            if (command == "save") saved = run.SaveBlueprint(draft, out reason);
            else if (command == "assign") saved = run.AssignBlueprint(0, existing.Id, out reason);
            else saved = run.UnlockPart("module.feed", out reason);
            Assert.That(saved, Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(io.WriteAttempts, Is.EqualTo(1));
            Assert.That(WorkshopState(run), Is.EqualTo(before));
            Assert.That(PageState(draft), Is.EqualTo(draftBefore));
            AssertStandard(draft);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytesBefore));
            Assert.That(File.Exists(path + ".bak"), Is.False);
            Assert.That(published, Is.Zero);
            Assert.That(run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.WriteFailed));
            Assert.That(run.Workshop.HasUnsavedChanges, Is.False);

            io.FailWrites = false;
            Assert.That(run.RetrySave(out reason), Is.True, reason);
            Assert.That(WorkshopState(run), Is.EqualTo(before), "Retry writes the current profile without replaying a rejected command.");
            var restarted = new WorkshopProfileStore(path).Load().Profile;
            Assert.That(restarted.Gold, Is.EqualTo(profile.Gold));
            Assert.That(restarted.ToData().Unlocked, Is.EqualTo(profile.ToData().Unlocked));
            Assert.That(restarted.Loadout, Is.EqualTo(profile.Loadout));
            Assert.That(restarted.Album.Select(p => p.Id), Is.EqualTo(profile.Album.Select(p => p.Id)));
        }

        [Test] public void RestartedStandardPageCanBeAssignedBuiltAndFireWithTheTrialAssembly()
        {
            balance = ScriptableObject.CreateInstance<GameBalanceAsset>();
            balance.ResetToDefaults();
            balance.generateNewThreats = false; balance.enemyMechanics = false; balance.inkWells = false;
            balance.passiveInkPerSecond = 0; balance.earlyLaunchInkPerSecond = 0;
            balance.planningSeconds = 1000; balance.workshopStartingInk = 1000;
            var gun = balance.weapons.Single(w => w.kind == StructureKind.Gun);
            gun.buildSeconds = .25f; gun.range = 200; gun.damage = 1; gun.shotInterval = .2f;
            balance.enemies = new[] { new GameBalanceAsset.EnemySettings {
                kind = EnemyKind.Doodle, health = 500, speed = .1f, damage = 1, attackInterval = 1, reward = 0
            } };
            balance.waves = new[] { new GameBalanceAsset.WaveSettings { count = 1, sides = 1, spawnInterval = .1f } };
            var profile = ProfileWithProgress();
            var page = StandardPage("standard.combat");
            Assert.That(profile.SaveBlueprint(page, out var reason), Is.True, reason);
            Assert.That(new WorkshopProfileStore(path).Save(profile).Success, Is.True);

            var run = Controller(new WorkshopProfileStore(path));
            var loaded = run.Workshop.Album.Single(p => p.Id == page.Id);
            AssertStandard(loaded);
            Assert.That(run.AssignBlueprint(0, loaded.Id, out reason), Is.True, reason);
            Assert.That(new WorkshopProfileStore(path).Load().Profile.Loadout[0], Is.EqualTo(loaded.Id));
            var preview = run.PreviewBlueprint(loaded);
            Assert.That(preview.Valid, Is.True, preview.Reason);
            int gold = run.Workshop.Gold, ink = run.Session.Ink;
            long tick = run.Session.TickIndex;
            var trial = run.RunTrial(loaded, TrialScenario.Crowd);
            Assert.That(trial, Is.Not.Null);
            Assert.That(trial.First.Damage, Is.GreaterThan(0));
            var trialTower = trial.First.Frames[0].Towers[0];
            AssertStandard(trialTower.Blueprint);
            Assert.That(PageState(trialTower.Blueprint), Is.EqualTo(PageState(loaded)));
            Assert.That(trialTower.MaxHealth, Is.EqualTo(preview.Health));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(run.Session.Ink, Is.EqualTo(ink));
            Assert.That(run.Session.TickIndex, Is.EqualTo(tick));

            Assert.That(run.StartRun(out reason), Is.True, reason);
            var cell = new Cell(48, 24);
            var quote = run.QuoteBlueprint(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None);
            Assert.That(quote.Valid, Is.True, quote.Reason);
            ink = run.Session.Ink;
            Assert.That(run.BuildBlueprint(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
            Assert.That(ink - run.Session.Ink, Is.EqualTo(preview.Cost));
            var built = run.Session.StructureAt(cell).Value;
            AssertStandard(built.Blueprint);
            Assert.That(PageState(built.Blueprint), Is.EqualTo(PageState(trialTower.Blueprint)));
            Assert.That(built.MaxHealth, Is.EqualTo(preview.Health));
            Assert.That(built.IsComplete, Is.False, "The saved page must go through real construction.");
            for (int frame = 0; frame < 30; frame++) run.AdvanceFrame(FixedStepClock.Step, false, 1);
            Assert.That(run.Session.StructureAt(cell).Value.IsComplete, Is.True);
            int shots = 0;
            run.SimulationEventReceived += item => {
                if (item.Kind == SimulationEventKind.Shot && item.SourceStructureId == built.Id) shots++;
            };
            run.NextWave();
            for (int frame = 0; frame < 90 && shots == 0; frame++) run.AdvanceFrame(FixedStepClock.Step, false, 1);
            Assert.That(shots, Is.GreaterThan(0), "The restarted empty-drawing standard page must fire in the paid battle.");
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
        }

        sealed class RefusingWriteIO : IWorkshopProfileIO
        {
            public bool FailWrites;
            public int WriteAttempts;
            public string ReadAllText(string file) => File.ReadAllText(file);
            public void CreateDirectory(string folder) => Directory.CreateDirectory(folder);
            public void WriteAllText(string file, string contents)
            {
                WriteAttempts++;
                if (FailWrites) throw new IOException("Injected isolated profile write refusal");
                File.WriteAllText(file, contents);
            }
            public void Move(string source, string target) => File.Move(source, target);
            public void Copy(string source, string target) => File.Copy(source, target);
            public void Replace(string source, string target, string backup) => File.Replace(source, target, backup);
            public void Delete(string file) => File.Delete(file);
        }
    }
}
