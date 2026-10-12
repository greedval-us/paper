using System;
using System.IO;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    /// <summary>Profile snapshots are separate from transient battle state.</summary>
    public static class WorkshopPersistence
    {
        public static string ProfilePath=>Path.Combine(UnityEngine.Application.persistentDataPath,"paper-workshop-v1.json");
        public static string LastError {get;private set;}
        static readonly Dictionary<string, WorkshopProfileStore> stores = new Dictionary<string, WorkshopProfileStore>(StringComparer.OrdinalIgnoreCase);
        static WorkshopProfileStore Store(string path)
        {
            string absolute = Path.GetFullPath(path ?? ProfilePath);
            if (!stores.TryGetValue(absolute, out WorkshopProfileStore store))
            { store = new WorkshopProfileStore(absolute); stores.Add(absolute, store); }
            return store;
        }
        public static WorkshopProfile Load(string path=null)
        {
            ProfileLoadResult result = Store(path).Load();
            LastError = result.CanWrite ? "" : result.Message;
            if (!result.CanWrite) Debug.LogWarning("Не удалось прочитать альбом: " + result.Message);
            return result.Profile;
        }
        public static bool Save(WorkshopProfile profile,string path=null)
        {
            ProfileSaveResult result = Store(path).Save(profile);
            LastError = result.Success ? "" : result.Message;
            if (!result.Success) Debug.LogWarning("Не удалось сохранить альбом: " + result.Message);
            return result.Success;
        }
    }
}
