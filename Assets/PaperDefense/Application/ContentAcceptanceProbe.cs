using System;
using System.IO;
using UnityEngine;

namespace PaperDefense.Application
{
    /// <summary>A validation-scene entry point with explicit content references retained in a built Player.</summary>
    public sealed class ContentAcceptanceProbe : MonoBehaviour
    {
        public ContentAcceptanceManifestAsset manifest;
        public bool runOnStart = true;
        public bool requireCommandLine = true;
        public string gameScene = "PaperDefense";
        void Start()
        {
            if (!runOnStart) return;
            if (!requireCommandLine || Array.IndexOf(Environment.GetCommandLineArgs(), "-paper-content-smoke") >= 0) Run();
            else if (!string.IsNullOrEmpty(gameScene)) UnityEngine.SceneManagement.SceneManager.LoadScene(gameScene);
        }
        [ContextMenu("Run data-only content acceptance")]
        public void Run()
        {
            var result = ContentAcceptanceRunner.Run(manifest);
            string reportPath = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-paper-content-report") reportPath = args[i + 1];
            if (string.IsNullOrWhiteSpace(reportPath)) reportPath = Path.Combine(UnityEngine.Application.temporaryCachePath, "content-acceptance-report.json");
            try
            {
                string absolute = Path.GetFullPath(reportPath), folder = Path.GetDirectoryName(absolute);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(absolute, JsonUtility.ToJson(result, true));
                Debug.Log("Content data-only acceptance: " + (result.Success ? "PASS" : "FAIL") + "; report: " + absolute + "; " + result.Error);
            }
            catch (Exception error)
            {
                result.Success = false; Debug.LogError("Cannot write content acceptance report: " + error.Message);
            }
            if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(result.Success ? 0 : 1);
        }
    }
}
