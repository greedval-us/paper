using System;
using System.IO;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Validation
{
    /// <summary>The sole persistent mechanics fixture; every IO operation is confined to a fresh temporary directory.</summary>
    public sealed class MechanicsProfileFixtureStore : IWorkshopProfileStore, IDisposable
    {
        public string DirectoryPath { get; }
        public string ProfilePath => Path.Combine(DirectoryPath, "fixture.json");
        public int LoadCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public bool FailWrites { get; set; }
        WorkshopProfileStore store;
        public MechanicsProfileFixtureStore()
        {
            DirectoryPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaperDefenseMechanics", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(DirectoryPath); Reopen();
        }
        public void Reopen() => store = new WorkshopProfileStore(ProfilePath, new ConfinedIO(this));
        public ProfileLoadResult Load() { LoadCalls++; return store.Load(); }
        public ProfileSaveResult Save(WorkshopProfile profile) { SaveCalls++; return store.Save(profile); }
        public void CorruptPrimary()
        { Check(ProfilePath); File.WriteAllText(ProfilePath, "{invalid-fixture-json"); }
        public void WriteVersionFive(ContentCatalogSnapshot content, GameBalance balance)
        {
            var data = new WorkshopProfile(content, balance).ToData(); data.Version = 5;
            Check(ProfilePath); File.WriteAllText(ProfilePath, JsonUtility.ToJson(data));
        }
        void Check(string path)
        {
            string resolved = Path.GetFullPath(path);
            string prefix = DirectoryPath + Path.DirectorySeparatorChar;
            if (!resolved.Equals(DirectoryPath, StringComparison.OrdinalIgnoreCase) && !resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("fixture: path escapes temporary directory.");
        }
        public void Dispose()
        {
            if (!Directory.Exists(DirectoryPath)) return;
            // Known-owned files only, no recursive deletion or traversal of arbitrary paths.
            foreach (string path in Directory.GetFiles(DirectoryPath)) { Check(path); File.Delete(path); }
            Directory.Delete(DirectoryPath, false);
        }
        sealed class ConfinedIO : IWorkshopProfileIO
        {
            readonly MechanicsProfileFixtureStore owner;
            readonly WorkshopProfileFileIO files = new WorkshopProfileFileIO();
            public ConfinedIO(MechanicsProfileFixtureStore owner) { this.owner = owner; }
            public string ReadAllText(string path) { owner.Check(path); return files.ReadAllText(path); }
            public void CreateDirectory(string folder) { owner.Check(folder); files.CreateDirectory(folder); }
            public void WriteAllText(string path, string contents)
            { owner.Check(path); if (owner.FailWrites) throw new IOException("fixture: deliberate write failure."); files.WriteAllText(path, contents); }
            public void Move(string source, string target) { owner.Check(source); owner.Check(target); files.Move(source, target); }
            public void Copy(string source, string target) { owner.Check(source); owner.Check(target); files.Copy(source, target); }
            public void Replace(string source, string target, string backup)
            { owner.Check(source); owner.Check(target); owner.Check(backup); files.Replace(source, target, backup); }
            public void Delete(string path) { owner.Check(path); files.Delete(path); }
        }
    }
}
