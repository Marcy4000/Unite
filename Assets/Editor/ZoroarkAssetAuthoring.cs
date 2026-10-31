using System;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Create-missing authoring only. Preserves existing models/controllers and subsequent prefab edits.</summary>
public static class ZoroarkAssetAuthoring
{
    private const string Visuals = "Assets/Resources/Effects/Zoroark/";
    public static readonly AvailableMoves[] Ids = { AvailableMoves.ZoroarkSlash, AvailableMoves.ZoroarkFurySwipes,
        AvailableMoves.ZoroarkShadowClaw, AvailableMoves.ZoroarkCut, AvailableMoves.ZoroarkNightSlash,
        AvailableMoves.ZoroarkFeintAttack, AvailableMoves.ZoroarkNightfallDaze };
    private static Scene preview;
    private static Material red, dark, white;
    private static Mesh crescent, edge, ring;

    [MenuItem("Tools/Zoroark/Create Missing Move Assets")]
    public static void Build()
    {
        foreach (var path in new[] { ZoroarkMoveRules.Folder.TrimEnd('/'), Visuals.TrimEnd('/'), "Assets/Materials/Zoroark", "Assets/Moves/Zoroark", "Assets/Resources/HPBarModules", "Assets/Prefabs/UI" }) Folder(path);
        red = Material("CrimsonClaw", new Color(0.85f, 0.025f, 0.13f));
        dark = Material("NightEnergy", new Color(0.18f, 0.025f, 0.3f));
        white = Material("SlashCore", new Color(1, 0.55f, 0.7f));
        crescent = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Absol/Art/DisasterCrescent.asset");
        edge = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Absol/Art/MoonlitEdge.asset");
        ring = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/ThinRing.asset");
        if (crescent == null || ring == null) throw new InvalidOperationException("Missing reference slash/ring art.");
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (var id in Ids) Effect(id);
            Visual("MeleeSlash"); Visual("BoostedSlash"); Visual("CrossSlash"); Visual("DarkShockwave"); Visual("IllusionSmoke");
            Gauge(); AuxiliaryButton();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (var id in Ids) Register(ZoroarkMoveRules.Folder + id + ".prefab");
        ConfigureMoves(); RestyleAuxiliaryButton(false); WireAuxiliaryButtons(); WireWildDisguise();
        const string icon = "Assets/Resources/ZoroarkIllusionIcon.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(icon) == null)
            AssetDatabase.CopyAsset("Assets/Sprites/MoveIcons/Zoroark/t_Skill_Zorua_P1.png", icon);
        AssetDatabase.SaveAssets();
        Debug.Log("Zoroark moves, segmented gauge and auxiliary action assets authored. Existing model/controller assets preserved.");
    }

    private static void Effect(AvailableMoves id)
    {
        string path = ZoroarkMoveRules.Folder + id + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(id.ToString());
        root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var effect = root.AddComponent<ZoroarkMoveEffect>();
        var graphics = Node("Graphics", root.transform);
        var trail = Node("ClawTrail", graphics.transform);
        for (int i = 0; i < 3; i++)
            Slash(trail.transform, new Vector3(0, 0.25f + i * 0.15f, 0), Vector3.one * (1 + 0.15f * i));
        var mark = Node("ReturnMark", graphics.transform);
        MeshNode("OriginRing", mark.transform, ring, red, Vector3.zero, Vector3.one * 0.65f);
        MeshNode("InnerOriginRing", mark.transform, ring, white, Vector3.up * 0.025f, Vector3.one * 0.4f);
        var serialized = new SerializedObject(effect);
        serialized.FindProperty("move").longValue = (long)id;
        serialized.FindProperty("maximumLifetime").floatValue = 60;
        serialized.FindProperty("graphics").objectReferenceValue = graphics;
        serialized.FindProperty("dashVisual").objectReferenceValue = trail;
        serialized.FindProperty("markVisual").objectReferenceValue = mark;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }

    private static void Visual(string name)
    {
        string path = Visuals + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(name); root.AddComponent<TimedCombatVisual>();
        if (name == "CrossSlash")
        {
            // Long, thin diagonal crescents and their bright cores bound the
            // two agreed 6 x 1.5 landing strips, not a circular blast.
            for (int i = 0; i < 2; i++)
            {
                var pivot = Node("Diagonal" + i, root.transform);
                pivot.transform.localRotation = Quaternion.Euler(0, i == 0 ? -45 : 45, 0);
                Slash(pivot.transform, new Vector3(0, 0.12f, -2.8f), new Vector3(0.75f, 1, 5.6f));
            }
        }
        else if (name == "DarkShockwave" || name == "IllusionSmoke")
        {
            MeshNode("DarkRing", root.transform, ring, dark, Vector3.up * 0.16f, Vector3.one);
            MeshNode("CrimsonEdge", root.transform, ring, red, Vector3.up * 0.21f, Vector3.one * 0.94f);
            Sparks(root.transform, 0.9f, name == "IllusionSmoke" ? 60 : 24);
        }
        else
        {
            int count = name == "BoostedSlash" ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                var slash = Slash(root.transform, new Vector3(0, 0.25f + i * 0.08f, 0), Vector3.one * 0.8f);
                slash.localRotation = Quaternion.Euler(0, (i - 1) * 12, 0);
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }

    private static void Gauge()
    {
        const string path = "Assets/Resources/HPBarModules/SegmentedGauge.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Rect("SegmentedGauge", null, new Vector2(2, 0.12f));
        root.layer = 6;
        var rect = (RectTransform)root.transform; rect.anchoredPosition = new Vector2(0, -0.3f);
        var module = root.AddComponent<SegmentedGaugeModule>();
        var background = Image("Background", root.transform, new Color(0.12f, 0.12f, 0.12f, 0.95f));
        var fill = Image("Fill", root.transform, new Color(0.9f, 0.12f, 0.25f));
        // UGUI's filled Image requires a sprite, even for a solid rectangle.
        fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GenericUI/Common/t_common_bg_id.png");
        fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        var separators = Rect("Separators", root.transform, Vector2.zero); Stretch((RectTransform)separators.transform);
        var separator = Image("SeparatorTemplate", root.transform, Color.black); separator.gameObject.SetActive(false);
        var data = new SerializedObject(module);
        data.FindProperty("moduleId").stringValue = "segmented_gauge";
        data.FindProperty("moduleRectTransform").objectReferenceValue = rect;
        data.FindProperty("gaugeBar").objectReferenceValue = fill;
        data.FindProperty("gaugeBackground").objectReferenceValue = background;
        data.FindProperty("separatorsHolder").objectReferenceValue = separators.transform;
        data.FindProperty("separatorPrefab").objectReferenceValue = separator;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }

    private static void AuxiliaryButton()
    {
        const string path = "Assets/Prefabs/UI/AuxiliaryAction.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Rect("AuxiliaryAction", null, new Vector2(60, 60));
        var ui = root.AddComponent<AuxiliaryActionUI>();
        var content = Rect("Content", root.transform, Vector2.zero); Stretch((RectTransform)content.transform);
        var background = Image("Background", content.transform, new Color(0.1f, 0.08f, 0.16f, 0.95f));
        background.raycastTarget = true;
        var button = content.AddComponent<Button>(); button.targetGraphic = background;
        var icon = Image("Icon", content.transform, Color.white);
        icon.rectTransform.offsetMin = Vector2.one * 4; icon.rectTransform.offsetMax = Vector2.one * -4;
        var cooldown = Image("Cooldown", content.transform, new Color(0, 0, 0, 0.7f));
        cooldown.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GenericUI/Common/t_common_bg_id.png");
        cooldown.type = UnityEngine.UI.Image.Type.Filled; cooldown.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        var label = Rect("CooldownText", content.transform, Vector2.zero); Stretch((RectTransform)label.transform);
        var text = label.AddComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset; text.fontSize = 22; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        Set(ui, "content", content); Set(ui, "button", button); Set(ui, "icon", icon); Set(ui, "cooldownFill", cooldown); Set(ui, "cooldownText", text);
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }

    [MenuItem("Tools/Zoroark/Update Auxiliary Action Style")]
    public static void UpdateAuxiliaryButtonStyle()
    {
        RestyleAuxiliaryButton(true);
        AssetDatabase.SaveAssets();
    }

    private static void RestyleAuxiliaryButton(bool force)
    {
        const string path = "Assets/Prefabs/UI/AuxiliaryAction.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var content = root.transform.Find("Content");
            if (!force && content.Find("MainBG") != null) return;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MoveUI.prefab");
            var sourceData = new SerializedObject(source.GetComponent<MoveUI>());
            var sourceFrame = source.transform.Find("MainBG").GetComponent<Image>();
            var sourceIcon = (Image)sourceData.FindProperty("moveIcon").objectReferenceValue;
            var sourceCooldown = ((GameObject)sourceData.FindProperty("cdBg").objectReferenceValue).GetComponent<Image>();
            var sourceLock = ((GameObject)sourceData.FindProperty("lockImage").objectReferenceValue).GetComponent<Image>();
            var sourceText = (TMP_Text)sourceData.FindProperty("cdText").objectReferenceValue;
            float scale = ((RectTransform)root.transform).rect.width / sourceFrame.rectTransform.rect.width;
            var ui = root.GetComponent<AuxiliaryActionUI>();
            var data = new SerializedObject(ui);
            var icon = (Image)data.FindProperty("icon").objectReferenceValue;
            var cooldown = (Image)data.FindProperty("cooldownFill").objectReferenceValue;
            var text = (TMP_Text)data.FindProperty("cooldownText").objectReferenceValue;
            var background = content.Find("Background").GetComponent<Image>();
            CopyGraphic(background, sourceCooldown); PlaceGraphic(background.rectTransform, sourceCooldown.rectTransform, scale);
            background.color = new Color(0.08f, 0.08f, 0.12f, 1); background.raycastTarget = true;
            PlaceGraphic(icon.rectTransform, sourceIcon.rectTransform, scale);
            icon.color = sourceIcon.color; icon.preserveAspect = sourceIcon.preserveAspect;
            CopyGraphic(cooldown, sourceCooldown); PlaceGraphic(cooldown.rectTransform, sourceCooldown.rectTransform, scale);
            cooldown.type = UnityEngine.UI.Image.Type.Filled; cooldown.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            cooldown.fillAmount = 0;
            var frame = StyleImage("MainBG", content); CopyGraphic(frame, sourceFrame); PlaceGraphic(frame.rectTransform, sourceFrame.rectTransform, scale);
            frame.transform.SetAsLastSibling();
            var locked = StyleImage("Lock", content); CopyGraphic(locked, sourceLock); PlaceGraphic(locked.rectTransform, sourceLock.rectTransform, scale);
            locked.gameObject.SetActive(false);
            data.FindProperty("lockImage").objectReferenceValue = locked.gameObject;
            data.ApplyModifiedPropertiesWithoutUndo();
            text.font = sourceText.font; text.fontSharedMaterial = sourceText.fontSharedMaterial;
            text.fontSize = sourceText.fontSize * scale; text.color = sourceText.color;
            text.alignment = sourceText.alignment; text.transform.SetAsLastSibling();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Image StyleImage(string name, Transform parent)
    {
        var child = parent.Find(name);
        if (child != null) return child.GetComponent<Image>();
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)) { layer = parent.gameObject.layer };
        SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }
    private static void CopyGraphic(Image target, Image source)
    {
        target.sprite = source.sprite; target.material = source.material; target.color = source.color;
        target.type = source.type; target.preserveAspect = source.preserveAspect;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.raycastTarget = false;
    }
    private static void PlaceGraphic(RectTransform target, RectTransform source, float scale)
    {
        target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
        target.pivot = source.pivot;
        target.sizeDelta = source.rect.size * scale;
        target.anchoredPosition = source.anchoredPosition * scale;
    }

    private static void WireAuxiliaryButtons()
    {
        const string path = "Assets/Prefabs/DefaultObjects.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = false;
            foreach (var holder in root.GetComponentsInChildren<MovesHolderUI>(true))
            {
                if (holder.GetComponentInChildren<AuxiliaryActionUI>(true) != null) continue;
                var button = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/AuxiliaryAction.prefab"), holder.gameObject.scene);
                button.transform.SetParent(holder.transform, false);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
                rect.pivot = new Vector2(1, 0);
                rect.anchoredPosition = new Vector2(-265, 175);
                changed = true;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureMoves()
    {
        string[] names = { "Slash", "Fury Swipes", "Shadow Claw", "Cut", "Night Slash", "Feint Attack", "Nightfall Daze" };
        string[] icons = { "S1", "S2", "S11", "S12", "S21", "S22", "U" };
        string[] previews = { "S1", "S2", "S1A", "S1B", "S2A", "S2B", "U" };
        string[] descriptions = {
            "Dash forward, damaging opposing Pokemon in the path.",
            "Dash forward, damaging opposing Pokemon. A hit grants one recast within 4s. Cooldown begins after the recast or window ends.",
            "Dash forward, damaging and briefly stunning opposing Pokemon in the path. Enemies near the endpoint are thrown for 0.5s (0.6s at level 13).",
            "Dash forward, damaging opposing Pokemon and restoring HP per enemy hit. At level 13, restore more HP.",
            "For 2.5s, movement becomes a dash attack. A successful dash grants a long-range special basic. Hitting with special basics or the other normal move refreshes the chain and allows another dash. You can keep holding a direction while using the special basic; the next dash waits for its recovery. Movement after a failed follow-up ends the chain. Six successful attacks trigger an invincible aerial cross slash with missing-HP damage and healing. Steer slightly with movement while airborne, or use the other normal move to reposition in the air. Cooldown begins when the chain ends; reduced by 1s at level 11.",
            "Dash forward. Hitting resets the other normal move's cooldown and marks the origin for 3s. Recast to dash back; using the other normal move relocates the mark to its origin. Four successful move hits unlock a targeted leap and directional bounce with missing-HP damage. Selecting a target starts a brief stationary buildup before the lunge. Aim at a target and choose the bounce with movement. Cooldown begins when the chain ends; reduced by 1s at level 11.",
            "Become unstoppable for 1.33s and release three increasingly large dark shockwaves over 1s. Afterward gain 80% movement speed and a 20% maximum-HP shield for 6s." };
        var assets = new MoveAsset[Ids.Length];
        for (int i = 0; i < Ids.Length; i++)
        {
            string path = "Assets/Moves/Zoroark/" + Ids[i] + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MoveAsset>();
                asset.move = Ids[i]; asset.moveName = names[i]; asset.cooldown = MoveDatabase.GetMove(Ids[i]).Cooldown;
                asset.moveType = i == 6 ? MoveType.UniteMove : i == 0 || i == 2 || i == 3 ? MoveType.MoveA : MoveType.MoveB;
                asset.moveLabel = i == 6 ? MoveLabels.Area : MoveLabels.Dash;
                asset.uniteEnergyCost = i == 6 ? ZoroarkMoveRules.UniteEnergy : 0; asset.description = descriptions[i];
                asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/Zoroark/t_Skill_Zorua_" + icons[i] + ".png");
                string preview = AssetDatabase.FindAssets("t_Growth_Skill_Zorua_" + previews[i] + "_", new[] { "Assets/Sprites/MoveIcons/Zoroark/Previews" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                asset.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(preview));
                AssetDatabase.CreateAsset(asset, path);
            }
            assets[i] = asset;
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadAssetAtPath<PokemonBase>("Assets/Pokemons/PlayableMons/Zoroark.asset"));
        pokemon.FindProperty("passive").enumValueIndex = (int)AvailablePassives.ZoroarkPassive;
        int[] levels = { 0, 2, 4, 6, 8, 10, 12 };
        int[][] choices = { new[] { 0, 1 }, new[] { 0, 1 }, new[] { 4, 5 }, new[] { 2, 3 }, new[] { 6 }, new[] { 4, 5 }, new[] { 2, 3 } };
        var rows = pokemon.FindProperty("learnableMoves");
        if (rows.arraySize == 0)
        {
            rows.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++)
            {
                var row = rows.GetArrayElementAtIndex(i); row.FindPropertyRelative("level").intValue = levels[i]; row.FindPropertyRelative("isUpgraded").boolValue = i >= 5;
                var moves = row.FindPropertyRelative("moves"); moves.arraySize = choices[i].Length;
                for (int j = 0; j < choices[i].Length; j++) moves.GetArrayElementAtIndex(j).objectReferenceValue = assets[choices[i][j]];
            }
        }
        pokemon.ApplyModifiedPropertiesWithoutUndo();
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset"));
        var entries = registry.FindProperty("moveEntries");
        foreach (var asset in assets)
        {
            bool exists = false;
            for (int i = 0; i < entries.arraySize; i++) exists |= entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").longValue == (long)asset.move;
            if (exists) continue;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize++);
            entry.FindPropertyRelative("moveType").longValue = (long)asset.move;
            entry.FindPropertyRelative("moveAsset").objectReferenceValue = asset;
        }
        registry.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireWildDisguise()
    {
        const string path = "Assets/Prefabs/Minimap/MapPlayer.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var data = new SerializedObject(root.GetComponent<MapPlayerIcon>());
            if (data.FindProperty("wildDisguiseSprite").objectReferenceValue != null) return;
            var image = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Minimap/MapWildPokemon.prefab").GetComponentInChildren<Image>(true);
            data.FindProperty("wildDisguiseSprite").objectReferenceValue = image.sprite;
            data.FindProperty("wildDisguiseSize").vector2Value = image.rectTransform.sizeDelta;
            data.FindProperty("wildDisguiseColor").colorValue = image.color;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/Zoroark/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat")) { name = name };
        material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, path); return material;
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var root = new GameObject(name) { layer = 8 };
        SceneManager.MoveGameObjectToScene(root, preview); if (parent != null) root.transform.SetParent(parent, false); return root;
    }
    private static Transform Slash(Transform parent, Vector3 position, Vector3 scale)
    {
        var slash = MeshNode("CrimsonSlash", parent, crescent, red, position, scale);
        MeshNode("SlashEdge", slash.transform, edge, white, Vector3.up * 0.015f, Vector3.one);
        return slash.transform;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var root = Node(name, parent); root.transform.localPosition = position; root.transform.localScale = scale;
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return root;
    }
    private static void Sparks(Transform parent, float radius, int count)
    {
        var root = Node("NightParticles", parent);
        var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.startColor = new Color(0.5f, 0.03f, 0.2f); main.startLifetime = 0.25f; main.startSpeed = 1; main.startSize = 0.15f; main.maxParticles = count; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = particles.emission; emission.rateOverTime = count;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
        root.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
    }
    private static GameObject Rect(string name, Transform parent, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform)) { layer = 6 };
        SceneManager.MoveGameObjectToScene(root, preview); if (parent != null) root.transform.SetParent(parent, false);
        ((RectTransform)root.transform).sizeDelta = size; return root;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Image Image(string name, Transform parent, Color color)
    {
        var root = Rect(name, parent, Vector2.zero); Stretch((RectTransform)root.transform);
        var image = root.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private static void Set(Object target, string field, Object value) { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path)); }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); prefab = PrefabUtility.SavePrefabAsset(prefab);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing spawn hash: " + path);
        var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path; EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")); var list = registry.FindProperty("List");
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) return;
        var entry = list.GetArrayElementAtIndex(list.arraySize++); entry.FindPropertyRelative("Override").enumValueIndex = 0; entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null; entry.FindPropertyRelative("SourceHashToOverride").longValue = 0; entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null; registry.ApplyModifiedPropertiesWithoutUndo();
    }
}
