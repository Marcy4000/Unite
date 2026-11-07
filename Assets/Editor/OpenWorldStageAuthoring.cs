using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class OpenWorldStageAuthoring
{
    [MenuItem("Tools/Open World Lobby/Configure Dynamic Stage Menu")]
    public static void ConfigureDynamicMenu()
    {
        const string path = "Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab";
        const string buttonPath = "Assets/Prefabs/Lobby/OpenWorldStageTrackButton.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var menu = root.GetComponent<OpenWorldStageMenu>();
            var data = new SerializedObject(menu);
            var panel = (GameObject)data.FindProperty("panel").objectReferenceValue;
            var scroll = panel.GetComponentsInChildren<ScrollRect>(true).Single(item => item.content != null);
            var existingRows = scroll.content.Cast<Transform>().Where(child => child.GetComponent<Button>() != null).ToArray();
            var prefab = AssetDatabase.LoadAssetAtPath<OpenWorldStageTrackButton>(buttonPath);
            if (prefab == null)
            {
                if (existingRows.Length == 0) throw new InvalidOperationException("Assign a stage track button prefab or provide a row to convert.");
                var template = Object.Instantiate(existingRows[0].gameObject);
                try
                {
                    template.name = "OpenWorldStageTrackButton";
                    var button = template.GetComponent<Button>();
                    button.onClick = new Button.ButtonClickedEvent();
                    var label = template.GetComponentInChildren<TMP_Text>(true);
                    label.text = "Track name";
                    var rect = (RectTransform)template.transform;
                    float height = Mathf.Max(52f, rect.sizeDelta.y);
                    float width = Mathf.Max(1f, rect.rect.width);
                    rect.localScale = Vector3.one;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(width, height);
                    var layout = template.GetComponent<LayoutElement>() ?? template.AddComponent<LayoutElement>();
                    layout.minHeight = height;
                    layout.preferredWidth = width;
                    layout.preferredHeight = height;
                    layout.flexibleHeight = 0f;
                    var row = template.AddComponent<OpenWorldStageTrackButton>();
                    var rowData = new SerializedObject(row);
                    rowData.FindProperty("button").objectReferenceValue = button;
                    rowData.FindProperty("label").objectReferenceValue = label;
                    rowData.ApplyModifiedPropertiesWithoutUndo();
                    prefab = PrefabUtility.SaveAsPrefabAsset(template, buttonPath).GetComponent<OpenWorldStageTrackButton>();
                }
                finally { Object.DestroyImmediate(template); }
            }
            foreach (var row in existingRows) Object.DestroyImmediate(row.gameObject);
            data.FindProperty("scroll").objectReferenceValue = scroll;
            data.FindProperty("buttonPrefab").objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
