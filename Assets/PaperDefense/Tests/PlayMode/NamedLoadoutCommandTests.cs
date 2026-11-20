#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class NamedLoadoutCommandTests
    {
        static AssemblyCommandResult Command(AssemblyCommandFixture fixture, string name, params object[] arguments)
        {
            var method = typeof(AssemblyCommands).GetMethod(name);
            Assert.That(method, Is.Not.Null, "Missing named loadout command: " + name);
            return (AssemblyCommandResult)method.Invoke(fixture.Commands, arguments);
        }
        static object[] Sets(object model)
        {
            var property = model.GetType().GetProperty("NamedLoadouts");
            Assert.That(property, Is.Not.Null, "The read model must expose saved named loadouts.");
            return ((IEnumerable)property.GetValue(model)).Cast<object>().ToArray();
        }
        static T Value<T>(object model, string name) => (T)model.GetType().GetProperty(name).GetValue(model);
        static object Set(AssemblyCommandFixture fixture, string id)
            => Sets(fixture.Run.Workshop).Single(value => Value<string>(value, "Id") == id);
        static string[] Slots(object set) => ((IEnumerable)set.GetType().GetProperty("Slots").GetValue(set)).Cast<string>().ToArray();
        static string[] Current(AssemblyCommandFixture fixture) => fixture.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray();
        static string State(AssemblyCommandFixture fixture) => fixture.State() + "|sets:" + string.Join("|",
            Sets(fixture.Run.Workshop).Select(set => Value<string>(set, "Id") + ":" + Value<string>(set, "Name") + ":" + string.Join(",", Slots(set))));
        static string Save(AssemblyCommandFixture fixture, string name)
        {
            var result = Command(fixture, "SaveNamedLoadout", name, Guid.NewGuid());
            Assert.That(result.Success, Is.True, result.Code);
            Assert.That(result.ResultId, Is.Not.Null.And.Not.Empty);
            return result.ResultId;
        }
        static void Clear(AssemblyCommandFixture fixture)
        {
            for (int slot = 0; slot < 8; slot++)
                Assert.That(fixture.Commands.AssignSlot(slot, null, Guid.NewGuid()).Success, Is.True);
        }
        static object[] Arguments(string command, string id, Guid operation) => command == "SaveNamedLoadout"
            ? new object[] { "Второй", operation } : command == "RenameNamedLoadout"
            ? new object[] { id, "Новое имя", operation } : new object[] { id, operation };

        [TestCase("SaveNamedLoadout")] [TestCase("UpdateNamedLoadout")] [TestCase("RenameNamedLoadout")]
        [TestCase("DeleteNamedLoadout")] [TestCase("ApplyNamedLoadout")]
        public void EveryMutationCommitsOnceAndARefusedWriteCanRetryTheSameOperation(string command)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string id = Save(fixture, "Исходный"); string[] original = Current(fixture);
                Assert.That(fixture.Commands.AssignSlot(7, original[0], Guid.NewGuid()).Success, Is.True);
                string[] changed = Current(fixture);
                var operation = Guid.NewGuid(); var args = Arguments(command, id, operation);
                string before = State(fixture); byte[] bytes = fixture.SavedBytes;
                int writes = fixture.Store.SaveCalls, publications = 0;
                fixture.Run.WorkshopChanged += () => publications++;
                fixture.Store.RefuseWrites = true;
                var failed = Command(fixture, command, args);
                Assert.That(failed.Success, Is.False); Assert.That(failed.Code, Is.EqualTo("profile.write_failed"));
                Assert.That(State(fixture), Is.EqualTo(before)); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 1)); Assert.That(publications, Is.Zero);
                fixture.Store.RefuseWrites = false;
                var accepted = Command(fixture, command, args);
                Assert.That(accepted.Success, Is.True, accepted.Code);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 2)); Assert.That(publications, Is.EqualTo(1));
                if (command == "SaveNamedLoadout") Assert.That(Sets(fixture.Run.Workshop).Length, Is.EqualTo(2));
                if (command == "UpdateNamedLoadout") CollectionAssert.AreEqual(changed, Slots(Set(fixture, id)));
                if (command == "RenameNamedLoadout") Assert.That(Value<string>(Set(fixture, id), "Name"), Is.EqualTo("Новое имя"));
                if (command == "DeleteNamedLoadout") Assert.That(Sets(fixture.Run.Workshop), Is.Empty);
                CollectionAssert.AreEqual(command == "ApplyNamedLoadout" ? original : changed, Current(fixture));
                string after = State(fixture); byte[] committed = fixture.SavedBytes;
                Assert.That(Command(fixture, command, args).Success, Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 2)); Assert.That(publications, Is.EqualTo(1));
                Assert.That(State(fixture), Is.EqualTo(after)); Assert.That(fixture.SavedBytes, Is.EqualTo(committed));
                fixture.Restart();
                Assert.That(Command(fixture, command, args).Success, Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 2)); Assert.That(State(fixture), Is.EqualTo(after));
                string conflicting = command == "SaveNamedLoadout" ? "DeleteNamedLoadout" : "SaveNamedLoadout";
                Assert.That(Command(fixture, conflicting, Arguments(conflicting, id, operation)).Code, Is.EqualTo("profile.operation.conflict"));
            }
        }

        [TestCase("empty")] [TestCase("support")] [TestCase("last-attack")]
        public void ApplyingAPartialSetPreservesEightPositionsAndLeavesBattleAdmissionToTheExistingGate(string kind)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string original = Save(fixture, "Полный");
                string chosen = kind == "support" ? fixture.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Structure).Id
                    : fixture.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun").Id;
                Clear(fixture);
                if (kind != "empty") Assert.That(fixture.Commands.AssignSlot(7, chosen, Guid.NewGuid()).Success, Is.True);
                string[] partial = Current(fixture); string saved = Save(fixture, "Частичный");
                Assert.That(Command(fixture, "ApplyNamedLoadout", original, Guid.NewGuid()).Success, Is.True);
                int writes = fixture.Store.SaveCalls; string before = State(fixture); byte[] bytes = fixture.SavedBytes;
                Assert.That(Command(fixture, "CheckNamedLoadout", saved).Success, Is.True, "Applying a saved work in progress must remain possible.");
                Assert.That(State(fixture), Is.EqualTo(before)); Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                Assert.That(Command(fixture, "ApplyNamedLoadout", saved, Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 1));
                CollectionAssert.AreEqual(partial, Current(fixture)); Assert.That(Current(fixture).Length, Is.EqualTo(8));
                Assert.That(fixture.Run.CanStartAlbumRun(out _), Is.EqualTo(kind == "last-attack"));
            }
        }

        [Test] public void CheckAndApplyRejectLockedReferencesWithoutPublishingAnySlot()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string id = Save(fixture, "Проверка прав");
                var data = fixture.SavedData;
                var source = fixture.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                    entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
                var tower = source.Snapshot.Recipe.Tower; var weapon = tower.Mounts[0].Weapon;
                var lockedWeapon = new WeaponRecipe(weapon.Mechanism, new PartRef("head.long", 1), weapon.Sight, weapon.Magazine, weapon.Trigger, weapon.Mount);
                var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, lockedWeapon) }));
                data.Entries[Array.FindIndex(data.Entries, entry => entry.Id == source.Id)] = new AlbumEntryData(
                    new AlbumEntry(source.Id, source.Name, source.Revision, new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions)));
                data.Unlocked = data.Unlocked.Where(part => part != "head.long").ToArray();
                File.WriteAllText(fixture.ProfilePath, JsonUtility.ToJson(data)); fixture.Restart();
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.False, "A non-starter permission remains locked after loading.");
                string before = State(fixture); byte[] bytes = fixture.SavedBytes; int writes = fixture.Store.SaveCalls;
                Assert.That(Command(fixture, "CheckNamedLoadout", id).Success, Is.False);
                Assert.That(Command(fixture, "ApplyNamedLoadout", id, Guid.NewGuid()).Success, Is.False);
                Assert.That(Command(fixture, "CheckNamedLoadout", "missing.set").Success, Is.False);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(State(fixture), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
            }
        }

        [Test] public void SavedSetsReferenceCurrentPagesWhileDeleteClearsEveryReferenceOnlyAfterCommit()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var source = fixture.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
                string first = Save(fixture, "Первый"), second = Save(fixture, "Второй");
                var edited = fixture.Plated(source);
                Assert.That(fixture.Commands.SaveEntry(edited, Guid.NewGuid()).Success, Is.True);
                Assert.That(Command(fixture, "ApplyNamedLoadout", first, Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.Workshop.LoadoutEntries.Single(entry => entry?.Id == source.Id).Revision, Is.EqualTo(source.Revision + 1));
                var variant = fixture.Commands.SaveEntryAsVariant(edited, Guid.NewGuid()); Assert.That(variant.Success, Is.True, variant.Code);
                Assert.That(Slots(Set(fixture, first)), Does.Contain(source.Id).And.Not.Contain(variant.ResultId));
                string before = State(fixture); byte[] bytes = fixture.SavedBytes; Guid deletion = Guid.NewGuid();
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Commands.DeleteEntry(source.Id, deletion).Success, Is.False);
                Assert.That(State(fixture), Is.EqualTo(before)); Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Commands.DeleteEntry(source.Id, deletion).Success, Is.True);
                Assert.That(Current(fixture), Does.Not.Contain(source.Id));
                Assert.That(Slots(Set(fixture, first)), Does.Not.Contain(source.Id));
                Assert.That(Slots(Set(fixture, second)), Does.Not.Contain(source.Id));
                Assert.That(fixture.Commands.RestoreStarterEntries(Guid.NewGuid()).Success, Is.True);
                fixture.Restart();
                Assert.That(Slots(Set(fixture, first)), Does.Not.Contain(source.Id), "Restoring a page must not revive an explicitly deleted saved-set binding.");
            }
        }

        [Test] public void CommandsAreBlockedDuringBattleAndLaterSwitchesCannotChangeTheFrozenRun()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string first = Save(fixture, "Первый"); string[] original = Current(fixture);
                Assert.That(fixture.Commands.AssignSlot(7, original[0], Guid.NewGuid()).Success, Is.True);
                string second = Save(fixture, "Второй");
                Assert.That(Command(fixture, "ApplyNamedLoadout", first, Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
                var frozen = fixture.Run.RunLoadout;
                var frozenSlots = frozen.Entries.Select(entry => entry?.Id).ToArray();
                float damage = frozen.ResolveSlot(0, MaterialTier.Marker).Weapons[0].Damage;
                int writes = fixture.Store.SaveCalls; string before = State(fixture);
                foreach (string command in new[] { "SaveNamedLoadout", "UpdateNamedLoadout", "RenameNamedLoadout", "DeleteNamedLoadout", "ApplyNamedLoadout" })
                    Assert.That(Command(fixture, command, Arguments(command, first, Guid.NewGuid())).Success, Is.False, command);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes)); Assert.That(State(fixture), Is.EqualTo(before));
                fixture.Run.OpenWorkshop();
                Assert.That(Command(fixture, "ApplyNamedLoadout", second, Guid.NewGuid()).Success, Is.True);
                CollectionAssert.AreEqual(frozenSlots, frozen.Entries.Select(entry => entry?.Id));
                Assert.That(frozen.ResolveSlot(0, MaterialTier.Marker).Weapons[0].Damage, Is.EqualTo(damage));
                Assert.That(Current(fixture), Is.Not.EqualTo(frozenSlots));
            }
        }

        [Test] public void PendingRewardRetryMergesWithASuccessfullySwitchedAndSavedSetExactlyOnce()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string saved = Save(fixture, "До боя");
                var rules = fixture.Preset.rules;
                rules.PlanningSeconds = rules.IntermissionSeconds = 10000;
                rules.Progression.UpgradeEveryWaves = 0;
                rules.Waves = Enumerable.Range(0, 8).Select(index => new WaveDefinition { Count = 0, InkGrant = 0 }).ToList();
                Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
                Guid runId = fixture.Run.Session.RunId;
                fixture.Store.RefuseWrites = true;
                fixture.Run.NextWave(); fixture.Run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                fixture.Run.OpenWorkshop(); fixture.Store.RefuseWrites = false;
                Clear(fixture);
                Assert.That(Command(fixture, "ApplyNamedLoadout", saved, Guid.NewGuid()).Success, Is.True);
                Save(fixture, "После боя"); string[] slots = Current(fixture);
                Assert.That(fixture.Run.RetrySave(out reason), Is.True, reason);
                int gold = fixture.Run.Workshop.Gold, xp = fixture.Run.Workshop.TotalXp;
                Assert.That(gold, Is.GreaterThan(0)); Assert.That(xp, Is.GreaterThan(0));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
                Assert.That(fixture.Run.RetrySave(out _), Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold)); Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(xp));
                fixture.Restart();
                CollectionAssert.AreEqual(slots, Current(fixture)); Assert.That(Sets(fixture.Run.Workshop).Length, Is.EqualTo(2));
                Assert.That(fixture.SavedData.Rewards.Single(receipt => receipt.RunId == runId.ToString("N")).Waves, Is.EqualTo(1));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void VersionNineMigrationPreservesSlotsAndOriginalBytesBeforeTheFirstNamedSetCommit(bool backup)
        {
            using (var fixture = new AssemblyCommandFixture(gold: 73, xp: 50))
            {
                Assert.That(typeof(AssemblyCommands).GetMethod("SaveNamedLoadout"), Is.Not.Null, "Missing named-loadout migration owner.");
                var old = fixture.SavedData; old.Version = 9; old.Loadout[2] = null;
                var field = old.GetType().GetField("NamedLoadouts"); if (field != null) field.SetValue(old, null);
                string source = backup ? fixture.ProfilePath + ".bak" : fixture.ProfilePath;
                File.WriteAllText(source, JsonUtility.ToJson(old, true)); byte[] original = File.ReadAllBytes(source);
                if (backup) File.Delete(fixture.ProfilePath);
                var store = new WorkshopProfileStore(fixture.ProfilePath, catalog: fixture.Catalog, legacyBalance: fixture.Balance);
                var loaded = store.Load();
                Assert.That(loaded.CanWrite, Is.True, loaded.Message); Assert.That(loaded.RequiresMigration, Is.True);
                CollectionAssert.AreEqual(old.Loadout, loaded.Profile.Loadout);
                Assert.That(loaded.Profile.Gold, Is.EqualTo(73)); Assert.That(loaded.Profile.TotalXp, Is.EqualTo(50));
                Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
                Assert.That(File.Exists(fixture.ProfilePath + ".v9-original"), Is.False);
                fixture.Restart(); Save(fixture, "Сохранённый");
                Assert.That(File.ReadAllBytes(fixture.ProfilePath + ".v9-original"), Is.EqualTo(original));
                Assert.That(fixture.SavedData.Version, Is.EqualTo(WorkshopProfile.CurrentVersion));
                fixture.Restart(); CollectionAssert.AreEqual(old.Loadout, Current(fixture));
                Assert.That(Sets(fixture.Run.Workshop).Length, Is.EqualTo(1));
            }
        }

        [TestCase("root")] [TestCase("set")]
        public void FuturePrimaryMetadataCannotBeOverwrittenEvenWithACompatibleBackup(string future)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Save(fixture, "Текущий"); var data = fixture.SavedData;
                File.Copy(fixture.ProfilePath, fixture.ProfilePath + ".bak", true);
                if (future == "root") data.Version = WorkshopProfile.CurrentVersion + 1;
                else
                {
                    var field = data.GetType().GetField("NamedLoadouts"); Assert.That(field, Is.Not.Null);
                    var saved = ((IEnumerable)field.GetValue(data)).Cast<object>().Single();
                    saved.GetType().GetField("Version").SetValue(saved, 999);
                }
                File.WriteAllText(fixture.ProfilePath, JsonUtility.ToJson(data));
                byte[] original = File.ReadAllBytes(fixture.ProfilePath), backup = File.ReadAllBytes(fixture.ProfilePath + ".bak");
                var store = new WorkshopProfileStore(fixture.ProfilePath, catalog: fixture.Catalog, legacyBalance: fixture.Balance);
                var loaded = store.Load(); Assert.That(loaded.CanWrite, Is.False); Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
                Assert.That(store.Save(WorkshopProfile.CreateNew(fixture.Catalog)).Success, Is.False);
                Assert.That(File.ReadAllBytes(fixture.ProfilePath), Is.EqualTo(original));
                Assert.That(File.ReadAllBytes(fixture.ProfilePath + ".bak"), Is.EqualTo(backup));
            }
        }

        [Test] public void FirstRefitSlotAssignmentAndMarkerRemainIndependentOfASavedNamedSet()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string preset = Save(fixture, "До обучения"); string[] slots = Slots(Set(fixture, preset));
                fixture.BalanceAsset.campaignEnabled = true; fixture.Restart();
                Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
                var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.Run);
                typeof(GameSession).GetProperty("CompletedWaves").SetValue(session, fixture.Run.ActiveMission.WaveCount);
                typeof(GameSession).GetProperty("Phase").SetValue(session, RunPhase.Victory); fixture.Run.StepSimulation();
                Assert.That(fixture.Run.BeginFirstRefitTutorial(out reason), Is.True, reason); fixture.Run.OpenWorkshop();
                var source = fixture.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
                var tower = source.Snapshot.Recipe.Tower; var weapon = tower.Mounts[0].Weapon;
                var longWeapon = new WeaponRecipe(weapon.Mechanism, new PartRef("head.long", 1), weapon.Sight, weapon.Magazine, weapon.Trigger, weapon.Mount);
                var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, longWeapon) }));
                var draft = new AlbumEntry(source.Id, "Дальний вариант", source.Revision, new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
                var saved = fixture.Commands.SaveEntryAsVariant(draft, Guid.NewGuid()); Assert.That(saved.Success, Is.True, saved.Code);
                Assert.That(fixture.Commands.AssignSlot(0, saved.ResultId, Guid.NewGuid()).Success, Is.True);
                CollectionAssert.AreEqual(slots, Slots(Set(fixture, preset)));
                Assert.That(fixture.Run.CompleteFirstRefitTutorial(saved.ResultId, out reason), Is.True, reason);
                Assert.That(Command(fixture, "ApplyNamedLoadout", preset, Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.FirstRefitTutorialHandled, Is.True);
                Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == saved.ResultId), Is.True);
                CollectionAssert.AreEqual(slots, Current(fixture));
            }
        }

        [Test] public void ANewerPrimaryAppearingAfterCheckRejectsTheCommitWithoutPublishingTheSwitchedSlots()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string id = Save(fixture, "До изменения"); string[] original = Current(fixture);
                Assert.That(fixture.Commands.AssignSlot(7, original[0], Guid.NewGuid()).Success, Is.True);
                Assert.That(Command(fixture, "CheckNamedLoadout", id).Success, Is.True);
                string before = State(fixture); var future = fixture.SavedData; future.Version = WorkshopProfile.CurrentVersion + 1;
                File.WriteAllText(fixture.ProfilePath, JsonUtility.ToJson(future)); byte[] bytes = fixture.SavedBytes;
                var result = Command(fixture, "ApplyNamedLoadout", id, Guid.NewGuid());
                Assert.That(result.Success, Is.False); Assert.That(State(fixture), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes)); Assert.That(fixture.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.RecoveryRequired));
                int writes = fixture.Store.SaveCalls;
                Assert.That(Command(fixture, "CheckNamedLoadout", id).Success, Is.False);
                Assert.That(Command(fixture, "SaveNamedLoadout", "Недоступный", Guid.NewGuid()).Success, Is.False);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
            }
        }

        [Test] public void MalformedNamedSetUsesTheCompatibleBackupAndKeepsCorruptBytesDuringRecovery()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string id = Save(fixture, "Резервный"); string[] expected = Current(fixture);
                File.Copy(fixture.ProfilePath, fixture.ProfilePath + ".bak", true);
                byte[] backup = File.ReadAllBytes(fixture.ProfilePath + ".bak");
                var malformed = fixture.SavedData;
                var field = malformed.GetType().GetField("NamedLoadouts"); Assert.That(field, Is.Not.Null);
                var saved = ((IEnumerable)field.GetValue(malformed)).Cast<object>().Single();
                saved.GetType().GetField("Slots").SetValue(saved, new string[7]);
                File.WriteAllText(fixture.ProfilePath, JsonUtility.ToJson(malformed)); byte[] corrupt = fixture.SavedBytes;
                var store = new WorkshopProfileStore(fixture.ProfilePath, catalog: fixture.Catalog, legacyBalance: fixture.Balance);
                Assert.That(store.Load().Source, Is.EqualTo(ProfileLoadSource.Backup));
                fixture.Restart();
                CollectionAssert.AreEqual(expected, Slots(Set(fixture, id)));
                Assert.That(Command(fixture, "RenameNamedLoadout", id, "Восстановленный", Guid.NewGuid()).Success, Is.True);
                Assert.That(File.ReadAllBytes(fixture.ProfilePath + ".bak"), Is.EqualTo(backup));
                var quarantined = Directory.GetFiles(fixture.DirectoryPath, "profile.json.corrupt-*");
                Assert.That(quarantined.Length, Is.EqualTo(1)); Assert.That(File.ReadAllBytes(quarantined[0]), Is.EqualTo(corrupt));
                fixture.Restart(); Assert.That(Value<string>(Set(fixture, id), "Name"), Is.EqualTo("Восстановленный"));
            }
        }
    }
}
#endif
