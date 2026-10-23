using System;
using System.Collections.Generic;
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

// Like Duraludon: editor-only authoring, persistent meshes/materials, editable
// Graphics hierarchy, disabled query hitboxes, no procedural art during play.
public static class CinderacePrefabBuilder
{
    private const string Materials = "Assets/Materials/Cinderace/";
    private static Scene scene;
    private static Mesh ring;
    private static Material orange, gold, white, red, smoke, particles;

    [MenuItem("Tools/Cinderace/Create Missing Move Prefabs")]
    public static void Build()
    {
        Folder(CinderMoveRules.Folder.TrimEnd('/')); Folder(CinderMoveRules.Folder + "Art"); Folder(Materials.TrimEnd('/'));
        orange = Material("FlameOrange", new Color(1, 0.18f, 0.015f)); gold = Material("FlameGold", new Color(1, 0.65f, 0.025f));
        white = Material("WhiteHot", new Color(1, 0.95f, 0.65f)); red = Material("CinderRed", new Color(0.7f, 0.025f, 0.01f));
        smoke = Material("DodgeBlue", new Color(0.4f, 0.75f, 1f));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        ring = Ring(); scene = EditorSceneManager.NewPreviewScene();
        try { foreach (CinderCast kind in Enum.GetValues(typeof(CinderCast))) Create(kind); }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        foreach (CinderCast kind in Enum.GetValues(typeof(CinderCast))) Register(CinderMoveRules.Path(kind));
        WireMoves(); AssetDatabase.SaveAssets();
        Debug.Log("Cinderace moves, learnset and network prefabs registered. Existing prefab layouts preserved.");
    }
    private static void Create(CinderCast kind)
    {
        string path = CinderMoveRules.Path(kind);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node("Cinder" + kind + "Object"); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var script = root.AddComponent<CinderMoveObject>();
        var graphics = Node("Graphics", root.transform); var travel = Node("Travel", graphics.transform); var impact = Node("Impact", graphics.transform);
        impact.SetActive(false);
        var area = Node("QueryHitbox", root.transform).AddComponent<SphereCollider>();
        area.radius = kind == CinderCast.PyroBall ? 0.65f : CinderMoveRules.Dash(kind) ? 1f : 0.4f; area.enabled = false; area.isTrigger = true;
        var explosion = Node("ExplosionArea", root.transform).AddComponent<SphereCollider>();
        explosion.radius = kind == CinderCast.Unite ? 3f : 1f; explosion.enabled = false; explosion.isTrigger = true;
        var pips = new List<GameObject>();
        if (kind == CinderCast.Cinders)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90 + 45) * Mathf.Deg2Rad;
                var pip = Primitive("Cinder" + (i + 1), travel.transform, PrimitiveType.Sphere, i % 2 == 0 ? gold : orange,
                    new Vector3(Mathf.Sin(a) * 0.35f, 2.1f, Mathf.Cos(a) * 0.35f), Vector3.one * 0.14f);
                pips.Add(pip);
            }
        }
        else if (kind == CinderCast.Burn)
        {
            for (int i = 0; i < 5; i++) Flame(travel.transform, "BurnFlame" + i, new Vector3(Mathf.Sin(i * 1.25f) * 0.4f, 0.5f, Mathf.Cos(i * 1.25f) * 0.4f), 0.3f);
            Sparks(travel.transform, orange.color, 0.45f, 22);
        }
        else if (kind == CinderCast.Blaze || kind == CinderCast.Feint)
        {
            for (int i = 0; i < 3; i++)
            {
                var hoop = MeshNode("AuraRing" + i, travel.transform, ring, kind == CinderCast.Feint ? smoke : gold,
                    Vector3.up * (0.2f + 0.55f * i), Vector3.one * (0.65f - 0.1f * i));
                hoop.transform.localRotation = Quaternion.Euler(i * 15, 0, i * 10);
            }
            Sparks(travel.transform, kind == CinderCast.Feint ? smoke.color : gold.color, 0.6f, 30);
        }
        else if (CinderMoveRules.Dash(kind) || kind == CinderCast.BlazeKick)
        {
            for (int i = 0; i < 5; i++)
            {
                var hoop = MeshNode("SpeedRing" + i, travel.transform, ring, i % 2 == 0 ? orange : gold,
                    new Vector3(0, 0.65f, -0.25f - 0.35f * i), Vector3.one * (0.6f - 0.07f * i));
                hoop.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            if (kind != CinderCast.LowSweep) { Flame(travel.transform, "FlamingFoot", new Vector3(0, 0.3f, 0.65f), 0.45f); Sparks(travel.transform, gold.color, 0.5f, 45); }
        }
        else
        {
            float size = kind == CinderCast.Unite ? 1.6f : kind == CinderCast.PyroBall ? 0.8f : 0.32f;
            Primitive("FlameBall", travel.transform, PrimitiveType.Sphere, orange, Vector3.zero, Vector3.one * size);
            Primitive("HotCore", travel.transform, PrimitiveType.Sphere, white, Vector3.zero, new Vector3(size * 0.75f, size * 0.75f, size * 1.03f));
            for (int i = 0; i < 3; i++)
            {
                var hoop = MeshNode("BallBand" + i, travel.transform, ring, i == 1 ? red : gold, Vector3.zero, Vector3.one * size * 0.53f);
                hoop.transform.localRotation = Quaternion.Euler(i * 60, i * 45, 35);
            }
            for (int i = 0; i < 3; i++) Flame(travel.transform, "FlameTail" + i, new Vector3((i - 1) * size * 0.2f, 0, -size * (0.5f + 0.2f * i)), size * 0.5f);
            Sparks(travel.transform, gold.color, size * 0.5f, kind == CinderCast.Unite ? 65 : 25);
        }
        float radius = kind == CinderCast.Unite ? 3 : 0.7f;
        MeshNode("Shockwave", impact.transform, ring, gold, Vector3.zero, Vector3.one * radius);
        MeshNode("InnerShockwave", impact.transform, ring, orange, Vector3.up * 0.05f, Vector3.one * radius * 0.65f);
        Sparks(impact.transform, gold.color, radius, 70);
        var serialized = new SerializedObject(script);
        serialized.FindProperty("kind").enumValueIndex = (int)kind;
        serialized.FindProperty("graphics").objectReferenceValue = graphics;
        serialized.FindProperty("travel").objectReferenceValue = travel;
        serialized.FindProperty("impact").objectReferenceValue = impact;
        serialized.FindProperty("hitbox").objectReferenceValue = area;
        serialized.FindProperty("explosionArea").objectReferenceValue = explosion;
        var list = serialized.FindProperty("markPips"); list.arraySize = pips.Count;
        for (int i = 0; i < pips.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void Flame(Transform parent, string name, Vector3 position, float size)
    {
        var go = Primitive(name, parent, PrimitiveType.Capsule, orange, position, new Vector3(size * 0.7f, size, size * 0.7f));
        go.transform.localRotation = Quaternion.Euler(35, 0, -20);
        Primitive(name + "Core", go.transform, PrimitiveType.Sphere, gold, Vector3.zero, new Vector3(0.65f, 0.7f, 1.05f));
    }
    private static void WireMoves()
    {
        MoveAsset kick = AssetDatabase.LoadAssetAtPath<MoveAsset>("Assets/Moves/Cinderace/CinderBlazeKick.asset");
        if (kick == null)
        {
            kick = ScriptableObject.CreateInstance<MoveAsset>(); kick.move = AvailableMoves.CinderBlazeKick; kick.moveType = MoveType.MoveA;
            kick.moveLabel = MoveLabels.SureHit; kick.moveName = "Blaze Kick";
            kick.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MoveIcons/Cinderace/t_Skill_Scorbunny_S12.png");
            kick.preview = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID("Assets/Sprites/MoveIcons/Cinderace/Preview/t_Growth_Skill_Scorbunny_S1B_dec675cbd5b2851b.jpg"));
            AssetDatabase.CreateAsset(kick, "Assets/Moves/Cinderace/CinderBlazeKick.asset");
        }
        foreach (CinderCast kind in Enum.GetValues(typeof(CinderCast)))
        {
            if (kind > CinderCast.Unite) continue;
            string name = kind == CinderCast.Unite ? "BlazingBycicleKick" : kind == CinderCast.PyroBall ? "CinderPyroball" : "Cinder" + kind;
            var move = AssetDatabase.LoadAssetAtPath<MoveAsset>("Assets/Moves/Cinderace/" + name + ".asset");
            move.cooldown = kind == CinderCast.Unite ? 112 : CinderMoveRules.Cooldown(kind);
            move.moveName = MoveDatabase.GetMove(move.move).Name;
            move.description = Description(kind); EditorUtility.SetDirty(move);
            var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset"));
            var entries = registry.FindProperty("moveEntries"); bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveType").intValue == (int)move.move)
                { entries.GetArrayElementAtIndex(i).FindPropertyRelative("moveAsset").objectReferenceValue = move; found = true; break; }
            if (!found)
            {
                int index = entries.arraySize++; var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("moveType").intValue = (int)move.move; entry.FindPropertyRelative("moveAsset").objectReferenceValue = move;
            }
            registry.ApplyModifiedPropertiesWithoutUndo();
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cinderace.asset"));
        var learns = pokemon.FindProperty("learnableMoves");
        for (int i = 0; i < learns.arraySize; i++)
        {
            var entry = learns.GetArrayElementAtIndex(i); int level = entry.FindPropertyRelative("level").intValue;
            if (level != 6 && level != 10) continue;
            var choices = entry.FindPropertyRelative("moves");
            if (!Enumerable.Range(0, choices.arraySize).Any(j => choices.GetArrayElementAtIndex(j).objectReferenceValue == kick))
                choices.GetArrayElementAtIndex(choices.arraySize++).objectReferenceValue = kick;
        }
        pokemon.ApplyModifiedPropertiesWithoutUndo();
    }
    private static string Description(CinderCast kind)
    {
        switch (kind)
        {
            case CinderCast.Ember: return "Launch a sure-hit flame. Burns for three ticks over 3s and reduces Attack and Sp. Attack by 5% for 2s.";
            case CinderCast.LowSweep: return "Dash forward, damaging opposing Pokémon in the path.";
            case CinderCast.PyroBall: return "Kick a piercing fireball, damaging and burning opposing Pokémon. Upgrade: hitting reduces Flame Charge or Feint's cooldown by 4s.";
            case CinderCast.BlazeKick: return "Become unstoppable and dash to the target with a guaranteed critical kick, shoving for 0.5s and burning. Jump back, or choose a rebound direction with movement input. Upgrade: grants 30% attack speed for 3s on hit.";
            case CinderCast.FlameCharge: return "Dash forward cloaked in flame, damaging opposing Pokémon. Upgrade: the next boosted attack slows by 30% for 2s.";
            case CinderCast.Feint: return "Cleanse slows, become invincible for 0.73s and gain 40% movement speed for 2s. Upgrade: the next three attacks heal for 30% of damage dealt but cannot critically hit.";
            default: return "Become unstoppable and kick a giant fireball toward the designated opponent's location. The explosion deals area damage plus 14% of each enemy's missing HP. Hitting grants 10% movement speed for 5s. Grants Unite movement speed, attack speed and shield buffs for 6s.";
        }
    }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0)
            throw new InvalidOperationException("Missing Cinderace network spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab);
        var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset"));
        var list = registry.FindProperty("List");
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) return;
        int index = list.arraySize++; var entry = list.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("Override").enumValueIndex = 0; entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null; entry.FindPropertyRelative("SourceHashToOverride").longValue = 0;
        entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null; registry.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(go, scene);
        if (parent != null) go.transform.SetParent(parent, false); return go;
    }
    private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 8; SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return go;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent); go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return go;
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat"; var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var result = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/RuinOrange.mat")) { name = name };
        result.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(result, path); return result;
    }
    private static Mesh Ring()
    {
        string path = CinderMoveRules.Folder + "Art/FlameRing.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        const int segments = 48, sides = 6; var vertices = new Vector3[(segments + 1) * (sides + 1)]; var triangles = new List<int>();
        for (int i = 0; i <= segments; i++) for (int j = 0; j <= sides; j++)
        {
            float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides, r = 1 + Mathf.Cos(b) * 0.055f;
            int n = i * (sides + 1) + j; vertices[n] = new Vector3(Mathf.Sin(a) * r, Mathf.Sin(b) * 0.055f, Mathf.Cos(a) * r);
            if (i < segments && j < sides) triangles.AddRange(new[] { n, n + sides + 1, n + 1, n + 1, n + sides + 1, n + sides + 2 });
        }
        var mesh = new Mesh { name = "FlameRing", vertices = vertices, triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var system = Node("Embers", parent).AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = true; main.playOnAwake = true; main.startLifetime = 0.35f; main.startSpeed = 0.8f;
        main.startSize = 0.08f; main.startColor = color; main.maxParticles = 100; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
        var life = system.colorOverLifetime; life.enabled = true; var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        life.color = gradient; system.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
