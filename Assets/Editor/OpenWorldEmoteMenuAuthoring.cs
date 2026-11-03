using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class OpenWorldEmoteMenuAuthoring
{
    private const string HudPath = "Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab";
    private const string RowPath = "Assets/Prefabs/Lobby/OpenWorldEmoteButton.prefab";
    private const string CatalogPath = "Assets/Animations/Lobby/OpenWorldEmotes.asset";

    [MenuItem("Tools/Open World Lobby/Rebuild Emote Menu")]
    public static void Rebuild()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<OpenWorldEmoteCatalog>(CatalogPath);
        if (catalog == null) throw new System.InvalidOperationException("Assign the authored OpenWorldEmotes catalog first.");
        catalog.ResolveAnimations();
        var root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var canvas = root.GetComponentInChildren<Canvas>(true).transform;
            var template = root.GetComponentsInChildren<Button>(true).Single(button => button.name == "Settings");
            var menu = root.GetComponent<OpenWorldEmoteMenu>();
            if (menu == null) menu = root.AddComponent<OpenWorldEmoteMenu>();
            var previous = new SerializedObject(menu);
            foreach (var field in new[] { "openButton", "panel" })
            {
                var component = previous.FindProperty(field).objectReferenceValue;
                var target = component is Component behaviour ? behaviour.gameObject : component as GameObject;
                if (target != null) Object.DestroyImmediate(target);
            }

            var open = Object.Instantiate(template, canvas, false);
            open.name = "Emotes";
            var openRect = (RectTransform)open.transform;
            openRect.anchoredPosition = ((RectTransform)template.transform).anchoredPosition + new Vector2(0f, -195f);
            open.onClick = new Button.ButtonClickedEvent();
            open.GetComponentInChildren<TMP_Text>(true).text = "Emotes";
            UnityEventTools.AddPersistentListener(open.onClick, menu.Toggle);

            var panel = Rect("Emote menu", canvas);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-360f, 0f);
            panel.sizeDelta = new Vector2(460f, 640f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.14f, 0.97f);

            var title = Object.Instantiate(template.GetComponentInChildren<TMP_Text>(true), panel, false);
            title.name = "Title";
            title.text = "Emotes";
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.fontSize = 30f;
            title.raycastTarget = false;
            Position(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(260f, 48f));

            var close = Object.Instantiate(template, panel, false);
            close.name = "Close emotes";
            Position((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(110f, 44f));
            close.GetComponentInChildren<TMP_Text>(true).text = "Close";
            close.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(close.onClick, menu.Close);

            var scrollRoot = Rect("Animations", panel);
            Stretch(scrollRoot, new Vector2(14f, 14f), new Vector2(-14f, -72f));
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            var viewport = Rect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, new Vector2(-22f, 0f));
            viewport.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = MakeScrollbar(scrollRoot);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPath);
            if (rowPrefab == null)
            {
                var row = Object.Instantiate(template, canvas, false);
                row.name = "OpenWorldEmoteButton";
                row.onClick = new Button.ButtonClickedEvent();
                var label = row.GetComponentInChildren<TMP_Text>(true);
                label.text = "Emote";
                label.fontSize = 24f;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 24f;
                label.raycastTarget = false;
                Position((RectTransform)row.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(390f, 48f));
                var size = row.gameObject.AddComponent<LayoutElement>();
                size.minHeight = 48f;
                size.preferredHeight = 48f;
                rowPrefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject, RowPath);
                Object.DestroyImmediate(row.gameObject);
            }

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("openButton").objectReferenceValue = open;
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("scroll").objectReferenceValue = scroll;
            serialized.FindProperty("buttonPrefab").objectReferenceValue = rowPrefab.GetComponent<Button>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            Debug.Log($"Authored emote menu with runtime-generated buttons in {HudPath}.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform));
        node.layer = LayerMask.NameToLayer("UI");
        node.transform.SetParent(parent, false);
        return (RectTransform)node.transform;
    }

    private static void Position(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minimum;
        rect.offsetMax = maximum;
    }

    private static Scrollbar MakeScrollbar(RectTransform parent)
    {
        var track = Rect("Scrollbar", parent);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(16f, 0f);
        track.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.22f, 0.3f, 1f);
        var slide = Rect("Sliding Area", track);
        Stretch(slide, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        var handle = Rect("Handle", slide);
        Stretch(handle, Vector2.zero, Vector2.zero);
        var image = handle.gameObject.AddComponent<Image>();
        image.color = new Color(0.7f, 0.76f, 0.9f, 1f);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = image;
        return scrollbar;
    }
}
