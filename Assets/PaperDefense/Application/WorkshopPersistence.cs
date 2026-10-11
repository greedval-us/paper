using System;
using System.IO;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    /// <summary>Profile snapshots are separate from transient battle state.</summary>
    public static class WorkshopPersistence
    {
        public static string ProfilePath=>Path.Combine(UnityEngine.Application.persistentDataPath,"paper-workshop-v1.json");
        public static string LastError {get;private set;}
        public static WorkshopProfile Load(string path=null)
        {
            LastError="";path=path??ProfilePath;
            try
            {
                if(!File.Exists(path))return new WorkshopProfile();
                string json=File.ReadAllText(path);
                if(json.Length>1024*1024)throw new InvalidDataException("Профиль слишком большой.");
                var data=JsonUtility.FromJson<WorkshopProfileData>(json);
                if(data==null||data.Version!=1)throw new InvalidDataException("Неизвестная версия профиля.");
                return WorkshopProfile.FromData(data);
            }
            catch(Exception e) when(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is ArgumentException)
            {LastError=e.Message;Debug.LogWarning("Не удалось прочитать альбом: "+e.Message);return new WorkshopProfile();}
        }
        public static bool Save(WorkshopProfile profile,string path=null)
        {
            LastError="";path=path??ProfilePath;
            try
            {
                string folder=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(folder))Directory.CreateDirectory(folder);
                string temporary=path+".tmp";File.WriteAllText(temporary,JsonUtility.ToJson(profile.ToData(),true));
                if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
                return true;
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException)
            {LastError=e.Message;Debug.LogWarning("Не удалось сохранить альбом: "+e.Message);return false;}
        }
    }
}
