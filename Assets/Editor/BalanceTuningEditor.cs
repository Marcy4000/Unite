using System;
using UnityEditor;
using UnityEngine;
using Unite.Updates;

[CustomEditor(typeof(BalanceTuning))]
public sealed class BalanceTuningEditor : Editor
{
    private string damageSearch = "";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "damageOverrides");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Character Damage", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Enter actual Attack/Sp. Attack coefficients, damage per level, and flat damage. Editing a value enables its override; uncheck it to restore the authored formula. For conditional values, an enabled override replaces that expression for all variants using this hit ID. Export through the existing balance patch workflow.", MessageType.Info);
        damageSearch = EditorGUILayout.TextField("Find damage", damageSearch);
        var entries = serializedObject.FindProperty("damageOverrides");
        int visible = 0;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("id").stringValue;
            if (!string.IsNullOrEmpty(damageSearch) && id.IndexOf(damageSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
            visible++;
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, id, true);
            if (!entry.isExpanded) continue;
            EditorGUI.indentLevel++;
            string formula = entry.FindPropertyRelative("authoredFormula").stringValue;
            if (!string.IsNullOrEmpty(formula)) EditorGUILayout.HelpBox(formula, MessageType.None);
            DrawValue(entry, "overrideAttack", "attackCoefficient", "Attack / Sp. Attack coefficient");
            DrawValue(entry, "overrideLevel", "levelDamage", "Damage per level");
            DrawValue(entry, "overrideBase", "baseDamage", "Base damage");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.LabelField($"{visible} of {entries.arraySize} damage entries", EditorStyles.miniLabel);
        serializedObject.ApplyModifiedProperties();
    }
    private static void DrawValue(SerializedProperty entry, string flagName, string fieldName, string label)
    {
        var enabled = entry.FindPropertyRelative(flagName);
        var value = entry.FindPropertyRelative(fieldName);
        EditorGUILayout.BeginHorizontal();
        enabled.boolValue = EditorGUILayout.Toggle(new GUIContent("", "Override this coefficient. Uncheck to use the authored formula."), enabled.boolValue, GUILayout.Width(18));
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(value, new GUIContent(label));
        if (EditorGUI.EndChangeCheck()) enabled.boolValue = true;
        EditorGUILayout.EndHorizontal();
    }

}
