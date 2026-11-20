using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PaperDefense.Tests
{
    /// <summary>One opt-in NUnit XML writer for the current EditMode and PlayMode suites.</summary>
    [InitializeOnLoad]
    public static class TestRunResults
    {
        const string OutputKey = "PaperDefense.CurrentTestResultsXml";
        static TestRunnerApi api;
        static readonly Recorder recorder = new Recorder();

        static TestRunResults()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(OutputKey, ""))) Register();
        }

        static void Register()
        {
            if (api != null) return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.hideFlags = HideFlags.HideAndDontSave;
            api.RegisterCallbacks(recorder);
        }

        public static void Enable(string mode, string outputDirectory = "Temp/TestResults")
        {
            if (mode != "EditMode" && mode != "PlayMode") throw new ArgumentException(nameof(mode));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException(nameof(outputDirectory));
            string directory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(directory);
            SessionState.SetString(OutputKey, Path.Combine(directory, mode.ToLowerInvariant() + "-results.xml"));
            Register();
        }

        public static void Disable() { SessionState.EraseString(OutputKey); }

        sealed class Recorder : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string path = SessionState.GetString(OutputKey, "");
                if (string.IsNullOrEmpty(path)) return;
                TestRunnerApi.SaveResultToFile(result, path);
                Disable();
            }
        }
    }
}
