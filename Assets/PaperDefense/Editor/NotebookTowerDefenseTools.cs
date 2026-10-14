using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    public static class NotebookTowerDefenseTools
    {
        public const string TargetPath="Assets/PaperDefense/Configuration/Content/Target.asset";
        [MenuItem("Paper Defense/Content/Apply notebook tower defense rules")]
        public static void ApplyMainPreset()
        {
            var target=AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(TargetPath);
            if(target==null)throw new System.InvalidOperationException("Target preset is missing.");
            var calibrated=NotebookTowerDefensePreset.Apply(target.CreateBalance());
            target.rules=GlobalBalanceSettings.From(calibrated);
            EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);
            Debug.Log("Notebook tower defense rules saved: roads, wells, terrain, support and six enemy families.");
        }
    }
}
