using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Editor-only authoring, like Duraludon: baked art, editable hitboxes, no runtime primitives.
public static class FlareonPrefabBuilder
{
    private const string Art = FlareonMoveRules.Folder + "Art/", Materials = "Assets/Materials/Flareon/";
    private static Scene authoring;
    private static Mesh ring, crescent, flame;
    private static Material orange, gold, cream, particles;
    private static AudioClip ignition, impact;

    [MenuItem("Tools/Flareon/Create Missing Move Prefabs")]
    public static void Build()
    {
        Folder(Art.TrimEnd('/')); Folder(Materials.TrimEnd('/'));
        ring = Ring(); crescent = Crescent(); flame = Flame();
        orange = Material("EmberOrange", new Color(1, 0.22f, 0.025f)); gold = Material("WarmGold", new Color(1, 0.65f, 0.1f));
        cream = Material("CreamHot", new Color(1, 0.93f, 0.67f));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        ignition = Sound("Ignition", false); impact = Sound("FluffyImpact", true);
        authoring = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (FlareonCast kind in Enum.GetValues(typeof(FlareonCast))) if (kind != FlareonCast.Basic) Create(kind);
            CreateBurn();
            CreatePeriodicTestFixture();
        }
        finally { EditorSceneManager.ClosePreviewScene(authoring); authoring = default; }
        foreach (FlareonCast kind in Enum.GetValues(typeof(FlareonCast))) if (kind != FlareonCast.Basic) Register(FlareonMoveRules.Path(kind));
        const string fixture = "Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab";
        AssetDatabase.ImportAsset(fixture, ImportAssetOptions.ForceUpdate);
        PrefabUtility.SavePrefabAsset(AssetDatabase.LoadAssetAtPath<GameObject>(fixture));
        Descriptions(); AssetDatabase.SaveAssets();
        Debug.Log("Flareon prefabs authored and registered. Existing layouts and unrelated license changes preserved.");
    }
    [MenuItem("Tools/Flareon/Render Effect Previews")]
    public static void RenderPreviews()
    {
        foreach (FlareonCast kind in new[] { FlareonCast.Unite, FlareonCast.Flamethrower, FlareonCast.HeatWave })
        {
            var preview = new PreviewRenderUtility(); Texture2D image = null;
            try
            {
                var model = preview.InstantiatePrefabInScene(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Flareon/Flareon.prefab"));
                foreach (Animator animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
                var effect = preview.InstantiatePrefabInScene(AssetDatabase.LoadAssetAtPath<GameObject>(FlareonMoveRules.Path(kind)));
                var script = effect.GetComponent<FlareonMoveObject>(); script.enabled = false; var data = new SerializedObject(script);
                foreach (string field in new[] { "body", "wave", "impact", "sweep" })
                {
                    Object value = data.FindProperty(field).objectReferenceValue;
                    var go = value is Transform t ? t.gameObject : value as GameObject; if (go != null) go.SetActive(false);
                }
                string shown = kind == FlareonCast.Unite ? "impact" : kind == FlareonCast.Flamethrower ? "sweep" : "wave";
                Object part = data.FindProperty(shown).objectReferenceValue; var graphics = part is Transform transform ? transform.gameObject : (GameObject)part;
                graphics.SetActive(true);
                if (kind != FlareonCast.Flamethrower) graphics.transform.localScale = Vector3.one * (kind == FlareonCast.HeatWave ? FlareonMoveRules.HeatRadius : 2);
                foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>()) { system.Simulate(0.18f, true, true); system.Pause(); }
                preview.camera.orthographic = true; preview.camera.orthographicSize = 4;
                preview.camera.transform.position = new Vector3(7, 9, -9); preview.camera.transform.LookAt(new Vector3(0, 0.3f, kind == FlareonCast.Flamethrower ? 1.5f : 0));
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(0.075f, 0.055f, 0.05f);
                preview.camera.nearClipPlane = 0.1f; preview.camera.farClipPlane = 50;
                preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(50, -35, 0); preview.lights[1].intensity = 0.7f;
                preview.ambientColor = new Color(0.5f, 0.42f, 0.36f);
                preview.BeginStaticPreview(new Rect(0, 0, 900, 600)); preview.Render(true, false); image = preview.EndStaticPreview();
                File.WriteAllBytes("/tmp/opencode/flareon-" + kind + ".png", image.EncodeToPNG());
            }
            finally { if (image != null) Object.DestroyImmediate(image); preview.Cleanup(); }
        }
        Debug.Log("Flareon effect previews saved under /tmp/opencode/flareon-*.png");
    }
    private static void Create(FlareonCast kind)
    {
        string path = FlareonMoveRules.Path(kind);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            if (kind == FlareonCast.State) WireMissingPreview(path);
            if (kind == FlareonCast.Flamethrower) WireFlameStream(path);
            return;
        }
        var root = Node("Flareon" + kind + "Object"); root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var script = root.AddComponent<FlareonMoveObject>();
        var graphics = Node("Graphics", root.transform); Set(script, "graphics", graphics);
        var body = Node("FireBody", graphics.transform); Set(script, "body", body);
        var wave = Node("HeatPulse", graphics.transform); Set(script, "wave", wave);
        MeshNode("OuterWave", wave.transform, ring, orange, Vector3.up * 0.08f, Vector3.one);
        MeshNode("HotInnerWave", wave.transform, ring, gold, Vector3.up * 0.1f, Vector3.one * 0.83f);
        var blast = Node("Impact", graphics.transform); Set(script, "impact", blast);
        MeshNode("ShockRing", blast.transform, ring, gold, Vector3.up * 0.1f, Vector3.one);
        MeshNode("InnerShockRing", blast.transform, ring, cream, Vector3.up * 0.14f, Vector3.one * 0.55f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4;
            var spark = MeshNode("ImpactSpark" + i, blast.transform, flame, i % 2 == 0 ? gold : orange,
                new Vector3(Mathf.Sin(a) * 0.7f, 0.35f, Mathf.Cos(a) * 0.7f), new Vector3(0.14f, 0.35f, 0.14f));
            spark.transform.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, -20);
        }
        Sparks(blast.transform, 0.65f, 30);
        var area = Node("AreaHitbox", root.transform).AddComponent<SphereCollider>();
        area.center = Vector3.up * 0.5f; area.radius = kind == FlareonCast.HeatWave ? FlareonMoveRules.HeatRadius : kind == FlareonCast.Swift || kind == FlareonCast.Unite ? 2 : 1;
        area.isTrigger = true; area.enabled = false; Set(script, "area", area);
        var sweep = Node("Sweep", graphics.transform); Set(script, "sweep", sweep.transform);
        var box = Node("FlameHitbox", sweep.transform).AddComponent<BoxCollider>(); box.isTrigger = true; box.enabled = false;
        box.center = FlareonMoveRules.FlameCenter; box.size = FlareonMoveRules.FlameSize; Set(script, "flameArea", box);
        if (kind == FlareonCast.Flamethrower)
        {
            AddFlameStream(sweep.transform);
        }
        else if (kind == FlareonCast.Swift)
        {
            var starPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlareonMoveRules.Folder + "FlareSwiftStar.prefab");
            for (int i = 0; i < 4; i++)
            {
                var star = (GameObject)PrefabUtility.InstantiatePrefab(starPrefab, authoring); star.name = "OrbitStar" + i; star.transform.SetParent(body.transform, false);
                float a = i * Mathf.PI / 2; star.transform.localPosition = new Vector3(Mathf.Sin(a) * 1.7f, 0.75f, Mathf.Cos(a) * 1.7f);
            }
        }
        else if (kind == FlareonCast.State) State(script, graphics.transform, body);
        else
        {
            bool aura = kind == FlareonCast.HeatWave;
            MeshNode("Footprint", body.transform, ring, orange, Vector3.up * 0.08f, Vector3.one * (aura ? FlareonMoveRules.HeatRadius : 0.7f));
            if (!aura)
            {
                for (int i = 0; i < 7; i++)
                {
                    float a = i * Mathf.PI * 2 / 7;
                    MeshNode("FlameLick" + i, body.transform, flame, i % 2 == 0 ? gold : orange,
                        new Vector3(Mathf.Sin(a) * 0.5f, 0.55f, Mathf.Cos(a) * 0.55f), new Vector3(0.2f, 0.7f, 0.2f));
                }
                if (kind == FlareonCast.FlareBlitz)
                    for (int i = 0; i < 3; i++)
                    {
                        var trail = MeshNode("StreamingFlame" + i, body.transform, crescent, i == 1 ? cream : orange,
                            new Vector3((i - 1) * 0.22f, 0.45f, -0.9f), new Vector3(0.3f, 1, 1.2f));
                        trail.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    }
            }
            Sparks(body.transform, aura ? 0.8f : 0.55f, 16);
        }
        var audio = graphics.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 1; audio.volume = kind == FlareonCast.Unite ? 0.35f : 0.16f;
        audio.minDistance = 3; audio.maxDistance = 18; audio.rolloffMode = AudioRolloffMode.Linear;
        Set(script, "effectAudio", audio); Set(script, "ignitionSound", ignition); Set(script, "impactSound", impact);
        foreach (var child in new[] { wave, blast, sweep }) child.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void State(FlareonMoveObject script, Transform parent, GameObject body)
    {
        AddPreview(script, parent);
        body.SetActive(false);
        var strike = Node("BasicContact", parent); Set(script, "strike", strike);
        MeshNode("FlameCrescent", strike.transform, crescent, gold, Vector3.zero, Vector3.one);
        MeshNode("WhiteContact", strike.transform, crescent, cream, Vector3.up * 0.05f, Vector3.one * 0.65f);
        strike.SetActive(false);
        var recast = Node("SlamTarget", parent); Set(script, "recastMarker", recast);
        MeshNode("RecastRing", recast.transform, ring, cream, Vector3.zero, Vector3.one * 0.65f); recast.SetActive(false);
        var full = Node("MaximumHeat", parent); Set(script, "fullHeat", full);
        MeshNode("WarmAura", full.transform, ring, orange, Vector3.up * 0.1f, Vector3.one * 0.8f); Sparks(full.transform, 0.65f, 12); full.SetActive(false);
        var serialized = new SerializedObject(script);
        foreach (string field in new[] { "heatPips" })
        {
            var array = serialized.FindProperty(field); array.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var pip = MeshNode(field + i, parent, flame, gold,
                    new Vector3((i - 1) * 0.24f, 1.98f, 0), new Vector3(0.09f, 0.15f, 0.09f));
                pip.SetActive(false); array.GetArrayElementAtIndex(i).objectReferenceValue = pip;
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void AddPreview(FlareonMoveObject script, Transform parent)
    {
        var preview = MeshNode("SweptFlameFootprint", parent, SweepFootprint(), gold, Vector3.zero, Vector3.one);
        Set(script, "flamePreview", preview); preview.SetActive(false);
    }
    private static void AddFlameStream(Transform parent)
    {
        var stream = Node("AuthoredFlameStream", parent);
        MeshNode("OrangeEnvelope", stream.transform, FireRibbon(), orange, new Vector3(0, 0.65f, 0.15f), Vector3.one);
        MeshNode("GoldRibbon", stream.transform, FireRibbon(), gold, new Vector3(0.025f, 0.68f, 0.18f), new Vector3(0.66f, 0.65f, 0.96f));
        MeshNode("CreamCore", stream.transform, FireRibbon(), cream, new Vector3(-0.015f, 0.7f, 0.2f), new Vector3(0.25f, 0.25f, 0.83f));
        var system = Node("StreamEmbers", stream.transform).AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = true; main.startLifetime = 0.55f; main.startSpeed = 7; main.startSize = 0.07f;
        main.startColor = new Color(1, 0.5f, 0.1f); main.maxParticles = 50;
        var emission = system.emission; emission.rateOverTime = 45;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 7; shape.radius = 0.12f;
        system.transform.localPosition = new Vector3(0, 0.65f, 0.45f); system.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static void WireFlameStream(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var script = root.GetComponent<FlareonMoveObject>(); var data = new SerializedObject(script);
            var pivot = (Transform)data.FindProperty("sweep").objectReferenceValue;
            if (pivot.Find("AuthoredFlameStream") != null) return;
            // Migrate only the initial placeholder nodes authored by this builder; retain the hitbox and layout.
            for (int i = pivot.childCount - 1; i >= 0; i--)
            {
                string name = pivot.GetChild(i).name;
                if (name.StartsWith("FlameRibbon") || name.StartsWith("HotCore") || name == "Embers") Object.DestroyImmediate(pivot.GetChild(i).gameObject);
            }
            AddFlameStream(pivot); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void WireMissingPreview(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var script = root.GetComponent<FlareonMoveObject>(); var data = new SerializedObject(script);
            if (data.FindProperty("flamePreview").objectReferenceValue != null) return;
            var parent = ((GameObject)data.FindProperty("graphics").objectReferenceValue).transform;
            AddPreview(script, parent); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void CreateBurn()
    {
        const string path = "Assets/Resources/Combat/BurnStatus.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        Folder("Assets/Resources/Combat");
        var root = Node("BurnStatus");
        Sparks(root.transform, 0.3f, 12);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2 / 3;
            MeshNode("Ember" + i, root.transform, flame, i == 0 ? gold : orange,
                new Vector3(Mathf.Sin(a) * 0.3f, 0.25f, Mathf.Cos(a) * 0.3f), new Vector3(0.08f, 0.25f, 0.08f));
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void CreatePeriodicTestFixture()
    {
        const string path = "Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        Folder("Assets/Editor/FlareonTests/Fixtures");
        var root = Node("PeriodicPokemon"); root.AddComponent<NetworkObject>(); root.AddComponent<Pokemon>();
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(go, parent != null ? parent.gameObject.scene : authoring);
        if (parent != null) go.transform.SetParent(parent, false); return go;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent); go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return go;
    }
    private static void Sparks(Transform parent, float radius, float rate)
    {
        var system = Node("Embers", parent).AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = true; main.playOnAwake = true; main.startLifetime = 0.4f; main.startSpeed = 0.8f;
        main.startSize = 0.055f; main.startColor = new Color(1, 0.5f, 0.08f); main.maxParticles = 45; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius; shape.rotation = new Vector3(90, 0, 0);
        var colors = system.colorOverLifetime; colors.enabled = true; var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(1, 0.65f, 0.1f), 0), new GradientColorKey(new Color(1, 0.15f, 0), 1) },
            new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) }); colors.color = gradient;
        system.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat"; var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/RuinOrange.mat")) { name = name };
        material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Mesh Ring()
    {
        string path = Art + "HeatRing.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 64; i++)
        {
            float a = i * Mathf.PI / 32; vertices.Add(new Vector3(Mathf.Sin(a) * 0.985f, 0, Mathf.Cos(a) * 0.985f)); vertices.Add(new Vector3(Mathf.Sin(a) * 1.015f, 0, Mathf.Cos(a) * 1.015f));
            if (i < 64) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        return Mesh(path, vertices.ToArray(), triangles.ToArray());
    }
    private static Mesh Crescent()
    {
        string path = Art + "ContactCrescent.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 24; i++)
        {
            float p = i / 24f, a = Mathf.Lerp(-80, 80, p) * Mathf.Deg2Rad, thickness = Mathf.Sin(p * Mathf.PI) * 0.25f;
            vertices.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a))); vertices.Add(new Vector3(Mathf.Sin(a) * (1 - thickness), 0.1f, Mathf.Cos(a) * (1 - thickness)));
            if (i < 24) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        return Mesh(path, vertices.ToArray(), triangles.ToArray());
    }
    private static Mesh SweepFootprint()
    {
        string path = Art + "SweptFlameFootprint.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        // Hull of the actual rotated hitbox corners, not a guessed cone/radius.
        var points = new List<Vector3>();
        for (int i = 0; i <= 12; i++)
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = FlareonMoveRules.FlameCenter + Vector3.Scale(FlareonMoveRules.FlameSize * 0.5f, new Vector3(x, 0, z)); point.y = 0;
                points.Add(Quaternion.Euler(0, -30 + i * 5, 0) * point);
            }
        points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
        var hull = new List<Vector3>();
        foreach (Vector3 p in points) { while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        int lower = hull.Count;
        for (int i = points.Count - 2; i >= 0; i--) { Vector3 p = points[i]; while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        hull.RemoveAt(hull.Count - 1); Vector3 center = Vector3.zero; foreach (Vector3 p in hull) center += p; center /= hull.Count;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= hull.Count; i++) { Vector3 p = hull[i % hull.Count]; vertices.Add(p); vertices.Add(p + (center - p).normalized * 0.035f); if (i < hull.Count) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); } }
        return Mesh(path, vertices.ToArray(), triangles.ToArray());
    }
    private static float Cross(Vector3 a, Vector3 b, Vector3 c) => (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
    private static Mesh Flame()
    {
        string path = Art + "FlameLick.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        return Mesh(path, new[] { new Vector3(0.2f, 1, 0), Vector3.down * 0.25f, Vector3.left, Vector3.forward, Vector3.right, Vector3.back },
            new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 });
    }
    private static Mesh FireRibbon()
    {
        string path = Art + "ContinuousFireRibbon.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        const int segments = 36;
        for (int sheet = 0; sheet < 3; sheet++)
        {
            int offset = vertices.Count; float angle = sheet * Mathf.PI / 3;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments, width = (0.12f + t * 0.5f) * (t > 0.82f ? (1 - t) / 0.18f : 1);
                width *= 1 + Mathf.Sin(t * 37 + sheet) * 0.11f;
                Vector3 center = new Vector3(Mathf.Sin(t * 15 + sheet) * t * 0.09f, Mathf.Sin(t * 18) * t * 0.05f, t * 4.3f);
                Vector3 side = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * width;
                vertices.Add(center - side); vertices.Add(center + side);
                if (i < segments) { int n = offset + i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
            }
        }
        return Mesh(path, vertices.ToArray(), triangles.ToArray());
    }
    private static Mesh Mesh(string path, Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path), vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static AudioClip Sound(string name, bool contact)
    {
        string path = Art + name + ".wav"; var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (existing != null) return existing;
        const int rate = 22050; int count = (int)(rate * (contact ? 0.22f : 0.32f)); var random = new System.Random(136); float filtered = 0;
        using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate, p = i / (float)count; filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, 0.2f);
                float envelope = contact ? Mathf.Exp(-p * 7) * Mathf.Min(1, p * 100) : Mathf.Sin(p * Mathf.PI) * (1 - p);
                float value = (filtered * 0.7f + Mathf.Sin(t * (contact ? 130 : 220 - p * 120) * Mathf.PI * 2) * 0.15f) * envelope;
                writer.Write((short)(Mathf.Clamp(value, -1, 1) * 22000));
            }
        }
        AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Register(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab);
        var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path; EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")); var entries = registry.FindProperty("List");
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) return;
        entries.InsertArrayElementAtIndex(entries.arraySize); var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        entry.FindPropertyRelative("Override").enumValueIndex = 0; entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null; entry.FindPropertyRelative("SourceHashToOverride").longValue = 0; entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        registry.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Descriptions()
    {
        var descriptions = new Dictionary<string, string>
        {
            { "Swift", "Summon four orbiting stars for 4s, damaging nearby enemies every 0.4s." },
            { "Tackle", "Dash a short distance in the chosen direction, damaging each enemy passed through once. Stops at walls." },
            { "HeatWave", "Ignite a mobile heat aura with eight pulses over about 6s. Burned enemies take increased pulse damage. Gain 20% movement speed for 6s. Upgrade: ten pulses with a shorter interval." },
            { "Takedown", "Rush to an enemy and launch them for 1.2s. The opening hit deals increased damage to burned enemies. Recast during the launch to slam the same enemy, briefly stun them and gain a 5% max-HP shield for 3s. Upgrade: launch duration increases to 1.4s." },
            { "Flamethrower", "Sweep a stream of fire across the chosen direction, damaging each enemy once and burning them for 3s. Become unstoppable during the cast. Upgrade: increased direct damage and a 10% max-HP shield for 4s." },
            { "FlareBlitz", "Ignite and charge in the chosen direction, briefly launching and burning enemies hit for 2s. Hitting an enemy reduces this move's remaining cooldown by 3s, once per cast. Stops at walls. Upgrade: burn lasts 3s." },
            { "Unite", "Leap toward an enemy and crash down in a fiery explosion. Become briefly invincible, damage and burn nearby enemies for 3s, then deal additional damage based on their missing HP. Gain a 12% max-HP shield for 3s." }
        };
        foreach (var pair in descriptions)
        {
            var asset = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Flareon/Flareon" + pair.Key + ".asset"));
            asset.FindProperty("description").stringValue = pair.Value; asset.ApplyModifiedPropertiesWithoutUndo();
        }
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Flareon.asset"));
        pokemon.FindProperty("description").stringValue = "A physical melee All-Rounder that ignites enemies and stays close to keep its combos burning. Every third connected basic attack is boosted, reduces both move cooldowns by 0.5s and refreshes Flareon's existing burns to at least 2s remaining. Flareon gains 5% Attack when directly damaging burned opposing players, up to three stacks; stacks expire together after 5s without a qualifying hit. Before evolving, taking damage below half HP grants 10% Attack for 5s. Burn deals source-scaled damage plus 1% target max HP each second, capped against non-player targets.";
        pokemon.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
