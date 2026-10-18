using System;
using System.IO;
using System.Text;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    public enum ProfileLoadSource { NewProfile, Primary, Backup, RecoveryRequired }
    public sealed class ProfileLoadResult
    {
        public WorkshopProfile Profile { get; }
        public ProfileLoadSource Source { get; }
        public string Message { get; }
        public bool CanWrite { get; }
        public bool RequiresMigration { get; }
        public ProfileLoadResult(WorkshopProfile profile, ProfileLoadSource source, string message, bool canWrite, bool requiresMigration = false)
        { Profile = profile; Source = source; Message = message ?? ""; CanWrite = canWrite; RequiresMigration = requiresMigration; }
    }
    public sealed class ProfileSaveResult
    {
        public bool Success { get; }
        public string Message { get; }
        public ProfileSaveResult(bool success, string message) { Success = success; Message = message ?? ""; }
    }
    public interface IWorkshopProfileStore
    {
        ProfileLoadResult Load();
        ProfileSaveResult Save(WorkshopProfile profile);
    }
    public interface IWorkshopProfileIO
    {
        string ReadAllText(string path);
        void CreateDirectory(string folder);
        void WriteAllText(string path, string contents);
        void Move(string source, string target);
        void Copy(string source, string target);
        void Replace(string source, string target, string backup);
        void Delete(string path);
    }
    public sealed class WorkshopProfileStore : IWorkshopProfileStore
    {
        readonly string path;
        readonly IWorkshopProfileIO io;
        readonly ComponentCatalog catalog;
        readonly GameBalance legacyBalance;
        bool loaded, protectedSource;
        string protectionMessage = "";
        SourceRead compatibleSource;
        public string LastDiagnostic { get; private set; }
        const int MaximumJsonBytes = 32 * 1024 * 1024;

        public WorkshopProfileStore(string path, IWorkshopProfileIO io = null, ComponentCatalog catalog = null, GameBalance legacyBalance = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException(UserText.Message("copy.4f8c45706d1efa1c"), nameof(path));
            this.path = Path.GetFullPath(path);
            this.io = io ?? new WorkshopProfileFileIO();
            this.catalog = catalog;
            this.legacyBalance = legacyBalance;
        }

        public ProfileLoadResult Load()
        {
            SourceRead primary;
            ProfileLoadResult result = ReadSources(out primary);
            loaded = true;
            if (!result.CanWrite)
            {
                protectedSource = true;
                protectionMessage = result.Message;
            }
            // A store that has seen an unsafe source never silently becomes writable on retry.
            if (protectedSource && result.CanWrite)
                return new ProfileLoadResult(result.Profile, ProfileLoadSource.RecoveryRequired, protectionMessage, false, result.RequiresMigration);
            return result;
        }

        public ProfileSaveResult Save(WorkshopProfile profile)
        {
            if (!loaded) Load();
            if (protectedSource) return new ProfileSaveResult(false, protectionMessage);
            if (profile == null) return new ProfileSaveResult(false, UserText.Message("copy.1d086d2bd6375b84"));

            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                // Re-check immediately before writing: an externally updated future profile is protected too.
                SourceRead primary;
                ProfileLoadResult current = ReadSources(out primary);
                if (!current.CanWrite)
                {
                    protectedSource = true; protectionMessage = current.Message;
                    return new ProfileSaveResult(false, protectionMessage);
                }

                WorkshopProfileData candidate = profile.ToData();
                string canonical = JsonUtility.ToJson(candidate);
                ValidateCandidate(canonical, canonical);
                string serialized = JsonUtility.ToJson(candidate, true);
                // Full legacy drawing albums can exceed the read limit through indentation alone.
                // The already validated compact form preserves the complete DTO within the same limit.
                if (Encoding.UTF8.GetByteCount(serialized) > MaximumJsonBytes) serialized = canonical;
                ValidateCandidate(serialized, canonical);
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) io.CreateDirectory(folder);
                io.WriteAllText(temporary, serialized);
                // Validate the bytes read back from the temporary file, not just the object in memory.
                ValidateCandidate(io.ReadAllText(temporary), canonical);

                // Migration is committed only after a byte-for-byte copy of the actual historical source exists.
                PreserveHistoricalOriginal(compatibleSource);

                if (primary.Kind == SourceKind.Corrupt)
                {
                    // Keep the damaged source separately, and leave the known-good .bak untouched.
                    string quarantine = path + ".corrupt-" + Guid.NewGuid().ToString("N");
                    io.Move(path, quarantine);
                    io.Move(temporary, path);
                }
                else if (primary.Kind == SourceKind.Valid)
                    io.Replace(temporary, path, path + ".bak");
                else
                    io.Move(temporary, path);

                // Commit is confirmed here. Optional cleanup in finally cannot turn this into false.
                return new ProfileSaveResult(true, "");
            }
            catch (Exception error) when (IsExpectedFileError(error))
            {
                LastDiagnostic = error.ToString();
                return new ProfileSaveResult(false, UserText.Message(error is UnauthorizedAccessException
                    ? "profile.error.write_access" : "profile.error.write"));
            }
            finally
            {
                try { io.Delete(temporary); }
                catch (Exception error) when (IsExpectedFileError(error)) { }
            }
        }

        ProfileLoadResult ReadSources(out SourceRead primary)
        {
            compatibleSource = null;
            primary = Read(path);
            if (primary.Kind == SourceKind.Valid)
            {
                compatibleSource = primary;
                return new ProfileLoadResult(primary.Profile, ProfileLoadSource.Primary, "", true,
                    primary.Version < WorkshopProfile.CurrentVersion);
            }
            if (primary.Kind == SourceKind.Future || primary.Kind == SourceKind.Failure)
                return Recovery(primary.Message);

            SourceRead backup = Read(path + ".bak");
            if (backup.Kind == SourceKind.Valid)
            {
                compatibleSource = backup;
                return new ProfileLoadResult(backup.Profile, ProfileLoadSource.Backup,
                    primary.Kind == SourceKind.Corrupt
                        ? UserText.Message("copy.080ccb22e8fd751f")
                        : UserText.Message("copy.4faf04ac2281dfea"), true,
                    backup.Version < WorkshopProfile.CurrentVersion);
            }
            if (primary.Kind == SourceKind.Missing && backup.Kind == SourceKind.Missing)
                return new ProfileLoadResult(WorkshopProfile.CreateNew(catalog, legacyBalance?.ResolveContent(), legacyBalance),
                    ProfileLoadSource.NewProfile, "", true);
            return Recovery(UserText.Message("copy.9a7f65a4cb32092f", primary.Message, backup.Message));
        }

        static ProfileLoadResult Recovery(string message) => new ProfileLoadResult(new WorkshopProfile(),
            ProfileLoadSource.RecoveryRequired, string.IsNullOrEmpty(message) ? UserText.Message("copy.17e344fd85f0edd1") : message, false);

        SourceRead Read(string file)
        {
            string json;
            try { json = io.ReadAllText(file); }
            catch (FileNotFoundException) { return new SourceRead(SourceKind.Missing, null, UserText.Message("copy.0120c5597f3a306e")); }
            catch (DirectoryNotFoundException) { return new SourceRead(SourceKind.Missing, null, UserText.Message("copy.f73342db09eb3711")); }
            catch (Exception error) when (IsExpectedFileError(error))
            {
                LastDiagnostic = error.ToString();
                return new SourceRead(SourceKind.Failure, null, UserText.Message(error is UnauthorizedAccessException
                    ? "profile.error.read_access" : "profile.error.read"));
            }
            if (json != null && Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
                return new SourceRead(SourceKind.Failure, null, UserText.Message("copy.6da2cfdd4088f27b"));
            try
            {
                int version = ReadVersion(json);
                if (version > WorkshopProfile.CurrentVersion)
                    return new SourceRead(SourceKind.Future, null, UserText.Message("copy.78db079a610f6075"));
                if (version < 1 || version > WorkshopProfile.CurrentVersion) throw new InvalidDataException(UserText.Message("copy.0ecaed17b95a05bc"));
                var data = JsonUtility.FromJson<WorkshopProfileData>(json);
                if (data == null) throw new InvalidDataException(UserText.Message("copy.46070c4c3dff7836"));
                return new SourceRead(SourceKind.Valid, WorkshopProfile.FromData(data, catalog, legacyBalance), "", version, json, file);
            }
            catch (NotSupportedException error)
            { LastDiagnostic = error.ToString(); return new SourceRead(SourceKind.Future, null, UserText.Message("profile.error.future_schema")); }
            catch (Exception error) when (IsExpectedFileError(error))
            { LastDiagnostic = error.ToString(); return new SourceRead(SourceKind.Corrupt, null, UserText.Message("profile.error.corrupt")); }
        }

        static int ReadVersion(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
                throw new InvalidDataException(UserText.Message("copy.c1ec68178b351e23"));
            string trimmed = json.Trim();
            if (trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                throw new InvalidDataException(UserText.Message("copy.7cc1c8a34ac9f61d"));
            var version = JsonUtility.FromJson<VersionHeader>(json);
            if (version == null) throw new InvalidDataException(UserText.Message("copy.22aa0f221a9126a9"));
            return version.Version;
        }

        void ValidateCandidate(string json, string expectedCanonical)
        {
            if (ReadVersion(json) != WorkshopProfile.CurrentVersion) throw new InvalidDataException(UserText.Message("copy.2a929baad02aaaad"));
            var candidate = JsonUtility.FromJson<WorkshopProfileData>(json);
            if (candidate == null || JsonUtility.ToJson(candidate) != expectedCanonical)
                throw new InvalidDataException(UserText.Message("copy.5c3e3703a7f7927e"));
            WorkshopProfile.FromData(candidate, catalog, legacyBalance);
        }

        void PreserveHistoricalOriginal(SourceRead source)
        {
            if (source == null || source.Version < 1 || source.Version >= WorkshopProfile.CurrentVersion) return;
            string original = path + ".v" + source.Version + "-original";
            try
            {
                io.ReadAllText(original);
                // Decoded text equality cannot prove that the original encoding and bytes match.
                // An earlier migration original is evidence too; preserve a fresh exact copy separately.
                original += "-" + Guid.NewGuid().ToString("N");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            string temporaryOriginal = original + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                io.Copy(source.File, temporaryOriginal);
                string copied = io.ReadAllText(temporaryOriginal);
                if (copied != source.Json || ReadVersion(copied) != source.Version)
                    throw new InvalidDataException(UserText.Message("copy.f3943c56f42d599a", source.Version));
                io.Move(temporaryOriginal, original);
            }
            finally
            {
                try { io.Delete(temporaryOriginal); }
                catch (Exception error) when (IsExpectedFileError(error)) { }
            }
        }

        static bool IsExpectedFileError(Exception error) => error is IOException || error is InvalidDataException ||
            error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException ||
            error is System.Security.SecurityException;

        enum SourceKind { Missing, Valid, Corrupt, Future, Failure }
        sealed class SourceRead
        {
            public SourceKind Kind { get; }
            public WorkshopProfile Profile { get; }
            public string Message { get; }
            public int Version { get; }
            public string Json { get; }
            public string File { get; }
            public SourceRead(SourceKind kind, WorkshopProfile profile, string message, int version = 0, string json = null, string file = null)
            { Kind = kind; Profile = profile; Message = message; Version = version; Json = json; File = file; }
        }
        [Serializable] sealed class VersionHeader { public int Version = 0; }
    }

    /// <summary>Read exceptions distinguish missing files from denied or failed access.</summary>
    public sealed class WorkshopProfileFileIO : IWorkshopProfileIO
    {
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void CreateDirectory(string folder) => Directory.CreateDirectory(folder);
        public void WriteAllText(string path, string contents)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(contents); writer.Flush(); stream.Flush(true);
            }
        }
        public void Move(string source, string target) => File.Move(source, target);
        public void Copy(string source, string target) => File.Copy(source, target, false);
        public void Replace(string source, string target, string backup) => File.Replace(source, target, backup);
        public void Delete(string path) => File.Delete(path);
    }
}
