using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OpenWorldEmoteCatalog))]
public sealed class OpenWorldEmoteCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        DrawList("maleAnimations", "Reset Male Display Names");
        DrawList("femaleAnimations", "Reset Female Display Names");
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawList(string propertyName, string buttonLabel)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
        if (!GUILayout.Button(buttonLabel)) return;
        serializedObject.ApplyModifiedProperties();
        ResetDisplayNames((OpenWorldEmoteCatalog)target, propertyName == "maleAnimations");
        serializedObject.Update();
    }

    public static void ResetDisplayNames(OpenWorldEmoteCatalog catalog, bool isMale)
    {
        var data = new SerializedObject(catalog);
        var animations = data.FindProperty(isMale ? "maleAnimations" : "femaleAnimations");
        for (int index = 0; index < animations.arraySize; index++)
            animations.GetArrayElementAtIndex(index).FindPropertyRelative("displayName").stringValue = "Emote " + (index + 1);
        data.ApplyModifiedProperties();
    }
}
