using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Same authored-asset workflow as Duraludon. Re-running preserves hand edits.
// Cosmetics use RPCs + local Addressables: no per-particle NetworkObjects and
// no runtime mesh generation. Gameplay is independent of visual load latency.
public static class MarshadowPrefabBuilder
{
    private const string Folder = MarshadowMoveRules.EffectFolder;
    private const string Materials = "Assets/Materials/Marshadow/";
    private static Scene preview;
    private static Mesh ring, arc;
    private static Material shadow, violet, green, white, particle;

    [MenuItem("Tools/Marshadow/Create Missing Rework Prefabs")]
    public static void Build()
    {
        ConfigureActionAnimationSpeed();
        EnsureFolder(Folder.TrimEnd('/')); EnsureFolder(Folder + "Art"); EnsureFolder(Materials.TrimEnd('/'));
        ring = BakeArc("ShadowRing", 360, 0.035f); arc = BakeArc("StrikeArc", 135, 0.14f);
        shadow = Material("Shadow", new Color(0.085f, 0.055f, 0.14f));
        violet = Material("SpectralViolet", new Color(0.55f, 0.12f, 0.9f));
        green = Material("ZenithGreen", new Color(0.26f, 1f, 0.48f));
        white = Material("ImpactCore", new Color(0.85f, 0.95f, 1f));
        particle = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        preview = EditorSceneManager.NewPreviewScene();
        try { foreach (MarshadowEffect effect in Enum.GetValues(typeof(MarshadowEffect))) Create(effect); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (MarshadowEffect effect in Enum.GetValues(typeof(MarshadowEffect)))
        {
            string path = MarshadowMoveRules.EffectPath(effect);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("MovesAssets");
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
            EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Marshadow authored effects registered. Existing prefab edits and legacy network prefabs preserved.");
    }
    private static void Create(MarshadowEffect effect)
    {
        string path = MarshadowMoveRules.EffectPath(effect);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Node(effect.ToString());
        var view = root.AddComponent<MarshadowVisual>();
        var graphics = Node("Graphics", root.transform);
        var accent = Node("Accent", graphics.transform);
        Set(view, "graphics", graphics); Set(view, "accent", accent.transform);
        switch (effect)
        {
            case MarshadowEffect.Punch:
            case MarshadowEffect.BoostedPunch:
            case MarshadowEffect.DrainPunch:
                bool heavy = effect != MarshadowEffect.Punch;
                MeshNode("ContactArc", accent.transform, arc, heavy ? violet : white, new Vector3(0, 0.65f, 1.35f), new Vector3(0.8f, 1, 0.8f));
                Fist(accent.transform, new Vector3(0, 0.75f, 1.5f), heavy ? 0.45f : 0.28f, heavy ? violet : shadow);
                Sparks(accent.transform, effect == MarshadowEffect.DrainPunch ? new Color(0.25f, 1f, 0.5f) : new Color(0.65f, 0.2f, 1f), 0.6f, 25);
                break;
            case MarshadowEffect.ShadowSneak:
            case MarshadowEffect.PhantomForce:
                MeshNode("ShadowBoundary", accent.transform, ring, shadow, Vector3.up * 0.06f, new Vector3(0.85f, 1, 0.6f));
                MeshNode("GhostEdge", accent.transform, arc, violet, Vector3.up * 0.08f, new Vector3(0.9f, 1, 0.65f));
                Sparks(accent.transform, new Color(0.35f, 0.1f, 0.55f), 0.6f, 12);
                break;
            case MarshadowEffect.AuraSphere:
                Primitive("Core", accent.transform, PrimitiveType.Sphere, green, Vector3.zero, Vector3.one * 0.3f);
                for (int i = 0; i < 3; i++)
                {
                    var shell = MeshNode("ShadowShell", accent.transform, ring, violet, Vector3.zero, Vector3.one * (0.3f + 0.06f * i));
                    shell.transform.localRotation = Quaternion.Euler(i * 60f, i * 45f, 90);
                }
                Trail(accent.transform); break;
            case MarshadowEffect.AuraMark:
                MeshNode("PursuitRing", accent.transform, ring, violet, Vector3.up * 1.8f, Vector3.one * 0.45f);
                for (int i = 0; i < 3; i++) Primitive("PursuitPip", accent.transform, PrimitiveType.Sphere, green,
                    new Vector3(Mathf.Sin(i * 2.094f) * 0.45f, 1.8f, Mathf.Cos(i * 2.094f) * 0.45f), Vector3.one * 0.12f);
                break;
            case MarshadowEffect.Rush:
                Fist(accent.transform, new Vector3(0, 0.85f, 0.85f), 0.45f, violet);
                MeshNode("RushArc", accent.transform, arc, green, new Vector3(0, 0.5f, 0.8f), Vector3.one);
                Trail(accent.transform); Sparks(accent.transform, new Color(0.5f, 0.1f, 0.9f), 0.7f, 20); break;
            case MarshadowEffect.SpectralThief:
                MeshNode("DamageBoundary", accent.transform, ring, violet, Vector3.up * 0.12f, Vector3.one * MarshadowMoveRules.SpectralRadius);
                MeshNode("TheftSweetSpot", accent.transform, ring, green, Vector3.up * 0.14f, Vector3.one * MarshadowMoveRules.TheftRadius);
                Sparks(accent.transform, new Color(0.5f, 0.15f, 0.9f), MarshadowMoveRules.SpectralRadius, 25); break;
            case MarshadowEffect.EchoBurst:
                MeshNode("BurstBoundary", accent.transform, ring, violet, Vector3.up * 0.15f, Vector3.one);
                MeshNode("BurstCore", accent.transform, arc, green, Vector3.up * 0.2f, Vector3.one * 0.9f);
                Sparks(accent.transform, new Color(0.4f, 1f, 0.6f), 1f, 30); break;
            case MarshadowEffect.EchoField:
                MeshNode("FieldBoundary", accent.transform, ring, violet, Vector3.up * 0.1f, Vector3.one * 5f);
                Sparks(accent.transform, new Color(0.4f, 0.3f, 0.9f), 5f, 35); break;
            case MarshadowEffect.Theft:
                for (int i = 0; i < 3; i++) Primitive("StolenEnergy", accent.transform, PrimitiveType.Sphere, green,
                    new Vector3((i - 1) * 0.22f, i * 0.15f, 0), Vector3.one * 0.17f);
                Trail(accent.transform); break;
            case MarshadowEffect.Kick:
                MeshNode("KickSweep", accent.transform, arc, violet, Vector3.up * 0.65f, new Vector3(2.1f, 1, 2.1f));
                MeshNode("KickCore", accent.transform, arc, white, Vector3.up * 0.7f, new Vector3(1.8f, 1, 1.8f)); break;
            case MarshadowEffect.WallImpact:
                MeshNode("WallBurst", accent.transform, ring, green, Vector3.up, Vector3.one);
                Sparks(accent.transform, new Color(0.4f, 1f, 0.6f), 0.8f, 60); break;
            case MarshadowEffect.MirrorShadow:
                for (int i = 0; i < 3; i++)
                {
                    var hoop = MeshNode("GatheringShadow", accent.transform, ring, i == 2 ? green : violet,
                        Vector3.up * (0.5f + i * 0.3f), Vector3.one * (0.7f + i * 0.15f));
                    hoop.transform.localRotation = Quaternion.Euler(i * 35f, 0, i * 25f);
                }
                Sparks(accent.transform, new Color(0.5f, 0.2f, 1f), 0.85f, 30); break;
            case MarshadowEffect.Passive:
                var serialized = new SerializedObject(view);
                var pips = serialized.FindProperty("pips"); pips.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var pip = Primitive("Stack" + (i + 1), accent.transform, PrimitiveType.Sphere, green,
                        new Vector3((i - 1) * 0.22f, 1.75f, 0), Vector3.one * 0.13f);
                    pips.GetArrayElementAtIndex(i).objectReferenceValue = pip;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo(); break;
        }
        if (effect == MarshadowEffect.DrainPunch || effect == MarshadowEffect.Kick)
        {
            var timing = new SerializedObject(view);
            timing.FindProperty("impactDelay").floatValue = effect == MarshadowEffect.DrainPunch ? 0.25f : 0.28f;
            timing.ApplyModifiedPropertiesWithoutUndo();
        }
        if (effect != MarshadowEffect.Passive && effect != MarshadowEffect.AuraMark)
        {
            var sound = root.AddComponent<AudioSource>(); sound.playOnAwake = false;
            sound.spatialBlend = 1f; sound.minDistance = 2; sound.maxDistance = 18; sound.volume = 0.22f;
            bool soft = effect == MarshadowEffect.ShadowSneak || effect == MarshadowEffect.PhantomForce || effect == MarshadowEffect.MirrorShadow;
            sound.clip = Sound(soft ? "ShadowGather" : effect == MarshadowEffect.AuraSphere || effect == MarshadowEffect.Theft ? "SpectralWisp" : "ShadowImpact");
            Set(view, "sound", sound);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }
    private static GameObject Node(string name, Transform parent = null)
    {
        var go = new GameObject(name) { layer = 8 };
        SceneManager.MoveGameObjectToScene(go, preview);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }
    private static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material mat, Vector3 position, Vector3 scale)
    {
        var go = Node(name, parent); go.transform.localPosition = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go;
    }
    private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Material mat, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 8;
        SceneManager.MoveGameObjectToScene(go, preview); go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go;
    }
    private static void Fist(Transform parent, Vector3 position, float size, Material mat)
    {
        Primitive("SpectralFist", parent, PrimitiveType.Sphere, mat, position, new Vector3(size * 1.4f, size, size));
        for (int i = 0; i < 3; i++) Primitive("Knuckle", parent, PrimitiveType.Sphere, white,
            position + new Vector3((i - 1) * size * 0.28f, size * 0.1f, size * 0.4f), Vector3.one * size * 0.32f);
    }
    private static void Sparks(Transform parent, Color color, float radius, float rate)
    {
        var go = Node("Wisps", parent); var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.startColor = color; main.startLifetime = 0.25f;
        main.startSpeed = 0.5f; main.startSize = 0.07f; main.maxParticles = 40; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius;
        shape.radiusThickness = 0.08f; shape.rotation = new Vector3(90, 0, 0);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particle;
    }
    private static void Trail(Transform parent)
    {
        var go = Node("ShadowTrail", parent); go.transform.localPosition = Vector3.up * 0.65f;
        var trail = go.AddComponent<TrailRenderer>(); trail.sharedMaterial = violet;
        trail.time = 0.16f; trail.startWidth = 0.3f; trail.endWidth = 0; trail.minVertexDistance = 0.08f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
    }
    private static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Duraludon/DragonEnergy.mat");
        var result = new Material(source) { name = name }; result.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(result, path); return result;
    }
    private static Mesh BakeArc(string name, float degrees, float width)
    {
        string path = Folder + "Art/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 64; i++)
        {
            float angle = (i / 64f - 0.5f) * degrees * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            vertices.Add(d * (1 - width)); vertices.Add(d);
            if (i < 64) { int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 }); }
        }
        var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    // Short authored placeholder sounds, baked once in the editor as real WAV
    // assets. No synthetic audio or allocation occurs during combat.
    private static AudioClip Sound(string name)
    {
        string path = Folder + "Art/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int rate = 22050;
            int count = (int)(rate * (name == "ShadowGather" ? 0.55f : 0.18f));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                var random = new System.Random(883);
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)count;
                    float envelope = Mathf.Sin(Mathf.PI * t) * Mathf.Exp(-t * (name == "ShadowImpact" ? 8f : 3f));
                    double frequency = name == "ShadowGather" ? 180 + 400 * t : name == "SpectralWisp" ? 850 - 650 * t : 100 - 60 * t;
                    double sample = Math.Sin(i * frequency * 2 * Math.PI / rate) * 0.5 + (random.NextDouble() - 0.5) * 0.35;
                    writer.Write((short)(sample * envelope * 22000));
                }
                File.WriteAllBytes(path, stream.ToArray());
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void ConfigureActionAnimationSpeed()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Marshadow/Marshadow.controller");
        const string parameter = "MarshadowActionSpeed";
        bool exists = false;
        foreach (var item in controller.parameters) if (item.name == parameter) exists = true;
        if (!exists) controller.AddParameter(new AnimatorControllerParameter { name = parameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            if (child.state.name != "pm0883_ba20_buturi01" && child.state.name != "pm0883_ba20_buturi02"
                && child.state.name != "pm0883_ba21_tokusyu01" && child.state.name != "pm0883_ba21_tokusyu02") continue;
            child.state.speedParameter = parameter; child.state.speedParameterActive = true;
            EditorUtility.SetDirty(child.state);
        }
        EditorUtility.SetDirty(controller);
    }
}
