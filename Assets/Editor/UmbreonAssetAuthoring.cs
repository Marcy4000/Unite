using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Create-missing authoring, following Duraludon. Never replaces existing models, stats or hand-edited prefabs.</summary>
public static class UmbreonAssetAuthoring
{
    public static readonly AvailableMoves[] Ids = { AvailableMoves.UmbreonSwift, AvailableMoves.UmbreonFakeTears,
        AvailableMoves.UmbreonMeanLook, AvailableMoves.UmbreonFoulPlay, AvailableMoves.UmbreonWish,
        AvailableMoves.UmbreonSnarl, AvailableMoves.UmbreonMoonlightPrance };
    private const string ResourcesFolder = "Assets/Resources/Effects/Umbreon/";
    private static Scene preview;
    private static Mesh ring, star, shard;
    private static Material violet, gold, white, dark, particles;

    [MenuItem("Tools/Characters/Umbreon/Create Missing Move Assets")]
    public static void Build()
    {
        foreach (var path in new[] { UmbreonMoveRules.Folder.TrimEnd('/'), ResourcesFolder.TrimEnd('/'), "Assets/Materials/Umbreon", "Assets/Moves/Umbreon" }) Folder(path);
        ring = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Duraludon/Art/ThinRing.asset");
        star = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Espeon/Art/SwiftStar.asset");
        shard = AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Prefabs/Objects/Moves/Duraludon/Art" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.IndexOf("Shard", StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(AssetDatabase.LoadAssetAtPath<Mesh>).FirstOrDefault();
        if (ring == null || star == null || shard == null) throw new InvalidOperationException("Missing Duraludon/Swift reference meshes.");
        violet = Material("NightViolet", new Color(0.42f, 0.06f, 0.7f));
        dark = Material("DarkZone", new Color(0.055f, 0.02f, 0.12f));
        gold = Material("MoonGold", new Color(1, 0.67f, 0.08f));
        white = Material("MoonCore", new Color(1, 0.94f, 0.65f));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Effect<UmbreonSwiftEffect>(Ids[0], Swift);
            Effect<UmbreonFakeTearsEffect>(Ids[1], (root, graphics, effect) => { });
            Effect<UmbreonMeanLookEffect>(Ids[2], Mean);
            Effect<UmbreonFoulPlayEffect>(Ids[3], (root, graphics, effect) => { });
            Effect<UmbreonWishEffect>(Ids[4], (root, graphics, effect) =>
            {
                MeshNode("WishStar", graphics.transform, star, white, Vector3.up * 2, Vector3.one * 0.35f);
                MeshNode("WishHalo", graphics.transform, ring, gold, Vector3.up * 1.8f, Vector3.one * 0.6f);
                Sparks(graphics.transform, new Color(1, 0.85f, 0.2f), 0.6f, 25);
            });
            Effect<UmbreonSnarlEffect>(Ids[5], (root, graphics, effect) =>
                MeshNode("SnarlAura", graphics.transform, ring, violet, Vector3.up * 0.1f, Vector3.one * UmbreonMoveRules.SnarlRadius));
            Effect<UmbreonMoonlightPranceEffect>(Ids[6], (root, graphics, effect) =>
            {
                MeshNode("TheftBoundary", graphics.transform, ring, violet, Vector3.up * 0.12f, Vector3.one * UmbreonMoveRules.UniteAuraRadius);
                MeshNode("InnerMoonCircle", graphics.transform, ring, gold, Vector3.up * 0.16f, Vector3.one * UmbreonMoveRules.UniteRadius);
                Sparks(graphics.transform, new Color(0.65f, 0.25f, 1), UmbreonMoveRules.UniteAuraRadius, 35);
            });
            foreach (var name in new[] { "Claw", "Tears", "Beam", "Mark", "Focus", "WishBloom", "SnarlPulse", "SnarlBurst", "MoonBlast" }) Visual(name);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (var id in Ids) Register(UmbreonMoveRules.Folder + id + ".prefab");
        string icon = ResourcesFolder + "FoulFollowup.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(icon) == null)
            AssetDatabase.CopyAsset("Assets/Sprites/MoveIcons/Umbreon/t_Skill_Umbreon_S12_1.png", icon);
        ConfigureMoves();
        AssetDatabase.SaveAssets();
        Debug.Log("Umbreon: seven moves, learnset, passive, editable effects and network/Addressables registrations authored. Existing stats/models/animator preserved.");
    }
    private static void Effect<T>(AvailableMoves id, Action<GameObject, GameObject, T> author) where T : PrefabMoveEffect
    {
        string path = UmbreonMoveRules.Folder + id + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(id.ToString()); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var graphics = Node("Graphics", root.transform); var effect = root.AddComponent<T>();
        Set(effect, "graphics", graphics); author(root, graphics, effect);
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Swift(GameObject root, GameObject graphics, UmbreonSwiftEffect effect)
    {
        var data = new SerializedObject(effect); var stars = data.FindProperty("stars"); stars.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var node = MeshNode(i == 0 ? "LargeStar" : "SmallStar" + i, graphics.transform, star, gold,
                new Vector3((i - 1.5f) * 0.35f, 1, 0), Vector3.one * (i == 0 ? 0.35f : 0.2f));
            Sparks(node.transform, Color.yellow, 0.05f, 12);
            stars.GetArrayElementAtIndex(i).objectReferenceValue = node.transform;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Mean(GameObject root, GameObject graphics, UmbreonMeanLookEffect effect)
    {
        var field = Node("ConfinementZone", graphics.transform);
        MeshNode("BlackBoundary", field.transform, ring, dark, Vector3.up * 0.12f, Vector3.one);
        MeshNode("VioletBoundary", field.transform, ring, violet, Vector3.up * 0.16f, Vector3.one * 1.04f);
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8;
            MeshNode("NightFang" + i, field.transform, shard, i % 2 == 0 ? gold : violet,
                new Vector3(Mathf.Sin(angle), 0.18f, Mathf.Cos(angle)), new Vector3(0.04f, 0.22f, 0.04f));
        }
        Set(effect, "field", field.transform);
    }
    private static void Visual(string name)
    {
        string path = ResourcesFolder + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(name); root.AddComponent<TimedCombatVisual>();
        if (name == "Beam")
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "DarkGazeBeam";
            SceneManager.MoveGameObjectToScene(cube, preview); cube.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(cube.GetComponent<Collider>()); cube.transform.localPosition = Vector3.forward * 0.5f;
            cube.GetComponent<MeshRenderer>().sharedMaterial = violet;
        }
        else if (name == "Mark")
            MeshNode("MarkedStar", root.transform, star, gold, Vector3.zero, Vector3.one);
        else
        {
            bool moon = name == "MoonBlast" || name == "WishBloom" || name == "Focus";
            MeshNode("OuterWave", root.transform, ring, moon ? gold : violet, Vector3.up * 0.12f, Vector3.one);
            MeshNode("InnerWave", root.transform, ring, moon ? white : dark, Vector3.up * 0.18f, Vector3.one * 0.85f);
            if (name == "Focus")
                for (int i = 0; i < 3; i++) MeshNode("FocusHoop" + i, root.transform, ring, gold, Vector3.zero, Vector3.one).transform.localRotation = Quaternion.Euler(i * 60, i * 45, 25);
            if (name == "Claw")
                for (int i = 0; i < 3; i++) MeshNode("DarkSlash" + i, root.transform, shard, i == 1 ? gold : violet,
                    new Vector3((i - 1) * 0.3f, 0.5f, 0), new Vector3(0.1f, 0.6f, 0.1f)).transform.localRotation = Quaternion.Euler(0, 0, -40);
            Sparks(root.transform, moon ? new Color(1, 0.8f, 0.2f) : new Color(0.6f, 0.15f, 1), 0.8f, 40);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void ConfigureMoves()
    {
        string[] icons = { "S1", "S2", "S11", "S12", "S21", "S22", "U1" };
        string[] previewNames = { "S1", "S2", "S11", "S12", "S21", "S22", "U" };
        string[] descriptions = {
            "Fire one large and three small homing stars. The fourth marks for 4s. Nearby marked enemies grant 15% movement speed; damaging them readies a boosted attack.",
            "Damage enemies in a circle and reduce their Defense and Sp. Def by 20% for 2s. Gain a 12% max-HP shield for 4s.",
            "Slow a target by 30% for 2s. After 0.5s, confine it in a stationary zone for 3s. While Umbreon is inside, its attacks become stronger boosted attacks. Cleanse can prevent formation during the delay only. Upgrade: 3.5s confinement.",
            "Dash through walls and enemies, slowing by 15% for 2.5s and recording their highest Attack. On hit, recast within 6s to dash again and tail-sweep, slowing by 30% and shoving for 0.6s. Follow-up uses the recorded Attack when it exceeded Umbreon's on the first hit. Cooldown begins after follow-up or expiry. Upgrade: each dash readies a boosted attack.",
            "Wish for an ally or self. Reduce incoming damage by 20% for 2s, then heal. On recipient death, transfer to the lowest-HP% nearby living ally without restarting the timer. Upgrade: 30% reduction and increased healing.",
            "Snarl for 1.5s, gaining hindrance resistance and a 20% max-HP shield for 3s. Move 30% slower; basics and other moves are locked but battle items remain usable. Eight pulses deal damage, slow, and reduce Attack/Sp. Atk by 3% up to six stacks. The final shout reduces them by another 15% and stuns for 0.7s. Upgrade: 1s stun.",
            "Leap with reduced movement and battle items available, stealing 50% of eligible enemy shields. Land with damage and a 1s stun; for 5s a following aura slows enemies by 30% and steals half of new shields. Stolen shields expire 3s after the aura. Rayquaza and Assault Vest shields cannot be stolen. Learning this move grants 1% max-HP healing per second out of combat." };
        var assets = new MoveAsset[Ids.Length];
        for (int i = 0; i < Ids.Length; i++)
        {
            string path = "Assets/Moves/Umbreon/" + Ids[i] + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
            if (asset == null)
            {
                var definition = MoveDatabase.GetMove(Ids[i]); asset = ScriptableObject.CreateInstance<MoveAsset>();
                asset.move = Ids[i]; asset.moveName = definition.Name; asset.cooldown = definition.Cooldown;
                asset.moveType = i == 6 ? MoveType.UniteMove : i == 0 || i == 2 || i == 3 ? MoveType.MoveA : MoveType.MoveB;
                asset.moveLabel = i == 4 ? MoveLabels.Recovery : i == 3 ? MoveLabels.Dash : i == 0 || i == 2 ? MoveLabels.SureHit : MoveLabels.Area;
                asset.uniteEnergyCost = i == 6 ? 90000 : 0; asset.description = descriptions[i];
                asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/Umbreon/t_Skill_Umbreon_" + icons[i] + ".png");
                string image = AssetDatabase.FindAssets("t_Growth_Skill_Umbreon_" + previewNames[i] + "_", new[] { "Assets/Sprites/MoveIcons/Umbreon/Previews" })
                    .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (image == null || asset.icon == null) throw new InvalidOperationException("Missing Umbreon move art: " + icons[i]);
                asset.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(image)); AssetDatabase.CreateAsset(asset, path);
            }
            assets[i] = asset;
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Umbreon.asset"));
        pokemon.FindProperty("passive").enumValueIndex = (int)AvailablePassives.UmbreonPassive;
        var rows = pokemon.FindProperty("learnableMoves");
        if (rows.arraySize == 0)
        {
            int[] levels = { 0, 1, 3, 5, 7, 9, 11 };
            int[][] choices = { new[] { 0, 1 }, new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6 }, new[] { 2, 3 }, new[] { 4, 5 } };
            rows.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++)
            {
                var row = rows.GetArrayElementAtIndex(i); row.FindPropertyRelative("level").intValue = levels[i]; row.FindPropertyRelative("isUpgraded").boolValue = i >= 5;
                var moves = row.FindPropertyRelative("moves"); moves.arraySize = choices[i].Length;
                for (int j = 0; j < choices[i].Length; j++) moves.GetArrayElementAtIndex(j).objectReferenceValue = assets[choices[i][j]];
            }
        }
        pokemon.ApplyModifiedPropertiesWithoutUndo();
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset")); var entries = registry.FindProperty("moveEntries");
        foreach (var asset in assets)
        {
            if (Enumerable.Range(0, entries.arraySize).Any(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").longValue == (long)asset.move)) continue;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize++); entry.FindPropertyRelative("moveType").longValue = (long)asset.move;
            entry.FindPropertyRelative("moveAsset").objectReferenceValue = asset;
        }
        registry.ApplyModifiedPropertiesWithoutUndo();
        var settings = AddressableAssetSettingsDefaultObject.Settings; var previews = settings.FindGroup("MovePreviews");
        foreach (var asset in assets) settings.CreateOrMoveEntry(asset.preview.AssetGUID, previews).address = AssetDatabase.GUIDToAssetPath(asset.preview.AssetGUID);
        EditorUtility.SetDirty(previews); EditorUtility.SetDirty(settings);
    }
    private static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/Umbreon/" + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path); if (result != null) return result;
        result = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat")) { name = name };
        result.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(result, path); return result;
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var node = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(node, preview);
        if (parent != null) node.transform.SetParent(parent, false); return node;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var node = Node(name, parent); node.transform.localPosition = position; node.transform.localScale = scale;
        node.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return node;
    }
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var node = Node("Sparks", parent); var system = node.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.startColor = color; main.startLifetime = 0.4f; main.startSize = 0.09f; main.startSpeed = 0.4f;
        main.loop = true; main.playOnAwake = true; main.maxParticles = 150; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
        node.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing network spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab);
        var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path; EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")); var list = registry.FindProperty("List");
        if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab)) return;
        var entry = list.GetArrayElementAtIndex(list.arraySize++); entry.FindPropertyRelative("Override").enumValueIndex = 0;
        entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab; entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
        entry.FindPropertyRelative("SourceHashToOverride").longValue = 0; entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null; registry.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
