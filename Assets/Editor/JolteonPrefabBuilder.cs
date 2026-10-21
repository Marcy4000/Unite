using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Creates only missing rework assets. Existing Jolteon/Swift/basic prefabs and
// subsequent hand edits are preserved. No geometry is generated during a match.
public static class JolteonPrefabBuilder
{
    private const string Folder = JoltMoveRules.EffectFolder;
    private const string Materials = "Assets/Materials/Jolteon/";
    private static Scene preview;
    private static Mesh ring, bolt;
    private static Material gold, white, cyan, particle;

    [MenuItem("Tools/Jolteon/Create Missing Rework Prefabs")]
    public static void Build()
    {
        EnsureFolder(Folder.TrimEnd('/'));
        EnsureFolder(Folder + "Art");
        EnsureFolder(Materials.TrimEnd('/'));
        ring = BakeRing();
        bolt = BakeBolt();
        gold = MakeMaterial("ElectricGold", new Color(1f, 0.72f, 0.025f));
        white = MakeMaterial("WhiteHot", new Color(1f, 0.98f, 0.65f));
        cyan = MakeMaterial("IonCyan", new Color(0.2f, 0.85f, 1f));
        particle = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/JolteonParticle.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (JoltEffect effect in Enum.GetValues(typeof(JoltEffect))) Create(effect);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (JoltEffect effect in Enum.GetValues(typeof(JoltEffect))) Register(JoltMoveRules.EffectPath(effect));
        AssetDatabase.SaveAssets();
        Debug.Log("Jolteon rework prefabs authored and registered; existing assets preserved.");
    }

    private static void Create(JoltEffect effect)
    {
        string path = JoltMoveRules.EffectPath(effect);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(effect.ToString());
        root.AddComponent<NetworkObject>();
        if (effect == JoltEffect.Electroweb) root.AddComponent<NetworkTransform>();
        var script = root.AddComponent<JoltEffectObject>();
        var graphics = Node("Graphics", root.transform);
        var accent = Node("ElectricArcs", graphics.transform);
        Set(script, "graphics", graphics);
        Set(script, "accent", accent.transform);
        var serialized = new SerializedObject(script);
        serialized.FindProperty("effect").enumValueIndex = (int)effect;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        switch (effect)
        {
            case JoltEffect.Electroweb:
                // Bowed lattice in front of the projectile; not a filled cube.
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 6; col++)
                    {
                        Vector3 a = WebPoint(col, row), b = WebPoint(col + 1, row);
                        Segment(graphics.transform, "CrossArc", a, b, 0.55f, row == 1 ? white : gold);
                    }
                for (int col = 0; col <= 6; col++) Segment(graphics.transform, "Lattice", WebPoint(col, 0), WebPoint(col, 2), 0.4f, cyan);
                var area = root.AddComponent<BoxCollider>();
                area.isTrigger = true; area.enabled = false;
                area.center = new Vector3(0, 0.75f, 0);
                area.size = new Vector3(JoltMoveRules.WebWidth, 1.5f, 0.3f);
                Set(script, "webArea", area);
                // A travelling net must not spin its collision-independent lattice.
                Set(script, "accent", null);
                break;
            case JoltEffect.WebMark:
                MeshNode("MarkBoundary", accent.transform, ring, gold, new Vector3(0, 1.9f, 0), Vector3.one * 0.5f);
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * 2f / 3f;
                    Vector3 point = new Vector3(Mathf.Cos(angle) * 0.4f, 1.9f, Mathf.Sin(angle) * 0.4f);
                    Segment(accent.transform, "MarkProng", point, point + Vector3.up * 0.3f, 0.7f, white);
                }
                break;
            case JoltEffect.Discharge:
                MeshNode("FieldBoundary", graphics.transform, ring, gold, Vector3.up * 0.12f, Vector3.one * JoltMoveRules.DischargeRadius);
                RadialBolts(accent.transform, 8, JoltMoveRules.DischargeRadius - 0.15f, 0.6f);
                Sparks(graphics.transform, JoltMoveRules.DischargeRadius, 30);
                break;
            case JoltEffect.ThunderFang:
                for (int i = 0; i < 2; i++)
                {
                    var jaw = MeshNode("ElectricJaw", graphics.transform, ring, i == 0 ? gold : white,
                        new Vector3(0, 0.7f + i * 0.25f, 1.1f), new Vector3(0.7f, 1, 0.35f));
                    jaw.transform.localRotation = Quaternion.Euler(i == 0 ? 25 : -25, 0, 0);
                }
                for (int i = 0; i < 4; i++) Segment(graphics.transform, "Fang", new Vector3((i - 1.5f) * 0.3f, 1.1f, 1.1f),
                    new Vector3((i - 1.5f) * 0.3f, 0.5f, 1.4f), 0.6f, white);
                Sparks(graphics.transform, 0.6f, 35);
                Set(script, "accent", null);
                break;
            case JoltEffect.Charge:
                MeshNode("ChargeRing", accent.transform, ring, gold, Vector3.up * 0.2f, Vector3.one * 0.9f);
                RadialBolts(accent.transform, 5, 0.9f, 1f);
                Sparks(graphics.transform, 0.75f, 18);
                break;
            case JoltEffect.LightningBlitz:
                RadialBolts(accent.transform, 8, 1.1f, 0.8f);
                Trail(graphics.transform, new Vector3(-0.25f, 0.7f, 0));
                Trail(graphics.transform, new Vector3(0.25f, 0.9f, 0));
                Sparks(graphics.transform, 0.8f, 50);
                break;
            case JoltEffect.Overcharge:
                MeshNode("OverchargeRing", accent.transform, ring, white, Vector3.up * 0.15f, Vector3.one);
                RadialBolts(accent.transform, 5, 0.8f, 1f);
                Sparks(graphics.transform, 0.8f, 22);
                break;
            case JoltEffect.Ready:
                RadialBolts(accent.transform, 3, 0.6f, 0.8f);
                Sparks(graphics.transform, 0.45f, 5);
                break;
            case JoltEffect.Agility:
                MeshNode("FootSparkRing", accent.transform, ring, gold, Vector3.up * 0.12f, Vector3.one * 0.7f);
                Trail(graphics.transform, Vector3.up * 0.35f);
                Sparks(graphics.transform, 0.55f, 12);
                break;
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static Vector3 WebPoint(int column, int row)
    {
        float x = (column / 6f - 0.5f) * JoltMoveRules.WebWidth;
        return new Vector3(x, 0.2f + row * 0.55f, Mathf.Abs(x) * 0.12f);
    }

    private static void RadialBolts(Transform parent, int count, float radius, float height)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2 / count;
            var start = new Vector3(Mathf.Sin(a) * 0.35f, height, Mathf.Cos(a) * 0.35f);
            var end = new Vector3(Mathf.Sin(a) * radius, height * 0.5f, Mathf.Cos(a) * radius);
            Segment(parent, "Bolt" + i, start, end, 0.8f, i % 3 == 0 ? cyan : gold);
        }
    }

    private static void Segment(Transform parent, string name, Vector3 from, Vector3 to, float width, Material material)
    {
        var go = MeshNode(name, parent, bolt, material, from, new Vector3(width, 1, (to - from).magnitude));
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, to - from);
    }

    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 };
        SceneManager.MoveGameObjectToScene(go, preview);
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

    private static void Trail(Transform parent, Vector3 offset)
    {
        var go = Node("SpeedTrail", parent); go.transform.localPosition = offset;
        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/JolteonTrail.mat");
        trail.time = 0.18f; trail.startWidth = 0.22f; trail.endWidth = 0;
        trail.minVertexDistance = 0.1f; trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.startColor = new Color(1, 0.95f, 0.5f, 0.8f); trail.endColor = new Color(1, 0.6f, 0, 0);
    }

    private static void Sparks(Transform parent, float radius, float rate)
    {
        var go = Node("Sparks", parent);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.startLifetime = 0.2f; main.startSpeed = 0.6f; main.startSize = 0.06f;
        main.startColor = new Color(1, 0.85f, 0.2f); main.maxParticles = 45;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius; shape.radiusThickness = 0.1f; shape.rotation = new Vector3(90, 0, 0);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particle;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        string path = Materials + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null) return result;
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat");
        result = new Material(source) { name = name };
        result.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(result, path);
        return result;
    }

    private static Mesh BakeRing()
    {
        string path = Folder + "Art/ElectricRing.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 64; i++)
        {
            float a = i * Mathf.PI * 2 / 64;
            Vector3 d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            vertices.Add(d * 0.99f); vertices.Add(d * 1.01f);
            if (i < 64) { int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 }); }
        }
        return SaveMesh(path, vertices, triangles);
    }

    private static Mesh BakeBolt()
    {
        string path = Folder + "Art/ElectricBolt.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 6; i++)
        {
            float x = i == 0 || i == 6 ? 0 : (i % 2 == 0 ? -0.1f : 0.1f);
            vertices.Add(new Vector3(x - 0.025f, 0, i / 6f)); vertices.Add(new Vector3(x + 0.025f, 0, i / 6f));
            if (i < 6) { int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 }); }
        }
        return SaveMesh(path, vertices, triangles);
    }

    private static Mesh SaveMesh(string path, List<Vector3> vertices, List<int> triangles)
    {
        var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0)
            throw new InvalidOperationException("Jolteon effect has no spawn hash: " + path);
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
        var item = list.GetArrayElementAtIndex(list.arraySize - 1);
        item.FindPropertyRelative("Override").enumValueIndex = 0;
        item.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        item.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
        item.FindPropertyRelative("SourceHashToOverride").longValue = 0;
        item.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        registry.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
