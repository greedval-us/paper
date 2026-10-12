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
        [MenuItem("Paper Defense/Проверить историческую мастерскую и чернильные рубежи")]
        public static void RunDefault()
        {
            SaveReplay("ink-loop-historical",RunDefaultJson());
        }

        [MenuItem("Paper Defense/Проверить целевую мастерскую и чернильные рубежи")]
        public static void RunTarget() => SaveReplay("ink-loop-target",RunTargetJson());

        public static string RunDefaultJson() => RunJson("Assets/PaperDefense/Configuration/NotebookBalance.asset");
        public static string RunTargetJson() => RunJson("Assets/PaperDefense/Configuration/TargetNotebookBalance.asset");

        static void SaveReplay(string prefix,string json)
        {
            string name=prefix+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".json";
            string path=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"..","docs","validation",name));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write))
            using(var writer=new StreamWriter(file))writer.Write(json);
            UnityEngine.Debug.Log("Ink-loop replay saved: "+path);
        }

        static string RunJson(string assetPath)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(assetPath);
            if(asset==null)throw new InvalidOperationException("Balance asset is missing: "+assetPath);
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
