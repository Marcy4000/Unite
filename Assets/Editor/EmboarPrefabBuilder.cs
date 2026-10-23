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

// Like Duraludon: bake editable art once, preserve subsequent prefab edits, register exact runtime paths.
public static class EmboarPrefabBuilder
{
    private const string Art = EmboarMoveRules.Folder + "Art/", Materials = "Assets/Materials/Emboar/";
    private static Scene authoring;
    private static Mesh ring, shard, flame;
    private static Material orange, gold, white, coal, particles;
    private static AudioClip rush, impact;

    [MenuItem("Tools/Emboar/Create Missing Move Prefabs")]
    public static void Build()
    {
        Folder(Art.TrimEnd('/')); Folder(Materials.TrimEnd('/'));
        ring = Ring(); shard = Shard(); flame = Flame();
        orange = Material("FurnaceOrange", new Color(1, 0.16f, 0.015f));
        gold = Material("FlameGold", new Color(1, 0.63f, 0.045f));
        white = Material("WhiteHot", new Color(1, 0.91f, 0.57f));
        coal = Material("Charcoal", new Color(0.16f, 0.105f, 0.09f));
        particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        rush = Sound("FurnaceRush", false); impact = Sound("RingbreakerImpact", true);
        authoring = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (EmboarCast kind in Enum.GetValues(typeof(EmboarCast)))
                if (kind != EmboarCast.Impact) Create(kind);
        }
        finally { EditorSceneManager.ClosePreviewScene(authoring); authoring = default; }
        foreach (EmboarCast kind in Enum.GetValues(typeof(EmboarCast)))
            if (kind != EmboarCast.Impact) Register(EmboarMoveRules.Path(kind));
        AssetDatabase.SaveAssets();
        Debug.Log("Emboar move prefabs authored and registered; existing layouts preserved.");
    }
    [MenuItem("Tools/Emboar/Render Ringbreaker Preview")]
    public static void RenderPreview()
    {
        // A private preview scene: never replace or save the user's open scene.
        const string path = "Assets/Sprites/MoveIcons/Emboar/EmboarRingbreakerPreview.png";
        var preview = new PreviewRenderUtility();
        Texture2D image = null;
        try
        {
            var model = preview.InstantiatePrefabInScene(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Emboar/Emboar.prefab"));
            foreach (Animator animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Model/Pokemons/Emboar/Files/Animations/Fight_landing.anim");
            if (clip != null) clip.SampleAnimation(model, 0.15f);
            model.transform.rotation = Quaternion.Euler(0, 165, 0);
            var effect = preview.InstantiatePrefabInScene(AssetDatabase.LoadAssetAtPath<GameObject>(EmboarMoveRules.Path(EmboarCast.Unite)));
            effect.GetComponent<EmboarMoveObject>().enabled = false;
            var data = new SerializedObject(effect.GetComponent<EmboarMoveObject>());
            foreach (string field in new[] { "body", "telegraph", "waveFront" }) ((GameObject)data.FindProperty(field).objectReferenceValue).SetActive(false);
            var blast = (GameObject)data.FindProperty("burst").objectReferenceValue; blast.SetActive(true); blast.transform.localScale = Vector3.one * EmboarMoveRules.UniteRadius;
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>()) { system.Simulate(0.18f, true, true); system.Pause(); }
            preview.camera.orthographic = true; preview.camera.orthographicSize = 4.3f;
            preview.camera.transform.position = new Vector3(6.5f, 8, -9);
            preview.camera.transform.LookAt(Vector3.up * 0.5f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(0.075f, 0.055f, 0.05f);
            preview.camera.nearClipPlane = 0.1f; preview.camera.farClipPlane = 50;
            preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(50, -35, 0);
            preview.lights[1].intensity = 0.7f; preview.ambientColor = new Color(0.45f, 0.4f, 0.35f);
            preview.BeginStaticPreview(new Rect(0, 0, 720, 440)); preview.Render(true, false); image = preview.EndStaticPreview();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { if (image != null) Object.DestroyImmediate(image); preview.Cleanup(); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var old = settings.FindAssetEntry("e718c3faf3795284a8e373f43b1e1f5f");
        var group = old != null ? old.parentGroup : settings.DefaultGroup;
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path; EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var move = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Emboar/EmboarUnite.asset"));
        move.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(path); move.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets(); Debug.Log("Ringbreaker preview rendered and linked: " + path);
    }
    private static void Create(EmboarCast kind)
    {
        string path = EmboarMoveRules.Path(kind);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            WireMissingFront(path);
            return;
        }
        var root = Node("Emboar" + kind + "Object");
        root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
        var script = root.AddComponent<EmboarMoveObject>();
        var graphics = Node("Graphics", root.transform);
        var body = Node("FlameBody", graphics.transform);
        var blast = Node("Impact", graphics.transform);
        var warning = Node("LandingWarning", graphics.transform);
        Set(script, "graphics", graphics); Set(script, "body", body); Set(script, "burst", blast); Set(script, "telegraph", warning);
        AddFront(script, graphics.transform);
        var area = Node("AreaHitbox", root.transform).AddComponent<SphereCollider>();
        area.center = Vector3.up * 0.65f; area.radius = kind == EmboarCast.Unite ? EmboarMoveRules.UniteRadius : kind == EmboarCast.HeatCrash ? EmboarMoveRules.CrashRadius : 2;
        area.isTrigger = true; area.enabled = false; Set(script, "area", area);
        var box = Node("WaveHitbox", root.transform).AddComponent<BoxCollider>();
        box.center = kind == EmboarCast.Flamethrower ? new Vector3(0, 0.8f, 3) : Vector3.up * 0.8f;
        box.size = kind == EmboarCast.Flamethrower ? new Vector3(2.2f, 2, 6) : new Vector3(3.2f, 2, 2.2f);
        box.isTrigger = true; box.enabled = false; Set(script, "waveArea", box);
        float radius = kind == EmboarCast.Unite ? EmboarMoveRules.UniteRadius : EmboarMoveRules.CrashRadius;
        MeshNode("Boundary", warning.transform, ring, gold, Vector3.zero, Vector3.one * radius);
        MeshNode("InnerBoundary", warning.transform, ring, orange, Vector3.up * 0.01f, Vector3.one * (kind == EmboarCast.Unite ? EmboarMoveRules.UniteInnerRadius : radius * 0.8f));
        Impact(blast.transform, kind == EmboarCast.Unite);
        if (kind == EmboarCast.Ember)
        {
            MeshNode("EmberCore", body.transform, shard, white, Vector3.zero, Vector3.one * 0.26f);
            MeshNode("FireShell", body.transform, shard, orange, Vector3.zero, new Vector3(0.35f, 0.35f, 0.6f));
            Sparks(body.transform, 0.12f, 35, 0.06f);
        }
        else if (kind == EmboarCast.Flamethrower)
        {
            MeshNode("TurbulentFlame", body.transform, flame, orange, new Vector3(0, 1, 0.1f), Vector3.one);
            MeshNode("WhiteHotCore", body.transform, flame, white, new Vector3(0, 1.02f, 0.15f), new Vector3(0.4f, 0.45f, 0.85f));
            for (int i = 0; i < 7; i++)
                MeshNode("FlameTongue" + i, body.transform, shard, i % 2 == 0 ? gold : orange,
                    new Vector3(Mathf.Sin(i * 2.4f) * (0.18f + i * 0.09f), 1 + Mathf.Cos(i) * 0.18f, 1 + i * 0.7f), new Vector3(0.2f, 0.3f, 0.75f));
        }
        else if (kind == EmboarCast.Smog)
        {
            MeshNode("CloudBoundary", body.transform, ring, coal, Vector3.up * 0.06f, Vector3.one * 2);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                MeshNode("SootPuff" + i, body.transform, shard, coal, new Vector3(Mathf.Sin(a) * 1.25f, 0.55f, Mathf.Cos(a) * 1.25f), new Vector3(0.5f, 0.7f, 0.5f));
            }
            Sparks(body.transform, 1.4f, 16, 0.15f, true);
        }
        else
        {
            bool reckless = kind == EmboarCast.Reckless;
            for (int i = 0; i < (reckless ? 6 : 8); i++)
            {
                float a = i * Mathf.PI * 2 / (reckless ? 6 : 8);
                MeshNode("FurnaceFlare" + i, body.transform, shard, i % 2 == 0 ? orange : gold,
                    new Vector3(Mathf.Sin(a) * 0.7f, reckless ? 1.55f : 0.8f, Mathf.Cos(a) * 0.7f), new Vector3(0.12f, reckless ? 0.3f : 0.5f, 0.12f));
            }
            MeshNode("HeatRing", body.transform, ring, orange, Vector3.up * 0.1f, Vector3.one * 0.85f);
            Sparks(body.transform, 0.65f, reckless ? 14 : 30, 0.055f);
        }
        if (kind == EmboarCast.FirePledge)
        {
            var serialized = new SerializedObject(script);
            var pips = serialized.FindProperty("pledgePips"); pips.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var pip = MeshNode("PledgeCharge" + (i + 1), body.transform, shard, i == 2 ? white : gold, new Vector3((i - 1) * 0.28f, 2.2f, 0), new Vector3(0.12f, 0.17f, 0.12f));
                pips.GetArrayElementAtIndex(i).objectReferenceValue = pip;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        var audio = graphics.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 1; audio.volume = kind == EmboarCast.Unite ? 0.5f : 0.22f;
        audio.minDistance = 3; audio.maxDistance = 22; audio.rolloffMode = AudioRolloffMode.Linear;
        Set(script, "effectAudio", audio); Set(script, "rushSound", rush); Set(script, "impactSound", impact);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }
    private static void Impact(Transform parent, bool unite)
    {
        MeshNode("FireShockwave", parent, ring, orange, Vector3.up * 0.08f, Vector3.one);
        MeshNode("HotInnerWave", parent, ring, gold, Vector3.up * 0.13f, Vector3.one * 0.74f);
        for (int i = 0; i < (unite ? 12 : 8); i++)
        {
            float a = i * Mathf.PI * 2 / (unite ? 12 : 8);
            var crack = MeshNode("RadialFracture" + i, parent, shard, i % 3 == 0 ? coal : orange,
                new Vector3(Mathf.Sin(a) * 0.65f, 0.07f, Mathf.Cos(a) * 0.65f), new Vector3(0.055f, 0.08f, 0.38f));
            crack.transform.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            MeshNode("Debris" + i, parent, shard, coal, new Vector3(Mathf.Sin(a) * 0.8f, 0.18f, Mathf.Cos(a) * 0.8f), new Vector3(0.06f, 0.13f, 0.08f));
        }
        Sparks(parent, 0.75f, unite ? 65 : 30, 0.045f);
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 };
        SceneManager.MoveGameObjectToScene(go, parent != null ? parent.gameObject.scene : authoring);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }
    private static void AddFront(EmboarMoveObject script, Transform parent)
    {
        var front = Node("ForwardFireFront", parent);
        MeshNode("GroundFire", front.transform, Front(), orange, Vector3.up * 0.06f, Vector3.one);
        for (int i = 0; i < 7; i++)
            MeshNode("WaveFlare" + i, front.transform, shard, i % 2 == 0 ? gold : orange, new Vector3((i - 3) / 7f, 0.25f, 0), new Vector3(0.055f, 0.3f, 0.22f));
        Set(script, "waveFront", front);
    }
    private static void WireMissingFront(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var script = root.GetComponent<EmboarMoveObject>(); var serialized = new SerializedObject(script);
            if (serialized.FindProperty("waveFront").objectReferenceValue != null) return;
            var graphics = (GameObject)serialized.FindProperty("graphics").objectReferenceValue;
            AddFront(script, graphics.transform); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent); go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go;
    }
    private static void Sparks(Transform parent, float radius, float rate, float size, bool smoke = false)
    {
        var system = Node(smoke ? "Soot" : "Embers", parent).AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = true; main.playOnAwake = true; main.startLifetime = smoke ? 0.8f : 0.35f;
        main.startSpeed = smoke ? 0.3f : 0.9f; main.startSize = size; main.startColor = smoke ? new Color(0.2f, 0.13f, 0.11f) : new Color(1, 0.4f, 0.05f);
        main.maxParticles = 70; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius; shape.rotation = new Vector3(90, 0, 0);
        var colors = system.colorOverLifetime; colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(main.startColor.color, 0), new GradientColorKey(main.startColor.color, 1) }, new[] { new GradientAlphaKey(0.85f, 0), new GradientAlphaKey(0, 1) });
        colors.color = gradient;
        system.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/RuinOrange.mat");
        if (source == null) throw new InvalidOperationException("Missing authored Duraludon material reference.");
        var material = new Material(source) { name = name }; material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Mesh Ring()
    {
        string path = Art + "FurnaceRing.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 64; i++)
        {
            float a = i * Mathf.PI / 32;
            vertices.Add(new Vector3(Mathf.Sin(a) * 0.97f, 0, Mathf.Cos(a) * 0.97f));
            vertices.Add(new Vector3(Mathf.Sin(a) * 1.03f, 0, Mathf.Cos(a) * 1.03f));
            if (i < 64) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        return Mesh(path, vertices.ToArray(), triangles.ToArray());
    }
    private static Mesh Shard()
    {
        string path = Art + "FurnaceShard.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        return Mesh(path, new[] { Vector3.up, Vector3.down * 0.5f, Vector3.left, Vector3.forward, Vector3.right, Vector3.back },
            new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 });
    }
    private static Mesh Flame()
    {
        string path = Art + "TaperedFlame.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        return Mesh(path, new[] { new Vector3(-0.2f, 0, 0), new Vector3(0.2f, 0, 0), new Vector3(0, 0.3f, 0),
            new Vector3(-1.1f, 0, 5.7f), new Vector3(1.1f, 0, 5.7f), new Vector3(0, 0.65f, 5.7f) },
            new[] { 0, 3, 4, 0, 4, 1, 0, 2, 5, 0, 5, 3, 1, 4, 5, 1, 5, 2, 3, 5, 4 });
    }
    private static Mesh Front()
    {
        string path = Art + "ShockwaveFront.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        return Mesh(path, new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) }, new[] { 0, 2, 1, 1, 2, 3 });
    }
    private static Mesh Mesh(string path, Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path), vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static AudioClip Sound(string name, bool heavy)
    {
        string path = Art + name + ".wav";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (existing != null) return existing;
        const int sampleRate = 22050; int count = (int)(sampleRate * (heavy ? 0.45f : 0.3f));
        var random = new System.Random(500); float filtered = 0;
        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate, p = i / (float)count;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, heavy ? 0.12f : 0.35f);
                float envelope = heavy ? Mathf.Exp(-p * 8) * Mathf.Min(1, p * 100) : Mathf.Sin(p * Mathf.PI) * (1 - p);
                float value = (filtered * 0.65f + (heavy ? Mathf.Sin(t * (80 - p * 35) * Mathf.PI * 2) * 0.35f : 0)) * envelope;
                writer.Write((short)(Mathf.Clamp(value, -1, 1) * 24000));
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
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue == 0) throw new InvalidOperationException("Missing spawn hash: " + path);
        prefab = PrefabUtility.SavePrefabAsset(prefab);
        var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("MovesAssets");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")); var entries = registry.FindProperty("List");
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) return;
        entries.InsertArrayElementAtIndex(entries.arraySize); var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        entry.FindPropertyRelative("Override").enumValueIndex = 0; entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null; entry.FindPropertyRelative("SourceHashToOverride").longValue = 0; entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        registry.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
