using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Creates the initial editable assets once. Existing prefabs/settings are never
// regenerated or overwritten, so artist changes survive future tooling runs.
public static class SpectatorUIAuthoring
{
    private const string Folder = "Assets/Prefabs/Spectator";
    private const string SettingsPath = "Assets/Resources/Spectator/SpectatorUIAssets.asset";
    private static readonly Color Ink = new Color(0.035f, 0.045f, 0.075f, 0.86f);

    [MenuItem("Tools/Unite/Spectator/Create Missing UI Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Assets/Resources/Spectator");
        AssetDatabase.Refresh();
        var settings = AssetDatabase.LoadAssetAtPath<SpectatorUIAssets>(SettingsPath);
        bool isNew = settings == null;
        if (isNew)
        {
            settings = ScriptableObject.CreateInstance<SpectatorUIAssets>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.blueCardBackground = settings.blueCardBackground != null ? settings.blueCardBackground : Sprite("t_observe_switch_player_bg");
        settings.orangeCardBackground = settings.orangeCardBackground != null ? settings.orangeCardBackground : Sprite("t_observe_switch_player_bg1");
        if (settings.neutralCardBackground == null) settings.neutralCardBackground = Sprite("t_observe_switch_player_bg_monochrome");
        if (settings.banSlot == null)
        {
            settings.banSlot = Existing<DraftBanHolder>("SpectatorBanSlot");
            if (settings.banSlot == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CharacterSelect/DraftMode/DraftBanHolder.prefab");
                settings.banSlot = Save<DraftBanHolder>(UnityEngine.Object.Instantiate(source), "SpectatorBanSlot");
            }
        }
        if (settings.playerCard == null) settings.playerCard = Existing<SpectatorPlayerCard>("SpectatorPlayerCard") ?? BuildPlayerCard();
        if (settings.selectionCard == null) settings.selectionCard = Existing<SpectatorSelectionCard>("SpectatorSelectionCard") ?? BuildSelectionCard();
        if (settings.teamPanel == null) settings.teamPanel = Existing<SpectatorTeamPanel>("SpectatorTeamPanel") ?? BuildTeamPanel();
        if (settings.battleHUD == null) settings.battleHUD = Existing<SpectatorBattleHUD>("SpectatorBattleHUD") ?? BuildBattleHUD();
        if (settings.selectionOverlay == null) settings.selectionOverlay = Existing<SpectatorSelectionOverlay>("SpectatorSelectionOverlay") ?? BuildSelectionOverlay();
        if (settings.lobbySlot == null) settings.lobbySlot = Existing<LobbyPlayerIcon>("SpectatorLobbySlot") ?? BuildLobbySlot();
        if (settings.lobbySlots == null) settings.lobbySlots = Existing<RectTransform>("SpectatorLobbySlots") ?? BuildLobbySlots();
        EnsureBanStrips();
        EditorUtility.SetDirty(settings);
        if (isNew)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MapInfo", new[] { "Assets/Maps" }))
            {
                var map = AssetDatabase.LoadAssetAtPath<MapInfo>(AssetDatabase.GUIDToAssetPath(guid));
                if (map == null) continue;
                map.spectatorBattleLayout = SpectatorTeamLayout.BlueVersusOthers;
                map.spectatorSelectionLayout = SpectatorTeamLayout.ProportionalTeams;
                map.spectatorScoreTimerOverride = map.sceneName == "RemoatStadium" || map.sceneName == "TheiaSkyRuins" || map.sceneName == "MerStadium";
                EditorUtility.SetDirty(map);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Spectator UI ready. Edit Assets/Prefabs/Spectator and Assets/Resources/Spectator/SpectatorUIAssets.asset; per-map options are on MapInfo.");
    }

    private static T Existing<T>(string name) where T : Component => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab")?.GetComponent<T>();
    private static T Save<T>(GameObject root, string name) where T : Component
    {
        root.name = name;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<T>();
    }
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GenericUI/SpectatorMode/" + name + ".png");
    private static RectTransform Rect(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rect = (RectTransform)go.transform;
        if (parent != null) rect.SetParent(parent, false);
        SpectatorTeamPanel.Place(rect, Vector2.zero, Vector2.one);
        return rect;
    }
    private static void Area(RectTransform rect, float x, float y, float w, float h) =>
        SpectatorTeamPanel.Place(rect, new Vector2(x, y), new Vector2(x + w, y + h));
    private static Image Image(string name, Transform parent, Color color, Sprite sprite = null)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }
    private static TMP_Text Text(string name, Transform parent, string value, float size, Color? color = null)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.color = color ?? Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(12, size);
        text.fontSizeMax = size;
        text.raycastTarget = false;
        return text;
    }
    private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(name);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + name + " not found");
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject CanvasRoot(string name, int order)
    {
        var root = Rect(name).gameObject;
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        return root;
    }

    private static SpectatorPlayerCard BuildPlayerCard()
    {
        var root = Rect("SpectatorPlayerCard");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(108, 200);
        var view = root.gameObject.AddComponent<SpectatorPlayerCard>();
        var bg = Image("Background", root, new Color(0.43f, 0.2f, 0.91f), Sprite("t_observe_switch_player_bg_monochrome"));
        bg.preserveAspect = true;
        bg.raycastTarget = true;
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        var selected = Image("FollowingHighlight", root, new Color(1, 0.88f, 0.1f, 0.9f));
        var outline = selected.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.yellow;
        outline.effectDistance = new Vector2(3, 3);
        selected.color = new Color(1, 0.87f, 0.03f, 0.3f);
        selected.gameObject.SetActive(false);
        var portrait = Image("Portrait", root, Color.white);
        portrait.preserveAspect = true;
        Area(portrait.rectTransform, 0.11f, 0.28f, 0.78f, 0.48f);
        var hpBG = Image("HPBackground", root, Color.black);
        Area(hpBG.rectTransform, 0.08f, 0.23f, 0.84f, 0.035f);
        var hp = Image("HPFill", hpBG.transform, new Color(0.58f, 1, 0.05f), AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"));
        hp.type = UnityEngine.UI.Image.Type.Filled;
        hp.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        var energy = Text("Energy", root, "0", 28);
        Area(energy.rectTransform, 0.1f, 0.83f, 0.8f, 0.15f);
        var level = Text("Level", root, "Lv.1", 22);
        level.fontStyle = FontStyles.Bold | FontStyles.Italic;
        Area(level.rectTransform, 0.05f, 0.06f, 0.9f, 0.14f);
        var name = Text("PlayerName", root, "Player", 15);
        Area(name.rectTransform, 0.01f, 0.75f, 0.98f, 0.1f);
        var ready = Image("UniteReady", root, new Color(0.95f, 0.9f, 0.1f), AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"));
        Area(ready.rectTransform, 0.42f, 0.76f, 0.16f, 0.08f);
        var death = Image("DeathOverlay", root, new Color(0, 0, 0, 0.6f));
        Area(death.rectTransform, 0.06f, 0.27f, 0.88f, 0.49f);
        var seconds = Text("DeathTimer", death.transform, "", 44);
        Set(view, "background", bg); Set(view, "portrait", portrait); Set(view, "hpBar", hp);
        Set(view, "energy", energy); Set(view, "level", level); Set(view, "playerName", name);
        Set(view, "uniteReady", ready.gameObject); Set(view, "deathOverlay", death.gameObject);
        Set(view, "deathTimer", seconds); Set(view, "selected", selected.gameObject); Set(view, "button", button);
        return Save<SpectatorPlayerCard>(root.gameObject, "SpectatorPlayerCard");
    }

    private static SpectatorSelectionCard BuildSelectionCard()
    {
        var root = Rect("SpectatorSelectionCard");
        root.anchorMin = root.anchorMax = root.pivot = Vector2.one * 0.5f;
        root.sizeDelta = new Vector2(288, 384); // Same 3:4 ratio as LoadingScreenPlayer.
        var view = root.gameObject.AddComponent<SpectatorSelectionCard>();
        var bg = Image("TeamBackground", root, new Color(0.43f, 0.2f, 0.91f));
        var face = Image("PortraitPanel", root, new Color(0.71f, 0.9f, 0.95f));
        Area(face.rectTransform, 0.025f, 0.22f, 0.95f, 0.75f);
        var portrait = Image("PokemonPortrait", face.transform, Color.white);
        portrait.preserveAspect = true;
        var strip = Image("NameStrip", root, Ink);
        Area(strip.rectTransform, 0.025f, 0.13f, 0.95f, 0.18f);
        var name = Text("PlayerName", strip.transform, "Player", 24);
        var status = Text("State", face.transform, "Choosing…", 21, new Color(0.12f, 0.2f, 0.3f));
        Area(status.rectTransform, 0, 0, 1, 0.15f);
        var active = Image("ActiveTurn", root, new Color(1, 0.85f, 0.1f));
        Area(active.rectTransform, 0, 0.97f, 1, 0.03f);
        var itemsRoot = Rect("HeldItems", root);
        Area(itemsRoot, 0.12f, 0.005f, 0.57f, 0.11f);
        var items = itemsRoot.gameObject.AddComponent<PlayerHeldItemsIcons>();
        var icons = new Image[3];
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = Image("HeldItem" + i, itemsRoot, Color.white);
            icons[i].preserveAspect = true;
            Area(icons[i].rectTransform, i / 3f, 0, 0.29f, 1);
        }
        var so = new SerializedObject(items);
        var array = so.FindProperty("icons"); array.arraySize = icons.Length;
        for (int i = 0; i < icons.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        var battle = Image("BattleItem", root, Color.white);
        battle.preserveAspect = true;
        Area(battle.rectTransform, 0.73f, 0.005f, 0.2f, 0.11f);
        Set(view, "background", bg); Set(view, "portrait", portrait); Set(view, "playerName", name);
        Set(view, "state", status); Set(view, "activeTurn", active.gameObject); Set(view, "heldItems", items); Set(view, "battleItem", battle);
        return Save<SpectatorSelectionCard>(root.gameObject, "SpectatorSelectionCard");
    }

    private static SpectatorTeamPanel BuildTeamPanel()
    {
        var root = Rect("SpectatorTeamPanel");
        var view = root.gameObject.AddComponent<SpectatorTeamPanel>();
        var accent = Image("TeamAccent", root, new Color(0.43f, 0.2f, 0.91f));
        Area(accent.rectTransform, 0, 0.93f, 1, 0.07f);
        var label = Text("TeamName", accent.transform, "Team", 18);
        var content = Rect("Players", root);
        Area(content, 0, 0, 1, 0.92f);
        Set(view, "teamName", label); Set(view, "accent", accent); Set(view, "content", content);
        return Save<SpectatorTeamPanel>(root.gameObject, "SpectatorTeamPanel");
    }

    private static SpectatorBattleHUD BuildBattleHUD()
    {
        var root = CanvasRoot("SpectatorBattleHUD", 15);
        var view = root.AddComponent<SpectatorBattleHUD>();
        var touch = Image("MobileCameraPanArea", root.transform, Color.clear);
        touch.raycastTarget = true;
        var panCanvas = touch.gameObject.AddComponent<Canvas>();
        panCanvas.overrideSorting = true;
        panCanvas.sortingOrder = -1;
        touch.gameObject.AddComponent<GraphicRaycaster>();
        // Reuse the existing single-pointer drag implementation on all platforms.
        // Mouse clicks on the world can drag too; card buttons are drawn above it.
        touch.gameObject.AddComponent<MobileCameraPanArea>();
        var bar = Rect("BottomBar", root.transform);
        Area(bar, 0.08f, 0.015f, 0.84f, 0.22f);
        Image("BarBackground", bar, Ink, Sprite("t_observe_down_bg"));
        var viewport = Rect("Viewport", bar);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Roster", viewport);
        content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1);
        content.pivot = new Vector2(0, 0.5f); content.sizeDelta = new Vector2(1600, 0);
        var scroll = bar.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var state = Text("CameraState", root.transform, "Free camera", 20);
        Area(state.rectTransform, 0.35f, 0.24f, 0.3f, 0.035f);
        Set(view, "bar", bar); Set(view, "rosterContent", content); Set(view, "rosterViewport", viewport); Set(view, "cameraState", state);
        return Save<SpectatorBattleHUD>(root, "SpectatorBattleHUD");
    }

    private static SpectatorSelectionOverlay BuildSelectionOverlay()
    {
        var root = CanvasRoot("SpectatorSelectionOverlay", 20);
        var view = root.AddComponent<SpectatorSelectionOverlay>();
        var fade = root.AddComponent<CanvasGroup>();
        var bg = Image("Background", root.transform, new Color(0.055f, 0.12f, 0.21f));
        bg.raycastTarget = true;
        var blue = Image("BlueSide", root.transform, new Color(0.27f, 0.12f, 0.55f));
        Area(blue.rectTransform, 0, 0, 0.26f, 1);
        var orange = Image("OrangeSide", root.transform, new Color(0.68f, 0.29f, 0.035f));
        Area(orange.rectTransform, 0.74f, 0, 0.26f, 1);
        var teams = Rect("Teams", root.transform);
        Area(teams, 0.015f, 0.015f, 0.97f, 0.85f);
        var map = Rect("MapPreview", teams);
        Area(map, 0.32f, 0.3f, 0.36f, 0.43f);
        var preview = Image("MapImage", map, Color.white);
        preview.preserveAspect = true;
        Area(preview.rectTransform, 0, 0.15f, 1, 0.85f);
        var mapName = Text("MapName", map, "Map", 34);
        Area(mapName.rectTransform, 0, -0.12f, 1, 0.25f);
        var message = Text("ModeMessage", root.transform, "SPECTATING", 20, new Color(0.75f, 0.9f, 1));
        Area(message.rectTransform, 0.35f, 0.01f, 0.3f, 0.04f);
        var bans = Text("Bans", root.transform, "", 22);
        Area(bans.rectTransform, 0.28f, 0.08f, 0.44f, 0.1f);
        var timer = CloneDraftTimer(root.transform);
        Set(view, "teamsRoot", teams); Set(view, "mapRoot", map); Set(view, "mapPreview", preview);
        Set(view, "mapName", mapName); Set(view, "modeMessage", message); Set(view, "bans", bans);
        Set(view, "timer", timer); Set(view, "fade", fade);
        return Save<SpectatorSelectionOverlay>(root, "SpectatorSelectionOverlay");
    }

    private static DraftTimerUI CloneDraftTimer(Transform parent)
    {
        string path = "Assets/Scenes/DraftSelect.unity";
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var original = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<DraftTimerUI>(true)).First();
            var clone = UnityEngine.Object.Instantiate(original, parent);
            clone.name = "DraftTimer";
            Area((RectTransform)clone.transform, 0.33f, 0.865f, 0.34f, 0.135f);
            foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            return clone;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static LobbyPlayerIcon BuildLobbySlot()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/LobbyPlayerIcon.prefab");
        var root = UnityEngine.Object.Instantiate(source);
        root.name = "SpectatorLobbySlot";
        return Save<LobbyPlayerIcon>(root, "SpectatorLobbySlot");
    }

    private static void EnsureBanStrips()
    {
        string path = Folder + "/SpectatorSelectionOverlay.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var overlay = root.GetComponent<SpectatorSelectionOverlay>();
            var so = new SerializedObject(overlay);
            bool changed = false;
            foreach (string name in new[] { "blueBanHolder", "orangeBanHolder" })
            {
                if (so.FindProperty(name).objectReferenceValue != null) continue;
                bool blue = name == "blueBanHolder";
                var strip = Rect(blue ? "BlueBans" : "OrangeBans", root.transform);
                Area(strip, blue ? 0.15f : 0.73f, 0.88f, 0.12f, 0.09f);
                Set(overlay, name, strip);
                changed = true;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static RectTransform BuildLobbySlots()
    {
        var root = Rect("SpectatorLobbySlots");
        Area(root, 0.05f, 0.72f, 0.65f, 0.14f);
        var label = Text("Label", root, "SPECTATORS", 20);
        Area(label.rectTransform, 0, 0.8f, 1, 0.2f);
        var slots = Rect("Slots", root);
        Area(slots, 0, 0, 1, 0.77f);
        var layout = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 15;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = layout.childForceExpandWidth = true;
        return Save<RectTransform>(root.gameObject, "SpectatorLobbySlots");
    }
}
