using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OpenWorldPokemonAuthoring
{
    private const string HudPath = "Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab";
    private const string TrainerPath = "Assets/Prefabs/Lobby/OpenWorldTrainer.prefab";
    private const string FollowerPath = "Assets/Prefabs/Lobby/OpenWorldPokemonFollower.prefab";
    private const string ButtonPath = "Assets/Prefabs/Lobby/OpenWorldPokemonButton.prefab";

    [MenuItem("Tools/Open World Lobby/Rebuild Pokémon Companions")]
    public static void Rebuild()
    {
        var boot = SceneManager.GetSceneByPath("Assets/Scenes/BootScene.unity");
        bool previewBoot = !boot.isLoaded;
        if (previewBoot) boot = EditorSceneManager.OpenPreviewScene("Assets/Scenes/BootScene.unity");
        var city = SceneManager.GetSceneByPath("Assets/Scenes/ChineseLobby.unity");
        bool previewCity = !city.isLoaded;
        if (previewCity) city = EditorSceneManager.OpenPreviewScene("Assets/Scenes/ChineseLobby.unity");
        try
        {
            var characters = boot.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CharactersList>(true))
                .Single().Characters.Where(character => character != null && !character.Hidden &&
                    character.pokemon != null && character.pokemon.RuntimeKeyIsValid()).ToArray();
            var surface = city.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true))
                .First(item => item.navMeshData != null);
            var follower = BuildFollower(surface.agentTypeID);
            var trainer = PrefabUtility.LoadPrefabContents(TrainerPath);
            try
            {
                var companion = trainer.GetComponent<OpenWorldPokemonCompanion>();
                if (companion == null) companion = trainer.AddComponent<OpenWorldPokemonCompanion>();
                var data = new SerializedObject(companion);
                data.FindProperty("followerPrefab").objectReferenceValue = follower;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(trainer, TrainerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(trainer); }
            BuildMenu(characters);
        }
        finally
        {
            if (previewCity) EditorSceneManager.ClosePreviewScene(city);
            if (previewBoot) EditorSceneManager.ClosePreviewScene(boot);
        }
    }

    private static OpenWorldPokemonFollower BuildFollower(int agentType)
    {
        var root = new GameObject("OpenWorldPokemonFollower");
        root.SetActive(false);
        try
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.agentTypeID = agentType;
            agent.speed = 5.5f;
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.radius = 0.4f;
            agent.height = 1.5f;
            agent.stoppingDistance = 1.5f;
            root.AddComponent<OpenWorldPokemonFollower>();
            root.SetActive(true);
            return PrefabUtility.SaveAsPrefabAsset(root, FollowerPath).GetComponent<OpenWorldPokemonFollower>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void BuildMenu(CharacterInfo[] characters)
    {
        var root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var canvas = root.GetComponentInChildren<Canvas>(true).transform;
            var template = root.GetComponentsInChildren<Button>(true).Single(button => button.name == "Settings");
            var menu = root.GetComponent<OpenWorldPokemonMenu>();
            if (menu == null) menu = root.AddComponent<OpenWorldPokemonMenu>();
            var data = new SerializedObject(menu);
            foreach (string field in new[] { "openButton", "panel" })
            {
                var previous = data.FindProperty(field).objectReferenceValue;
                var node = previous is Component component ? component.gameObject : previous as GameObject;
                if (node != null) Object.DestroyImmediate(node);
            }

            var open = Object.Instantiate(template, canvas, false);
            open.name = "Pokémon companion";
            Position((RectTransform)open.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(260f, 88f));
            open.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(open.onClick, menu.Toggle);
            var currentName = open.GetComponentInChildren<TMP_Text>(true);
            currentName.text = "Companion";
            currentName.fontSize = 24f;
            currentName.enableAutoSizing = true;
            currentName.fontSizeMin = 16f;
            currentName.fontSizeMax = 24f;
            Stretch(currentName.rectTransform, new Vector2(88f, 8f), new Vector2(-8f, -8f));
            var currentIcon = Icon("Current Pokémon", open.transform);
            Position(currentIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(72f, 72f));
            currentIcon.sprite = characters.First().CircleIcon;

            var panel = Rect("Pokémon companion menu", canvas);
            Position(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 122f), new Vector2(620f, 650f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.14f, 0.97f);
            var title = Label("Title", "Choose your companion", template, panel);
            title.fontSize = 30f;
            Position(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(450f, 44f));
            var status = Label("Status", "Choose a Pokémon to explore the city with you.", template, panel);
            status.fontSize = 20f;
            Position(status.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -58f), new Vector2(584f, 52f));
            var close = Object.Instantiate(template, panel, false);
            close.name = "Close companions";
            Position((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-14f, -14f), new Vector2(110f, 42f));
            close.GetComponentInChildren<TMP_Text>(true).text = "Close";
            close.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(close.onClick, menu.Close);

            var scrollRoot = Rect("Pokémon", panel);
            Stretch(scrollRoot, new Vector2(14f, 14f), new Vector2(-14f, -118f));
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
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
            content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.spacing = new Vector2(8f, 8f);
            grid.cellSize = new Vector2(178f, 132f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = Scrollbar(scrollRoot);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            data.Update();
            data.FindProperty("openButton").objectReferenceValue = open;
            data.FindProperty("currentIcon").objectReferenceValue = currentIcon;
            data.FindProperty("currentName").objectReferenceValue = currentName;
            data.FindProperty("status").objectReferenceValue = status;
            data.FindProperty("panel").objectReferenceValue = panel.gameObject;
            data.FindProperty("scroll").objectReferenceValue = scroll;
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
            if (buttonPrefab == null)
            {
                var button = Object.Instantiate(template, content, false);
                button.name = "OpenWorldPokemonButton";
                button.onClick = new Button.ButtonClickedEvent();
                var label = button.GetComponentInChildren<TMP_Text>(true);
                label.text = "Companion";
                label.fontSize = 22f;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 22f;
                Position(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(164f, 32f));
                var icon = Icon("Portrait", button.transform);
                Position(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(84f, 84f));
                buttonPrefab = PrefabUtility.SaveAsPrefabAsset(button.gameObject, ButtonPath);
                Object.DestroyImmediate(button.gameObject);
            }
            data.FindProperty("buttonPrefab").objectReferenceValue = buttonPrefab.GetComponent<Button>();
            data.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
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

    private static Image Icon(string name, Transform parent)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Label(string name, string text, Button template, Transform parent)
    {
        var label = Object.Instantiate(template.GetComponentInChildren<TMP_Text>(true), parent, false);
        label.name = name;
        label.text = text;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        return label;
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

    private static Scrollbar Scrollbar(RectTransform parent)
    {
        var track = Rect("Scrollbar", parent);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(16f, 0f);
        track.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.22f, 0.3f, 1f);
        var handle = Rect("Handle", track);
        Stretch(handle, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        var image = handle.gameObject.AddComponent<Image>();
        image.color = new Color(0.7f, 0.76f, 0.9f, 1f);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = image;
        return scrollbar;
    }
}
