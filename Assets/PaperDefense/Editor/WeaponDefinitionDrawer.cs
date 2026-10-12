using System.Collections.Generic;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Hide archived enum/slow columns from weapon authoring; EffectIds is the single effect source.</summary>
    [CustomPropertyDrawer(typeof(WeaponDefinitionInput))]
    sealed class WeaponDefinitionDrawer : PropertyDrawer
    {
        const float Gap=2;
        static IEnumerable<SerializedProperty> Rows(SerializedProperty property)
        {
            var iterator=property.Copy(); var end=iterator.GetEndProperty(); bool enter=true;
            while(iterator.NextVisible(enter) && !SerializedProperty.EqualContents(iterator,end))
            {
                enter=false;
                if(iterator.depth!=property.depth+1 || iterator.name=="LegacyKind")continue;
                if(iterator.name=="Stats")
                {
                    var stats=iterator.Copy(); var statsEnd=stats.GetEndProperty(); bool enterStats=true;
                    while(stats.NextVisible(enterStats) && !SerializedProperty.EqualContents(stats,statsEnd))
                    { enterStats=false; if(stats.name!="Kind" && stats.name!="SlowDuration" && stats.name!="SlowSpeedMultiplier")yield return stats.Copy(); }
                }
                else yield return iterator.Copy();
            }
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height=EditorGUIUtility.singleLineHeight;
            if(property.isExpanded)foreach(var row in Rows(property))height+=EditorGUI.GetPropertyHeight(row,true)+Gap;
            return height;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position,label,property);
            var line=new Rect(position.x,position.y,position.width,EditorGUIUtility.singleLineHeight);
            property.isExpanded=EditorGUI.Foldout(line,property.isExpanded,label,true);
            if(property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach(var row in Rows(property))
                {
                    line.y+=line.height+Gap; line.height=EditorGUI.GetPropertyHeight(row,true);
                    var title=new GUIContent(row.displayName);
                    if(row.propertyPath.Contains(".Stats."))title.tooltip="Параметр существующего алгоритма атаки. Эффекты настраиваются записями EffectIds.";
                    EditorGUI.PropertyField(line,row,title,true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
