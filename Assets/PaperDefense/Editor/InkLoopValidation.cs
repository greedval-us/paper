using System;
using System.Diagnostics;
using System.IO;
using PaperDefense.Application;
using PaperDefense.Validation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    public static class InkLoopValidation
    {
        [MenuItem("Paper Defense/Проверить мастерскую и чернильные рубежи")]
        public static void RunDefault()
        {
            string json=RunDefaultJson();
            string path=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"..","docs","validation","ink-loop-replay.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,json);
            UnityEngine.Debug.Log("Ink-loop replay saved: "+path);
        }
        public static string RunDefaultJson()
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/NotebookBalance.asset");
            if(asset==null)throw new InvalidOperationException("NotebookBalance asset is missing.");
            var scenario=new InkLoopScenario(asset.CreateWorkshopBalance());
            scenario.Report.SourceBalanceAsset=AssetDatabase.GetAssetPath(asset);
            var timer=Stopwatch.StartNew();while(!scenario.Finished)scenario.Step();timer.Stop();
            UnityEngine.Debug.Log("Ink-loop campaign: "+scenario.Report.Phase+", kills="+scenario.Report.Kills+"/"+
                scenario.Report.ExpectedKills+", well losses="+scenario.Report.WellLosses+", blue earned="+scenario.Report.BlueInkEarned+
                ", mechanics observed="+scenario.Report.MechanicsObserved+", CPU replay ms="+timer.Elapsed.TotalMilliseconds);
            return JsonUtility.ToJson(scenario.Report,true);
        }
    }
}
