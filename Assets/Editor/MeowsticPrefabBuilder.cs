using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Duraludon-style authoring: assets are baked once, never constructed in a match.
// Existing prefabs/materials are preserved when the builder is run again.
public static class MeowsticPrefabBuilder
{
    private const string Folder = MeowsticMoveRules.VisualFolder;
    private const string Materials = "Assets/Materials/Meowstic/";
    private static Scene preview;
    private static Mesh ring, arc, cone, square, eyeRing, panel, canopyRib;
    private static Material lavender, cyan, pink, indigo, white, particle;

    [MenuItem("Tools/Meowstic/Create Missing Rework Prefabs")]
    public static void Build()
    {
        EnsureFolder(Folder.TrimEnd('/')); EnsureFolder(Folder + "Art"); EnsureFolder(Materials.TrimEnd('/'));
        ring = BakeArc("PsychicRing", 360, 0.018f);
        arc = BakeArc("ClawArc", 110, 0.09f);
        cone = BakeArc("LeerCone", 90, 0.3f);
        square = BakeSquare();
        eyeRing = BakeArc("EyeContour", 360, 0.14f);
        panel = BakePanel(); canopyRib = BakeCanopyRib();
        lavender = Material("Lavender", new Color(0.53f, 0.36f, 1));
        cyan = Material("ProtectiveCyan", new Color(0.25f, 0.9f, 1));
        pink = Material("ReflectedMagenta", new Color(1, 0.25f, 0.8f));
        indigo = Material("Indigo", new Color(0.15f, 0.13f, 0.4f));
        white = Material("PsychicCore", new Color(0.85f, 0.95f, 1));
        particle = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (MeowsticEffect effect in Enum.GetValues(typeof(MeowsticEffect))) Create(effect);
            CreateTestFixture();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("MovesAssets");
        foreach (MeowsticEffect effect in Enum.GetValues(typeof(MeowsticEffect)))
        {
            string path = Folder + effect + ".prefab";
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        }
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        Debug.Log("Meowstic authored effects registered. Existing layouts and legacy network prefabs preserved.");
    }

    [MenuItem("Tools/Meowstic/Render Effect Previews")]
    public static void RenderPreviews()
    {
        Directory.CreateDirectory("/tmp/opencode");
        foreach (MeowsticEffect effect in new[] { MeowsticEffect.Reflect, MeowsticEffect.Psychic, MeowsticEffect.WonderRoom, MeowsticEffect.Unite })
        {
            var renderer = new PreviewRenderUtility(); Texture2D image = null;
            try
            {
                var model = renderer.InstantiatePrefabInScene(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Meowstic/Meowstic(male).prefab"));
                foreach (Animator animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
                var instance = renderer.InstantiatePrefabInScene(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + effect + ".prefab"));
                instance.GetComponent<MeowsticVisual>().enabled = false;
                float size = effect == MeowsticEffect.Reflect ? 1 : effect == MeowsticEffect.Psychic ? MeowsticMoveRules.PsychicRadius : effect == MeowsticEffect.WonderRoom ? MeowsticMoveRules.RoomHalfSize : MeowsticMoveRules.UniteRadius;
                instance.transform.localScale = new Vector3(size, 1, size);
                if (effect == MeowsticEffect.Unite) model.transform.localPosition = Vector3.up * 0.3f;
                foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>()) { ps.Simulate(0.3f, true, true); ps.Pause(); }
                renderer.camera.orthographic = true; renderer.camera.orthographicSize = Mathf.Max(2.5f, size * 1.25f);
                renderer.camera.transform.position = new Vector3(7, 10, -9); renderer.camera.transform.LookAt(Vector3.up * 0.5f);
                renderer.camera.clearFlags = CameraClearFlags.SolidColor; renderer.camera.backgroundColor = new Color(0.04f, 0.055f, 0.1f);
                renderer.camera.nearClipPlane = 0.1f; renderer.camera.farClipPlane = 50;
                renderer.lights[0].intensity = 1.4f; renderer.lights[0].transform.rotation = Quaternion.Euler(50, -35, 0); renderer.lights[1].intensity = 0.7f;
                renderer.ambientColor = new Color(0.45f, 0.45f, 0.55f);
                renderer.BeginStaticPreview(new Rect(0, 0, 900, 650)); renderer.Render(true, false); image = renderer.EndStaticPreview();
                File.WriteAllBytes("/tmp/opencode/meowstic-" + effect + ".png", image.EncodeToPNG());
            }
            finally { if (image != null) Object.DestroyImmediate(image); renderer.Cleanup(); }
        }
        Debug.Log("Meowstic effect previews saved under /tmp/opencode/meowstic-*.png");
    }

    private static void Create(MeowsticEffect effect)
    {
        string path = Folder + effect + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { UpgradeReadability(path, effect); return; }
        var root = Node(effect.ToString()); var view = root.AddComponent<MeowsticVisual>();
        var graphics = Node("Graphics", root.transform);
        Set(view, "graphics", graphics);
        var motion = Node("Orbit", graphics.transform);
        bool orbit = true;
        switch (effect)
        {
            case MeowsticEffect.Basic:
            case MeowsticEffect.Boosted:
            case MeowsticEffect.Reflection:
                var color = effect == MeowsticEffect.Reflection ? pink : effect == MeowsticEffect.Boosted ? cyan : lavender;
                Primitive("PsychicCore", motion.transform, PrimitiveType.Sphere, white, Vector3.zero, Vector3.one * (effect == MeowsticEffect.Basic ? 0.15f : 0.24f));
                for (int i = 0; i < 2; i++)
                {
                    var hoop = MeshNode("Orbit" + i, motion.transform, ring, color, Vector3.zero, Vector3.one * (0.2f + i * 0.08f));
                    hoop.transform.localRotation = Quaternion.Euler(45 + i * 70, 30, i * 90);
                }
                Sparks(motion.transform, color.color, 0.12f, 18);
                break;
            case MeowsticEffect.Scratch:
                orbit = false; Set(view, "wave", motion.transform);
                for (int i = 0; i < 2; i++)
                    MeshNode("ClawSwipe" + i, motion.transform, arc, i == 0 ? lavender : white, new Vector3(0, 0.55f + i * 0.2f, 0), new Vector3(2.5f, 1, 2.5f));
                break;
            case MeowsticEffect.Leer:
                orbit = false; Set(view, "wave", motion.transform);
                MeshNode("ConeEdge", motion.transform, cone, pink, Vector3.up * 0.1f, new Vector3(3, 1, 3));
                Eye(motion.transform, new Vector3(0, 1, 0.5f), 0.6f, pink);
                break;
            case MeowsticEffect.Reflect:
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3;
                    var frame = MeshNode("ReflectPanel" + i, motion.transform, panel, i % 2 == 0 ? cyan : lavender,
                        new Vector3(Mathf.Sin(a) * 0.65f, 0.8f, Mathf.Cos(a) * 0.65f), new Vector3(0.3f, 0.65f, 0.035f));
                    frame.transform.localRotation = Quaternion.Euler(15, i * 60, 15);
                }
                var storage = new GameObject[8];
                for (int i = 0; i < storage.Length; i++) storage[i] = Primitive("StoredDamage" + i, graphics.transform, PrimitiveType.Sphere, pink, new Vector3((i - 3.5f) * 0.13f, 1.75f, 0), Vector3.one * 0.09f);
                Array(view, "storagePips", storage);
                Set(view, "timer", Primitive("DurationBar", graphics.transform, PrimitiveType.Cube, cyan, new Vector3(0, 1.95f, 0), new Vector3(1, 0.05f, 0.05f)).transform);
                break;
            case MeowsticEffect.Detonation:
            case MeowsticEffect.Pulse:
                orbit = false; Set(view, "wave", motion.transform);
                MeshNode("ExpandingWave", motion.transform, ring, effect == MeowsticEffect.Detonation ? pink : cyan, Vector3.up * 0.15f, Vector3.one);
                MeshNode("InnerWave", motion.transform, ring, white, Vector3.up * 0.18f, Vector3.one * 0.85f);
                break;
            case MeowsticEffect.MagicCoat:
            case MeowsticEffect.Psychic:
                MeshNode("AreaBoundary", graphics.transform, ring, effect == MeowsticEffect.MagicCoat ? pink : lavender, Vector3.up * 0.08f, Vector3.one);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2;
                    Eye(motion.transform, new Vector3(Mathf.Sin(a) * 0.7f, 0.8f, Mathf.Cos(a) * 0.7f), 0.13f, effect == MeowsticEffect.MagicCoat ? pink : cyan);
                }
                Sparks(graphics.transform, effect == MeowsticEffect.MagicCoat ? new Color(1, 0.3f, 0.8f) : new Color(0.4f, 0.7f, 1), 1, 18);
                break;
            case MeowsticEffect.WonderRoom:
            case MeowsticEffect.RoomAim:
                orbit = false;
                MeshNode("SquareBoundary", graphics.transform, square, effect == MeowsticEffect.RoomAim ? cyan : lavender, Vector3.up * 0.08f, Vector3.one);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = new Vector3(i % 2 == 0 ? -1 : 1, 0.2f, i < 2 ? -1 : 1);
                    Eye(graphics.transform, corner, 0.09f, white);
                    if (effect != MeowsticEffect.RoomAim) Primitive("CornerPillar" + i, graphics.transform, PrimitiveType.Cube, indigo, corner, new Vector3(0.025f, 0.5f, 0.025f));
                }
                if (effect == MeowsticEffect.WonderRoom)
                {
                    MeshNode("InnerSquare", graphics.transform, square, indigo, Vector3.up * 0.1f, Vector3.one * 0.9f);
                    Eye(graphics.transform, Vector3.up * 0.15f, 0.18f, pink);
                }
                break;
            case MeowsticEffect.Unite:
                MeshNode("HarmonyBoundary", graphics.transform, ring, cyan, Vector3.up * 0.1f, Vector3.one);
                MeshNode("InnerHarmony", graphics.transform, ring, lavender, Vector3.up * 0.12f, Vector3.one * 0.93f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    Eye(motion.transform, new Vector3(Mathf.Sin(a) * 0.95f, 1.5f, Mathf.Cos(a) * 0.95f), 0.07f, i % 2 == 0 ? cyan : lavender);
                }
                MeshNode("OpenCanopy", motion.transform, ring, lavender, Vector3.up * 1.7f, Vector3.one * 0.8f);
                CanopyRibs(motion.transform);
                Sparks(graphics.transform, new Color(0.4f, 0.8f, 1), 1, 30);
                break;
            case MeowsticEffect.State:
                orbit = false;
                var enemies = new GameObject[5];
                for (int i = 0; i < enemies.Length; i++) enemies[i] = Primitive("EnemyPip" + i, graphics.transform, PrimitiveType.Sphere, lavender, new Vector3((i - 2) * 0.13f, 2.3f, 0), Vector3.one * 0.06f);
                Array(view, "enemyPips", enemies);
                break;
            case MeowsticEffect.Inversion:
                Eye(motion.transform, Vector3.up * 1.7f, 0.2f, pink);
                MeshNode("InvertedStats", motion.transform, arc, lavender, Vector3.up * 1.7f, Vector3.one * 0.4f);
                break;
        }
        if (orbit) Set(view, "motion", motion.transform);
        if (effect != MeowsticEffect.State && effect != MeowsticEffect.RoomAim && effect != MeowsticEffect.Basic && effect != MeowsticEffect.Pulse && effect != MeowsticEffect.Inversion)
        {
            var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 1; audio.minDistance = 3; audio.maxDistance = 18; audio.volume = 0.15f;
            audio.clip = Sound(effect == MeowsticEffect.Reflection ? "Reflection" : effect == MeowsticEffect.Detonation ? "Detonation" : "Cast");
            Set(view, "sound", audio);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static void CreateTestFixture()
    {
        const string folder = "Assets/Editor/MeowsticTests/Fixtures";
        const string path = folder + "/Pokemon.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        EnsureFolder(folder);
        var root = Node("MeowsticTestPokemon"); root.AddComponent<NetworkObject>(); root.AddComponent<Pokemon>();
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        // NGO requires a persistent, nonzero prefab hash, including test actors.
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        PrefabUtility.SavePrefabAsset(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    }
    private static void UpgradeReadability(string path, MeowsticEffect effect)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path); bool changed = false;
        try
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name == "EyeContour" && filter.sharedMesh == ring) { filter.sharedMesh = eyeRing; changed = true; }
                if (filter.name.StartsWith("ReflectPanel", StringComparison.Ordinal) && filter.sharedMesh != null && filter.sharedMesh.name == "Cube") { filter.sharedMesh = panel; changed = true; }
            }
            Transform orbit = root.transform.Find("Graphics/Orbit");
            if (effect == MeowsticEffect.Unite && orbit != null && orbit.Find("CanopyRib0") == null) { CanopyRibs(orbit); changed = true; }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void CanopyRibs(Transform parent)
    {
        for (int i = 0; i < 6; i++)
        {
            var rib = MeshNode("CanopyRib" + i, parent, canopyRib, i % 2 == 0 ? lavender : cyan, Vector3.up * 0.1f, Vector3.one);
            rib.transform.localRotation = Quaternion.Euler(0, i * 60, 0);
        }
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 }; SceneManager.MoveGameObjectToScene(go, parent != null ? parent.gameObject.scene : preview);
        if (parent != null) go.transform.SetParent(parent, false); return go;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent); go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; Configure(go.AddComponent<MeshRenderer>(), material); return go;
    }
    private static GameObject Primitive(string name, Transform parent, PrimitiveType primitive, Material material, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(primitive); go.name = name; go.layer = 8; SceneManager.MoveGameObjectToScene(go, preview);
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>()); Configure(go.GetComponent<MeshRenderer>(), material); return go;
    }
    private static void Configure(Renderer renderer, Material material) { renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
    private static void Eye(Transform parent, Vector3 position, float scale, Material color)
    {
        MeshNode("EyeContour", parent, eyeRing, color, position, new Vector3(scale * 1.7f, 1, scale));
        Primitive("EyeCore", parent, PrimitiveType.Sphere, white, position, new Vector3(scale * 0.4f, scale * 0.4f, scale * 0.7f));
    }
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var go = Node("PsychicMotes", parent); var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.startLifetime = 0.5f; main.startSpeed = 0.3f; main.startSize = 0.045f; main.startColor = color;
        main.maxParticles = 60; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius; shape.radiusThickness = 0.03f; shape.rotation = new Vector3(90, 0, 0);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particle;
    }
    private static Mesh BakeArc(string name, float degrees, float thickness)
    {
        string path = Folder + "Art/" + name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        const int segments = 64; var vertices = new Vector3[(segments + 1) * 2]; var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-degrees / 2, degrees / 2, i / (float)segments) * Mathf.Deg2Rad;
            Vector3 p = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)); vertices[i * 2] = p * (1 - thickness); vertices[i * 2 + 1] = p;
            if (i < segments) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        return SaveMesh(path, name, vertices, triangles.ToArray());
    }
    private static Mesh BakeSquare()
    {
        string path = Folder + "Art/RoomBoundary.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new Vector3[16]; var triangles = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = Quaternion.Euler(0, i * 90, 0) * new Vector3(-1, 0, 1), b = Quaternion.Euler(0, i * 90, 0) * new Vector3(1, 0, 1);
            int n = i * 4; vertices[n] = a; vertices[n + 1] = b; vertices[n + 2] = a * 0.984f; vertices[n + 3] = b * 0.984f;
            triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 });
        }
        return SaveMesh(path, "RoomBoundary", vertices, triangles.ToArray());
    }
    private static Mesh SaveMesh(string path, string name, Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static Mesh BakePanel()
    {
        string path = Folder + "Art/ReflectFrame.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var corners = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
        var vertices = new Vector3[8]; var triangles = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            int n = i * 2, next = ((i + 1) % 4) * 2; vertices[n] = corners[i]; vertices[n + 1] = corners[i] * 0.72f;
            triangles.AddRange(new[] { n, next, n + 1, next, next + 1, n + 1 });
        }
        return SaveMesh(path, "ReflectFrame", vertices, triangles.ToArray());
    }
    private static Mesh BakeCanopyRib()
    {
        string path = Folder + "Art/CanopyRib.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        const int segments = 24; var vertices = new Vector3[(segments + 1) * 2]; var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 0.5f;
            Vector3 position = new Vector3(0, Mathf.Sin(a) * 1.6f, Mathf.Cos(a));
            vertices[i * 2] = position + Vector3.left * 0.004f; vertices[i * 2 + 1] = position + Vector3.right * 0.004f;
            if (i < segments) { int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        }
        return SaveMesh(path, "CanopyRib", vertices, triangles.ToArray());
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat"; var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/WhiteHot.mat");
        var material = new Material(source) { name = name }; material.SetColor("_BaseColor", color); material.SetFloat("_Cull", 0); AssetDatabase.CreateAsset(material, path); return material;
    }
    private static AudioClip Sound(string name)
    {
        string path = Folder + "Art/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int rate = 22050; int count = (int)(rate * (name == "Detonation" ? 0.25f : 0.14f));
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float progress = i / (float)count; float frequency = name == "Reflection" ? 850 : name == "Detonation" ? 170 : 550;
                    float sample = Mathf.Sin(2 * Mathf.PI * frequency * i / rate) + 0.3f * Mathf.Sin(2 * Mathf.PI * frequency * 1.5f * i / rate);
                    writer.Write((short)(sample * Mathf.Sin(progress * Mathf.PI) * (1 - progress) * 14000));
                }
                File.WriteAllBytes(path, stream.ToArray());
            }
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Array(Object target, string field, GameObject[] values)
    {
        var serialized = new SerializedObject(target); var list = serialized.FindProperty(field); list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
