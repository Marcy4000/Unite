using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit, repeatable authoring command. Never runs automatically on import.
public static class LanePickerSetup
{
    private const string PrefabPath = "Assets/Prefabs/CharacterSelect/PathPickerPrefab.prefab";
    private const string SpritePath = "Assets/Sprites/CharacterSelect/";

    [MenuItem("Tools/Character Select/Configure Lane Picker")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your open scenes before configuring the lane picker.");

        if (!AssetDatabase.IsValidFolder("Assets/Maps/LaneLayouts")) AssetDatabase.CreateFolder("Assets/Maps", "LaneLayouts");
        var remoat = CreateLayout("Remoat", false);
        var theia = CreateLayout("Theia", true);
        foreach (string guid in AssetDatabase.FindAssets("t:MapInfo", new[] { "Assets/Maps" }))
        {
            var map = AssetDatabase.LoadAssetAtPath<MapInfo>(AssetDatabase.GUIDToAssetPath(guid));
            if (map.name == "RemoatStadium") map.lanePickerLayout = remoat;
            else if (map.name == "TheiaSkyRuins" || map.name == "TheiaSkyRuinsDraft") map.lanePickerLayout = theia;
            else if (!map.customProperties.Exists(property => property.HasProperty("disableLaneSelection")))
                map.customProperties.Add(new CustomMapProperties { key = "disableLaneSelection", value = "true" });
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
        }

        ConfigurePickerPrefab();
        ConfigureBadge("Assets/Prefabs/CharacterSelect/PlayerSelectIcon.prefab", typeof(PlayerSelectionIcon));
        ConfigureBadge("Assets/Prefabs/CharacterSelect/DraftMode/DraftPlayerBlue.prefab", typeof(DraftPlayerIcon));
        ConfigureScene("Assets/Scenes/CharacterSelect.unity", typeof(CharacterSelectController));
        ConfigureScene("Assets/Scenes/DraftSelect.unity", typeof(DraftSelectController));
    }

    private static Sprite Sprite(string relativePath) => AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath + relativePath + ".png");

    private static LanePickerLayout CreateLayout(string name, bool theia)
    {
        string path = "Assets/Maps/LaneLayouts/" + name + "LaneLayout.asset";
        var layout = AssetDatabase.LoadAssetAtPath<LanePickerLayout>(path);
        if (layout != null) return layout; // Preserve hand-tuned layouts on subsequent runs.
        layout = ScriptableObject.CreateInstance<LanePickerLayout>();
        layout.compactMap = Sprite(theia ? "Prepare/t_prepare_bg_map1_skyCastle" : "Prepare/t_prepare_bg_map1");
        layout.expandedMap = Sprite(theia ? "t_heroselec_bg1_skyCastle" : "t_heroselec_bg1");
        string[] suffixes = { "up", "mid", "down" };
        string[] icons = { "top", "mid", "down" };
        Vector2[] compactPositions = { new Vector2(-46.4f, 62.9f), new Vector2(-54, 10.7f), new Vector2(-46.4f, -45.9f) };
        Vector2[] expandedPositions = { new Vector2(-271.77f, 58.79f), new Vector2(-336.32f, -29.09f), new Vector2(-271.77f, -120.7f) };
        for (int i = 0; i < layout.lanes.Length; i++)
        {
            var lane = layout.lanes[i];
            lane.icon = Sprite("Prepare/t_prepare_smallmap_" + icons[i] + "1");
            lane.compact.highlight = lane.expanded.highlight = Sprite("t_HeroSelection_bg_split_" + suffixes[i]);
            lane.compact.highlightPosition = compactPositions[i];
            lane.expanded.highlightPosition = expandedPositions[i];
            lane.compact.highlightSize = i == 1 ? new Vector2(108, 93) : new Vector2(168, 66);
            lane.expanded.highlightSize = i == 1 ? new Vector2(216.32f, 157.55f) : new Vector2(450.46f, 109.37f);
        }
        SetPath(layout.lanes[0].compact, new Vector2(-140, 42), new Vector2(-25, 119), new Vector2(90, 63));
        SetPath(layout.lanes[1].compact, new Vector2(-80, 11), new Vector2(35, 11), new Vector2(150, 11));
        SetPath(layout.lanes[2].compact, new Vector2(-140, -26), new Vector2(-25, -103), new Vector2(90, -47));
        SetPath(layout.lanes[0].expanded, new Vector2(-480, 24), new Vector2(-285, 139), new Vector2(-90, 59));
        SetPath(layout.lanes[1].expanded, new Vector2(-380, -30), new Vector2(-185, -30), new Vector2(10, -30));
        SetPath(layout.lanes[2].expanded, new Vector2(-480, -85), new Vector2(-285, -206), new Vector2(-90, -119));
        AssetDatabase.CreateAsset(layout, path);
        AssetDatabase.SaveAssetIfDirty(layout);
        return layout;
    }

    private static void SetPath(LaneMapLayout lane, Vector2 start, Vector2 control, Vector2 end)
    {
        lane.markerStart = start;
        lane.markerControl = control;
        lane.markerEnd = end;
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurePickerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var ui = root.GetComponent<LanePickerUI>() ?? root.AddComponent<LanePickerUI>();
            var compact = root.transform.Find("MapPathsPreview");
            var expanded = root.transform.Find("MapPathSelector");
            var bigMap = expanded.Find("BigMapBG");
            var smallMap = compact.Find("MapBG");
            var holder = bigMap.Find("PathsHolder");
            var toggle = holder.Find("PathToggle").GetComponent<Toggle>();
            var option = toggle.GetComponent<LanePickerOption>() ?? toggle.gameObject.AddComponent<LanePickerOption>();
            Set(option, "toggle", toggle);
            Set(option, "icon", toggle.transform.Find("Background/PathIcon").GetComponent<Image>());
            Set(option, "label", toggle.transform.Find("Background/PathName").GetComponent<TMP_Text>());
            toggle.gameObject.SetActive(false);

            Set(ui, "compactRoot", compact.gameObject);
            Set(ui, "expandedRoot", expanded.gameObject);
            Set(ui, "openButton", compact.Find("SelectPathButton").GetComponent<Button>());
            Set(ui, "closeButton", bigMap.Find("CloseButton").GetComponent<Button>());
            Set(ui, "optionsHolder", holder);
            Set(ui, "optionTemplate", option);
            Set(ui, "compactMap", ConfigureMap(smallMap, false));
            Set(ui, "expandedMap", ConfigureMap(bigMap, true));

            // Decorative images must not intercept option/button clicks.
            foreach (var image in root.GetComponentsInChildren<Image>(true)) image.raycastTarget = false;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
                if (selectable.targetGraphic != null) selectable.targetGraphic.raycastTarget = true;
            expanded.GetComponent<Image>().raycastTarget = true; // Modal input blocker.
            compact.gameObject.SetActive(true);
            expanded.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static LanePickerMapView ConfigureMap(Transform map, bool expanded)
    {
        var view = map.GetComponent<LanePickerMapView>();
        bool newView = view == null;
        if (newView) view = map.gameObject.AddComponent<LanePickerMapView>();
        var highlight = map.Find("LaneHighlightTemplate") ?? map.Find("TopLane");
        highlight.name = "LaneHighlightTemplate";
        foreach (string unused in new[] { "BotLane", "Jungle" })
        {
            var child = map.Find(unused);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        var marker = map.Find("MapPathPlayer");
        Set(view, "background", map.GetComponent<Image>());
        Set(view, "highlightTemplate", highlight.GetComponent<Image>());
        Set(view, "markerTemplate", marker);
        Set(view, "localPlayerFrame", Sprite("Prepare/t_prepare_pokemon_frame_me"));
        Set(view, "teammateFrame", Sprite("Prepare/t_prepare_pokemon_frame_oteher"));
        var serialized = new SerializedObject(view);
        serialized.FindProperty("expanded").boolValue = expanded;
        if (newView) serialized.FindProperty("markerScale").floatValue = expanded ? 1.35f : 1.25f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        highlight.gameObject.SetActive(false);
        marker.gameObject.SetActive(false);
        return view;
    }

    private static void ConfigureBadge(string path, Type cardType)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var icon = root.GetComponentsInChildren<Image>(true).First(image => image.name == "MapIcon");
            var badge = root.GetComponent<LaneSelectionBadge>() ?? root.AddComponent<LaneSelectionBadge>();
            Set(badge, "icon", icon);
            Set(root.GetComponent(cardType), "laneBadge", badge);
            icon.enabled = false;
            icon.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureScene(string path, Type controllerType)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var controller = transforms.Select(t => t.GetComponent(controllerType)).First(component => component != null);
            var state = controller.GetComponent<LaneSelectionNetwork>() ?? controller.gameObject.AddComponent<LaneSelectionNetwork>();
            var canvas = transforms.First(t => t.name == "Canvas" && t.GetComponent<Canvas>() != null);
            var existing = transforms.FirstOrDefault(t => t.name == "PathPickerPrefab");
            GameObject picker;
            if (existing != null && existing.GetComponent<LanePickerUI>() != null)
                picker = existing.gameObject;
            else
            {
                // Replace the original unpacked preparation with the shared, wired prefab.
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                picker = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), canvas);
            }
            picker.transform.SetAsLastSibling();
            var pickerSettings = new SerializedObject(picker.GetComponent<LanePickerUI>());
            pickerSettings.FindProperty("usePreparationLayout").boolValue = controllerType == typeof(DraftSelectController);
            pickerSettings.ApplyModifiedPropertiesWithoutUndo();
            if (controllerType == typeof(DraftSelectController))
            {
                picker.GetComponent<LanePickerUI>().ShowPreparationLayout();
                PrefabUtility.RecordPrefabInstancePropertyModifications(picker.transform.Find("MapPathsPreview"));
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(picker.GetComponent<LanePickerUI>());
            Set(state, "picker", picker.GetComponent<LanePickerUI>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
