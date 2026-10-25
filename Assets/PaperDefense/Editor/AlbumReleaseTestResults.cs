using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Opt-in native NUnit XML export, retained across PlayMode domain reloads.</summary>
    [InitializeOnLoad]
    public static class AlbumReleaseTestResults
    {
        const string Key = "PaperDefense.ReleaseTestXml";
        static TestRunnerApi api;
        static readonly Recorder recorder = new Recorder();
        static AlbumReleaseTestResults() { if (!string.IsNullOrEmpty(SessionState.GetString(Key,""))) Register(); }
        static void Register()
        {
            if (api != null) return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.hideFlags = HideFlags.HideAndDontSave;
            api.RegisterCallbacks(recorder);
        }
        public static void Enable(string mode)
        {
            if (mode != "EditMode" && mode != "PlayMode") throw new ArgumentException(nameof(mode));
            string directory = Path.GetFullPath("docs/validation/modular-album/release");
            Directory.CreateDirectory(directory);
            SessionState.SetString(Key,Path.Combine(directory,mode.ToLowerInvariant()+"-results.xml"));
            Register();
        }
        public static void Disable() { SessionState.EraseString(Key); }
        sealed class Recorder : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string path = SessionState.GetString(Key,"");
                if (!string.IsNullOrEmpty(path)) TestRunnerApi.SaveResultToFile(result,path);
            }
        }
    }
}
