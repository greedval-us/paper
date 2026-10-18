using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace PaperDefense.Tests
{
    /// <summary>Opt-in XML capture for runs in the already-open editor.</summary>
    [InitializeOnLoad]
    public sealed class AssemblyValidationResults : ICallbacks
    {
        public const string OutputKey = "PaperDefense.AssemblyValidationXml";
        static AssemblyValidationResults() { TestRunnerApi.RegisterTestCallback(new AssemblyValidationResults()); }
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string path = SessionState.GetString(OutputKey, "");
            if (string.IsNullOrEmpty(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            TestRunnerApi.SaveResultToFile(result, path);
            SessionState.EraseString(OutputKey);
        }
    }
}
