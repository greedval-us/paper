using System;
using System.Collections.Generic;
using System.IO;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    public static class ContentDatabaseMigration
    {
        public const string DirectoryPath="Assets/PaperDefense/Configuration/Content";
        public const string DatabasePath=DirectoryPath+"/NotebookContent.asset";
        [Serializable] sealed class TemplateCapture { public List<DrawingTemplateDefinitionInput> Templates; }

        [MenuItem("Paper Defense/Content/Migrate legacy balance assets once")]
        public static void Migrate()
        {
            var oldHistorical=AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/NotebookBalance.asset");
            var oldTarget=AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/TargetNotebookBalance.asset");
            if (oldHistorical==null || oldTarget==null) throw new InvalidOperationException("Не найдены исходные ассеты баланса.");
            if (oldHistorical.dataPreset!=null || oldTarget.dataPreset!=null || AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(DatabasePath)!=null)
                throw new InvalidOperationException("Миграция уже выполнена: существующие данные не перезаписываются.");
            var historical=oldHistorical.CreateWorkshopBalance(); var target=NotebookTowerDefensePreset.Apply(oldTarget.CreateWorkshopBalance());
            var input=LegacyContentCatalog.CreateInput(historical);
            string capture=Path.GetFullPath("docs/validation/content/before/templates.json");
            var templates=JsonUtility.FromJson<TemplateCapture>(File.ReadAllText(capture));
            if (templates==null || templates.Templates==null) throw new InvalidOperationException("Отсутствует исходный снимок контуров.");
            input.Templates=templates.Templates;
            foreach (var part in input.Chassis) part.TemplateId="template."+part.Id;
            foreach (var part in input.Weapons) part.TemplateId="template."+part.Id;
            foreach (var part in input.Modules) part.TemplateId="template."+part.Id;
            input.CreateSnapshot();
            Directory.CreateDirectory(DirectoryPath); AssetDatabase.Refresh();
            var database=ScriptableObject.CreateInstance<ContentDatabaseAsset>(); database.catalog=input;
            var hp=ScriptableObject.CreateInstance<BalancePresetAsset>(); hp.name="Historical"; hp.database=database; hp.rules=GlobalBalanceSettings.From(historical);
            var tp=ScriptableObject.CreateInstance<BalancePresetAsset>(); tp.name="Target"; tp.database=database; tp.rules=GlobalBalanceSettings.From(target);
            var targetInput=LegacyContentCatalog.CreateInput(target);
            var armor=targetInput.Modules.Find(item=>item.Id=="module.armor"); armor.TemplateId="template."+armor.Id; tp.overrides.Modules.Add(armor);
            // Prepare both complete candidates before publishing any references to the existing assets.
            hp.CreateBalance(); tp.CreateBalance();
            AssetDatabase.CreateAsset(database,DatabasePath);
            AssetDatabase.CreateAsset(hp,DirectoryPath+"/Historical.asset"); AssetDatabase.CreateAsset(tp,DirectoryPath+"/Target.asset");
            database.presets=new[]{hp,tp}; EditorUtility.SetDirty(database);
            oldHistorical.dataPreset=hp; oldTarget.dataPreset=tp; oldHistorical.legacyAuthoringAdapter=false; oldTarget.legacyAuthoringAdapter=false;
            EditorUtility.SetDirty(oldHistorical); EditorUtility.SetDirty(oldTarget);
            AssetDatabase.SaveAssetIfDirty(database); AssetDatabase.SaveAssetIfDirty(hp); AssetDatabase.SaveAssetIfDirty(tp);
            AssetDatabase.SaveAssetIfDirty(oldHistorical); AssetDatabase.SaveAssetIfDirty(oldTarget);
            Debug.Log("Content migration complete. Original balance assets and scene GUIDs preserved.");
        }

        [MenuItem("Paper Defense/Content/Validate all databases and presets")]
        public static void ValidateAll()
        {
            int count=0;
            foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabaseAsset"))
            { var database=AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AssetDatabase.GUIDToAssetPath(guid)); database.CreateSnapshot(); count++; }
            foreach (string guid in AssetDatabase.FindAssets("t:BalancePresetAsset"))
            { AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(AssetDatabase.GUIDToAssetPath(guid)).CreateBalance(); count++; }
            Debug.Log("Validated content assets: "+count);
        }
    }

    [CustomEditor(typeof(GameBalanceAsset))]
    sealed class GameBalanceAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update(); EditorGUILayout.PropertyField(serializedObject.FindProperty("dataPreset"));
            var asset=(GameBalanceAsset)target;
            if (asset.dataPreset==null && asset.legacyAuthoringAdapter) DrawPropertiesExcluding(serializedObject,"m_Script","dataPreset");
            else if (asset.dataPreset==null) EditorGUILayout.HelpBox("Назначьте пресет контента. Старый баланс не подставляется вместо потерянной ссылки.",MessageType.Error);
            else
            {
                EditorGUILayout.HelpBox("Активный источник: каталог + пресет. Старые поля сохранены для миграции и совместимости.",MessageType.Info);
                if (GUILayout.Button("Открыть активный пресет")) Selection.activeObject=asset.dataPreset;
                if (GUILayout.Button("Открыть каталог")) Selection.activeObject=asset.dataPreset.database;
            }
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("Проверить подготовку"))
                try { asset.CreateWorkshopBalance(); Debug.Log(asset.name+": OK"); } catch(Exception e) { Debug.LogError(e.Message,asset); }
        }
    }

    [CustomEditor(typeof(ContentDatabaseAsset))]
    sealed class ContentDatabaseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Проверить каталог и связанные пресеты"))
                try
                {
                    var database=(ContentDatabaseAsset)target; database.CreateSnapshot();
                    foreach (var preset in database.presets) { if(preset==null || preset.database!=database)throw new ArgumentException("presets: ссылка должна указывать на пресет этой базы."); preset.CreateBalance(); }
                    Debug.Log(database.name+": OK");
                } catch(Exception e) { Debug.LogError(e.Message,target); }
            EditorGUILayout.HelpBox("Дублируйте запись в списке Inspector, задайте новый стабильный Id, сбросьте LegacyKind/LegacyModule, назначьте TemplateId и проверьте базу. Для нового варианта не нужен C#.",MessageType.Info);
        }
    }

    [CustomEditor(typeof(BalancePresetAsset))]
    sealed class BalancePresetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Проверить пресет"))
                try { ((BalancePresetAsset)target).CreateBalance(); Debug.Log(target.name+": OK"); } catch(Exception e) { Debug.LogError(e.Message,target); }
        }
    }
}
