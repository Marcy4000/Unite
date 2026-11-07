using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OpenWorldTrainerMenuAuthoring
{
    [MenuItem("Tools/Open World Lobby/Rebuild Trainer Menu")]
    public static void Rebuild()
    {
        const string path = "Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var canvas = root.GetComponentInChildren<Canvas>(true);
            var menu = root.GetComponent<OpenWorldTrainerMenu>() ?? root.AddComponent<OpenWorldTrainerMenu>();
            var data = new SerializedObject(menu);
            var previous = data.FindProperty("panel").objectReferenceValue;
            if (previous != null) Object.DestroyImmediate(previous);
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrainerModels/TrainerCard.prefab").GetComponent<TrainerCardUI>();
            var template = canvas.GetComponentsInChildren<Button>(true).First(button => button.name == "Settings");
            var rect = new GameObject("Trainer interaction", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(780f, 640f);
            rect.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.14f, 0.98f);
            var panel = rect.gameObject;
            var playerName = Label(template, rect, "Trainer", new Vector2(0f, 275f), new Vector2(500f, 55f), 32f);
            var close = MakeButton(template, rect, "Close", new Vector2(305f, 275f), new Vector2(125f, 48f));
            var card = (TrainerCardUI)PrefabUtility.InstantiatePrefab(cardPrefab, rect);
            var cardRect = (RectTransform)card.transform;
            Place(cardRect, new Vector2(-200f, -10f), new Vector2(300f, 456f));
            var privateChat = MakeButton(template, rect, "Private chat", new Vector2(170f, 65f), new Vector2(280f, 55f));
            Label(template, rect, "Match history is coming soon!", new Vector2(170f, -65f), new Vector2(330f, 100f), 25f);
            panel.SetActive(false);

            data.FindProperty("panel").objectReferenceValue = panel;
            data.FindProperty("playerName").objectReferenceValue = playerName;
            data.FindProperty("card").objectReferenceValue = card;
            data.FindProperty("privateChat").objectReferenceValue = privateChat;
            data.FindProperty("closeButton").objectReferenceValue = close;
            data.FindProperty("chat").objectReferenceValue = root.GetComponent<ChatManager>();
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Button MakeButton(Button template, Transform parent, string text, Vector2 position, Vector2 size)
    {
        var button = Object.Instantiate(template, parent, false);
        button.name = text;
        button.onClick = new Button.ButtonClickedEvent();
        button.GetComponentInChildren<TMP_Text>(true).text = text;
        Place((RectTransform)button.transform, position, size);
        return button;
    }

    private static TMP_Text Label(Button template, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Object.Instantiate(template.GetComponentInChildren<TMP_Text>(true), parent, false);
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        Place(label.rectTransform, position, size);
        return label;
    }
}
