using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Authors editable graphics rows using the settings menu's existing dropdown style.</summary>
public static class DisplaySettingsAuthoring
{
    [MenuItem("Tools/Settings/Wire Graphics Controls")]
    public static void WireGraphicsControls()
    {
        var manager = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (manager == null) throw new System.InvalidOperationException("Open BootScene to author its settings menu.");
        var serialized = new SerializedObject(manager);
        var template = (TMP_Dropdown)serialized.FindProperty("frameRateDropdown").objectReferenceValue;
        AddRow(serialized, template, "refreshRateDropdown", "DisplayRefreshRate", "Refresh rate:",
            new[] { "System default", "60 Hz (Requested)", "90 Hz (Requested)", "120 Hz (Requested)", "144 Hz (Requested)", "165 Hz (Requested)", "240 Hz (Requested)" }, 5);
        AddRow(serialized, template, "renderScaleDropdown", "RenderScale", "Render scale:",
            new[] { "Use preset", "50%", "67%", "75%", "85%", "100% (Native)" }, 6);
        AddRow(serialized, template, "antiAliasingDropdown", "AntiAliasing", "Anti-aliasing:",
            new[] { "Use preset", "Off", "FXAA (Fast)", "SMAA (Sharper)" }, 7);
        AddRow(serialized, template, "postProcessingDropdown", "PostProcessing", "Post-processing:",
            new[] { "Use preset", "Off", "On" }, 8);
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
    }

    private static void AddRow(SerializedObject manager, TMP_Dropdown template, string field, string name, string label, string[] options, int index)
    {
        var property = manager.FindProperty(field);
        var dropdown = property.objectReferenceValue as TMP_Dropdown;
        if (dropdown == null)
        {
            var row = Object.Instantiate(template.transform.parent.gameObject, template.transform.parent.parent, false);
            Undo.RegisterCreatedObjectUndo(row, "Add graphics setting");
            row.name = name;
            dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
            var title = row.GetComponentsInChildren<TMP_Text>(true).Single(text => !text.transform.IsChildOf(dropdown.transform));
            title.text = label;
            // These labels need room on the existing single-line rows.
            title.enableAutoSizing = true;
            title.fontSizeMin = 18;
            title.fontSizeMax = title.fontSize;
            property.objectReferenceValue = dropdown;
        }
        dropdown.transform.parent.name = name;
        var rowTitle = dropdown.transform.parent.GetComponentsInChildren<TMP_Text>(true).Single(text => !text.transform.IsChildOf(dropdown.transform));
        rowTitle.text = label;
        dropdown.transform.parent.SetSiblingIndex(index);
        // Never inherit another row's persistent callbacks.
        var control = new SerializedObject(dropdown);
        control.FindProperty("m_OnValueChanged.m_PersistentCalls.m_Calls").ClearArray();
        control.ApplyModifiedProperties();
        dropdown.ClearOptions();
        dropdown.AddOptions(options.ToList());
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
    }
}
