using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// A one-time migration of Flygon's existing art, plus idempotent missing-asset authoring.
// Existing script/prefab GUIDs and Supersonic's traveling particle effect are preserved.
public static class FlygonPrefabBuilder
{
    private const string Art = FlygonMoveRules.Folder + "Art/";
    private const string Materials = "Assets/Materials/Flygon/";
    private static Mesh ring, spiral, shard;
    private static Material gold, green, earth, dust, particles;

    [MenuItem("Tools/Flygon/Upgrade Move Prefabs")]
    public static void Build()
    {
        Folder(Art.TrimEnd('/')); Folder(Materials.TrimEnd('/'));
        ring = Ring(); spiral = Spiral(); shard = Shard();
        gold = Material("SandGold", new Color(0.94f, 0.68f, 0.28f));
        green = Material("SonicGreen", new Color(0.33f, 0.89f, 0.65f));
        earth = Material("WarmEarth", new Color(0.4f, 0.24f, 0.11f));
        dust = Material("SandRibbon", new Color(0.82f, 0.59f, 0.27f, 0.18f), true);
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        foreach (FlygonCast kind in Enum.GetValues(typeof(FlygonCast))) Upgrade(kind);
        UpgradeTornadoShell();
        AssetDatabase.SaveAssets();
        Debug.Log("Flygon prefabs upgraded. Original particles preserved; authored hitboxes and sand/sound presentation registered.");
    }
    private static void Upgrade(FlygonCast kind)
    {
        string path = FlygonMoveRules.Path(kind);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null && prefab.GetComponentInChildren<FlygonMovePresentation>(true) != null) { Register(path); return; }
        var root = prefab != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject("FlygonBiteObject");
        try
        {
            root.layer = 8;
            if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
            var oldTransform = root.GetComponent<NetworkTransform>();
            if (oldTransform != null && oldTransform.GetType() != typeof(NetworkTransform)) Object.DestroyImmediate(oldTransform);
            var networkTransform = root.GetComponent<NetworkTransform>() ?? root.AddComponent<NetworkTransform>();
            networkTransform.SyncRotAngleY = true;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var original = new List<Transform>();
            foreach (Transform child in root.transform) original.Add(child);
            var graphics = Node("Graphics", root.transform);
            var oldArt = Node("OriginalArt", graphics.transform);
            foreach (var child in original) child.SetParent(oldArt.transform, false);
            foreach (var child in oldArt.GetComponentsInChildren<Transform>(true))
                if (child.name == "Square" || child.name == "GraphicsMaks" || child.name == "Indicator" || child.name == "WarningCircle"
                    || child.name == "pref_Indicator_Circle_Tower_Warning_lv3" || (kind == FlygonCast.Sandstorm && child.name == "Graphic")) child.gameObject.SetActive(false);
            var effect = root.GetComponent<FlygonMoveObject>() ?? AddEffect(root, kind);
            Set(effect, "graphics", graphics);
            SphereCollider area = Node("GameplayArea", root.transform).AddComponent<SphereCollider>();
            area.enabled = false; area.isTrigger = true;
            area.radius = kind == FlygonCast.Dig ? FlygonMoveRules.DigRadius : kind == FlygonCast.Earthquake ? FlygonMoveRules.QuakeRadius
                : kind == FlygonCast.Sandstorm ? FlygonMoveRules.StormRadius : kind == FlygonCast.Unite ? FlygonMoveRules.UniteRadius : kind == FlygonCast.Boomburst ? 5 : 1.2f;
            if (kind == FlygonCast.Bite) area.center = Vector3.forward * 1.65f;
            Set(effect, "area", area);
            var sourceParticles = oldArt.GetComponentInChildren<ParticleSystem>(true);
            foreach (var system in oldArt.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = system.main; main.playOnAwake = false; main.loop = false;
            }
            if (kind == FlygonCast.Supersonic)
            {
                Set(effect, "particles", sourceParticles);
                Set(effect, "waveArea", root.GetComponent<BoxCollider>());
                // Same authored particle geometry/material, emitted as a short wave train.
                var emission = sourceParticles.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1, 3, 0.1f) });
            }
            var anticipation = Node("Anticipation", graphics.transform);
            var active = Node("ActiveSand", graphics.transform);
            var spin = Node("Swirl", active.transform);
            float radius = area.radius;
            if (kind != FlygonCast.Supersonic && kind != FlygonCast.Bite)
            {
                MeshNode("CastBoundary", anticipation.transform, ring, gold, Vector3.up * 0.06f, new Vector3(radius, 1, radius));
                MeshNode("ActiveBoundary", active.transform, ring, kind == FlygonCast.Boomburst ? green : gold, Vector3.up * 0.07f, new Vector3(radius, 1, radius));
            }
            if (kind == FlygonCast.Boomburst)
            {
                MeshNode("ClosePowerBoundary", active.transform, ring, gold, Vector3.up * 0.1f, new Vector3(2, 1, 2));
                MeshNode("OuterShredBoundary", active.transform, ring, green, Vector3.up * 0.1f, new Vector3(4, 1, 4));
            }
            if (kind == FlygonCast.Bite)
            {
                for (int i = 0; i < 5; i++) MeshNode("BiteTooth" + i, active.transform, shard, gold,
                    new Vector3((i - 2) * 0.3f, 0.45f, 1.6f + Mathf.Abs(i - 2) * 0.15f), new Vector3(0.1f, 0.3f, 0.1f));
            }
            if (kind == FlygonCast.Sandstorm || kind == FlygonCast.Unite)
            {
                for (int i = 0; i < 3; i++)
                {
                    var ribbon = MeshNode("SandRibbon" + i, spin.transform, spiral, dust, Vector3.up * 0.06f,
                        new Vector3(radius * (0.55f + i * 0.15f), kind == FlygonCast.Unite ? 3 : 0.7f, radius * (0.55f + i * 0.15f)));
                    ribbon.localRotation = Quaternion.Euler(0, i * 120, 0);
                }
            }
            if (kind == FlygonCast.Dig || kind == FlygonCast.Earthquake)
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI / 6;
                    MeshNode("Debris" + i, spin.transform, shard, i % 3 == 0 ? gold : earth,
                        new Vector3(Mathf.Sin(angle) * radius * 0.8f, 0.12f, Mathf.Cos(angle) * radius * 0.8f), new Vector3(0.12f, 0.25f, 0.18f));
                }
            var impact = MeshNode("ImpactRing", graphics.transform, ring, kind == FlygonCast.Boomburst ? green : gold, Vector3.up * 0.15f, Vector3.one);
            var sand = kind == FlygonCast.Supersonic || kind == FlygonCast.Boomburst || kind == FlygonCast.Bite ? null : Sand(spin.transform, radius);
            var view = graphics.AddComponent<FlygonMovePresentation>();
            Set(view, "effect", effect); Set(view, "anticipation", anticipation); Set(view, "active", active);
            Set(view, "impactRing", impact); Set(view, "swirl", spin.transform); Set(view, "sand", sand);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (prefab != null) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        Register(path);
    }
    private static FlygonMoveObject AddEffect(GameObject root, FlygonCast kind)
    {
        switch (kind)
        {
            case FlygonCast.Bite: return root.AddComponent<FlygonBiteObject>();
            case FlygonCast.Dig: return root.AddComponent<FlygonDigWarning>();
            case FlygonCast.Supersonic: return root.AddComponent<SupersonicHitbox>();
            case FlygonCast.Boomburst: return root.AddComponent<BoomburstHitbox>();
            case FlygonCast.Earthquake: return root.AddComponent<EarthquakeArea>();
            case FlygonCast.Sandstorm: return root.AddComponent<SandstormArea>();
            default: return root.AddComponent<TornadoHitbox>();
        }
    }
    private static void UpgradeTornadoShell()
    {
        string path = FlygonMoveRules.Path(FlygonCast.Unite);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view = root.GetComponentInChildren<FlygonMovePresentation>(true);
            var data = new SerializedObject(view);
            if (data.FindProperty("tornadoShell").objectReferenceValue != null) return;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name == "Tornado")
                {
                    renderer.sharedMaterial = Material("TornadoVeil", new Color(0.88f, 0.64f, 0.28f, 0.12f), true);
                    Set(view, "tornadoShell", renderer.transform);
                    break;
                }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static GameObject Node(string name, Transform parent)
    {
        var go = new GameObject(name) { layer = 8 }; go.transform.SetParent(parent, false); return go;
    }
    private static Transform MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent); go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material; return go.transform;
    }
    private static ParticleSystem Sand(Transform parent, float radius)
    {
        var go = Node("SandGrains", parent);
        var system = go.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = true; main.playOnAwake = false; main.startLifetime = 0.7f; main.startSpeed = 0.35f;
        main.startSize = 0.07f; main.startColor = new Color(0.88f, 0.66f, 0.34f, 0.65f); main.maxParticles = 80;
        var emission = system.emission; emission.rateOverTime = 35;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius * 0.85f;
        shape.radiusThickness = 0.2f; shape.rotation = new Vector3(90, 0, 0);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles; return system;
    }
    private static Material Material(string name, Color color, bool transparent = false)
    {
        string path = Materials + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path); if (result != null) return result;
        var source = AssetDatabase.LoadAssetAtPath<Material>(transparent ? "Assets/Materials/Sandstorm.mat" : "Assets/Materials/Duraludon/DragonEnergy.mat");
        result = new Material(source) { name = name };
        result.SetColor("_BaseColor", color); result.SetColor("_Color", color); result.SetFloat("_Cull", 0);
        AssetDatabase.CreateAsset(result, path); return result;
    }
    private static Mesh Ring()
    {
        string path = Art + "BoundaryRing.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (old != null) return old;
        var vertices = new Vector3[130]; var triangles = new List<int>();
        for (int i = 0; i <= 64; i++)
        {
            float a = i * Mathf.PI / 32;
            vertices[i * 2] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.99f;
            vertices[i * 2 + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 1.01f;
            if (i < 64) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        return Save(path, vertices, triangles.ToArray());
    }
    private static Mesh Spiral()
    {
        string path = Art + "SandSpiral.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (old != null) return old;
        var vertices = new Vector3[194]; var triangles = new List<int>();
        for (int i = 0; i <= 96; i++)
        {
            float t = i / 96f, a = t * Mathf.PI * 4;
            Vector3 p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Lerp(0.45f, 1, t) + Vector3.up * t;
            vertices[i * 2] = p; vertices[i * 2 + 1] = p + Vector3.up * 0.08f;
            if (i < 96) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        return Save(path, vertices, triangles.ToArray());
    }
    private static Mesh Shard()
    {
        string path = Art + "SandShard.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (old != null) return old;
        return Save(path, new[] { Vector3.up, Vector3.down * 0.3f, Vector3.left, Vector3.forward, Vector3.right, Vector3.back },
            new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 });
    }
    private static Mesh Save(string path, Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path), vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing Flygon spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset"));
        var list = registry.FindProperty("List");
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) return;
        list.InsertArrayElementAtIndex(list.arraySize); var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("Override").enumValueIndex = 0; entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null; entry.FindPropertyRelative("SourceHashToOverride").longValue = 0;
        entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null; registry.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
