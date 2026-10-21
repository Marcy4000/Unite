using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Editor-only placeholder authoring. Meshes, particles, materials and hitboxes
/// are saved to assets; none of this geometry is constructed during a match.
/// Re-running creates missing assets only and preserves subsequent prefab edits.
/// </summary>
public static class DuraludonPrefabBuilder
{
    private const string Art = DuraMovePrefabPaths.Folder + "Art/";
    private const string Materials = "Assets/Materials/Duraludon/";
    private const string MarkPath = DuraMovePrefabPaths.Folder + "DuraDragonPulseMarks.prefab";
    private static Mesh thinRing, wideRing, arc, shard;
    private static Material cyan, steel, bright, gold, fire, particles;
    private static Scene authoringScene;

    [MenuItem("Tools/Duraludon/Create Missing Move Prefabs")]
    public static void Build()
    {
        Folder(DuraMovePrefabPaths.Folder.TrimEnd('/'));
        Folder(Art.TrimEnd('/'));
        Folder(Materials.TrimEnd('/'));
        thinRing = Ring("ThinRing", 0.035f);
        wideRing = Ring("WideRing", 0.09f);
        arc = Arc();
        shard = Shard();
        cyan = Material("DragonEnergy", new Color(0.12f, 0.85f, 1f));
        steel = Material("SteelRock", new Color(0.19f, 0.27f, 0.36f));
        bright = Material("WhiteHot", new Color(0.8f, 0.98f, 1f));
        gold = Material("FocusedGold", new Color(1f, 0.79f, 0.18f));
        fire = Material("RuinOrange", new Color(1f, 0.19f, 0.04f));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");

        authoringScene = EditorSceneManager.NewPreviewScene();
        try
        {
            Create(DuraMoveEffect.LaserFocus, Focus);
            Create(DuraMoveEffect.DragonPulse, Pulse);
            Create(DuraMoveEffect.DragonTail, Tail);
            Create(DuraMoveEffect.StealthRock, Rock);
            Create(DuraMoveEffect.RevolvingRuin, Ruin);
            CreateMarks();
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(authoringScene);
            authoringScene = default;
        }

        foreach (DuraMoveEffect effect in Enum.GetValues(typeof(DuraMoveEffect))) Register(DuraMovePrefabPaths.Get(effect));
        WireMarks();
        AssetDatabase.SaveAssets();
        Debug.Log("Duraludon move prefabs authored and registered. Existing prefab layouts were preserved.");
    }

    private static void Create(DuraMoveEffect effect, Action<GameObject, GameObject> author)
    {
        string path = DuraMovePrefabPaths.Get(effect);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(System.IO.Path.GetFileNameWithoutExtension(path));
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();
        var graphics = Node("Graphics", root.transform);
        author(root, graphics);
        var script = root.GetComponent<DuraMoveObject>();
        Set(script, "graphics", graphics);
        // Keep the asset inspectable in Prefab Mode. OnNetworkSpawn hides it
        // synchronously until InitializeRPC supplies the cast parameters.
        graphics.SetActive(true);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void Focus(GameObject root, GameObject graphics)
    {
        var script = root.AddComponent<DuraLaserFocusObject>();
        var shielding = Node("Shielding", graphics.transform);
        for (int i = 0; i < 3; i++)
        {
            var hoop = MeshNode("FocusHoop" + i, shielding.transform, thinRing, gold, new Vector3(0, 1.2f, 0), Vector3.one * 1.1f);
            hoop.transform.localRotation = Quaternion.Euler(i * 60, i * 60, 25);
        }
        var focus = Node("EmpoweredAttacks", graphics.transform);
        focus.SetActive(false);
        MeshNode("FocusRing", focus.transform, thinRing, gold, new Vector3(0, 1.9f, 0), Vector3.one * 0.65f);
        for (int i = 0; i < 3; i++)
            Primitive("FocusPip" + i, focus.transform, PrimitiveType.Sphere, bright,
                new Vector3((i - 1) * 0.32f, 2.05f, 0), Vector3.one * 0.16f);
        Sparks(focus.transform, new Color(1, 0.8f, 0.25f), 0.6f, 10);
        Set(script, "shielding", shielding); Set(script, "focus", focus);
        Number(script, "maximumLifetime", 10);
    }

    private static void Pulse(GameObject root, GameObject graphics)
    {
        var script = root.AddComponent<DuraDragonPulseObject>();
        var area = Sphere(root, "PulseArea", 7.5f);
        var wave = Node("PulseWave", graphics.transform);
        MeshNode("EnergyRing", wave.transform, thinRing, cyan, new Vector3(0, 0.15f, 0), Vector3.one);
        MeshNode("InnerRing", wave.transform, thinRing, bright, new Vector3(0, 0.2f, 0), Vector3.one * 0.88f);
        Sparks(wave.transform, new Color(0.15f, 0.85f, 1f), 1, 45);
        Set(script, "pulseArea", area); Set(script, "pulseWave", wave.transform);
        Number(script, "maximumLifetime", 2);
    }

    private static void Tail(GameObject root, GameObject graphics)
    {
        var script = root.AddComponent<DuraDragonTailObject>();
        var area = Node("TailHitbox", root.transform).AddComponent<BoxCollider>();
        area.center = new Vector3(0, 1.2f, 2);
        area.size = new Vector3(5, 2.5f, 4);
        area.isTrigger = true; area.enabled = false;
        var sweep = Node("TailSweep", graphics.transform);
        for (int i = 0; i < 3; i++)
        {
            float radius = 2f + i * 0.7f;
            MeshNode("SteelSlash" + i, sweep.transform, arc, i == 1 ? bright : cyan,
                new Vector3(0, 0.4f + i * 0.15f, 0), new Vector3(radius, 1, radius));
            var tip = MeshNode("TailTip" + i, sweep.transform, shard, steel,
                new Vector3(0, 0.5f + i * 0.15f, radius), new Vector3(0.28f, 0.35f, 0.6f));
            tip.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        Set(script, "tailArea", area); Set(script, "tailSweep", sweep.transform);
        Number(script, "maximumLifetime", 2);
    }

    private static void Rock(GameObject root, GameObject graphics)
    {
        var script = root.AddComponent<DuraStealthRockObject>();
        var area = Sphere(root, "RockFieldHitbox", DuraMoveRules.RockRadius);
        var projectile = Node("ThrownStone", graphics.transform);
        var stone = MeshNode("Stone", projectile.transform, shard, steel, Vector3.up, new Vector3(0.45f, 0.6f, 0.4f));
        stone.transform.localRotation = Quaternion.Euler(15, 40, 20);
        MeshNode("EnergyCore", projectile.transform, shard, cyan, new Vector3(0, 1.25f, 0), Vector3.one * 0.22f);
        var hidden = Node("HiddenRockField", graphics.transform);
        var triggered = Node("TriggeredRockField", graphics.transform);
        hidden.SetActive(false); triggered.SetActive(false);
        RockField(hidden.transform, steel, false);
        RockField(triggered.transform, cyan, true);
        Set(script, "rockArea", area); Set(script, "projectile", projectile);
        Set(script, "hiddenField", hidden); Set(script, "triggeredField", triggered);
    }

    private static void RockField(Transform parent, Material material, bool active)
    {
        MeshNode("FieldBoundary", parent, thinRing, material, Vector3.up * 0.12f, Vector3.one * 2);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6;
            var stone = MeshNode("RockShard" + i, parent, shard, i % 3 == 0 ? material : steel,
                new Vector3(Mathf.Sin(a) * 1.5f, 0.25f, Mathf.Cos(a) * 1.5f),
                new Vector3(0.2f, active ? 0.8f : 0.45f, 0.25f));
            stone.transform.localRotation = Quaternion.Euler(12, i * 30, 15);
        }
        if (active) Sparks(parent, new Color(0.2f, 0.9f, 1), 1.7f, 25);
    }

    private static void Ruin(GameObject root, GameObject graphics)
    {
        var script = root.AddComponent<DuraludonUniteObject>();
        var area = Sphere(root, "SweepArea", DuraMoveRules.RuinRadius);
        var ringArea = Sphere(root, "BurningRingArea", DuraMoveRules.RuinRadius + 0.6f);
        var pivot = Node("BeamPivot", graphics.transform);
        var beam = Node("SweepingBeam", pivot.transform);
        Vector3 start = new Vector3(0, 1.4f, 0), end = new Vector3(0, 0.15f, 6);
        var ray = Primitive("OrangeBeam", beam.transform, PrimitiveType.Cylinder, fire, (start + end) * 0.5f,
            new Vector3(0.32f, (end - start).magnitude * 0.5f, 0.32f));
        ray.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
        var core = Primitive("HotBeamCore", beam.transform, PrimitiveType.Cylinder, bright,
            (start + end) * 0.5f + Vector3.up * 0.14f, new Vector3(0.09f, (end - start).magnitude * 0.5f, 0.09f));
        core.transform.localRotation = ray.transform.localRotation;
        var ring = Node("BurningRing", graphics.transform);
        ring.SetActive(false);
        MeshNode("MoltenRim", ring.transform, wideRing, fire, Vector3.up * 0.18f, Vector3.one * 6);
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI / 12;
            MeshNode("Flame" + i, ring.transform, shard, i % 2 == 0 ? fire : gold,
                new Vector3(Mathf.Sin(a) * 6, 0.4f, Mathf.Cos(a) * 6), new Vector3(0.18f, 0.7f, 0.18f));
        }
        Sparks(ring.transform, new Color(1, 0.3f, 0.05f), 6, 60);
        var explosion = Node("FinalBlast", graphics.transform);
        explosion.SetActive(false);
        MeshNode("BlastRing", explosion.transform, wideRing, gold, Vector3.up * 0.3f, Vector3.one);
        Set(script, "sweepArea", area); Set(script, "ringArea", ringArea);
        Set(script, "beamPivot", pivot.transform); Set(script, "beam", beam);
        Set(script, "burningRing", ring); Set(script, "explosion", explosion);
        Number(script, "maximumLifetime", 15);
    }

    private static void CreateMarks()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MarkPath) != null) return;
        var root = Node("DuraDragonPulseMarks");
        var view = root.AddComponent<DuraDragonPulseMarkView>();
        var orbs = new GameObject[5];
        for (int i = 0; i < orbs.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / orbs.Length;
            orbs[i] = Primitive("MarkOrb" + (i + 1), root.transform, PrimitiveType.Sphere, cyan,
                new Vector3(Mathf.Sin(angle) * 0.45f, 0, Mathf.Cos(angle) * 0.45f), Vector3.one * 0.16f);
            orbs[i].SetActive(false);
        }
        var ring = MeshNode("FinalMarkRing", root.transform, thinRing, bright, Vector3.zero, Vector3.one * 0.6f);
        ring.SetActive(false);
        Set(view, "finalRing", ring);
        var serialized = new SerializedObject(view);
        var list = serialized.FindProperty("orbs"); list.arraySize = orbs.Length;
        for (int i = 0; i < orbs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = orbs[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, MarkPath);
        Object.DestroyImmediate(root);
    }

    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 };
        SceneManager.MoveGameObjectToScene(go, authoringScene);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent);
        go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go;
    }

    private static GameObject Primitive(string name, Transform parent, PrimitiveType primitive, Material material, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(primitive); go.name = name; go.layer = 8;
        SceneManager.MoveGameObjectToScene(go, authoringScene);
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go;
    }

    private static SphereCollider Sphere(GameObject root, string name, float radius)
    {
        var collider = Node(name, root.transform).AddComponent<SphereCollider>();
        collider.radius = radius; collider.isTrigger = true; collider.enabled = false;
        return collider;
    }

    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var go = Node("Sparks", parent);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true; main.playOnAwake = true; main.startLifetime = 0.45f;
        main.startSpeed = 0.4f; main.startSize = 0.09f; main.startColor = color;
        main.maxParticles = 100; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius; shape.radiusThickness = 0.05f; shape.rotation = new Vector3(90, 0, 0);
        var overLife = system.colorOverLifetime; overLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        overLife.color = gradient;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }

    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Sprites/AimIndicator/pref_fx_Duraludon_spell01A_indicator_lv1/mat_fx_0884_wenli_06_gp.mat");
        var result = new Material(source) { name = name, renderQueue = 2000 };
        result.SetTexture("_BaseMap", null); result.SetColor("_BaseColor", color);
        result.SetFloat("_Surface", 0); result.SetFloat("_AlphaClip", 0); result.SetFloat("_Cull", 0);
        result.SetFloat("_SrcBlend", 1); result.SetFloat("_DstBlend", 0); result.SetFloat("_ZWrite", 1);
        result.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); result.DisableKeyword("_ALPHATEST_ON");
        AssetDatabase.CreateAsset(result, path);
        return result;
    }

    private static Mesh Ring(string name, float thickness)
    {
        string path = Art + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        const int segments = 64, sides = 8;
        var vertices = new Vector3[(segments + 1) * (sides + 1)];
        var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
            for (int j = 0; j <= sides; j++)
            {
                float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides;
                float r = 1 + Mathf.Cos(b) * thickness;
                vertices[i * (sides + 1) + j] = new Vector3(Mathf.Sin(a) * r, Mathf.Sin(b) * thickness, Mathf.Cos(a) * r);
                if (i < segments && j < sides)
                {
                    int n = i * (sides + 1) + j;
                    triangles.AddRange(new[] { n, n + sides + 1, n + 1, n + 1, n + sides + 1, n + sides + 2 });
                }
            }
        return SaveMesh(path, name, vertices, triangles.ToArray());
    }

    private static Mesh Arc()
    {
        string path = Art + "TailArc.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var vertices = new Vector3[98]; var triangles = new List<int>();
        for (int i = 0; i <= 48; i++)
        {
            float a = Mathf.Lerp(-65, 65, i / 48f) * Mathf.Deg2Rad;
            vertices[i * 2] = new Vector3(Mathf.Sin(a) * 0.92f, 0, Mathf.Cos(a) * 0.92f);
            vertices[i * 2 + 1] = new Vector3(Mathf.Sin(a) * 1.04f, 0, Mathf.Cos(a) * 1.04f);
            if (i < 48) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        return SaveMesh(path, "TailArc", vertices, triangles.ToArray());
    }

    private static Mesh Shard()
    {
        string path = Art + "RockShard.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        return SaveMesh(path, "RockShard", new[] { Vector3.up, Vector3.down * 0.5f, Vector3.left, Vector3.forward, Vector3.right, Vector3.back },
            new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 });
    }

    private static Mesh SaveMesh(string path, string name, Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }

    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Number(Object target, string field, float value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Register(string path)
    {
        // A newly saved object from a preview scene initially has no persistent
        // GlobalObjectId. Import the saved asset so NGO validates its prefab hash
        // before registering it or saving the Addressables group.
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var network = new SerializedObject(prefab.GetComponent<NetworkObject>());
        if (network.FindProperty("GlobalObjectIdHash").longValue == 0)
            throw new InvalidOperationException("Network prefab has not generated a spawn hash: " + path);
        // SaveAssets alone does not persist prefab-component validation changes.
        // Save the imported prefab explicitly so player builds see the same hash.
        prefab = PrefabUtility.SavePrefabAsset(prefab);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset"));
        var list = registry.FindProperty("List");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) return;
        list.InsertArrayElementAtIndex(list.arraySize);
        var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("Override").enumValueIndex = 0;
        entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
        entry.FindPropertyRelative("SourceHashToOverride").longValue = 0;
        entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        registry.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireMarks()
    {
        const string path = "Assets/Prefabs/Objects/BasicAtk/DuraludonBoostedAtk.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var serialized = new SerializedObject(root.GetComponent<DuraBoostedObj>());
            var field = serialized.FindProperty("pulseMarkPrefab");
            if (field.objectReferenceValue != null) return;
            field.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(MarkPath).GetComponent<DuraDragonPulseMarkView>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path));
    }
}
