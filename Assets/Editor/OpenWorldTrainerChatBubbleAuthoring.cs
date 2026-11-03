using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OpenWorldTrainerChatBubbleAuthoring
{
    private const string PrefabPath = "Assets/Prefabs/Lobby/OpenWorldTrainer.prefab";
    private const string SpritePath = "Assets/Sprites/Chat/t_HeroSelection_bg_Chat.png";

    [MenuItem("Tools/Open World Lobby/Wire Trainer Chat Bubble")]
    public static void Wire()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var bubble = root.GetComponent<OpenWorldTrainerChatBubble>();
            if (bubble == null) bubble = root.AddComponent<OpenWorldTrainerChatBubble>();
            var bubbleTransform = root.transform.Find("ChatBubble") as RectTransform;
            if (bubbleTransform == null)
            {
                var node = new GameObject("ChatBubble", typeof(RectTransform), typeof(Canvas), typeof(LookAtCamera));
                bubbleTransform = node.GetComponent<RectTransform>();
                bubbleTransform.SetParent(root.transform, false);
                bubbleTransform.localPosition = new Vector3(0f, 2.4f, 0f);
                bubbleTransform.localScale = Vector3.one * 0.007f;
                bubbleTransform.pivot = new Vector2(0.5f, 0f);
                bubbleTransform.sizeDelta = new Vector2(240f, 80f);
                node.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

                var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
                var backgroundRect = background.GetComponent<RectTransform>();
                backgroundRect.SetParent(bubbleTransform, false);
                Stretch(backgroundRect, Vector2.zero, Vector2.zero);
                var image = background.GetComponent<Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
                image.type = Image.Type.Sliced;
                image.raycastTarget = false;

                var textNode = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
                var textRect = textNode.GetComponent<RectTransform>();
                textRect.SetParent(bubbleTransform, false);
                Stretch(textRect, new Vector2(24f, 32f), new Vector2(-24f, -20f));
                var text = textNode.GetComponent<TextMeshProUGUI>();
                text.font = root.transform.Find("PlayerName").GetComponent<TMP_Text>().font;
                text.fontSize = 24f;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.richText = false;
                text.parseCtrlCharacters = false;
                text.raycastTarget = false;
                text.text = string.Empty;
            }

            var serialized = new SerializedObject(bubble);
            serialized.FindProperty("bubbleRoot").objectReferenceValue = bubbleTransform;
            serialized.FindProperty("messageText").objectReferenceValue = bubbleTransform.Find("Message").GetComponent<TMP_Text>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            bubbleTransform.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
